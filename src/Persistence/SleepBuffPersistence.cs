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
        // SLEEP BUFF PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist sleep buff data to world save data.
        /// </summary>
        public static void PersistSleepBuffData()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = SleepBuffExpiration.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Magic bytes: "SLB" (Sleep Buff)
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x42); // 'B'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var entry in snapshot)
                            {
                                writer.Write(entry.Key); // Player UID
                                writer.Write(entry.Value); // Expiration day
                                // Get multiplier (default to 1.0 if somehow missing)
                                float mult = SleepBuffMultiplier.TryGetValue(entry.Key, out float m) ? m : 1.0f;
                                writer.Write(mult);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(SLEEP_BUFF_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted sleep buff data for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist sleep buff data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load sleep buff data from world save data.
        /// Expired buffs are discarded on load.
        /// </summary>
        private void LoadSleepBuffData()
        {
            if (ServerApi == null) return;

            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(SLEEP_BUFF_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No sleep buff data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x4C || b3 != 0x42) // "SLB"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid sleep buff data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int count = reader.ReadInt32();

                        if (version == 1)
                        {
                            double currentDay = ServerApi.World?.Calendar?.TotalDays ?? 0;
                            int loaded = 0;

                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    double expiration = reader.ReadDouble();
                                    float multiplier = reader.ReadSingle();

                                    // Only restore buffs that haven't expired
                                    if (currentDay < expiration)
                                    {
                                        SleepBuffExpiration[playerUid] = expiration;
                                        SleepBuffMultiplier[playerUid] = multiplier;
                                        loaded++;
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{count} in sleep buff data: {innerEx.Message}");
                                    break;
                                }
                            }

                            ServerApi.Logger.Notification($"[SeraphLeveling] Loaded sleep buff data: {loaded} active buffs ({count - loaded} expired)");
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown sleep buff save format version {version}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load sleep buff data: {ex.Message}");
            }
        }
    }
}
