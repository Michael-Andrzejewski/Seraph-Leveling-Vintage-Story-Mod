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

        // Keys for walking speed progression system
        public const string WALKING_STAT_CODE = "sitWalkingBonus";
        private const string WALKING_PROGRESS_SAVE_KEY = "sitWalkingProgress";

        // WatchedAttributes keys for client sync (walking)
        public const string WATCHED_WALKING_LEVEL = "sitWalkingLevel";
        public const string WATCHED_WALKING_BONUS = "sitWalkingBonusPercent";

        // Trait code for the walking speed mastery trait (Fleetfooted)
        public const string WALKING_TRAIT_CODE = "sitwalkingmastery";

        // Walking speed progression configuration
        // Base blocks for first 1%: 1000 blocks
        // Each subsequent 1% requires +1000 more blocks (1000, 2000, 3000, etc.)
        public static int BaseBlocksWalkedPerIncrement = 1000;  // Base blocks needed for first credit
        public static int WalkingIncrementStep = 1000;          // How much more blocks each subsequent credit needs
        public static int MaxWalkingSpeedPercent = 15;          // 15% max bonus (115% total speed)

        // Vanilla Fleetfooted trait walk speed bonus (used for cap calculations)
        public const int VANILLA_FLEETFOOTED_WALK_BONUS = 10;

        // Storage for walking progress - keyed by player UID
        public static ConcurrentDictionary<string, WalkingProgressData> WalkingProgress = new ConcurrentDictionary<string, WalkingProgressData>();

        // Flag to indicate pending walking progress save
        private static volatile bool pendingWalkingProgressSave = false;

        // Tracking last known positions for walking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastPlayerPositions = new ConcurrentDictionary<string, Position2D>();

        // Maximum distance per tick to count (prevents teleportation from counting)
        private const float MAX_DISTANCE_PER_TICK = 10f;

        /// <summary>
        /// Handler for /trait walking command.
        /// </summary>
        private TextCommandResult OnTraitWalkingCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateWalkingBonusPercent(currentCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Walking progression: {currentCredits}% / {MaxWalkingSpeedPercent}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% walk speed");
            sb.AppendLine($"Progress: {progress.BlocksInIncrement:F1}/{progress.CurrentIncrementSize} blocks");

            if (currentCredits >= MaxWalkingSpeedPercent)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait walkingbase command.
        /// Sets the base blocks needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitWalkingBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksWalkedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksWalkedPerIncrement}. New progress will require this many blocks for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksWalkedPerIncrement}\nIncrement step: +{WalkingIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait walkingincrement command.
        /// Sets how many additional blocks are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitWalkingIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                WalkingIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Walking increment step set to +{WalkingIncrementStep} per credit.\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current walking increment step: +{WalkingIncrementStep} per credit\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait walkinglevel command.
        /// Gets or sets the player's walking credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitWalkingLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });

            int? newCredits = OptInt(args, 0);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateWalkingBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current walking level: {progress.TotalCredits}/{MaxWalkingSpeedPercent} (+{currentBonus}% walk speed)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxWalkingSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxWalkingSpeedPercent})");
            }

            // Set the player's progress
            progress.TotalCredits = newCredits.Value;
            progress.BlocksInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseBlocksWalkedPerIncrement + (newCredits.Value * WalkingIncrementStep);


            // Apply the bonus
            int bonusPercent = ApplyWalkingBonusStatic(player, newCredits.Value);

            UpdateSkillActivityDay(playerUid, "walking");

            return TextCommandResult.Success($"Walking credits set to {newCredits.Value} (+{bonusPercent}% walk speed).");
        }

        /// <summary>
        /// Handler for /trait walkingmax command.
        /// Gets or sets the maximum walking speed bonus percent.
        /// </summary>
        private TextCommandResult OnTraitWalkingMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max walking speed percent must be at least 1");
                }

                MaxWalkingSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
                    {
                        CurrentIncrementSize = BaseBlocksWalkedPerIncrement
                    });
                    ApplyWalkingBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max walking speed bonus set to +{MaxWalkingSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max walking speed bonus: +{MaxWalkingSpeedPercent}%");
            }
        }

        /// <summary>
        /// Calculate the walking speed bonus as an integer percentage.
        /// Accounts for vanilla Fleetfooted trait (+10% walk speed).
        /// </summary>
        public static int CalculateWalkingBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasFleetfooted = entity != null && PlayerHasVanillaFleetfootedStatic(entity);
            int vanillaBonus = hasFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxWalkingSpeedPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Fleetfooted trait.
        /// </summary>
        private static bool PlayerHasVanillaFleetfootedStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "fleetfooted");
        }

        /// <summary>
        /// Apply walking speed bonus to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static int ApplyWalkingBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFleetfooted = cache?.HasFleetfooted ?? PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (MaxWalkingSpeedPercent - CapCounted(vanillaFleetfootedBonus)) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("walkspeed", WALKING_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_WALKING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_WALKING_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent)
                || watchedAttrs.GetBool("sitHasVanillaFleetfooted") != hasVanillaFleetfooted;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_WALKING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_WALKING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

                // Add our trait to extraTraits only if player doesn't already have Fleetfooted
                UpdateExtraTraitStatic(player.Entity, WALKING_TRAIT_CODE, level > 0 && !hasVanillaFleetfooted);

                watchedAttrs.MarkPathDirty(WATCHED_WALKING_LEVEL);
            }

            return bonusPercent;
        }

        /// <summary>
        /// Called every 500ms to track walking distance for all online players.
        /// Calculates 2D horizontal distance moved (ignoring Y-axis for climbing/falling).
        /// </summary>
        private void OnWalkingTick(float dt)
        {
            // Skip walking progression if disabled
            if (IsSkillDisabled("walking")) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;

                string playerUid = player.PlayerUID;
                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last position (using Position2D struct to avoid Vec3d allocations)
                if (!lastPlayerPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastPlayerPositions[playerUid] = new Position2D(currentX, currentZ);
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
                var playerProgress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
                {
                    CurrentIncrementSize = BaseBlocksWalkedPerIncrement
                });

                // Skip all processing if already at max - completely invisible
                // A capped skill is still in use; keep its decay clock fresh.
                UpdateSkillActivityDay(playerUid, "walking");
                if (playerProgress.TotalCredits >= MaxWalkingSpeedPercent) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Apply sleep buff multiplier to distance
                float modifiedDistance = ApplyXPMultiplier(playerUid, distance);

                // Add distance to progress
                playerProgress.BlocksInIncrement += modifiedDistance;

                // Check if we've earned any new credits
                while (playerProgress.BlocksInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxWalkingSpeedPercent)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.BlocksInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += WalkingIncrementStep;

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned walking credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    // Walking counts as activity for decay even between level-ups.
                    // Without this the decay timer only reset on admin commands.
                    UpdateSkillActivityDay(playerUid, "walking");
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyWalkingBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    NotifyLevelUp(player,
                        Lang.Get("seraphleveling:message-walking-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));
                }
            }
        }
    }
}
