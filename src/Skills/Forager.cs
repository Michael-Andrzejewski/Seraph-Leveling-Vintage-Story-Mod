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
        // FORAGER TRAIT - Tracks wild crop breaking for foraging loot bonuses
        // =========================================================================
        public const string FORAGER_LOOT_STAT_CODE = "sitForagerLoot";
        public const string FORAGER_WILD_CROP_STAT_CODE = "sitForagerWildCrop";
        private const string FORAGER_PROGRESS_SAVE_KEY = "sitForagerProgress";
        public const string WATCHED_FORAGER_LEVEL = "sitForagerLevel";
        public const string WATCHED_FORAGER_LOOT_BONUS = "sitForagerLootBonusPercent";
        public const string WATCHED_FORAGER_WILD_CROP_BONUS = "sitForagerWildCropBonusPercent";
        public const string FORAGER_TRAIT_CODE = "sitforagermastery";

        // Forager progression configuration
        public static int BaseForagerCropsPerIncrement = 10;    // Base crops for first credit
        public static int ForagerIncrementStep = 10;            // Increment step per credit
        public static int MaxForagerLootPercent = 20;           // 20% max foraging loot bonus
        public static int MaxForagerWildCropPercent = 20;       // 20% max wild crop drop bonus

        // Vanilla Forager trait bonuses (Hunter/Malefactor)
        public const int VANILLA_FORAGER_LOOT_BONUS = 10;
        public const int VANILLA_FORAGER_WILD_CROP_BONUS = 20;

        // Storage for forager progress
        public static ConcurrentDictionary<string, ForagerProgressData> ForagerProgress = new ConcurrentDictionary<string, ForagerProgressData>();
        private static volatile bool pendingForagerProgressSave = false;

        // =========================================================================
        // FORAGER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait forager command.
        /// </summary>
        private TextCommandResult OnTraitForagerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());
            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);
            int maxCredits = GetMaxForagerCredits(player.Entity);

            // Use net bonuses from WatchedAttributes (set by ApplyForagerBonusStatic)
            int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);

            var sb = new StringBuilder();
            sb.AppendLine($"Forager progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{netLootBonus}% foraging loot, +{netWildCropBonus}% wild crop drops");
            if (hasVanillaForager)
            {
                sb.AppendLine($"(Has vanilla Forager trait)");
            }
            if (hasCivil)
            {
                int civilRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_CIVIL_REMAINING, 0);
                if (civilRemaining > 0)
                    sb.AppendLine($"(Civil penalty remaining: -{civilRemaining}% foraging loot)");
                else
                    sb.AppendLine("(Civil penalty cancelled!)");
            }
            if (hasHeavyhanded)
            {
                int forageRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, 0);
                int wildCropRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, 0);
                if (forageRemaining > 0 || wildCropRemaining > 0)
                    sb.AppendLine($"(Heavyhanded penalties remaining: -{forageRemaining}% foraging, -{wildCropRemaining}% wild crop)");
                else
                    sb.AppendLine("(Heavyhanded penalties cancelled!)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                sb.AppendLine($"Progress: {progress.CropsInIncrement} / {progress.CurrentIncrementSize} crops until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait foragerbase command.
        /// </summary>
        private TextCommandResult OnTraitForagerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base crops must be at least 1.");
                BaseForagerCropsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager base crops set to {BaseForagerCropsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current forager base crops: {BaseForagerCropsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait foragerlevel command.
        /// Gets or sets the player's forager level.
        /// </summary>
        private TextCommandResult OnTraitForagerLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
                int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);
                return TextCommandResult.Success($"Current forager level: {progress.TotalCredits}/{maxCredits} (+{netLootBonus}% loot, +{netWildCropBonus}% wild crop)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.CropsInIncrement = 0;
            progress.CurrentIncrementSize = BaseForagerCropsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ForagerIncrementStep;
            }

            pendingForagerProgressSave = true;

            ApplyForagerBonusStatic(player, progress.TotalCredits);
            // Use net bonuses from WatchedAttributes which accounts for Civil/Heavyhanded penalties
            int newNetLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int newNetWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);

            UpdateSkillActivityDay(player.PlayerUID, "forager");

            return TextCommandResult.Success($"Forager level set to {newLevel.Value} (+{newNetLootBonus}% loot, +{newNetWildCropBonus}% wild crop).");
        }

        /// <summary>
        /// Handler for /trait foragermax command.
        /// </summary>
        private TextCommandResult OnTraitForagerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxForagerLootPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager max loot bonus set to {MaxForagerLootPercent}%.");
            }

            return TextCommandResult.Success($"Current forager max loot bonus: {MaxForagerLootPercent}%.");
        }

        /// <summary>
        /// Calculate the forager loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerLootPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the forager wild crop bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerWildCropBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_WILD_CROP_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerWildCropPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Forager trait.
        /// </summary>
        private static bool PlayerHasVanillaForagerStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "forager");
        }

        /// <summary>
        /// Get the maximum forager credits a player can earn based on their traits.
        /// Players with negative traits (Civil, Heavyhanded) can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxForagerCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxForagerLootPercent;

            // Class cap offsets: no extra credits to pay off starting penalties.
            if (EnableClassCapOffsets) return MaxForagerLootPercent;

            bool hasCivil = PlayerHasVanillaCivil(entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Civil penalty is 10% foraging loot, need 10 extra levels to cancel it
            if (hasCivil)
            {
                return MaxForagerLootPercent + VANILLA_CIVIL_FORAGING_PENALTY;
            }

            // Heavyhanded has two penalties - use the larger one (wild crop = 20%)
            if (hasHeavyhanded)
            {
                return MaxForagerWildCropPercent + VANILLA_HEAVYHANDED_WILD_CROP_PENALTY;
            }

            return MaxForagerLootPercent;
        }

        /// <summary>
        /// Apply forager bonus.
        /// Also handles Civil and Heavyhanded negative trait cancellation.
        /// </summary>
        private static void ApplyForagerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            int lootBonusPercent = CalculateForagerLootBonusPercent(level, player.Entity);
            int wildCropBonusPercent = CalculateForagerWildCropBonusPercent(level, player.Entity);

            // Calculate remaining negative trait penalties
            int civilRemaining = hasCivil ? CalculateRemainingPenalty(VANILLA_CIVIL_FORAGING_PENALTY, level) : 0;
            int heavyhandedForagingRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_FORAGING_PENALTY, level) : 0;
            int heavyhandedWildCropRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_WILD_CROP_PENALTY, level) : 0;

            // Calculate net bonus (earned bonus - remaining penalty)
            // For Civil: need to earn level > 10 to start gaining bonus
            // For Heavyhanded: need to earn level > 15 for foraging, > 20 for wild crop
            int netLootBonus = lootBonusPercent;
            int netWildCropBonus = wildCropBonusPercent;

            if (hasCivil)
            {
                // Civil penalty is cancelled first, then bonus starts
                netLootBonus = Math.Max(0, level - VANILLA_CIVIL_FORAGING_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                }
            }

            if (hasHeavyhanded)
            {
                // Heavyhanded penalties are cancelled first
                netLootBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_FORAGING_PENALTY);
                netWildCropBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_WILD_CROP_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                    netWildCropBonus = Math.Min(netWildCropBonus, MaxForagerWildCropPercent);
                }
            }

            float lootBonus = netLootBonus * 0.01f;
            float wildCropBonus = netWildCropBonus * 0.01f;

            // Apply to forager-related stats
            // Note: forageDropRate/wildCropDropRate are additive stats where vanilla traits use
            // values like 0.1 for +10%. The game applies (1 + blended) as the multiplier.
            // Using just the bonus value (not 1 + bonus) to avoid doubling.
            player.Entity.Stats.Set("forageDropRate", FORAGER_LOOT_STAT_CODE, lootBonus, false);
            player.Entity.Stats.Set("wildCropDropRate", FORAGER_WILD_CROP_STAT_CODE, wildCropBonus, false);

            // Counter-stats: when negative trait penalties are fully cancelled, apply +penalty
            // counters so functional foraging stats match the displayed cap. Civil affects loot
            // (Tailor); Heavyhanded affects loot AND wild crop (Blackguard).
            if (hasCivil)
            {
                if (civilRemaining == 0)
                    player.Entity.Stats.Set("forageDropRate", "sitCivilForagingCancel", VANILLA_CIVIL_FORAGING_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("forageDropRate", "sitCivilForagingCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("forageDropRate", "sitCivilForagingCancel");
            }
            if (hasHeavyhanded)
            {
                if (heavyhandedForagingRemaining == 0)
                    player.Entity.Stats.Set("forageDropRate", "sitHeavyhandedForagingCancel", VANILLA_HEAVYHANDED_FORAGING_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("forageDropRate", "sitHeavyhandedForagingCancel");

                if (heavyhandedWildCropRemaining == 0)
                    player.Entity.Stats.Set("wildCropDropRate", "sitHeavyhandedWildCropCancel", VANILLA_HEAVYHANDED_WILD_CROP_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("wildCropDropRate", "sitHeavyhandedWildCropCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("forageDropRate", "sitHeavyhandedForagingCancel");
                player.Entity.Stats.Remove("wildCropDropRate", "sitHeavyhandedWildCropCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LOOT_BONUS, netLootBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_WILD_CROP_BONUS, netWildCropBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaForager", hasVanillaForager);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasCivil", hasCivil);
            player.Entity.WatchedAttributes.SetInt(WATCHED_CIVIL_REMAINING, civilRemaining);
            player.Entity.WatchedAttributes.SetBool("sitHasHeavyhanded", hasHeavyhanded);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, heavyhandedForagingRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, heavyhandedWildCropRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_FORAGER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, FORAGER_TRAIT_CODE, level > 0 && !hasVanillaForager);
        }

        /// <summary>
        /// Process wild crop broken (for Forager progression).
        /// </summary>
        public static void ProcessWildCropBroken(IServerPlayer player)
        {
            ProcessWildCropBrokenCore(player);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessWildCropBrokenCore(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "forager");
            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedCrops = ApplyXPMultiplier(playerUid, 1);
            progress.CropsInIncrement += modifiedCrops;

            while (progress.CropsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.CropsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ForagerIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned forager credit {progress.TotalCredits}");
            }

            pendingForagerProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "forager");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyForagerBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Civil/Heavyhanded)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-forager-level-up", progress.TotalCredits, progress.TotalCredits, progress.TotalCredits));
            }
        }

        /// <summary>
        /// Check if a block is a wild crop (for Forager progression).
        /// Wild crops are crops like turnip, flax, spelt that grow on dirt/soil (not farmland).
        /// Berry bushes are NOT counted since they can be replanted infinitely.
        /// </summary>
        private static bool IsWildCropBlock(int blockId, BlockPos blockPos)
        {
            // Crops count when they grow on natural ground (not farmland); with no
            // position to check, only when the code says "wild". Mushrooms count.
            // Tallgrass, flowers, berries and wild vines do not (replantable or too common).
            // The code checks run once per block type (KindOfBlock).
            if (ServerApi == null) return false;
            var kind = KindOfBlock(blockId);
            if ((kind & BlockKind.Crop) != 0)
            {
                if (blockPos != null)
                {
                    var below = KindOfBlock(ServerApi.World.BlockAccessor.GetBlock(blockPos.DownCopy())?.Id ?? -1);
                    if ((below & BlockKind.Farmland) != 0) return false;
                    if ((below & BlockKind.NaturalGround) != 0) return true;
                }
                return (kind & BlockKind.CropMarkedWild) != 0;
            }
            return (kind & BlockKind.Mushroom) != 0;
        }
    }
}
