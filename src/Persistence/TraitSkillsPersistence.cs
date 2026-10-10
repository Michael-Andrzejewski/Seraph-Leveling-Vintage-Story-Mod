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
        public static void PersistClothierProgress() => PersistTable(ClothierProgress, CLOTHIER_PROGRESS_SAVE_KEY, "clothier", 0x43, 0x4C, 0x54, 1,
            (writer, progress) =>
            {
                writer.Write(progress.SewingKitUnlocked);
                var clothesSnapshot = progress.UniqueClothesWorn.ToArray();
                writer.Write(clothesSnapshot.Length);
                foreach (var clothCode in clothesSnapshot)
                {
                    writer.Write(clothCode);
                }
            });

        /// <summary>
        /// Load clothier progress from world save data.
        /// </summary>
        private void LoadClothierProgress() => LoadTableRangeChecked(ClothierProgress, CLOTHIER_PROGRESS_SAVE_KEY, "clothier", 0x43, 0x4C, 0x54, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new ClothierProgressData();
                progress.SewingKitUnlocked = reader.ReadBoolean();
                int clothCount = reader.ReadInt32();
                for (int j = 0; j < clothCount; j++)
                {
                    progress.UniqueClothesWorn.Add(reader.ReadString());
                }
                return progress;
            });

        /// <summary>
        /// Persist mender progress to world save data.
        /// </summary>
        public static void PersistMenderProgress() => PersistTable(MenderProgress, MENDER_PROGRESS_SAVE_KEY, "mender", 0x4D, 0x4E, 0x44, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.RepairsInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            });

        /// <summary>
        /// Load mender progress from world save data.
        /// </summary>
        private void LoadMenderProgress() => LoadTableRangeChecked(MenderProgress, MENDER_PROGRESS_SAVE_KEY, "mender", 0x4D, 0x4E, 0x44, maxVersion: 2,
            (reader, version) =>
            {
                if (version == 1)
                {
                    var progress = new MenderProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        RepairsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    pendingMenderProgressSave = true;
                    return progress;
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
                    return progress;
                }

                return null;
            });

        /// <summary>
        /// Persist pilferer progress to world save data.
        /// </summary>
        public static void PersistPilfererProgress() => PersistTable(PilfererProgress, PILFERER_PROGRESS_SAVE_KEY, "pilferer", 0x50, 0x4C, 0x46, 3,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.PointsInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            });

        /// <summary>
        /// Load pilferer progress from world save data.
        /// </summary>
        private void LoadPilfererProgress() => LoadTableRangeChecked(PilfererProgress, PILFERER_PROGRESS_SAVE_KEY, "pilferer", 0x50, 0x4C, 0x46, maxVersion: 3,
            (reader, version) =>
            {
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
                    pendingPilfererProgressSave = true;
                    return progress;
                }
                else if (version == 2)
                {
                    var progress = new PilfererProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        PointsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    pendingPilfererProgressSave = true;
                    return progress;
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
                    return progress;
                }

                return null;
            });

        /// <summary>
        /// Persist resourceful progress to world save data.
        /// </summary>
        public static void PersistResourcefulProgress() => PersistTable(ResourcefulProgress, RESOURCEFUL_PROGRESS_SAVE_KEY, "resourceful", 0x52, 0x53, 0x46, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.AnimalsInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            });

        /// <summary>
        /// Load resourceful progress from world save data.
        /// </summary>
        private void LoadResourcefulProgress() => LoadTableRangeChecked(ResourcefulProgress, RESOURCEFUL_PROGRESS_SAVE_KEY, "resourceful", 0x52, 0x53, 0x46, maxVersion: 2,
            (reader, version) =>
            {
                if (version == 1)
                {
                    var progress = new ResourcefulProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        AnimalsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    pendingResourcefulProgressSave = true;
                    return progress;
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
                    return progress;
                }

                return null;
            });

        /// <summary>
        /// Persist forager progress to world save data.
        /// </summary>
        public static void PersistForagerProgress() => PersistTable(ForagerProgress, FORAGER_PROGRESS_SAVE_KEY, "forager", 0x46, 0x52, 0x47, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.CropsInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            });

        /// <summary>
        /// Load forager progress from world save data.
        /// </summary>
        private void LoadForagerProgress() => LoadTableRangeChecked(ForagerProgress, FORAGER_PROGRESS_SAVE_KEY, "forager", 0x46, 0x52, 0x47, maxVersion: 2,
            (reader, version) =>
            {
                if (version == 1)
                {
                    var progress = new ForagerProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        CropsInIncrement = reader.ReadInt32(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    pendingForagerProgressSave = true;
                    return progress;
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
                    return progress;
                }

                return null;
            });

        // =========================================================================
        // FURTIVE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist furtive progress to world save data.
        /// </summary>
        public static void PersistFurtiveProgress() => PersistTable(FurtiveProgress, FURTIVE_PROGRESS_SAVE_KEY, "furtive", 0x46, 0x55, 0x52, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.BlocksInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            });

        /// <summary>
        /// Load furtive progress from world save data.
        /// </summary>
        private void LoadFurtiveProgress()
        {
            var v1 = new TableRowFormat<FurtiveProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new FurtiveProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                    return progress;
                },
                markRewrite: () => pendingFurtiveProgressSave = true
            );
            var v2 = new TableRowFormat<FurtiveProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new FurtiveProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                    return progress;
                }
            );
            LoadTable(FurtiveProgress, FURTIVE_PROGRESS_SAVE_KEY, "furtive", 0x46, 0x55, 0x52, TableLoadStyle.Middle,
                new Dictionary<int, TableRowFormat<FurtiveProgressData>> { [1] = v1, [2] = v2 });
        }

        // =========================================================================
        // PRECISE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist precise progress to world save data.
        /// </summary>
        public static void PersistPreciseProgress() => PersistTable(PreciseProgress, PRECISE_PROGRESS_SAVE_KEY, "precise", 0x50, 0x52, 0x43, 2,
            (writer, progress) =>
            {
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
            });

        /// <summary>
        /// Load precise progress from world save data.
        /// </summary>
        private void LoadPreciseProgress()
        {
            var v1 = new TableRowFormat<PreciseProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                markRewrite: () => pendingPreciseProgressSave = true
            );
            var v2 = new TableRowFormat<PreciseProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                }
            );
            LoadTable(PreciseProgress, PRECISE_PROGRESS_SAVE_KEY, "precise", 0x50, 0x52, 0x43, TableLoadStyle.Middle,
                new Dictionary<int, TableRowFormat<PreciseProgressData>> { [1] = v1, [2] = v2 });
        }
    }
}
