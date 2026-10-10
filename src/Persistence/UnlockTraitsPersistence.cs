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
        // TECHNICAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist technical progress to world save data.
        /// </summary>
        public static void PersistTechnicalProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = TechnicalProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)0x45); // 'E'
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TranslocatorsRepaired);
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(TECHNICAL_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist technical progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load technical progress from world save data.
        /// </summary>
        private void LoadTechnicalProgress()
        {
            TechnicalProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(TECHNICAL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No technical progress data found");
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

                        if (magic1 != 0x54 || magic2 != 0x45 || magic3 != 0x43)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid technical progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown technical save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new TechnicalProgressData
                                {
                                    TranslocatorsRepaired = reader.ReadInt32(),
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                TechnicalProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in technical data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded technical progress for {TechnicalProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load technical progress: {ex.Message}");
            }
        }

        // =========================================================================
        // HARDY HEALTH TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist hardy health progress to world save data.
        /// </summary>
        public static void PersistHardyHealthProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = HardyHealthProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x48); // 'H'
                            writer.Write((byte)0x44); // 'D'
                            writer.Write((byte)0x48); // 'H'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(HARDY_HEALTH_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist hardy health progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load hardy health progress from world save data.
        /// </summary>
        private void LoadHardyHealthProgress()
        {
            HardyHealthProgress.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(HARDY_HEALTH_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No hardy health progress data found");
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

                        if (magic1 != 0x48 || magic2 != 0x44 || magic3 != 0x48)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid hardy health progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown hardy health save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new HardyHealthProgressData
                                {
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                HardyHealthProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in hardy health data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded hardy health progress for {HardyHealthProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load hardy health progress: {ex.Message}");
            }
        }

        // =========================================================================
        // BOWYER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist bowyer progress to world save data.
        /// </summary>
        public static void PersistBowyerProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = BowyerProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x42); // 'B'
                            writer.Write((byte)0x57); // 'W'
                            writer.Write((byte)0x59); // 'Y'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalBowDamage);
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(BOWYER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist bowyer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load bowyer progress from world save data.
        /// </summary>
        private void LoadBowyerProgress()
        {
            BowyerProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(BOWYER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No bowyer progress data found");
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

                        if (magic1 != 0x42 || magic2 != 0x57 || magic3 != 0x59)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid bowyer progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown bowyer save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new BowyerProgressData
                                {
                                    TotalBowDamage = reader.ReadSingle(),
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                BowyerProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in bowyer data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded bowyer progress for {BowyerProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load bowyer progress: {ex.Message}");
            }
        }

        // =========================================================================
        // IMPROVISER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist improviser progress to world save data.
        /// </summary>
        public static void PersistImproviserProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ImproviserProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x4D); // 'M'
                            writer.Write((byte)0x50); // 'P'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalRockDamage);
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(IMPROVISER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist improviser progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load improviser progress from world save data.
        /// </summary>
        private void LoadImproviserProgress()
        {
            ImproviserProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(IMPROVISER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No improviser progress data found");
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

                        if (magic1 != 0x49 || magic2 != 0x4D || magic3 != 0x50)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid improviser progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown improviser save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new ImproviserProgressData
                                {
                                    TotalRockDamage = reader.ReadSingle(),
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                ImproviserProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in improviser data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded improviser progress for {ImproviserProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load improviser progress: {ex.Message}");
            }
        }

        // =========================================================================
        // TINKERER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist tinkerer progress to world save data.
        /// </summary>
        public static void PersistTinkererProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = TinkererProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)0x4E); // 'N'
                            writer.Write((byte)0x4B); // 'K'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(TINKERER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist tinkerer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load tinkerer progress from world save data.
        /// </summary>
        private void LoadTinkererProgress()
        {
            TinkererProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(TINKERER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No tinkerer progress data found");
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

                        if (magic1 != 0x54 || magic2 != 0x4E || magic3 != 0x4B)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid tinkerer progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown tinkerer save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new TinkererProgressData
                                {
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                TinkererProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in tinkerer data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded tinkerer progress for {TinkererProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load tinkerer progress: {ex.Message}");
            }
        }

        // =========================================================================
        // MERCILESS TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist merciless progress to world save data.
        /// </summary>
        public static void PersistMercilessProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = MercilessProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x4D); // 'M'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.IsUnlocked);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MERCILESS_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist merciless progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load merciless progress from world save data.
        /// </summary>
        private void LoadMercilessProgress()
        {
            MercilessProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MERCILESS_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No merciless progress data found");
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

                        if (magic1 != 0x4D || magic2 != 0x52 || magic3 != 0x43)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid merciless progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown merciless save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new MercilessProgressData
                                {
                                    IsUnlocked = reader.ReadBoolean()
                                };
                                MercilessProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in merciless data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded merciless progress for {MercilessProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load merciless progress: {ex.Message}");
            }
        }

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist claustrophobic removal progress to world save data.
        /// </summary>
        public static void PersistClaustrophobicRemovalProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = ClaustrophobicRemovalProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.IsRemoved);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist claustrophobic removal progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load claustrophobic removal progress from world save data.
        /// </summary>
        private void LoadClaustrophobicRemovalProgress()
        {
            ClaustrophobicRemovalProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No claustrophobic removal progress data found");
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

                        if (magic1 != 0x43 || magic2 != 0x4C || magic3 != 0x52)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid claustrophobic removal progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > 1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown claustrophobic removal save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                var progress = new ClaustrophobicRemovalProgressData
                                {
                                    IsRemoved = reader.ReadBoolean()
                                };
                                ClaustrophobicRemovalProgress[playerUid] = progress;
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in claustrophobic removal data: {innerEx.Message}");
                                break;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded claustrophobic removal progress for {ClaustrophobicRemovalProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load claustrophobic removal progress: {ex.Message}");
            }
        }
    }
}
