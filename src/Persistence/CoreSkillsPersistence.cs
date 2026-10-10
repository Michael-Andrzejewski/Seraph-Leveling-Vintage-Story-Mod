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
        public static void PersistMiningProgress() => PersistTable(MiningProgress, MINING_PROGRESS_SAVE_KEY, "mining", 0x53, 0x49, 0x54, 4,
            (writer, progress) =>
            {
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
            },
            persistedDebug: "[SeraphLeveling] Persisted mining progress for {0} players (v4 format)");

        /// <summary>
        /// Load mining progress from world save data.
        /// Supports versions 1 (legacy blocks), 2 (single pickaxe), and 3 (per-pickaxe).
        /// </summary>
        private void LoadMiningProgress()
        {
            var v1 = new TableRowFormat<MiningProgressData>(
                reader: (reader, version) =>
                {
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
                    return progress;
                },
                notice: "[SeraphLeveling] Converting legacy v1 save data to v3 format...",
                markRewrite: () => pendingMiningProgressSave = true
            );
            var v2 = new TableRowFormat<MiningProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                notice: "[SeraphLeveling] Converting v2 save data to v3 format...",
                markRewrite: () => pendingMiningProgressSave = true
            );
            var v3 = new TableRowFormat<MiningProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                markRewrite: () => pendingMiningProgressSave = true
            );
            var v4 = new TableRowFormat<MiningProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                }
            );
            LoadTable(MiningProgress, MINING_PROGRESS_SAVE_KEY, "mining", 0x53, 0x49, 0x54, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<MiningProgressData>> { [1] = v1, [2] = v2, [3] = v3, [4] = v4 }, unknownVersionMessage: "[SeraphLeveling] Unknown save format version {0}");
        }

        /// <summary>
        /// Persist melee progress to world save data.
        /// Version 1 format stores per-weapon progress dictionary.
        /// </summary>
        public static void PersistMeleeProgress() => PersistTable(MeleeProgress, MELEE_PROGRESS_SAVE_KEY, "melee", 0x53, 0x49, 0x4D, 2,
            (writer, progress) =>
            {
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
            },
            persistedDebug: "[SeraphLeveling] Persisted melee progress for {0} players (v2 format)");

        /// <summary>
        /// Load melee progress from world save data.
        /// </summary>
        private void LoadMeleeProgress()
        {
            var v1 = new TableRowFormat<MeleeProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                markRewrite: () => pendingMeleeProgressSave = true
            );
            var v2 = new TableRowFormat<MeleeProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                }
            );
            LoadTable(MeleeProgress, MELEE_PROGRESS_SAVE_KEY, "melee", 0x53, 0x49, 0x4D, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<MeleeProgressData>> { [1] = v1, [2] = v2 });
        }

        /// <summary>
        /// Persist ranged progress to world save data.
        /// Version 2 format stores per-weapon progress dictionary + LastActivityDay.
        /// </summary>
        public static void PersistRangedProgress() => PersistTable(RangedProgress, RANGED_PROGRESS_SAVE_KEY, "ranged", 0x53, 0x49, 0x52, 2,
            (writer, progress) =>
            {
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
            },
            persistedDebug: "[SeraphLeveling] Persisted ranged progress for {0} players (v2 format)");

        /// <summary>
        /// Load ranged progress from world save data.
        /// </summary>
        private void LoadRangedProgress()
        {
            var v1 = new TableRowFormat<RangedProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                markRewrite: () => pendingRangedProgressSave = true
            );
            var v2 = new TableRowFormat<RangedProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                }
            );
            LoadTable(RangedProgress, RANGED_PROGRESS_SAVE_KEY, "ranged", 0x53, 0x49, 0x52, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<RangedProgressData>> { [1] = v1, [2] = v2 });
        }

        /// <summary>
        /// Persist walking progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistWalkingProgress() => PersistTable(WalkingProgress, WALKING_PROGRESS_SAVE_KEY, "walking", 0x53, 0x49, 0x57, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.BlocksInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            },
            persistedDebug: "[SeraphLeveling] Persisted walking progress v2 for {0} players");

        /// <summary>
        /// Load walking progress from world save data.
        /// </summary>
        private void LoadWalkingProgress()
        {
            var v1 = new TableRowFormat<WalkingProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new WalkingProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };

                    return progress;
                },
                markRewrite: () => pendingWalkingProgressSave = true
            );
            var v2 = new TableRowFormat<WalkingProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new WalkingProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        BlocksInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    return progress;
                }
            );
            LoadTable(WalkingProgress, WALKING_PROGRESS_SAVE_KEY, "walking", 0x53, 0x49, 0x57, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<WalkingProgressData>> { [1] = v1, [2] = v2 });
        }

        /// <summary>
        /// Persist hunger progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistHungerProgress() => PersistTable(HungerProgress, HUNGER_PROGRESS_SAVE_KEY, "hunger", 0x53, 0x49, 0x48, 2,
            (writer, progress) =>
            {
                writer.Write(progress.TotalCredits);
                writer.Write(progress.SecondsInIncrement);
                writer.Write(progress.CurrentIncrementSize);
                writer.Write(progress.LastActivityDay);
            },
            persistedDebug: "[SeraphLeveling] Persisted hunger progress v2 for {0} players");

        /// <summary>
        /// Load hunger progress from world save data.
        /// </summary>
        private void LoadHungerProgress()
        {
            var v1 = new TableRowFormat<HungerProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new HungerProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        SecondsInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32()
                    };

                    return progress;
                },
                markRewrite: () => pendingHungerProgressSave = true
            );
            var v2 = new TableRowFormat<HungerProgressData>(
                reader: (reader, version) =>
                {
                    var progress = new HungerProgressData
                    {
                        TotalCredits = reader.ReadInt32(),
                        SecondsInIncrement = reader.ReadSingle(),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };

                    return progress;
                }
            );
            LoadTable(HungerProgress, HUNGER_PROGRESS_SAVE_KEY, "hunger", 0x53, 0x49, 0x48, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<HungerProgressData>> { [1] = v1, [2] = v2 });
        }

        /// <summary>
        /// Persist armor progress to world save data.
        /// Version 1 format stores durability credits, walk speed credits, and per-armor progress.
        /// </summary>
        public static void PersistArmorProgress() => PersistTable(ArmorProgress, ARMOR_PROGRESS_SAVE_KEY, "armor", 0x53, 0x49, 0x41, 3,
            (writer, progress) =>
            {
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
            },
            persistedDebug: "[SeraphLeveling] Saved armor progress for {0} players");

        /// <summary>
        /// Load armor progress from world save data.
        /// </summary>
        private void LoadArmorProgress()
        {
            var v1 = new TableRowFormat<ArmorProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                },
                markRewrite: () => pendingArmorProgressSave = true
            );
            var v2or3 = new TableRowFormat<ArmorProgressData>(
                reader: (reader, version) =>
                {
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

                    return progress;
                }
            );
            LoadTable(ArmorProgress, ARMOR_PROGRESS_SAVE_KEY, "armor", 0x53, 0x49, 0x41, TableLoadStyle.Core,
                new Dictionary<int, TableRowFormat<ArmorProgressData>> { [1] = v1, [2] = v2or3, [3] = v2or3 });
        }
    }
}
