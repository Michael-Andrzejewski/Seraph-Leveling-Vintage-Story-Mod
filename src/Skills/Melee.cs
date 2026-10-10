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

        // Keys for melee damage progression system
        public const string MELEE_DAMAGE_KEY = "sitMeleeDamage";
        public const string MELEE_STAT_CODE = "sitMeleeBonus";
        private const string MELEE_PROGRESS_SAVE_KEY = "sitMeleeProgress";

        // WatchedAttributes keys for client sync (melee)
        public const string WATCHED_MELEE_LEVEL = "sitMeleeLevel";
        public const string WATCHED_MELEE_BONUS = "sitMeleeBonusPercent";

        // Trait code for the melee mastery trait (Soldier)
        public const string MELEE_TRAIT_CODE = "sitmeleemastery";

        // Melee damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon types resets the increment counter back to base
        public static int BaseDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int MeleeIncrementStep = 100;       // How much more damage each subsequent credit needs
        public static int MaxMeleeDamagePercent = 50;     // 50% max bonus

        // Vanilla Soldier trait melee damage bonus (used for cap calculations)
        public const int VANILLA_SOLDIER_MELEE_BONUS = 30;

        // Storage for melee progress - keyed by player UID
        public static ConcurrentDictionary<string, MeleeProgressData> MeleeProgress = new ConcurrentDictionary<string, MeleeProgressData>();

        // Flag to indicate pending melee progress save
        private static volatile bool pendingMeleeProgressSave = false;

        /// <summary>
        /// Handler for /trait melee command.
        /// </summary>
        private TextCommandResult OnTraitMeleeCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMeleeBonusPercent(currentCredits);
            int maxCredits = GetMaxMeleeCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Melee progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% melee damage");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (weaponName.StartsWith("game:", StringComparison.Ordinal))
                        weaponName = weaponName.Substring(5);

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal damage with swords, falx, or spears to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait meleebase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMeleeBaseCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Base damage per increment must be at least 1",
            v => BaseDamagePerIncrement = v,
            () => $"Base damage per increment set to {BaseDamagePerIncrement}. New weapons will require this much damage for first 1%.",
            () => $"Current base damage per increment: {BaseDamagePerIncrement}\nIncrement step: +{MeleeIncrementStep} per credit");

        /// <summary>
        /// Handler for /trait meleeincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMeleeIncrementCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 0, "Increment step cannot be negative",
            v => MeleeIncrementStep = v,
            () => $"Melee increment step set to +{MeleeIncrementStep} per credit.\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...",
            () => $"Current melee increment step: +{MeleeIncrementStep} per credit\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...");

        /// <summary>
        /// Handler for /trait meleelevel command.
        /// Gets or sets the player's melee credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMeleeLevelCommand(TextCommandCallingArgs args)
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
                int maxCredits = GetMaxMeleeCredits(player.Entity);
                var progress = MeleeProgress.GetOrAdd(player.PlayerUID, _ => new MeleeProgressData());
                int currentBonus = CalculateMeleeBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current melee level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% melee damage)");
            }

            string toolName = (string)args[1];
            return SetMeleeLevelForPlayer(player, player.PlayerUID, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait meleemax command.
        /// Gets or sets the maximum melee damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMeleeMaxCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Max melee damage percent must be at least 1",
            v => MaxMeleeDamagePercent = v,
            () => $"Max melee damage bonus set to +{MaxMeleeDamagePercent}%. All player bonuses recalculated.",
            () => $"Current max melee damage bonus: +{MaxMeleeDamagePercent}%",
            afterSet: () =>
            {
                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
                    ApplyMeleeBonusStatic(player, progress.TotalCredits);
                }
            });

        /// <summary>
        /// Calculate the melee damage bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMeleeDamagePercent.
        /// </summary>
        public static int CalculateMeleeBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMeleeDamagePercent);
        }

        /// <summary>
        /// Get the maximum melee credits a player can earn based on their traits.
        /// Players with Farsighted or Nervous traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMeleeCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMeleeDamagePercent;

            // Class cap offsets: no extra credits to pay off starting penalties.
            if (EnableClassCapOffsets) return MaxMeleeDamagePercent;

            bool hasFarsighted = PlayerHasVanillaFarsighted(entity);
            bool hasNervous = PlayerHasVanillaNervous(entity);

            // Sum the extra credits; the apply path subtracts BOTH penalties, so
            // a class with both needs both to be cancellable (15 + 15).
            int extraCredits = (hasFarsighted ? VANILLA_FARSIGHTED_MELEE_PENALTY : 0)
                             + (hasNervous ? VANILLA_NERVOUS_MELEE_PENALTY : 0);
            return MaxMeleeDamagePercent + extraCredits;
        }

        /// <summary>
        /// Process melee damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessMeleeDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            ProcessMeleeDamageCore(attackerPlayer, weaponType, damage);
            MarkProgressChanged(attackerPlayer);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessMeleeDamageCore(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponType)) return;

            // Check if melee skill is disabled
            if (IsSkillDisabled("melee")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            // Get the player-specific max credits (accounts for Farsighted/Nervous penalties)
            int maxCredits = GetMaxMeleeCredits(attackerPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "melee");
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon type
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to damage
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);

            // Add damage to THIS weapon type's progress
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon type
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += MeleeIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned melee credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "melee");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMeleeBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-melee-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on melee damage
                CheckMercilessUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Static version of ApplyMeleeBonus for use from Harmony patches.
        /// Also handles Farsighted and Nervous negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        private static int ApplyMeleeBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierStatic(player.Entity);
            bool hasFarsighted = cache?.HasFarsighted ?? PlayerHasVanillaFarsighted(player.Entity);
            bool hasNervous = cache?.HasNervous ?? PlayerHasVanillaNervous(player.Entity);

            int vanillaSoldierBonus = hasVanillaSoldier ? VANILLA_SOLDIER_MELEE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int farsightedRemaining = hasFarsighted ? CalculateRemainingPenalty(VANILLA_FARSIGHTED_MELEE_PENALTY, level) : 0;
            int nervousRemaining = hasNervous ? CalculateRemainingPenalty(VANILLA_NERVOUS_MELEE_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits. Sum the
            // penalties; assigning per-trait would let the second overwrite the
            // first for a custom class that has both.
            int totalMeleePenalty = (hasFarsighted ? VANILLA_FARSIGHTED_MELEE_PENALTY : 0)
                                  + (hasNervous ? VANILLA_NERVOUS_MELEE_PENALTY : 0);
            int netBonusPercent = Math.Max(0, level - totalMeleePenalty);

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMeleeDamagePercent
            int maxEarnableBonus = MaxMeleeDamagePercent - CapCounted(vanillaSoldierBonus);
            netBonusPercent = Math.Min(netBonusPercent, Math.Max(0, maxEarnableBonus));

            float bonus = netBonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("meleeWeaponsDamage", MELEE_STAT_CODE, bonus, false);

            // Counter-stats: when Farsighted/Nervous melee penalty is fully cancelled, apply a
            // +penalty counter so functional melee damage matches the displayed cap. Without
            // this, Hunter (Farsighted) and Malefactor/Clockmaker (Nervous) would land on a
            // functional +35% melee at maxall while their displayed +50% suggests parity.
            if (hasFarsighted)
            {
                if (farsightedRemaining == 0)
                    player.Entity.Stats.Set("meleeWeaponsDamage", "sitFarsightedMeleeCancel", VANILLA_FARSIGHTED_MELEE_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("meleeWeaponsDamage", "sitFarsightedMeleeCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("meleeWeaponsDamage", "sitFarsightedMeleeCancel");
            }
            if (hasNervous)
            {
                if (nervousRemaining == 0)
                    player.Entity.Stats.Set("meleeWeaponsDamage", "sitNervousMeleeCancel", VANILLA_NERVOUS_MELEE_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("meleeWeaponsDamage", "sitNervousMeleeCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("meleeWeaponsDamage", "sitNervousMeleeCancel");
            }

            // Check if any values have changed before updating WatchedAttributes.
            // The trait booleans must be part of the comparison: after a class
            // change with unchanged level/bonus (e.g. both 0), the display attrs
            // would otherwise keep the old class's traits.
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MELEE_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MELEE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != netBonusPercent)
                || watchedAttrs.GetBool("sitHasVanillaSoldier") != hasVanillaSoldier
                || watchedAttrs.GetBool("sitHasFarsighted") != hasFarsighted
                || watchedAttrs.GetInt(WATCHED_FARSIGHTED_REMAINING, -1) != farsightedRemaining
                || watchedAttrs.GetBool("sitHasNervous") != hasNervous
                || watchedAttrs.GetInt(WATCHED_NERVOUS_REMAINING, -1) != nervousRemaining;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MELEE_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MELEE_BONUS, netBonusPercent);
                watchedAttrs.SetBool("sitHasVanillaSoldier", hasVanillaSoldier);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasFarsighted", hasFarsighted);
                watchedAttrs.SetInt(WATCHED_FARSIGHTED_REMAINING, farsightedRemaining);
                watchedAttrs.SetBool("sitHasNervous", hasNervous);
                watchedAttrs.SetInt(WATCHED_NERVOUS_REMAINING, nervousRemaining);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, MELEE_TRAIT_CODE, level > 0 && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_MELEE_LEVEL);
            }

            // Apply CO melee tier bonus if CO is enabled (Frightened of Melee / Melee Expert)
            if (IsCOCompatEnabled)
            {
                ApplyCOMeleeTier(player, level);
            }

            return netBonusPercent;
        }

        /// <summary>
        /// Static version of PlayerHasVanillaSoldier for use from Harmony patches.
        /// </summary>
        private static bool PlayerHasVanillaSoldierStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "soldier");
        }

        /// <summary>
        /// Gets the weapon code from a held item if it's a qualifying melee weapon, or null otherwise.
        /// Returns the full item code (e.g., "game:sword-copper") to track each weapon type individually.
        /// Static version for use from Harmony patches.
        /// </summary>
        public static string GetWeaponTypeFromCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            // Remove namespace prefix for pattern matching
            string codeToCheck = itemCode;
            if (itemCode.Contains(":"))
            {
                codeToCheck = itemCode.Substring(itemCode.IndexOf(':') + 1);
            }
            string lowerCode = codeToCheck.ToLowerInvariant();

            // =================================================================
            // SWORDS (one-handed and two-handed)
            // =================================================================

            // Two-handed swords
            if (lowerCode.StartsWith("greatsword-", StringComparison.Ordinal) || lowerCode.StartsWith("zweihander-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("claymore-", StringComparison.Ordinal) || lowerCode.StartsWith("flamberge-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("montante-", StringComparison.Ordinal) || lowerCode.StartsWith("nodachi-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("2hsword-", StringComparison.Ordinal) || lowerCode.StartsWith("2h-sword-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("twohandedsword-", StringComparison.Ordinal) || lowerCode.StartsWith("twohanded-sword-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("sword-great-", StringComparison.Ordinal) || lowerCode.StartsWith("sword-long-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("longsword-", StringComparison.Ordinal) ||
                (lowerCode.Contains("twohanded") && lowerCode.Contains("sword")) ||
                (lowerCode.Contains("2h") && lowerCode.Contains("sword")))
            {
                return itemCode;
            }

            // One-handed swords, blades, and similar
            if (lowerCode.StartsWith("sword-", StringComparison.Ordinal) || lowerCode.StartsWith("blade-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("shortsword-", StringComparison.Ordinal) || lowerCode.StartsWith("sword-short-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("sword-arming-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("saber-", StringComparison.Ordinal) || lowerCode.StartsWith("sabre-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("rapier-", StringComparison.Ordinal) || lowerCode.StartsWith("scimitar-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("cutlass-", StringComparison.Ordinal) || lowerCode.StartsWith("falx-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("falchion-", StringComparison.Ordinal) || lowerCode.StartsWith("kopis-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("gladius-", StringComparison.Ordinal) || lowerCode.StartsWith("messer-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // Daggers and knives (combat)
            if (lowerCode.StartsWith("dagger-", StringComparison.Ordinal) || lowerCode.StartsWith("knife-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("stiletto-", StringComparison.Ordinal) || lowerCode.StartsWith("khanjar-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("baselard-", StringComparison.Ordinal) || lowerCode.StartsWith("dirk-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("tanto-", StringComparison.Ordinal) || lowerCode.StartsWith("kukri-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // =================================================================
            // POLEARMS
            // =================================================================

            // Spears (melee)
            if (lowerCode.StartsWith("spear-", StringComparison.Ordinal) || lowerCode.StartsWith("pike-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("lance-", StringComparison.Ordinal) || lowerCode.StartsWith("trident-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("pilum-", StringComparison.Ordinal) || lowerCode.StartsWith("sarissa-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // Javelins (can be used in melee too)
            if (lowerCode.StartsWith("javelin-", StringComparison.Ordinal) || lowerCode.StartsWith("throwing-spear-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("thrown-spear-", StringComparison.Ordinal) || lowerCode.StartsWith("dart-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("plumbata-", StringComparison.Ordinal) ||
                lowerCode.Contains("javelin") || lowerCode.Contains("throwingspear") ||
                lowerCode.Contains("thrownspear"))
            {
                return itemCode;
            }

            // Halberds and poleaxes
            if (lowerCode.StartsWith("halberd-", StringComparison.Ordinal) || lowerCode.StartsWith("poleaxe-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("glaive-", StringComparison.Ordinal) || lowerCode.StartsWith("bardiche-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("voulge-", StringComparison.Ordinal) || lowerCode.StartsWith("guisarme-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("billhook-", StringComparison.Ordinal) || lowerCode.StartsWith("partisan-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("naginata-", StringComparison.Ordinal) || lowerCode.StartsWith("sovnya-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // Quarterstaves
            if (lowerCode.StartsWith("quarterstaff-", StringComparison.Ordinal) || lowerCode.StartsWith("staff-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("bo-", StringComparison.Ordinal) || lowerCode.Contains("bo-staff"))
            {
                return itemCode;
            }

            // =================================================================
            // BLUNT WEAPONS
            // =================================================================

            // Maces and warhammers
            if (lowerCode.StartsWith("mace-", StringComparison.Ordinal) || lowerCode.StartsWith("morningstar-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("flail-", StringComparison.Ordinal) || lowerCode.StartsWith("warhammer-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("maul-", StringComparison.Ordinal) || lowerCode.StartsWith("hammer-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("flangedmace-", StringComparison.Ordinal) || lowerCode.StartsWith("spikedmace-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // Clubs
            if (lowerCode.StartsWith("club-", StringComparison.Ordinal) || lowerCode.StartsWith("cudgel-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("baton-", StringComparison.Ordinal) || lowerCode.StartsWith("truncheon-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("shillelagh-", StringComparison.Ordinal) || lowerCode.StartsWith("blackjack-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // =================================================================
            // AXES (combat, not tools)
            // =================================================================
            if (lowerCode.StartsWith("battleaxe-", StringComparison.Ordinal) || lowerCode.StartsWith("waraxe-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("handaxe-", StringComparison.Ordinal) || lowerCode.StartsWith("hatchet-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("tomahawk-", StringComparison.Ordinal) || lowerCode.StartsWith("francisca-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("dane-axe-", StringComparison.Ordinal) || lowerCode.StartsWith("daneaxe-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("broadaxe-", StringComparison.Ordinal) || lowerCode.StartsWith("labrys-", StringComparison.Ordinal) ||
                (lowerCode.StartsWith("axe-", StringComparison.Ordinal) && !lowerCode.Contains("pickaxe")))
            {
                return itemCode;
            }

            // =================================================================
            // ANCIENT ARMORY MOD WEAPONS
            // =================================================================

            // aa-blade: gladius, arming, claymore, sabre, longsword, falchion (swords)
            if (lowerCode.StartsWith("aa-blade-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // aa-axe: bearded, battle, bardiche (battle axes)
            if (lowerCode.StartsWith("aa-axe-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // aa-club: flanged, morningstar, spiked, warhammer (maces)
            if (lowerCode.StartsWith("aa-club-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // aa-knife: dagger, stiletto, khanjar, baselard (combat knives)
            if (lowerCode.StartsWith("aa-knife-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            // aa-spear: boar, voulge, fork, ranseur (polearms)
            if (lowerCode.StartsWith("aa-spear-", StringComparison.Ordinal))
            {
                return itemCode;
            }

            return null;
        }
    }
}
