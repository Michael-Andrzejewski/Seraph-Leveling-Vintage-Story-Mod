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
    /// Server-side Harmony patches for bed sleeping (sleep buff).
    /// </summary>
    public static class BedSleepPatches
    {
        /// <summary>
        /// The buff requires at least this many in-game hours in bed. Real sleep
        /// accelerates the calendar, so a night passes this easily. Hopping in
        /// and out of a bed passes almost no game time and grants nothing.
        /// </summary>
        public const double MIN_SLEEP_HOURS = 0.5;

        /// <summary>
        /// Postfix for BlockEntityBed.DidMount - records when the player got
        /// into bed so DidUnmount can verify they actually slept.
        /// </summary>
        public static void DidMount_Postfix(EntityAgent entityAgent)
        {
            try
            {
                var serverApi = SeraphLevelingModSystem.ServerApi;
                if (serverApi == null) return;

                var playerEntity = entityAgent as EntityPlayer;
                if (playerEntity?.PlayerUID == null) return;

                SeraphLevelingModSystem.SleepMountHours[playerEntity.PlayerUID] = serverApi.World.Calendar.TotalHours;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in DidMount_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for BlockEntityBed.DidUnmount - applies sleep buff when player gets out of bed.
        /// DidUnmount(EntityAgent) is called when a player unmounts from the bed after sleeping.
        /// __instance is the BlockEntityBed itself (a BlockEntity subclass).
        /// </summary>
        public static void DidUnmount_Postfix(object __instance, EntityAgent entityAgent)
        {
            try
            {
                if (!SeraphLevelingModSystem.EnableSleepBuff) return;
                if (entityAgent == null) return;

                var playerEntity = entityAgent as EntityPlayer;
                if (playerEntity == null) return;

                var serverPlayer = SeraphLevelingModSystem.ServerApi?.World?.PlayerByUid(playerEntity.PlayerUID) as IServerPlayer;
                if (serverPlayer == null) return;

                string playerUid = serverPlayer.PlayerUID;
                if (string.IsNullOrEmpty(playerUid)) return;

                // Require real sleep: the mount record must exist and enough
                // game time must have passed while in bed. TryRemove also makes
                // the head/foot double-fire a no-op.
                if (!SeraphLevelingModSystem.SleepMountHours.TryRemove(playerUid, out double mountHours)) return;
                double currentHours = SeraphLevelingModSystem.ServerApi?.World?.Calendar?.TotalHours ?? mountHours;
                double hoursSlept = currentHours - mountHours;
                if (hoursSlept < MIN_SLEEP_HOURS)
                {
                    if (SeraphLevelingModSystem.DebugLoggingEnabled)
                    {
                        SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                            $"[SeraphLeveling] No sleep buff for {serverPlayer.PlayerName}: only {hoursSlept:F2} game hours in bed.");
                    }
                    return;
                }

                // Dedup: beds have two parts (head + foot), so DidUnmount fires twice.
                // Skip if we already applied the buff within the last 2 seconds (real time).
                long currentTick = Environment.TickCount64;
                if (SeraphLevelingModSystem.LastSleepBuffApplyTick.TryGetValue(playerUid, out long lastTick)
                    && (currentTick - lastTick) < 2000)
                {
                    return;
                }
                SeraphLevelingModSystem.LastSleepBuffApplyTick[playerUid] = currentTick;

                // Determine bed type from the block entity's block code
                float multiplier = SeraphLevelingModSystem.SleepBuffLinenBedMultiplier; // Default to linen bed multiplier
                bool isHayBed = false;

                try
                {
                    // __instance IS the BlockEntityBed, which inherits from BlockEntity
                    var blockEntity = __instance as BlockEntity;
                    if (blockEntity?.Block?.Code != null)
                    {
                        string blockCode = blockEntity.Block.Code.ToString().ToLowerInvariant();

                        // Check if it's a hay bed
                        if (blockCode.Contains("hay"))
                        {
                            isHayBed = true;
                            multiplier = SeraphLevelingModSystem.SleepBuffHayBedMultiplier;
                        }
                        // Linen and old beds use the default linen multiplier

                        if (SeraphLevelingModSystem.DebugLoggingEnabled)
                        {
                            SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                                $"[SeraphLeveling] Sleep buff: {serverPlayer.PlayerName} slept in {blockCode}, multiplier: {multiplier}x");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If we can't determine bed type, use default linen multiplier
                    System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Could not determine bed type: {ex.Message}");
                }

                // Calculate expiration time
                double currentDay = SeraphLevelingModSystem.ServerApi?.World?.Calendar?.TotalDays ?? 0;
                double expirationDay = currentDay + SeraphLevelingModSystem.SleepBuffDurationDays;

                // Apply the buff
                SeraphLevelingModSystem.SleepBuffExpiration[playerUid] = expirationDay;
                SeraphLevelingModSystem.SleepBuffMultiplier[playerUid] = multiplier;
                SeraphLevelingModSystem.pendingSleepBuffSave = true;

                // Notify the player
                string bedType = isHayBed ? "hay bed" : "comfortable bed";
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Well rested! Skill XP x{multiplier} for the next {SeraphLevelingModSystem.SleepBuffDurationDays} day(s) from sleeping in a {bedType}.",
                    EnumChatType.Notification);

                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Applied sleep buff to {serverPlayer.PlayerName}: {multiplier}x until day {expirationDay:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in DidUnmount_Postfix: {ex.Message}");
            }
        }
    }
}
