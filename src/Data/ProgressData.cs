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
    /// <summary>
    /// Tracks progress for a specific ranged weapon combination (for ranged damage progression).
    /// Each weapon combination (bow+arrow) has its own increment counter that persists.
    /// </summary>
    public class RangedWeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon combination.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon combination (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public RangedWeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public RangedWeaponProgressData Clone()
        {
            return new RangedWeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking ranged damage progression with per-weapon progress.
    /// Each weapon combination remembers its own increment counter, encouraging use of many weapon types.
    /// </summary>
    public class RangedProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus to damage/accuracy/distance). Max 130.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon combination (e.g., "bow-long+arrow-copper").</summary>
        public Dictionary<string, RangedWeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public RangedProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, RangedWeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon combination.
        /// New weapons start with the configured BaseRangedDamagePerIncrement.
        /// </summary>
        public RangedWeaponProgressData GetWeaponProgress(string weaponCombo)
        {
            if (!WeaponProgress.TryGetValue(weaponCombo, out var progress))
            {
                progress = new RangedWeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseRangedDamagePerIncrement
                };
                WeaponProgress[weaponCombo] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public RangedProgressData Clone()
        {
            var clone = new RangedProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, RangedWeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Data structure for tracking walking speed progression.
    /// Simpler than other progression systems since walking has no "tools".
    /// </summary>
    public class WalkingProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 15.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Blocks walked toward the next credit.</summary>
        public float BlocksInIncrement { get; set; }

        /// <summary>Blocks needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public WalkingProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 1000; // Base increment size
            LastActivityDay = 0;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public WalkingProgressData Clone()
        {
            return new WalkingProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Data structure for tracking hunger rate progression.
    /// Simpler than other progression systems since hunger has no "tools".
    /// Tracks time spent at full saturation.
    /// </summary>
    public class HungerProgressData
    {
        /// <summary>Total credits earned (each credit = 1% hunger rate reduction). Max 25.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Seconds at full saturation toward the next credit.</summary>
        public float SecondsInIncrement { get; set; }

        /// <summary>Seconds needed for the next credit (300, 360, 420, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public HungerProgressData()
        {
            TotalCredits = 0;
            SecondsInIncrement = 0;
            CurrentIncrementSize = 300; // Base increment size (5 minutes)
            LastActivityDay = 0;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public HungerProgressData Clone()
        {
            return new HungerProgressData
            {
                TotalCredits = this.TotalCredits,
                SecondsInIncrement = this.SecondsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Tracks progress for a specific armor piece (for armor progression).
    /// Each armor piece tracks time worn, damage blocked, repairs, and first-equip bonus.
    /// </summary>
    public class ArmorPieceProgressData
    {
        /// <summary>Seconds worn in this armor piece toward next time credit.</summary>
        public float SecondsWornInIncrement { get; set; }

        /// <summary>Seconds needed for next time credit with this armor piece (2880, 5760, etc.).</summary>
        public int CurrentTimeIncrementSize { get; set; }

        /// <summary>Time credits earned with this armor piece.</summary>
        public int TimeCredits { get; set; }

        /// <summary>Damage blocked toward next damage credit with this armor piece.</summary>
        public float DamageBlockedInIncrement { get; set; }

        /// <summary>Damage needed for next damage credit with this armor piece (100, 200, etc.).</summary>
        public int CurrentDamageIncrementSize { get; set; }

        /// <summary>Damage credits earned with this armor piece.</summary>
        public int DamageCredits { get; set; }

        /// <summary>Repairs done toward next repair credit with this armor piece.</summary>
        public int RepairsInIncrement { get; set; }

        /// <summary>Repairs needed for next repair credit with this armor piece (1, 2, etc.).</summary>
        public int CurrentRepairIncrementSize { get; set; }

        /// <summary>Repair credits earned with this armor piece.</summary>
        public int RepairCredits { get; set; }

        /// <summary>Whether this armor piece has been equipped before (for first-equip bonus).</summary>
        public bool HasBeenEquipped { get; set; }

        public ArmorPieceProgressData()
        {
            SecondsWornInIncrement = 0;
            CurrentTimeIncrementSize = 2880; // 1 VS day (48 minutes) in seconds
            TimeCredits = 0;
            DamageBlockedInIncrement = 0;
            CurrentDamageIncrementSize = 100; // Base damage for first credit
            DamageCredits = 0;
            RepairsInIncrement = 0;
            CurrentRepairIncrementSize = 1; // Base repairs for first credit
            RepairCredits = 0;
            HasBeenEquipped = false;
        }

        public ArmorPieceProgressData Clone()
        {
            return new ArmorPieceProgressData
            {
                SecondsWornInIncrement = this.SecondsWornInIncrement,
                CurrentTimeIncrementSize = this.CurrentTimeIncrementSize,
                TimeCredits = this.TimeCredits,
                DamageBlockedInIncrement = this.DamageBlockedInIncrement,
                CurrentDamageIncrementSize = this.CurrentDamageIncrementSize,
                DamageCredits = this.DamageCredits,
                RepairsInIncrement = this.RepairsInIncrement,
                CurrentRepairIncrementSize = this.CurrentRepairIncrementSize,
                RepairCredits = this.RepairCredits,
                HasBeenEquipped = this.HasBeenEquipped
            };
        }
    }

    /// <summary>
    /// Data structure for tracking armor progression with per-piece progress.
    /// Armor XP comes from: first-equip bonus, time worn, damage blocked, and repairs.
    /// </summary>
    public class ArmorProgressData
    {
        /// <summary>Total durability credits earned (each = 1% armor durability bonus).</summary>
        public int TotalDurabilityCredits { get; set; }

        /// <summary>Total walk speed penalty reduction credits earned (each = 1% reduction).</summary>
        public int TotalWalkSpeedCredits { get; set; }

        /// <summary>Total hunger reduction credits earned (each = 1% hunger rate reduction). Optional feature.</summary>
        public int TotalHungerReductionCredits { get; set; }

        /// <summary>Total healing effectiveness credits earned (each = 1% healing bonus). Optional feature.</summary>
        public int TotalHealingCredits { get; set; }

        /// <summary>Per-armor piece progress tracking. Key is armor code (e.g., "game:armor-body-plate-iron").</summary>
        public Dictionary<string, ArmorPieceProgressData> ArmorProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ArmorProgressData()
        {
            TotalDurabilityCredits = 0;
            TotalWalkSpeedCredits = 0;
            TotalHungerReductionCredits = 0;
            TotalHealingCredits = 0;
            ArmorProgress = new Dictionary<string, ArmorPieceProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific armor piece.
        /// </summary>
        public ArmorPieceProgressData GetArmorProgress(string armorCode)
        {
            if (!ArmorProgress.TryGetValue(armorCode, out var progress))
            {
                progress = new ArmorPieceProgressData
                {
                    CurrentTimeIncrementSize = SeraphLevelingModSystem.BaseSecondsInArmorPerIncrement,
                    CurrentDamageIncrementSize = SeraphLevelingModSystem.BaseDamageBlockedPerIncrement,
                    CurrentRepairIncrementSize = SeraphLevelingModSystem.BaseRepairsPerIncrement
                };
                ArmorProgress[armorCode] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public ArmorProgressData Clone()
        {
            var clone = new ArmorProgressData
            {
                TotalDurabilityCredits = this.TotalDurabilityCredits,
                TotalWalkSpeedCredits = this.TotalWalkSpeedCredits,
                TotalHungerReductionCredits = this.TotalHungerReductionCredits,
                TotalHealingCredits = this.TotalHealingCredits,
                LastActivityDay = this.LastActivityDay,
                ArmorProgress = new Dictionary<string, ArmorPieceProgressData>()
            };
            foreach (var kvp in this.ArmorProgress)
            {
                clone.ArmorProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Tracks progress for a specific weapon type (for melee damage progression).
    /// Each weapon type has its own increment counter that persists.
    /// </summary>
    public class WeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon type.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon type (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public WeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public WeaponProgressData Clone()
        {
            return new WeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking melee damage progression with per-weapon progress.
    /// Each weapon type remembers its own increment counter, encouraging use of many weapon types.
    /// </summary>
    public class MeleeProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 150.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon type (e.g., "sword", "falx", "spear").</summary>
        public Dictionary<string, WeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MeleeProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, WeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon type.
        /// New weapons start with the configured BaseDamagePerIncrement.
        /// </summary>
        public WeaponProgressData GetWeaponProgress(string weaponType)
        {
            if (!WeaponProgress.TryGetValue(weaponType, out var progress))
            {
                progress = new WeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseDamagePerIncrement
                };
                WeaponProgress[weaponType] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public MeleeProgressData Clone()
        {
            var clone = new MeleeProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, WeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Tracks progress for a specific pickaxe type.
    /// Each pickaxe type has its own increment counter that persists.
    /// </summary>
    public class PickaxeProgressData
    {
        /// <summary>Points accumulated toward the next credit with this pickaxe.</summary>
        public int BlocksInIncrement { get; set; }

        /// <summary>Points needed for the next credit with this pickaxe (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public PickaxeProgressData()
        {
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public PickaxeProgressData Clone()
        {
            return new PickaxeProgressData
            {
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking mining progression with per-pickaxe progress.
    /// Each pickaxe type remembers its own increment counter, encouraging use of many pickaxe types.
    /// </summary>
    public class MiningProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 150.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-pickaxe progress tracking. Key is pickaxe code (e.g., "game:pickaxe-copper").</summary>
        public Dictionary<string, PickaxeProgressData> PickaxeProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MiningProgressData()
        {
            TotalCredits = 0;
            PickaxeProgress = new Dictionary<string, PickaxeProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific pickaxe.
        /// New pickaxes start with the configured BaseBlocksPerIncrement.
        /// </summary>
        public PickaxeProgressData GetPickaxeProgress(string pickaxeCode)
        {
            if (!PickaxeProgress.TryGetValue(pickaxeCode, out var progress))
            {
                progress = new PickaxeProgressData
                {
                    BlocksInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BaseBlocksPerIncrement
                };
                PickaxeProgress[pickaxeCode] = progress;
            }
            return progress;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public MiningProgressData Clone()
        {
            var clone = new MiningProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                PickaxeProgress = new Dictionary<string, PickaxeProgressData>()
            };
            foreach (var kvp in this.PickaxeProgress)
            {
                clone.PickaxeProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Data structure for tracking Clothier progression.
    /// Tracks unique clothing items worn to unlock sewing kit crafting.
    /// </summary>
    public class ClothierProgressData
    {
        /// <summary>Set of unique clothing item codes that have been worn.</summary>
        public HashSet<string> UniqueClothesWorn { get; set; }

        /// <summary>Whether the sewing kit crafting has been unlocked.</summary>
        public bool SewingKitUnlocked { get; set; }

        public ClothierProgressData()
        {
            UniqueClothesWorn = new HashSet<string>();
            SewingKitUnlocked = false;
        }

        public ClothierProgressData Clone()
        {
            return new ClothierProgressData
            {
                UniqueClothesWorn = new HashSet<string>(this.UniqueClothesWorn),
                SewingKitUnlocked = this.SewingKitUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Mender progression.
    /// Tracks repairs done with sewing kit to earn armor/clothing durability bonuses.
    /// </summary>
    public class MenderProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Repairs done toward the next credit.</summary>
        public int RepairsInIncrement { get; set; }

        /// <summary>Repairs needed for the next credit (5, 6, 7, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public MenderProgressData()
        {
            TotalCredits = 0;
            RepairsInIncrement = 0;
            CurrentIncrementSize = 5; // Base increment size
            LastActivityDay = 0;
        }

        public MenderProgressData Clone()
        {
            return new MenderProgressData
            {
                TotalCredits = this.TotalCredits,
                RepairsInIncrement = this.RepairsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Pilferer progression.
    /// Tracks loot vessels broken for loot bonuses.
    /// </summary>
    public class PilfererProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Points accumulated toward the next credit.</summary>
        public int PointsInIncrement { get; set; }

        /// <summary>Points needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public PilfererProgressData()
        {
            TotalCredits = 0;
            PointsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public PilfererProgressData Clone()
        {
            return new PilfererProgressData
            {
                TotalCredits = this.TotalCredits,
                PointsInIncrement = this.PointsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Resourceful progression.
    /// Tracks animal harvesting for loot and speed bonuses.
    /// </summary>
    public class ResourcefulProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Animals harvested toward the next credit.</summary>
        public int AnimalsInIncrement { get; set; }

        /// <summary>Animals needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ResourcefulProgressData()
        {
            TotalCredits = 0;
            AnimalsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ResourcefulProgressData Clone()
        {
            return new ResourcefulProgressData
            {
                TotalCredits = this.TotalCredits,
                AnimalsInIncrement = this.AnimalsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Forager progression.
    /// Tracks wild crop breaking for foraging loot bonuses.
    /// </summary>
    public class ForagerProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Wild crops broken toward the next credit.</summary>
        public int CropsInIncrement { get; set; }

        /// <summary>Crops needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public ForagerProgressData()
        {
            TotalCredits = 0;
            CropsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            LastActivityDay = 0;
        }

        public ForagerProgressData Clone()
        {
            return new ForagerProgressData
            {
                TotalCredits = this.TotalCredits,
                CropsInIncrement = this.CropsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Furtive progression.
    /// Tracks blocks of sneaking for animal detection range reduction.
    /// </summary>
    public class FurtiveProgressData
    {
        /// <summary>Total credits earned (each credit = -1% animal detection range). Max 35.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Sneaking blocks accumulated toward the next credit.</summary>
        public float BlocksInIncrement { get; set; }

        /// <summary>Blocks needed for the next credit (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public FurtiveProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
            LastActivityDay = 0;
        }

        public FurtiveProgressData Clone()
        {
            return new FurtiveProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }
    }

    /// <summary>
    /// Tracks progress for a specific weapon type (for Precise damage to mechanicals).
    /// Each weapon type has its own increment counter that persists.
    /// </summary>
    public class PreciseWeaponProgressData
    {
        /// <summary>Damage accumulated toward the next credit with this weapon type.</summary>
        public float DamageInIncrement { get; set; }

        /// <summary>Damage needed for the next credit with this weapon type (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public PreciseWeaponProgressData()
        {
            DamageInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public PreciseWeaponProgressData Clone()
        {
            return new PreciseWeaponProgressData
            {
                DamageInIncrement = this.DamageInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Precise progression.
    /// Tracks damage dealt to mechanical creatures for damage bonus.
    /// </summary>
    public class PreciseProgressData
    {
        /// <summary>Total credits earned (each credit = +1% damage to mechanicals). Max 30.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Per-weapon progress tracking. Key is weapon type (e.g., "sword-copper", "spear-iron").</summary>
        public Dictionary<string, PreciseWeaponProgressData> WeaponProgress { get; set; }

        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public PreciseProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, PreciseWeaponProgressData>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific weapon type.
        /// </summary>
        public PreciseWeaponProgressData GetWeaponProgress(string weaponType)
        {
            if (!WeaponProgress.TryGetValue(weaponType, out var progress))
            {
                progress = new PreciseWeaponProgressData
                {
                    DamageInIncrement = 0,
                    CurrentIncrementSize = SeraphLevelingModSystem.BasePreciseDamagePerIncrement
                };
                WeaponProgress[weaponType] = progress;
            }
            return progress;
        }

        public PreciseProgressData Clone()
        {
            var clone = new PreciseProgressData
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                WeaponProgress = new Dictionary<string, PreciseWeaponProgressData>()
            };
            foreach (var kvp in this.WeaponProgress)
            {
                clone.WeaponProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Data structure for tracking Technical progression.
    /// Binary unlock after repairing translocators.
    /// </summary>
    public class TechnicalProgressData
    {
        /// <summary>Number of translocators repaired.</summary>
        public int TranslocatorsRepaired { get; set; }

        /// <summary>Whether the Technical trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public TechnicalProgressData()
        {
            TranslocatorsRepaired = 0;
            IsUnlocked = false;
        }

        public TechnicalProgressData Clone()
        {
            return new TechnicalProgressData
            {
                TranslocatorsRepaired = this.TranslocatorsRepaired,
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Hardy health unlock progression.
    /// One-time burst unlock when reaching mining and armor durability thresholds.
    /// </summary>
    public class HardyHealthProgressData
    {
        /// <summary>Whether the Hardy health bonus has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public HardyHealthProgressData()
        {
            IsUnlocked = false;
        }

        public HardyHealthProgressData Clone()
        {
            return new HardyHealthProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Bowyer unlock progression.
    /// Tracks bow damage with simple bow and longbow for unlock.
    /// </summary>
    public class BowyerProgressData
    {
        /// <summary>Total damage dealt with simple bow or longbow.</summary>
        public float TotalBowDamage { get; set; }

        /// <summary>Whether the Bowyer trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public BowyerProgressData()
        {
            TotalBowDamage = 0;
            IsUnlocked = false;
        }

        public BowyerProgressData Clone()
        {
            return new BowyerProgressData
            {
                TotalBowDamage = this.TotalBowDamage,
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Improviser unlock progression.
    /// Tracks damage dealt with thrown rocks for sling unlock.
    /// </summary>
    public class ImproviserProgressData
    {
        /// <summary>Total damage dealt with thrown rocks.</summary>
        public float TotalRockDamage { get; set; }

        /// <summary>Whether the Improviser trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public ImproviserProgressData()
        {
            TotalRockDamage = 0;
            IsUnlocked = false;
        }

        public ImproviserProgressData Clone()
        {
            return new ImproviserProgressData
            {
                TotalRockDamage = this.TotalRockDamage,
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Tinkerer unlock progression.
    /// Unlocks after obtaining Technical trait and reaching Precise damage threshold.
    /// </summary>
    public class TinkererProgressData
    {
        /// <summary>Whether the Tinkerer trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public TinkererProgressData()
        {
            IsUnlocked = false;
        }

        public TinkererProgressData Clone()
        {
            return new TinkererProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Merciless unlock progression.
    /// Unlocks after reaching armor durability and melee damage thresholds.
    /// </summary>
    public class MercilessProgressData
    {
        /// <summary>Whether the Merciless trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; }

        public MercilessProgressData()
        {
            IsUnlocked = false;
        }

        public MercilessProgressData Clone()
        {
            return new MercilessProgressData
            {
                IsUnlocked = this.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Claustrophobic removal progression (Hunter class).
    /// Removes the Claustrophobic negative trait after reaching mining threshold.
    /// </summary>
    public class ClaustrophobicRemovalProgressData
    {
        /// <summary>Whether the Claustrophobic trait has been removed.</summary>
        public bool IsRemoved { get; set; }

        public ClaustrophobicRemovalProgressData()
        {
            IsRemoved = false;
        }

        public ClaustrophobicRemovalProgressData Clone()
        {
            return new ClaustrophobicRemovalProgressData
            {
                IsRemoved = this.IsRemoved
            };
        }
    }

    /// <summary>
    /// Cached vanilla trait data for a player.
    /// Populated once on player join to avoid repeated GetStringArray calls.
    /// </summary>
    public class CachedVanillaTraits
    {
        public bool HasHardy { get; set; }
        public bool HasSoldier { get; set; }
        public bool HasFocused { get; set; }
        public bool HasFleetfooted { get; set; }
        public bool HasRavenous { get; set; }
        public bool HasFarsighted { get; set; }
        public bool HasNervous { get; set; }
        public bool HasNearsighted { get; set; }
        public bool HasFrail { get; set; }
        public bool HasCivil { get; set; }
        public bool HasWeak { get; set; }
        public bool HasKind { get; set; }
        public bool HasHeavyhanded { get; set; }
        public bool HasClaustrophobic { get; set; }
        public bool HasFurtive { get; set; }
        public bool HasPrecise { get; set; }
        public bool HasMender { get; set; }
        public bool HasPilferer { get; set; }
        public bool HasResourceful { get; set; }
        public bool HasForager { get; set; }

        // Combat Overhaul negative traits
        public bool HasCOTremblingAim { get; set; }
        public bool HasCOClumsyHands { get; set; }
        public bool HasCOFearOfMelee { get; set; }
        public bool HasCOWeakHand { get; set; }
        public bool HasCONervous { get; set; }

        // Combat Overhaul mixed/positive traits (Big Head, Thick Skull, Leg Day, Melee Expert, Self Defence)
        public bool HasCOBigHead { get; set; }
        public bool HasCOThickSkull { get; set; }
        public bool HasCOLegDay { get; set; }
        public bool HasCOMeleeExpert { get; set; }
        public bool HasCOSelfDefence { get; set; }
    }

    /// <summary>
    /// Last known horizontal position of a player for the walking and sneaking
    /// ticks. A class, updated in place: a struct stored in a ConcurrentDictionary
    /// allocates a new dictionary node on every write.
    /// </summary>
    public sealed class Position2D
    {
        public double X;
        public double Z;

        public Position2D(double x, double z)
        {
            X = x;
            Z = z;
        }
    }
}
