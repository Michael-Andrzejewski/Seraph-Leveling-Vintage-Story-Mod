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

        // Keys for mining progression system
        public const string BLOCKS_MINED_KEY = "sitBlocksMined";
        public const string MINING_STAT_CODE = "sitMiningBonus";
        private const string MINING_PROGRESS_SAVE_KEY = "sitMiningProgress";

        // WatchedAttributes keys for client sync
        public const string WATCHED_MINING_LEVEL = "sitMiningLevel";
        public const string WATCHED_MINING_BONUS = "sitMiningBonusPercent";

        // Trait code for the mining mastery trait
        public const string MINING_TRAIT_CODE = "sitminingmastery";

        // Mining progression configuration
        // Base blocks for first 1%: 100 blocks
        // Each subsequent 1% requires +100 more blocks (100, 200, 300, etc.)
        // Switching pickaxe types resets the increment counter back to base
        public static int BaseBlocksPerIncrement = 100;  // Base points needed for first credit
        public static int IncrementStep = 100;           // How much more points each subsequent credit needs
        public static int MaxMiningSpeedPercent = 50;    // 50% max bonus
        public static int OreMultiplier = 5;             // Ore blocks count for 5x points

        // Vanilla Hardy trait mining speed bonus (used for cap calculations)
        public const int VANILLA_HARDY_MINING_BONUS = 10;

        // Storage for mining progress - keyed by player UID
        public static ConcurrentDictionary<string, MiningProgressData> MiningProgress = new ConcurrentDictionary<string, MiningProgressData>();

        // Flag to indicate pending mining progress save
        private static volatile bool pendingMiningProgressSave = false;

        /// <summary>
        /// Handler for /trait mining command.
        /// </summary>
        private TextCommandResult OnTraitMiningCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMiningBonusPercent(currentCredits);
            int maxCredits = GetMaxMiningCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mining progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% mining speed");

            if (progress.PickaxeProgress.Count > 0)
            {
                sb.AppendLine("\nPer-pickaxe progress:");
                foreach (var kvp in progress.PickaxeProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string pickaxeName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (pickaxeName.StartsWith("game:", StringComparison.Ordinal))
                        pickaxeName = pickaxeName.Substring(5);

                    var pickProgress = kvp.Value;
                    sb.AppendLine($"  {pickaxeName}: {pickProgress.BlocksInIncrement}/{pickProgress.CurrentIncrementSize} points");
                }
            }
            else
            {
                sb.AppendLine("\nNo pickaxe progress yet. Mine stone or ore with a pickaxe to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait miningbase command.
        /// Sets the base points needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMiningBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksPerIncrement}. New pickaxes will require this many points for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksPerIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait miningincrement command.
        /// Sets how many additional points are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMiningIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                IncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Increment step set to +{IncrementStep} per credit.\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current increment step: +{IncrementStep} per credit\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait mininglevel command.
        /// Gets or sets the player's mining credits (level) directly.
        /// Optionally specify a tool name to set credits on a specific pickaxe without clearing other progress.
        /// </summary>
        private TextCommandResult OnTraitMiningLevelCommand(TextCommandCallingArgs args)
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
                int maxCredits = GetMaxMiningCredits(player.Entity);
                var progress = MiningProgress.GetOrAdd(player.PlayerUID, _ => new MiningProgressData());
                int currentBonus = CalculateMiningBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current mining level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% mining speed)");
            }

            string toolName = (string)args[1];
            return SetMiningLevelForPlayer(player, player.PlayerUID, newCredits.Value, toolName);
        }

        /// <summary>
        /// Gets the pickaxe code from the player's held item, or null if not holding a pickaxe.
        /// </summary>
        private string GetHeldPickaxeCode(IServerPlayer player)
        {
            if (player?.Entity == null) return null;

            var heldItem = HeldSlotOf(player)?.Itemstack?.Collectible;
            if (heldItem == null) return null;

            // Check if it's a pickaxe (Tool property = Pickaxe)
            if (heldItem.Tool != EnumTool.Pickaxe) return null;

            // Return the item code as the pickaxe identifier (cached per item type)
            return GearInfoOf(heldItem)?.Code;
        }

        /// <summary>
        /// Determines the point value for a broken block.
        /// Returns OreMultiplier (default 5) for ore blocks, 1 for stone blocks, 0 for other blocks.
        ///
        /// Stone block patterns (1 point each):
        /// - rock-{type} (e.g., rock-granite, rock-limestone)
        /// - crackedrock-{type} (e.g., crackedrock-granite)
        ///
        /// Ore block patterns (OreMultiplier points):
        /// - Contains "ore-" (e.g., ore-copper-granite, ore-lignite-chalk)
        /// </summary>
        private int GetBlockPoints(int blockId)
        {
            // Ore ("ore-", meteorite, meteoric iron) = OreMultiplier; rock-/crackedrock- = 1.
            // The code checks run once per block type (KindOfBlock).
            var kind = KindOfBlock(blockId);
            if ((kind & BlockKind.Ore) != 0) return OreMultiplier;
            if ((kind & BlockKind.Rock) != 0) return 1;
            return 0;
        }

        /// <summary>
        /// Handler for /trait miningmax command.
        /// Gets or sets the maximum mining speed bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMiningMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max mining speed percent must be at least 1");
                }

                MaxMiningSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
                    ApplyMiningBonus(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max mining speed bonus set to +{MaxMiningSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max mining speed bonus: +{MaxMiningSpeedPercent}%");
            }
        }

        /// <summary>
        /// Called when a player breaks a block. Updates mining progress based on new mechanics:
        /// - Only counts blocks broken with pickaxes
        /// - Only counts stone (1 point) and ore (5 points) blocks
        /// - Each pickaxe type tracks its own increment progress independently
        /// </summary>
        private void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            // Mining marks the handbook report changed itself, only when points land;
            // wild crops and vessels mark it in their own Process* wrappers. Most
            // broken blocks (dirt, grass, logs, hand breaks) change nothing.
            OnBlockBrokenCore(byPlayer, oldblockId, blockSel);
        }

        private void OnBlockBrokenCore(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return;

            // Check for Forager progression (wild crops on dirt, not farmland)
            if (!IsSkillDisabled("forager") && IsWildCropBlock(oldblockId, blockSel?.Position))
            {
                ProcessWildCropBroken(byPlayer);
            }

            // Check for Pilferer progression (cracked vessels only - they can't be re-placed)
            if (!IsSkillDisabled("pilferer") && IsCrackedVesselBlock(oldblockId))
            {
                ProcessVesselBreak(byPlayer);
            }

            // Skip mining progression if disabled
            if (IsSkillDisabled("mining")) return;

            // Check if player is using a pickaxe for mining progression
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            if (pickaxeCode == null) return; // Not using a pickaxe, skip mining

            // Check block type and get points
            int points = GetBlockPoints(oldblockId);
            if (points <= 0) return; // Not a stone/ore block, skip

            string playerUid = byPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = GetMaxMiningCredits(byPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            // A capped skill is still in use; keep its decay clock fresh.
            UpdateSkillActivityDay(playerUid, "mining");
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific pickaxe type
            var pickaxeProgress = playerProgress.GetPickaxeProgress(pickaxeCode);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to points
            int modifiedPoints = ApplyXPMultiplier(playerUid, points);

            // Add points to THIS pickaxe's progress
            pickaxeProgress.BlocksInIncrement += modifiedPoints;

            // Check if we've earned any new credits with this pickaxe
            while (pickaxeProgress.BlocksInIncrement >= pickaxeProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                pickaxeProgress.BlocksInIncrement -= pickaxeProgress.CurrentIncrementSize;
                pickaxeProgress.CurrentIncrementSize += IncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {byPlayer.PlayerName} earned credit {playerProgress.TotalCredits} with {pickaxeCode}, next requires {pickaxeProgress.CurrentIncrementSize} points");
            }

            pendingMiningProgressSave = true;
            MarkProgressChanged(byPlayer);   // handbook page: push the new numbers within 100 ms

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "mining");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMiningBonus(byPlayer, playerProgress.TotalCredits);

                // Notify player of level up with the level as the bonus (the raw mining speed improvement)
                // This shows the true progress even when negative traits are still being cancelled
                NotifyLevelUp(byPlayer,
                    Lang.Get("seraphleveling:message-mining-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on mining level
                CheckHardyHealthUnlock(byPlayer);
                CheckClaustrophobicRemoval(byPlayer);
            }
        }

        /// <summary>
        /// Also handles Weak and Claustrophobic negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns the actual applied bonus percentage (0-100 scale).
        /// </summary>
        private int ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaHardy = cache?.HasHardy ?? PlayerHasVanillaHardy(player.Entity);
            bool hasWeak = cache?.HasWeak ?? PlayerHasVanillaWeak(player.Entity);
            bool hasClaustrophobic = cache?.HasClaustrophobic ?? PlayerHasVanillaClaustrophobic(player.Entity);

            int vanillaHardyBonus = hasVanillaHardy ? VANILLA_HARDY_MINING_BONUS : 0;

            // Calculate remaining negative trait penalties
            int weakMiningRemaining = hasWeak ? CalculateRemainingPenalty(VANILLA_WEAK_MINING_PENALTY, level) : 0;
            // HP penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), HP is also cancelled
            int weakHpRemaining = weakMiningRemaining > 0 ? VANILLA_WEAK_HP_PENALTY : 0;
            int claustrophobicMiningRemaining = hasClaustrophobic ? CalculateRemainingPenalty(VANILLA_CLAUSTROPHOBIC_MINING_PENALTY, level) : 0;
            // Ore penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), ore is also cancelled
            int claustrophobicOreRemaining = claustrophobicMiningRemaining > 0 ? VANILLA_CLAUSTROPHOBIC_ORE_PENALTY : 0;

            // Calculate net bonus after cancelling negative traits
            // Negative trait penalty must be fully cancelled before bonus starts showing
            int totalNegativePenalty = 0;
            if (hasWeak) totalNegativePenalty += VANILLA_WEAK_MINING_PENALTY;
            if (hasClaustrophobic) totalNegativePenalty += VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;

            int netLevel = Math.Max(0, level - totalNegativePenalty);

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMiningSpeedPercent
            int maxEarnableBonus = MaxMiningSpeedPercent - CapCounted(vanillaHardyBonus);
            int bonusPercent = Math.Min(netLevel, Math.Max(0, maxEarnableBonus));

            float bonus = bonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            // Set the mining speed stat
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, bonus, false);

            // Counter-stats: when a vanilla negative trait's mining penalty is fully cancelled
            // (remaining == 0), apply a +penalty counter on the same stat so the ACTUAL applied
            // mining speed matches the displayed value. Without this, Hunter (Claustrophobic)
            // and Tailor (Weak) would land at a functional +40% mining at maxall (vanilla -10%
            // still applied, our +50% on top, net +40%) while their displayed +50% suggests
            // parity with other classes.
            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining == 0)
                {
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicMiningCancel", VANILLA_CLAUSTROPHOBIC_MINING_PENALTY * 0.01f, false);
                    // Negate the -15% ore drop penalty by applying +15%
                    player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicOreCancel", VANILLA_CLAUSTROPHOBIC_ORE_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicMiningCancel");
                    player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicOreCancel");
                }
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicMiningCancel");
                player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicOreCancel");
            }

            // When Weak mining penalty is fully cancelled, also negate the HP penalty AND the mining speed penalty
            if (hasWeak)
            {
                if (weakMiningRemaining == 0)
                {
                    // Negate the -2 HP penalty by applying +2 HP
                    player.Entity.Stats.Set("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE, VANILLA_WEAK_HP_PENALTY, false);
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitWeakMiningCancel", VANILLA_WEAK_MINING_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE);
                    player.Entity.Stats.Remove("miningSpeedMul", "sitWeakMiningCancel");
                }
            }
            else
            {
                // Trait gone (class change, or cancellation switched off): drop the counter.
                player.Entity.Stats.Remove("miningSpeedMul", "sitWeakMiningCancel");
            }

            // Check if any values have changed before updating WatchedAttributes.
            // The trait booleans must be part of the comparison: after a class
            // change with unchanged level/bonus (e.g. both 0), the display attrs
            // would otherwise keep the old class's traits.
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MINING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MINING_BONUS, -1);
            int oldClaustoMining = watchedAttrs.GetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent) || (oldClaustoMining != claustrophobicMiningRemaining)
                || watchedAttrs.GetBool("sitHasVanillaHardy") != hasVanillaHardy
                || watchedAttrs.GetBool("sitHasWeak") != hasWeak
                || watchedAttrs.GetInt(WATCHED_WEAK_MINING_REMAINING, -1) != weakMiningRemaining
                || watchedAttrs.GetBool("sitHasClaustrophobic") != hasClaustrophobic
                || watchedAttrs.GetInt(WATCHED_CLAUSTROPHOBIC_ORE_REMAINING, -1) != claustrophobicOreRemaining;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MINING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MINING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaHardy", hasVanillaHardy);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasWeak", hasWeak);
                watchedAttrs.SetInt(WATCHED_WEAK_MINING_REMAINING, weakMiningRemaining);
                watchedAttrs.SetInt(WATCHED_WEAK_HP_REMAINING, weakHpRemaining);
                watchedAttrs.SetBool("sitHasClaustrophobic", hasClaustrophobic);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, claustrophobicMiningRemaining);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_ORE_REMAINING, claustrophobicOreRemaining);

                // Add our trait to extraTraits only if:
                // - Player doesn't already have Hardy AND
                // - All negative mining penalties are cancelled (bonusPercent > 0)
                UpdateExtraTrait(player.Entity, MINING_TRAIT_CODE, bonusPercent > 0 && !hasVanillaHardy);

                // Only call MarkPathDirty once at the end (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_MINING_LEVEL);
            }

            return bonusPercent;
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Hardy trait.
        /// </summary>
        private bool PlayerHasVanillaHardy(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "hardy");
        }

        /// <summary>
        /// Calculate the mining speed bonus as a float (0.0 to 1.5 for 0% to 150%).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static float CalculateMiningBonus(int credits)
        {
            float bonus = credits * 0.01f;
            return Math.Min(bonus, MaxMiningSpeedPercent / 100f);
        }

        /// <summary>
        /// Calculate the mining speed bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static int CalculateMiningBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMiningSpeedPercent);
        }

        /// <summary>
        /// Calculate the maximum credits (level) based on the bonus cap.
        /// </summary>
        public static int CalculateMaxCredits()
        {
            return MaxMiningSpeedPercent;
        }

        /// <summary>
        /// Get the maximum mining credits a player can earn based on their traits.
        /// Players with Weak or Claustrophobic traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMiningCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMiningSpeedPercent;

            // Class cap offsets: no extra credits to pay off starting penalties, so
            // penalized classes end lower instead of catching up.
            if (EnableClassCapOffsets) return MaxMiningSpeedPercent;

            bool hasWeak = PlayerHasVanillaWeak(entity);
            bool hasClaustrophobic = PlayerHasVanillaClaustrophobic(entity);

            // Sum the extra credits; the apply path subtracts BOTH penalties, so
            // a class with both needs both to be cancellable (10 + 10).
            int extraCredits = (hasWeak ? VANILLA_WEAK_MINING_PENALTY : 0)
                             + (hasClaustrophobic ? VANILLA_CLAUSTROPHOBIC_MINING_PENALTY : 0);
            return MaxMiningSpeedPercent + extraCredits;
        }
    }
}
