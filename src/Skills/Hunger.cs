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

        // Keys for hunger rate progression system
        public const string HUNGER_STAT_CODE = "sitHungerBonus";
        private const string HUNGER_PROGRESS_SAVE_KEY = "sitHungerProgress";

        // WatchedAttributes keys for client sync (hunger)
        public const string WATCHED_HUNGER_LEVEL = "sitHungerLevel";
        public const string WATCHED_HUNGER_BONUS = "sitHungerBonusPercent";

        // Trait code for the hunger mastery trait
        public const string HUNGER_TRAIT_CODE = "sithungermastery";

        // Hunger rate progression configuration
        // Base seconds at full saturation for first 1%: 300 seconds (5 minutes)
        // Each subsequent 1% requires +60 more seconds (5 min, 6 min, 7 min, etc.)
        public static int BaseSecondsPerIncrement = 300;   // Base seconds needed for first credit (5 minutes)
        public static int HungerIncrementStep = 60;        // How many more seconds each subsequent credit needs (1 minute)
        public static int MaxHungerReductionPercent = 25;  // 25% max hunger rate reduction (to 75% rate)
        public static int HungerSaturationThresholdPercent = 100;  // count time at >= this % of max saturation

        // Vanilla Ravenous trait hunger rate increase (used for cap calculations)
        // Blackguard has +30% hunger rate, so earning 25% brings them back to nearly normal
        public const int VANILLA_RAVENOUS_HUNGER_PENALTY = 30;
        public const string WATCHED_RAVENOUS_REMAINING = "sitRavenousRemaining";

        // Storage for hunger progress - keyed by player UID
        public static ConcurrentDictionary<string, HungerProgressData> HungerProgress = new ConcurrentDictionary<string, HungerProgressData>();

        // Flag to indicate pending hunger progress save
        private static volatile bool pendingHungerProgressSave = false;

        /// <summary>
        /// Handler for /trait hunger command.
        /// </summary>
        private TextCommandResult OnTraitHungerCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int currentCredits = progress.TotalCredits;
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity as EntityPlayer);
            int bonusPercent = CalculateHungerBonusPercent(currentCredits, player.Entity as EntityPlayer);
            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity as EntityPlayer);

            // Calculate target hunger rate (same for all classes)
            int targetHungerRate = 100 - MaxHungerReductionPercent;

            var sb = new StringBuilder();
            sb.AppendLine($"Hunger progression: {currentCredits} / {playerMaxCredits} credits");
            sb.AppendLine($"Current bonus: -{bonusPercent}% hunger rate");
            if (hasRavenous)
            {
                int currentRate = 130 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}% (Ravenous: 130% base)");
            }
            else
            {
                int currentRate = 100 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}%");
            }
            sb.AppendLine($"Target hunger rate: {targetHungerRate}%");
            sb.AppendLine($"\nProgress toward next credit:");
            sb.AppendLine($"  {progress.SecondsInIncrement:F0}/{progress.CurrentIncrementSize} seconds at full saturation");

            if (currentCredits >= playerMaxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait hungerbase command.
        /// Sets the base seconds needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitHungerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds per increment must be at least 1");
                }

                BaseSecondsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds per increment set to {BaseSecondsPerIncrement}. First 1% requires {BaseSecondsPerIncrement} seconds at full saturation.");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds per increment: {BaseSecondsPerIncrement}\nIncrement step: +{HungerIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait hungerincrement command.
        /// Sets how many additional seconds are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitHungerIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                HungerIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Hunger increment step set to +{HungerIncrementStep} per credit.\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current hunger increment step: +{HungerIncrementStep} per credit\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait hungerlevel command.
        /// Gets or sets the player's hunger credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitHungerLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Calculate player-specific max credits
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int? newCredits = OptInt(args, 0);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                bool hasRavenousCurrent = PlayerHasVanillaRavenousStatic(player.Entity);
                int currentEffectiveRate = hasRavenousCurrent ? (130 - progress.TotalCredits) : (100 - progress.TotalCredits);
                return TextCommandResult.Success($"Current hunger level: {progress.TotalCredits}/{playerMaxCredits} (-{progress.TotalCredits}% hunger rate, effective rate: {currentEffectiveRate}%)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > playerMaxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max for this player ({playerMaxCredits})");
            }

            // Set the player's progress
            progress.TotalCredits = newCredits.Value;
            progress.SecondsInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseSecondsPerIncrement + (newCredits.Value * HungerIncrementStep);

            pendingHungerProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyHungerBonusStatic(player, newCredits.Value);

            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity);
            int effectiveRate = hasRavenous ? (130 - bonusPercent) : (100 - bonusPercent);

            UpdateSkillActivityDay(playerUid, "hunger");

            return TextCommandResult.Success($"Hunger credits set to {newCredits.Value}/{playerMaxCredits} (-{bonusPercent}% hunger rate, effective rate: {effectiveRate}%).");
        }

        /// <summary>
        /// Handler for /trait hungermax command.
        /// Gets or sets the maximum hunger rate reduction percent (for non-Ravenous players).
        /// This determines the target hunger rate for all classes.
        /// </summary>
        private TextCommandResult OnTraitHungerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max hunger rate reduction percent must be at least 1");
                }

                MaxHungerReductionPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                    {
                        CurrentIncrementSize = BaseSecondsPerIncrement
                    });
                    ApplyHungerBonusStatic(player, progress.TotalCredits);
                }

                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate set to {targetRate}% (non-Ravenous: {MaxHungerReductionPercent} credits, Ravenous: {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits). All player bonuses recalculated.");
            }
            else
            {
                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate: {targetRate}%\nNon-Ravenous players need {MaxHungerReductionPercent} credits\nRavenous players need {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits");
            }
        }

        /// <summary>
        /// Calculate the maximum hunger credits a player can earn.
        /// Ravenous players need more credits to reach the same target hunger rate.
        /// Target is (100 - MaxHungerReductionPercent)% = 75% by default.
        /// Non-Ravenous: 100% - 75% = 25 credits needed
        /// Ravenous: 130% - 75% = 55 credits needed
        /// </summary>
        public static int CalculateMaxHungerCredits(EntityPlayer entity)
        {
            // Class cap offsets: no extra credits to pay off Ravenous, same as every
            // other max-credit helper.
            if (EnableClassCapOffsets) return MaxHungerReductionPercent;

            bool hasRavenous = entity != null && PlayerHasVanillaRavenousStatic(entity);
            int ravenousPenalty = hasRavenous ? VANILLA_RAVENOUS_HUNGER_PENALTY : 0;
            // MaxHungerReductionPercent represents how much a normal player needs to reduce
            // Ravenous players need that PLUS their penalty to reach the same target
            return MaxHungerReductionPercent + ravenousPenalty;
        }

        /// <summary>
        /// Calculate the hunger rate reduction bonus as an integer percentage.
        /// This is the actual reduction applied (1% per credit, up to player's max).
        /// </summary>
        public static int CalculateHungerBonusPercent(int credits, EntityPlayer entity)
        {
            int maxCredits = CalculateMaxHungerCredits(entity);
            return Math.Min(credits, maxCredits);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Ravenous trait.
        /// </summary>
        private static bool PlayerHasVanillaRavenousStatic(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "ravenous");
        }

        /// <summary>
        /// Apply hunger rate reduction to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// All classes can reach the same target hunger rate (75% by default).
        /// Ravenous players start at 130% and need 55 credits to reach 75%.
        /// Non-Ravenous players start at 100% and need 25 credits to reach 75%.
        /// </summary>
        public static int ApplyHungerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaRavenous = cache?.HasRavenous ?? PlayerHasVanillaRavenousStatic(player.Entity);

            // Calculate max credits this player can earn
            int maxCredits = CalculateMaxHungerCredits(player.Entity);

            // Calculate bonus from level (1% per level, capped at player's max)
            int cappedLevel = Math.Min(level, maxCredits);
            float bonus = cappedLevel * 0.01f;
            int bonusPercent = (int)(bonus * 100);

            // Calculate remaining Ravenous penalty (0 when fully cancelled at level 30)
            int ravenousRemaining = hasVanillaRavenous ? CalculateRemainingPenalty(VANILLA_RAVENOUS_HUNGER_PENALTY, level) : 0;

            // Always apply stats (they're not persistent)
            // Set the hunger rate stat - this value is ADDED to the base (1.0)
            // We want to REDUCE hunger rate, so we use a negative value
            player.Entity.Stats.Set("hungerrate", HUNGER_STAT_CODE, -bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_HUNGER_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_HUNGER_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent)
                || watchedAttrs.GetBool("sitHasVanillaRavenous") != hasVanillaRavenous
                || watchedAttrs.GetInt("sitMaxHungerCredits", -1) != maxCredits
                || watchedAttrs.GetInt(WATCHED_RAVENOUS_REMAINING, -1) != ravenousRemaining;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_HUNGER_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_HUNGER_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaRavenous", hasVanillaRavenous);
                watchedAttrs.SetInt("sitMaxHungerCredits", maxCredits);
                watchedAttrs.SetInt(WATCHED_RAVENOUS_REMAINING, ravenousRemaining);

                // Add our trait to extraTraits (hunger mastery is unique, doesn't replace a vanilla trait)
                UpdateExtraTraitStatic(player.Entity, HUNGER_TRAIT_CODE, level > 0);

                // Only call MarkPathDirty once (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_HUNGER_LEVEL);
            }

            return bonusPercent;
        }

        /// <summary>
        /// Called every 1000ms (1 second) to track time spent at full saturation for all online players.
        /// Players at maximum saturation (1500/1500) accumulate time toward hunger rate reduction.
        /// </summary>
        private void OnHungerTick(float dt)
        {
            // Skip hunger progression if disabled
            if (IsSkillDisabled("hunger")) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;

                string playerUid = player.PlayerUID;

                // Get the player's hunger data from WatchedAttributes
                var hungerTree = player.Entity.WatchedAttributes.GetTreeAttribute("hunger");
                if (hungerTree == null) continue;

                // Check if player is at full saturation (1500/1500)
                float currentSaturation = hungerTree.GetFloat("currentsaturation", 0);
                float maxSaturation = hungerTree.GetFloat("maxsaturation", 1500);

                // Count time at or above the configured share of max saturation
                // (100 = only at exactly full, the original rule).
                if (currentSaturation < maxSaturation * (HungerSaturationThresholdPercent / 100f)) continue;

                // Get or create player progress data
                var playerProgress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                {
                    CurrentIncrementSize = BaseSecondsPerIncrement
                });

                // Calculate player-specific max credits (Ravenous players need more)
                int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);

                // Skip all processing if already at max - completely invisible
                // A capped skill is still in use; keep its decay clock fresh.
                UpdateSkillActivityDay(playerUid, "hunger");
                if (playerProgress.TotalCredits >= playerMaxCredits) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Apply sleep buff multiplier to time (since tick is every 1000ms)
                float modifiedSeconds = ApplyXPMultiplier(playerUid, 1f);

                // Add time to progress
                playerProgress.SecondsInIncrement += modifiedSeconds;

                // Check if we've earned any new credits
                while (playerProgress.SecondsInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < playerMaxCredits)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.SecondsInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += HungerIncrementStep;

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned hunger credit {playerProgress.TotalCredits}/{playerMaxCredits}, next requires {playerProgress.CurrentIncrementSize} seconds");
                }

                // Mark for saving if any progress was made
                if (playerProgress.SecondsInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingHungerProgressSave = true;
                    // Time at full saturation is activity for decay purposes even
                    // between level-ups; increments get long at high levels.
                    UpdateSkillActivityDay(playerUid, "hunger");
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyHungerBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when cancelling Ravenous)
                    NotifyLevelUp(player,
                        Lang.Get("seraphleveling:message-hunger-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));
                }
            }
        }
    }
}
