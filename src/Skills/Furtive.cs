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
        // FURTIVE TRAIT - Tracks sneaking blocks for animal detection range reduction
        // =========================================================================
        public const string FURTIVE_STAT_CODE = "sitFurtiveBonus";
        private const string FURTIVE_PROGRESS_SAVE_KEY = "sitFurtiveProgress";
        public const string WATCHED_FURTIVE_LEVEL = "sitFurtiveLevel";
        public const string WATCHED_FURTIVE_BONUS = "sitFurtiveBonusPercent";
        public const string FURTIVE_TRAIT_CODE = "sitfurtivemastery";

        // Furtive progression configuration
        public static int BaseFurtiveSneakBlocksPerIncrement = 100;  // Base sneaking blocks for first credit
        public static int FurtiveIncrementStep = 100;                 // Increment step per credit
        public static int MaxFurtivePercent = 35;                     // 35% max animal detection range reduction

        // Vanilla Furtive trait bonus (Malefactor)
        public const int VANILLA_FURTIVE_DETECTION_REDUCTION = 35;

        // Storage for furtive progress
        public static ConcurrentDictionary<string, FurtiveProgressData> FurtiveProgress = new ConcurrentDictionary<string, FurtiveProgressData>();
        private static volatile bool pendingFurtiveProgressSave = false;

        // Tracking last known positions for sneaking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastSneakingPositions = new ConcurrentDictionary<string, Position2D>();

        // =========================================================================
        // FURTIVE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Called every 500ms to track sneaking distance for all online players.
        /// Calculates 2D horizontal distance moved while sneaking (ignoring Y-axis).
        /// </summary>
        private void OnSneakingTick(float dt)
        {
            // Skip furtive progression if disabled
            if (IsSkillDisabled("furtive")) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;

                string playerUid = player.PlayerUID;

                // Check if player is sneaking
                bool isSneaking = player.Entity.Controls?.Sneak ?? false;

                if (!isSneaking)
                {
                    // Not sneaking, clear last position so movement doesn't count
                    lastSneakingPositions.TryRemove(playerUid, out _);
                    continue;
                }

                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last sneaking position (using Position2D struct to avoid Vec3d allocations)
                if (!lastSneakingPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastSneakingPositions[playerUid] = new Position2D(currentX, currentZ);
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentX - lastPos.X;
                double dz = currentZ - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position (no allocation - struct assignment)
                lastPos.X = currentX;
                lastPos.Z = currentZ;

                // Skip if no movement or teleportation (too far)
                if (distance < 0.01f || distance > MAX_DISTANCE_PER_TICK) continue;

                // Get or create player progress data
                var playerProgress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
                {
                    CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
                });

                // Skip all processing if already at max
                // A capped skill is still in use; keep its decay clock fresh.
                UpdateSkillActivityDay(playerUid, "furtive");
                if (playerProgress.TotalCredits >= MaxFurtivePercent) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Apply sleep buff multiplier to distance
                float modifiedDistance = ApplyXPMultiplier(playerUid, distance);

                // Add distance to progress
                playerProgress.BlocksInIncrement += modifiedDistance;

                // Check if we've earned any new credits
                while (playerProgress.BlocksInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxFurtivePercent)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.BlocksInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += FurtiveIncrementStep;

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned furtive credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingFurtiveProgressSave = true;
                    // Sneaking is activity for decay purposes even between
                    // level-ups; increments get long at high levels.
                    UpdateSkillActivityDay(playerUid, "furtive");
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyFurtiveBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    NotifyLevelUp(player,
                        Lang.Get("seraphleveling:message-furtive-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));
                }
            }
        }

        /// <summary>
        /// Apply the Furtive bonus to a player based on their earned credits.
        /// The bonus reduces animal detection range.
        /// </summary>
        private static int ApplyFurtiveBonusStatic(IServerPlayer player, int credits)
        {
            if (player?.Entity == null) return 0;

            // Check if player has vanilla Furtive trait (Malefactor)
            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);

            // Calculate effective cap (vanilla trait already gives max, so no additional bonus possible)
            int effectiveMax = hasVanillaFurtive ? 0 : MaxFurtivePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent (reduction in detection range)
            int bonusPercent = effectiveCredits;

            // animalSeekingRange blends as WeightedSum over a base of 1, so each
            // stat value is a delta, not a multiplier. Vanilla Furtive stores
            // -0.35 and ends up at 0.65, meaning animals see you at 65% of the
            // normal range. Writing 0.65 here would blend to 1.65 and let them
            // see you 65% further away, so the bonus has to be negative.
            if (bonusPercent > 0)
            {
                float statValue = -(bonusPercent / 100f);
                player.Entity.Stats.Set("animalSeekingRange", FURTIVE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove("animalSeekingRange", FURTIVE_STAT_CODE);
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaFurtive", hasVanillaFurtive);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, FURTIVE_TRAIT_CODE, credits > 0 && !hasVanillaFurtive);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Furtive trait (Malefactor).
        /// </summary>
        private static bool PlayerHasVanillaFurtiveStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            // The class is stored as the "characterClass" string attribute, not a
            // tree. Check the trait list first so custom classes with Furtive are
            // detected too, then fall back to the Malefactor class name.
            return PlayerHasVanillaTraitOrClassStatic(entity, "furtive", "malefactor");
        }

        // =========================================================================
        // NEW TRAIT COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait furtive command.
        /// </summary>
        private TextCommandResult OnTraitFurtiveCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
            int bonusPercent = hasVanillaFurtive ? 0 : progress.TotalCredits;

            var sb = new StringBuilder();
            sb.AppendLine($"Furtive progression: Level {progress.TotalCredits} / {MaxFurtivePercent}");
            sb.AppendLine($"Current bonus: -{bonusPercent}% animal detection range");
            if (hasVanillaFurtive)
            {
                sb.AppendLine($"Vanilla Furtive trait active: -{VANILLA_FURTIVE_DETECTION_REDUCTION}% detection (max reached)");
            }
            else if (progress.TotalCredits < MaxFurtivePercent)
            {
                float remaining = progress.CurrentIncrementSize - progress.BlocksInIncrement;
                sb.AppendLine($"Progress: {progress.BlocksInIncrement:F1} / {progress.CurrentIncrementSize} blocks sneaked");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait furtivelevel command.
        /// Gets or sets the player's furtive level.
        /// </summary>
        private TextCommandResult OnTraitFurtiveLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
                int currentBonus = hasVanillaFurtive ? VANILLA_FURTIVE_DETECTION_REDUCTION : progress.TotalCredits;
                return TextCommandResult.Success($"Current furtive level: {progress.TotalCredits}/{MaxFurtivePercent} (-{currentBonus}% detection)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxFurtivePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxFurtivePercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.BlocksInIncrement = 0;
            progress.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement + (newLevel.Value * FurtiveIncrementStep);

            pendingFurtiveProgressSave = true;
            int bonusPercent = ApplyFurtiveBonusStatic(player, newLevel.Value);

            UpdateSkillActivityDay(player.PlayerUID, "furtive");

            return TextCommandResult.Success($"Furtive level set to {newLevel.Value} (-{bonusPercent}% detection).");
        }
    }
}
