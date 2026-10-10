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
        public static void PersistTechnicalProgress() => PersistTable(TechnicalProgress, TECHNICAL_PROGRESS_SAVE_KEY, "technical", 0x54, 0x45, 0x43, 1,
            (writer, progress) =>
            {
                writer.Write(progress.TranslocatorsRepaired);
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load technical progress from world save data.
        /// </summary>
        private void LoadTechnicalProgress() => LoadTableRangeChecked(TechnicalProgress, TECHNICAL_PROGRESS_SAVE_KEY, "technical", 0x54, 0x45, 0x43, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new TechnicalProgressData
                {
                    TranslocatorsRepaired = reader.ReadInt32(),
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // HARDY HEALTH TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist hardy health progress to world save data.
        /// </summary>
        public static void PersistHardyHealthProgress() => PersistTable(HardyHealthProgress, HARDY_HEALTH_PROGRESS_SAVE_KEY, "hardy health", 0x48, 0x44, 0x48, 1,
            (writer, progress) =>
            {
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load hardy health progress from world save data.
        /// </summary>
        private void LoadHardyHealthProgress() => LoadTableRangeChecked(HardyHealthProgress, HARDY_HEALTH_PROGRESS_SAVE_KEY, "hardy health", 0x48, 0x44, 0x48, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new HardyHealthProgressData
                {
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // BOWYER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist bowyer progress to world save data.
        /// </summary>
        public static void PersistBowyerProgress() => PersistTable(BowyerProgress, BOWYER_PROGRESS_SAVE_KEY, "bowyer", 0x42, 0x57, 0x59, 1,
            (writer, progress) =>
            {
                writer.Write(progress.TotalBowDamage);
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load bowyer progress from world save data.
        /// </summary>
        private void LoadBowyerProgress() => LoadTableRangeChecked(BowyerProgress, BOWYER_PROGRESS_SAVE_KEY, "bowyer", 0x42, 0x57, 0x59, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new BowyerProgressData
                {
                    TotalBowDamage = reader.ReadSingle(),
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // IMPROVISER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist improviser progress to world save data.
        /// </summary>
        public static void PersistImproviserProgress() => PersistTable(ImproviserProgress, IMPROVISER_PROGRESS_SAVE_KEY, "improviser", 0x49, 0x4D, 0x50, 1,
            (writer, progress) =>
            {
                writer.Write(progress.TotalRockDamage);
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load improviser progress from world save data.
        /// </summary>
        private void LoadImproviserProgress() => LoadTableRangeChecked(ImproviserProgress, IMPROVISER_PROGRESS_SAVE_KEY, "improviser", 0x49, 0x4D, 0x50, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new ImproviserProgressData
                {
                    TotalRockDamage = reader.ReadSingle(),
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // TINKERER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist tinkerer progress to world save data.
        /// </summary>
        public static void PersistTinkererProgress() => PersistTable(TinkererProgress, TINKERER_PROGRESS_SAVE_KEY, "tinkerer", 0x54, 0x4E, 0x4B, 1,
            (writer, progress) =>
            {
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load tinkerer progress from world save data.
        /// </summary>
        private void LoadTinkererProgress() => LoadTableRangeChecked(TinkererProgress, TINKERER_PROGRESS_SAVE_KEY, "tinkerer", 0x54, 0x4E, 0x4B, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new TinkererProgressData
                {
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // MERCILESS TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist merciless progress to world save data.
        /// </summary>
        public static void PersistMercilessProgress() => PersistTable(MercilessProgress, MERCILESS_PROGRESS_SAVE_KEY, "merciless", 0x4D, 0x52, 0x43, 1,
            (writer, progress) =>
            {
                writer.Write(progress.IsUnlocked);
            });

        /// <summary>
        /// Load merciless progress from world save data.
        /// </summary>
        private void LoadMercilessProgress() => LoadTableRangeChecked(MercilessProgress, MERCILESS_PROGRESS_SAVE_KEY, "merciless", 0x4D, 0x52, 0x43, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new MercilessProgressData
                {
                    IsUnlocked = reader.ReadBoolean()
                };
                return progress;
            });

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist claustrophobic removal progress to world save data.
        /// </summary>
        public static void PersistClaustrophobicRemovalProgress() => PersistTable(ClaustrophobicRemovalProgress, CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY, "claustrophobic removal", 0x43, 0x4C, 0x52, 1,
            (writer, progress) =>
            {
                writer.Write(progress.IsRemoved);
            });

        /// <summary>
        /// Load claustrophobic removal progress from world save data.
        /// </summary>
        private void LoadClaustrophobicRemovalProgress() => LoadTableRangeChecked(ClaustrophobicRemovalProgress, CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY, "claustrophobic removal", 0x43, 0x4C, 0x52, maxVersion: 1,
            (reader, version) =>
            {
                var progress = new ClaustrophobicRemovalProgressData
                {
                    IsRemoved = reader.ReadBoolean()
                };
                return progress;
            });
    }
}
