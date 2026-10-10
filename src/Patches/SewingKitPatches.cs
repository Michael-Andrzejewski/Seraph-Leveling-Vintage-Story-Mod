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
    /// <summary>
    /// Server-side Harmony patches for sewing kit repairs (Mender trait).
    /// </summary>
    public static class SewingKitPatches
    {
        // Track which players have recently had repairs to avoid duplicate credits
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastRepairTime =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        // Track item durabilities to detect repairs
        // Last durability per (inventory, slot, item type); the old string key was
        // "{InventoryID}_{slot}_{code}". An int value is written in place.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string, int, CollectibleObject), int> ItemDurabilities =
            new System.Collections.Concurrent.ConcurrentDictionary<(string, int, CollectibleObject), int>();

        // Minimum interval between repair credits (in ticks, 20 ticks = 1 second)
        private const long MIN_REPAIR_INTERVAL = 10;

        /// <summary>
        /// Prefix for CollectibleBehaviorWearable.TryMergeStacks: captures the
        /// clothing's condition before the merge so the postfix can tell whether
        /// a repair actually happened.
        /// </summary>
        public static void TryMergeStacks_Prefix(ItemStackMergeOperation op, out float __state)
        {
            __state = op?.SinkSlot?.Itemstack?.Attributes?.GetFloat("condition", 1f) ?? 1f;
        }

        /// <summary>
        /// Postfix for CollectibleBehaviorWearable.TryMergeStacks: merging a
        /// sewing kit onto damaged clothing is the vanilla repair mechanic. The
        /// merge raises the clothing's "condition" attribute and consumes one
        /// kit, so a condition increase IS a completed repair. This cannot be
        /// farmed: every credit costs a sewing kit spent on actually damaged
        /// clothing.
        /// </summary>
        public static void TryMergeStacks_Postfix(ItemStackMergeOperation op, float __state)
        {
            try
            {
                var player = op?.ActingPlayer as IServerPlayer;
                float newCondition = op?.SinkSlot?.Itemstack?.Attributes?.GetFloat("condition", 1f) ?? 1f;

                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                        $"[SeraphLeveling] TryMergeStacks postfix: before={__state:F2} after={newCondition:F2} actor={(op?.ActingPlayer?.PlayerName ?? "null")} isServerPlayer={player != null}");
                }

                if (player == null) return;
                if (newCondition <= __state + 0.001f) return;

                SeraphLevelingModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in TryMergeStacks_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnModifiedInInventorySlot - tracks durability changes.
        /// When a wearable item's durability increases, it's likely due to a repair.
        /// </summary>
        public static void OnModifiedInInventorySlot_Postfix(
            CollectibleObject __instance,
            IWorldAccessor world,
            ItemSlot slot,
            ItemStack extractedStack)
        {
            try
            {
                // Only process server-side
                if (world?.Side != EnumAppSide.Server) return;

                // Vanilla calls this on every MarkDirty of every slot in every
                // inventory (each chute and hopper transfer, each tool hit), so
                // the cheapest filters go first and nothing allocates before them.
                // Only the player's own character and backpack inventories can
                // ever credit a repair below; others were tracked for nothing.
                string invClass = slot?.Inventory?.ClassName;
                if (invClass != GlobalConstants.characterInvClassName && invClass != GlobalConstants.backpackInvClassName) return;

                // Only track wearable items (clothing and armor)
                string path = __instance.Code?.Path;
                if (path == null) return;
                if (!path.Contains("clothes-", StringComparison.Ordinal) && !path.Contains("armor-", StringComparison.Ordinal)) return;

                // Get the current durability
                var currentStack = slot?.Itemstack;
                if (currentStack == null) return;

                int currentDurability = currentStack.Collectible?.GetRemainingDurability(currentStack) ?? 0;

                // This item type in this slot of this inventory
                var itemKey = (slot.Inventory?.InventoryID, slot.Inventory?.GetSlotId(slot) ?? -1, __instance);

                // Check if durability increased (repair happened)
                if (ItemDurabilities.TryGetValue(itemKey, out int previousDurability))
                {
                    if (currentDurability > previousDurability)
                    {
                        // Durability increased - repair happened!
                        // Try to find which player owns this inventory
                        var inventory = slot.Inventory;
                        if (inventory != null)
                        {
                            // Find player by checking if this is a character or backpack inventory
                            foreach (var serverPlayer in SeraphLevelingModSystem.OnlinePlayers)
                            {
                                if (serverPlayer?.InventoryManager == null) continue;

                                // Check if this inventory belongs to this player
                                var owned = SeraphLevelingModSystem.InventoriesOf(serverPlayer);
                                var characterInv = owned?.Character;
                                var backpackInv = owned?.Backpack;

                                if (characterInv == inventory || backpackInv == inventory)
                                {
                                    // Check cooldown
                                    long currentTick = world.ElapsedMilliseconds;
                                    string playerKey = serverPlayer.PlayerUID + "_mod";

                                    if (!LastRepairTime.TryGetValue(playerKey, out long lastTime) ||
                                        currentTick - lastTime >= MIN_REPAIR_INTERVAL * 50)
                                    {
                                        LastRepairTime[playerKey] = currentTick;
                                        SeraphLevelingModSystem.ProcessMenderRepair(serverPlayer);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Update tracked durability
                ItemDurabilities[itemKey] = currentDurability;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnModifiedInInventorySlot_Postfix: {ex.Message}");
            }
        }

    }
}
