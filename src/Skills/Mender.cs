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
        // MENDER TRAIT - Tracks sewing kit repairs for durability bonus
        // =========================================================================
        public const string MENDER_STAT_CODE = "sitMenderBonus";
        private const string MENDER_PROGRESS_SAVE_KEY = "sitMenderProgress";
        public const string WATCHED_MENDER_LEVEL = "sitMenderLevel";
        public const string WATCHED_MENDER_BONUS = "sitMenderBonusPercent";
        public const string MENDER_TRAIT_CODE = "sitmendermastery";

        // Mender progression configuration
        public static int BaseMenderRepairsPerIncrement = 5;   // Base repairs for first credit
        public static int MenderIncrementStep = 1;              // Increment step per credit
        public static int MaxMenderPercent = 25;                // 25% total cap (matches vanilla Mender +25% so Tailor and non-Tailor end up equal)

        // Vanilla Mender trait bonus (used for cap calculations)
        // Vanilla Mender shows "+25% armor durability" (armorDurabilityLoss: -0.25). This is
        // used both for cap math (Tailor's earnable = MaxMenderPercent - 25, so total caps at
        // MaxMenderPercent like every other class) and for inline display Replace.
        public const int VANILLA_MENDER_ARMOR_DURABILITY_BONUS = 25;

        // Storage for mender progress
        public static ConcurrentDictionary<string, MenderProgressData> MenderProgress = new ConcurrentDictionary<string, MenderProgressData>();
        private static volatile bool pendingMenderProgressSave = false;

        // Durability tracking for repair detection - key is "playerUid_slotId", value is last known durability
        // Keyed by player, slot and item code; the value carries the item's own tracking id
        // so a fresh item of the same code swapped into the slot does not read as a repair.
        // Mender grid-repair backup: per player, the durability last seen for each
        // (character slot, item type), like the old "uid_slot_code" string keys but
        // without building a string per slot per tick. Values are updated in place.
        private sealed class TrackedDurability { public long Id; public int Durability; public double RawAttr; public bool Seen; }
        private static ConcurrentDictionary<string, Dictionary<(int, CollectibleObject), TrackedDurability>> TrackedItemDurabilities =
            new ConcurrentDictionary<string, Dictionary<(int, CollectibleObject), TrackedDurability>>();
        private const string TRACK_ID_ATTR = "sitTrackId";
        private static long nextTrackId = 1;

        /// <summary>
        /// Tick handler for Mender repair detection.
        /// Uses two detection methods:
        /// 1. Tracks sewing kit consumption from mouse cursor (most reliable)
        /// 2. Tracks durability increases on wearable items (backup method)
        /// </summary>
        private void OnMenderRepairTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;

                // NOTE: cursor sewing-kit stack tracking used to live here. It
                // counted ANY stack decrease as repairs, so depositing kits into
                // a chest or splitting the stack farmed Mender XP. The real
                // repair (kit merged onto clothing) is now detected precisely by
                // the TryMergeStacks patch in SewingKitPatches.

                // =============================================
                // Track durability increases on worn items (grid repair backup)
                // =============================================
                // Worn durability only changes when a character slot changes
                if (!GearScanDue(InventoriesOf(player), clothing: false)) continue;
                var characterInventory = CharacterInventoryOf(player);
                if (characterInventory == null) continue;
                var trackedForPlayer = TrackedItemDurabilities.GetOrAdd(playerUid, _ => new Dictionary<(int, CollectibleObject), TrackedDurability>());

                for (int slotIndex = 0; slotIndex < characterInventory.Count; slotIndex++)
                {
                    var slot = characterInventory[slotIndex];
                    if (slot?.Itemstack?.Collectible == null) continue;

                    var gear = GearInfoOf(slot.Itemstack.Collectible);
                    if (gear == null || string.IsNullOrEmpty(gear.Code)) continue;
                    string itemCode = gear.Code;

                    // Only track clothing and armor
                    if (!gear.IsClothing && !gear.IsArmor) continue;

                    // Nothing changed since last tick (same item, same raw "durability"
                    // attribute): skip GetRemainingDurability/GetMaxDurability, which walk
                    // the item behaviours with fresh closures on every call.
                    var shortcutKey = (slotIndex, slot.Itemstack.Collectible);
                    double rawAttr = slot.Itemstack.Attributes.GetDecimal("durability", double.NaN);
                    long attrTrackId = slot.Itemstack.Attributes.GetLong(TRACK_ID_ATTR, 0);
                    if (trackedForPlayer.TryGetValue(shortcutKey, out var seen) && seen.Seen && seen.Id == attrTrackId
                        && (seen.RawAttr == rawAttr || (double.IsNaN(seen.RawAttr) && double.IsNaN(rawAttr))))
                        continue;

                    // Get current durability
                    int currentDurability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                    // Skip items without durability (remembered, so the shortcut above skips them next tick)
                    if (maxDurability <= 0)
                    {
                        if (seen == null) trackedForPlayer[shortcutKey] = seen = new TrackedDurability();
                        seen.Id = attrTrackId; seen.RawAttr = rawAttr; seen.Seen = true;
                        seen.Durability = int.MinValue;   // never compared: no durability
                        continue;
                    }

                    // This item type in this slot
                    var trackingKey = shortcutKey;

                    // Identify THIS item. A fresh item of the same code swapped into the
                    // slot starts at full durability, which used to look like a repair.
                    long trackId = slot.Itemstack.Attributes.GetLong(TRACK_ID_ATTR, 0);
                    if (trackId == 0)
                    {
                        trackId = System.Threading.Interlocked.Increment(ref nextTrackId) + (ServerApi?.World?.ElapsedMilliseconds ?? 0) * 1000;
                        slot.Itemstack.Attributes.SetLong(TRACK_ID_ATTR, trackId);
                        slot.MarkDirty();
                    }

                    // Check if durability increased (repair happened) on the same item
                    var tracked = seen;
                    if (tracked != null && tracked.Seen && tracked.Durability != int.MinValue && tracked.Id == trackId)
                    {
                        int previousDurability = tracked.Durability;
                        if (currentDurability > previousDurability)
                        {
                            // Durability increased - a repair happened!
                            int durabilityRestored = currentDurability - previousDurability;
                            int repairPercent = (durabilityRestored * 100) / maxDurability;

                            // Only credit significant repairs (at least 5% durability restored)
                            // This filters out minor fluctuations and avoids double-counting with method 1
                            // Use a higher threshold since method 1 should catch most sewing kit repairs
                            if (repairPercent >= 10)
                            {
                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired {itemCode} (+{repairPercent}% durability) via durability tracking");
                                ProcessMenderRepair(player);
                            }
                        }
                    }

                    // Update tracked durability (in place after the first sighting)
                    if (tracked == null) trackedForPlayer[trackingKey] = tracked = new TrackedDurability();
                    tracked.Id = trackId;
                    tracked.Durability = currentDurability;
                    tracked.RawAttr = slot.Itemstack.Attributes.GetDecimal("durability", double.NaN);   // after any trackId write
                    tracked.Seen = true;
                }
            }
        }

        /// <summary>
        /// Check if an item code represents armor.
        /// </summary>
        private static bool IsArmorItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();
            return lowerCode.Contains("armor-");
        }

        // =========================================================================
        // MENDER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait mender command.
        /// </summary>
        private TextCommandResult OnTraitMenderCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());
            int bonusPercent = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mender progression: Level {progress.TotalCredits} / {MaxMenderPercent}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% armor and clothing durability");
            if (hasVanillaMender)
            {
                sb.AppendLine($"Combined with Mender trait: +{VANILLA_MENDER_ARMOR_DURABILITY_BONUS + bonusPercent}% total");
            }
            if (progress.TotalCredits < MaxMenderPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.RepairsInIncrement;
                sb.AppendLine($"Progress: {progress.RepairsInIncrement} / {progress.CurrentIncrementSize} repairs until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait menderbase command.
        /// </summary>
        private TextCommandResult OnTraitMenderBaseCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Base repairs must be at least 1.",
            v => BaseMenderRepairsPerIncrement = v,
            () => $"Mender base repairs set to {BaseMenderRepairsPerIncrement}.",
            () => $"Current mender base repairs: {BaseMenderRepairsPerIncrement}.");

        /// <summary>
        /// Handler for /trait menderlevel command.
        /// Gets or sets the player's mender level.
        /// </summary>
        private TextCommandResult OnTraitMenderLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current mender level: {progress.TotalCredits}/{MaxMenderPercent} (+{currentBonus}% durability)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxMenderPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxMenderPercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.RepairsInIncrement = 0;
            progress.CurrentIncrementSize = BaseMenderRepairsPerIncrement;

            // Recalculate increment size for this level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += MenderIncrementStep;
            }


            int bonusPercent = ApplyMenderBonusStatic(player, progress.TotalCredits);

            UpdateSkillActivityDay(player.PlayerUID, "mender");

            return TextCommandResult.Success($"Mender level set to {newLevel.Value} (+{bonusPercent}% durability).");
        }

        /// <summary>
        /// Handler for /trait mendermax command.
        /// </summary>
        private TextCommandResult OnTraitMenderMaxCommand(TextCommandCallingArgs args) => ShowOrSetConfig(args, 1, "Max percent must be at least 1.",
            v => MaxMenderPercent = v,
            () => $"Mender max bonus set to {MaxMenderPercent}%.",
            () => $"Current mender max bonus: {MaxMenderPercent}%.");

        /// <summary>
        /// Calculate the mender durability bonus as an integer percentage.
        /// </summary>
        public static int CalculateMenderBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaMender = entity != null && PlayerHasVanillaMenderStatic(entity);
            int vanillaBonus = hasVanillaMender ? VANILLA_MENDER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxMenderPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Mender trait.
        /// </summary>
        private static bool PlayerHasVanillaMenderStatic(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "mender");
        }

        /// <summary>
        /// Apply mender bonus.
        /// </summary>
        private static int ApplyMenderBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);
            int bonusPercent = CalculateMenderBonusPercent(level, player.Entity);
            float bonus = bonusPercent * 0.01f;

            // Apply to armor durability loss stat (reduces durability damage taken).
            // Negative delta, same as vanilla Mender's -0.25. See ApplyArmorBonusesStatic.
            player.Entity.Stats.Set("armorDurabilityLoss", MENDER_STAT_CODE, -bonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaMender", hasVanillaMender);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MENDER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, MENDER_TRAIT_CODE, level > 0 && !hasVanillaMender);

            return bonusPercent;
        }

        /// <summary>
        /// Process a sewing kit repair (called externally or via Harmony patch).
        /// </summary>
        public static void ProcessMenderRepair(IServerPlayer player)
        {
            ProcessMenderRepairCore(player);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessMenderRepairCore(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if mender skill is disabled
            if (IsSkillDisabled("mender")) return;

            string playerUid = player.PlayerUID;
            var progress = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());

            // Skip if at max
            if (progress.TotalCredits >= MaxMenderPercent) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedRepairs = ApplyXPMultiplier(playerUid, 1);
            progress.RepairsInIncrement += modifiedRepairs;

            // Check if we've earned a credit
            while (progress.RepairsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxMenderPercent)
            {
                progress.TotalCredits++;
                progress.RepairsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += MenderIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned mender credit {progress.TotalCredits}");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "mender");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyMenderBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-mender-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }
    }
}
