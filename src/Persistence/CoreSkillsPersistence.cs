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

        // Lock object for persistence operations
        private static readonly object persistLock = new object();

        /// <summary>
        /// Serializes all progress into the save game's mod data. Used by the
        /// reset commands so a wipe is in the save the game writes next.
        /// </summary>
        private void SaveAllPendingProgress()
        {
            // Reuse the same logic as OnGameWorldSave
            OnGameWorldSave();
            // The temporal dicts persist in their own world-save subscriber
            // (TemporalTraits.cs), so flush them here too. Without this,
            // disconnect saves, auto-save ticks, and the reset commands would
            // leave temporal progress unsaved until the next full world save.
            SaveTemporalProgressOnWorldSave();
        }

        /// <summary>
        /// Called when the world is saved. Persist all progress and config to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
            // Guard against persisting empty data after Dispose() has cleared dictionaries
            if (isDisposed) return;

            if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
            {
                PersistMiningProgress();
                pendingMiningProgressSave = false;
            }

            if (pendingMeleeProgressSave || !MeleeProgress.IsEmpty)
            {
                PersistMeleeProgress();
                pendingMeleeProgressSave = false;
            }

            if (pendingRangedProgressSave || !RangedProgress.IsEmpty)
            {
                PersistRangedProgress();
                pendingRangedProgressSave = false;
            }

            if (pendingWalkingProgressSave || !WalkingProgress.IsEmpty)
            {
                PersistWalkingProgress();
                pendingWalkingProgressSave = false;
            }

            if (pendingHungerProgressSave || !HungerProgress.IsEmpty)
            {
                PersistHungerProgress();
                pendingHungerProgressSave = false;
            }

            if (pendingArmorProgressSave || !ArmorProgress.IsEmpty)
            {
                PersistArmorProgress();
                pendingArmorProgressSave = false;
            }

            if (pendingClothierProgressSave || !ClothierProgress.IsEmpty)
            {
                PersistClothierProgress();
                pendingClothierProgressSave = false;
            }

            if (pendingMenderProgressSave || !MenderProgress.IsEmpty)
            {
                PersistMenderProgress();
                pendingMenderProgressSave = false;
            }

            if (pendingPilfererProgressSave || !PilfererProgress.IsEmpty)
            {
                PersistPilfererProgress();
                pendingPilfererProgressSave = false;
            }

            if (pendingResourcefulProgressSave || !ResourcefulProgress.IsEmpty)
            {
                PersistResourcefulProgress();
                pendingResourcefulProgressSave = false;
            }

            if (pendingForagerProgressSave || !ForagerProgress.IsEmpty)
            {
                PersistForagerProgress();
                pendingForagerProgressSave = false;
            }

            if (pendingFurtiveProgressSave || !FurtiveProgress.IsEmpty)
            {
                PersistFurtiveProgress();
                pendingFurtiveProgressSave = false;
            }

            if (pendingPreciseProgressSave || !PreciseProgress.IsEmpty)
            {
                PersistPreciseProgress();
                pendingPreciseProgressSave = false;
            }

            if (pendingTechnicalProgressSave || !TechnicalProgress.IsEmpty)
            {
                PersistTechnicalProgress();
                pendingTechnicalProgressSave = false;
            }

            if (pendingHardyHealthProgressSave || !HardyHealthProgress.IsEmpty)
            {
                PersistHardyHealthProgress();
                pendingHardyHealthProgressSave = false;
            }

            if (pendingBowyerProgressSave || !BowyerProgress.IsEmpty)
            {
                PersistBowyerProgress();
                pendingBowyerProgressSave = false;
            }

            if (pendingImproviserProgressSave || !ImproviserProgress.IsEmpty)
            {
                PersistImproviserProgress();
                pendingImproviserProgressSave = false;
            }

            if (pendingTinkererProgressSave || !TinkererProgress.IsEmpty)
            {
                PersistTinkererProgress();
                pendingTinkererProgressSave = false;
            }

            if (pendingMercilessProgressSave || !MercilessProgress.IsEmpty)
            {
                PersistMercilessProgress();
                pendingMercilessProgressSave = false;
            }

            if (pendingClaustrophobicRemovalProgressSave || !ClaustrophobicRemovalProgress.IsEmpty)
            {
                PersistClaustrophobicRemovalProgress();
                pendingClaustrophobicRemovalProgressSave = false;
            }

            // Combat Overhaul compatibility persistence
            if (pendingCOProgressSave || !COProgress.IsEmpty)
            {
                PersistCOProgress();
                pendingCOProgressSave = false;
            }

            if (pendingSleepBuffSave || !SleepBuffExpiration.IsEmpty)
            {
                PersistSleepBuffData();
                pendingSleepBuffSave = false;
            }

            if (pendingConfigSave)
            {
                PersistConfig();
                pendingConfigSave = false;
            }
        }

        /// <summary>
        /// Persist mining progress to world save data.
        /// Version 3 format stores per-pickaxe progress dictionary.
        /// </summary>
        public static void PersistMiningProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = MiningProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)4);    // Version 4: Per-pickaxe progress + LastActivityDay

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.LastActivityDay);

                                // Snapshot inner dictionary to avoid concurrent modification
                                var pickaxeSnapshot = progress.PickaxeProgress.ToArray();
                                writer.Write(pickaxeSnapshot.Length);
                                foreach (var pickaxeKvp in pickaxeSnapshot)
                                {
                                    writer.Write(pickaxeKvp.Key); // Pickaxe code
                                    writer.Write(pickaxeKvp.Value.BlocksInIncrement);
                                    writer.Write(pickaxeKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MINING_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted mining progress for {snapshot.Length} players (v4 format)");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist mining progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load mining progress from world save data.
        /// Supports versions 1 (legacy blocks), 2 (single pickaxe), and 3 (per-pickaxe).
        /// </summary>
        private void LoadMiningProgress()
        {
            if (ServerApi == null) return;

            MiningProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MINING_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No mining progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x54) // "SIT"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid mining progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Legacy format: convert old blocks-based progress to credits
                            ServerApi.Logger.Notification("[SeraphLeveling] Converting legacy v1 save data to v3 format...");
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    long blocksMined = reader.ReadInt64();

                                    // Convert old blocks to credits using legacy formula
                                    int legacyLevel = 0;
                                    if (blocksMined >= 100)
                                    {
                                        double discriminant = 1.0 + (8.0 * blocksMined / 100);
                                        legacyLevel = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);
                                    }

                                    var progress = new MiningProgressData
                                    {
                                        TotalCredits = Math.Min(legacyLevel, MaxMiningSpeedPercent)
                                    };
                                    // No pickaxe progress to migrate
                                    MiningProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in mining data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingMiningProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Version 2: single pickaxe tracking - convert to v3
                            ServerApi.Logger.Notification("[SeraphLeveling] Converting v2 save data to v3 format...");
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    int totalCredits = reader.ReadInt32();
                                    string currentPickaxeCode = reader.ReadString();
                                    int blocksInIncrement = reader.ReadInt32();
                                    int currentIncrementSize = reader.ReadInt32();

                                    var progress = new MiningProgressData
                                    {
                                        TotalCredits = totalCredits
                                    };

                                    // Migrate single pickaxe progress if it exists
                                    if (!string.IsNullOrEmpty(currentPickaxeCode))
                                    {
                                        progress.PickaxeProgress[currentPickaxeCode] = new PickaxeProgressData
                                        {
                                            BlocksInIncrement = blocksInIncrement,
                                            CurrentIncrementSize = currentIncrementSize
                                        };
                                    }

                                    MiningProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in mining data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingMiningProgressSave = true;
                        }
                        else if (version == 3)
                        {
                            // V3 format: per-pickaxe progress (no LastActivityDay)
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new MiningProgressData
                                    {
                                        TotalCredits = reader.ReadInt32()
                                    };

                                    int pickaxeCount = reader.ReadInt32();
                                    for (int j = 0; j < pickaxeCount; j++)
                                    {
                                        string pickaxeCode = reader.ReadString();
                                        var pickaxeProgress = new PickaxeProgressData
                                        {
                                            BlocksInIncrement = reader.ReadInt32(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.PickaxeProgress[pickaxeCode] = pickaxeProgress;
                                    }

                                    MiningProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in mining data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingMiningProgressSave = true; // Re-save in v4 format
                        }
                        else if (version == 4)
                        {
                            // Current format: per-pickaxe progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new MiningProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    int pickaxeCount = reader.ReadInt32();
                                    for (int j = 0; j < pickaxeCount; j++)
                                    {
                                        string pickaxeCode = reader.ReadString();
                                        var pickaxeProgress = new PickaxeProgressData
                                        {
                                            BlocksInIncrement = reader.ReadInt32(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.PickaxeProgress[pickaxeCode] = pickaxeProgress;
                                    }

                                    MiningProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in mining data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded mining progress for {MiningProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load mining progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist melee progress to world save data.
        /// Version 1 format stores per-weapon progress dictionary.
        /// </summary>
        public static void PersistMeleeProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = MeleeProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x4D); // 'M' (for Melee)
                            writer.Write((byte)2);    // Version 2: Per-weapon progress + LastActivityDay

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.LastActivityDay);

                                // Write per-weapon progress dictionary
                                var weaponSnapshot = progress.WeaponProgress.ToArray();
                                writer.Write(weaponSnapshot.Length);
                                foreach (var weaponKvp in weaponSnapshot)
                                {
                                    writer.Write(weaponKvp.Key); // Weapon type
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MELEE_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted melee progress for {snapshot.Length} players (v2 format)");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist melee progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load melee progress from world save data.
        /// </summary>
        private void LoadMeleeProgress()
        {
            if (ServerApi == null) return;

            MeleeProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MELEE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No melee progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x4D) // "SIM"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid melee progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Legacy format: per-weapon progress, no LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new MeleeProgressData
                                    {
                                        TotalCredits = reader.ReadInt32()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponType = reader.ReadString();
                                        var weaponProgress = new WeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponType] = weaponProgress;
                                    }

                                    MeleeProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in melee data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingMeleeProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Current format: per-weapon progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new MeleeProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponType = reader.ReadString();
                                        var weaponProgress = new WeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponType] = weaponProgress;
                                    }

                                    MeleeProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in melee data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown melee save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded melee progress for {MeleeProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load melee progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist ranged progress to world save data.
        /// Version 2 format stores per-weapon progress dictionary + LastActivityDay.
        /// </summary>
        public static void PersistRangedProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = RangedProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x52); // 'R' (for Ranged)
                            writer.Write((byte)2);    // Version 2: Per-weapon progress + LastActivityDay

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.LastActivityDay);

                                // Write per-weapon progress dictionary
                                var weaponSnapshot = progress.WeaponProgress.ToArray();
                                writer.Write(weaponSnapshot.Length);
                                foreach (var weaponKvp in weaponSnapshot)
                                {
                                    writer.Write(weaponKvp.Key); // Weapon combo
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(RANGED_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted ranged progress for {snapshot.Length} players (v2 format)");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist ranged progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load ranged progress from world save data.
        /// </summary>
        private void LoadRangedProgress()
        {
            if (ServerApi == null) return;

            RangedProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(RANGED_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No ranged progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x52) // "SIR"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid ranged progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Legacy format: per-weapon progress, no LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new RangedProgressData
                                    {
                                        TotalCredits = reader.ReadInt32()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponCombo = reader.ReadString();
                                        var weaponProgress = new RangedWeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponCombo] = weaponProgress;
                                    }

                                    RangedProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in ranged data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingRangedProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Current format: per-weapon progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new RangedProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponCombo = reader.ReadString();
                                        var weaponProgress = new RangedWeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponCombo] = weaponProgress;
                                    }

                                    RangedProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in ranged data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown ranged save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded ranged progress for {RangedProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load ranged progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist walking progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistWalkingProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = WalkingProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x57); // 'W' (for Walking)
                            writer.Write((byte)2);    // Version 2

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.BlocksInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(WALKING_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted walking progress v2 for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist walking progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load walking progress from world save data.
        /// </summary>
        private void LoadWalkingProgress()
        {
            if (ServerApi == null) return;

            WalkingProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(WALKING_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No walking progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x57) // "SIW"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid walking progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new WalkingProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        BlocksInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };

                                    WalkingProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in walking data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingWalkingProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new WalkingProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        BlocksInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    WalkingProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in walking data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown walking save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded walking progress for {WalkingProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load walking progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist hunger progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistHungerProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = HungerProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x48); // 'H' (for Hunger)
                            writer.Write((byte)2);    // Version 2

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.SecondsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(HUNGER_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted hunger progress v2 for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist hunger progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load hunger progress from world save data.
        /// </summary>
        private void LoadHungerProgress()
        {
            if (ServerApi == null) return;

            HungerProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(HUNGER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No hunger progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x48) // "SIH"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid hunger progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new HungerProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        SecondsInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };

                                    HungerProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in hunger data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingHungerProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new HungerProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        SecondsInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    HungerProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in hunger data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown hunger save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded hunger progress for {HungerProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load hunger progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist armor progress to world save data.
        /// Version 1 format stores durability credits, walk speed credits, and per-armor progress.
        /// </summary>
        public static void PersistArmorProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ArmorProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Header: "SIA" + version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x41); // 'A' for Armor
                            writer.Write((byte)3);    // Version 3: v2 plus the hunger-reduction and healing credit pools

                            // Number of players
                            writer.Write(snapshot.Length);

                            foreach (var kvp in snapshot)
                            {
                                string playerUid = kvp.Key;
                                var progress = kvp.Value;

                                writer.Write(playerUid);
                                writer.Write(progress.TotalDurabilityCredits);
                                writer.Write(progress.TotalWalkSpeedCredits);
                                writer.Write(progress.LastActivityDay);
                                writer.Write(progress.TotalHungerReductionCredits);
                                writer.Write(progress.TotalHealingCredits);

                                // Snapshot inner dictionary to avoid concurrent modification
                                var armorSnapshot = progress.ArmorProgress.ToArray();
                                writer.Write(armorSnapshot.Length);
                                foreach (var armorKvp in armorSnapshot)
                                {
                                    string armorCode = armorKvp.Key;
                                    var armorProg = armorKvp.Value;

                                    writer.Write(armorCode);
                                    writer.Write(armorProg.SecondsWornInIncrement);
                                    writer.Write(armorProg.CurrentTimeIncrementSize);
                                    writer.Write(armorProg.TimeCredits);
                                    writer.Write(armorProg.DamageBlockedInIncrement);
                                    writer.Write(armorProg.CurrentDamageIncrementSize);
                                    writer.Write(armorProg.DamageCredits);
                                    writer.Write(armorProg.RepairsInIncrement);
                                    writer.Write(armorProg.CurrentRepairIncrementSize);
                                    writer.Write(armorProg.RepairCredits);
                                    writer.Write(armorProg.HasBeenEquipped);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(ARMOR_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Saved armor progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist armor progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load armor progress from world save data.
        /// </summary>
        private void LoadArmorProgress()
        {
            if (ServerApi == null) return;

            ArmorProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(ARMOR_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No armor progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x41) // "SIA"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid armor progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new ArmorProgressData
                                    {
                                        TotalDurabilityCredits = reader.ReadInt32(),
                                        TotalWalkSpeedCredits = reader.ReadInt32()
                                    };

                                    // Read per-armor progress
                                    int armorCount = reader.ReadInt32();
                                    for (int j = 0; j < armorCount; j++)
                                    {
                                        string armorCode = reader.ReadString();
                                        var armorProg = new ArmorPieceProgressData
                                        {
                                            SecondsWornInIncrement = reader.ReadSingle(),
                                            CurrentTimeIncrementSize = reader.ReadInt32(),
                                            TimeCredits = reader.ReadInt32(),
                                            DamageBlockedInIncrement = reader.ReadSingle(),
                                            CurrentDamageIncrementSize = reader.ReadInt32(),
                                            DamageCredits = reader.ReadInt32(),
                                            RepairsInIncrement = reader.ReadInt32(),
                                            CurrentRepairIncrementSize = reader.ReadInt32(),
                                            RepairCredits = reader.ReadInt32(),
                                            HasBeenEquipped = reader.ReadBoolean()
                                        };
                                        progress.ArmorProgress[armorCode] = armorProg;
                                    }

                                    ArmorProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in armor data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingArmorProgressSave = true;
                        }
                        else if (version == 2 || version == 3)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new ArmorProgressData
                                    {
                                        TotalDurabilityCredits = reader.ReadInt32(),
                                        TotalWalkSpeedCredits = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    if (version >= 3)
                                    {
                                        // v2 files never stored these two pools, which is why the
                                        // optional armor hunger and healing bonuses reset on restart.
                                        progress.TotalHungerReductionCredits = reader.ReadInt32();
                                        progress.TotalHealingCredits = reader.ReadInt32();
                                    }

                                    // Read per-armor progress
                                    int armorCount = reader.ReadInt32();
                                    for (int j = 0; j < armorCount; j++)
                                    {
                                        string armorCode = reader.ReadString();
                                        var armorProg = new ArmorPieceProgressData
                                        {
                                            SecondsWornInIncrement = reader.ReadSingle(),
                                            CurrentTimeIncrementSize = reader.ReadInt32(),
                                            TimeCredits = reader.ReadInt32(),
                                            DamageBlockedInIncrement = reader.ReadSingle(),
                                            CurrentDamageIncrementSize = reader.ReadInt32(),
                                            DamageCredits = reader.ReadInt32(),
                                            RepairsInIncrement = reader.ReadInt32(),
                                            CurrentRepairIncrementSize = reader.ReadInt32(),
                                            RepairCredits = reader.ReadInt32(),
                                            HasBeenEquipped = reader.ReadBoolean()
                                        };
                                        progress.ArmorProgress[armorCode] = armorProg;
                                    }

                                    ArmorProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in armor data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown armor save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded armor progress for {ArmorProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load armor progress: {ex.Message}");
            }
        }
    }
}
