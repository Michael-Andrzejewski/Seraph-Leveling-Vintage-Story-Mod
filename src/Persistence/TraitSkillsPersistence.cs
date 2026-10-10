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
        // PERSISTENCE METHODS FOR NEW TRAITS
        // =========================================================================

        /// <summary>
        /// Persist clothier progress to world save data.
        /// </summary>
        public static void PersistClothierProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ClothierProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.SewingKitUnlocked);
                                var clothesSnapshot = progress.UniqueClothesWorn.ToArray();
                                writer.Write(clothesSnapshot.Length);
                                foreach (var clothCode in clothesSnapshot)
                                {
                                    writer.Write(clothCode);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(CLOTHIER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist clothier progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load clothier progress from world save data.
        /// </summary>
        private void LoadClothierProgress()
        {
            ClothierProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CLOTHIER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No clothier progress data found");
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

                        if (magic1 != 0x43 || magic2 != 0x4C || magic3 != 0x54)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid clothier progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown clothier save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new ClothierProgressData();
                                progress.SewingKitUnlocked = reader.ReadBoolean();
                                int clothCount = reader.ReadInt32();
                                for (int j = 0; j < clothCount; j++)
                                {
                                    progress.UniqueClothesWorn.Add(reader.ReadString());
                                }
                                ClothierProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in clothier data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded clothier progress for {ClothierProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load clothier progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist mender progress to world save data.
        /// </summary>
        public static void PersistMenderProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = MenderProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x4D); // 'M'
                            writer.Write((byte)0x4E); // 'N'
                            writer.Write((byte)0x44); // 'D'
                            writer.Write((byte)2);    // Version 2 - added LastActivityDay

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.RepairsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MENDER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist mender progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load mender progress from world save data.
        /// </summary>
        private void LoadMenderProgress()
        {
            MenderProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MENDER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No mender progress data found");
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

                        if (magic1 != 0x4D || magic2 != 0x4E || magic3 != 0x44)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid mender progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 2)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown mender save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                if (version == 1)
                                {
                                    var progress = new MenderProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        RepairsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    MenderProgress[playerUid] = progress;
                                    pendingMenderProgressSave = true;
                                }
                                else if (version == 2)
                                {
                                    var progress = new MenderProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        RepairsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    MenderProgress[playerUid] = progress;
                                }
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in mender data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded mender progress for {MenderProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load mender progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist pilferer progress to world save data.
        /// </summary>
        public static void PersistPilfererProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = PilfererProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x50); // 'P'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)3);    // Version 3 - added LastActivityDay

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.PointsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(PILFERER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist pilferer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load pilferer progress from world save data.
        /// </summary>
        private void LoadPilfererProgress()
        {
            PilfererProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(PILFERER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No pilferer progress data found");
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

                        if (magic1 != 0x50 || magic2 != 0x4C || magic3 != 0x46)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid pilferer progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 3)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown pilferer save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                if (version == 1)
                                {
                                    var progress = new PilfererProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        PointsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    // Version 1 had chest positions - skip them
                                    int chestCount = reader.ReadInt32();
                                    for (int j = 0; j < chestCount; j++)
                                    {
                                        reader.ReadString(); // Skip old chest position data
                                    }
                                    PilfererProgress[playerUid] = progress;
                                    pendingPilfererProgressSave = true;
                                }
                                else if (version == 2)
                                {
                                    var progress = new PilfererProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        PointsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    PilfererProgress[playerUid] = progress;
                                    pendingPilfererProgressSave = true;
                                }
                                else if (version == 3)
                                {
                                    var progress = new PilfererProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        PointsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    PilfererProgress[playerUid] = progress;
                                }
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in pilferer data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded pilferer progress for {PilfererProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load pilferer progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist resourceful progress to world save data.
        /// </summary>
        public static void PersistResourcefulProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ResourcefulProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)2);    // Version 2 - added LastActivityDay

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.AnimalsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(RESOURCEFUL_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist resourceful progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load resourceful progress from world save data.
        /// </summary>
        private void LoadResourcefulProgress()
        {
            ResourcefulProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(RESOURCEFUL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No resourceful progress data found");
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

                        if (magic1 != 0x52 || magic2 != 0x53 || magic3 != 0x46)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid resourceful progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 2)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown resourceful save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                if (version == 1)
                                {
                                    var progress = new ResourcefulProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        AnimalsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    ResourcefulProgress[playerUid] = progress;
                                    pendingResourcefulProgressSave = true;
                                }
                                else if (version == 2)
                                {
                                    var progress = new ResourcefulProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        AnimalsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    ResourcefulProgress[playerUid] = progress;
                                }
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in resourceful data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded resourceful progress for {ResourcefulProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load resourceful progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist forager progress to world save data.
        /// </summary>
        public static void PersistForagerProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ForagerProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x47); // 'G'
                            writer.Write((byte)2);    // Version 2 - added LastActivityDay

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.CropsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(FORAGER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist forager progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load forager progress from world save data.
        /// </summary>
        private void LoadForagerProgress()
        {
            ForagerProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(FORAGER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No forager progress data found");
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

                        if (magic1 != 0x46 || magic2 != 0x52 || magic3 != 0x47)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid forager progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 2)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown forager save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                if (version == 1)
                                {
                                    var progress = new ForagerProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        CropsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    ForagerProgress[playerUid] = progress;
                                    pendingForagerProgressSave = true;
                                }
                                else if (version == 2)
                                {
                                    var progress = new ForagerProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        CropsInIncrement = reader.ReadInt32(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    ForagerProgress[playerUid] = progress;
                                }
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in forager data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded forager progress for {ForagerProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load forager progress: {ex.Message}");
            }
        }

        // =========================================================================
        // FURTIVE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist furtive progress to world save data.
        /// </summary>
        public static void PersistFurtiveProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = FurtiveProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)0x55); // 'U'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)2);    // Version 2

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.BlocksInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.LastActivityDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(FURTIVE_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist furtive progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load furtive progress from world save data.
        /// </summary>
        private void LoadFurtiveProgress()
        {
            FurtiveProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(FURTIVE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No furtive progress data found");
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

                        if (magic1 != 0x46 || magic2 != 0x55 || magic3 != 0x52)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid furtive progress magic bytes");
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
                                    var progress = new FurtiveProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        BlocksInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32()
                                    };
                                    FurtiveProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in furtive data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingFurtiveProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Current format: per-player progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new FurtiveProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        BlocksInIncrement = reader.ReadSingle(),
                                        CurrentIncrementSize = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };
                                    FurtiveProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in furtive data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown furtive save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded furtive progress for {FurtiveProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load furtive progress: {ex.Message}");
            }
        }

        // =========================================================================
        // PRECISE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist precise progress to world save data.
        /// </summary>
        public static void PersistPreciseProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = PreciseProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x50); // 'P'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)2);    // Version 2

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.LastActivityDay);

                                // Write weapon progress
                                var weaponSnapshot = progress.WeaponProgress.ToArray();
                                writer.Write(weaponSnapshot.Length);
                                foreach (var weaponKvp in weaponSnapshot)
                                {
                                    writer.Write(weaponKvp.Key);
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(PRECISE_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist precise progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load precise progress from world save data.
        /// </summary>
        private void LoadPreciseProgress()
        {
            PreciseProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(PRECISE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No precise progress data found");
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

                        if (magic1 != 0x50 || magic2 != 0x52 || magic3 != 0x43)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid precise progress magic bytes");
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
                                    var progress = new PreciseProgressData
                                    {
                                        TotalCredits = reader.ReadInt32()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponKey = reader.ReadString();
                                        var weaponProgress = new PreciseWeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponKey] = weaponProgress;
                                    }

                                    PreciseProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in precise data: {innerEx.Message}");
                                    break;
                                }
                            }
                            pendingPreciseProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Current format: per-weapon progress + LastActivityDay
                            for (int i = 0; i < playerCount; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    var progress = new PreciseProgressData
                                    {
                                        TotalCredits = reader.ReadInt32(),
                                        LastActivityDay = reader.ReadDouble()
                                    };

                                    int weaponCount = reader.ReadInt32();
                                    for (int j = 0; j < weaponCount; j++)
                                    {
                                        string weaponKey = reader.ReadString();
                                        var weaponProgress = new PreciseWeaponProgressData
                                        {
                                            DamageInIncrement = reader.ReadSingle(),
                                            CurrentIncrementSize = reader.ReadInt32()
                                        };
                                        progress.WeaponProgress[weaponKey] = weaponProgress;
                                    }

                                    PreciseProgress[playerUid] = progress;
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in precise data: {innerEx.Message}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown precise save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded precise progress for {PreciseProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load precise progress: {ex.Message}");
            }
        }
    }
}
