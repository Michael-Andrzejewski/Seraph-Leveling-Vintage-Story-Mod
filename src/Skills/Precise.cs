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

        // =========================================================================
        // PRECISE TRAIT - Tracks damage to mechanicals for damage bonus
        // =========================================================================
        public const string PRECISE_STAT_CODE = "sitPreciseBonus";
        private const string PRECISE_PROGRESS_SAVE_KEY = "sitPreciseProgress";
        public const string WATCHED_PRECISE_LEVEL = "sitPreciseLevel";
        public const string WATCHED_PRECISE_BONUS = "sitPreciseBonusPercent";
        public const string PRECISE_TRAIT_CODE = "sitprecisemastery";

        // Precise progression configuration
        public static int BasePreciseDamagePerIncrement = 100;  // Base damage for first credit
        public static int PreciseIncrementStep = 100;            // Increment step per credit
        public static int MaxPrecisePercent = 30;                // 30% max damage bonus to mechanicals

        // Vanilla Precise trait bonus (Clockmaker)
        public const int VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS = 25;

        // Storage for precise progress
        public static ConcurrentDictionary<string, PreciseProgressData> PreciseProgress = new ConcurrentDictionary<string, PreciseProgressData>();
        private static volatile bool pendingPreciseProgressSave = false;

        // =========================================================================
        // PRECISE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Check if an entity is a mechanical creature (e.g., locust, bell, etc.).
        /// </summary>
        public static bool IsMechanicalCreature(Entity entity)
        {
            if (entity == null) return false;
            var props = entity.Properties;
            if (props == null) return IsMechanicalCreatureUncached(entity);
            if (mechanicalByType.TryGetValue(props, out bool known)) return known;
            bool result = IsMechanicalCreatureUncached(entity);
            mechanicalByType[props] = result;
            return result;
        }

        private static bool IsMechanicalCreatureUncached(Entity entity)
        {
            string entityCode = entity.Code?.ToString()?.ToLowerInvariant() ?? "";

            // Check for known mechanical creatures
            // Locusts are the main mechanical enemies in Vintage Story
            if (entityCode.Contains("locust")) return true;
            if (entityCode.Contains("bell")) return true;
            if (entityCode.Contains("mechanical")) return true;
            if (entityCode.Contains("automaton")) return true;
            if (entityCode.Contains("construct")) return true;

            // Also check the entity class
            string entityClass = entity.GetType().Name.ToLowerInvariant();
            if (entityClass.Contains("locust")) return true;

            return false;
        }

        /// <summary>
        /// Process damage dealt to a mechanical creature by a player.
        /// Adds progress toward the Precise trait.
        /// </summary>
        public static void ProcessPreciseDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            ProcessPreciseDamageCore(attackerPlayer, weaponType, damage);
            MarkProgressChanged(attackerPlayer);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessPreciseDamageCore(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || damage <= 0) return;
            if (string.IsNullOrEmpty(weaponType)) return;

            // Check if precise skill is disabled
            if (IsSkillDisabled("precise")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            // Check if already at max
            int effectiveMax = GetPreciseEffectiveMax(attackerPlayer.Entity);
            if (playerProgress.TotalCredits >= effectiveMax) return;

            int oldCredits = playerProgress.TotalCredits;

            // Get or create weapon progress
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            // Add damage to progress (apply sleep buff multiplier if active)
            float modifiedDamage = ApplyXPMultiplier(attackerPlayer.PlayerUID, damage);
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < effectiveMax)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += PreciseIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned precise credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            // Mark for saving if any progress was made
            if (damage > 0)
            {
                pendingPreciseProgressSave = true;
            }

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "precise");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyPreciseBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-precise-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check if Tinkerer should be unlocked
                CheckTinkererUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Get the effective maximum for Precise based on player class.
        /// Clockmaker has vanilla +25%, so they can only earn 5 more levels.
        /// </summary>
        private static int GetPreciseEffectiveMax(EntityPlayer entity)
        {
            if (PlayerHasVanillaPreciseStatic(entity))
            {
                // Clockmaker already has +25%, cap at +5 more to reach 30% total
                return MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS;
            }
            return MaxPrecisePercent;
        }

        /// <summary>
        /// Apply the Precise bonus to a player based on their earned credits.
        /// The bonus increases damage to mechanical creatures.
        /// </summary>
        private static int ApplyPreciseBonusStatic(IServerPlayer player, int credits)
        {
            if (player?.Entity == null) return 0;
            // Check if player has vanilla Precise trait (Clockmaker)
            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);

            // Calculate effective cap
            int effectiveMax = hasVanillaPrecise ? (MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS) : MaxPrecisePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent
            int bonusPercent = effectiveCredits;

            // mechanicalsDamage is a delta on a base of 1, the same as vanilla
            // Precise's 0.4705. Writing 1 + bonus added a whole extra 100% on top
            // of the bonus the character sheet advertises.
            if (bonusPercent > 0)
            {
                float statValue = bonusPercent / 100f;
                player.Entity.Stats.Set("mechanicalsDamage", PRECISE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove("mechanicalsDamage", PRECISE_STAT_CODE);
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPrecise", hasVanillaPrecise);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, PRECISE_TRAIT_CODE, credits > 0 && !hasVanillaPrecise);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Precise trait (Clockmaker).
        /// </summary>
        private static bool PlayerHasVanillaPreciseStatic(EntityPlayer entity)
        {
            return PlayerHasVanillaTraitOrClassStatic(entity, "precise", "clockmaker");
        }

        /// <summary>
        /// Handler for /trait precise command.
        /// </summary>
        private TextCommandResult OnTraitPreciseCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
            int effectiveMax = GetPreciseEffectiveMax(player.Entity);
            int bonusPercent = Math.Min(progress.TotalCredits, effectiveMax);

            var sb = new StringBuilder();
            sb.AppendLine($"Precise progression: Level {progress.TotalCredits} / {effectiveMax}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% damage to mechanicals");
            if (hasVanillaPrecise)
            {
                int totalBonus = VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + bonusPercent;
                sb.AppendLine($"Combined with Clockmaker trait: +{totalBonus}% total");
            }
            if (progress.TotalCredits < effectiveMax)
            {
                sb.AppendLine($"Per-weapon progress:");
                foreach (var kvp in progress.WeaponProgress)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value.DamageInIncrement:F0} / {kvp.Value.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait preciselevel command.
        /// Gets or sets the player's precise level.
        /// </summary>
        private TextCommandResult OnTraitPreciseLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                var progress = PreciseProgress.GetOrAdd(player.PlayerUID, _ => new PreciseProgressData());
                bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
                int currentBonus = hasVanillaPrecise ? VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS : progress.TotalCredits;
                return TextCommandResult.Success($"Current precise level: {progress.TotalCredits}/{MaxPrecisePercent} (+{currentBonus}% mechanical damage)");
            }

            string toolName = (string)args[1];
            return SetPreciseLevelForPlayer(player, player.PlayerUID, newLevel.Value, toolName);
        }
    }
}
