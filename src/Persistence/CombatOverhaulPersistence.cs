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
        // COMBAT OVERHAUL PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist Combat Overhaul proficiency progress to world save data.
        /// </summary>
        public static void PersistCOProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = COProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Magic bytes: "COB" (Combat Overhaul Bonus)
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)0x4F); // 'O'
                            writer.Write((byte)0x42); // 'B'
                            writer.Write((byte)2);    // Version 2

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key); // Player UID
                                var playerProgress = playerKvp.Value;

                                // Write Steady Aim credits
                                writer.Write(playerProgress.SteadyAimCredits);
                                writer.Write(playerProgress.LastActivityDay);

                                // Write proficiency count and each proficiency
                                var profSnapshot = playerProgress.Proficiencies.ToArray();
                                writer.Write(profSnapshot.Length);
                                foreach (var profKvp in profSnapshot)
                                {
                                    writer.Write(profKvp.Key); // Proficiency stat name
                                    var profProgress = profKvp.Value;
                                    writer.Write(profProgress.TotalCredits);

                                    // Write weapon progress
                                    var weaponSnapshot = profProgress.WeaponProgress.ToArray();
                                    writer.Write(weaponSnapshot.Length);
                                    foreach (var weaponKvp in weaponSnapshot)
                                    {
                                        writer.Write(weaponKvp.Key); // Weapon code
                                        writer.Write(weaponKvp.Value.DamageInIncrement);
                                        writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                    }
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(CO_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted CO progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist CO progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load Combat Overhaul proficiency progress from world save data.
        /// </summary>
        private void LoadCOProgress()
        {
            COProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CO_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No CO progress data found");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte magic1 = reader.ReadByte();
                        byte magic2 = reader.ReadByte();
                        byte magic3 = reader.ReadByte();
                        byte version = reader.ReadByte();

                        if (magic1 != 0x43 || magic2 != 0x4F || magic3 != 0x42)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid CO progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var playerProgress = new COPlayerProgressData();

                                    // Read Steady Aim credits
                                    playerProgress.SteadyAimCredits = reader.ReadInt32();

                                    // Read proficiencies
                                    int proficiencyCount = reader.ReadInt32();
                                    for (int j = 0; j < proficiencyCount; j++)
                                    {
                                        string proficiencyStat = reader.ReadString();
                                        var profProgress = new COProficiencyProgressData();
                                        profProgress.TotalCredits = reader.ReadInt32();

                                        // Read weapon progress
                                        int weaponCount = reader.ReadInt32();
                                        for (int k = 0; k < weaponCount; k++)
                                        {
                                            string weaponCode = reader.ReadString();
                                            var weaponProgress = new COWeaponProgressData
                                            {
                                                DamageInIncrement = reader.ReadSingle(),
                                                CurrentIncrementSize = reader.ReadInt32()
                                            };
                                            profProgress.WeaponProgress[weaponCode] = weaponProgress;
                                        }

                                        playerProgress.Proficiencies[proficiencyStat] = profProgress;
                                    }

                                    COProgress[playerUid] = playerProgress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in CO proficiency data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingCOProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Current format: per-proficiency progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var playerProgress = new COPlayerProgressData();

                                    // Read Steady Aim credits
                                    playerProgress.SteadyAimCredits = reader.ReadInt32();
                                    playerProgress.LastActivityDay = reader.ReadDouble();

                                    // Read proficiencies
                                    int proficiencyCount = reader.ReadInt32();
                                    for (int j = 0; j < proficiencyCount; j++)
                                    {
                                        string proficiencyStat = reader.ReadString();
                                        var profProgress = new COProficiencyProgressData();
                                        profProgress.TotalCredits = reader.ReadInt32();

                                        // Read weapon progress
                                        int weaponCount = reader.ReadInt32();
                                        for (int k = 0; k < weaponCount; k++)
                                        {
                                            string weaponCode = reader.ReadString();
                                            var weaponProgress = new COWeaponProgressData
                                            {
                                                DamageInIncrement = reader.ReadSingle(),
                                                CurrentIncrementSize = reader.ReadInt32()
                                            };
                                            profProgress.WeaponProgress[weaponCode] = weaponProgress;
                                        }

                                        playerProgress.Proficiencies[proficiencyStat] = profProgress;
                                    }

                                    COProgress[playerUid] = playerProgress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in CO proficiency data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown CO save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded CO progress for {COProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load CO progress: {ex.Message}");
            }
        }
    }
}
