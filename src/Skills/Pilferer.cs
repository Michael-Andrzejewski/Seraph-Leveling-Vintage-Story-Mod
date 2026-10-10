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
        // PILFERER TRAIT - Tracks chests/vessels for loot bonuses
        // =========================================================================
        public const string PILFERER_RUSTY_GEAR_STAT_CODE = "sitPilfererRustyGear";
        public const string PILFERER_VESSEL_CONTENTS_STAT_CODE = "sitPilfererVesselContents";
        public const string PILFERER_WHOLE_VESSEL_STAT_CODE = "sitPilfererWholeVessel";
        private const string PILFERER_PROGRESS_SAVE_KEY = "sitPilfererProgress";
        public const string WATCHED_PILFERER_LEVEL = "sitPilfererLevel";
        public const string WATCHED_PILFERER_BONUS = "sitPilfererBonusPercent";
        // Per-stat displayed bonuses. Pilferer's three stats have different vanilla values
        // (vessel +15%, rusty gear +10%, whole vessel +12%), so a single shared bonus value
        // can't drive all three to the same cap simultaneously for Malefactor (vanilla
        // Pilferer). Tracking each stat's earned amount independently keeps every class at
        // exactly +20% per stat at maxall.
        public const string WATCHED_PILFERER_VESSEL_BONUS = "sitPilfererVesselBonus";
        public const string WATCHED_PILFERER_RUSTY_BONUS = "sitPilfererRustyBonus";
        public const string WATCHED_PILFERER_WHOLE_BONUS = "sitPilfererWholeBonus";
        public const string PILFERER_TRAIT_CODE = "sitpilferermastery";

        // Pilferer progression configuration
        public static int BasePilfererPointsPerIncrement = 10;  // Base points for first credit
        public static int PilfererIncrementStep = 10;           // Increment step per credit
        public static int MaxPilfererPercent = 20;              // 20% max bonus for all three stats
        public const int PILFERER_VESSEL_POINTS = 2;            // Points per broken loot vessel

        // Vanilla Pilferer trait bonuses (Malefactor exclusive)
        public const int VANILLA_PILFERER_RUSTY_GEAR_BONUS = 10;
        public const int VANILLA_PILFERER_VESSEL_CONTENTS_BONUS = 15;
        public const int VANILLA_PILFERER_WHOLE_VESSEL_BONUS = 12;

        // Storage for pilferer progress
        public static ConcurrentDictionary<string, PilfererProgressData> PilfererProgress = new ConcurrentDictionary<string, PilfererProgressData>();
        private static volatile bool pendingPilfererProgressSave = false;

        // =========================================================================
        // PILFERER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait pilferer command.
        /// </summary>
        private TextCommandResult OnTraitPilfererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());
            int bonusPercent = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Pilferer progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% rusty gear, vessel contents, and collection chance");
            if (hasVanillaPilferer)
            {
                sb.AppendLine($"(Has vanilla Pilferer trait)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                int remaining = progress.CurrentIncrementSize - progress.PointsInIncrement;
                sb.AppendLine($"Progress: {progress.PointsInIncrement} / {progress.CurrentIncrementSize} points until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait pilfererbase command.
        /// </summary>
        private TextCommandResult OnTraitPilfererBaseCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Base points must be at least 1.",
            v => BasePilfererPointsPerIncrement = v,
            () => $"Pilferer base points set to {BasePilfererPointsPerIncrement}.",
            () => $"Current pilferer base points: {BasePilfererPointsPerIncrement}.");

        /// <summary>
        /// Handler for /trait pilfererlevel command.
        /// Gets or sets the player's pilferer level.
        /// </summary>
        private TextCommandResult OnTraitPilfererLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current pilferer level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% bonuses)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.PointsInIncrement = 0;
            progress.CurrentIncrementSize = BasePilfererPointsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += PilfererIncrementStep;
            }


            int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);

            UpdateSkillActivityDay(player.PlayerUID, "pilferer");

            return TextCommandResult.Success($"Pilferer level set to {newLevel.Value} (+{bonusPercent}% bonuses).");
        }

        /// <summary>
        /// Handler for /trait pilferermax command.
        /// </summary>
        private TextCommandResult OnTraitPilfererMaxCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Max percent must be at least 1.",
            v => MaxPilfererPercent = v,
            () => $"Pilferer max bonus set to {MaxPilfererPercent}%.",
            () => $"Current pilferer max bonus: {MaxPilfererPercent}%.");

        /// <summary>
        /// Calculate the pilferer bonus as an integer percentage.
        /// </summary>
        public static int CalculatePilfererBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaPilferer = entity != null && PlayerHasVanillaPilfererStatic(entity);
            int vanillaBonus = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxPilfererPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Get the maximum pilferer credits a player can earn based on their traits.
        /// Players with Heavyhanded trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxPilfererCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxPilfererPercent;

            // Class cap offsets: no extra credits to pay off starting penalties.
            if (EnableClassCapOffsets) return MaxPilfererPercent;

            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Heavyhanded vessel penalty is 10%, need 10 extra levels to cancel it
            if (hasHeavyhanded)
            {
                return MaxPilfererPercent + VANILLA_HEAVYHANDED_VESSEL_PENALTY;
            }

            return MaxPilfererPercent;
        }

        /// <summary>
        /// Check if player has vanilla Pilferer trait.
        /// </summary>
        private static bool PlayerHasVanillaPilfererStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "pilferer");
        }

        /// <summary>
        /// Apply pilferer bonus.
        /// Also handles Heavyhanded vessel loot negative trait cancellation.
        /// </summary>
        private static int ApplyPilfererBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            // Calculate remaining Heavyhanded vessel penalty
            int heavyhandedVesselRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_VESSEL_PENALTY, level) : 0;

            // Calculate net level after cancelling Heavyhanded's vessel penalty (only affects vessel stat)
            int netLevel = level;
            if (hasHeavyhanded)
            {
                netLevel = Math.Max(0, level - VANILLA_HEAVYHANDED_VESSEL_PENALTY);
            }

            // Per-stat earned bonuses. Pilferer's three stats have different vanilla values,
            // so we compute the earnable cap per stat (MaxPilfererPercent - vanilla_for_that_stat).
            // Earlier code used a single shared `bonusPercent` based on the rusty-gear cap,
            // which let Malefactor's vessel total exceed +20% (15 vanilla + 10 earned = 25)
            // and left rusty/whole below the cap. Splitting per-stat keeps every class at
            // exactly +20% per stat at maxall.
            int vanillaVessel = hasVanillaPilferer ? VANILLA_PILFERER_VESSEL_CONTENTS_BONUS : 0;
            int vanillaRusty = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int vanillaWhole = hasVanillaPilferer ? VANILLA_PILFERER_WHOLE_VESSEL_BONUS : 0;

            int earnableVessel = Math.Max(0, MaxPilfererPercent - CapCounted(vanillaVessel));
            int earnableRusty = Math.Max(0, MaxPilfererPercent - CapCounted(vanillaRusty));
            int earnableWhole = Math.Max(0, MaxPilfererPercent - CapCounted(vanillaWhole));

            int vesselBonus = Math.Min(netLevel, earnableVessel);
            int rustyBonus = Math.Min(level, earnableRusty);  // rusty/whole aren't affected by Heavyhanded
            int wholeBonus = Math.Min(level, earnableWhole);

            // Apply per-stat. These additive stats use values like 0.1 for +10%; the game
            // applies (1 + blended) as the final multiplier.
            player.Entity.Stats.Set("rustyGearDropRate", PILFERER_RUSTY_GEAR_STAT_CODE, rustyBonus * 0.01f, false);
            player.Entity.Stats.Set("vesselContentsDropRate", PILFERER_VESSEL_CONTENTS_STAT_CODE, vesselBonus * 0.01f, false);
            player.Entity.Stats.Set("wholeVesselLootChance", PILFERER_WHOLE_VESSEL_STAT_CODE, wholeBonus * 0.01f, false);

            // Counter-stat: when Heavyhanded vessel penalty is fully cancelled, apply +10% to
            // negate the vanilla -10% so functional vessel drop rate hits the displayed cap.
            if (hasHeavyhanded)
            {
                if (heavyhandedVesselRemaining == 0)
                    player.Entity.Stats.Set("vesselContentsDropRate", "sitHeavyhandedVesselCancel", VANILLA_HEAVYHANDED_VESSEL_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("vesselContentsDropRate", "sitHeavyhandedVesselCancel");
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("vesselContentsDropRate", "sitHeavyhandedVesselCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_LEVEL, level);
            // Keep WATCHED_PILFERER_BONUS as the max of the three for any legacy readers
            // (display now uses the per-stat values instead).
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_BONUS, Math.Max(vesselBonus, Math.Max(rustyBonus, wholeBonus)));
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_VESSEL_BONUS, vesselBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_RUSTY_BONUS, rustyBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_WHOLE_BONUS, wholeBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPilferer", hasVanillaPilferer);

            // Sync negative trait status (Heavyhanded vessel part)
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_VESSEL_REMAINING, heavyhandedVesselRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_PILFERER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, PILFERER_TRAIT_CODE, level > 0 && !hasVanillaPilferer);

            // Return the largest earned bonus (caller is informational; all three may differ)
            return Math.Max(vesselBonus, Math.Max(rustyBonus, wholeBonus));
        }

        /// <summary>
        /// Process cracked vessel break (called from OnBlockBroken for cracked vessels).
        /// Only cracked vessels count - they can't be re-placed by players.
        /// </summary>
        public static void ProcessVesselBreak(IServerPlayer player)
        {
            ProcessVesselBreakCore(player);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessVesselBreakCore(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "pilferer");
            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedPoints = ApplyXPMultiplier(playerUid, PILFERER_VESSEL_POINTS);
            progress.PointsInIncrement += modifiedPoints;

            while (progress.PointsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.PointsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += PilfererIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned pilferer credit {progress.TotalCredits} from cracked vessel");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "pilferer");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyPilfererBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Heavyhanded)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-pilferer-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }

        /// <summary>
        /// Check if a block is a loot vessel / cracked vessel (for Pilferer progression).
        /// Only loot vessels count - they can't be re-placed by players, preventing exploits.
        /// Storage vessels and urns are excluded since players can place and break them repeatedly.
        /// Block code: game:lootvessel-*
        /// </summary>
        private static bool IsCrackedVesselBlock(int blockId)
        {
            // Only loot vessels (cracked vessels) count - they don't drop themselves when broken
            return (KindOfBlock(blockId) & BlockKind.LootVessel) != 0;
        }
    }
}
