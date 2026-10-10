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
        // RESOURCEFUL TRAIT - Tracks animal harvesting for loot/speed bonuses
        // =========================================================================
        public const string RESOURCEFUL_LOOT_STAT_CODE = "sitResourcefulLoot";
        public const string RESOURCEFUL_SPEED_STAT_CODE = "sitResourcefulSpeed";
        private const string RESOURCEFUL_PROGRESS_SAVE_KEY = "sitResourcefulProgress";
        public const string WATCHED_RESOURCEFUL_LEVEL = "sitResourcefulLevel";
        public const string WATCHED_RESOURCEFUL_LOOT_BONUS = "sitResourcefulLootBonusPercent";
        public const string WATCHED_RESOURCEFUL_SPEED_BONUS = "sitResourcefulSpeedBonusPercent";
        public const string RESOURCEFUL_TRAIT_CODE = "sitresourcefulmastery";

        // Resourceful progression configuration
        public static int BaseResourcefulAnimalsPerIncrement = 10;  // Base animals for first credit
        public static int ResourcefulIncrementStep = 10;            // Increment step per credit
        public static int MaxResourcefulLootPercent = 20;           // 20% max animal loot bonus
        public static int MaxResourcefulSpeedPercent = 25;          // 25% max harvesting speed bonus

        // Vanilla Resourceful trait bonuses (Hunter/Malefactor)
        public const int VANILLA_RESOURCEFUL_LOOT_BONUS = 10;
        public const int VANILLA_RESOURCEFUL_SPEED_BONUS = 25;

        // Storage for resourceful progress
        public static ConcurrentDictionary<string, ResourcefulProgressData> ResourcefulProgress = new ConcurrentDictionary<string, ResourcefulProgressData>();
        private static volatile bool pendingResourcefulProgressSave = false;


        // =========================================================================
        // RESOURCEFUL TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait resourceful command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());
            int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
            int speedBonus = CalculateResourcefulSpeedBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Resourceful progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{lootBonus}% animal loot, +{speedBonus}% harvesting speed");
            if (hasVanillaResourceful)
            {
                sb.AppendLine($"(Has vanilla Resourceful trait)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                int remaining = progress.CurrentIncrementSize - progress.AnimalsInIncrement;
                sb.AppendLine($"Progress: {progress.AnimalsInIncrement} / {progress.CurrentIncrementSize} animals until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait resourcefulbase command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulBaseCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Base animals must be at least 1.",
            v => BaseResourcefulAnimalsPerIncrement = v,
            () => $"Resourceful base animals set to {BaseResourcefulAnimalsPerIncrement}.",
            () => $"Current resourceful base animals: {BaseResourcefulAnimalsPerIncrement}.");

        /// <summary>
        /// Handler for /trait resourcefullevel command.
        /// Gets or sets the player's resourceful level.
        /// </summary>
        private TextCommandResult OnTraitResourcefulLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current resourceful level: {progress.TotalCredits}/{maxCredits} (+{lootBonus}% loot)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.AnimalsInIncrement = 0;
            progress.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ResourcefulIncrementStep;
            }


            ApplyResourcefulBonusStatic(player, progress.TotalCredits);
            int newLootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);

            UpdateSkillActivityDay(player.PlayerUID, "resourceful");

            return TextCommandResult.Success($"Resourceful level set to {newLevel.Value} (+{newLootBonus}% loot).");
        }

        /// <summary>
        /// Handler for /trait resourcefulmax command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulMaxCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Max percent must be at least 1.",
            v => MaxResourcefulLootPercent = v,
            () => $"Resourceful max loot bonus set to {MaxResourcefulLootPercent}%.",
            () => $"Current resourceful max loot bonus: {MaxResourcefulLootPercent}%.");

        /// <summary>
        /// Calculate the resourceful loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateResourcefulLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaResourceful = entity != null && PlayerHasVanillaResourcefulStatic(entity);
            int vanillaBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxResourcefulLootPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the resourceful speed bonus as an integer percentage.
        /// Speed bonus scales indefinitely with level (1% per credit), no cap.
        /// </summary>
        public static int CalculateResourcefulSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            // Speed bonus scales indefinitely - 1% per credit, no cap
            return credits;
        }

        /// <summary>
        /// Get the maximum resourceful credits a player can earn based on their traits.
        /// Players with Kind trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxResourcefulCredits(EntityPlayer entity)
        {
            // Resourceful covers two stats with different caps (loot 20%, speed 25%).
            // The credit count must be high enough to fill BOTH stats to their cap, so:
            //   base = max(MaxLoot, MaxSpeed)
            //   Kind classes need an extra buffer of max(KindLootPenalty, KindSpeedPenalty)
            //     so the larger penalty is fully cancelled before earnable bonus starts.
            // Earlier versions used `MaxLoot + KindSpeedPenalty` (20 + 25 = 45) which was
            // too low — Tailor would land at +20% speed instead of the +25% cap.
            int baseMax = Math.Max(MaxResourcefulLootPercent, MaxResourcefulSpeedPercent);
            if (entity == null) return baseMax;

            // Class cap offsets: no extra credits to pay off starting penalties.
            if (EnableClassCapOffsets) return baseMax;

            bool hasKind = PlayerHasVanillaKind(entity);
            if (hasKind)
            {
                int largestKindPenalty = Math.Max(VANILLA_KIND_LOOT_PENALTY, VANILLA_KIND_SPEED_PENALTY);
                return baseMax + largestKindPenalty;
            }

            return baseMax;
        }

        /// <summary>
        /// Check if player has vanilla Resourceful trait.
        /// </summary>
        private static bool PlayerHasVanillaResourcefulStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "resourceful");
        }

        /// <summary>
        /// Apply resourceful bonus.
        /// Also handles Kind negative trait cancellation.
        /// </summary>
        private static void ApplyResourcefulBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            // If resourceful skill is disabled, set bonus to 0 and return
            if (IsSkillDisabled("resourceful"))
            {
                player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, 0f, false);
                player.Entity.Stats.Set("animalHarvestingTime", RESOURCEFUL_SPEED_STAT_CODE, 0f, false);
                return;
            }

            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            bool hasKind = PlayerHasVanillaKind(player.Entity);

            // Calculate remaining Kind penalties
            int kindLootRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_LOOT_PENALTY, level) : 0;
            int kindSpeedRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_SPEED_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netLootLevel = level;
            int netSpeedLevel = level;

            if (hasKind)
            {
                // Kind penalties are cancelled first, then bonuses start
                netLootLevel = Math.Max(0, level - VANILLA_KIND_LOOT_PENALTY);
                netSpeedLevel = Math.Max(0, level - VANILLA_KIND_SPEED_PENALTY);
            }

            // Apply vanilla caps if player has Resourceful trait
            int vanillaLootBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int vanillaSpeedBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_SPEED_BONUS : 0;

            int maxEarnableLoot = Math.Max(0, MaxResourcefulLootPercent - CapCounted(vanillaLootBonus));
            int maxEarnableSpeed = Math.Max(0, MaxResourcefulSpeedPercent - CapCounted(vanillaSpeedBonus));

            int lootBonusPercent = Math.Min(netLootLevel, maxEarnableLoot);
            int speedBonusPercent = Math.Min(netSpeedLevel, maxEarnableSpeed);

            float lootBonus = lootBonusPercent * 0.01f;
            float speedBonus = speedBonusPercent * 0.01f;

            // Apply to resourceful-related stats
            // Note: Stats use WeightedSum blending with a base of 1.0. Vanilla traits set values
            // like 0.1 for +10%. We set just the bonus value, not 1 + bonus.
            player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, lootBonus, false);
            // The stat is animalHarvestingTime, which multiplies the harvest duration, so a
            // faster harvest is a negative value. Vanilla Resourceful stores -0.25 for it.
            // There is no stat called harvestingSpeedMul, so the old code went nowhere.
            player.Entity.Stats.Set("animalHarvestingTime", RESOURCEFUL_SPEED_STAT_CODE, -speedBonus, false);

            // Counter-stats for Kind (Tailor): when each penalty is fully cancelled, apply a
            // counter on the same stat vanilla uses so functional matches displayed cap.
            // Kind sets animalLootDropRate -0.10 and animalHarvestingTime +0.25.
            if (hasKind)
            {
                if (kindLootRemaining == 0)
                    player.Entity.Stats.Set("animalLootDropRate", "sitKindLootCancel", VANILLA_KIND_LOOT_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("animalLootDropRate", "sitKindLootCancel");

                if (kindSpeedRemaining == 0)
                    // Kind uses animalHarvestingTime where positive = slower; cancel with negative.
                    player.Entity.Stats.Set("animalHarvestingTime", "sitKindSpeedCancel", -VANILLA_KIND_SPEED_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("animalHarvestingTime", "sitKindSpeedCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("animalLootDropRate", "sitKindLootCancel");
                player.Entity.Stats.Remove("animalHarvestingTime", "sitKindSpeedCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LOOT_BONUS, lootBonusPercent);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_SPEED_BONUS, speedBonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaResourceful", hasVanillaResourceful);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasKind", hasKind);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_LOOT_REMAINING, kindLootRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_SPEED_REMAINING, kindSpeedRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_RESOURCEFUL_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, RESOURCEFUL_TRAIT_CODE, level > 0 && !hasVanillaResourceful);
        }

        /// <summary>
        /// Process animal harvested (called from Harmony patch when player harvests an animal).
        /// </summary>
        public static void ProcessAnimalHarvested(IServerPlayer player)
        {
            ProcessAnimalHarvestedCore(player);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessAnimalHarvestedCore(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if resourceful skill is disabled
            if (IsSkillDisabled("resourceful")) return;

            string playerUid = player.PlayerUID;
            var progress = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "resourceful");
            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedAnimals = ApplyXPMultiplier(playerUid, 1);
            progress.AnimalsInIncrement += modifiedAnimals;

            while (progress.AnimalsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.AnimalsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ResourcefulIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned resourceful credit {progress.TotalCredits}");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "resourceful");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyResourcefulBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Kind)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-resourceful-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }
    }
}
