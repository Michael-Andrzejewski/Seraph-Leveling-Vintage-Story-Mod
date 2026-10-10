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
        public static void PersistCOProgress() => PersistTable(COProgress, CO_PROGRESS_SAVE_KEY, "CO", 0x43, 0x4F, 0x42, 2,
            (writer, playerProgress) =>
            {
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
            },
            persistedDebug: "[SeraphLeveling] Persisted CO progress for {0} players");

        /// <summary>
        /// Load Combat Overhaul proficiency progress from world save data.
        /// </summary>
        private void LoadCOProgress()
        {
            var v1 = new TableRowFormat<COPlayerProgressData>(
                reader: (reader, version) =>
                {
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

                    return playerProgress;
                },
                markRewrite: () => pendingCOProgressSave = true
            );
            var v2 = new TableRowFormat<COPlayerProgressData>(
                reader: (reader, version) =>
                {
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

                    return playerProgress;
                }
            );
            LoadTable(COProgress, CO_PROGRESS_SAVE_KEY, "CO", 0x43, 0x4F, 0x42, TableLoadStyle.Middle,
                new Dictionary<int, TableRowFormat<COPlayerProgressData>> { [1] = v1, [2] = v2 }, corruptLabel: "CO proficiency");
        }
    }
}
