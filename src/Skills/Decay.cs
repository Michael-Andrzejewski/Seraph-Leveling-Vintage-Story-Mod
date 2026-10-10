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

        // Skill decay settings
        public static bool EnableSkillDecay = false;
        public static double DecayGracePeriodDays = 1.0;
        public static int DecayBasePointsPerDay = 10;
        public static int DecayMaxPointsPerDay = 100;
        public static HashSet<string> DecayExemptSkills = new HashSet<string>();
        public static Dictionary<string, double> DecayGracePeriodOverrides = new Dictionary<string, double>();
        public static Dictionary<string, int> DecayBasePointsOverrides = new Dictionary<string, int>();
        public static Dictionary<string, int> DecayMaxPointsOverrides = new Dictionary<string, int>();
        public static bool VerboseDecayLogging = false;

        // Per-player tracking for daily decay tick (maps player UID to last checked in-game day)
        public static ConcurrentDictionary<string, double> LastDecayCheckDay = new ConcurrentDictionary<string, double>();

        // =========================================================================
        // SKILL DECAY SYSTEM
        // =========================================================================

        /// <summary>
        /// Get per-skill decay parameters, falling back to global defaults.
        /// Returns (gracePeriodDays, basePointsPerDay, maxPointsPerDay).
        /// </summary>
        private static (double grace, int basePoints, int maxPoints) GetDecayParams(string skillKey)
        {
            double grace = DecayGracePeriodOverrides.TryGetValue(skillKey, out var g) ? g : DecayGracePeriodDays;
            int basePoints = DecayBasePointsOverrides.TryGetValue(skillKey, out var b) ? b : DecayBasePointsPerDay;
            int maxPoints = DecayMaxPointsOverrides.TryGetValue(skillKey, out var m) ? m : DecayMaxPointsPerDay;
            return (grace, basePoints, maxPoints);
        }

        /// <summary>
        /// Calculate decay points for one day of inactivity based on days since last activity.
        /// Uses triangular formula: consecutive inactive days multiply the base rate.
        /// Only calculates decay for a single day tick (not cumulative).
        /// </summary>
        private static int CalculateDecayPoints(double lastActivityDay, double currentDay,
            double gracePeriodDays, int basePointsPerDay, int maxPointsPerDay)
        {
            if (lastActivityDay <= 0) return 0; // No activity recorded yet, no decay
            if (currentDay <= lastActivityDay) return 0; // No time passed

            double daysSinceActivity = currentDay - lastActivityDay;
            double daysAfterGrace = daysSinceActivity - gracePeriodDays;

            if (daysAfterGrace <= 0) return 0; // Still in grace period

            // Triangular: the Nth day past grace loses N * base points
            int dayNumber = (int)Math.Ceiling(daysAfterGrace);
            int decayThisDay = dayNumber * basePointsPerDay;

            // Cap at maximum per day
            return Math.Min(decayThisDay, maxPointsPerDay);
        }

        /// <summary>
        /// Decay tick handler. Runs every 10 seconds, checks if a full in-game day has passed
        /// for each online player, and applies decay if so.
        /// </summary>
        private void OnDecayTick(float dt)
        {
            if (!EnableSkillDecay) return;
            if (ServerApi?.World == null) return;

            double currentDay = ServerApi.World.Calendar.TotalDays;

            foreach (var player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;

                string playerUid = player.PlayerUID;

                if (!LastDecayCheckDay.TryGetValue(playerUid, out double lastCheck))
                    continue; // Not initialized yet (hasn't joined)

                // Process one day at a time to handle multi-day skips incrementally
                while (currentDay - lastCheck >= 1.0)
                {
                    lastCheck += 1.0;
                    ApplyDailyDecay(player, lastCheck);
                }

                LastDecayCheckDay[playerUid] = lastCheck;
            }
        }

        /// <summary>
        /// Apply one day of decay for a single player across all 13 progression skills.
        /// Called from OnDecayTick when a full in-game day has elapsed.
        /// </summary>
        private void ApplyDailyDecay(IServerPlayer player, double currentDay)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            int totalDecayApplied = 0;
            StringBuilder verboseSb = VerboseDecayLogging ? new StringBuilder() : null;

            // --- Per-tool dictionary skills ---

            // Mining
            if (!DecayExemptSkills.Contains("mining") && !DisabledSkills.Contains("mining"))
            {
                if (MiningProgress.TryGetValue(playerUid, out var mProg) && (mProg.TotalCredits > 0 || mProg.PickaxeProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("mining");
                    int decayCredits = CalculateDecayPoints(mProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = mProg.TotalCredits;
                        var toolEntries = mProg.PickaxeProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.BlocksInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseBlocksPerIncrement, IncrementStep, oldCredits,
                                (k, a, s) => { if (mProg.PickaxeProgress.TryGetValue(k, out var p)) {
                                    p.BlocksInIncrement = (int)Math.Floor(a); p.CurrentIncrementSize = s; } },
                                k => mProg.PickaxeProgress.Remove(k), verboseSb, "Mining");
                            mProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = IncrementStep > 0 ? (entry.Item3 - BaseBlocksPerIncrement) / IncrementStep : 0;
                                if (mProg.PickaxeProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = IncrementStep > 0 ? (after.CurrentIncrementSize - BaseBlocksPerIncrement) / IncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.BlocksInIncrement}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            mProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Mining: {oldCredits} \u2192 {mProg.TotalCredits} (-{lost} credits)"); }
                        }
                    }
                }
            }

            // Melee
            if (!DecayExemptSkills.Contains("melee") && !DisabledSkills.Contains("melee"))
            {
                if (MeleeProgress.TryGetValue(playerUid, out var mProg) && (mProg.TotalCredits > 0 || mProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("melee");
                    int decayCredits = CalculateDecayPoints(mProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = mProg.TotalCredits;
                        var toolEntries = mProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseDamagePerIncrement, MeleeIncrementStep, oldCredits,
                                (k, a, s) => { if (mProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => mProg.WeaponProgress.Remove(k), verboseSb, "Melee");
                            mProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Melee: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = MeleeIncrementStep > 0 ? (entry.Item3 - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                if (mProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = MeleeIncrementStep > 0 ? (after.CurrentIncrementSize - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            mProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Melee: {oldCredits} \u2192 {mProg.TotalCredits} (-{lost} credits)"); }
                        }
                    }
                }
            }

            // Ranged
            if (!DecayExemptSkills.Contains("ranged") && !DisabledSkills.Contains("ranged"))
            {
                if (RangedProgress.TryGetValue(playerUid, out var rProg) && (rProg.TotalCredits > 0 || rProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("ranged");
                    int decayCredits = CalculateDecayPoints(rProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = rProg.TotalCredits;
                        var toolEntries = rProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseRangedDamagePerIncrement, RangedIncrementStep, oldCredits,
                                (k, a, s) => { if (rProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => rProg.WeaponProgress.Remove(k), verboseSb, "Ranged");
                            rProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Ranged: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = RangedIncrementStep > 0 ? (entry.Item3 - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                if (rProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = RangedIncrementStep > 0 ? (after.CurrentIncrementSize - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            rProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Ranged: {oldCredits} \u2192 {rProg.TotalCredits} (-{lost} credits)"); }
                        }
                    }
                }
            }

            // Precise
            if (!DecayExemptSkills.Contains("precise") && !DisabledSkills.Contains("precise"))
            {
                if (PreciseProgress.TryGetValue(playerUid, out var pProg) && (pProg.TotalCredits > 0 || pProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("precise");
                    int decayCredits = CalculateDecayPoints(pProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = pProg.TotalCredits;
                        var toolEntries = pProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BasePreciseDamagePerIncrement, PreciseIncrementStep, oldCredits,
                                (k, a, s) => { if (pProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => pProg.WeaponProgress.Remove(k), verboseSb, "Precise");
                            pProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Precise: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = PreciseIncrementStep > 0 ? (entry.Item3 - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                if (pProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = PreciseIncrementStep > 0 ? (after.CurrentIncrementSize - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            pProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Precise: {oldCredits} \u2192 {pProg.TotalCredits} (-{lost} credits)"); }
                        }
                    }
                }
            }

            // --- Single-accumulator skills ---

            // Walking
            if (!DecayExemptSkills.Contains("walking") && !DisabledSkills.Contains("walking"))
            {
                if (WalkingProgress.TryGetValue(playerUid, out var wProg) && (wProg.TotalCredits > 0 || wProg.BlocksInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("walking");
                    int decayCredits = CalculateDecayPoints(wProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = wProg.TotalCredits;
                        float oldAcc = wProg.BlocksInIncrement; int oldInc = wProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseBlocksWalkedPerIncrement, WalkingIncrementStep, verboseSb, "Walking");
                        wProg.TotalCredits = newCr; wProg.BlocksInIncrement = (float)newAcc; wProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Walking: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                    }
                }
            }

            // Hunger
            if (!DecayExemptSkills.Contains("hunger") && !DisabledSkills.Contains("hunger"))
            {
                if (HungerProgress.TryGetValue(playerUid, out var hProg) && (hProg.TotalCredits > 0 || hProg.SecondsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("hunger");
                    int decayCredits = CalculateDecayPoints(hProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = hProg.TotalCredits;
                        float oldAcc = hProg.SecondsInIncrement; int oldInc = hProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseSecondsPerIncrement, HungerIncrementStep, verboseSb, "Hunger");
                        hProg.TotalCredits = newCr; hProg.SecondsInIncrement = (float)newAcc; hProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Hunger: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                    }
                }
            }

            // Armor is exempt from decay (leveled by wearing new pieces, not renewable)

            // Mender
            if (!DecayExemptSkills.Contains("mender") && !DisabledSkills.Contains("mender"))
            {
                if (MenderProgress.TryGetValue(playerUid, out var meProg) && (meProg.TotalCredits > 0 || meProg.RepairsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("mender");
                    int decayCredits = CalculateDecayPoints(meProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = meProg.TotalCredits;
                        int oldAcc = meProg.RepairsInIncrement; int oldInc = meProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseMenderRepairsPerIncrement, MenderIncrementStep, verboseSb, "Mender");
                        meProg.TotalCredits = newCr; meProg.RepairsInIncrement = (int)Math.Floor(newAcc); meProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Mender: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                    }
                }
            }

            // Pilferer
            if (!DecayExemptSkills.Contains("pilferer") && !DisabledSkills.Contains("pilferer"))
            {
                if (PilfererProgress.TryGetValue(playerUid, out var piProg) && (piProg.TotalCredits > 0 || piProg.PointsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("pilferer");
                    int decayCredits = CalculateDecayPoints(piProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = piProg.TotalCredits;
                        int oldAcc = piProg.PointsInIncrement; int oldInc = piProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BasePilfererPointsPerIncrement, PilfererIncrementStep, verboseSb, "Pilferer");
                        piProg.TotalCredits = newCr; piProg.PointsInIncrement = (int)Math.Floor(newAcc); piProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Pilferer: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                    }
                }
            }

            // Resourceful
            if (!DecayExemptSkills.Contains("resourceful") && !DisabledSkills.Contains("resourceful"))
            {
                if (ResourcefulProgress.TryGetValue(playerUid, out var reProg) && (reProg.TotalCredits > 0 || reProg.AnimalsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("resourceful");
                    int decayCredits = CalculateDecayPoints(reProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = reProg.TotalCredits;
                        int oldAcc = reProg.AnimalsInIncrement; int oldInc = reProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseResourcefulAnimalsPerIncrement, ResourcefulIncrementStep, verboseSb, "Resourceful");
                        reProg.TotalCredits = newCr; reProg.AnimalsInIncrement = (int)Math.Floor(newAcc); reProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Resourceful: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                    }
                }
            }

            // Forager
            if (!DecayExemptSkills.Contains("forager") && !DisabledSkills.Contains("forager"))
            {
                if (ForagerProgress.TryGetValue(playerUid, out var fProg) && (fProg.TotalCredits > 0 || fProg.CropsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("forager");
                    int decayCredits = CalculateDecayPoints(fProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = fProg.TotalCredits;
                        int oldAcc = fProg.CropsInIncrement; int oldInc = fProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseForagerCropsPerIncrement, ForagerIncrementStep, verboseSb, "Forager");
                        fProg.TotalCredits = newCr; fProg.CropsInIncrement = (int)Math.Floor(newAcc); fProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Forager: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                    }
                }
            }

            // Furtive
            if (!DecayExemptSkills.Contains("furtive") && !DisabledSkills.Contains("furtive"))
            {
                if (FurtiveProgress.TryGetValue(playerUid, out var fuProg) && (fuProg.TotalCredits > 0 || fuProg.BlocksInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("furtive");
                    int decayCredits = CalculateDecayPoints(fuProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = fuProg.TotalCredits;
                        float oldAcc = fuProg.BlocksInIncrement; int oldInc = fuProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseFurtiveSneakBlocksPerIncrement, FurtiveIncrementStep, verboseSb, "Furtive");
                        fuProg.TotalCredits = newCr; fuProg.BlocksInIncrement = (float)newAcc; fuProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Furtive: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                    }
                }
            }

            // CO Proficiency (per-proficiency absolute-position drain + SteadyAim direct)
            if (!DecayExemptSkills.Contains("coproficiency") && !DisabledSkills.Contains("coproficiency") && IsCOCompatEnabled)
            {
                if (COProgress.TryGetValue(playerUid, out var coProg))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("coproficiency");
                    int decay = CalculateDecayPoints(coProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decay > 0)
                    {
                        int coDecayTotal = 0;
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
                                    double rawPenalty = (double)decay;

                                    var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                        COBaseDamagePerIncrement, COIncrementStep, oldProfCredits,
                                        (k, a, s) => { if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p)) {
                                            p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                        k => profKvp.Value.WeaponProgress.Remove(k), verboseSb, $"CO:{profKvp.Key}");
                                    profKvp.Value.TotalCredits = newCr;
                                    coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                                    coDecayTotal += lost;
                                }
                                else
                                {
                                    int lost = Math.Min(decay, oldProfCredits);
                                    profKvp.Value.TotalCredits -= lost;
                                    if (lost > 0) coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {profKvp.Value.TotalCredits} (-{lost} credits)");
                                    coDecayTotal += lost;
                                }
                            }
                        }
                        if (coProg.SteadyAimCredits > 0)
                        {
                            int old = coProg.SteadyAimCredits;
                            coProg.SteadyAimCredits = Math.Max(0, coProg.SteadyAimCredits - decay);
                            int steadyLost = old - coProg.SteadyAimCredits;
                            if (steadyLost > 0) coSb.AppendLine($"    SteadyAim: {old} \u2192 {coProg.SteadyAimCredits} (-{steadyLost} credits)");
                            coDecayTotal += steadyLost;
                        }
                        if (coDecayTotal > 0) totalDecayApplied += coDecayTotal;
                        if (coSb.Length > 0)
                        {
                            sb.AppendLine($"  CO Proficiency: -{coDecayTotal} credits");
                            sb.Append(coSb);
                        }
                    }
                }
            }

            // If any decay occurred, re-apply all bonuses and notify
            if (totalDecayApplied > 0 || sb.Length > 0)
            {
                if (totalDecayApplied > 0) ReapplyAllBonuses(player);

                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Skills decayed due to inactivity{(totalDecayApplied > 0 ? $" (-{totalDecayApplied} total credits)" : "")}:\n{sb}Use them to regain your progress!",
                    EnumChatType.Notification);

                ServerApi.Logger.Debug($"[SeraphLeveling] Daily decay applied to {player.PlayerName}: {totalDecayApplied} total credits lost");
            }

            if (VerboseDecayLogging && verboseSb != null && verboseSb.Length > 0)
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Decay Detail]\n{verboseSb}", EnumChatType.Notification);
        }

        // =========================================================================
        // ACCUMULATOR DRAIN MATH (shared by decay and the death penalty)
        // =========================================================================

        /// <summary>
        /// Drains 'penalty' raw points from a list of per-tool accumulators using water-leveling.
        /// Brings the highest accumulator down toward the next highest, then drains equally, etc.
        /// Returns remaining penalty after all accumulators are drained to zero.
        /// </summary>
        private static double DrainAccumulatorsLeveling(List<(string key, double value)> accumulators, double penalty)
        {
            if (accumulators == null || accumulators.Count == 0 || penalty <= 0) return penalty;

            // Sort descending by value
            accumulators.Sort((a, b) => b.value.CompareTo(a.value));

            double remaining = penalty;

            while (remaining > 0)
            {
                // Find the current top value
                double topValue = accumulators[0].value;
                if (topValue <= 0) break; // All accumulators are drained

                // Find how many entries share the top tier and the next level down
                int topCount = 1;
                double nextLevel = 0;
                for (int i = 1; i < accumulators.Count; i++)
                {
                    if (accumulators[i].value >= topValue - 0.001)
                    {
                        topCount++;
                    }
                    else
                    {
                        nextLevel = accumulators[i].value;
                        break;
                    }
                }

                // Cost to bring all top entries down to nextLevel
                double dropPerEntry = topValue - nextLevel;
                double totalCost = dropPerEntry * topCount;

                if (remaining >= totalCost)
                {
                    // Fully drain this tier to the next level
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, nextLevel);
                    }
                    remaining -= totalCost;
                }
                else
                {
                    // Partially drain: distribute remaining evenly among top entries
                    double drainPerEntry = remaining / topCount;
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, accumulators[i].value - drainPerEntry);
                    }
                    remaining = 0;
                }
            }

            // Clamp any negative values from floating point
            for (int i = 0; i < accumulators.Count; i++)
            {
                if (accumulators[i].value < 0)
                    accumulators[i] = (accumulators[i].key, 0);
            }

            return remaining;
        }

        /// <summary>
        /// Compute the absolute position (total raw points ever earned) for a single tool,
        /// given its current accumulator value and increment size.
        /// The cost for the Nth credit from this tool is: baseIncrement + (N-1)*incrementStep.
        /// So if currentIncrementSize = baseIncrement + N*incrementStep, then N credits were earned.
        /// Absolute position = sum of costs for those N credits + current accumulator.
        /// </summary>
        private static double ToolToAbsolutePosition(double accumulator, int currentIncrementSize,
            int baseIncrement, int incrementStep)
        {
            // N = number of credits this tool has earned
            int N = incrementStep > 0 ? (currentIncrementSize - baseIncrement) / incrementStep : 0;
            if (N < 0) N = 0;
            // Sum of costs: N*base + step*N*(N-1)/2
            double sumOfCosts = (double)N * baseIncrement + (double)incrementStep * N * (N - 1) / 2.0;
            return sumOfCosts + Math.Max(0, accumulator);
        }

        /// <summary>
        /// Convert an absolute position back into (credits, accumulator, nextIncrementSize).
        /// Subtracts escalating costs until the remainder is less than the next cost.
        /// </summary>
        private static (int credits, double accumulator, int incrementSize) AbsolutePositionToToolState(
            double absolutePosition, int baseIncrement, int incrementStep)
        {
            if (absolutePosition <= 0)
                return (0, 0, baseIncrement);

            int credits = 0;
            double remaining = absolutePosition;
            while (true)
            {
                int nextCost = baseIncrement + credits * incrementStep;
                if (remaining < nextCost)
                    break;
                remaining -= nextCost;
                credits++;
            }
            int incrementSize = baseIncrement + credits * incrementStep;
            // Guard against floating-point edge case where remaining ≈ incrementSize
            if (remaining >= incrementSize - 0.01)
                remaining = incrementSize - 1;
            return (credits, remaining, incrementSize);
        }

        /// <summary>
        /// Apply absolute-position decay to a set of per-tool accumulators.
        /// 1) Convert each tool to absolute position
        /// 2) Water-level drain absolute positions via DrainAccumulatorsLeveling
        /// 3) Convert back to (credits, accumulator, incrementSize) per tool
        /// 4) Write back via delegates; remove entries at zero
        /// Returns (newTotalCredits, creditsLost).
        /// </summary>
        private static (int newTotalCredits, int creditsLost) ApplyAbsolutePositionDecay(
            List<(string key, double accumulator, int incrementSize)> toolEntries,
            double rawPenalty, int baseIncrement, int incrementStep, int oldTotalCredits,
            Action<string, double, int> writeBack,
            Action<string> removeEntry,
            StringBuilder verboseLog, string skillName)
        {
            // Step 1: Convert to absolute positions
            var absPositions = new List<(string key, double value)>();
            foreach (var entry in toolEntries)
            {
                double absPos = ToolToAbsolutePosition(entry.accumulator, entry.incrementSize, baseIncrement, incrementStep);
                absPositions.Add((entry.key, absPos));
            }

            // Step 2: Water-level drain
            double remaining = DrainAccumulatorsLeveling(absPositions, rawPenalty);

            // Step 3: Convert back and write
            int newTotalCredits = 0;
            var toRemove = new List<string>();
            foreach (var absEntry in absPositions)
            {
                var (credits, accum, incSize) = AbsolutePositionToToolState(absEntry.value, baseIncrement, incrementStep);
                if (credits == 0 && accum < 0.001)
                {
                    toRemove.Add(absEntry.key);
                }
                else
                {
                    writeBack(absEntry.key, accum, incSize);
                    newTotalCredits += credits;
                }

                if (verboseLog != null)
                    verboseLog.AppendLine($"  [{skillName}] {absEntry.key}: absPos={absEntry.value:F1} -> cr={credits}, acc={accum:F1}, inc={incSize}");
            }

            foreach (var key in toRemove)
                removeEntry(key);

            // If there's remaining penalty after all tools drained to zero, subtract from credits directly
            if (remaining > 0.001 && newTotalCredits > 0)
            {
                // This shouldn't normally happen since absolute positions encompass credits,
                // but handle edge case of oldTotalCredits > sum of per-tool credits
                int extraLoss = (int)Math.Floor(remaining / baseIncrement);
                newTotalCredits = Math.Max(0, newTotalCredits - extraLoss);
            }

            int creditsLost = Math.Max(0, oldTotalCredits - newTotalCredits);
            return (newTotalCredits, creditsLost);
        }

        /// <summary>
        /// Apply absolute-position decay to a single-accumulator skill.
        /// Computes absolute position from oldTotalCredits (more robust than from incrementSize),
        /// subtracts rawPenalty, converts back.
        /// </summary>
        private static (int newCredits, double newAccumulator, int newIncrementSize, int creditsLost)
            ApplySingleAccumulatorDecay(
            double currentAccumulator, int currentIncrementSize, int oldTotalCredits,
            double rawPenalty, int baseIncrement, int incrementStep,
            StringBuilder verboseLog, string skillName)
        {
            // Compute absolute position from oldTotalCredits rather than incrementSize for robustness
            // Sum of costs for N credits: N*base + step*N*(N-1)/2
            double sumOfCosts = (double)oldTotalCredits * baseIncrement +
                (double)incrementStep * oldTotalCredits * (oldTotalCredits - 1) / 2.0;
            double absolutePosition = sumOfCosts + Math.Max(0, currentAccumulator);

            double newAbsPosition = Math.Max(0, absolutePosition - rawPenalty);
            var (newCredits, newAccumulator, newIncSize) = AbsolutePositionToToolState(newAbsPosition, baseIncrement, incrementStep);

            int creditsLost = Math.Max(0, oldTotalCredits - newCredits);

            if (verboseLog != null)
                verboseLog.AppendLine($"  [{skillName}] absPos={absolutePosition:F1} - penalty={rawPenalty:F1} -> newAbsPos={newAbsPosition:F1}, cr={newCredits}, acc={newAccumulator:F1}, inc={newIncSize}");

            return (newCredits, newAccumulator, newIncSize, creditsLost);
        }

        /// <summary>
        /// Handler for /trait decay command. Shows skill decay status.
        /// </summary>
        private TextCommandResult OnTraitDecayCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();

            if (!EnableSkillDecay)
            {
                sb.AppendLine("Skill Decay: DISABLED (not enabled in config)");
                return TextCommandResult.Success(sb.ToString());
            }

            sb.AppendLine("Skill Decay: ENABLED (online-only, checked once per in-game day)");
            sb.AppendLine($"  Default grace period: {DecayGracePeriodDays:F1} days");
            sb.AppendLine($"  Default base decay: {DecayBasePointsPerDay} points/day");
            sb.AppendLine($"  Default max decay: {DecayMaxPointsPerDay} points/day");
            if (DecayExemptSkills.Count > 0)
            {
                sb.AppendLine($"  Exempt skills: {string.Join(", ", DecayExemptSkills)}");
            }
            sb.AppendLine();

            string playerUid = player.PlayerUID;
            double currentDay = ServerApi?.World?.Calendar?.TotalDays ?? 0;

            // Combat skills
            sb.AppendLine("--- Combat Skills ---");
            AppendDecayStatus(sb, "Mining", "mining", playerUid, currentDay,
                () => MiningProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Melee", "melee", playerUid, currentDay,
                () => MeleeProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Ranged", "ranged", playerUid, currentDay,
                () => RangedProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Precise", "precise", playerUid, currentDay,
                () => PreciseProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Movement/Survival skills
            sb.AppendLine("--- Movement/Survival ---");
            AppendDecayStatus(sb, "Walking", "walking", playerUid, currentDay,
                () => WalkingProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Hunger", "hunger", playerUid, currentDay,
                () => HungerProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Furtive", "furtive", playerUid, currentDay,
                () => FurtiveProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Utility skills
            sb.AppendLine("--- Utility ---");
            AppendDecayStatus(sb, "Mender", "mender", playerUid, currentDay,
                () => MenderProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Pilferer", "pilferer", playerUid, currentDay,
                () => PilfererProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Resourceful", "resourceful", playerUid, currentDay,
                () => ResourcefulProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Forager", "forager", playerUid, currentDay,
                () => ForagerProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // CO Proficiency
            if (IsCOCompatEnabled)
            {
                sb.AppendLine("--- Combat Overhaul ---");
                AppendDecayStatus(sb, "CO Proficiency", "coproficiency", playerUid, currentDay,
                    () => COProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.SteadyAimCredits) : (0, 0));
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Helper to append decay status for a single skill with per-skill parameters.
        /// </summary>
        private void AppendDecayStatus(StringBuilder sb, string displayName, string skillKey, string playerUid, double currentDay, Func<(double lastActivity, int credits)> getProgress)
        {
            var (lastActivity, credits) = getProgress();
            bool exempt = DecayExemptSkills.Contains(skillKey);

            sb.Append($"  {displayName}: ");

            if (exempt)
            {
                sb.AppendLine("EXEMPT");
                return;
            }

            if (credits <= 0 && lastActivity <= 0)
            {
                sb.AppendLine("No progress yet");
                return;
            }

            if (lastActivity <= 0)
            {
                sb.AppendLine($"{credits} credits (no activity tracked)");
                return;
            }

            var (grace, basePoints, maxPoints) = GetDecayParams(skillKey);
            double daysSinceActivity = currentDay - lastActivity;
            double daysUntilDecay = grace - daysSinceActivity;
            int pendingDecay = CalculateDecayPoints(lastActivity, currentDay, grace, basePoints, maxPoints);

            // Show per-skill params if they differ from global defaults
            string paramInfo = "";
            if (grace != DecayGracePeriodDays || basePoints != DecayBasePointsPerDay || maxPoints != DecayMaxPointsPerDay)
            {
                paramInfo = $" [grace:{grace:F1}d, base:{basePoints}, max:{maxPoints}]";
            }

            if (daysUntilDecay > 0)
            {
                sb.AppendLine($"{credits} credits - Safe ({daysUntilDecay:F1} days until decay){paramInfo}");
            }
            else if (pendingDecay > 0)
            {
                sb.AppendLine($"{credits} credits - DECAYING (-{pendingDecay} pts/day, {daysSinceActivity:F1} days inactive){paramInfo}");
            }
            else
            {
                sb.AppendLine($"{credits} credits - Active{paramInfo}");
            }
        }
    }
}
