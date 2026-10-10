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

        // Death penalty settings
        public static bool EnableDeathPenalty = false;
        public static double DeathPenaltyFraction = 0.5;
        public static bool DeathPenaltyFullReset = false;
        public static HashSet<string> DeathPenaltyExemptSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Binary-search for the minimum rawPenalty that, when water-leveled across per-tool
        /// absolute positions, reduces the total per-tool credits by at least targetLoss.
        /// Returns 0 if no drain is needed.
        /// </summary>
        private static double ComputeDeathPenaltyRawPenalty(
            List<(string key, double value)> absPositions,
            int targetLoss, int baseIncrement, int incrementStep)
        {
            if (absPositions.Count == 0 || targetLoss <= 0) return 0;

            double totalAbsPos = 0;
            int currentCredits = 0;
            foreach (var e in absPositions)
            {
                totalAbsPos += e.value;
                currentCredits += AbsolutePositionToToolState(e.value, baseIncrement, incrementStep).credits;
            }

            int targetCredits = Math.Max(0, currentCredits - targetLoss);
            if (currentCredits <= targetCredits) return 0;

            double lo = 0, hi = totalAbsPos + 1;
            for (int iter = 0; iter < 50; iter++)
            {
                double mid = (lo + hi) / 2;
                var test = absPositions.Select(e => (e.key, e.value)).ToList();
                DrainAccumulatorsLeveling(test, mid);
                int credits = 0;
                foreach (var e in test)
                    credits += AbsolutePositionToToolState(e.value, baseIncrement, incrementStep).credits;

                if (credits <= targetCredits)
                    hi = mid;
                else
                    lo = mid;
            }

            return hi;
        }

        /// <summary>
        /// Analytically compute the minimum rawPenalty needed to guarantee losing at least
        /// intendedLoss credits from a single-accumulator skill.
        /// </summary>
        private static double ComputeMinSingleAccumulatorPenalty(
            double currentAccumulator, int oldTotalCredits, int intendedLoss,
            int baseIncrement, int incrementStep)
        {
            if (intendedLoss <= 0 || oldTotalCredits <= 0) return 0;
            int targetCredits = Math.Max(0, oldTotalCredits - intendedLoss);
            double currentAbsPos = (double)oldTotalCredits * baseIncrement +
                (double)incrementStep * oldTotalCredits * (oldTotalCredits - 1) / 2.0 + Math.Max(0, currentAccumulator);
            // Max absolute position that still results in targetCredits (just below next credit boundary)
            double targetSum = (double)targetCredits * baseIncrement +
                (double)incrementStep * targetCredits * (targetCredits - 1) / 2.0;
            double nextCost = baseIncrement + targetCredits * incrementStep;
            double maxAllowed = targetSum + nextCost - 0.01;
            return Math.Max(0, currentAbsPos - maxAllowed);
        }

        /// <summary>
        /// DeathPenaltyFullReset: wipe every progression system back to zero, keeping
        /// only the systems the admin listed in DeathPenaltyExemptSkills (or disabled
        /// outright). Implemented as snapshot, full reset via the same routine
        /// /trait reset uses, restore, reapply, so it cannot drift from the reset logic.
        /// </summary>
        public static void ApplyFullDeathReset(IServerPlayer player)
        {
            if (Instance == null || player?.Entity == null) return;

            string uid = player.PlayerUID;
            var restores = new List<Action>();
            var keptSkills = new List<string>();

            void Keep<T>(string skill, ConcurrentDictionary<string, T> dict) where T : class
            {
                if (!DeathPenaltyExemptSkills.Contains(skill) && !DisabledSkills.Contains(skill)) return;
                // Remove the entry so ResetProgressForPlayer cannot touch it.
                // A TryGetValue snapshot would keep the same object reference,
                // and the reset mutates entries in place, so the restore would
                // write back an already-zeroed object.
                if (dict.TryRemove(uid, out var data))
                {
                    restores.Add(() => dict[uid] = data);
                    keptSkills.Add(skill);
                }
            }

            Keep("mining", MiningProgress);
            Keep("melee", MeleeProgress);
            Keep("ranged", RangedProgress);
            Keep("precise", PreciseProgress);
            Keep("walking", WalkingProgress);
            Keep("hunger", HungerProgress);
            Keep("armor", ArmorProgress);
            Keep("clothier", ClothierProgress);
            Keep("mender", MenderProgress);
            Keep("pilferer", PilfererProgress);
            Keep("resourceful", ResourcefulProgress);
            Keep("forager", ForagerProgress);
            Keep("furtive", FurtiveProgress);
            Keep("technical", TechnicalProgress);
            Keep("hardyhealth", HardyHealthProgress);
            Keep("bowyer", BowyerProgress);
            Keep("improviser", ImproviserProgress);
            Keep("tinkerer", TinkererProgress);
            Keep("merciless", MercilessProgress);
            Keep("claustrophobicremoval", ClaustrophobicRemovalProgress);
            Keep("coproficiency", COProgress);
            Keep("tempresist", TemporalResistanceProgress);
            Keep("temprecharge", TemporalRechargeProgress);

            Instance.ResetProgressForPlayer(player);

            foreach (var restore in restores) restore();

            // Reapply bonuses for whatever was restored (the same routine that runs on join).
            Instance.OnPlayerJoin(player);

            string keptNote = keptSkills.Count > 0 ? $" Kept: {string.Join(", ", keptSkills)}." : "";
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"Death penalty: all trait progression has been reset.{keptNote}",
                EnumChatType.Notification);
            ServerApi?.Logger.Debug($"[SeraphLeveling] Full death reset applied to {player.PlayerName}.{keptNote}");
        }

        /// <summary>
        /// Apply death penalty to all skills for a player.
        /// Drains BaseIncrement * DeathPenaltyFraction * sqrt(credits) raw points from each
        /// skill (a few minutes of play at the defaults), so a death costs at most one credit
        /// and usually only progress inside the current increment. Deliberately gentle:
        /// a credit at level 40 is 4,000 blocks, so taking whole credits would cost hours.
        /// ComputeDeathPenaltyRawPenalty / ComputeMinSingleAccumulatorPenalty implement a
        /// stricter credit-based drain and are kept unwired on purpose.
        /// </summary>
        public static void ApplyDeathPenalty(IServerPlayer player)
        {
            if (!EnableDeathPenalty) return;
            if (player?.Entity == null) return;

            if (DeathPenaltyFullReset)
            {
                ApplyFullDeathReset(player);
                return;
            }

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            int totalCreditsLost = 0;

            // --- Per-tool dictionary skills ---

            // Mining
            if (!DeathPenaltyExemptSkills.Contains("mining") && !DisabledSkills.Contains("mining"))
            {
                if (MiningProgress.TryGetValue(playerUid, out var miningProg) && (miningProg.TotalCredits > 0 || miningProg.PickaxeProgress.Count > 0))
                {
                    int oldCredits = miningProg.TotalCredits;

                    var toolEntries = miningProg.PickaxeProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.BlocksInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseBlocksPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseBlocksPerIncrement, IncrementStep, oldCredits,
                            (k, a, s) => { if (miningProg.PickaxeProgress.TryGetValue(k, out var p)) {
                                p.BlocksInIncrement = (int)Math.Floor(a); p.CurrentIncrementSize = s; } },
                            k => miningProg.PickaxeProgress.Remove(k), null, "Mining");
                        miningProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = IncrementStep > 0 ? (entry.Item3 - BaseBlocksPerIncrement) / IncrementStep : 0;
                            if (miningProg.PickaxeProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = IncrementStep > 0 ? (after.CurrentIncrementSize - BaseBlocksPerIncrement) / IncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.BlocksInIncrement}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            miningProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - miningProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Mining: {oldCredits} \u2192 {miningProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingMiningProgressSave = true;
                }
            }

            // Melee
            if (!DeathPenaltyExemptSkills.Contains("melee") && !DisabledSkills.Contains("melee"))
            {
                if (MeleeProgress.TryGetValue(playerUid, out var meleeProg) && (meleeProg.TotalCredits > 0 || meleeProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = meleeProg.TotalCredits;

                    var toolEntries = meleeProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseDamagePerIncrement, MeleeIncrementStep, oldCredits,
                            (k, a, s) => { if (meleeProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => meleeProg.WeaponProgress.Remove(k), null, "Melee");
                        meleeProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Melee: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = MeleeIncrementStep > 0 ? (entry.Item3 - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                            if (meleeProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = MeleeIncrementStep > 0 ? (after.CurrentIncrementSize - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            meleeProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - meleeProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Melee: {oldCredits} \u2192 {meleeProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingMeleeProgressSave = true;
                }
            }

            // Ranged
            if (!DeathPenaltyExemptSkills.Contains("ranged") && !DisabledSkills.Contains("ranged"))
            {
                if (RangedProgress.TryGetValue(playerUid, out var rangedProg) && (rangedProg.TotalCredits > 0 || rangedProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = rangedProg.TotalCredits;

                    var toolEntries = rangedProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseRangedDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseRangedDamagePerIncrement, RangedIncrementStep, oldCredits,
                            (k, a, s) => { if (rangedProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => rangedProg.WeaponProgress.Remove(k), null, "Ranged");
                        rangedProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Ranged: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = RangedIncrementStep > 0 ? (entry.Item3 - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                            if (rangedProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = RangedIncrementStep > 0 ? (after.CurrentIncrementSize - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            rangedProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - rangedProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Ranged: {oldCredits} \u2192 {rangedProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingRangedProgressSave = true;
                }
            }

            // Precise
            if (!DeathPenaltyExemptSkills.Contains("precise") && !DisabledSkills.Contains("precise"))
            {
                if (PreciseProgress.TryGetValue(playerUid, out var preciseProg) && (preciseProg.TotalCredits > 0 || preciseProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = preciseProg.TotalCredits;

                    var toolEntries = preciseProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BasePreciseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BasePreciseDamagePerIncrement, PreciseIncrementStep, oldCredits,
                            (k, a, s) => { if (preciseProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => preciseProg.WeaponProgress.Remove(k), null, "Precise");
                        preciseProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Precise: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = PreciseIncrementStep > 0 ? (entry.Item3 - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                            if (preciseProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = PreciseIncrementStep > 0 ? (after.CurrentIncrementSize - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            preciseProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - preciseProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Precise: {oldCredits} \u2192 {preciseProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingPreciseProgressSave = true;
                }
            }

            // --- Single accumulator skills ---

            // Walking
            if (!DeathPenaltyExemptSkills.Contains("walking") && !DisabledSkills.Contains("walking"))
            {
                if (WalkingProgress.TryGetValue(playerUid, out var walkingProg) && (walkingProg.TotalCredits > 0 || walkingProg.BlocksInIncrement > 0))
                {
                    int oldCredits = walkingProg.TotalCredits;
                    float oldAcc = walkingProg.BlocksInIncrement; int oldInc = walkingProg.CurrentIncrementSize;
                    double rawPenalty = BaseBlocksWalkedPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseBlocksWalkedPerIncrement, WalkingIncrementStep, null, "Walking");
                    walkingProg.TotalCredits = newCr; walkingProg.BlocksInIncrement = (float)newAcc; walkingProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingWalkingProgressSave = true;
                    sb.AppendLine($"  Walking: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

            // Hunger
            if (!DeathPenaltyExemptSkills.Contains("hunger") && !DisabledSkills.Contains("hunger"))
            {
                if (HungerProgress.TryGetValue(playerUid, out var hungerProg) && (hungerProg.TotalCredits > 0 || hungerProg.SecondsInIncrement > 0))
                {
                    int oldCredits = hungerProg.TotalCredits;
                    float oldAcc = hungerProg.SecondsInIncrement; int oldInc = hungerProg.CurrentIncrementSize;
                    double rawPenalty = BaseSecondsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseSecondsPerIncrement, HungerIncrementStep, null, "Hunger");
                    hungerProg.TotalCredits = newCr; hungerProg.SecondsInIncrement = (float)newAcc; hungerProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingHungerProgressSave = true;
                    sb.AppendLine($"  Hunger: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

            // Mender
            if (!DeathPenaltyExemptSkills.Contains("mender") && !DisabledSkills.Contains("mender"))
            {
                if (MenderProgress.TryGetValue(playerUid, out var menderProg) && (menderProg.TotalCredits > 0 || menderProg.RepairsInIncrement > 0))
                {
                    int oldCredits = menderProg.TotalCredits;
                    int oldAcc = menderProg.RepairsInIncrement; int oldInc = menderProg.CurrentIncrementSize;
                    double rawPenalty = BaseMenderRepairsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseMenderRepairsPerIncrement, MenderIncrementStep, null, "Mender");
                    menderProg.TotalCredits = newCr; menderProg.RepairsInIncrement = (int)Math.Floor(newAcc); menderProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingMenderProgressSave = true;
                    sb.AppendLine($"  Mender: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Pilferer
            if (!DeathPenaltyExemptSkills.Contains("pilferer") && !DisabledSkills.Contains("pilferer"))
            {
                if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg) && (pilfererProg.TotalCredits > 0 || pilfererProg.PointsInIncrement > 0))
                {
                    int oldCredits = pilfererProg.TotalCredits;
                    int oldAcc = pilfererProg.PointsInIncrement; int oldInc = pilfererProg.CurrentIncrementSize;
                    double rawPenalty = BasePilfererPointsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BasePilfererPointsPerIncrement, PilfererIncrementStep, null, "Pilferer");
                    pilfererProg.TotalCredits = newCr; pilfererProg.PointsInIncrement = (int)Math.Floor(newAcc); pilfererProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingPilfererProgressSave = true;
                    sb.AppendLine($"  Pilferer: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Resourceful
            if (!DeathPenaltyExemptSkills.Contains("resourceful") && !DisabledSkills.Contains("resourceful"))
            {
                if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg) && (resourcefulProg.TotalCredits > 0 || resourcefulProg.AnimalsInIncrement > 0))
                {
                    int oldCredits = resourcefulProg.TotalCredits;
                    int oldAcc = resourcefulProg.AnimalsInIncrement; int oldInc = resourcefulProg.CurrentIncrementSize;
                    double rawPenalty = BaseResourcefulAnimalsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseResourcefulAnimalsPerIncrement, ResourcefulIncrementStep, null, "Resourceful");
                    resourcefulProg.TotalCredits = newCr; resourcefulProg.AnimalsInIncrement = (int)Math.Floor(newAcc); resourcefulProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingResourcefulProgressSave = true;
                    sb.AppendLine($"  Resourceful: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Forager
            if (!DeathPenaltyExemptSkills.Contains("forager") && !DisabledSkills.Contains("forager"))
            {
                if (ForagerProgress.TryGetValue(playerUid, out var foragerProg) && (foragerProg.TotalCredits > 0 || foragerProg.CropsInIncrement > 0))
                {
                    int oldCredits = foragerProg.TotalCredits;
                    int oldAcc = foragerProg.CropsInIncrement; int oldInc = foragerProg.CurrentIncrementSize;
                    double rawPenalty = BaseForagerCropsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseForagerCropsPerIncrement, ForagerIncrementStep, null, "Forager");
                    foragerProg.TotalCredits = newCr; foragerProg.CropsInIncrement = (int)Math.Floor(newAcc); foragerProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingForagerProgressSave = true;
                    sb.AppendLine($"  Forager: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Furtive
            if (!DeathPenaltyExemptSkills.Contains("furtive") && !DisabledSkills.Contains("furtive"))
            {
                if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg) && (furtiveProg.TotalCredits > 0 || furtiveProg.BlocksInIncrement > 0))
                {
                    int oldCredits = furtiveProg.TotalCredits;
                    float oldAcc = furtiveProg.BlocksInIncrement; int oldInc = furtiveProg.CurrentIncrementSize;
                    double rawPenalty = BaseFurtiveSneakBlocksPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseFurtiveSneakBlocksPerIncrement, FurtiveIncrementStep, null, "Furtive");
                    furtiveProg.TotalCredits = newCr; furtiveProg.BlocksInIncrement = (float)newAcc; furtiveProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingFurtiveProgressSave = true;
                    sb.AppendLine($"  Furtive: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

            // --- CO Proficiency (per-proficiency subcredit drain) ---
            if (!DeathPenaltyExemptSkills.Contains("coproficiency") && !DisabledSkills.Contains("coproficiency") && IsCOCompatEnabled)
            {
                if (COProgress.TryGetValue(playerUid, out var coProg))
                {
                    int coCreditsLost = 0;
                    var coSb = new StringBuilder();
                    foreach (var profKvp in coProg.Proficiencies)
                    {
                        if (profKvp.Value.TotalCredits > 0 || profKvp.Value.WeaponProgress.Count > 0)
                        {
                            int oldProfCredits = profKvp.Value.TotalCredits;

                            var toolEntries = profKvp.Value.WeaponProgress.Select(kvp =>
                                (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                            if (toolEntries.Count > 0)
                            {
                                double rawPenalty = COBaseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldProfCredits));
                                var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                    COBaseDamagePerIncrement, COIncrementStep, oldProfCredits,
                                    (k, a, s) => { if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p)) {
                                        p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                    k => profKvp.Value.WeaponProgress.Remove(k), null, $"CO:{profKvp.Key}");
                                profKvp.Value.TotalCredits = newCr;
                                int actualLost = oldProfCredits - newCr;
                                coCreditsLost += actualLost;
                                coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                            }
                            else if (oldProfCredits > 0)
                            {
                                int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldProfCredits)));
                                intendedLoss = Math.Min(intendedLoss, oldProfCredits);
                                if (intendedLoss > 0)
                                {
                                    profKvp.Value.TotalCredits = Math.Max(0, oldProfCredits - intendedLoss);
                                    int actualLost = oldProfCredits - profKvp.Value.TotalCredits;
                                    coCreditsLost += actualLost;
                                    coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {profKvp.Value.TotalCredits} (-{actualLost} credits)");
                                }
                            }
                        }
                    }
                    if (coProg.SteadyAimCredits > 0)
                    {
                        int old = coProg.SteadyAimCredits;
                        int loss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, old)));
                        if (loss > 0)
                        {
                            coProg.SteadyAimCredits = Math.Max(0, old - loss);
                            int actualLoss = old - coProg.SteadyAimCredits;
                            coCreditsLost += actualLoss;
                            if (actualLoss > 0)
                                coSb.AppendLine($"    SteadyAim: {old} \u2192 {coProg.SteadyAimCredits} (-{actualLoss} credits)");
                        }
                    }
                    if (coCreditsLost > 0)
                    {
                        totalCreditsLost += coCreditsLost;
                        pendingCOProgressSave = true;
                        sb.AppendLine($"  CO Proficiency: -{coCreditsLost} credits");
                        sb.Append(coSb);
                    }
                }
            }

            // Re-apply bonuses and notify player
            if (totalCreditsLost > 0 || sb.Length > 0)
            {
                Instance?.ReapplyAllBonuses(player);

                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Death penalty applied (-{totalCreditsLost} total credits):\n{sb}Fight on to regain your skills!",
                    EnumChatType.Notification);

                ServerApi?.Logger.Debug($"[SeraphLeveling] Death penalty applied to {player.PlayerName}: {totalCreditsLost} total credits lost");
            }
        }
    }
}
