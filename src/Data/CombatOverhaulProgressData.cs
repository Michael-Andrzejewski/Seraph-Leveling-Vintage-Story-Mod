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
    // =========================================================================
    // COMBAT OVERHAUL COMPATIBILITY DATA CLASSES
    // =========================================================================

    /// <summary>
    /// Tracks progress for a specific CO weapon (for proficiency progression).
    /// Each weapon has its own increment counter that persists.
    /// </summary>
    public class COWeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public COWeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public COWeaponProgressData Clone()
        {
            return new COWeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking a single Combat Overhaul proficiency progression.
    /// Each proficiency type (bows, crossbows, one-handed swords, etc.) has its own instance.
    /// </summary>
    public class COProficiencyProgressData
    {
        /// <summary>Total credits earned (each credit = 0.01 proficiency bonus).</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon code (e.g., "combatoverhaul:crossbow-iron").</summary>
        public Dictionary<string, COWeaponProgressData> WeaponProgress { get; set; }

        public COProficiencyProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, COWeaponProgressData>();
        }

        /// <summary>
        /// Get or create progress data for a specific weapon.
        /// </summary>
        public COWeaponProgressData GetWeaponProgress(string weaponCode, int baseIncrement)
        {
            if (!WeaponProgress.TryGetValue(weaponCode, out var progress))
            {
                progress = new COWeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = baseIncrement
                };
                WeaponProgress[weaponCode] = progress;
            }
            return progress;
        }

        public COProficiencyProgressData Clone()
        {
            var clone = new COProficiencyProgressData
            {
                TotalCredits = this.TotalCredits,
                WeaponProgress = new Dictionary<string, COWeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Master data structure for all Combat Overhaul proficiency progressions for a player.
    /// Contains one COProficiencyProgressData per proficiency type.
    /// </summary>
    public class COPlayerProgressData
    {
        /// <summary>Progress for each proficiency stat. Key is stat name (e.g., "bowsProficiency").</summary>
        public Dictionary<string, COProficiencyProgressData> Proficiencies { get; set; }

        /// <summary>Steady Aim credits (earned alongside ranged proficiencies).</summary>
        public int SteadyAimCredits { get; set; }

        /// <summary>Last in-game day when any CO proficiency was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public COPlayerProgressData()
        {
            Proficiencies = new Dictionary<string, COProficiencyProgressData>();
            SteadyAimCredits = 0;
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific proficiency.
        /// </summary>
        public COProficiencyProgressData GetProficiencyProgress(string proficiencyStat)
        {
            if (!Proficiencies.TryGetValue(proficiencyStat, out var progress))
            {
                progress = new COProficiencyProgressData();
                Proficiencies[proficiencyStat] = progress;
            }
            return progress;
        }

        public COPlayerProgressData Clone()
        {
            var clone = new COPlayerProgressData
            {
                Proficiencies = new Dictionary<string, COProficiencyProgressData>(),
                SteadyAimCredits = this.SteadyAimCredits,
                LastActivityDay = this.LastActivityDay
            };
            foreach (var kvp in this.Proficiencies)
            {
                clone.Proficiencies[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }
}
