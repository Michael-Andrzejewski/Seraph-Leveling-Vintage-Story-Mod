using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling
{
    public partial class SeraphLevelingModSystem
    {

        // Keys for ranged damage progression system
        public const string RANGED_DAMAGE_KEY = "sitRangedDamage";
        public const string RANGED_DAMAGE_STAT_CODE = "sitRangedDamageBonus";
        public const string RANGED_ACCURACY_STAT_CODE = "sitRangedAccuracyBonus";
        public const string RANGED_DISTANCE_STAT_CODE = "sitRangedDistanceBonus";
        private const string RANGED_PROGRESS_SAVE_KEY = "sitRangedProgress";

        // WatchedAttributes keys for client sync (ranged)
        public const string WATCHED_RANGED_LEVEL = "sitRangedLevel";
        public const string WATCHED_RANGED_DAMAGE_BONUS = "sitRangedDamageBonusPercent";
        public const string WATCHED_RANGED_ACCURACY_BONUS = "sitRangedAccuracyBonusPercent";
        public const string WATCHED_RANGED_DISTANCE_BONUS = "sitRangedDistanceBonusPercent";

        // Trait code for the ranged mastery trait (Focused)
        public const string RANGED_TRAIT_CODE = "sitrangedmastery";

        // Ranged damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon combinations resets the increment counter back to base
        public static int BaseRangedDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int RangedIncrementStep = 100;             // How much more damage each subsequent credit needs
        public static int MaxRangedDamagePercent = 50;           // 50% max bonus for damage
        public static int MaxRangedAccuracyPercent = 50;         // 50% max bonus for accuracy
        public static int MaxRangedDistancePercent = 50;         // 50% max bonus for distance

        // Vanilla Focused trait bonuses (used for cap calculations)
        public const int VANILLA_FOCUSED_DAMAGE_BONUS = 20;
        public const int VANILLA_FOCUSED_ACCURACY_BONUS = 30;
        public const int VANILLA_FOCUSED_DISTANCE_BONUS = 20;

        // Storage for ranged progress - keyed by player UID
        public static ConcurrentDictionary<string, RangedProgressData> RangedProgress = new ConcurrentDictionary<string, RangedProgressData>();

        // Flag to indicate pending ranged progress save
        private static volatile bool pendingRangedProgressSave = false;

        /// <summary>
        /// Handler for /trait ranged command.
        /// </summary>
        private TextCommandResult OnTraitRangedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            int currentCredits = progress.TotalCredits;
            var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(currentCredits, player.Entity as EntityPlayer);
            int maxCredits = GetMaxRangedCredits(player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Ranged progression: {currentCredits} credits / {maxCredits} max");
            sb.AppendLine($"Current bonuses: +{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    weaponName = weaponName.Replace("game:", "");

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal ranged damage with bows or slings to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait rangedbase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitRangedBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage per increment must be at least 1");
                }

                BaseRangedDamagePerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base ranged damage per increment set to {BaseRangedDamagePerIncrement}. New weapons will require this much damage for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base ranged damage per increment: {BaseRangedDamagePerIncrement}\nIncrement step: +{RangedIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait rangedincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitRangedIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                RangedIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Ranged increment step set to +{RangedIncrementStep} per credit.\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current ranged increment step: +{RangedIncrementStep} per credit\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait rangedlevel command.
        /// Gets or sets the player's ranged credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitRangedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = OptInt(args, 0);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int maxCredits = GetMaxRangedCredits(player.Entity);
                var progress = RangedProgress.GetOrAdd(player.PlayerUID, _ => new RangedProgressData());
                var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current ranged level: {progress.TotalCredits}/{maxCredits} (+{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance)");
            }

            string toolName = (string)args[1];
            return SetRangedLevelForPlayer(player, player.PlayerUID, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait rangedmax command.
        /// Gets or sets the maximum ranged damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged damage percent must be at least 1");
                }

                MaxRangedDamagePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged damage bonus set to +{MaxRangedDamagePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged damage bonus: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxacc command.
        /// Gets or sets the maximum ranged accuracy bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxAccuracyCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged accuracy percent must be at least 1");
                }

                MaxRangedAccuracyPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged accuracy bonus set to +{MaxRangedAccuracyPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged accuracy bonus: +{MaxRangedAccuracyPercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxdist command.
        /// Gets or sets the maximum ranged distance bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxDistanceCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged distance percent must be at least 1");
                }

                MaxRangedDistancePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged distance bonus set to +{MaxRangedDistancePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged distance bonus: +{MaxRangedDistancePercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%");
            }
        }

        /// <summary>
        /// Calculate ranged bonuses as percentages, accounting for vanilla Focused trait.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as integers.
        /// </summary>
        public static (int damage, int accuracy, int distance) CalculateRangedBonusPercents(int credits, EntityPlayer entity)
        {
            bool hasFocused = entity != null && PlayerHasVanillaFocusedStatic(entity);
            int vanillaDamage = hasFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Each stat is capped individually
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - CapCounted(vanillaDamage));
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - CapCounted(vanillaAccuracy));
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - CapCounted(vanillaDistance));

            int damageBonus = Math.Min(credits, earnableDamage);
            int accuracyBonus = Math.Min(credits, earnableAccuracy);
            int distanceBonus = Math.Min(credits, earnableDistance);

            return (damageBonus, accuracyBonus, distanceBonus);
        }

        /// <summary>
        /// Get the maximum ranged credits a player can earn based on their traits.
        /// Players with Nearsighted or Frail traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxRangedCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxRangedDamagePercent;

            // Class cap offsets: no extra credits to pay off starting penalties.
            if (EnableClassCapOffsets) return MaxRangedDamagePercent;

            bool hasNearsighted = PlayerHasVanillaNearsighted(entity);
            bool hasFrail = PlayerHasVanillaFrail(entity);

            // Use the larger penalty to determine max credits
            int extraCredits = 0;

            // Nearsighted penalty is 15% ranged damage, need 15 extra levels to cancel it
            if (hasNearsighted)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }

            // Frail penalty is 25% ranged distance, need 25 extra levels to cancel it
            if (hasFrail)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            return MaxRangedDamagePercent + extraCredits;
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Focused trait.
        /// </summary>
        private static bool PlayerHasVanillaFocusedStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "focused");
        }

        /// <summary>
        /// Process ranged damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessRangedDamage(IServerPlayer attackerPlayer, string weaponCombo, float damage)
        {
            ProcessRangedDamageCore(attackerPlayer, weaponCombo, damage);
            MarkProgressChanged(attackerPlayer);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessRangedDamageCore(IServerPlayer attackerPlayer, string weaponCombo, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponCombo)) return;

            // Check if ranged skill is disabled
            if (IsSkillDisabled("ranged")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Track unlock-trait progression FIRST, independent of the ranged credit cap.
            // Bowyer and Improviser are separate unlocks that should still progress for players
            // who have already maxed their ranged credits.
            if (IsSimpleBowOrLongbow(weaponCombo))
            {
                TrackBowyerBowDamage(attackerPlayer, damage);
            }
            if (IsThrownRock(weaponCombo))
            {
                TrackImproviserRockDamage(attackerPlayer, damage);
            }

            // Get or create player progress data
            var playerProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            // Get the player-specific max credits (accounts for Nearsighted/Frail penalties)
            int maxCredits = GetMaxRangedCredits(attackerPlayer.Entity);

            // Skip remaining credit processing if already at max - completely invisible
            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "ranged");
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon combination
            var weaponProgress = playerProgress.GetWeaponProgress(weaponCombo);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to damage
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);

            // Add damage to THIS weapon combination's progress
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon combination
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += RangedIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned ranged credit {playerProgress.TotalCredits} with {weaponCombo}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "ranged");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyRangedBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-ranged-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on ranged damage
                CheckBowyerUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Check if the weapon combo represents a simple bow or longbow.
        /// </summary>
        private static bool IsSimpleBowOrLongbow(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("bow-simple") || lower.Contains("bow-long") ||
                   lower.StartsWith("simple", StringComparison.Ordinal) || lower.StartsWith("long", StringComparison.Ordinal);
        }

        /// <summary>
        /// Check if the weapon combo represents a thrown rock.
        /// </summary>
        private static bool IsThrownRock(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("stone-") || lower.Contains("sling+stone") ||
                   lower.StartsWith("stone", StringComparison.Ordinal) || lower.Contains("thrownstone") ||
                   (lower.Contains("stone") && !lower.Contains("whetstone"));
        }

        /// <summary>
        /// Track bow damage for Bowyer unlock.
        /// </summary>
        private static void TrackBowyerBowDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);
            progress.TotalBowDamage += modifiedDamage;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_BOWYER_BOW_DAMAGE, progress.TotalBowDamage);

            // Check if unlock threshold is reached
            CheckBowyerUnlock(player);
        }

        /// <summary>
        /// Track thrown rock damage for Improviser unlock.
        /// </summary>
        private static void TrackImproviserRockDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);
            progress.TotalRockDamage += modifiedDamage;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_IMPROVISER_ROCK_DAMAGE, progress.TotalRockDamage);

            if (DebugLoggingEnabled)
            {
                ServerApi?.Logger?.Debug($"[SeraphLeveling] Improviser rock damage tracked for {player.PlayerName}: +{modifiedDamage:F1} (total {progress.TotalRockDamage:F1}/{ImproviserRockDamageThreshold})");
            }

            // Check if unlock threshold is reached
            CheckImproviserUnlock(player);
        }

        /// <summary>
        /// Static version of ApplyRangedBonus for use from Harmony patches.
        /// Also handles Nearsighted and Frail negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as percentages.
        /// </summary>
        public static (int damage, int accuracy, int distance) ApplyRangedBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            // When Combat Overhaul is loaded, apply tier-based bonuses instead of percentages
            if (IsCOCompatEnabled)
            {
                return ApplyRangedBonusCO(player, level);
            }

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFocused = cache?.HasFocused ?? PlayerHasVanillaFocusedStatic(player.Entity);
            bool hasNearsighted = cache?.HasNearsighted ?? PlayerHasVanillaNearsighted(player.Entity);
            bool hasFrail = cache?.HasFrail ?? PlayerHasVanillaFrail(player.Entity);

            int vanillaDamage = hasVanillaFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasVanillaFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasVanillaFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int nearsightedRemaining = hasNearsighted ? CalculateRemainingPenalty(VANILLA_NEARSIGHTED_RANGED_PENALTY, level) : 0;
            int frailDistanceRemaining = hasFrail ? CalculateRemainingPenalty(VANILLA_FRAIL_DISTANCE_PENALTY, level) : 0;
            // HP penalty is tied to distance penalty - when distance penalty is cancelled (at level 25), HP is also cancelled
            float frailHpRemaining = frailDistanceRemaining > 0 ? VANILLA_FRAIL_HP_PENALTY : 0f;

            // Calculate net bonus after cancelling negative traits
            int netDamageLevel = level;
            int netDistanceLevel = level;

            if (hasNearsighted)
            {
                netDamageLevel = Math.Max(0, level - VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }
            if (hasFrail)
            {
                netDistanceLevel = Math.Max(0, level - VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            // Calculate earnable bonuses (each stat capped individually)
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - CapCounted(vanillaDamage));
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - CapCounted(vanillaAccuracy));
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - CapCounted(vanillaDistance));

            // Calculate actual bonuses from level (using net level after penalty cancellation)
            int damagePct = Math.Min(netDamageLevel, earnableDamage);
            int accuracyPct = Math.Min(level, earnableAccuracy);
            int distancePct = Math.Min(netDistanceLevel, earnableDistance);

            float damageBonus = damagePct * 0.01f;
            float accuracyBonus = accuracyPct * 0.01f;
            float distanceBonus = distancePct * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("rangedWeaponsDamage", RANGED_DAMAGE_STAT_CODE, damageBonus, false);
            player.Entity.Stats.Set("rangedWeaponsAcc", RANGED_ACCURACY_STAT_CODE, accuracyBonus, false);
            player.Entity.Stats.Set("bowDrawingStrength", RANGED_DISTANCE_STAT_CODE, distanceBonus, false);

            // Counter-stats: when Nearsighted damage / Frail distance penalty is fully cancelled,
            // apply a +penalty counter so functional ranged stats match the displayed cap.
            if (hasNearsighted)
            {
                if (nearsightedRemaining == 0)
                    player.Entity.Stats.Set("rangedWeaponsDamage", "sitNearsightedRangedCancel", VANILLA_NEARSIGHTED_RANGED_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("rangedWeaponsDamage", "sitNearsightedRangedCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("rangedWeaponsDamage", "sitNearsightedRangedCancel");
            }

            // When Frail distance penalty is fully cancelled, also negate the HP penalty AND the distance penalty
            if (hasFrail)
            {
                if (frailDistanceRemaining == 0)
                {
                    player.Entity.Stats.Set("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE, VANILLA_FRAIL_HP_PENALTY, false);
                    player.Entity.Stats.Set("bowDrawingStrength", "sitFrailDistanceCancel", VANILLA_FRAIL_DISTANCE_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE);
                    player.Entity.Stats.Remove("bowDrawingStrength", "sitFrailDistanceCancel");
                }
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("bowDrawingStrength", "sitFrailDistanceCancel");
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_RANGED_LEVEL, -1);
            int oldDamageBonus = watchedAttrs.GetInt(WATCHED_RANGED_DAMAGE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldDamageBonus != damagePct)
                || watchedAttrs.GetInt(WATCHED_RANGED_ACCURACY_BONUS, -1) != accuracyPct
                || watchedAttrs.GetInt(WATCHED_RANGED_DISTANCE_BONUS, -1) != distancePct
                || watchedAttrs.GetBool("sitHasVanillaFocused") != hasVanillaFocused
                || watchedAttrs.GetBool("sitHasNearsighted") != hasNearsighted
                || watchedAttrs.GetInt(WATCHED_NEARSIGHTED_REMAINING, -1) != nearsightedRemaining
                || watchedAttrs.GetBool("sitHasFrail") != hasFrail
                || watchedAttrs.GetInt(WATCHED_FRAIL_DISTANCE_REMAINING, -1) != frailDistanceRemaining
                || watchedAttrs.GetFloat(WATCHED_FRAIL_HP_REMAINING, -1f) != frailHpRemaining;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonuses to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_RANGED_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_RANGED_DAMAGE_BONUS, damagePct);
                watchedAttrs.SetInt(WATCHED_RANGED_ACCURACY_BONUS, accuracyPct);
                watchedAttrs.SetInt(WATCHED_RANGED_DISTANCE_BONUS, distancePct);
                watchedAttrs.SetBool("sitHasVanillaFocused", hasVanillaFocused);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasNearsighted", hasNearsighted);
                watchedAttrs.SetInt(WATCHED_NEARSIGHTED_REMAINING, nearsightedRemaining);
                watchedAttrs.SetBool("sitHasFrail", hasFrail);
                watchedAttrs.SetInt(WATCHED_FRAIL_DISTANCE_REMAINING, frailDistanceRemaining);
                watchedAttrs.SetFloat(WATCHED_FRAIL_HP_REMAINING, frailHpRemaining);

                // Add our trait to extraTraits only if player doesn't already have Focused
                UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);

                watchedAttrs.MarkPathDirty(WATCHED_RANGED_LEVEL);
            }

            return (damagePct, accuracyPct, distancePct);
        }

        /// <summary>
        /// Apply ranged bonuses when Combat Overhaul is installed.
        /// Uses damage tier bonuses instead of percentage damage bonuses.
        /// CO Focused trait gives +1 ranged slashing tier (not % damage).
        /// </summary>
        private static (int damage, int accuracy, int distance) ApplyRangedBonusCO(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFocused = cache?.HasFocused ?? PlayerHasVanillaFocusedStatic(player.Entity);

            // In CO: Focused gives +1 ranged slashing tier (not %)
            // Hunter has Focused (+1 tier already from CO), others can earn up to +1 tier
            int vanillaTier = hasVanillaFocused ? 1 : 0;
            int maxEarnableTier = 1; // Cap at 1 tier total for all classes

            // 100 credits = 1 tier. With class cap offsets the vanilla tier stops
            // counting against the cap, so Focused classes end one tier ahead.
            int earnedTier = level / 100;
            int totalTier = Math.Min(CapCounted(vanillaTier) + earnedTier, maxEarnableTier);
            int netEarnedTier = Math.Max(0, totalTier - CapCounted(vanillaTier));

            // Apply tier stat (integer, stored as float for Stats.Set compatibility)
            player.Entity.Stats.Set(CO_RANGED_TIER_SLASHING, RANGED_DAMAGE_STAT_CODE, (float)netEarnedTier, false);

            // Sync for UI
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_RANGED_LEVEL, -1);

            if (oldLevel != level)
            {
                watchedAttrs.SetInt(WATCHED_RANGED_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_CO_RANGED_TIER_BONUS, netEarnedTier);
                watchedAttrs.SetBool("sitHasVanillaFocused", hasVanillaFocused);
                watchedAttrs.SetBool("sitCOEnabled", true);
                watchedAttrs.MarkPathDirty(WATCHED_RANGED_LEVEL);

                // Add our trait to extraTraits only if player doesn't already have Focused
                UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);
            }

            // Return tier as the "damage" value for compatibility (accuracy and distance not used in CO tier system)
            return (netEarnedTier, 0, 0);
        }

        /// <summary>
        /// Gets a weapon combination code from a projectile and the shooter's held weapon.
        /// For bows+arrows, returns "bowCode+arrowCode" (e.g., "bow-long+arrow-copper").
        /// For slings+stones, returns "sling+stone".
        /// Returns null if not a qualifying ranged weapon.
        /// </summary>
        public static string GetRangedWeaponCombo(Entity projectile, EntityPlayer shooter)
        {
            if (projectile == null || shooter == null) return null;

            string projectileCode = projectile.Code?.ToString() ?? "";
            string heldItemCode = shooter.RightHandItemSlot?.Itemstack?.Collectible?.Code?.ToString() ?? "";

            // Remove any mod prefix (e.g., "game:", "combatoverhaul:") for checking
            string projCheck = projectileCode.Contains(":") ? projectileCode.Substring(projectileCode.IndexOf(':') + 1) : projectileCode;
            string heldCheck = heldItemCode.Contains(":") ? heldItemCode.Substring(heldItemCode.IndexOf(':') + 1) : heldItemCode;

            // Check for arrow projectiles (bows)
            if (projCheck.StartsWith("arrow-", StringComparison.Ordinal) || projCheck == "arrow" || projCheck.Contains("arrow"))
            {
                // Get bow type from held item (if still holding a bow)
                string bowCode = "unknown-bow";
                if (heldCheck.StartsWith("bow-", StringComparison.Ordinal) || heldCheck == "bow" ||
                    heldCheck.StartsWith("longbow", StringComparison.Ordinal) || heldCheck.StartsWith("recurvebow", StringComparison.Ordinal) ||
                    heldCheck.StartsWith("crudebow", StringComparison.Ordinal) || heldCheck.StartsWith("simplebow", StringComparison.Ordinal) ||
                    heldCheck.Contains("bow"))
                {
                    bowCode = heldCheck;
                }
                return $"{bowCode}+{projCheck}";
            }

            // Check for crossbow bolts/quarrels
            if (projCheck.StartsWith("bolt-", StringComparison.Ordinal) || projCheck == "bolt" || projCheck.Contains("bolt") ||
                projCheck.StartsWith("quarrel-", StringComparison.Ordinal) || projCheck == "quarrel" || projCheck.Contains("quarrel"))
            {
                // Get crossbow type from held item
                string crossbowCode = "unknown-crossbow";
                if (heldCheck.StartsWith("crossbow", StringComparison.Ordinal) || heldCheck.Contains("crossbow"))
                {
                    crossbowCode = heldCheck;
                }
                return $"{crossbowCode}+{projCheck}";
            }

            // Check for firearm projectiles (bullets, musket balls, etc.)
            if (projCheck.StartsWith("bullet-", StringComparison.Ordinal) || projCheck == "bullet" || projCheck.Contains("bullet") ||
                projCheck.StartsWith("musketball", StringComparison.Ordinal) || projCheck.Contains("musketball") ||
                projCheck.StartsWith("shot-", StringComparison.Ordinal) || projCheck.Contains("shot"))
            {
                // Get firearm type from held item
                string firearmCode = "unknown-firearm";
                if (heldCheck.StartsWith("musket", StringComparison.Ordinal) || heldCheck.StartsWith("pistol", StringComparison.Ordinal) ||
                    heldCheck.StartsWith("rifle", StringComparison.Ordinal) || heldCheck.StartsWith("blunderbuss", StringComparison.Ordinal) ||
                    heldCheck.Contains("gun") || heldCheck.Contains("firearm"))
                {
                    firearmCode = heldCheck;
                }
                return $"{firearmCode}+{projCheck}";
            }

            // Check for sling stones (slung from sling — entity is thrownstone-{rock})
            if (projCheck.StartsWith("stone-", StringComparison.Ordinal) || projCheck == "stone" || projCheck.StartsWith("thrownstone", StringComparison.Ordinal) ||
                projCheck.Contains("slingstone") || projCheck.Contains("sling-stone"))
            {
                // Check if holding a sling
                string slingCode = "thrown";
                if (heldCheck.StartsWith("sling", StringComparison.Ordinal) || heldCheck.Contains("sling"))
                {
                    slingCode = heldCheck;
                }
                return $"{slingCode}+{projCheck}";
            }

            // Generic thrown items (CollectibleBehaviorThrowable). VS 1.22 spawns hand-thrown
            // stones, bones, etc. as a single shared `game:thrownitem` projectile entity — the
            // actual item is in ProjectileStack. Peek there to figure out what was thrown so
            // Improviser etc. can recognize it as a stone.
            if (projCheck == "thrownitem" || projCheck.StartsWith("thrownitem-", StringComparison.Ordinal) || projCheck.StartsWith("thrownitem+", StringComparison.Ordinal))
            {
                string stackCode = "";
                if (projectile is IProjectile proj && proj.ProjectileStack?.Collectible?.Code != null)
                {
                    stackCode = proj.ProjectileStack.Collectible.Code.ToString();
                }
                string stackCheck = !string.IsNullOrEmpty(stackCode) && stackCode.Contains(":")
                    ? stackCode.Substring(stackCode.IndexOf(':') + 1)
                    : stackCode;
                if (!string.IsNullOrEmpty(stackCheck))
                {
                    return $"thrown+{stackCheck}";
                }
                return $"thrown+{projCheck}";
            }

            // Check for spear/javelin throws (thrown spears deal ranged damage)
            if (projCheck.StartsWith("spear-", StringComparison.Ordinal) || projCheck.StartsWith("thrownspear", StringComparison.Ordinal) ||
                projCheck.StartsWith("javelin-", StringComparison.Ordinal) || projCheck.Contains("javelin") ||
                projCheck.StartsWith("pilum-", StringComparison.Ordinal) || projCheck.Contains("throwingspear"))
            {
                return $"thrown+{projCheck}";
            }

            // Check for Atlatl darts (Return of the Atlatl mod)
            // Projectile entities are "atlatl:apdart-{material}", projCheck will be "apdart-{material}"
            if (projCheck.StartsWith("apdart", StringComparison.Ordinal) || projectileCode.Contains("atlatl:apdart"))
            {
                string launcherCode = "unknown-atlatl";
                if (heldCheck.StartsWith("aplauncher", StringComparison.Ordinal) || heldCheck.Contains("aplauncher") ||
                    heldItemCode.Contains("atlatl:aplauncher"))
                {
                    launcherCode = heldCheck;
                }
                return $"{launcherCode}+{projCheck}";
            }

            return null;
        }

        /// <summary>
        /// Checks if a damage source is from a ranged attack (projectile).
        /// </summary>
        public static bool IsRangedDamage(DamageSource damageSource)
        {
            // Debug logging at start to diagnose CO issues (disabled by default)
            if (DebugLoggingEnabled)
            {
                ServerApi?.Logger.Debug($"[SeraphLeveling] IsRangedDamage called: SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}, Same={damageSource?.SourceEntity == damageSource?.CauseEntity}");
            }

            // CauseEntity is non-null for projectile damage (it's the shooter)
            // SourceEntity is the projectile itself
            if (damageSource?.CauseEntity == null) return false;

            // For melee attacks, SourceEntity equals CauseEntity (both are the attacker).
            // For ranged attacks, SourceEntity is the projectile, CauseEntity is the shooter.
            // Combat Overhaul may set CauseEntity for melee attacks, so we check if they're
            // the same entity to distinguish melee from ranged.
            if (damageSource.SourceEntity == damageSource.CauseEntity) return false;

            // Additional check: the damage should be from a projectile type
            // PiercingAttack is typically used for arrows in vanilla
            // SlashingAttack is used by Combat Overhaul for arrows
            // BluntAttack is used for thrown stones
            return damageSource.Type == EnumDamageType.PiercingAttack ||
                   damageSource.Type == EnumDamageType.SlashingAttack ||
                   damageSource.Type == EnumDamageType.BluntAttack;
        }
    }
}
