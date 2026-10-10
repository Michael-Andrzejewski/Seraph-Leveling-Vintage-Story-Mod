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
        // TECHNICAL TRAIT - Unlocks after repairing translocators
        // =========================================================================
        public const string TECHNICAL_STAT_CODE = "sitTechnicalBonus";
        private const string TECHNICAL_PROGRESS_SAVE_KEY = "sitTechnicalProgress";
        public const string WATCHED_TECHNICAL_UNLOCKED = "sitTechnicalUnlocked";
        public const string WATCHED_TECHNICAL_REPAIRS = "sitTechnicalRepairs";
        public const string TECHNICAL_TRAIT_CODE = "sittechnicalmastery";

        // Technical progression configuration
        public static int TechnicalRequiredTranslocatorRepairs = 5;  // Repairs needed to unlock

        // Storage for technical progress
        public static ConcurrentDictionary<string, TechnicalProgressData> TechnicalProgress = new ConcurrentDictionary<string, TechnicalProgressData>();
        private static volatile bool pendingTechnicalProgressSave = false;

        /// <summary>
        /// Apply Technical trait (unlocks translocator gear cost reduction).
        /// Sets the temporalGearTLRepairCost stat to -1 when unlocked, reducing gear cost by 1.
        /// </summary>
        private static void ApplyTechnicalBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_TECHNICAL_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, TECHNICAL_TRAIT_CODE, unlocked);

            // Set the temporal gear repair cost reduction stat
            // -1 means one fewer temporal gear needed to repair translocators
            // Clockmaker already has vanilla Technical (-1 gear). The stat is a flat sum,
            // so a second -1 would advance a repair two stages per gear. By default the
            // class ends level with everyone else; with EnableClassCapOffsets it stacks.
            int vanillaTechnical = PlayerHasGameTrait(player.Entity, "technical") ? 1 : 0;
            float gearCostReduction = unlocked ? -Math.Max(0, 1 - CapCounted(vanillaTechnical)) : 0f;
            player.Entity.Stats.Set("temporalGearTLRepairCost", TECHNICAL_STAT_CODE, gearCostReduction, false);
        }

        /// <summary>
        /// Handler for /trait technical command.
        /// </summary>
        private TextCommandResult OnTraitTechnicalCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Technical trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Translocators repaired: {progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs}");
            if (!progress.IsUnlocked)
            {
                int remaining = TechnicalRequiredTranslocatorRepairs - progress.TranslocatorsRepaired;
                sb.AppendLine($"Repair {remaining} more translocators to unlock!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait technicalunlock command.
        /// </summary>
        private TextCommandResult OnTraitTechnicalUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            progress.IsUnlocked = unlock;

            ApplyTechnicalBonusStatic(player, unlock);

            // Check if Tinkerer should be unlocked
            if (unlock)
            {
                CheckTinkererUnlock(player);
            }

            return TextCommandResult.Success($"Technical trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Process a translocator repair (called from Harmony patch).
        /// Gives progress toward Technical trait unlock.
        /// </summary>
        public static void ProcessTranslocatorRepair(IServerPlayer player)
        {
            ProcessTranslocatorRepairCore(player);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessTranslocatorRepairCore(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if technical skill is disabled
            if (IsSkillDisabled("technical")) return;

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            // Increment translocator repairs (apply sleep buff multiplier if active)
            int modifiedRepairs = ApplyXPMultiplier(playerUid, 1);
            progress.TranslocatorsRepaired += modifiedRepairs;

            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired translocator ({progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs})");

            // Check if we've reached the unlock threshold
            if (progress.TranslocatorsRepaired >= TechnicalRequiredTranslocatorRepairs)
            {
                progress.IsUnlocked = true;
                ApplyTechnicalBonusStatic(player, true);

                // Notify player
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-technical-unlock"));

                // Check if Tinkerer should now be unlocked
                CheckTinkererUnlock(player);
            }
        }
    }
}
