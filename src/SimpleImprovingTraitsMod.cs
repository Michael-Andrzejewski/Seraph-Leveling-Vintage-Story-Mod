using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SimpleImprovingTraits
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

        public RangedProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, RangedWeaponProgressData>();
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
                    CurrentIncrementSize = SimpleImprovingTraitsModSystem.BaseRangedDamagePerIncrement
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

        public WalkingProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 1000; // Base increment size
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
                CurrentIncrementSize = this.CurrentIncrementSize
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

        public HungerProgressData()
        {
            TotalCredits = 0;
            SecondsInIncrement = 0;
            CurrentIncrementSize = 300; // Base increment size (5 minutes)
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
                CurrentIncrementSize = this.CurrentIncrementSize
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

        /// <summary>Seconds needed for next time credit with this armor piece (86400, 172800, etc.).</summary>
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
            CurrentTimeIncrementSize = 86400; // 1 day in seconds
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

        /// <summary>Per-armor piece progress tracking. Key is armor code (e.g., "game:armor-body-plate-iron").</summary>
        public Dictionary<string, ArmorPieceProgressData> ArmorProgress { get; set; }

        public ArmorProgressData()
        {
            TotalDurabilityCredits = 0;
            TotalWalkSpeedCredits = 0;
            ArmorProgress = new Dictionary<string, ArmorPieceProgressData>();
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
                    CurrentTimeIncrementSize = SimpleImprovingTraitsModSystem.BaseSecondsInArmorPerIncrement,
                    CurrentDamageIncrementSize = SimpleImprovingTraitsModSystem.BaseDamageBlockedPerIncrement,
                    CurrentRepairIncrementSize = SimpleImprovingTraitsModSystem.BaseRepairsPerIncrement
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

        public MeleeProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, WeaponProgressData>();
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
                    CurrentIncrementSize = SimpleImprovingTraitsModSystem.BaseDamagePerIncrement
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

        public MiningProgressData()
        {
            TotalCredits = 0;
            PickaxeProgress = new Dictionary<string, PickaxeProgressData>();
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
                    CurrentIncrementSize = SimpleImprovingTraitsModSystem.BaseBlocksPerIncrement
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

        public MenderProgressData()
        {
            TotalCredits = 0;
            RepairsInIncrement = 0;
            CurrentIncrementSize = 5; // Base increment size
        }

        public MenderProgressData Clone()
        {
            return new MenderProgressData
            {
                TotalCredits = this.TotalCredits,
                RepairsInIncrement = this.RepairsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
            };
        }
    }

    /// <summary>
    /// Data structure for tracking Pilferer progression.
    /// Tracks collapsed chests opened and vessels broken for loot bonuses.
    /// </summary>
    public class PilfererProgressData
    {
        /// <summary>Total credits earned (each credit = 1% bonus). Max 20.</summary>
        public int TotalCredits { get; set; }

        /// <summary>Points accumulated toward the next credit.</summary>
        public int PointsInIncrement { get; set; }

        /// <summary>Points needed for the next credit (10, 20, 30, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        /// <summary>Set of chest block positions that have been opened (for first-time tracking).</summary>
        public HashSet<string> OpenedChestPositions { get; set; }

        public PilfererProgressData()
        {
            TotalCredits = 0;
            PointsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
            OpenedChestPositions = new HashSet<string>();
        }

        public PilfererProgressData Clone()
        {
            return new PilfererProgressData
            {
                TotalCredits = this.TotalCredits,
                PointsInIncrement = this.PointsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize,
                OpenedChestPositions = new HashSet<string>(this.OpenedChestPositions)
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

        public ResourcefulProgressData()
        {
            TotalCredits = 0;
            AnimalsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
        }

        public ResourcefulProgressData Clone()
        {
            return new ResourcefulProgressData
            {
                TotalCredits = this.TotalCredits,
                AnimalsInIncrement = this.AnimalsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
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

        public ForagerProgressData()
        {
            TotalCredits = 0;
            CropsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
        }

        public ForagerProgressData Clone()
        {
            return new ForagerProgressData
            {
                TotalCredits = this.TotalCredits,
                CropsInIncrement = this.CropsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
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

        public FurtiveProgressData()
        {
            TotalCredits = 0;
            BlocksInIncrement = 0;
            CurrentIncrementSize = 100; // Base increment size
        }

        public FurtiveProgressData Clone()
        {
            return new FurtiveProgressData
            {
                TotalCredits = this.TotalCredits,
                BlocksInIncrement = this.BlocksInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
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

        public PreciseProgressData()
        {
            TotalCredits = 0;
            WeaponProgress = new Dictionary<string, PreciseWeaponProgressData>();
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
                    CurrentIncrementSize = SimpleImprovingTraitsModSystem.BasePreciseDamagePerIncrement
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
    /// Main mod system for Simple Improving Traits.
    /// Provides a progression system that improves player traits through gameplay.
    /// Currently implements mining speed progression based on blocks mined.
    /// </summary>
    public class SimpleImprovingTraitsModSystem : ModSystem
    {
        public static ICoreServerAPI ServerApi { get; private set; }

        // Keys for mining progression system
        public const string BLOCKS_MINED_KEY = "sitBlocksMined";
        public const string MINING_STAT_CODE = "sitMiningBonus";
        private const string MINING_PROGRESS_SAVE_KEY = "sitMiningProgress";

        // WatchedAttributes keys for client sync
        public const string WATCHED_MINING_LEVEL = "sitMiningLevel";
        public const string WATCHED_MINING_BONUS = "sitMiningBonusPercent";

        // Trait code for the mining mastery trait
        public const string MINING_TRAIT_CODE = "sitminingmastery";

        // Mining progression configuration
        // Base blocks for first 1%: 100 blocks
        // Each subsequent 1% requires +100 more blocks (100, 200, 300, etc.)
        // Switching pickaxe types resets the increment counter back to base
        public static int BaseBlocksPerIncrement = 100;  // Base points needed for first credit
        public static int IncrementStep = 100;           // How much more points each subsequent credit needs
        public static int MaxMiningSpeedPercent = 150;   // 150% max bonus
        public static int OreMultiplier = 5;             // Ore blocks count for 5x points

        // Keys for melee damage progression system
        public const string MELEE_DAMAGE_KEY = "sitMeleeDamage";
        public const string MELEE_STAT_CODE = "sitMeleeBonus";
        private const string MELEE_PROGRESS_SAVE_KEY = "sitMeleeProgress";

        // WatchedAttributes keys for client sync (melee)
        public const string WATCHED_MELEE_LEVEL = "sitMeleeLevel";
        public const string WATCHED_MELEE_BONUS = "sitMeleeBonusPercent";

        // Trait code for the melee mastery trait (Soldier)
        public const string MELEE_TRAIT_CODE = "sitmeleemastery";

        // Melee damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon types resets the increment counter back to base
        public static int BaseDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int MeleeIncrementStep = 100;       // How much more damage each subsequent credit needs
        public static int MaxMeleeDamagePercent = 150;    // 150% max bonus

        // Vanilla Soldier trait melee damage bonus (used for cap calculations)
        public const int VANILLA_SOLDIER_MELEE_BONUS = 30;

        // Storage for melee progress - keyed by player UID
        public static ConcurrentDictionary<string, MeleeProgressData> MeleeProgress = new ConcurrentDictionary<string, MeleeProgressData>();

        // Flag to indicate pending melee progress save
        private static volatile bool pendingMeleeProgressSave = false;

        // Keys for ranged damage progression system
        public const string RANGED_DAMAGE_KEY = "sitRangedDamage";
        public const string RANGED_DAMAGE_STAT_CODE = "sitRangedDamageBonus";
        public const string RANGED_ACCURACY_STAT_CODE = "sitRangedAccuracyBonus";
        public const string RANGED_DISTANCE_STAT_CODE = "sitRangedDistanceBonus";
        private const string RANGED_PROGRESS_SAVE_KEY = "sitRangedProgress";

        // WatchedAttributes keys for client sync (ranged)
        public const string WATCHED_RANGED_LEVEL = "sitRangedLevel";
        public const string WATCHED_RANGED_DAMAGE_BONUS = "sitRangedDamageBonusPercent";
        public const string WATCHED_RANGED_ACCURACY_BONUS = "sitRangedAccuracyBonusPercent";
        public const string WATCHED_RANGED_DISTANCE_BONUS = "sitRangedDistanceBonusPercent";

        // Trait code for the ranged mastery trait (Focused)
        public const string RANGED_TRAIT_CODE = "sitrangedmastery";

        // Ranged damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon combinations resets the increment counter back to base
        public static int BaseRangedDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int RangedIncrementStep = 100;             // How much more damage each subsequent credit needs
        public static int MaxRangedDamagePercent = 130;          // 130% max bonus for damage
        public static int MaxRangedAccuracyPercent = 50;         // 50% max bonus for accuracy
        public static int MaxRangedDistancePercent = 50;         // 50% max bonus for distance

        // Vanilla Focused trait bonuses (used for cap calculations)
        public const int VANILLA_FOCUSED_DAMAGE_BONUS = 20;
        public const int VANILLA_FOCUSED_ACCURACY_BONUS = 30;
        public const int VANILLA_FOCUSED_DISTANCE_BONUS = 20;

        // Storage for ranged progress - keyed by player UID
        public static ConcurrentDictionary<string, RangedProgressData> RangedProgress = new ConcurrentDictionary<string, RangedProgressData>();

        // Flag to indicate pending ranged progress save
        private static volatile bool pendingRangedProgressSave = false;

        // Keys for walking speed progression system
        public const string WALKING_STAT_CODE = "sitWalkingBonus";
        private const string WALKING_PROGRESS_SAVE_KEY = "sitWalkingProgress";

        // WatchedAttributes keys for client sync (walking)
        public const string WATCHED_WALKING_LEVEL = "sitWalkingLevel";
        public const string WATCHED_WALKING_BONUS = "sitWalkingBonusPercent";

        // Trait code for the walking speed mastery trait (Fleetfooted)
        public const string WALKING_TRAIT_CODE = "sitwalkingmastery";

        // Walking speed progression configuration
        // Base blocks for first 1%: 1000 blocks
        // Each subsequent 1% requires +1000 more blocks (1000, 2000, 3000, etc.)
        public static int BaseBlocksWalkedPerIncrement = 1000;  // Base blocks needed for first credit
        public static int WalkingIncrementStep = 1000;          // How much more blocks each subsequent credit needs
        public static int MaxWalkingSpeedPercent = 15;          // 15% max bonus (115% total speed)

        // Vanilla Fleetfooted trait walk speed bonus (used for cap calculations)
        public const int VANILLA_FLEETFOOTED_WALK_BONUS = 10;

        // Storage for walking progress - keyed by player UID
        public static ConcurrentDictionary<string, WalkingProgressData> WalkingProgress = new ConcurrentDictionary<string, WalkingProgressData>();

        // Flag to indicate pending walking progress save
        private static volatile bool pendingWalkingProgressSave = false;

        // Tracking last known positions for walking distance calculation
        private static ConcurrentDictionary<string, Vec3d> lastPlayerPositions = new ConcurrentDictionary<string, Vec3d>();

        // Maximum distance per tick to count (prevents teleportation from counting)
        private const float MAX_DISTANCE_PER_TICK = 10f;

        // Keys for hunger rate progression system
        public const string HUNGER_STAT_CODE = "sitHungerBonus";
        private const string HUNGER_PROGRESS_SAVE_KEY = "sitHungerProgress";

        // WatchedAttributes keys for client sync (hunger)
        public const string WATCHED_HUNGER_LEVEL = "sitHungerLevel";
        public const string WATCHED_HUNGER_BONUS = "sitHungerBonusPercent";

        // Trait code for the hunger mastery trait
        public const string HUNGER_TRAIT_CODE = "sithungermastery";

        // Hunger rate progression configuration
        // Base seconds at full saturation for first 1%: 300 seconds (5 minutes)
        // Each subsequent 1% requires +60 more seconds (5 min, 6 min, 7 min, etc.)
        public static int BaseSecondsPerIncrement = 300;   // Base seconds needed for first credit (5 minutes)
        public static int HungerIncrementStep = 60;        // How many more seconds each subsequent credit needs (1 minute)
        public static int MaxHungerReductionPercent = 25;  // 25% max hunger rate reduction (to 75% rate)

        // Vanilla Ravenous trait hunger rate increase (used for cap calculations)
        // Blackguard has +30% hunger rate, so earning 25% brings them back to nearly normal
        public const int VANILLA_RAVENOUS_HUNGER_PENALTY = 30;

        // Storage for hunger progress - keyed by player UID
        public static ConcurrentDictionary<string, HungerProgressData> HungerProgress = new ConcurrentDictionary<string, HungerProgressData>();

        // Flag to indicate pending hunger progress save
        private static volatile bool pendingHungerProgressSave = false;

        // Keys for armor progression system
        public const string ARMOR_DURABILITY_STAT_CODE = "sitArmorDurabilityBonus";
        public const string ARMOR_WALKSPEED_STAT_CODE = "sitArmorWalkSpeedBonus";
        private const string ARMOR_PROGRESS_SAVE_KEY = "sitArmorProgress";

        // WatchedAttributes keys for client sync (armor)
        public const string WATCHED_ARMOR_DURABILITY_LEVEL = "sitArmorDurabilityLevel";
        public const string WATCHED_ARMOR_DURABILITY_BONUS = "sitArmorDurabilityBonusPercent";
        public const string WATCHED_ARMOR_WALKSPEED_LEVEL = "sitArmorWalkSpeedLevel";
        public const string WATCHED_ARMOR_WALKSPEED_BONUS = "sitArmorWalkSpeedBonusPercent";

        // Trait code for the armor mastery trait (Soldier)
        public const string ARMOR_TRAIT_CODE = "sitarmormastery";

        // Armor progression configuration
        // Time-based progression: 1 day base, +1 day increment per credit (gives -1% walk speed penalty per credit)
        public static int BaseSecondsInArmorPerIncrement = 86400;  // Base seconds (1 day) for first credit
        public static int ArmorTimeIncrementStep = 86400;          // How many more seconds each subsequent credit needs (1 day)

        // Damage-based progression: 100 damage base, +100 increment per credit (gives +1% durability per credit)
        public static int BaseDamageBlockedPerIncrement = 100;     // Base damage blocked for first credit
        public static int ArmorDamageIncrementStep = 100;          // How much more damage each subsequent credit needs

        // Repair-based progression: 1 repair base, +1 increment per credit (gives +1% durability per credit)
        public static int BaseRepairsPerIncrement = 1;             // Base repairs for first credit
        public static int ArmorRepairIncrementStep = 1;            // How many more repairs each subsequent credit needs

        // First-equip bonuses (durability only):
        // +1% for light armor and chain, +2% for brigandine, +3% for scale and plate
        public const int FIRST_EQUIP_LIGHT_BONUS = 1;
        public const int FIRST_EQUIP_CHAIN_BONUS = 1;
        public const int FIRST_EQUIP_BRIGANDINE_BONUS = 2;
        public const int FIRST_EQUIP_SCALE_BONUS = 3;
        public const int FIRST_EQUIP_PLATE_BONUS = 3;

        // Max bonuses
        public static int MaxArmorDurabilityPercent = 150;         // 150% max armor durability bonus
        public static int MaxArmorWalkSpeedPercent = 50;           // 50% max walk speed penalty reduction

        // Vanilla Soldier trait armor bonuses (used for cap calculations)
        public const int VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS = 15;
        public const int VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS = 25;

        // Storage for armor progress - keyed by player UID
        public static ConcurrentDictionary<string, ArmorProgressData> ArmorProgress = new ConcurrentDictionary<string, ArmorProgressData>();

        // Flag to indicate pending armor progress save
        private static volatile bool pendingArmorProgressSave = false;

        // Tracking currently equipped armor for each player (for time tracking and equip detection)
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedArmor = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // CLOTHIER TRAIT - Tracks unique clothing worn to unlock sewing kit crafting
        // =========================================================================
        public const string CLOTHIER_STAT_CODE = "sitClothierBonus";
        private const string CLOTHIER_PROGRESS_SAVE_KEY = "sitClothierProgress";
        public const string WATCHED_CLOTHIER_COUNT = "sitClothierCount";
        public const string WATCHED_CLOTHIER_UNLOCKED = "sitClothierUnlocked";
        public const string CLOTHIER_TRAIT_CODE = "sitclothiermastery";

        // Clothier progression configuration
        public static int ClothierRequiredUniqueClothes = 20; // Number of unique clothes to unlock sewing kit

        // Vanilla Clothier trait (Tailor exclusive)
        public const int VANILLA_CLOTHIER_BONUS = 0; // No vanilla bonus, this is unlock-based

        // Storage for clothier progress
        public static ConcurrentDictionary<string, ClothierProgressData> ClothierProgress = new ConcurrentDictionary<string, ClothierProgressData>();
        private static volatile bool pendingClothierProgressSave = false;

        // Tracking currently equipped clothing for each player
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedClothing = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // MENDER TRAIT - Tracks sewing kit repairs for durability bonus
        // =========================================================================
        public const string MENDER_STAT_CODE = "sitMenderBonus";
        private const string MENDER_PROGRESS_SAVE_KEY = "sitMenderProgress";
        public const string WATCHED_MENDER_LEVEL = "sitMenderLevel";
        public const string WATCHED_MENDER_BONUS = "sitMenderBonusPercent";
        public const string MENDER_TRAIT_CODE = "sitmendermastery";

        // Mender progression configuration
        public static int BaseMenderRepairsPerIncrement = 5;   // Base repairs for first credit
        public static int MenderIncrementStep = 1;              // Increment step per credit
        public static int MaxMenderPercent = 20;                // 20% max armor/clothing durability bonus

        // Vanilla Mender trait bonus (used for cap calculations)
        public const int VANILLA_MENDER_ARMOR_DURABILITY_BONUS = 10;

        // Storage for mender progress
        public static ConcurrentDictionary<string, MenderProgressData> MenderProgress = new ConcurrentDictionary<string, MenderProgressData>();
        private static volatile bool pendingMenderProgressSave = false;

        // Durability tracking for repair detection - key is "playerUid_slotId", value is last known durability
        private static ConcurrentDictionary<string, int> TrackedItemDurabilities = new ConcurrentDictionary<string, int>();

        // Sewing kit consumption tracking - key is playerUid, value is last known sewing kit count on mouse cursor
        private static ConcurrentDictionary<string, int> TrackedSewingKitCounts = new ConcurrentDictionary<string, int>();

        // =========================================================================
        // PILFERER TRAIT - Tracks chests/vessels for loot bonuses
        // =========================================================================
        public const string PILFERER_RUSTY_GEAR_STAT_CODE = "sitPilfererRustyGear";
        public const string PILFERER_VESSEL_CONTENTS_STAT_CODE = "sitPilfererVesselContents";
        public const string PILFERER_WHOLE_VESSEL_STAT_CODE = "sitPilfererWholeVessel";
        private const string PILFERER_PROGRESS_SAVE_KEY = "sitPilfererProgress";
        public const string WATCHED_PILFERER_LEVEL = "sitPilfererLevel";
        public const string WATCHED_PILFERER_BONUS = "sitPilfererBonusPercent";
        public const string PILFERER_TRAIT_CODE = "sitpilferermastery";

        // Pilferer progression configuration
        public static int BasePilfererPointsPerIncrement = 10;  // Base points for first credit
        public static int PilfererIncrementStep = 10;           // Increment step per credit
        public static int MaxPilfererPercent = 20;              // 20% max bonus for all three stats
        public const int PILFERER_CHEST_POINTS = 1;             // Points per first-time chest opening
        public const int PILFERER_VESSEL_POINTS = 2;            // Points per broken vessel

        // Vanilla Pilferer trait bonuses (Malefactor exclusive)
        public const int VANILLA_PILFERER_RUSTY_GEAR_BONUS = 10;
        public const int VANILLA_PILFERER_VESSEL_CONTENTS_BONUS = 15;
        public const int VANILLA_PILFERER_WHOLE_VESSEL_BONUS = 12;

        // Storage for pilferer progress
        public static ConcurrentDictionary<string, PilfererProgressData> PilfererProgress = new ConcurrentDictionary<string, PilfererProgressData>();
        private static volatile bool pendingPilfererProgressSave = false;

        // =========================================================================
        // RESOURCEFUL TRAIT - Tracks animal harvesting for loot/speed bonuses
        // =========================================================================
        public const string RESOURCEFUL_LOOT_STAT_CODE = "sitResourcefulLoot";
        public const string RESOURCEFUL_SPEED_STAT_CODE = "sitResourcefulSpeed";
        private const string RESOURCEFUL_PROGRESS_SAVE_KEY = "sitResourcefulProgress";
        public const string WATCHED_RESOURCEFUL_LEVEL = "sitResourcefulLevel";
        public const string WATCHED_RESOURCEFUL_LOOT_BONUS = "sitResourcefulLootBonusPercent";
        public const string WATCHED_RESOURCEFUL_SPEED_BONUS = "sitResourcefulSpeedBonusPercent";
        public const string RESOURCEFUL_TRAIT_CODE = "sitresourcefulmastery";

        // Resourceful progression configuration
        public static int BaseResourcefulAnimalsPerIncrement = 10;  // Base animals for first credit
        public static int ResourcefulIncrementStep = 10;            // Increment step per credit
        public static int MaxResourcefulLootPercent = 20;           // 20% max animal loot bonus
        public static int MaxResourcefulSpeedPercent = 25;          // 25% max harvesting speed bonus

        // Vanilla Resourceful trait bonuses (Hunter/Malefactor)
        public const int VANILLA_RESOURCEFUL_LOOT_BONUS = 10;
        public const int VANILLA_RESOURCEFUL_SPEED_BONUS = 25;

        // Storage for resourceful progress
        public static ConcurrentDictionary<string, ResourcefulProgressData> ResourcefulProgress = new ConcurrentDictionary<string, ResourcefulProgressData>();
        private static volatile bool pendingResourcefulProgressSave = false;

        // =========================================================================
        // FORAGER TRAIT - Tracks wild crop breaking for foraging loot bonuses
        // =========================================================================
        public const string FORAGER_LOOT_STAT_CODE = "sitForagerLoot";
        public const string FORAGER_WILD_CROP_STAT_CODE = "sitForagerWildCrop";
        private const string FORAGER_PROGRESS_SAVE_KEY = "sitForagerProgress";
        public const string WATCHED_FORAGER_LEVEL = "sitForagerLevel";
        public const string WATCHED_FORAGER_LOOT_BONUS = "sitForagerLootBonusPercent";
        public const string WATCHED_FORAGER_WILD_CROP_BONUS = "sitForagerWildCropBonusPercent";
        public const string FORAGER_TRAIT_CODE = "sitforagermastery";

        // Forager progression configuration
        public static int BaseForagerCropsPerIncrement = 10;    // Base crops for first credit
        public static int ForagerIncrementStep = 10;            // Increment step per credit
        public static int MaxForagerLootPercent = 20;           // 20% max foraging loot bonus
        public static int MaxForagerWildCropPercent = 20;       // 20% max wild crop drop bonus

        // Vanilla Forager trait bonuses (Hunter/Malefactor)
        public const int VANILLA_FORAGER_LOOT_BONUS = 10;
        public const int VANILLA_FORAGER_WILD_CROP_BONUS = 20;

        // Storage for forager progress
        public static ConcurrentDictionary<string, ForagerProgressData> ForagerProgress = new ConcurrentDictionary<string, ForagerProgressData>();
        private static volatile bool pendingForagerProgressSave = false;

        // =========================================================================
        // FURTIVE TRAIT - Tracks sneaking blocks for animal detection range reduction
        // =========================================================================
        public const string FURTIVE_STAT_CODE = "sitFurtiveBonus";
        private const string FURTIVE_PROGRESS_SAVE_KEY = "sitFurtiveProgress";
        public const string WATCHED_FURTIVE_LEVEL = "sitFurtiveLevel";
        public const string WATCHED_FURTIVE_BONUS = "sitFurtiveBonusPercent";
        public const string FURTIVE_TRAIT_CODE = "sitfurtivemastery";

        // Furtive progression configuration
        public static int BaseFurtiveSneakBlocksPerIncrement = 100;  // Base sneaking blocks for first credit
        public static int FurtiveIncrementStep = 100;                 // Increment step per credit
        public static int MaxFurtivePercent = 35;                     // 35% max animal detection range reduction

        // Vanilla Furtive trait bonus (Malefactor)
        public const int VANILLA_FURTIVE_DETECTION_REDUCTION = 35;

        // Storage for furtive progress
        public static ConcurrentDictionary<string, FurtiveProgressData> FurtiveProgress = new ConcurrentDictionary<string, FurtiveProgressData>();
        private static volatile bool pendingFurtiveProgressSave = false;

        // Tracking last known positions for sneaking distance calculation
        private static ConcurrentDictionary<string, Vec3d> lastSneakingPositions = new ConcurrentDictionary<string, Vec3d>();

        // =========================================================================
        // PRECISE TRAIT - Tracks damage to mechanicals for damage bonus
        // =========================================================================
        public const string PRECISE_STAT_CODE = "sitPreciseBonus";
        private const string PRECISE_PROGRESS_SAVE_KEY = "sitPreciseProgress";
        public const string WATCHED_PRECISE_LEVEL = "sitPreciseLevel";
        public const string WATCHED_PRECISE_BONUS = "sitPreciseBonusPercent";
        public const string PRECISE_TRAIT_CODE = "sitprecisemastery";

        // Precise progression configuration
        public static int BasePreciseDamagePerIncrement = 100;  // Base damage for first credit
        public static int PreciseIncrementStep = 100;            // Increment step per credit
        public static int MaxPrecisePercent = 30;                // 30% max damage bonus to mechanicals

        // Vanilla Precise trait bonus (Clockmaker)
        public const int VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS = 25;

        // Storage for precise progress
        public static ConcurrentDictionary<string, PreciseProgressData> PreciseProgress = new ConcurrentDictionary<string, PreciseProgressData>();
        private static volatile bool pendingPreciseProgressSave = false;

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

        // =========================================================================
        // HARDY HEALTH TRAIT - Unlocks +5 HP after reaching mining and armor thresholds
        // =========================================================================
        public const string HARDY_HEALTH_STAT_CODE = "sitHardyHealthBonus";
        private const string HARDY_HEALTH_PROGRESS_SAVE_KEY = "sitHardyHealthProgress";
        public const string WATCHED_HARDY_HEALTH_UNLOCKED = "sitHardyHealthUnlocked";
        public const string HARDY_HEALTH_TRAIT_CODE = "sithardyhealthmastery";

        // Hardy health unlock thresholds
        public static int HardyHealthMiningThreshold = 110;          // 110% mining speed bonus required
        public static int HardyHealthArmorDurabilityThreshold = 10;  // 10% armor durability bonus required
        public static int HardyHealthBonus = 5;                      // +5 HP bonus

        // Storage for hardy health progress
        public static ConcurrentDictionary<string, HardyHealthProgressData> HardyHealthProgress = new ConcurrentDictionary<string, HardyHealthProgressData>();
        private static volatile bool pendingHardyHealthProgressSave = false;

        // =========================================================================
        // BOWYER TRAIT - Unlocks crude bow/arrows after ranged damage + bow damage
        // =========================================================================
        public const string BOWYER_STAT_CODE = "sitBowyerBonus";
        private const string BOWYER_PROGRESS_SAVE_KEY = "sitBowyerProgress";
        public const string WATCHED_BOWYER_UNLOCKED = "sitBowyerUnlocked";
        public const string WATCHED_BOWYER_BOW_DAMAGE = "sitBowyerBowDamage";
        public const string BOWYER_TRAIT_CODE = "sitbowyermastery";

        // Bowyer unlock thresholds
        public static int BowyerRangedDamageThreshold = 10;          // 10% ranged damage bonus required
        public static int BowyerBowDamageThreshold = 300;            // 300 total bow damage required

        // Storage for bowyer progress
        public static ConcurrentDictionary<string, BowyerProgressData> BowyerProgress = new ConcurrentDictionary<string, BowyerProgressData>();
        private static volatile bool pendingBowyerProgressSave = false;

        // =========================================================================
        // IMPROVISER TRAIT - Unlocks sling after thrown rock damage
        // =========================================================================
        public const string IMPROVISER_STAT_CODE = "sitImproviserBonus";
        private const string IMPROVISER_PROGRESS_SAVE_KEY = "sitImproviserProgress";
        public const string WATCHED_IMPROVISER_UNLOCKED = "sitImproviserUnlocked";
        public const string WATCHED_IMPROVISER_ROCK_DAMAGE = "sitImproviserRockDamage";
        public const string IMPROVISER_TRAIT_CODE = "sitimprovisermastery";

        // Improviser unlock threshold
        public static int ImproviserRockDamageThreshold = 300;       // 300 total thrown rock damage required

        // Storage for improviser progress
        public static ConcurrentDictionary<string, ImproviserProgressData> ImproviserProgress = new ConcurrentDictionary<string, ImproviserProgressData>();
        private static volatile bool pendingImproviserProgressSave = false;

        // =========================================================================
        // TINKERER TRAIT - Unlocks tuning spear after Technical + Precise threshold
        // =========================================================================
        public const string TINKERER_STAT_CODE = "sitTinkererBonus";
        private const string TINKERER_PROGRESS_SAVE_KEY = "sitTinkererProgress";
        public const string WATCHED_TINKERER_UNLOCKED = "sitTinkererUnlocked";
        public const string TINKERER_TRAIT_CODE = "sittinkerermastery";

        // Tinkerer unlock threshold
        public static int TinkererPreciseThreshold = 10;              // 10% Precise damage bonus required (plus Technical)

        // Storage for tinkerer progress
        public static ConcurrentDictionary<string, TinkererProgressData> TinkererProgress = new ConcurrentDictionary<string, TinkererProgressData>();
        private static volatile bool pendingTinkererProgressSave = false;

        // =========================================================================
        // MERCILESS TRAIT - Unlocks shortsword/shield after armor + melee thresholds
        // =========================================================================
        public const string MERCILESS_STAT_CODE = "sitMercilessBonus";
        private const string MERCILESS_PROGRESS_SAVE_KEY = "sitMercilessProgress";
        public const string WATCHED_MERCILESS_UNLOCKED = "sitMercilessUnlocked";
        public const string MERCILESS_TRAIT_CODE = "sitmercilessmastery";

        // Merciless unlock thresholds
        public static int MercilessArmorDurabilityThreshold = 10;    // 10% armor durability bonus required
        public static int MercilessMeleeDamageThreshold = 15;        // 15% melee damage bonus required

        // Storage for merciless progress
        public static ConcurrentDictionary<string, MercilessProgressData> MercilessProgress = new ConcurrentDictionary<string, MercilessProgressData>();
        private static volatile bool pendingMercilessProgressSave = false;

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL - Removes trait after reaching mining threshold (Hunter)
        // =========================================================================
        private const string CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY = "sitClaustrophobicRemovalProgress";
        public const string WATCHED_CLAUSTROPHOBIC_REMOVED = "sitClaustrophobicRemoved";
        public const string CLAUSTROPHOBIC_REMOVED_TRAIT_CODE = "sitclaustrophobicremoved";

        // Claustrophobic removal threshold
        public static int ClaustrophobicRemovalMiningThreshold = 100;  // 100% mining speed bonus required

        // Storage for claustrophobic removal progress
        public static ConcurrentDictionary<string, ClaustrophobicRemovalProgressData> ClaustrophobicRemovalProgress = new ConcurrentDictionary<string, ClaustrophobicRemovalProgressData>();
        private static volatile bool pendingClaustrophobicRemovalProgressSave = false;

        private const string CONFIG_SAVE_KEY = "sitConfig";

        // Vanilla Hardy trait mining speed bonus (used for cap calculations)
        public const int VANILLA_HARDY_MINING_BONUS = 10;

        // Storage for mining progress - keyed by player UID
        public static ConcurrentDictionary<string, MiningProgressData> MiningProgress = new ConcurrentDictionary<string, MiningProgressData>();

        // Lock object for persistence operations
        private static readonly object persistLock = new object();

        // Flag to indicate pending mining progress save
        private static volatile bool pendingMiningProgressSave = false;

        // Flag to indicate pending config save
        private static volatile bool pendingConfigSave = false;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;

            // Register /trait command with subcommands
            api.ChatCommands.Create("trait")
                .WithDescription("Manage and view trait progression")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitHelpCommand)
                .BeginSubCommand("mining")
                    .WithDescription("View your mining progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningCommand)
                .EndSubCommand()
                .BeginSubCommand("miningbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("mininglevel")
                    .WithDescription("Set your mining level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("miningmax")
                    .WithDescription("Get or set the max mining speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("miningincrement")
                    .WithDescription("Get or set the increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("melee")
                    .WithDescription("View your melee damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeCommand)
                .EndSubCommand()
                .BeginSubCommand("meleebase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("meleelevel")
                    .WithDescription("Set your melee level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("meleemax")
                    .WithDescription("Get or set the max melee damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("meleeincrement")
                    .WithDescription("Get or set the melee increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("ranged")
                    .WithDescription("View your ranged damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedbase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedlevel")
                    .WithDescription("Set your ranged level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmax")
                    .WithDescription("Get or set the max ranged damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxacc")
                    .WithDescription("Get or set the max ranged accuracy bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxAccuracyCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxdist")
                    .WithDescription("Get or set the max ranged distance bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxDistanceCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedincrement")
                    .WithDescription("Get or set the ranged increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("walking")
                    .WithDescription("View your walking speed progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitWalkingCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("walkinglevel")
                    .WithDescription("Set your walking level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitWalkingLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingmax")
                    .WithDescription("Get or set the max walking speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingincrement")
                    .WithDescription("Get or set the walking increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("hunger")
                    .WithDescription("View your hunger rate progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerbase")
                    .WithDescription("Get or set the base seconds per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerlevel")
                    .WithDescription("Set your hunger level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("hungermax")
                    .WithDescription("Get or set the max hunger rate reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerincrement")
                    .WithDescription("Get or set the hunger increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("armor")
                    .WithDescription("View your armor progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorCommand)
                .EndSubCommand()
                .BeginSubCommand("armorlevel")
                    .WithDescription("Set your armor durability level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedlevel")
                    .WithDescription("Set your armor walk speed penalty reduction level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorWalkSpeedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armordurabilitymax")
                    .WithDescription("Get or set the max armor durability bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDurabilityMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedmax")
                    .WithDescription("Get or set the max walk speed penalty reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorWalkSpeedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armortimebase")
                    .WithDescription("Get or set the base seconds in armor per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorTimeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armordamagebase")
                    .WithDescription("Get or set the base damage blocked per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDamageBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armorrepairbase")
                    .WithDescription("Get or set the base repairs per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorRepairBaseCommand)
                .EndSubCommand()
                // Clothier trait commands
                .BeginSubCommand("clothier")
                    .WithDescription("View your clothier progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierrequired")
                    .WithDescription("Get or set the required unique clothes to unlock sewing kit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitClothierRequiredCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierlevel")
                    .WithDescription("Set your clothier progress (unique clothes count) (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierLevelCommand)
                .EndSubCommand()
                // Mender trait commands
                .BeginSubCommand("mender")
                    .WithDescription("View your mender progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderCommand)
                .EndSubCommand()
                .BeginSubCommand("menderbase")
                    .WithDescription("Get or set the base repairs per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("menderlevel")
                    .WithDescription("Set your mender level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("mendermax")
                    .WithDescription("Get or set the max mender bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderMaxCommand)
                .EndSubCommand()
                // Pilferer trait commands
                .BeginSubCommand("pilferer")
                    .WithDescription("View your pilferer progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererbase")
                    .WithDescription("Get or set the base points per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("points"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererlevel")
                    .WithDescription("Set your pilferer level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("pilferermax")
                    .WithDescription("Get or set the max pilferer bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererMaxCommand)
                .EndSubCommand()
                // Resourceful trait commands
                .BeginSubCommand("resourceful")
                    .WithDescription("View your resourceful progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulbase")
                    .WithDescription("Get or set the base animals per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("animals"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefullevel")
                    .WithDescription("Set your resourceful level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulmax")
                    .WithDescription("Get or set the max resourceful loot bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulMaxCommand)
                .EndSubCommand()
                // Forager trait commands
                .BeginSubCommand("forager")
                    .WithDescription("View your forager progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerbase")
                    .WithDescription("Get or set the base crops per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("crops"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerlevel")
                    .WithDescription("Set your forager level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("foragermax")
                    .WithDescription("Get or set the max forager bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerMaxCommand)
                .EndSubCommand()
                // Furtive trait commands
                .BeginSubCommand("furtive")
                    .WithDescription("View your furtive (sneaking) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveCommand)
                .EndSubCommand()
                .BeginSubCommand("furtivelevel")
                    .WithDescription("Set your furtive level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveLevelCommand)
                .EndSubCommand()
                // Precise trait commands
                .BeginSubCommand("precise")
                    .WithDescription("View your precise (mechanical damage) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseCommand)
                .EndSubCommand()
                .BeginSubCommand("preciselevel")
                    .WithDescription("Set your precise level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Int("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseLevelCommand)
                .EndSubCommand()
                // Technical trait commands
                .BeginSubCommand("technical")
                    .WithDescription("View your technical trait progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalCommand)
                .EndSubCommand()
                .BeginSubCommand("technicalunlock")
                    .WithDescription("Manually unlock/lock technical trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalUnlockCommand)
                .EndSubCommand()
                // Hardy health trait commands
                .BeginSubCommand("hardyhealth")
                    .WithDescription("View your hardy health unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthCommand)
                .EndSubCommand()
                .BeginSubCommand("hardyhealthunlock")
                    .WithDescription("Manually unlock/lock hardy health trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthUnlockCommand)
                .EndSubCommand()
                // Bowyer trait commands
                .BeginSubCommand("bowyer")
                    .WithDescription("View your bowyer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerCommand)
                .EndSubCommand()
                .BeginSubCommand("bowyerunlock")
                    .WithDescription("Manually unlock/lock bowyer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerUnlockCommand)
                .EndSubCommand()
                // Improviser trait commands
                .BeginSubCommand("improviser")
                    .WithDescription("View your improviser unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserCommand)
                .EndSubCommand()
                .BeginSubCommand("improviserunlock")
                    .WithDescription("Manually unlock/lock improviser trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserUnlockCommand)
                .EndSubCommand()
                // Tinkerer trait commands
                .BeginSubCommand("tinkerer")
                    .WithDescription("View your tinkerer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererCommand)
                .EndSubCommand()
                .BeginSubCommand("tinkererunlock")
                    .WithDescription("Manually unlock/lock tinkerer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererUnlockCommand)
                .EndSubCommand()
                // Merciless trait commands
                .BeginSubCommand("merciless")
                    .WithDescription("View your merciless unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessCommand)
                .EndSubCommand()
                .BeginSubCommand("mercilessunlock")
                    .WithDescription("Manually unlock/lock merciless trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessUnlockCommand)
                .EndSubCommand()
                // Claustrophobic removal commands
                .BeginSubCommand("claustrophobic")
                    .WithDescription("View your claustrophobic removal progress (Hunter only)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicCommand)
                .EndSubCommand()
                .BeginSubCommand("claustrophobicunlock")
                    .WithDescription("Manually set claustrophobic removed status (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("removed"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicUnlockCommand)
                .EndSubCommand();

            // Hook into block breaking for mining progression
            api.Event.DidBreakBlock += OnBlockBroken;

            // Apply Harmony patches for melee damage tracking
            ApplyServerHarmonyPatches(api);

            // Hook into player join to apply saved bonuses
            api.Event.PlayerJoin += OnPlayerJoin;

            // Hook into world save event to persist progress
            api.Event.GameWorldSave += OnGameWorldSave;

            // Load config and progress data after save game is loaded
            api.Event.SaveGameLoaded += LoadConfig;
            api.Event.SaveGameLoaded += LoadMiningProgress;
            api.Event.SaveGameLoaded += LoadMeleeProgress;
            api.Event.SaveGameLoaded += LoadRangedProgress;
            api.Event.SaveGameLoaded += LoadWalkingProgress;
            api.Event.SaveGameLoaded += LoadHungerProgress;
            api.Event.SaveGameLoaded += LoadArmorProgress;
            api.Event.SaveGameLoaded += LoadClothierProgress;
            api.Event.SaveGameLoaded += LoadMenderProgress;
            api.Event.SaveGameLoaded += LoadPilfererProgress;
            api.Event.SaveGameLoaded += LoadResourcefulProgress;
            api.Event.SaveGameLoaded += LoadForagerProgress;
            api.Event.SaveGameLoaded += LoadFurtiveProgress;
            api.Event.SaveGameLoaded += LoadPreciseProgress;
            api.Event.SaveGameLoaded += LoadTechnicalProgress;
            api.Event.SaveGameLoaded += LoadHardyHealthProgress;
            api.Event.SaveGameLoaded += LoadBowyerProgress;
            api.Event.SaveGameLoaded += LoadImproviserProgress;
            api.Event.SaveGameLoaded += LoadTinkererProgress;
            api.Event.SaveGameLoaded += LoadMercilessProgress;
            api.Event.SaveGameLoaded += LoadClaustrophobicRemovalProgress;

            // Register game tick listener for walking distance tracking (every 500ms)
            api.Event.RegisterGameTickListener(OnWalkingTick, 500);

            // Register game tick listener for hunger tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnHungerTick, 1000);

            // Register game tick listener for armor time tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnArmorTick, 1000);

            // Register game tick listener for clothing tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnClothingTick, 1000);

            // Register game tick listener for Mender repair tracking (every 500ms for responsive detection)
            api.Event.RegisterGameTickListener(OnMenderRepairTick, 500);

            // Register game tick listener for sneaking distance tracking (every 500ms for Furtive)
            api.Event.RegisterGameTickListener(OnSneakingTick, 500);

            // Hook into player disconnect to clean up position tracking
            api.Event.PlayerDisconnect += OnPlayerDisconnect;

            api.Logger.Notification("[SimpleImprovingTraits] Mod loaded");
        }

        /// <summary>
        /// Handler for /trait command (shows help).
        /// </summary>
        private TextCommandResult OnTraitHelpCommand(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                "Usage:\n" +
                "  /trait mining - View your mining progression stats\n" +
                "  /trait miningbase [value] - Get or set base points for first credit (admin)\n" +
                "  /trait miningincrement [value] - Get or set increment step per credit (admin)\n" +
                "  /trait mininglevel [level] - Set your mining level (admin)\n" +
                "  /trait miningmax [percent] - Get or set max mining speed bonus (admin)\n" +
                "  /trait melee - View your melee damage progression stats\n" +
                "  /trait meleebase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait meleeincrement [value] - Get or set melee increment step per credit (admin)\n" +
                "  /trait meleelevel [level] - Set your melee level (admin)\n" +
                "  /trait meleemax [percent] - Get or set max melee damage bonus (admin)\n" +
                "  /trait ranged - View your ranged damage progression stats\n" +
                "  /trait rangedbase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait rangedincrement [value] - Get or set ranged increment step per credit (admin)\n" +
                "  /trait rangedlevel [level] - Set your ranged level (admin)\n" +
                "  /trait rangedmax [percent] - Get or set max ranged damage bonus (admin)\n" +
                "  /trait rangedmaxacc [percent] - Get or set max ranged accuracy bonus (admin)\n" +
                "  /trait rangedmaxdist [percent] - Get or set max ranged distance bonus (admin)\n" +
                "  /trait walking - View your walking speed progression stats\n" +
                "  /trait walkingbase [value] - Get or set base blocks for first credit (admin)\n" +
                "  /trait walkingincrement [value] - Get or set walking increment step per credit (admin)\n" +
                "  /trait walkinglevel [level] - Set your walking level (admin)\n" +
                "  /trait walkingmax [percent] - Get or set max walking speed bonus (admin)\n" +
                "  /trait hunger - View your hunger rate progression stats\n" +
                "  /trait hungerbase [value] - Get or set base seconds for first credit (admin)\n" +
                "  /trait hungerincrement [value] - Get or set hunger increment step per credit (admin)\n" +
                "  /trait hungerlevel [level] - Set your hunger level (admin)\n" +
                "  /trait hungermax [percent] - Get or set max hunger rate reduction (admin)\n" +
                "  /trait armor - View your armor progression stats\n" +
                "  /trait armorlevel [level] - Set your armor durability level (admin)\n" +
                "  /trait armorwalkspeedlevel [level] - Set walk speed penalty reduction level (admin)\n" +
                "  /trait armordurabilitymax [percent] - Get or set max durability bonus (admin)\n" +
                "  /trait armorwalkspeedmax [percent] - Get or set max walk speed reduction (admin)");
        }

        /// <summary>
        /// Handler for /trait mining command.
        /// </summary>
        private TextCommandResult OnTraitMiningCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMiningBonusPercent(currentCredits);

            var sb = new StringBuilder();
            sb.AppendLine($"Mining progression: {currentCredits}% / {MaxMiningSpeedPercent}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% mining speed");

            if (progress.PickaxeProgress.Count > 0)
            {
                sb.AppendLine("\nPer-pickaxe progress:");
                foreach (var kvp in progress.PickaxeProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string pickaxeName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (pickaxeName.StartsWith("game:"))
                        pickaxeName = pickaxeName.Substring(5);

                    var pickProgress = kvp.Value;
                    sb.AppendLine($"  {pickaxeName}: {pickProgress.BlocksInIncrement}/{pickProgress.CurrentIncrementSize} points");
                }
            }
            else
            {
                sb.AppendLine("\nNo pickaxe progress yet. Mine stone/ore with a pickaxe to start!");
            }

            if (currentCredits >= MaxMiningSpeedPercent)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait miningbase command.
        /// Sets the base points needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMiningBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksPerIncrement}. New pickaxes will require this many points for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksPerIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait miningincrement command.
        /// Sets how many additional points are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMiningIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                IncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Increment step set to +{IncrementStep} per credit.\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current increment step: +{IncrementStep} per credit\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait mininglevel command.
        /// Sets the player's mining credits (level) directly.
        /// Note: This resets all per-pickaxe progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMiningLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxMiningSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxMiningSpeedPercent})");
            }

            // Set the player's progress (clears per-pickaxe progress)
            string playerUid = player.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            progress.TotalCredits = newCredits;
            progress.PickaxeProgress.Clear(); // Reset all pickaxe progress

            pendingMiningProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyMiningBonus(player, newCredits);

            return TextCommandResult.Success($"Mining credits set to {newCredits} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
        }

        /// <summary>
        /// Gets the pickaxe code from the player's held item, or null if not holding a pickaxe.
        /// </summary>
        private string GetHeldPickaxeCode(IServerPlayer player)
        {
            if (player?.Entity == null) return null;

            var heldItem = player.Entity.RightHandItemSlot?.Itemstack?.Collectible;
            if (heldItem == null) return null;

            // Check if it's a pickaxe (Tool property = Pickaxe)
            if (heldItem.Tool != EnumTool.Pickaxe) return null;

            // Return the item code as the pickaxe identifier
            return heldItem.Code?.ToString();
        }

        /// <summary>
        /// Determines the point value for a broken block.
        /// Returns OreMultiplier (default 5) for ore blocks, 1 for stone blocks, 0 for other blocks.
        ///
        /// Stone block patterns (1 point each):
        /// - rock-{type} (e.g., rock-granite, rock-limestone)
        /// - crackedrock-{type} (e.g., crackedrock-granite)
        ///
        /// Ore block patterns (OreMultiplier points):
        /// - Contains "ore-" (e.g., ore-copper-granite, ore-lignite-chalk)
        /// </summary>
        private int GetBlockPoints(int blockId)
        {
            if (ServerApi == null) return 0;

            var block = ServerApi.World.GetBlock(blockId);
            if (block == null) return 0;

            string blockCode = block.Code?.ToString() ?? "";

            // Remove "game:" prefix if present for consistent matching
            string codeToCheck = blockCode.StartsWith("game:") ? blockCode.Substring(5) : blockCode;

            // Ore blocks: code contains "ore-" (e.g., "ore-lignite-chalk", "ore-copper-granite")
            if (codeToCheck.Contains("ore-"))
            {
                return OreMultiplier;
            }

            // Stone/rock blocks that should count for mining XP
            if (codeToCheck.StartsWith("rock-") ||           // Regular rock (rock-granite)
                codeToCheck.StartsWith("crackedrock-"))      // Cracked rock (crackedrock-granite)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Handler for /trait miningmax command.
        /// Gets or sets the maximum mining speed bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMiningMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max mining speed percent must be at least 1");
                }

                MaxMiningSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
                    ApplyMiningBonus(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max mining speed bonus set to +{MaxMiningSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max mining speed bonus: +{MaxMiningSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait melee command.
        /// </summary>
        private TextCommandResult OnTraitMeleeCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMeleeBonusPercent(currentCredits);

            var sb = new StringBuilder();
            sb.AppendLine($"Melee progression: {currentCredits}% / {MaxMeleeDamagePercent}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% melee damage");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (weaponName.StartsWith("game:"))
                        weaponName = weaponName.Substring(5);

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal damage with swords, falx, or spears to start!");
            }

            if (currentCredits >= MaxMeleeDamagePercent)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait meleebase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMeleeBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage per increment must be at least 1");
                }

                BaseDamagePerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base damage per increment set to {BaseDamagePerIncrement}. New weapons will require this much damage for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base damage per increment: {BaseDamagePerIncrement}\nIncrement step: +{MeleeIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait meleeincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMeleeIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                MeleeIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Melee increment step set to +{MeleeIncrementStep} per credit.\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current melee increment step: +{MeleeIncrementStep} per credit\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait meleelevel command.
        /// Sets the player's melee credits (level) directly.
        /// Note: This resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMeleeLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxMeleeDamagePercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxMeleeDamagePercent})");
            }

            // Set the player's progress (clears per-weapon progress)
            string playerUid = player.PlayerUID;
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            progress.TotalCredits = newCredits;
            progress.WeaponProgress.Clear(); // Reset all weapon progress

            pendingMeleeProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyMeleeBonusStatic(player, newCredits);

            // Check for trait unlocks that depend on melee level
            CheckMercilessUnlock(player);

            return TextCommandResult.Success($"Melee credits set to {newCredits} (+{bonusPercent}% melee damage). Per-weapon progress reset.");
        }

        /// <summary>
        /// Handler for /trait meleemax command.
        /// Gets or sets the maximum melee damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMeleeMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max melee damage percent must be at least 1");
                }

                MaxMeleeDamagePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
                    ApplyMeleeBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max melee damage bonus set to +{MaxMeleeDamagePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max melee damage bonus: +{MaxMeleeDamagePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait ranged command.
        /// </summary>
        private TextCommandResult OnTraitRangedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            int currentCredits = progress.TotalCredits;
            var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(currentCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Ranged progression: {currentCredits} credits / {MaxRangedDamagePercent} max");
            sb.AppendLine($"Current bonuses: +{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    weaponName = weaponName.Replace("game:", "");

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal ranged damage with bows or slings to start!");
            }

            if (currentCredits >= MaxRangedDamagePercent)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait rangedbase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitRangedBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage per increment must be at least 1");
                }

                BaseRangedDamagePerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base ranged damage per increment set to {BaseRangedDamagePerIncrement}. New weapons will require this much damage for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base ranged damage per increment: {BaseRangedDamagePerIncrement}\nIncrement step: +{RangedIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait rangedincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitRangedIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                RangedIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Ranged increment step set to +{RangedIncrementStep} per credit.\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current ranged increment step: +{RangedIncrementStep} per credit\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait rangedlevel command.
        /// Sets the player's ranged credits (level) directly.
        /// Note: This resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitRangedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxRangedDamagePercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxRangedDamagePercent})");
            }

            // Set the player's progress (clears per-weapon progress)
            string playerUid = player.PlayerUID;
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            progress.TotalCredits = newCredits;
            progress.WeaponProgress.Clear(); // Reset all weapon progress

            pendingRangedProgressSave = true;

            // Apply the bonus
            var (damageBonus, accuracyBonus, distanceBonus) = ApplyRangedBonusStatic(player, newCredits);

            return TextCommandResult.Success($"Ranged credits set to {newCredits} (+{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance). Per-weapon progress reset.");
        }

        /// <summary>
        /// Handler for /trait rangedmax command.
        /// Gets or sets the maximum ranged damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged damage percent must be at least 1");
                }

                MaxRangedDamagePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged damage bonus set to +{MaxRangedDamagePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged damage bonus: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxacc command.
        /// Gets or sets the maximum ranged accuracy bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxAccuracyCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged accuracy percent must be at least 1");
                }

                MaxRangedAccuracyPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged accuracy bonus set to +{MaxRangedAccuracyPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged accuracy bonus: +{MaxRangedAccuracyPercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxdist command.
        /// Gets or sets the maximum ranged distance bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxDistanceCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged distance percent must be at least 1");
                }

                MaxRangedDistancePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged distance bonus set to +{MaxRangedDistancePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged distance bonus: +{MaxRangedDistancePercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait walking command.
        /// </summary>
        private TextCommandResult OnTraitWalkingCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateWalkingBonusPercent(currentCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Walking progression: {currentCredits}% / {MaxWalkingSpeedPercent}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% walk speed");
            sb.AppendLine($"Progress: {progress.BlocksInIncrement:F1}/{progress.CurrentIncrementSize} blocks");

            if (currentCredits >= MaxWalkingSpeedPercent)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait walkingbase command.
        /// Sets the base blocks needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitWalkingBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksWalkedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksWalkedPerIncrement}. New progress will require this many blocks for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksWalkedPerIncrement}\nIncrement step: +{WalkingIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait walkingincrement command.
        /// Sets how many additional blocks are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitWalkingIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                WalkingIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Walking increment step set to +{WalkingIncrementStep} per credit.\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current walking increment step: +{WalkingIncrementStep} per credit\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait walkinglevel command.
        /// Sets the player's walking credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitWalkingLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxWalkingSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxWalkingSpeedPercent})");
            }

            // Set the player's progress
            string playerUid = player.PlayerUID;
            var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });

            progress.TotalCredits = newCredits;
            progress.BlocksInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseBlocksWalkedPerIncrement + (newCredits * WalkingIncrementStep);

            pendingWalkingProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyWalkingBonusStatic(player, newCredits);

            return TextCommandResult.Success($"Walking credits set to {newCredits} (+{bonusPercent}% walk speed).");
        }

        /// <summary>
        /// Handler for /trait walkingmax command.
        /// Gets or sets the maximum walking speed bonus percent.
        /// </summary>
        private TextCommandResult OnTraitWalkingMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max walking speed percent must be at least 1");
                }

                MaxWalkingSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
                    {
                        CurrentIncrementSize = BaseBlocksWalkedPerIncrement
                    });
                    ApplyWalkingBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max walking speed bonus set to +{MaxWalkingSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max walking speed bonus: +{MaxWalkingSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait hunger command.
        /// </summary>
        private TextCommandResult OnTraitHungerCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int currentCredits = progress.TotalCredits;
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity as EntityPlayer);
            int bonusPercent = CalculateHungerBonusPercent(currentCredits, player.Entity as EntityPlayer);
            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity as EntityPlayer);

            // Calculate target hunger rate (same for all classes)
            int targetHungerRate = 100 - MaxHungerReductionPercent;

            var sb = new StringBuilder();
            sb.AppendLine($"Hunger progression: {currentCredits} / {playerMaxCredits} credits");
            sb.AppendLine($"Current bonus: -{bonusPercent}% hunger rate");
            if (hasRavenous)
            {
                int currentRate = 130 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}% (Ravenous: 130% base)");
            }
            else
            {
                int currentRate = 100 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}%");
            }
            sb.AppendLine($"Target hunger rate: {targetHungerRate}%");
            sb.AppendLine($"\nProgress toward next credit:");
            sb.AppendLine($"  {progress.SecondsInIncrement:F0}/{progress.CurrentIncrementSize} seconds at full saturation");

            if (currentCredits >= playerMaxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait hungerbase command.
        /// Sets the base seconds needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitHungerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds per increment must be at least 1");
                }

                BaseSecondsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds per increment set to {BaseSecondsPerIncrement}. First 1% requires {BaseSecondsPerIncrement} seconds at full saturation.");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds per increment: {BaseSecondsPerIncrement}\nIncrement step: +{HungerIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait hungerincrement command.
        /// Sets how many additional seconds are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitHungerIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                HungerIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Hunger increment step set to +{HungerIncrementStep} per credit.\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current hunger increment step: +{HungerIncrementStep} per credit\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait hungerlevel command.
        /// Sets the player's hunger credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitHungerLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            // Calculate player-specific max credits
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);

            if (newCredits > playerMaxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max for this player ({playerMaxCredits})");
            }

            // Set the player's progress
            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            progress.TotalCredits = newCredits;
            progress.SecondsInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseSecondsPerIncrement + (newCredits * HungerIncrementStep);

            pendingHungerProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyHungerBonusStatic(player, newCredits);

            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity);
            int effectiveRate = hasRavenous ? (130 - bonusPercent) : (100 - bonusPercent);

            return TextCommandResult.Success($"Hunger credits set to {newCredits}/{playerMaxCredits} (-{bonusPercent}% hunger rate, effective rate: {effectiveRate}%).");
        }

        /// <summary>
        /// Handler for /trait hungermax command.
        /// Gets or sets the maximum hunger rate reduction percent (for non-Ravenous players).
        /// This determines the target hunger rate for all classes.
        /// </summary>
        private TextCommandResult OnTraitHungerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max hunger rate reduction percent must be at least 1");
                }

                MaxHungerReductionPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                    {
                        CurrentIncrementSize = BaseSecondsPerIncrement
                    });
                    ApplyHungerBonusStatic(player, progress.TotalCredits);
                }

                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate set to {targetRate}% (non-Ravenous: {MaxHungerReductionPercent} credits, Ravenous: {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits). All player bonuses recalculated.");
            }
            else
            {
                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate: {targetRate}%\nNon-Ravenous players need {MaxHungerReductionPercent} credits\nRavenous players need {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits");
            }
        }

        /// <summary>
        /// Handler for /trait armor command.
        /// </summary>
        private TextCommandResult OnTraitArmorCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int durabilityBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity as EntityPlayer);
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Armor progression:");
            sb.AppendLine($"  Durability: {progress.TotalDurabilityCredits} credits, +{durabilityBonus}% bonus (max {MaxArmorDurabilityPercent}%)");
            sb.AppendLine($"  Walk Speed Penalty Reduction: {progress.TotalWalkSpeedCredits} credits, -{walkSpeedBonus}% (max {MaxArmorWalkSpeedPercent}%)");

            if (progress.ArmorProgress.Count > 0)
            {
                sb.AppendLine("\nPer-armor progress:");
                foreach (var kvp in progress.ArmorProgress.OrderByDescending(p => p.Value.TimeCredits + p.Value.DamageCredits + p.Value.RepairCredits))
                {
                    string armorName = kvp.Key;
                    if (armorName.StartsWith("game:"))
                        armorName = armorName.Substring(5);

                    var armorProg = kvp.Value;
                    sb.AppendLine($"  {armorName}:");
                    sb.AppendLine($"    Time: {armorProg.TimeCredits} credits ({armorProg.SecondsWornInIncrement:F0}/{armorProg.CurrentTimeIncrementSize}s)");
                    sb.AppendLine($"    Damage: {armorProg.DamageCredits} credits ({armorProg.DamageBlockedInIncrement:F1}/{armorProg.CurrentDamageIncrementSize})");
                    sb.AppendLine($"    Repairs: {armorProg.RepairCredits} credits ({armorProg.RepairsInIncrement}/{armorProg.CurrentRepairIncrementSize})");
                }
            }
            else
            {
                sb.AppendLine("\nNo armor progress yet. Wear armor to start!");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait armorlevel command.
        /// Sets the player's armor durability credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxArmorDurabilityPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorDurabilityPercent})");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            progress.TotalDurabilityCredits = newCredits;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorDurabilityBonusPercent(newCredits, player.Entity);

            // Check for trait unlocks that depend on armor durability
            CheckHardyHealthUnlock(player);
            CheckMercilessUnlock(player);

            return TextCommandResult.Success($"Armor durability credits set to {newCredits} (+{bonusPercent}% durability).");
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedlevel command.
        /// Sets the player's armor walk speed penalty reduction credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int newCredits = (int)args[0];

            if (newCredits < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits > MaxArmorWalkSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorWalkSpeedPercent})");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            progress.TotalWalkSpeedCredits = newCredits;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorWalkSpeedBonusPercent(newCredits, player.Entity);

            return TextCommandResult.Success($"Armor walk speed penalty reduction credits set to {newCredits} (-{bonusPercent}% penalty).");
        }

        /// <summary>
        /// Handler for /trait armordurabilitymax command.
        /// </summary>
        private TextCommandResult OnTraitArmorDurabilityMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor durability percent must be at least 1");
                }

                MaxArmorDurabilityPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor durability bonus set to +{MaxArmorDurabilityPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor durability bonus: +{MaxArmorDurabilityPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedmax command.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor walk speed penalty reduction percent must be at least 1");
                }

                MaxArmorWalkSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor walk speed penalty reduction set to -{MaxArmorWalkSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor walk speed penalty reduction: -{MaxArmorWalkSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armortimebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorTimeBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds must be at least 1");
                }

                BaseSecondsInArmorPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds in armor per increment set to {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours).");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds in armor: {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours)\nIncrement step: +{ArmorTimeIncrementStep} ({ArmorTimeIncrementStep / 3600f:F1} hours)");
            }
        }

        /// <summary>
        /// Handler for /trait armordamagebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorDamageBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage must be at least 1");
                }

                BaseDamageBlockedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base damage blocked per increment set to {BaseDamageBlockedPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base damage blocked: {BaseDamageBlockedPerIncrement}\nIncrement step: +{ArmorDamageIncrementStep}");
            }
        }

        /// <summary>
        /// Handler for /trait armorrepairbase command.
        /// </summary>
        private TextCommandResult OnTraitArmorRepairBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base repairs must be at least 1");
                }

                BaseRepairsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base repairs per increment set to {BaseRepairsPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base repairs: {BaseRepairsPerIncrement}\nIncrement step: +{ArmorRepairIncrementStep}");
            }
        }

        /// <summary>
        /// Calculate the maximum hunger credits a player can earn.
        /// Ravenous players need more credits to reach the same target hunger rate.
        /// Target is (100 - MaxHungerReductionPercent)% = 75% by default.
        /// Non-Ravenous: 100% - 75% = 25 credits needed
        /// Ravenous: 130% - 75% = 55 credits needed
        /// </summary>
        public static int CalculateMaxHungerCredits(EntityPlayer entity)
        {
            bool hasRavenous = entity != null && PlayerHasVanillaRavenousStatic(entity);
            int ravenousPenalty = hasRavenous ? VANILLA_RAVENOUS_HUNGER_PENALTY : 0;
            // MaxHungerReductionPercent represents how much a normal player needs to reduce
            // Ravenous players need that PLUS their penalty to reach the same target
            return MaxHungerReductionPercent + ravenousPenalty;
        }

        /// <summary>
        /// Calculate the hunger rate reduction bonus as an integer percentage.
        /// This is the actual reduction applied (1% per credit, up to player's max).
        /// </summary>
        public static int CalculateHungerBonusPercent(int credits, EntityPlayer entity)
        {
            int maxCredits = CalculateMaxHungerCredits(entity);
            return Math.Min(credits, maxCredits);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Ravenous trait.
        /// </summary>
        private static bool PlayerHasVanillaRavenousStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("ravenous", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Ravenous (Blackguard)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Apply hunger rate reduction to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// All classes can reach the same target hunger rate (75% by default).
        /// Ravenous players start at 130% and need 55 credits to reach 75%.
        /// Non-Ravenous players start at 100% and need 25 credits to reach 75%.
        /// </summary>
        public static int ApplyHungerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Check if player has vanilla Ravenous
            bool hasVanillaRavenous = PlayerHasVanillaRavenousStatic(player.Entity);

            // Calculate max credits this player can earn
            int maxCredits = CalculateMaxHungerCredits(player.Entity);

            // Calculate bonus from level (1% per level, capped at player's max)
            int cappedLevel = Math.Min(level, maxCredits);
            float bonus = cappedLevel * 0.01f;

            // Set the hunger rate stat (hungerrate is multiplicative, so 0.75 = 75% hunger rate)
            // We want to REDUCE hunger rate, so we subtract the bonus from 1.0
            player.Entity.Stats.Set("hungerrate", HUNGER_STAT_CODE, 1f - bonus, false);

            int bonusPercent = (int)(bonus * 100);

            // Sync level and bonus to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_HUNGER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HUNGER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaRavenous", hasVanillaRavenous);
            player.Entity.WatchedAttributes.SetInt("sitMaxHungerCredits", maxCredits);

            // Add our trait to extraTraits (hunger mastery is unique, doesn't replace a vanilla trait)
            UpdateExtraTraitStatic(player.Entity, HUNGER_TRAIT_CODE, level > 0);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_HUNGER_LEVEL);

            return bonusPercent;
        }

        /// <summary>
        /// Calculate the walking speed bonus as an integer percentage.
        /// Accounts for vanilla Fleetfooted trait (+10% walk speed).
        /// </summary>
        public static int CalculateWalkingBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasFleetfooted = entity != null && PlayerHasVanillaFleetfootedStatic(entity);
            int vanillaBonus = hasFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxWalkingSpeedPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the melee damage bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMeleeDamagePercent.
        /// </summary>
        public static int CalculateMeleeBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMeleeDamagePercent);
        }

        /// <summary>
        /// Calculate ranged bonuses as percentages, accounting for vanilla Focused trait.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as integers.
        /// </summary>
        public static (int damage, int accuracy, int distance) CalculateRangedBonusPercents(int credits, EntityPlayer entity)
        {
            bool hasFocused = entity != null && PlayerHasVanillaFocusedStatic(entity);
            int vanillaDamage = hasFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Each stat is capped individually
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - vanillaDamage);
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - vanillaAccuracy);
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - vanillaDistance);

            int damageBonus = Math.Min(credits, earnableDamage);
            int accuracyBonus = Math.Min(credits, earnableAccuracy);
            int distanceBonus = Math.Min(credits, earnableDistance);

            return (damageBonus, accuracyBonus, distanceBonus);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Focused trait.
        /// </summary>
        private static bool PlayerHasVanillaFocusedStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("focused", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Focused (Hunter)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Fleetfooted trait.
        /// </summary>
        private static bool PlayerHasVanillaFleetfootedStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("fleetfooted", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Fleetfooted (Hunter, Clockmaker)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Soldier trait.
        /// </summary>
        private static bool PlayerHasVanillaSoldierForArmor(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("soldier", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Soldier (Blackguard)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calculate armor durability bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+15% armor durability).
        /// </summary>
        public static int CalculateArmorDurabilityBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorDurabilityPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate armor walk speed penalty reduction bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+25% armor walk speed penalty reduction).
        /// </summary>
        public static int CalculateArmorWalkSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorWalkSpeedPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Apply armor bonuses to a player.
        /// </summary>
        public static void ApplyArmorBonusesStatic(IServerPlayer player, int durabilityCredits, int walkSpeedCredits)
        {
            if (player?.Entity == null) return;

            // Check if player has vanilla Soldier (affects bonus cap)
            bool hasVanillaSoldier = PlayerHasVanillaSoldierForArmor(player.Entity);

            // Calculate durability bonus (reduces armor damage taken)
            int durabilityBonus = CalculateArmorDurabilityBonusPercent(durabilityCredits, player.Entity);
            // armorDurabilityLoss is a multiplier, lower = less durability lost
            // A bonus of 50% means armor loses 50% less durability, so multiplier = 0.5
            float durabilityMultiplier = 1f - (durabilityBonus * 0.01f);
            player.Entity.Stats.Set("armorDurabilityLoss", ARMOR_DURABILITY_STAT_CODE, durabilityMultiplier, false);

            // Calculate walk speed penalty reduction
            // This reduces the negative walkspeed effect from armor
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(walkSpeedCredits, player.Entity);
            // We add a positive walkspeed bonus to counteract armor penalty
            float walkSpeedAddition = walkSpeedBonus * 0.01f;
            player.Entity.Stats.Set("walkspeed", ARMOR_WALKSPEED_STAT_CODE, walkSpeedAddition, false);

            // Sync to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_DURABILITY_LEVEL, durabilityCredits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_DURABILITY_BONUS, durabilityBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, walkSpeedCredits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_WALKSPEED_BONUS, walkSpeedBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaSoldierArmor", hasVanillaSoldier);

            // Add our trait to extraTraits only if player doesn't already have Soldier
            UpdateExtraTraitStatic(player.Entity, ARMOR_TRAIT_CODE, (durabilityCredits > 0 || walkSpeedCredits > 0) && !hasVanillaSoldier);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_ARMOR_DURABILITY_LEVEL);
        }

        /// <summary>
        /// Determines the armor type from an item code.
        /// Returns: "light" (leather, gambeson), "chain", "brigandine", "scale", "plate", or null if not armor.
        /// </summary>
        public static string GetArmorType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            string codeToCheck = itemCode.StartsWith("game:") ? itemCode.Substring(5) : itemCode;

            // Check if it's armor (starts with "armor-")
            if (!codeToCheck.StartsWith("armor-")) return null;

            // Determine armor type from code
            if (codeToCheck.Contains("-plate-")) return "plate";
            if (codeToCheck.Contains("-scale-")) return "scale";
            if (codeToCheck.Contains("-brigandine-")) return "brigandine";
            if (codeToCheck.Contains("-chain-")) return "chain";
            if (codeToCheck.Contains("-lamellar-")) return "chain"; // Treat lamellar as chain for first-equip
            if (codeToCheck.Contains("-leather-") || codeToCheck.Contains("-gambeson-") ||
                codeToCheck.Contains("-jerkin-") || codeToCheck.Contains("-improvised-"))
                return "light";

            // Default to light if unrecognized armor type
            return "light";
        }

        /// <summary>
        /// Gets the first-equip durability bonus for an armor type.
        /// </summary>
        public static int GetFirstEquipBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FIRST_EQUIP_PLATE_BONUS;
                case "scale": return FIRST_EQUIP_SCALE_BONUS;
                case "brigandine": return FIRST_EQUIP_BRIGANDINE_BONUS;
                case "chain": return FIRST_EQUIP_CHAIN_BONUS;
                case "light":
                default: return FIRST_EQUIP_LIGHT_BONUS;
            }
        }

        /// <summary>
        /// Initialize armor tracking for a player by checking their currently equipped armor.
        /// </summary>
        private void InitializePlayerArmorTracking(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            // Get the player's currently equipped armor
            var equippedArmor = new Dictionary<string, string>();

            // Check armor slots (head, body, legs) using character inventory
            var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (characterInventory != null)
            {
                // Armor slots are typically: 12 = head, 13 = body, 14 = legs (may vary)
                foreach (var slot in characterInventory)
                {
                    if (slot?.Itemstack?.Collectible != null)
                    {
                        string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                        string armorType = GetArmorType(itemCode);
                        if (armorType != null)
                        {
                            string slotId = slot.Inventory?.InventoryID + "_" + slot.Inventory?.GetSlotId(slot);
                            equippedArmor[slotId] = itemCode;

                            // Check for first-time equip bonus
                            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                            var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                            if (!pieceProgress.HasBeenEquipped)
                            {
                                pieceProgress.HasBeenEquipped = true;
                                int firstEquipBonus = GetFirstEquipBonus(armorType);
                                armorProgress.TotalDurabilityCredits += firstEquipBonus;
                                pendingArmorProgressSave = true;

                                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} first-time equipped {itemCode}, +{firstEquipBonus}% durability bonus");

                                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);
                            }
                        }
                    }
                }
            }

            playerEquippedArmor[playerUid] = equippedArmor;
        }

        /// <summary>
        /// Game tick handler for armor time tracking.
        /// Checks each player's equipped armor and accumulates time credits.
        /// Also detects armor equip/unequip for first-equip bonus.
        /// </summary>
        private void OnArmorTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                var currentArmor = new Dictionary<string, string>();

                // Get the player's currently equipped armor using character inventory
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory != null)
                {
                    foreach (var slot in characterInventory)
                    {
                        if (slot?.Itemstack?.Collectible != null)
                        {
                            string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                            string armorType = GetArmorType(itemCode);
                            if (armorType != null)
                            {
                                string slotId = slot.Inventory?.InventoryID + "_" + slot.Inventory?.GetSlotId(slot);
                                currentArmor[slotId] = itemCode;
                            }
                        }
                    }
                }

                // Get previous armor state
                var previousArmor = playerEquippedArmor.GetOrAdd(playerUid, _ => new Dictionary<string, string>());

                // Check for newly equipped armor (first-equip bonus)
                foreach (var kvp in currentArmor)
                {
                    string slotId = kvp.Key;
                    string itemCode = kvp.Value;

                    // Check if this is new armor in this slot
                    if (!previousArmor.TryGetValue(slotId, out string prevArmor) || prevArmor != itemCode)
                    {
                        // New armor equipped - check for first-time bonus
                        var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                        if (!pieceProgress.HasBeenEquipped)
                        {
                            pieceProgress.HasBeenEquipped = true;
                            string armorType = GetArmorType(itemCode);
                            int firstEquipBonus = GetFirstEquipBonus(armorType);
                            armorProgress.TotalDurabilityCredits += firstEquipBonus;
                            pendingArmorProgressSave = true;

                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            player.SendMessage(GlobalConstants.GeneralChatGroup,
                                Lang.Get("simpleimprovingtraits:message-armor-first-equip", firstEquipBonus),
                                EnumChatType.Notification);
                        }
                    }

                    // Track time worn for this armor piece
                    var progress = armorProgress.GetArmorProgress(itemCode);
                    int oldWalkSpeedCredits = armorProgress.TotalWalkSpeedCredits;

                    progress.SecondsWornInIncrement += dt;

                    // Check if we've earned a time credit
                    while (progress.SecondsWornInIncrement >= progress.CurrentTimeIncrementSize &&
                           armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent)
                    {
                        progress.TimeCredits++;
                        armorProgress.TotalWalkSpeedCredits++;
                        progress.SecondsWornInIncrement -= progress.CurrentTimeIncrementSize;
                        progress.CurrentTimeIncrementSize += ArmorTimeIncrementStep;
                        pendingArmorProgressSave = true;

                        ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned time credit {progress.TimeCredits} with {itemCode}");
                    }

                    if (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits)
                    {
                        ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                        int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(armorProgress.TotalWalkSpeedCredits, player.Entity);
                        player.SendMessage(GlobalConstants.GeneralChatGroup,
                            Lang.Get("simpleimprovingtraits:message-armor-time-level-up", armorProgress.TotalWalkSpeedCredits, walkSpeedBonus),
                            EnumChatType.Notification);
                    }
                }

                // Update the equipped armor tracking
                playerEquippedArmor[playerUid] = currentArmor;
            }
        }

        /// <summary>
        /// Process armor damage blocked. Called from Harmony patch when player takes damage.
        /// </summary>
        public static void ProcessArmorDamageBlocked(IServerPlayer player, float damageBlocked, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            pieceProgress.DamageBlockedInIncrement += damageBlocked;

            // Check if we've earned any new damage credits
            while (pieceProgress.DamageBlockedInIncrement >= pieceProgress.CurrentDamageIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.DamageCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.DamageBlockedInIncrement -= pieceProgress.CurrentDamageIncrementSize;
                pieceProgress.CurrentDamageIncrementSize += ArmorDamageIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned damage credit {pieceProgress.DamageCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                int durabilityBonus = CalculateArmorDurabilityBonusPercent(armorProgress.TotalDurabilityCredits, player.Entity);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-armor-damage-level-up", armorProgress.TotalDurabilityCredits, durabilityBonus),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Process armor repair. Called from Harmony patch when armor is repaired.
        /// </summary>
        public static void ProcessArmorRepair(IServerPlayer player, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            pieceProgress.RepairsInIncrement++;

            // Check if we've earned a repair credit
            while (pieceProgress.RepairsInIncrement >= pieceProgress.CurrentRepairIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.RepairCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.RepairsInIncrement -= pieceProgress.CurrentRepairIncrementSize;
                pieceProgress.CurrentRepairIncrementSize += ArmorRepairIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned repair credit {pieceProgress.RepairCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                int durabilityBonus = CalculateArmorDurabilityBonusPercent(armorProgress.TotalDurabilityCredits, player.Entity);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-armor-repair-level-up", armorProgress.TotalDurabilityCredits, durabilityBonus),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Apply walking speed bonus to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// </summary>
        public static int ApplyWalkingBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Check if player has vanilla Fleetfooted (affects bonus cap)
            bool hasVanillaFleetfooted = PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (MaxWalkingSpeedPercent - vanillaFleetfootedBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));

            // Set the walk speed stat (persistent = false since we reapply on join)
            // walkspeed is a multiplicative stat used by Stats.GetBlended("walkspeed")
            // Adding 0.1 means +10% speed, 0.5 means +50%, etc.
            player.Entity.Stats.Set("walkspeed", WALKING_STAT_CODE, bonus, false);

            int bonusPercent = (int)(bonus * 100);

            // Sync level and bonus to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_WALKING_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_WALKING_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

            // Add our trait to extraTraits only if player doesn't already have Fleetfooted
            UpdateExtraTraitStatic(player.Entity, WALKING_TRAIT_CODE, level > 0 && !hasVanillaFleetfooted);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_WALKING_LEVEL);

            return bonusPercent;
        }

        /// <summary>
        /// Called when a player breaks a block. Updates mining progress based on new mechanics:
        /// - Only counts blocks broken with pickaxes
        /// - Only counts stone (1 point) and ore (5 points) blocks
        /// - Each pickaxe type tracks its own increment progress independently
        /// </summary>
        private void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return;

            // Check for Forager progression (wild crops on dirt, not farmland)
            if (IsWildCropBlock(oldblockId, blockSel?.Position))
            {
                ProcessWildCropBroken(byPlayer);
            }

            // Check for Pilferer progression (vessels)
            if (IsVesselBlock(oldblockId))
            {
                ProcessVesselBreak(byPlayer);
            }

            // Check if player is using a pickaxe for mining progression
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            if (pickaxeCode == null) return; // Not using a pickaxe, skip mining

            // Check block type and get points
            int points = GetBlockPoints(oldblockId);
            if (points <= 0) return; // Not a stone/ore block, skip

            string playerUid = byPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= MaxMiningSpeedPercent) return;

            // Get or create progress for this specific pickaxe type
            var pickaxeProgress = playerProgress.GetPickaxeProgress(pickaxeCode);

            int oldCredits = playerProgress.TotalCredits;

            // Add points to THIS pickaxe's progress
            pickaxeProgress.BlocksInIncrement += points;

            // Check if we've earned any new credits with this pickaxe
            while (pickaxeProgress.BlocksInIncrement >= pickaxeProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxMiningSpeedPercent)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                pickaxeProgress.BlocksInIncrement -= pickaxeProgress.CurrentIncrementSize;
                pickaxeProgress.CurrentIncrementSize += IncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {byPlayer.PlayerName} earned credit {playerProgress.TotalCredits} with {pickaxeCode}, next requires {pickaxeProgress.CurrentIncrementSize} points");
            }

            pendingMiningProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                int actualBonusPercent = ApplyMiningBonus(byPlayer, playerProgress.TotalCredits);

                // Notify player of level up with actual applied bonus (respects caps)
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-mining-level-up", playerProgress.TotalCredits, actualBonusPercent),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on mining level
                CheckHardyHealthUnlock(byPlayer);
                CheckClaustrophobicRemoval(byPlayer);
            }
        }

        /// <summary>
        /// Called every 500ms to track walking distance for all online players.
        /// Calculates 2D horizontal distance moved (ignoring Y-axis for climbing/falling).
        /// </summary>
        private void OnWalkingTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;
                Vec3d currentPos = player.Entity.Pos.XYZ;

                // Get or initialize last position
                if (!lastPlayerPositions.TryGetValue(playerUid, out Vec3d lastPos))
                {
                    lastPlayerPositions[playerUid] = currentPos.Clone();
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentPos.X - lastPos.X;
                double dz = currentPos.Z - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position
                lastPlayerPositions[playerUid] = currentPos.Clone();

                // Skip if no movement or teleportation (too far)
                if (distance < 0.01f || distance > MAX_DISTANCE_PER_TICK) continue;

                // Get or create player progress data
                var playerProgress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
                {
                    CurrentIncrementSize = BaseBlocksWalkedPerIncrement
                });

                // Skip all processing if already at max - completely invisible
                if (playerProgress.TotalCredits >= MaxWalkingSpeedPercent) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Add distance to progress
                playerProgress.BlocksInIncrement += distance;

                // Check if we've earned any new credits
                while (playerProgress.BlocksInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxWalkingSpeedPercent)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.BlocksInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += WalkingIncrementStep;

                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned walking credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingWalkingProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    int actualBonusPercent = ApplyWalkingBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with actual applied bonus (respects caps)
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("simpleimprovingtraits:message-walking-level-up", playerProgress.TotalCredits, actualBonusPercent),
                        EnumChatType.Notification);
                }
            }
        }

        /// <summary>
        /// Called every 1000ms (1 second) to track time spent at full saturation for all online players.
        /// Players at maximum saturation (1500/1500) accumulate time toward hunger rate reduction.
        /// </summary>
        private void OnHungerTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;

                // Get the player's hunger data from WatchedAttributes
                var hungerTree = player.Entity.WatchedAttributes.GetTreeAttribute("hunger");
                if (hungerTree == null) continue;

                // Check if player is at full saturation (1500/1500)
                float currentSaturation = hungerTree.GetFloat("currentsaturation", 0);
                float maxSaturation = hungerTree.GetFloat("maxsaturation", 1500);

                // Only count time when at exactly max saturation
                if (currentSaturation < maxSaturation) continue;

                // Get or create player progress data
                var playerProgress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                {
                    CurrentIncrementSize = BaseSecondsPerIncrement
                });

                // Calculate player-specific max credits (Ravenous players need more)
                int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);

                // Skip all processing if already at max - completely invisible
                if (playerProgress.TotalCredits >= playerMaxCredits) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Add 1 second of time (since tick is every 1000ms)
                playerProgress.SecondsInIncrement += 1f;

                // Check if we've earned any new credits
                while (playerProgress.SecondsInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < playerMaxCredits)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.SecondsInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += HungerIncrementStep;

                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned hunger credit {playerProgress.TotalCredits}/{playerMaxCredits}, next requires {playerProgress.CurrentIncrementSize} seconds");
                }

                // Mark for saving if any progress was made
                if (playerProgress.SecondsInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingHungerProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    int actualBonusPercent = ApplyHungerBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with actual applied bonus
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("simpleimprovingtraits:message-hunger-level-up", playerProgress.TotalCredits, actualBonusPercent),
                        EnumChatType.Notification);
                }
            }
        }

        /// <summary>
        /// Called when a player disconnects. Cleans up their position and armor tracking data.
        /// </summary>
        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;
            lastPlayerPositions.TryRemove(byPlayer.PlayerUID, out _);
            playerEquippedArmor.TryRemove(byPlayer.PlayerUID, out _);
        }

        /// <summary>
        /// Called when a player joins. Applies their saved bonuses (mining, melee, ranged, walking, and hunger).
        /// </summary>
        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            string playerUid = byPlayer.PlayerUID;

            // Apply mining bonus
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningCredits = miningProg.TotalCredits;
            ApplyMiningBonus(byPlayer, miningCredits);
            if (miningCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied mining bonus {miningCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply melee bonus
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            int meleeCredits = meleeProg.TotalCredits;
            ApplyMeleeBonusStatic(byPlayer, meleeCredits);
            if (meleeCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied melee bonus {meleeCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply ranged bonus
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            int rangedCredits = rangedProg.TotalCredits;
            ApplyRangedBonusStatic(byPlayer, rangedCredits);
            if (rangedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied ranged bonus {rangedCredits} credits to player {byPlayer.PlayerName}");
            }

            // Apply walking bonus
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });
            int walkingCredits = walkingProg.TotalCredits;
            ApplyWalkingBonusStatic(byPlayer, walkingCredits);
            if (walkingCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied walking bonus {walkingCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply hunger bonus
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });
            int hungerCredits = hungerProg.TotalCredits;
            ApplyHungerBonusStatic(byPlayer, hungerCredits);
            if (hungerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied hunger bonus -{hungerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply armor bonuses
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            ApplyArmorBonusesStatic(byPlayer, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (armorProg.TotalDurabilityCredits > 0 || armorProg.TotalWalkSpeedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied armor bonuses: +{armorProg.TotalDurabilityCredits}% durability, -{armorProg.TotalWalkSpeedCredits}% walk speed penalty to player {byPlayer.PlayerName}");
            }

            // Apply clothier bonus
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            ApplyClothierBonusStatic(byPlayer, clothierProg);
            if (clothierProg.SewingKitUnlocked)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied clothier unlock to player {byPlayer.PlayerName}");
            }

            // Apply mender bonus
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData
            {
                CurrentIncrementSize = BaseMenderRepairsPerIncrement
            });
            int menderCredits = menderProg.TotalCredits;
            ApplyMenderBonusStatic(byPlayer, menderCredits);
            if (menderCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied mender bonus +{menderCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply pilferer bonus
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData
            {
                CurrentIncrementSize = BasePilfererPointsPerIncrement
            });
            int pilfererCredits = pilfererProg.TotalCredits;
            ApplyPilfererBonusStatic(byPlayer, pilfererCredits);
            if (pilfererCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied pilferer bonus +{pilfererCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply resourceful bonus
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData
            {
                CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement
            });
            int resourcefulCredits = resourcefulProg.TotalCredits;
            ApplyResourcefulBonusStatic(byPlayer, resourcefulCredits);
            if (resourcefulCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied resourceful bonus +{resourcefulCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply forager bonus
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData
            {
                CurrentIncrementSize = BaseForagerCropsPerIncrement
            });
            int foragerCredits = foragerProg.TotalCredits;
            ApplyForagerBonusStatic(byPlayer, foragerCredits);
            if (foragerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied forager bonus +{foragerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply furtive bonus
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
            {
                CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
            });
            int furtiveCredits = furtiveProg.TotalCredits;
            ApplyFurtiveBonusStatic(byPlayer, furtiveCredits);
            if (furtiveCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied furtive bonus -{furtiveCredits}% detection to player {byPlayer.PlayerName}");
            }

            // Apply precise bonus
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            int preciseCredits = preciseProg.TotalCredits;
            ApplyPreciseBonusStatic(byPlayer, preciseCredits);
            if (preciseCredits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied precise bonus +{preciseCredits}% mechanical damage to player {byPlayer.PlayerName}");
            }

            // Apply technical unlock
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (technicalProg.IsUnlocked)
            {
                ApplyTechnicalBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied technical unlock to player {byPlayer.PlayerName}");
            }

            // Apply hardy health unlock
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            if (hardyHealthProg.IsUnlocked)
            {
                ApplyHardyHealthBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied hardy health +{HardyHealthBonus} HP to player {byPlayer.PlayerName}");
            }

            // Apply bowyer unlock
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            if (bowyerProg.IsUnlocked)
            {
                ApplyBowyerBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied bowyer unlock to player {byPlayer.PlayerName}");
            }

            // Apply improviser unlock
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            if (improviserProg.IsUnlocked)
            {
                ApplyImproviserBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied improviser unlock to player {byPlayer.PlayerName}");
            }

            // Apply tinkerer unlock
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            if (tinkererProg.IsUnlocked)
            {
                ApplyTinkererBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied tinkerer unlock to player {byPlayer.PlayerName}");
            }

            // Apply merciless unlock
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            if (mercilessProg.IsUnlocked)
            {
                ApplyMercilessBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied merciless unlock to player {byPlayer.PlayerName}");
            }

            // Apply claustrophobic removal
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            if (claustrophobicProg.IsRemoved)
            {
                ApplyClaustrophobicRemovalStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied claustrophobic removal to player {byPlayer.PlayerName}");
            }

            // Initialize equipped armor tracking for this player
            InitializePlayerArmorTracking(byPlayer);
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// Also syncs the level and bonus to WatchedAttributes for client display,
        /// and adds/removes the mining mastery trait from extraTraits.
        /// Returns the actual applied bonus percentage (0-100 scale).
        /// </summary>
        private int ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Check if player has vanilla Hardy (affects bonus cap)
            bool hasVanillaHardy = PlayerHasVanillaHardy(player.Entity);
            int vanillaHardyBonus = hasVanillaHardy ? VANILLA_HARDY_MINING_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMiningSpeedPercent
            float maxEarnableBonus = (MaxMiningSpeedPercent - vanillaHardyBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));

            // Set the mining speed stat (persistent = false since we reapply on join)
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, 1f + bonus, false);

            int bonusPercent = (int)(bonus * 100);

            // Sync level and bonus to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_MINING_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MINING_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaHardy", hasVanillaHardy);

            // Add our trait to extraTraits only if player doesn't already have Hardy
            // (if they have Hardy, we update the existing trait display instead of adding duplicate)
            UpdateExtraTrait(player.Entity, MINING_TRAIT_CODE, level > 0 && !hasVanillaHardy);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MINING_LEVEL);

            return bonusPercent;
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Hardy trait.
        /// </summary>
        private bool PlayerHasVanillaHardy(EntityPlayer entity)
        {
            // Get the player's class traits (not extraTraits which we manage)
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("hardy", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Hardy
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds or removes a trait from the player's extraTraits array.
        /// </summary>
        private void UpdateExtraTrait(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            // Get current extra traits
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                // Add the trait
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Added trait {traitCode} to player");
            }
            else if (!shouldHave && hasTrait)
            {
                // Remove the trait
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Removed trait {traitCode} from player");
            }
        }

        /// <summary>
        /// Calculate the mining speed bonus as a float (0.0 to 1.5 for 0% to 150%).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static float CalculateMiningBonus(int credits)
        {
            float bonus = credits * 0.01f;
            return Math.Min(bonus, MaxMiningSpeedPercent / 100f);
        }

        /// <summary>
        /// Calculate the mining speed bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static int CalculateMiningBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMiningSpeedPercent);
        }

        /// <summary>
        /// Calculate the maximum credits (level) based on the bonus cap.
        /// </summary>
        public static int CalculateMaxCredits()
        {
            return MaxMiningSpeedPercent;
        }

        // Server-side Harmony instance for melee damage tracking
        private Harmony serverHarmony;

        /// <summary>
        /// Apply Harmony patches for server-side melee damage tracking.
        /// </summary>
        private void ApplyServerHarmonyPatches(ICoreServerAPI api)
        {
            serverHarmony = new Harmony("simpleimprovingtraits.server");

            try
            {
                // Find Entity.ReceiveDamage method
                var entityType = typeof(Entity);
                var receiveDamageMethod = entityType.GetMethod("ReceiveDamage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (receiveDamageMethod == null)
                {
                    api.Logger.Warning("[SimpleImprovingTraits] Could not find Entity.ReceiveDamage method");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(EntityDamagePatches),
                    nameof(EntityDamagePatches.ReceiveDamage_Postfix));

                serverHarmony.Patch(receiveDamageMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SimpleImprovingTraits] Successfully patched Entity.ReceiveDamage for melee tracking");

                // Patch EntityBehaviorHarvestable.SetHarvested for Resourceful trait (animal harvesting)
                PatchAnimalHarvesting(api);

                // Patch CollectibleObject.OnHeldInteractStep for Mender trait (sewing kit repairs)
                PatchSewingKitRepairs(api);

                // Patch BlockEntityStaticTranslocator.DoRepair for Technical trait (translocator repairs)
                PatchTranslocatorRepairs(api);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SimpleImprovingTraits] Failed to apply server Harmony patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch EntityBehaviorHarvestable.SetHarvested to track animal harvesting for Resourceful trait.
        /// </summary>
        private void PatchAnimalHarvesting(ICoreServerAPI api)
        {
            try
            {
                // Find the EntityBehaviorHarvestable type in VSSurvivalMod
                var harvestableType = AccessTools.TypeByName("Vintagestory.GameContent.EntityBehaviorHarvestable");
                if (harvestableType == null)
                {
                    api.Logger.Warning("[SimpleImprovingTraits] Could not find EntityBehaviorHarvestable type");
                    return;
                }

                // Find the SetHarvested method
                var setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvested");
                if (setHarvestedMethod == null)
                {
                    // Try alternative method name
                    setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvestedBy");
                }
                if (setHarvestedMethod == null)
                {
                    api.Logger.Warning("[SimpleImprovingTraits] Could not find SetHarvested or SetHarvestedBy method in EntityBehaviorHarvestable");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(HarvestingPatches),
                    nameof(HarvestingPatches.SetHarvested_Postfix));

                serverHarmony.Patch(setHarvestedMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SimpleImprovingTraits] Successfully patched EntityBehaviorHarvestable.SetHarvested for Resourceful trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SimpleImprovingTraits] Failed to patch EntityBehaviorHarvestable: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch methods to track sewing kit repairs for Mender trait.
        /// Tries multiple approaches since sewing kit repairs can happen in different ways.
        /// </summary>
        private void PatchSewingKitRepairs(ICoreServerAPI api)
        {
            bool anyPatchSucceeded = false;

            // Approach 1: Try to patch ItemSewingKit directly if it exists
            try
            {
                var sewingKitType = AccessTools.TypeByName("Vintagestory.GameContent.ItemSewingKit");
                if (sewingKitType != null)
                {
                    // Try to find repair-related methods
                    var onHeldInteractStopMethod = AccessTools.Method(sewingKitType, "OnHeldInteractStop");
                    if (onHeldInteractStopMethod != null)
                    {
                        var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                            nameof(SewingKitPatches.OnHeldInteractStop_Postfix));
                        serverHarmony.Patch(onHeldInteractStopMethod, postfix: new HarmonyMethod(postfixMethod));
                        api.Logger.Notification("[SimpleImprovingTraits] Successfully patched ItemSewingKit.OnHeldInteractStop for Mender trait");
                        anyPatchSucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SimpleImprovingTraits] ItemSewingKit patch attempt: {ex.Message}");
            }

            // Approach 2: Patch CollectibleObject.OnModifiedInInventorySlot to detect durability restoration
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onModifiedMethod = AccessTools.Method(collectibleType, "OnModifiedInInventorySlot");
                if (onModifiedMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.OnModifiedInInventorySlot_Postfix));
                    serverHarmony.Patch(onModifiedMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SimpleImprovingTraits] Successfully patched CollectibleObject.OnModifiedInInventorySlot for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SimpleImprovingTraits] OnModifiedInInventorySlot patch attempt: {ex.Message}");
            }

            // Approach 3: Patch OnHeldInteractStep as fallback
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onHeldInteractStepMethod = AccessTools.Method(collectibleType, "OnHeldInteractStep");
                if (onHeldInteractStepMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.OnHeldInteractStep_Postfix));
                    serverHarmony.Patch(onHeldInteractStepMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SimpleImprovingTraits] Successfully patched CollectibleObject.OnHeldInteractStep for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SimpleImprovingTraits] OnHeldInteractStep patch attempt: {ex.Message}");
            }

            if (!anyPatchSucceeded)
            {
                api.Logger.Warning("[SimpleImprovingTraits] Could not patch any method for Mender trait (sewing kit repairs)");
            }
        }

        /// <summary>
        /// Patch BlockEntityStaticTranslocator.DoRepair to track translocator repairs for Technical trait.
        /// </summary>
        private void PatchTranslocatorRepairs(ICoreServerAPI api)
        {
            try
            {
                // Find the BlockEntityStaticTranslocator type
                var translocatorType = AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityStaticTranslocator");
                if (translocatorType == null)
                {
                    api.Logger.Warning("[SimpleImprovingTraits] Could not find BlockEntityStaticTranslocator type");
                    return;
                }

                // Find the DoRepair method
                var doRepairMethod = AccessTools.Method(translocatorType, "DoRepair");
                if (doRepairMethod == null)
                {
                    api.Logger.Warning("[SimpleImprovingTraits] Could not find DoRepair method in BlockEntityStaticTranslocator");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(TranslocatorPatches),
                    nameof(TranslocatorPatches.DoRepair_Postfix));

                serverHarmony.Patch(doRepairMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SimpleImprovingTraits] Successfully patched BlockEntityStaticTranslocator.DoRepair for Technical trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SimpleImprovingTraits] Failed to patch BlockEntityStaticTranslocator.DoRepair: {ex.Message}");
            }
        }

        /// <summary>
        /// Process melee damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessMeleeDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer == null || string.IsNullOrEmpty(weaponType)) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= MaxMeleeDamagePercent) return;

            // Get or create progress for this specific weapon type
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            int oldCredits = playerProgress.TotalCredits;

            // Add damage to THIS weapon type's progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits with this weapon type
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxMeleeDamagePercent)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += MeleeIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {attackerPlayer.PlayerName} earned melee credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingMeleeProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                int actualBonusPercent = ApplyMeleeBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with actual applied bonus (respects caps)
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-melee-level-up", playerProgress.TotalCredits, actualBonusPercent),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on melee damage
                CheckMercilessUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Static version of ApplyMeleeBonus for use from Harmony patches.
        /// </summary>
        private static int ApplyMeleeBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Check if player has vanilla Soldier (affects bonus cap)
            bool hasVanillaSoldier = PlayerHasVanillaSoldierStatic(player.Entity);
            int vanillaSoldierBonus = hasVanillaSoldier ? VANILLA_SOLDIER_MELEE_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMeleeDamagePercent
            float maxEarnableBonus = (MaxMeleeDamagePercent - vanillaSoldierBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));

            // Set the melee damage stat (persistent = false since we reapply on join)
            // Note: meleeWeaponsDamage is an additive stat, so we just add the bonus (not 1 + bonus)
            player.Entity.Stats.Set("meleeWeaponsDamage", MELEE_STAT_CODE, bonus, false);

            int bonusPercent = (int)(bonus * 100);

            // Sync level and bonus to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_MELEE_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MELEE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaSoldier", hasVanillaSoldier);

            // Add our trait to extraTraits only if player doesn't already have Soldier
            UpdateExtraTraitStatic(player.Entity, MELEE_TRAIT_CODE, level > 0 && !hasVanillaSoldier);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MELEE_LEVEL);

            return bonusPercent;
        }

        /// <summary>
        /// Static version of PlayerHasVanillaSoldier for use from Harmony patches.
        /// </summary>
        private static bool PlayerHasVanillaSoldierStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("soldier", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Static version of UpdateExtraTrait for use from Harmony patches.
        /// </summary>
        private static void UpdateExtraTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
        }

        /// <summary>
        /// Updates the characterTraits array to add or remove a trait.
        /// This is used for traits that unlock recipes (like Clothier).
        /// Unlike extraTraits which is only for UI display, characterTraits is
        /// what the game actually checks for recipe requirements.
        /// </summary>
        private static void UpdateCharacterTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("characterTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("characterTraits");
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("characterTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("characterTraits");
            }
        }

        /// <summary>
        /// Gets the weapon code from a held item if it's a qualifying melee weapon, or null otherwise.
        /// Returns the full item code (e.g., "game:sword-copper") to track each weapon type individually.
        /// Static version for use from Harmony patches.
        /// </summary>
        public static string GetWeaponTypeFromCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            string codeToCheck = itemCode.StartsWith("game:") ? itemCode.Substring(5) : itemCode;

            // Check for sword types
            if (codeToCheck.StartsWith("sword-") ||
                codeToCheck.StartsWith("blade-") ||
                codeToCheck.StartsWith("longsword-") ||
                codeToCheck.StartsWith("shortsword-"))
            {
                return itemCode; // Return full code for per-weapon tracking
            }

            // Check for falx types
            if (codeToCheck.StartsWith("falx-"))
            {
                return itemCode; // Return full code for per-weapon tracking
            }

            // Check for spear types
            if (codeToCheck.StartsWith("spear-"))
            {
                return itemCode; // Return full code for per-weapon tracking
            }

            return null;
        }

        /// <summary>
        /// Process ranged damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessRangedDamage(IServerPlayer attackerPlayer, string weaponCombo, float damage)
        {
            if (attackerPlayer == null || string.IsNullOrEmpty(weaponCombo)) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= MaxRangedDamagePercent) return;

            // Get or create progress for this specific weapon combination
            var weaponProgress = playerProgress.GetWeaponProgress(weaponCombo);

            int oldCredits = playerProgress.TotalCredits;

            // Add damage to THIS weapon combination's progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits with this weapon combination
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxRangedDamagePercent)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += RangedIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {attackerPlayer.PlayerName} earned ranged credit {playerProgress.TotalCredits} with {weaponCombo}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingRangedProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                var (damageBonus, accuracyBonus, distanceBonus) = ApplyRangedBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with actual applied bonuses
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-ranged-level-up", playerProgress.TotalCredits, damageBonus, accuracyBonus, distanceBonus),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on ranged damage
                CheckBowyerUnlock(attackerPlayer);
            }

            // Track bow damage for Bowyer unlock (simple bow or longbow)
            if (IsSimpleBowOrLongbow(weaponCombo))
            {
                TrackBowyerBowDamage(attackerPlayer, damage);
            }

            // Track thrown rock damage for Improviser unlock
            if (IsThrownRock(weaponCombo))
            {
                TrackImproviserRockDamage(attackerPlayer, damage);
            }
        }

        /// <summary>
        /// Check if the weapon combo represents a simple bow or longbow.
        /// </summary>
        private static bool IsSimpleBowOrLongbow(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("bow-simple") || lower.Contains("bow-long") ||
                   lower.StartsWith("simple") || lower.StartsWith("long");
        }

        /// <summary>
        /// Check if the weapon combo represents a thrown rock.
        /// </summary>
        private static bool IsThrownRock(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("stone-") || lower.Contains("sling+stone") ||
                   lower.StartsWith("stone") || lower.Contains("thrownstone") ||
                   (lower.Contains("stone") && !lower.Contains("whetstone"));
        }

        /// <summary>
        /// Track bow damage for Bowyer unlock.
        /// </summary>
        private static void TrackBowyerBowDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            progress.TotalBowDamage += damage;
            pendingBowyerProgressSave = true;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_BOWYER_BOW_DAMAGE, progress.TotalBowDamage);

            // Check if unlock threshold is reached
            CheckBowyerUnlock(player);
        }

        /// <summary>
        /// Track thrown rock damage for Improviser unlock.
        /// </summary>
        private static void TrackImproviserRockDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            progress.TotalRockDamage += damage;
            pendingImproviserProgressSave = true;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_IMPROVISER_ROCK_DAMAGE, progress.TotalRockDamage);

            // Check if unlock threshold is reached
            CheckImproviserUnlock(player);
        }

        /// <summary>
        /// Static version of ApplyRangedBonus for use from Harmony patches.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as percentages.
        /// </summary>
        public static (int damage, int accuracy, int distance) ApplyRangedBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            // Check if player has vanilla Focused (affects bonus caps)
            bool hasVanillaFocused = PlayerHasVanillaFocusedStatic(player.Entity);
            int vanillaDamage = hasVanillaFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasVanillaFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasVanillaFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Calculate earnable bonuses (each stat capped individually)
            float earnableDamage = Math.Max(0, (MaxRangedDamagePercent - vanillaDamage) / 100f);
            float earnableAccuracy = Math.Max(0, (MaxRangedAccuracyPercent - vanillaAccuracy) / 100f);
            float earnableDistance = Math.Max(0, (MaxRangedDistancePercent - vanillaDistance) / 100f);

            // Calculate actual bonuses from level
            float rawBonus = level * 0.01f;
            float damageBonus = Math.Min(rawBonus, earnableDamage);
            float accuracyBonus = Math.Min(rawBonus, earnableAccuracy);
            float distanceBonus = Math.Min(rawBonus, earnableDistance);

            // Set the ranged stats (persistent = false since we reapply on join)
            // Note: All ranged stats are additive (0 = no change, not 1.0)
            // rangedWeaponsDamage - affects projectile damage
            // rangedWeaponsAcc - affects aim accuracy (reticle size)
            // bowDrawingStrength - affects projectile velocity, thus travel distance (this is how vanilla Focused implements +20% ranged distance)
            player.Entity.Stats.Set("rangedWeaponsDamage", RANGED_DAMAGE_STAT_CODE, damageBonus, false);
            player.Entity.Stats.Set("rangedWeaponsAcc", RANGED_ACCURACY_STAT_CODE, accuracyBonus, false);
            player.Entity.Stats.Set("bowDrawingStrength", RANGED_DISTANCE_STAT_CODE, distanceBonus, false);

            // Debug logging to verify stats are being applied
            if (damageBonus > 0 || accuracyBonus > 0 || distanceBonus > 0)
            {
                ServerApi?.Logger.Debug($"[SimpleImprovingTraits] Applied ranged stats to {player.PlayerName}: Damage={damageBonus:F2}, Accuracy={accuracyBonus:F2}, Distance={distanceBonus:F2}");
            }

            int damagePct = (int)(damageBonus * 100);
            int accuracyPct = (int)(accuracyBonus * 100);
            int distancePct = (int)(distanceBonus * 100);

            // Sync level and bonuses to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_RANGED_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RANGED_DAMAGE_BONUS, damagePct);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RANGED_ACCURACY_BONUS, accuracyPct);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RANGED_DISTANCE_BONUS, distancePct);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaFocused", hasVanillaFocused);

            // Add our trait to extraTraits only if player doesn't already have Focused
            UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_RANGED_LEVEL);

            return (damagePct, accuracyPct, distancePct);
        }

        /// <summary>
        /// Gets a weapon combination code from a projectile and the shooter's held weapon.
        /// For bows+arrows, returns "bowCode+arrowCode" (e.g., "bow-long+arrow-copper").
        /// For slings+stones, returns "sling+stone".
        /// Returns null if not a qualifying ranged weapon.
        /// </summary>
        public static string GetRangedWeaponCombo(Entity projectile, EntityPlayer shooter)
        {
            if (projectile == null || shooter == null) return null;

            string projectileCode = projectile.Code?.ToString() ?? "";
            string heldItemCode = shooter.RightHandItemSlot?.Itemstack?.Collectible?.Code?.ToString() ?? "";

            // Remove prefixes for checking
            string projCheck = projectileCode.StartsWith("game:") ? projectileCode.Substring(5) : projectileCode;
            string heldCheck = heldItemCode.StartsWith("game:") ? heldItemCode.Substring(5) : heldItemCode;

            // Check for arrow projectiles
            if (projCheck.StartsWith("arrow-") || projCheck == "arrow")
            {
                // Get bow type from held item (if still holding a bow)
                string bowCode = "unknown-bow";
                if (heldCheck.StartsWith("bow-") || heldCheck == "bow" ||
                    heldCheck.StartsWith("longbow") || heldCheck.StartsWith("recurvebow") ||
                    heldCheck.StartsWith("crudebow") || heldCheck.StartsWith("simplebow"))
                {
                    bowCode = heldCheck;
                }
                return $"{bowCode}+{projCheck}";
            }

            // Check for sling stones (thrown stones)
            if (projCheck.StartsWith("stone-") || projCheck == "stone" || projCheck.StartsWith("thrownstone"))
            {
                // Check if holding a sling
                string slingCode = "thrown";
                if (heldCheck.StartsWith("sling"))
                {
                    slingCode = heldCheck;
                }
                return $"{slingCode}+{projCheck}";
            }

            // Check for spear throws (thrown spears deal ranged damage)
            if (projCheck.StartsWith("spear-") || projCheck.StartsWith("thrownspear"))
            {
                return $"thrown+{projCheck}";
            }

            return null;
        }

        /// <summary>
        /// Checks if a damage source is from a ranged attack (projectile).
        /// </summary>
        public static bool IsRangedDamage(DamageSource damageSource)
        {
            // CauseEntity is non-null for projectile damage (it's the shooter)
            // SourceEntity is the projectile itself
            if (damageSource?.CauseEntity == null) return false;

            // Additional check: the damage should be from a projectile type
            // PiercingAttack is typically used for arrows
            return damageSource.Type == EnumDamageType.PiercingAttack ||
                   damageSource.Type == EnumDamageType.BluntAttack; // For thrown stones
        }

        public override void Dispose()
        {
            // Persist any pending progress before shutdown
            if (ServerApi != null)
            {
                if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
                {
                    PersistMiningProgress();
                }
                if (pendingMeleeProgressSave || !MeleeProgress.IsEmpty)
                {
                    PersistMeleeProgress();
                }
                if (pendingRangedProgressSave || !RangedProgress.IsEmpty)
                {
                    PersistRangedProgress();
                }
                if (pendingWalkingProgressSave || !WalkingProgress.IsEmpty)
                {
                    PersistWalkingProgress();
                }
                if (pendingHungerProgressSave || !HungerProgress.IsEmpty)
                {
                    PersistHungerProgress();
                }
                if (pendingArmorProgressSave || !ArmorProgress.IsEmpty)
                {
                    PersistArmorProgress();
                }
                if (pendingClothierProgressSave || !ClothierProgress.IsEmpty)
                {
                    PersistClothierProgress();
                }
                if (pendingMenderProgressSave || !MenderProgress.IsEmpty)
                {
                    PersistMenderProgress();
                }
                if (pendingPilfererProgressSave || !PilfererProgress.IsEmpty)
                {
                    PersistPilfererProgress();
                }
                if (pendingResourcefulProgressSave || !ResourcefulProgress.IsEmpty)
                {
                    PersistResourcefulProgress();
                }
                if (pendingForagerProgressSave || !ForagerProgress.IsEmpty)
                {
                    PersistForagerProgress();
                }
                if (pendingFurtiveProgressSave || !FurtiveProgress.IsEmpty)
                {
                    PersistFurtiveProgress();
                }
                if (pendingPreciseProgressSave || !PreciseProgress.IsEmpty)
                {
                    PersistPreciseProgress();
                }
                if (pendingTechnicalProgressSave || !TechnicalProgress.IsEmpty)
                {
                    PersistTechnicalProgress();
                }
                if (pendingHardyHealthProgressSave || !HardyHealthProgress.IsEmpty)
                {
                    PersistHardyHealthProgress();
                }
                if (pendingBowyerProgressSave || !BowyerProgress.IsEmpty)
                {
                    PersistBowyerProgress();
                }
                if (pendingImproviserProgressSave || !ImproviserProgress.IsEmpty)
                {
                    PersistImproviserProgress();
                }
                if (pendingTinkererProgressSave || !TinkererProgress.IsEmpty)
                {
                    PersistTinkererProgress();
                }
                if (pendingMercilessProgressSave || !MercilessProgress.IsEmpty)
                {
                    PersistMercilessProgress();
                }
                if (pendingClaustrophobicRemovalProgressSave || !ClaustrophobicRemovalProgress.IsEmpty)
                {
                    PersistClaustrophobicRemovalProgress();
                }

                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMeleeProgress;
                ServerApi.Event.SaveGameLoaded -= LoadRangedProgress;
                ServerApi.Event.SaveGameLoaded -= LoadWalkingProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHungerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadArmorProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClothierProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMenderProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPilfererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadResourcefulProgress;
                ServerApi.Event.SaveGameLoaded -= LoadForagerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadFurtiveProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPreciseProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTechnicalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHardyHealthProgress;
                ServerApi.Event.SaveGameLoaded -= LoadBowyerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadImproviserProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTinkererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMercilessProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClaustrophobicRemovalProgress;
            }

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("simpleimprovingtraits.server");

            MiningProgress.Clear();
            MeleeProgress.Clear();
            RangedProgress.Clear();
            WalkingProgress.Clear();
            HungerProgress.Clear();
            ArmorProgress.Clear();
            ClothierProgress.Clear();
            MenderProgress.Clear();
            PilfererProgress.Clear();
            ResourcefulProgress.Clear();
            ForagerProgress.Clear();
            FurtiveProgress.Clear();
            PreciseProgress.Clear();
            TechnicalProgress.Clear();
            HardyHealthProgress.Clear();
            BowyerProgress.Clear();
            ImproviserProgress.Clear();
            TinkererProgress.Clear();
            MercilessProgress.Clear();
            ClaustrophobicRemovalProgress.Clear();
            lastPlayerPositions.Clear();
            lastSneakingPositions.Clear();
            pendingMiningProgressSave = false;
            pendingMeleeProgressSave = false;
            pendingRangedProgressSave = false;
            pendingWalkingProgressSave = false;
            pendingHungerProgressSave = false;
            pendingArmorProgressSave = false;
            pendingClothierProgressSave = false;
            pendingMenderProgressSave = false;
            pendingPilfererProgressSave = false;
            pendingResourcefulProgressSave = false;
            pendingForagerProgressSave = false;
            pendingFurtiveProgressSave = false;
            pendingPreciseProgressSave = false;
            pendingTechnicalProgressSave = false;
            pendingHardyHealthProgressSave = false;
            pendingBowyerProgressSave = false;
            pendingImproviserProgressSave = false;
            pendingTinkererProgressSave = false;
            pendingMercilessProgressSave = false;
            pendingClaustrophobicRemovalProgressSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Called when the world is saved. Persist all progress and config to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
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
        public static void PersistMiningProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (MiningProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(MINING_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = MiningProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)3);    // Version 3: Per-pickaxe progress

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);

                                // Write per-pickaxe progress dictionary
                                writer.Write(progress.PickaxeProgress.Count);
                                foreach (var pickaxeKvp in progress.PickaxeProgress)
                                {
                                    writer.Write(pickaxeKvp.Key); // Pickaxe code
                                    writer.Write(pickaxeKvp.Value.BlocksInIncrement);
                                    writer.Write(pickaxeKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MINING_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted mining progress for {snapshot.Length} players (v3 format)");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist mining progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load mining progress from world save data.
        /// Supports versions 1 (legacy blocks), 2 (single pickaxe), and 3 (per-pickaxe).
        /// </summary>
        private void LoadMiningProgress()
        {
            if (ServerApi == null) return;

            MiningProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MINING_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No mining progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x54) // "SIT"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid mining progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Legacy format: convert old blocks-based progress to credits
                            ServerApi.Logger.Notification("[SimpleImprovingTraits] Converting legacy v1 save data to v3 format...");
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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
                                MiningProgress[playerUid] = progress;
                            }
                            pendingMiningProgressSave = true;
                        }
                        else if (version == 2)
                        {
                            // Version 2: single pickaxe tracking - convert to v3
                            ServerApi.Logger.Notification("[SimpleImprovingTraits] Converting v2 save data to v3 format...");
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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

                                MiningProgress[playerUid] = progress;
                            }
                            pendingMiningProgressSave = true;
                        }
                        else if (version == 3)
                        {
                            // Current format: per-pickaxe progress
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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

                                MiningProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded mining progress for {MiningProgress.Count} players");
            }
            catch (Exception ex)
            {
                MiningProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load mining progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist melee progress to world save data.
        /// Version 1 format stores per-weapon progress dictionary.
        /// </summary>
        public static void PersistMeleeProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (MeleeProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(MELEE_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = MeleeProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x4D); // 'M' (for Melee)
                            writer.Write((byte)1);    // Version 1: Per-weapon progress

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);

                                // Write per-weapon progress dictionary
                                writer.Write(progress.WeaponProgress.Count);
                                foreach (var weaponKvp in progress.WeaponProgress)
                                {
                                    writer.Write(weaponKvp.Key); // Weapon type
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MELEE_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted melee progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist melee progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load melee progress from world save data.
        /// </summary>
        private void LoadMeleeProgress()
        {
            if (ServerApi == null) return;

            MeleeProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MELEE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No melee progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x4D) // "SIM"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid melee progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Current format: per-weapon progress
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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

                                MeleeProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown melee save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded melee progress for {MeleeProgress.Count} players");
            }
            catch (Exception ex)
            {
                MeleeProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load melee progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist ranged progress to world save data.
        /// Version 1 format stores per-weapon progress dictionary.
        /// </summary>
        public static void PersistRangedProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (RangedProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(RANGED_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = RangedProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x52); // 'R' (for Ranged)
                            writer.Write((byte)1);    // Version 1: Per-weapon progress

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);

                                // Write per-weapon progress dictionary
                                writer.Write(progress.WeaponProgress.Count);
                                foreach (var weaponKvp in progress.WeaponProgress)
                                {
                                    writer.Write(weaponKvp.Key); // Weapon combo
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(RANGED_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted ranged progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist ranged progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load ranged progress from world save data.
        /// </summary>
        private void LoadRangedProgress()
        {
            if (ServerApi == null) return;

            RangedProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(RANGED_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No ranged progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x52) // "SIR"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid ranged progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Current format: per-weapon progress
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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

                                RangedProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown ranged save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded ranged progress for {RangedProgress.Count} players");
            }
            catch (Exception ex)
            {
                RangedProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load ranged progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist walking progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistWalkingProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (WalkingProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(WALKING_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = WalkingProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x57); // 'W' (for Walking)
                            writer.Write((byte)1);    // Version 1

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.BlocksInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(WALKING_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted walking progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist walking progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load walking progress from world save data.
        /// </summary>
        private void LoadWalkingProgress()
        {
            if (ServerApi == null) return;

            WalkingProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(WALKING_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No walking progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x57) // "SIW"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid walking progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
                                var progress = new WalkingProgressData
                                {
                                    TotalCredits = reader.ReadInt32(),
                                    BlocksInIncrement = reader.ReadSingle(),
                                    CurrentIncrementSize = reader.ReadInt32()
                                };

                                WalkingProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown walking save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded walking progress for {WalkingProgress.Count} players");
            }
            catch (Exception ex)
            {
                WalkingProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load walking progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist hunger progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistHungerProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (HungerProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(HUNGER_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = HungerProgress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Write magic bytes and version
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x49); // 'I'
                            writer.Write((byte)0x48); // 'H' (for Hunger)
                            writer.Write((byte)1);    // Version 1

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.SecondsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(HUNGER_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted hunger progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist hunger progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load hunger progress from world save data.
        /// </summary>
        private void LoadHungerProgress()
        {
            if (ServerApi == null) return;

            HungerProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(HUNGER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No hunger progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x48) // "SIH"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid hunger progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
                                var progress = new HungerProgressData
                                {
                                    TotalCredits = reader.ReadInt32(),
                                    SecondsInIncrement = reader.ReadSingle(),
                                    CurrentIncrementSize = reader.ReadInt32()
                                };

                                HungerProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown hunger save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded hunger progress for {HungerProgress.Count} players");
            }
            catch (Exception ex)
            {
                HungerProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load hunger progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist armor progress to world save data.
        /// Version 1 format stores durability credits, walk speed credits, and per-armor progress.
        /// </summary>
        public static void PersistArmorProgress()
        {
            if (ServerApi == null) return;

            try
            {
                byte[] data;
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        // Header: "SIA" + version
                        writer.Write((byte)0x53); // 'S'
                        writer.Write((byte)0x49); // 'I'
                        writer.Write((byte)0x41); // 'A' for Armor
                        writer.Write((byte)1);    // Version 1

                        // Number of players
                        writer.Write(ArmorProgress.Count);

                        foreach (var kvp in ArmorProgress)
                        {
                            string playerUid = kvp.Key;
                            var progress = kvp.Value;

                            writer.Write(playerUid);
                            writer.Write(progress.TotalDurabilityCredits);
                            writer.Write(progress.TotalWalkSpeedCredits);

                            // Write per-armor progress
                            writer.Write(progress.ArmorProgress.Count);
                            foreach (var armorKvp in progress.ArmorProgress)
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
                        }
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(ARMOR_PROGRESS_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Saved armor progress for {ArmorProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist armor progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Load armor progress from world save data.
        /// </summary>
        private void LoadArmorProgress()
        {
            if (ServerApi == null) return;

            ArmorProgress.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(ARMOR_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No armor progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        // Check magic bytes
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x49 || b3 != 0x41) // "SIA"
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid armor progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            for (int i = 0; i < playerCount; i++)
                            {
                                string playerUid = reader.ReadString();
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

                                ArmorProgress[playerUid] = progress;
                            }
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SimpleImprovingTraits] Unknown armor save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded armor progress for {ArmorProgress.Count} players");
            }
            catch (Exception ex)
            {
                ArmorProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load armor progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist config to world save data.
        /// Version 8 adds armor configuration.
        /// </summary>
        private void PersistConfig()
        {
            if (ServerApi == null) return;

            try
            {
                byte[] data;
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)8); // Version 8: adds armor config
                        writer.Write(BaseBlocksPerIncrement);
                        writer.Write(IncrementStep);
                        writer.Write(MaxMiningSpeedPercent);
                        writer.Write(OreMultiplier);
                        // Melee config
                        writer.Write(BaseDamagePerIncrement);
                        writer.Write(MeleeIncrementStep);
                        writer.Write(MaxMeleeDamagePercent);
                        // Ranged config
                        writer.Write(BaseRangedDamagePerIncrement);
                        writer.Write(RangedIncrementStep);
                        writer.Write(MaxRangedDamagePercent);
                        writer.Write(MaxRangedAccuracyPercent);
                        writer.Write(MaxRangedDistancePercent);
                        // Walking config
                        writer.Write(BaseBlocksWalkedPerIncrement);
                        writer.Write(WalkingIncrementStep);
                        writer.Write(MaxWalkingSpeedPercent);
                        // Hunger config
                        writer.Write(BaseSecondsPerIncrement);
                        writer.Write(HungerIncrementStep);
                        writer.Write(MaxHungerReductionPercent);
                        // Armor config
                        writer.Write(BaseSecondsInArmorPerIncrement);
                        writer.Write(ArmorTimeIncrementStep);
                        writer.Write(BaseDamageBlockedPerIncrement);
                        writer.Write(ArmorDamageIncrementStep);
                        writer.Write(BaseRepairsPerIncrement);
                        writer.Write(ArmorRepairIncrementStep);
                        writer.Write(MaxArmorDurabilityPercent);
                        writer.Write(MaxArmorWalkSpeedPercent);
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Config saved (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist config: {ex.Message}");
            }
        }

        /// <summary>
        /// Load config from world save data.
        /// Supports versions 1-7 for backwards compatibility.
        /// </summary>
        private void LoadConfig()
        {
            if (ServerApi == null) return;

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CONFIG_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No config data found, using defaults");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte version = reader.ReadByte();

                        if (version <= 2)
                        {
                            // Legacy format: just had BaseBlocksPerLevel (now BaseBlocksPerIncrement)
                            int legacyBase = reader.ReadInt32();
                            BaseBlocksPerIncrement = legacyBase;
                            IncrementStep = legacyBase; // Match old behavior

                            if (version >= 2)
                            {
                                MaxMiningSpeedPercent = reader.ReadInt32();
                            }
                            // OreMultiplier uses default (5)
                            // Melee, Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 3)
                        {
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            // Melee, Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 4)
                        {
                            // Version 4: has melee config but not ranged, walking, or hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            // Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 5)
                        {
                            // Version 5: has ranged config but not walking or hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            // Walking and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 6)
                        {
                            // Version 6: has walking config but not hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            // Hunger uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 7)
                        {
                            // Version 7: has hunger config but not armor
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            // Armor uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 8)
                        {
                            // Current format with armor config
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            BaseSecondsInArmorPerIncrement = reader.ReadInt32();
                            ArmorTimeIncrementStep = reader.ReadInt32();
                            BaseDamageBlockedPerIncrement = reader.ReadInt32();
                            ArmorDamageIncrementStep = reader.ReadInt32();
                            BaseRepairsPerIncrement = reader.ReadInt32();
                            ArmorRepairIncrementStep = reader.ReadInt32();
                            MaxArmorDurabilityPercent = reader.ReadInt32();
                            MaxArmorWalkSpeedPercent = reader.ReadInt32();
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load config: {ex.Message}");
            }
        }

        // =========================================================================
        // CLOTHIER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait clothier command.
        /// </summary>
        private TextCommandResult OnTraitClothierCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());
            int uniqueCount = progress.UniqueClothesWorn.Count;
            bool unlocked = progress.SewingKitUnlocked;

            var sb = new StringBuilder();
            sb.AppendLine($"Clothier progression: {uniqueCount} / {ClothierRequiredUniqueClothes} unique clothes worn");
            if (unlocked)
            {
                sb.AppendLine("Status: Sewing kit crafting UNLOCKED!");
            }
            else
            {
                sb.AppendLine($"Status: Wear {ClothierRequiredUniqueClothes - uniqueCount} more unique clothes to unlock sewing kit");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait clothierrequired command.
        /// </summary>
        private TextCommandResult OnTraitClothierRequiredCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Required clothes must be at least 1.");
                ClothierRequiredUniqueClothes = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Clothier required unique clothes set to {ClothierRequiredUniqueClothes}.");
            }

            return TextCommandResult.Success($"Current clothier required: {ClothierRequiredUniqueClothes} unique clothes.");
        }

        /// <summary>
        /// Handler for /trait clothierlevel command.
        /// Sets the player's clothier progress (unique clothes count).
        /// </summary>
        private TextCommandResult OnTraitClothierLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0)
                return TextCommandResult.Error("Level must be 0 or greater.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());

            // Clear the existing clothes set
            progress.UniqueClothesWorn.Clear();

            // Add placeholder entries up to the desired level
            for (int i = 0; i < newLevel; i++)
            {
                progress.UniqueClothesWorn.Add($"__placeholder_cloth_{i}");
            }

            // Set unlock status based on whether we've reached the required amount
            progress.SewingKitUnlocked = newLevel >= ClothierRequiredUniqueClothes;

            pendingClothierProgressSave = true;

            // Apply the bonus (this updates WatchedAttributes and extraTraits)
            ApplyClothierBonusStatic(player, progress);

            string status = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - newLevel} more needed to unlock.";
            return TextCommandResult.Success($"Clothier level set to {newLevel}/{ClothierRequiredUniqueClothes}. {status}");
        }

        /// <summary>
        /// Tick handler for clothing tracking.
        /// </summary>
        private void OnClothingTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var clothierProgress = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());

                // Skip if already unlocked
                if (clothierProgress.SewingKitUnlocked) continue;

                // Get the player's currently equipped clothing using character inventory
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory != null)
                {
                    foreach (var slot in characterInventory)
                    {
                        if (slot?.Itemstack?.Collectible != null)
                        {
                            string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                            if (IsClothingItem(itemCode))
                            {
                                if (clothierProgress.UniqueClothesWorn.Add(itemCode))
                                {
                                    // New unique clothing worn
                                    pendingClothierProgressSave = true;
                                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} wore new clothing: {itemCode} ({clothierProgress.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes})");

                                    // Check if unlocked
                                    if (clothierProgress.UniqueClothesWorn.Count >= ClothierRequiredUniqueClothes && !clothierProgress.SewingKitUnlocked)
                                    {
                                        clothierProgress.SewingKitUnlocked = true;
                                        ApplyClothierBonusStatic(player, clothierProgress);
                                        player.SendMessage(GlobalConstants.GeneralChatGroup,
                                            Lang.Get("simpleimprovingtraits:message-clothier-unlocked"),
                                            EnumChatType.Notification);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if an item code represents clothing (not armor).
        /// </summary>
        private static bool IsClothingItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();

            // Clothing items include clothes, not armor
            if (lowerCode.Contains("clothes-")) return true;
            if (lowerCode.Contains("shirt-")) return true;
            if (lowerCode.Contains("trousers-")) return true;
            if (lowerCode.Contains("dress-")) return true;
            if (lowerCode.Contains("hat-")) return true;
            if (lowerCode.Contains("cape-")) return true;
            if (lowerCode.Contains("cloak-")) return true;
            if (lowerCode.Contains("jacket-")) return true;
            if (lowerCode.Contains("vest-")) return true;
            if (lowerCode.Contains("skirt-")) return true;
            if (lowerCode.Contains("gloves-")) return true;
            if (lowerCode.Contains("boots-")) return true;
            if (lowerCode.Contains("shoes-")) return true;
            if (lowerCode.Contains("headband-")) return true;
            if (lowerCode.Contains("mask-")) return true;
            if (lowerCode.Contains("scarf-")) return true;

            return false;
        }

        /// <summary>
        /// Apply clothier bonus (update WatchedAttributes for client sync).
        /// Also adds "clothier" to extraTraits to unlock sewing kit recipes.
        /// </summary>
        private static void ApplyClothierBonusStatic(IServerPlayer player, ClothierProgressData progress)
        {
            if (player?.Entity == null) return;

            player.Entity.WatchedAttributes.SetInt(WATCHED_CLOTHIER_COUNT, progress.UniqueClothesWorn.Count);
            player.Entity.WatchedAttributes.SetBool(WATCHED_CLOTHIER_UNLOCKED, progress.SewingKitUnlocked);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CLOTHIER_COUNT);

            // Update extraTraits to show Clothier trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, CLOTHIER_TRAIT_CODE, progress.SewingKitUnlocked);

            // IMPORTANT: Add "clothier" to extraTraits to unlock sewing kit recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the sewing kit requires "clothier")
            UpdateExtraTraitStatic(player.Entity, "clothier", progress.SewingKitUnlocked);
        }

        /// <summary>
        /// Tick handler for Mender repair detection.
        /// Uses two detection methods:
        /// 1. Tracks sewing kit consumption from mouse cursor (most reliable)
        /// 2. Tracks durability increases on wearable items (backup method)
        /// </summary>
        private void OnMenderRepairTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;

                // =============================================
                // METHOD 1: Track sewing kit consumption from mouse cursor
                // When player holds sewing kits and clicks on clothing, the count decreases
                // =============================================
                var mouseSlot = player.InventoryManager?.MouseItemSlot;
                if (mouseSlot?.Itemstack?.Collectible != null)
                {
                    string mouseItemCode = mouseSlot.Itemstack.Collectible.Code?.ToString()?.ToLowerInvariant() ?? "";

                    if (mouseItemCode.Contains("sewingkit"))
                    {
                        int currentCount = mouseSlot.Itemstack.StackSize;

                        if (TrackedSewingKitCounts.TryGetValue(playerUid, out int previousCount))
                        {
                            if (currentCount < previousCount)
                            {
                                // Sewing kit was consumed - repair happened!
                                int kitsUsed = previousCount - currentCount;
                                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} used {kitsUsed} sewing kit(s) for repair");

                                for (int i = 0; i < kitsUsed; i++)
                                {
                                    ProcessMenderRepair(player);
                                }
                            }
                        }

                        // Update tracked count
                        TrackedSewingKitCounts[playerUid] = currentCount;
                    }
                    else
                    {
                        // Not holding sewing kit anymore, clear tracking
                        TrackedSewingKitCounts.TryRemove(playerUid, out _);
                    }
                }
                else
                {
                    // Mouse slot empty, clear tracking
                    TrackedSewingKitCounts.TryRemove(playerUid, out _);
                }

                // =============================================
                // METHOD 2: Track durability increases on wearable items (backup)
                // =============================================
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory == null) continue;

                int slotIndex = 0;
                foreach (var slot in characterInventory)
                {
                    slotIndex++;
                    if (slot?.Itemstack?.Collectible == null) continue;

                    string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                    if (string.IsNullOrEmpty(itemCode)) continue;

                    // Only track clothing and armor
                    if (!IsClothingItem(itemCode) && !IsArmorItem(itemCode)) continue;

                    // Get current durability
                    int currentDurability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                    // Skip items without durability
                    if (maxDurability <= 0) continue;

                    // Create a tracking key for this item in this slot
                    string trackingKey = $"{playerUid}_{slotIndex}_{itemCode}";

                    // Check if durability increased (repair happened)
                    if (TrackedItemDurabilities.TryGetValue(trackingKey, out int previousDurability))
                    {
                        if (currentDurability > previousDurability)
                        {
                            // Durability increased - a repair happened!
                            int durabilityRestored = currentDurability - previousDurability;
                            int repairPercent = (durabilityRestored * 100) / maxDurability;

                            // Only credit significant repairs (at least 5% durability restored)
                            // This filters out minor fluctuations and avoids double-counting with method 1
                            // Use a higher threshold since method 1 should catch most sewing kit repairs
                            if (repairPercent >= 10)
                            {
                                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} repaired {itemCode} (+{repairPercent}% durability) via durability tracking");
                                ProcessMenderRepair(player);
                            }
                        }
                    }

                    // Update tracked durability
                    TrackedItemDurabilities[trackingKey] = currentDurability;
                }
            }
        }

        /// <summary>
        /// Check if an item code represents armor.
        /// </summary>
        private static bool IsArmorItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();
            return lowerCode.Contains("armor-");
        }

        // =========================================================================
        // FURTIVE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Called every 500ms to track sneaking distance for all online players.
        /// Calculates 2D horizontal distance moved while sneaking (ignoring Y-axis).
        /// </summary>
        private void OnSneakingTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;

                // Check if player is sneaking
                bool isSneaking = player.Entity.Controls?.Sneak ?? false;

                if (!isSneaking)
                {
                    // Not sneaking, clear last position so movement doesn't count
                    lastSneakingPositions.TryRemove(playerUid, out _);
                    continue;
                }

                Vec3d currentPos = player.Entity.Pos.XYZ;

                // Get or initialize last sneaking position
                if (!lastSneakingPositions.TryGetValue(playerUid, out Vec3d lastPos))
                {
                    lastSneakingPositions[playerUid] = currentPos.Clone();
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentPos.X - lastPos.X;
                double dz = currentPos.Z - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position
                lastSneakingPositions[playerUid] = currentPos.Clone();

                // Skip if no movement or teleportation (too far)
                if (distance < 0.01f || distance > MAX_DISTANCE_PER_TICK) continue;

                // Get or create player progress data
                var playerProgress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
                {
                    CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
                });

                // Skip all processing if already at max
                if (playerProgress.TotalCredits >= MaxFurtivePercent) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Add distance to progress
                playerProgress.BlocksInIncrement += distance;

                // Check if we've earned any new credits
                while (playerProgress.BlocksInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxFurtivePercent)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.BlocksInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += FurtiveIncrementStep;

                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned furtive credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingFurtiveProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    int actualBonusPercent = ApplyFurtiveBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with actual applied bonus
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("simpleimprovingtraits:message-furtive-level-up", playerProgress.TotalCredits, actualBonusPercent),
                        EnumChatType.Notification);
                }
            }
        }

        /// <summary>
        /// Apply the Furtive bonus to a player based on their earned credits.
        /// The bonus reduces animal detection range.
        /// </summary>
        private static int ApplyFurtiveBonusStatic(IServerPlayer player, int credits)
        {
            // Check if player has vanilla Furtive trait (Malefactor)
            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);

            // Calculate effective cap (vanilla trait already gives max, so no additional bonus possible)
            int effectiveMax = hasVanillaFurtive ? 0 : MaxFurtivePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent (reduction in detection range)
            int bonusPercent = effectiveCredits;

            // Apply the stat (negative value to reduce detection range)
            // The animalSeekingRange stat is a multiplier, so 0.65 means 65% of original range (-35%)
            if (bonusPercent > 0)
            {
                float statValue = 1f - (bonusPercent / 100f);
                player.Entity.Stats.Set("animalSeekingRange", FURTIVE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove(FURTIVE_STAT_CODE, "animalSeekingRange");
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaFurtive", hasVanillaFurtive);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, FURTIVE_TRAIT_CODE, credits > 0 && !hasVanillaFurtive);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Furtive trait (Malefactor).
        /// </summary>
        private static bool PlayerHasVanillaFurtiveStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            // Check if player class is Malefactor
            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "malefactor";
            }

            return false;
        }

        // =========================================================================
        // PRECISE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Check if an entity is a mechanical creature (e.g., locust, bell, etc.).
        /// </summary>
        public static bool IsMechanicalCreature(Entity entity)
        {
            if (entity == null) return false;

            string entityCode = entity.Code?.ToString()?.ToLowerInvariant() ?? "";

            // Check for known mechanical creatures
            // Locusts are the main mechanical enemies in Vintage Story
            if (entityCode.Contains("locust")) return true;
            if (entityCode.Contains("bell")) return true;
            if (entityCode.Contains("mechanical")) return true;
            if (entityCode.Contains("automaton")) return true;
            if (entityCode.Contains("construct")) return true;

            // Also check the entity class
            string entityClass = entity.GetType().Name.ToLowerInvariant();
            if (entityClass.Contains("locust")) return true;

            return false;
        }

        /// <summary>
        /// Process damage dealt to a mechanical creature by a player.
        /// Adds progress toward the Precise trait.
        /// </summary>
        public static void ProcessPreciseDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || damage <= 0) return;
            if (string.IsNullOrEmpty(weaponType)) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            // Check if already at max
            int effectiveMax = GetPreciseEffectiveMax(attackerPlayer.Entity);
            if (playerProgress.TotalCredits >= effectiveMax) return;

            int oldCredits = playerProgress.TotalCredits;

            // Get or create weapon progress
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            // Add damage to progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < effectiveMax)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += PreciseIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {attackerPlayer.PlayerName} earned precise credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            // Mark for saving if any progress was made
            if (damage > 0)
            {
                pendingPreciseProgressSave = true;
            }

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                int actualBonusPercent = ApplyPreciseBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-precise-level-up", playerProgress.TotalCredits, actualBonusPercent),
                    EnumChatType.Notification);

                // Check if Tinkerer should be unlocked
                CheckTinkererUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Get the effective maximum for Precise based on player class.
        /// Clockmaker has vanilla +25%, so they can only earn 5 more levels.
        /// </summary>
        private static int GetPreciseEffectiveMax(EntityPlayer entity)
        {
            if (PlayerHasVanillaPreciseStatic(entity))
            {
                // Clockmaker already has +25%, cap at +5 more to reach 30% total
                return MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS;
            }
            return MaxPrecisePercent;
        }

        /// <summary>
        /// Apply the Precise bonus to a player based on their earned credits.
        /// The bonus increases damage to mechanical creatures.
        /// </summary>
        private static int ApplyPreciseBonusStatic(IServerPlayer player, int credits)
        {
            // Check if player has vanilla Precise trait (Clockmaker)
            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);

            // Calculate effective cap
            int effectiveMax = hasVanillaPrecise ? (MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS) : MaxPrecisePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent
            int bonusPercent = effectiveCredits;

            // Apply the stat (mechanical damage is typically via mechanicalsDamage stat)
            if (bonusPercent > 0)
            {
                float statValue = 1f + (bonusPercent / 100f);
                player.Entity.Stats.Set("mechanicalsDamage", PRECISE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove(PRECISE_STAT_CODE, "mechanicalsDamage");
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPrecise", hasVanillaPrecise);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, PRECISE_TRAIT_CODE, credits > 0 && !hasVanillaPrecise);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Precise trait (Clockmaker).
        /// </summary>
        private static bool PlayerHasVanillaPreciseStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            // Check if player class is Clockmaker
            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "clockmaker";
            }

            return false;
        }

        // =========================================================================
        // UNLOCK CHECKING METHODS
        // =========================================================================

        /// <summary>
        /// Check and apply Hardy health unlock if thresholds are met.
        /// Requires 110% mining speed and 10% armor durability.
        /// </summary>
        private static void CheckHardyHealthUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < HardyHealthMiningThreshold) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < HardyHealthArmorDurabilityThreshold) return;

            // Both thresholds met - unlock Hardy health!
            progress.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;

            // Apply the health bonus
            ApplyHardyHealthBonusStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-hardy-health-unlock", HardyHealthBonus),
                EnumChatType.Notification);
        }

        /// <summary>
        /// Apply Hardy health bonus (+5 HP).
        /// </summary>
        private static void ApplyHardyHealthBonusStatic(IServerPlayer player, bool unlocked)
        {
            if (unlocked)
            {
                player.Entity.Stats.Set("maxhealthExtraPoints", HARDY_HEALTH_STAT_CODE, HardyHealthBonus, false);
            }
            else
            {
                player.Entity.Stats.Remove(HARDY_HEALTH_STAT_CODE, "maxhealthExtraPoints");
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_HARDY_HEALTH_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, HARDY_HEALTH_TRAIT_CODE, unlocked);
        }

        /// <summary>
        /// Check and apply Tinkerer unlock if thresholds are met.
        /// Requires Technical trait AND 10% Precise damage bonus.
        /// </summary>
        private static void CheckTinkererUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check Technical trait
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (!technicalProgress.IsUnlocked) return;

            // Check Precise threshold
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            if (preciseProgress.TotalCredits < TinkererPreciseThreshold) return;

            // Both conditions met - unlock Tinkerer!
            progress.IsUnlocked = true;
            pendingTinkererProgressSave = true;

            // Apply the trait
            ApplyTinkererBonusStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-tinkerer-unlock"),
                EnumChatType.Notification);
        }

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
            float gearCostReduction = unlocked ? -1f : 0f;
            player.Entity.Stats.Set("temporalGearTLRepairCost", TECHNICAL_STAT_CODE, gearCostReduction, false);
        }

        /// <summary>
        /// Apply Tinkerer trait (unlocks tuning spear crafting).
        /// Also adds "tinkerer" to extraTraits to unlock tuning spear recipes.
        /// </summary>
        private static void ApplyTinkererBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_TINKERER_UNLOCKED, unlocked);

            // Update extraTraits to show Tinkerer trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, TINKERER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "tinkerer" to extraTraits to unlock tuning spear recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the tuning spear requires "tinkerer")
            UpdateExtraTraitStatic(player.Entity, "tinkerer", unlocked);
        }

        /// <summary>
        /// Check and apply Merciless unlock if thresholds are met.
        /// Requires 10% armor durability AND 15% melee damage.
        /// </summary>
        private static void CheckMercilessUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < MercilessArmorDurabilityThreshold) return;

            // Check melee damage threshold
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            if (meleeProgress.TotalCredits < MercilessMeleeDamageThreshold) return;

            // Both thresholds met - unlock Merciless!
            progress.IsUnlocked = true;
            pendingMercilessProgressSave = true;

            // Apply the trait
            ApplyMercilessBonusStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-merciless-unlock"),
                EnumChatType.Notification);
        }

        /// <summary>
        /// Apply Merciless trait (unlocks shortsword/shield crafting).
        /// </summary>
        private static void ApplyMercilessBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_MERCILESS_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, MERCILESS_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "merciless" to extraTraits to unlock shortsword/shield recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., shortsword/shield require "merciless")
            UpdateExtraTraitStatic(player.Entity, "merciless", unlocked);
        }

        /// <summary>
        /// Check and apply Bowyer unlock if thresholds are met.
        /// Requires 10% ranged damage AND 300 damage with simple bow/longbow.
        /// </summary>
        private static void CheckBowyerUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check ranged damage threshold
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            if (rangedProgress.TotalCredits < BowyerRangedDamageThreshold) return;

            // Check bow damage threshold
            if (progress.TotalBowDamage < BowyerBowDamageThreshold) return;

            // Both thresholds met - unlock Bowyer!
            progress.IsUnlocked = true;
            pendingBowyerProgressSave = true;

            // Apply the trait
            ApplyBowyerBonusStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-bowyer-unlock"),
                EnumChatType.Notification);
        }

        /// <summary>
        /// Apply Bowyer trait (unlocks crude bow/arrows crafting).
        /// </summary>
        private static void ApplyBowyerBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_BOWYER_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, BOWYER_TRAIT_CODE, unlocked);
        }

        /// <summary>
        /// Check and apply Improviser unlock if threshold is met.
        /// Requires 300 damage with thrown rocks.
        /// </summary>
        private static void CheckImproviserUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check rock damage threshold
            if (progress.TotalRockDamage < ImproviserRockDamageThreshold) return;

            // Threshold met - unlock Improviser!
            progress.IsUnlocked = true;
            pendingImproviserProgressSave = true;

            // Apply the trait
            ApplyImproviserBonusStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-improviser-unlock"),
                EnumChatType.Notification);
        }

        /// <summary>
        /// Apply Improviser trait (unlocks sling crafting).
        /// </summary>
        private static void ApplyImproviserBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_IMPROVISER_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, IMPROVISER_TRAIT_CODE, unlocked);
        }

        /// <summary>
        /// Check and apply Claustrophobic removal if threshold is met (Hunter only).
        /// Requires 100% mining speed.
        /// </summary>
        private static void CheckClaustrophobicRemoval(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Only applies to Hunter class
            if (!PlayerIsHunterStatic(player.Entity)) return;

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());

            // Already removed
            if (progress.IsRemoved) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < ClaustrophobicRemovalMiningThreshold) return;

            // Threshold met - remove Claustrophobic!
            progress.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;

            // Apply the removal (negate the Claustrophobic penalties)
            ApplyClaustrophobicRemovalStatic(player, true);

            // Notify player
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("simpleimprovingtraits:message-claustrophobic-removed"),
                EnumChatType.Notification);
        }

        /// <summary>
        /// Apply Claustrophobic removal (negates ore drop and mining speed penalties).
        /// </summary>
        private static void ApplyClaustrophobicRemovalStatic(IServerPlayer player, bool removed)
        {
            if (removed)
            {
                // Negate Claustrophobic penalties: -15% ore drop, -10% mining speed
                // By adding positive stats to counteract them
                player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicRemoval", 1.15f, false); // +15% to negate -15%
                player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicRemoval", 1.10f, false); // +10% to negate -10%
            }
            else
            {
                player.Entity.Stats.Remove("sitClaustrophobicRemoval", "oreDropRate");
                player.Entity.Stats.Remove("sitClaustrophobicRemoval", "miningSpeedMul");
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_CLAUSTROPHOBIC_REMOVED, removed);
            UpdateExtraTraitStatic(player.Entity, CLAUSTROPHOBIC_REMOVED_TRAIT_CODE, removed);
        }

        /// <summary>
        /// Check if player is the Hunter class.
        /// </summary>
        private static bool PlayerIsHunterStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "hunter";
            }

            return false;
        }

        // =========================================================================
        // MENDER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait mender command.
        /// </summary>
        private TextCommandResult OnTraitMenderCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());
            int bonusPercent = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mender progression: Level {progress.TotalCredits} / {MaxMenderPercent}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% armor/clothing durability");
            if (hasVanillaMender)
            {
                sb.AppendLine($"Combined with Mender trait: +{VANILLA_MENDER_ARMOR_DURABILITY_BONUS + bonusPercent}% total");
            }
            if (progress.TotalCredits < MaxMenderPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.RepairsInIncrement;
                sb.AppendLine($"Progress: {progress.RepairsInIncrement} / {progress.CurrentIncrementSize} repairs until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait menderbase command.
        /// </summary>
        private TextCommandResult OnTraitMenderBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base repairs must be at least 1.");
                BaseMenderRepairsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Mender base repairs set to {BaseMenderRepairsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current mender base repairs: {BaseMenderRepairsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait menderlevel command.
        /// </summary>
        private TextCommandResult OnTraitMenderLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxMenderPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxMenderPercent}.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());
            progress.TotalCredits = newLevel;
            progress.RepairsInIncrement = 0;
            progress.CurrentIncrementSize = BaseMenderRepairsPerIncrement;

            // Recalculate increment size for this level
            for (int i = 0; i < newLevel; i++)
            {
                progress.CurrentIncrementSize += MenderIncrementStep;
            }

            pendingMenderProgressSave = true;

            int bonusPercent = ApplyMenderBonusStatic(player, progress.TotalCredits);
            return TextCommandResult.Success($"Mender level set to {newLevel} (+{bonusPercent}% durability).");
        }

        /// <summary>
        /// Handler for /trait mendermax command.
        /// </summary>
        private TextCommandResult OnTraitMenderMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxMenderPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Mender max bonus set to {MaxMenderPercent}%.");
            }

            return TextCommandResult.Success($"Current mender max bonus: {MaxMenderPercent}%.");
        }

        /// <summary>
        /// Calculate the mender durability bonus as an integer percentage.
        /// </summary>
        public static int CalculateMenderBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaMender = entity != null && PlayerHasVanillaMenderStatic(entity);
            int vanillaBonus = hasVanillaMender ? VANILLA_MENDER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxMenderPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Mender trait.
        /// </summary>
        private static bool PlayerHasVanillaMenderStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("mender", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Apply mender bonus.
        /// </summary>
        private static int ApplyMenderBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);
            int bonusPercent = CalculateMenderBonusPercent(level, player.Entity);
            float bonus = bonusPercent * 0.01f;

            // Apply to armor durability loss stat (reduces durability damage taken)
            player.Entity.Stats.Set("armorDurabilityLoss", MENDER_STAT_CODE, 1f - bonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaMender", hasVanillaMender);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MENDER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, MENDER_TRAIT_CODE, level > 0 && !hasVanillaMender);

            return bonusPercent;
        }

        /// <summary>
        /// Process a sewing kit repair (called externally or via Harmony patch).
        /// </summary>
        public static void ProcessMenderRepair(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());

            // Skip if at max
            if (progress.TotalCredits >= MaxMenderPercent) return;

            int oldCredits = progress.TotalCredits;
            progress.RepairsInIncrement++;

            // Check if we've earned a credit
            while (progress.RepairsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxMenderPercent)
            {
                progress.TotalCredits++;
                progress.RepairsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += MenderIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned mender credit {progress.TotalCredits}");
            }

            pendingMenderProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                int bonusPercent = ApplyMenderBonusStatic(player, progress.TotalCredits);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-mender-level-up", progress.TotalCredits, bonusPercent),
                    EnumChatType.Notification);
            }
        }

        // =========================================================================
        // PILFERER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait pilferer command.
        /// </summary>
        private TextCommandResult OnTraitPilfererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());
            int bonusPercent = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Pilferer progression: Level {progress.TotalCredits} / {MaxPilfererPercent}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% rusty gear, vessel contents, and collection chance");
            if (hasVanillaPilferer)
            {
                sb.AppendLine($"(Has vanilla Pilferer trait)");
            }
            sb.AppendLine($"Chests opened: {progress.OpenedChestPositions.Count}");
            if (progress.TotalCredits < MaxPilfererPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.PointsInIncrement;
                sb.AppendLine($"Progress: {progress.PointsInIncrement} / {progress.CurrentIncrementSize} points until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait pilfererbase command.
        /// </summary>
        private TextCommandResult OnTraitPilfererBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base points must be at least 1.");
                BasePilfererPointsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Pilferer base points set to {BasePilfererPointsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current pilferer base points: {BasePilfererPointsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait pilfererlevel command.
        /// </summary>
        private TextCommandResult OnTraitPilfererLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxPilfererPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxPilfererPercent}.");

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());
            progress.TotalCredits = newLevel;
            progress.PointsInIncrement = 0;
            progress.CurrentIncrementSize = BasePilfererPointsPerIncrement;

            for (int i = 0; i < newLevel; i++)
            {
                progress.CurrentIncrementSize += PilfererIncrementStep;
            }

            pendingPilfererProgressSave = true;

            int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);
            return TextCommandResult.Success($"Pilferer level set to {newLevel} (+{bonusPercent}% bonuses).");
        }

        /// <summary>
        /// Handler for /trait pilferermax command.
        /// </summary>
        private TextCommandResult OnTraitPilfererMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxPilfererPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Pilferer max bonus set to {MaxPilfererPercent}%.");
            }

            return TextCommandResult.Success($"Current pilferer max bonus: {MaxPilfererPercent}%.");
        }

        /// <summary>
        /// Calculate the pilferer bonus as an integer percentage.
        /// </summary>
        public static int CalculatePilfererBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaPilferer = entity != null && PlayerHasVanillaPilfererStatic(entity);
            int vanillaBonus = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxPilfererPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Pilferer trait.
        /// </summary>
        private static bool PlayerHasVanillaPilfererStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("pilferer", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Apply pilferer bonus.
        /// </summary>
        private static int ApplyPilfererBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            int bonusPercent = CalculatePilfererBonusPercent(level, player.Entity);
            float bonus = bonusPercent * 0.01f;

            // Apply to pilferer-related stats
            player.Entity.Stats.Set("rustyGearDropRate", PILFERER_RUSTY_GEAR_STAT_CODE, 1f + bonus, false);
            player.Entity.Stats.Set("vesselContentsDropRate", PILFERER_VESSEL_CONTENTS_STAT_CODE, 1f + bonus, false);
            player.Entity.Stats.Set("wholeVesselLootChance", PILFERER_WHOLE_VESSEL_STAT_CODE, bonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPilferer", hasVanillaPilferer);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_PILFERER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, PILFERER_TRAIT_CODE, level > 0 && !hasVanillaPilferer);

            return bonusPercent;
        }

        /// <summary>
        /// Process vessel break (called from OnBlockBroken for vessels).
        /// </summary>
        public static void ProcessVesselBreak(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());

            if (progress.TotalCredits >= MaxPilfererPercent) return;

            int oldCredits = progress.TotalCredits;
            progress.PointsInIncrement += PILFERER_VESSEL_POINTS;

            while (progress.PointsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxPilfererPercent)
            {
                progress.TotalCredits++;
                progress.PointsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += PilfererIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned pilferer credit {progress.TotalCredits} from vessel");
            }

            pendingPilfererProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-pilferer-level-up", progress.TotalCredits, bonusPercent),
                    EnumChatType.Notification);
            }
        }

        /// <summary>
        /// Process chest opening (called when player opens a chest for the first time).
        /// </summary>
        public static void ProcessChestOpening(IServerPlayer player, BlockPos pos)
        {
            if (player?.Entity == null || pos == null) return;

            string playerUid = player.PlayerUID;
            var progress = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());

            if (progress.TotalCredits >= MaxPilfererPercent) return;

            // Create a unique key for this chest position
            string posKey = $"{pos.X},{pos.Y},{pos.Z}";
            if (!progress.OpenedChestPositions.Add(posKey)) return; // Already opened this chest

            int oldCredits = progress.TotalCredits;
            progress.PointsInIncrement += PILFERER_CHEST_POINTS;

            while (progress.PointsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxPilfererPercent)
            {
                progress.TotalCredits++;
                progress.PointsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += PilfererIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned pilferer credit {progress.TotalCredits} from chest");
            }

            pendingPilfererProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-pilferer-level-up", progress.TotalCredits, bonusPercent),
                    EnumChatType.Notification);
            }
        }

        // =========================================================================
        // RESOURCEFUL TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait resourceful command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());
            int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
            int speedBonus = CalculateResourcefulSpeedBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Resourceful progression: Level {progress.TotalCredits} / {MaxResourcefulLootPercent}");
            sb.AppendLine($"Current bonus: +{lootBonus}% animal loot, +{speedBonus}% harvesting speed");
            if (hasVanillaResourceful)
            {
                sb.AppendLine($"(Has vanilla Resourceful trait)");
            }
            if (progress.TotalCredits < MaxResourcefulLootPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.AnimalsInIncrement;
                sb.AppendLine($"Progress: {progress.AnimalsInIncrement} / {progress.CurrentIncrementSize} animals until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait resourcefulbase command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base animals must be at least 1.");
                BaseResourcefulAnimalsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Resourceful base animals set to {BaseResourcefulAnimalsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current resourceful base animals: {BaseResourcefulAnimalsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait resourcefullevel command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxResourcefulLootPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxResourcefulLootPercent}.");

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());
            progress.TotalCredits = newLevel;
            progress.AnimalsInIncrement = 0;
            progress.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;

            for (int i = 0; i < newLevel; i++)
            {
                progress.CurrentIncrementSize += ResourcefulIncrementStep;
            }

            pendingResourcefulProgressSave = true;

            ApplyResourcefulBonusStatic(player, progress.TotalCredits);
            int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
            return TextCommandResult.Success($"Resourceful level set to {newLevel} (+{lootBonus}% loot).");
        }

        /// <summary>
        /// Handler for /trait resourcefulmax command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxResourcefulLootPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Resourceful max loot bonus set to {MaxResourcefulLootPercent}%.");
            }

            return TextCommandResult.Success($"Current resourceful max loot bonus: {MaxResourcefulLootPercent}%.");
        }

        /// <summary>
        /// Calculate the resourceful loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateResourcefulLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaResourceful = entity != null && PlayerHasVanillaResourcefulStatic(entity);
            int vanillaBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxResourcefulLootPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the resourceful speed bonus as an integer percentage.
        /// Speed bonus scales indefinitely with level (1% per credit), no cap.
        /// </summary>
        public static int CalculateResourcefulSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            // Speed bonus scales indefinitely - 1% per credit, no cap
            return credits;
        }

        /// <summary>
        /// Check if player has vanilla Resourceful trait.
        /// </summary>
        private static bool PlayerHasVanillaResourcefulStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("resourceful", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Apply resourceful bonus.
        /// </summary>
        private static void ApplyResourcefulBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            int lootBonusPercent = CalculateResourcefulLootBonusPercent(level, player.Entity);
            int speedBonusPercent = CalculateResourcefulSpeedBonusPercent(level, player.Entity);

            float lootBonus = lootBonusPercent * 0.01f;
            float speedBonus = speedBonusPercent * 0.01f;

            // Apply to resourceful-related stats
            // animalLootDropRate is additive (1.0 + bonus means +X% more loot)
            player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, 1f + lootBonus, false);
            // harvestingSpeedMul is multiplicative (1.25 = 25% faster harvesting)
            player.Entity.Stats.Set("harvestingSpeedMul", RESOURCEFUL_SPEED_STAT_CODE, 1f + speedBonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LOOT_BONUS, lootBonusPercent);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_SPEED_BONUS, speedBonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaResourceful", hasVanillaResourceful);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_RESOURCEFUL_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, RESOURCEFUL_TRAIT_CODE, level > 0 && !hasVanillaResourceful);
        }

        /// <summary>
        /// Process animal harvested (called from Harmony patch when player harvests an animal).
        /// </summary>
        public static void ProcessAnimalHarvested(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());

            if (progress.TotalCredits >= MaxResourcefulLootPercent) return;

            int oldCredits = progress.TotalCredits;
            progress.AnimalsInIncrement++;

            while (progress.AnimalsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxResourcefulLootPercent)
            {
                progress.TotalCredits++;
                progress.AnimalsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ResourcefulIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned resourceful credit {progress.TotalCredits}");
            }

            pendingResourcefulProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyResourcefulBonusStatic(player, progress.TotalCredits);
                int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-resourceful-level-up", progress.TotalCredits, lootBonus),
                    EnumChatType.Notification);
            }
        }

        // =========================================================================
        // FORAGER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait forager command.
        /// </summary>
        private TextCommandResult OnTraitForagerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());
            int lootBonus = CalculateForagerLootBonusPercent(progress.TotalCredits, player.Entity);
            int wildCropBonus = CalculateForagerWildCropBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Forager progression: Level {progress.TotalCredits} / {MaxForagerLootPercent}");
            sb.AppendLine($"Current bonus: +{lootBonus}% foraging loot, +{wildCropBonus}% wild crop drops");
            if (hasVanillaForager)
            {
                sb.AppendLine($"(Has vanilla Forager trait)");
            }
            if (progress.TotalCredits < MaxForagerLootPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.CropsInIncrement;
                sb.AppendLine($"Progress: {progress.CropsInIncrement} / {progress.CurrentIncrementSize} crops until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait foragerbase command.
        /// </summary>
        private TextCommandResult OnTraitForagerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base crops must be at least 1.");
                BaseForagerCropsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager base crops set to {BaseForagerCropsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current forager base crops: {BaseForagerCropsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait foragerlevel command.
        /// </summary>
        private TextCommandResult OnTraitForagerLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxForagerLootPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxForagerLootPercent}.");

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());
            progress.TotalCredits = newLevel;
            progress.CropsInIncrement = 0;
            progress.CurrentIncrementSize = BaseForagerCropsPerIncrement;

            for (int i = 0; i < newLevel; i++)
            {
                progress.CurrentIncrementSize += ForagerIncrementStep;
            }

            pendingForagerProgressSave = true;

            ApplyForagerBonusStatic(player, progress.TotalCredits);
            int lootBonus = CalculateForagerLootBonusPercent(progress.TotalCredits, player.Entity);
            return TextCommandResult.Success($"Forager level set to {newLevel} (+{lootBonus}% loot).");
        }

        /// <summary>
        /// Handler for /trait foragermax command.
        /// </summary>
        private TextCommandResult OnTraitForagerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxForagerLootPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager max loot bonus set to {MaxForagerLootPercent}%.");
            }

            return TextCommandResult.Success($"Current forager max loot bonus: {MaxForagerLootPercent}%.");
        }

        /// <summary>
        /// Calculate the forager loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerLootPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the forager wild crop bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerWildCropBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_WILD_CROP_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerWildCropPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Forager trait.
        /// </summary>
        private static bool PlayerHasVanillaForagerStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("forager", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Apply forager bonus.
        /// </summary>
        private static void ApplyForagerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            int lootBonusPercent = CalculateForagerLootBonusPercent(level, player.Entity);
            int wildCropBonusPercent = CalculateForagerWildCropBonusPercent(level, player.Entity);

            float lootBonus = lootBonusPercent * 0.01f;
            float wildCropBonus = wildCropBonusPercent * 0.01f;

            // Apply to forager-related stats
            player.Entity.Stats.Set("forageDropRate", FORAGER_LOOT_STAT_CODE, 1f + lootBonus, false);
            player.Entity.Stats.Set("wildCropDropRate", FORAGER_WILD_CROP_STAT_CODE, 1f + wildCropBonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LOOT_BONUS, lootBonusPercent);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_WILD_CROP_BONUS, wildCropBonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaForager", hasVanillaForager);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_FORAGER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, FORAGER_TRAIT_CODE, level > 0 && !hasVanillaForager);
        }

        /// <summary>
        /// Process wild crop broken (for Forager progression).
        /// </summary>
        public static void ProcessWildCropBroken(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());

            if (progress.TotalCredits >= MaxForagerLootPercent) return;

            int oldCredits = progress.TotalCredits;
            progress.CropsInIncrement++;

            while (progress.CropsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxForagerLootPercent)
            {
                progress.TotalCredits++;
                progress.CropsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ForagerIncrementStep;

                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} earned forager credit {progress.TotalCredits}");
            }

            pendingForagerProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyForagerBonusStatic(player, progress.TotalCredits);
                int lootBonus = CalculateForagerLootBonusPercent(progress.TotalCredits, player.Entity);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-forager-level-up", progress.TotalCredits, lootBonus),
                    EnumChatType.Notification);
            }
        }

        /// <summary>
        /// Check if a block is a wild crop (for Forager progression).
        /// Wild crops are crops like turnip, flax, spelt that grow on dirt/soil (not farmland).
        /// Berry bushes are NOT counted since they can be replanted infinitely.
        /// </summary>
        private static bool IsWildCropBlock(int blockId, BlockPos blockPos)
        {
            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            // Check if it's a crop block (like crop-turnip-4, crop-flax-7, etc.)
            if (blockCode.Contains("crop-"))
            {
                // Skip if it's explicitly a "wild" block - those are already wild
                // Regular crops on farmland should NOT count
                // Wild crops spawn on dirt/soil naturally

                // Check if the block below is farmland - if so, this is a planted crop, not wild
                if (blockPos != null)
                {
                    BlockPos belowPos = blockPos.DownCopy();
                    Block blockBelow = ServerApi.World.BlockAccessor.GetBlock(belowPos);
                    string belowCode = blockBelow?.Code?.ToString()?.ToLowerInvariant() ?? "";

                    // If on farmland, this is a cultivated crop - don't count it
                    if (belowCode.Contains("farmland"))
                    {
                        return false;
                    }

                    // If on dirt, soil, grass, or other natural blocks - this is a wild crop
                    if (belowCode.Contains("soil") || belowCode.Contains("dirt") ||
                        belowCode.Contains("grass") || belowCode.Contains("forest") ||
                        belowCode.Contains("peat") || belowCode.Contains("sand") ||
                        belowCode.Contains("gravel") || belowCode.Contains("clay"))
                    {
                        return true;
                    }
                }

                // If position is null or block below couldn't be checked,
                // only count if explicitly marked as "wild"
                if (blockCode.Contains("wild"))
                {
                    return true;
                }

                return false;
            }

            // Forageable ground plants (NOT berry bushes - those are replantable)
            // Note: Tallgrass, flowers, mushrooms are one-time finds in the wild
            if (blockCode.Contains("tallgrass")) return true;
            if (blockCode.Contains("flower-")) return true;
            if (blockCode.Contains("mushroom-")) return true;
            if (blockCode.Contains("cattail")) return true;
            if (blockCode.Contains("fern")) return true;
            if (blockCode.Contains("reeds")) return true;
            if (blockCode.Contains("waterlily")) return true;
            if (blockCode.Contains("seaweed")) return true;

            // NOT included:
            // - berry- (berry bushes can be replanted)
            // - wildvine (can be replanted/grown)

            return false;
        }

        /// <summary>
        /// Check if a block is a vessel (for Pilferer progression).
        /// </summary>
        private static bool IsVesselBlock(int blockId)
        {
            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            if (blockCode.Contains("vessel-")) return true;
            if (blockCode.Contains("storagevessel")) return true;
            if (blockCode.Contains("crackedvessel")) return true;
            if (blockCode.Contains("urn-")) return true;

            return false;
        }

        // =========================================================================
        // NEW TRAIT COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait furtive command.
        /// </summary>
        private TextCommandResult OnTraitFurtiveCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
            int bonusPercent = hasVanillaFurtive ? 0 : progress.TotalCredits;

            var sb = new StringBuilder();
            sb.AppendLine($"Furtive progression: Level {progress.TotalCredits} / {MaxFurtivePercent}");
            sb.AppendLine($"Current bonus: -{bonusPercent}% animal detection range");
            if (hasVanillaFurtive)
            {
                sb.AppendLine($"Vanilla Furtive trait active: -{VANILLA_FURTIVE_DETECTION_REDUCTION}% detection (max reached)");
            }
            else if (progress.TotalCredits < MaxFurtivePercent)
            {
                float remaining = progress.CurrentIncrementSize - progress.BlocksInIncrement;
                sb.AppendLine($"Progress: {progress.BlocksInIncrement:F1} / {progress.CurrentIncrementSize} blocks sneaked");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait furtivelevel command.
        /// </summary>
        private TextCommandResult OnTraitFurtiveLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxFurtivePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxFurtivePercent}.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            progress.TotalCredits = newLevel;
            progress.BlocksInIncrement = 0;
            progress.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement + (newLevel * FurtiveIncrementStep);

            pendingFurtiveProgressSave = true;
            int bonusPercent = ApplyFurtiveBonusStatic(player, newLevel);

            return TextCommandResult.Success($"Furtive level set to {newLevel} (-{bonusPercent}% detection).");
        }

        /// <summary>
        /// Handler for /trait precise command.
        /// </summary>
        private TextCommandResult OnTraitPreciseCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
            int effectiveMax = GetPreciseEffectiveMax(player.Entity);
            int bonusPercent = Math.Min(progress.TotalCredits, effectiveMax);

            var sb = new StringBuilder();
            sb.AppendLine($"Precise progression: Level {progress.TotalCredits} / {effectiveMax}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% damage to mechanicals");
            if (hasVanillaPrecise)
            {
                int totalBonus = VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + bonusPercent;
                sb.AppendLine($"Combined with Clockmaker trait: +{totalBonus}% total");
            }
            if (progress.TotalCredits < effectiveMax)
            {
                sb.AppendLine($"Per-weapon progress:");
                foreach (var kvp in progress.WeaponProgress)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value.DamageInIncrement:F0} / {kvp.Value.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait preciselevel command.
        /// </summary>
        private TextCommandResult OnTraitPreciseLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int newLevel = (int)args[0];
            if (newLevel < 0 || newLevel > MaxPrecisePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxPrecisePercent}.");

            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            progress.TotalCredits = newLevel;
            progress.WeaponProgress.Clear();

            pendingPreciseProgressSave = true;
            int bonusPercent = ApplyPreciseBonusStatic(player, newLevel);

            // Check for trait unlocks that depend on precise level
            CheckTinkererUnlock(player);

            return TextCommandResult.Success($"Precise level set to {newLevel} (+{bonusPercent}% mechanical damage).");
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

            pendingTechnicalProgressSave = true;
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
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            // Increment translocator repairs
            progress.TranslocatorsRepaired++;
            pendingTechnicalProgressSave = true;

            ServerApi.Logger.Debug($"[SimpleImprovingTraits] Player {player.PlayerName} repaired translocator ({progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs})");

            // Check if we've reached the unlock threshold
            if (progress.TranslocatorsRepaired >= TechnicalRequiredTranslocatorRepairs)
            {
                progress.IsUnlocked = true;
                ApplyTechnicalBonusStatic(player, true);

                // Notify player
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-technical-unlock"),
                    EnumChatType.Notification);

                // Check if Tinkerer should now be unlocked
                CheckTinkererUnlock(player);
            }
        }

        /// <summary>
        /// Handler for /trait hardyhealth command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Hardy Health trait: {(progress.IsUnlocked ? $"UNLOCKED (+{HardyHealthBonus} HP)" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Mining level: {miningProgress.TotalCredits} / {HardyHealthMiningThreshold} ({(miningProgress.TotalCredits >= HardyHealthMiningThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {HardyHealthArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= HardyHealthArmorDurabilityThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait bowyer command.
        /// </summary>
        private TextCommandResult OnTraitBowyerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Bowyer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Ranged level: {rangedProgress.TotalCredits} / {BowyerRangedDamageThreshold} ({(rangedProgress.TotalCredits >= BowyerRangedDamageThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Bow damage: {progress.TotalBowDamage:F0} / {BowyerBowDamageThreshold} ({(progress.TotalBowDamage >= BowyerBowDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait improviser command.
        /// </summary>
        private TextCommandResult OnTraitImproviserCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Improviser trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Rock damage: {progress.TotalRockDamage:F0} / {ImproviserRockDamageThreshold} ({(progress.TotalRockDamage >= ImproviserRockDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait tinkerer command.
        /// </summary>
        private TextCommandResult OnTraitTinkererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Tinkerer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Technical trait: {(technicalProgress.IsUnlocked ? "UNLOCKED ✓" : "Locked ✗")}");
            sb.AppendLine($"  Precise level: {preciseProgress.TotalCredits} / {TinkererPreciseThreshold} ({(preciseProgress.TotalCredits >= TinkererPreciseThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait merciless command.
        /// </summary>
        private TextCommandResult OnTraitMercilessCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Merciless trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {MercilessArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= MercilessArmorDurabilityThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Melee level: {meleeProgress.TotalCredits} / {MercilessMeleeDamageThreshold} ({(meleeProgress.TotalCredits >= MercilessMeleeDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait claustrophobic command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Check if player is Hunter
            if (!PlayerIsHunterStatic(player.Entity))
            {
                return TextCommandResult.Success("Claustrophobic removal is only available for the Hunter class.");
            }

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Claustrophobic trait: {(progress.IsRemoved ? "REMOVED" : "Active")}");
            sb.AppendLine($"Mining level: {miningProgress.TotalCredits} / {ClaustrophobicRemovalMiningThreshold} ({(miningProgress.TotalCredits >= ClaustrophobicRemovalMiningThreshold ? "✓" : "✗")})");
            if (!progress.IsRemoved)
            {
                int remaining = ClaustrophobicRemovalMiningThreshold - miningProgress.TotalCredits;
                sb.AppendLine($"Reach {remaining}% more mining level to remove Claustrophobic!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait hardyhealthunlock command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            progress.IsUnlocked = unlock;

            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, unlock);

            return TextCommandResult.Success($"Hardy Health trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait bowyerunlock command.
        /// </summary>
        private TextCommandResult OnTraitBowyerUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            progress.IsUnlocked = unlock;

            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, unlock);

            return TextCommandResult.Success($"Bowyer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait improviserunlock command.
        /// </summary>
        private TextCommandResult OnTraitImproviserUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            progress.IsUnlocked = unlock;

            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, unlock);

            return TextCommandResult.Success($"Improviser trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait tinkererunlock command.
        /// </summary>
        private TextCommandResult OnTraitTinkererUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            progress.IsUnlocked = unlock;

            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, unlock);

            return TextCommandResult.Success($"Tinkerer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait mercilessunlock command.
        /// </summary>
        private TextCommandResult OnTraitMercilessUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            progress.IsUnlocked = unlock;

            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, unlock);

            return TextCommandResult.Success($"Merciless trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait claustrophobicunlock command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool removed = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            progress.IsRemoved = removed;

            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, removed);

            return TextCommandResult.Success($"Claustrophobic trait {(removed ? "removed" : "restored")}.");
        }

        // =========================================================================
        // PERSISTENCE METHODS FOR NEW TRAITS
        // =========================================================================

        /// <summary>
        /// Persist clothier progress to world save data.
        /// </summary>
        public static void PersistClothierProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (ClothierProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(CLOTHIER_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = ClothierProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x54); // 'T'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.SewingKitUnlocked);
                                writer.Write(progress.UniqueClothesWorn.Count);
                                foreach (string clothCode in progress.UniqueClothesWorn)
                                {
                                    writer.Write(clothCode);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(CLOTHIER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist clothier progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load clothier progress from world save data.
        /// </summary>
        private void LoadClothierProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CLOTHIER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No clothier progress data found");
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

                        if (magic1 != 0x43 || magic2 != 0x4C || magic3 != 0x54)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid clothier progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new ClothierProgressData();
                            progress.SewingKitUnlocked = reader.ReadBoolean();
                            int clothCount = reader.ReadInt32();
                            for (int j = 0; j < clothCount; j++)
                            {
                                progress.UniqueClothesWorn.Add(reader.ReadString());
                            }
                            ClothierProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded clothier progress for {ClothierProgress.Count} players");
            }
            catch (Exception ex)
            {
                ClothierProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load clothier progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist mender progress to world save data.
        /// </summary>
        public static void PersistMenderProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (MenderProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(MENDER_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = MenderProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x4D); // 'M'
                            writer.Write((byte)0x4E); // 'N'
                            writer.Write((byte)0x44); // 'D'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.RepairsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MENDER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist mender progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load mender progress from world save data.
        /// </summary>
        private void LoadMenderProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MENDER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No mender progress data found");
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

                        if (magic1 != 0x4D || magic2 != 0x4E || magic3 != 0x44)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid mender progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new MenderProgressData
                            {
                                TotalCredits = reader.ReadInt32(),
                                RepairsInIncrement = reader.ReadInt32(),
                                CurrentIncrementSize = reader.ReadInt32()
                            };
                            MenderProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded mender progress for {MenderProgress.Count} players");
            }
            catch (Exception ex)
            {
                MenderProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load mender progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist pilferer progress to world save data.
        /// </summary>
        public static void PersistPilfererProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (PilfererProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(PILFERER_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = PilfererProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x50); // 'P'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.PointsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                                writer.Write(progress.OpenedChestPositions.Count);
                                foreach (string posKey in progress.OpenedChestPositions)
                                {
                                    writer.Write(posKey);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(PILFERER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist pilferer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load pilferer progress from world save data.
        /// </summary>
        private void LoadPilfererProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(PILFERER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No pilferer progress data found");
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

                        if (magic1 != 0x50 || magic2 != 0x4C || magic3 != 0x46)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid pilferer progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new PilfererProgressData
                            {
                                TotalCredits = reader.ReadInt32(),
                                PointsInIncrement = reader.ReadInt32(),
                                CurrentIncrementSize = reader.ReadInt32()
                            };
                            int chestCount = reader.ReadInt32();
                            for (int j = 0; j < chestCount; j++)
                            {
                                progress.OpenedChestPositions.Add(reader.ReadString());
                            }
                            PilfererProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded pilferer progress for {PilfererProgress.Count} players");
            }
            catch (Exception ex)
            {
                PilfererProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load pilferer progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist resourceful progress to world save data.
        /// </summary>
        public static void PersistResourcefulProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (ResourcefulProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(RESOURCEFUL_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = ResourcefulProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.AnimalsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(RESOURCEFUL_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist resourceful progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load resourceful progress from world save data.
        /// </summary>
        private void LoadResourcefulProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(RESOURCEFUL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No resourceful progress data found");
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

                        if (magic1 != 0x52 || magic2 != 0x53 || magic3 != 0x46)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid resourceful progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new ResourcefulProgressData
                            {
                                TotalCredits = reader.ReadInt32(),
                                AnimalsInIncrement = reader.ReadInt32(),
                                CurrentIncrementSize = reader.ReadInt32()
                            };
                            ResourcefulProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded resourceful progress for {ResourcefulProgress.Count} players");
            }
            catch (Exception ex)
            {
                ResourcefulProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load resourceful progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist forager progress to world save data.
        /// </summary>
        public static void PersistForagerProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (ForagerProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(FORAGER_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = ForagerProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x47); // 'G'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.CropsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(FORAGER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist forager progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load forager progress from world save data.
        /// </summary>
        private void LoadForagerProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(FORAGER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No forager progress data found");
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

                        if (magic1 != 0x46 || magic2 != 0x52 || magic3 != 0x47)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid forager progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new ForagerProgressData
                            {
                                TotalCredits = reader.ReadInt32(),
                                CropsInIncrement = reader.ReadInt32(),
                                CurrentIncrementSize = reader.ReadInt32()
                            };
                            ForagerProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded forager progress for {ForagerProgress.Count} players");
            }
            catch (Exception ex)
            {
                ForagerProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load forager progress: {ex.Message}");
            }
        }

        // =========================================================================
        // FURTIVE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist furtive progress to world save data.
        /// </summary>
        public static void PersistFurtiveProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (FurtiveProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(FURTIVE_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = FurtiveProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x46); // 'F'
                            writer.Write((byte)0x55); // 'U'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.BlocksInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(FURTIVE_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist furtive progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load furtive progress from world save data.
        /// </summary>
        private void LoadFurtiveProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(FURTIVE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No furtive progress data found");
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

                        if (magic1 != 0x46 || magic2 != 0x55 || magic3 != 0x52)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid furtive progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new FurtiveProgressData
                            {
                                TotalCredits = reader.ReadInt32(),
                                BlocksInIncrement = reader.ReadSingle(),
                                CurrentIncrementSize = reader.ReadInt32()
                            };
                            FurtiveProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded furtive progress for {FurtiveProgress.Count} players");
            }
            catch (Exception ex)
            {
                FurtiveProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load furtive progress: {ex.Message}");
            }
        }

        // =========================================================================
        // PRECISE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist precise progress to world save data.
        /// </summary>
        public static void PersistPreciseProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (PreciseProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(PRECISE_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = PreciseProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x50); // 'P'
                            writer.Write((byte)0x52); // 'R'
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);

                                // Write weapon progress
                                writer.Write(progress.WeaponProgress.Count);
                                foreach (var weaponKvp in progress.WeaponProgress)
                                {
                                    writer.Write(weaponKvp.Key);
                                    writer.Write(weaponKvp.Value.DamageInIncrement);
                                    writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(PRECISE_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist precise progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load precise progress from world save data.
        /// </summary>
        private void LoadPreciseProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(PRECISE_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No precise progress data found");
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

                        if (magic1 != 0x50 || magic2 != 0x52 || magic3 != 0x43)
                        {
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid precise progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
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

                            PreciseProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded precise progress for {PreciseProgress.Count} players");
            }
            catch (Exception ex)
            {
                PreciseProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load precise progress: {ex.Message}");
            }
        }

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
                if (TechnicalProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(TECHNICAL_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist technical progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load technical progress from world save data.
        /// </summary>
        private void LoadTechnicalProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(TECHNICAL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No technical progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid technical progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new TechnicalProgressData
                            {
                                TranslocatorsRepaired = reader.ReadInt32(),
                                IsUnlocked = reader.ReadBoolean()
                            };
                            TechnicalProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded technical progress for {TechnicalProgress.Count} players");
            }
            catch (Exception ex)
            {
                TechnicalProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load technical progress: {ex.Message}");
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
                if (HardyHealthProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(HARDY_HEALTH_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist hardy health progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load hardy health progress from world save data.
        /// </summary>
        private void LoadHardyHealthProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(HARDY_HEALTH_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No hardy health progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid hardy health progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new HardyHealthProgressData
                            {
                                IsUnlocked = reader.ReadBoolean()
                            };
                            HardyHealthProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded hardy health progress for {HardyHealthProgress.Count} players");
            }
            catch (Exception ex)
            {
                HardyHealthProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load hardy health progress: {ex.Message}");
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
                if (BowyerProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(BOWYER_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist bowyer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load bowyer progress from world save data.
        /// </summary>
        private void LoadBowyerProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(BOWYER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No bowyer progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid bowyer progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new BowyerProgressData
                            {
                                TotalBowDamage = reader.ReadSingle(),
                                IsUnlocked = reader.ReadBoolean()
                            };
                            BowyerProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded bowyer progress for {BowyerProgress.Count} players");
            }
            catch (Exception ex)
            {
                BowyerProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load bowyer progress: {ex.Message}");
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
                if (ImproviserProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(IMPROVISER_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist improviser progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load improviser progress from world save data.
        /// </summary>
        private void LoadImproviserProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(IMPROVISER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No improviser progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid improviser progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new ImproviserProgressData
                            {
                                TotalRockDamage = reader.ReadSingle(),
                                IsUnlocked = reader.ReadBoolean()
                            };
                            ImproviserProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded improviser progress for {ImproviserProgress.Count} players");
            }
            catch (Exception ex)
            {
                ImproviserProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load improviser progress: {ex.Message}");
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
                if (TinkererProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(TINKERER_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist tinkerer progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load tinkerer progress from world save data.
        /// </summary>
        private void LoadTinkererProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(TINKERER_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No tinkerer progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid tinkerer progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new TinkererProgressData
                            {
                                IsUnlocked = reader.ReadBoolean()
                            };
                            TinkererProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded tinkerer progress for {TinkererProgress.Count} players");
            }
            catch (Exception ex)
            {
                TinkererProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load tinkerer progress: {ex.Message}");
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
                if (MercilessProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(MERCILESS_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist merciless progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load merciless progress from world save data.
        /// </summary>
        private void LoadMercilessProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(MERCILESS_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No merciless progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid merciless progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new MercilessProgressData
                            {
                                IsUnlocked = reader.ReadBoolean()
                            };
                            MercilessProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded merciless progress for {MercilessProgress.Count} players");
            }
            catch (Exception ex)
            {
                MercilessProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load merciless progress: {ex.Message}");
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
                if (ClaustrophobicRemovalProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY, null);
                    return;
                }

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
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist claustrophobic removal progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load claustrophobic removal progress from world save data.
        /// </summary>
        private void LoadClaustrophobicRemovalProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SimpleImprovingTraits] No claustrophobic removal progress data found");
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
                            ServerApi.Logger.Warning("[SimpleImprovingTraits] Invalid claustrophobic removal progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            var progress = new ClaustrophobicRemovalProgressData
                            {
                                IsRemoved = reader.ReadBoolean()
                            };
                            ClaustrophobicRemovalProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Loaded claustrophobic removal progress for {ClaustrophobicRemovalProgress.Count} players");
            }
            catch (Exception ex)
            {
                ClaustrophobicRemovalProgress.Clear();
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load claustrophobic removal progress: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Client-side mod system that displays mining progression in the character traits dialog.
    /// Uses Harmony to patch the CharacterSystem's trait display method.
    /// </summary>
    public class SimpleImprovingTraitsClientSystem : ModSystem
    {
        private ICoreClientAPI clientApi;
        private Harmony harmony;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            clientApi = api;

            // Apply Harmony patches manually for better control
            harmony = new Harmony("simpleimprovingtraits");
            try
            {
                ApplyPatches(api);
                api.Logger.Notification("[SimpleImprovingTraits] Client-side mod loaded, Harmony patches applied");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SimpleImprovingTraits] Failed to apply Harmony patches: {ex.Message}");
                api.Logger.Error($"[SimpleImprovingTraits] Stack trace: {ex.StackTrace}");
            }
        }

        private void ApplyPatches(ICoreClientAPI api)
        {
            // Set the API reference for the patch to use
            CharacterSystemPatches.ClientApi = api;

            // Find the CharacterSystem type
            var characterSystemType = AccessTools.TypeByName("Vintagestory.GameContent.CharacterSystem");
            if (characterSystemType == null)
            {
                api.Logger.Warning("[SimpleImprovingTraits] Could not find CharacterSystem type");
                return;
            }

            // Find the getClassTraitText method
            var targetMethod = AccessTools.Method(characterSystemType, "getClassTraitText");
            if (targetMethod == null)
            {
                api.Logger.Warning("[SimpleImprovingTraits] Could not find getClassTraitText method");

                // List available methods for debugging
                var methods = characterSystemType.GetMethods(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                api.Logger.Debug($"[SimpleImprovingTraits] Available methods in CharacterSystem:");
                foreach (var m in methods)
                {
                    if (m.Name.ToLower().Contains("trait"))
                    {
                        api.Logger.Debug($"  - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
                    }
                }
                return;
            }

            api.Logger.Debug($"[SimpleImprovingTraits] Found method: {targetMethod.Name}, params: {string.Join(", ", targetMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");

            // Get our postfix method
            var postfixMethod = AccessTools.Method(typeof(CharacterSystemPatches), nameof(CharacterSystemPatches.GetClassTraitText_Postfix));

            // Apply the patch
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            api.Logger.Notification("[SimpleImprovingTraits] Successfully patched getClassTraitText");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("simpleimprovingtraits");
            base.Dispose();
        }
    }

    /// <summary>
    /// Harmony patch methods for CharacterSystem.
    /// </summary>
    public static class CharacterSystemPatches
    {
        // Reference to the client API, set during patch application
        public static ICoreClientAPI ClientApi { get; set; }

        /// <summary>
        /// Postfix for getClassTraitText - adds dynamic mining and melee progression info.
        /// The method has NO parameters - it's an instance method on CharacterSystem.
        /// </summary>
        public static void GetClassTraitText_Postfix(ref string __result)
        {
            // Get the player entity from the client API
            if (ClientApi == null) return;

            EntityPlayer eplr = ClientApi.World?.Player?.Entity;
            if (eplr == null) return;

            // Get mining progression data
            int miningLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MINING_LEVEL, 0);
            int miningBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MINING_BONUS, 0);
            bool hasVanillaHardy = eplr.WatchedAttributes.GetBool("sitHasVanillaHardy", false);

            // Get melee progression data
            int meleeLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MELEE_LEVEL, 0);
            int meleeBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MELEE_BONUS, 0);
            bool hasVanillaSoldier = eplr.WatchedAttributes.GetBool("sitHasVanillaSoldier", false);

            // Get ranged progression data
            int rangedLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RANGED_LEVEL, 0);
            int rangedDamageBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RANGED_DAMAGE_BONUS, 0);
            int rangedAccuracyBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RANGED_ACCURACY_BONUS, 0);
            int rangedDistanceBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RANGED_DISTANCE_BONUS, 0);
            bool hasVanillaFocused = eplr.WatchedAttributes.GetBool("sitHasVanillaFocused", false);

            // Get walking progression data
            int walkingLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_WALKING_LEVEL, 0);
            int walkingBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_WALKING_BONUS, 0);
            bool hasVanillaFleetfooted = eplr.WatchedAttributes.GetBool("sitHasVanillaFleetfooted", false);

            // Get armor progression data
            int armorDurabilityLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, 0);
            int armorDurabilityBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_ARMOR_DURABILITY_BONUS, 0);
            int armorWalkSpeedLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, 0);
            int armorWalkSpeedBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_ARMOR_WALKSPEED_BONUS, 0);
            bool hasVanillaSoldierArmor = eplr.WatchedAttributes.GetBool("sitHasVanillaSoldierArmor", false);

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier} | Ranged: Level={rangedLevel}, HasFocused={hasVanillaFocused} | Walking: Level={walkingLevel}, HasFleetfooted={hasVanillaFleetfooted} | Armor: Dur={armorDurabilityLevel}, Walk={armorWalkSpeedLevel}");

            // Get the "no traits" message
            string noTraitsMsg = Lang.Get("charactersheet-notraits");

            // Check if we have NO real traits (only "no traits" message or empty)
            bool hasNoTraits = string.IsNullOrEmpty(__result) ||
                               __result.Trim() == noTraitsMsg.Trim() ||
                               __result == noTraitsMsg;

            // Process mining progression (Hardy trait)
            if (miningLevel > 0)
            {
                string plainMiningTraitName = Lang.Get("simpleimprovingtraits:trait-sitminingmastery");

                if (hasVanillaHardy)
                {
                    // Class already has Hardy (e.g., Blackguard) - update the existing Hardy's mining speed
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_HARDY_MINING_BONUS + miningBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_HARDY_MINING_BONUS}% mining speed",
                        $"+{combinedBonus}% mining speed");

                    // Remove our separate sitminingmastery entry if somehow present
                    if (__result.Contains(plainMiningTraitName))
                    {
                        __result = __result.Replace("\n" + plainMiningTraitName, "");
                        __result = __result.Replace(plainMiningTraitName + "\n", "");
                        __result = __result.Replace(plainMiningTraitName, "");
                    }
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Hardy
                    __result = Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", miningBonus);
                    hasNoTraits = false; // We now have traits
                }
                else if (__result.Contains(plainMiningTraitName))
                {
                    // We have our trait but no vanilla Hardy - replace plain name with dynamic version
                    __result = __result.Replace(plainMiningTraitName,
                        Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", miningBonus));
                }
                else
                {
                    // Has other traits but no Hardy at all - append our dynamic Hardy
                    __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", miningBonus);
                }
            }

            // Process melee progression (Soldier trait)
            if (meleeLevel > 0)
            {
                string plainMeleeTraitName = Lang.Get("simpleimprovingtraits:trait-sitmeleemastery");

                // Re-check hasNoTraits after mining processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaSoldier)
                {
                    // Class already has Soldier (e.g., Blackguard) - update the existing Soldier's melee damage
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_MELEE_BONUS + meleeBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_MELEE_BONUS}% melee damage",
                        $"+{combinedBonus}% melee damage");

                    // Remove our separate sitmeleemastery entry if somehow present
                    if (__result.Contains(plainMeleeTraitName))
                    {
                        __result = __result.Replace("\n" + plainMeleeTraitName, "");
                        __result = __result.Replace(plainMeleeTraitName + "\n", "");
                        __result = __result.Replace(plainMeleeTraitName, "");
                    }
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Soldier
                    __result = Lang.Get("simpleimprovingtraits:trait-soldier-dynamic", meleeBonus);
                }
                else if (__result.Contains(plainMeleeTraitName))
                {
                    // We have our trait but no vanilla Soldier - replace plain name with dynamic version
                    __result = __result.Replace(plainMeleeTraitName,
                        Lang.Get("simpleimprovingtraits:trait-soldier-dynamic", meleeBonus));
                }
                else
                {
                    // Has other traits but no Soldier at all - append our dynamic Soldier
                    __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-soldier-dynamic", meleeBonus);
                }
            }

            // Process ranged progression (Focused trait)
            if (rangedLevel > 0)
            {
                string plainRangedTraitName = Lang.Get("simpleimprovingtraits:trait-sitrangedmastery");

                // Re-check hasNoTraits after melee processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaFocused)
                {
                    // Class already has Focused (e.g., Hunter) - update the existing Focused's stats
                    // Ranged damage
                    int combinedDamage = SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_DAMAGE_BONUS + rangedDamageBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_DAMAGE_BONUS}% ranged damage",
                        $"+{combinedDamage}% ranged damage");

                    // Ranged accuracy
                    int combinedAccuracy = SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_ACCURACY_BONUS + rangedAccuracyBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_ACCURACY_BONUS}% ranged accuracy",
                        $"+{combinedAccuracy}% ranged accuracy");

                    // Ranged distance
                    int combinedDistance = SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_DISTANCE_BONUS + rangedDistanceBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FOCUSED_DISTANCE_BONUS}% ranged distance",
                        $"+{combinedDistance}% ranged distance");

                    // Remove our separate sitrangedmastery entry if somehow present
                    if (__result.Contains(plainRangedTraitName))
                    {
                        __result = __result.Replace("\n" + plainRangedTraitName, "");
                        __result = __result.Replace(plainRangedTraitName + "\n", "");
                        __result = __result.Replace(plainRangedTraitName, "");
                    }
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Focused
                    __result = Lang.Get("simpleimprovingtraits:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
                else if (__result.Contains(plainRangedTraitName))
                {
                    // We have our trait but no vanilla Focused - replace plain name with dynamic version
                    __result = __result.Replace(plainRangedTraitName,
                        Lang.Get("simpleimprovingtraits:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus));
                }
                else
                {
                    // Has other traits but no Focused at all - append our dynamic Focused
                    __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
            }

            // Process walking progression (Fleetfooted trait)
            if (walkingLevel > 0)
            {
                string plainWalkingTraitName = Lang.Get("simpleimprovingtraits:trait-sitwalkingmastery");

                // Re-check hasNoTraits after ranged processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaFleetfooted)
                {
                    // Class already has Fleetfooted (e.g., Hunter, Clockmaker) - update the existing Fleetfooted's walk speed
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_FLEETFOOTED_WALK_BONUS + walkingBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FLEETFOOTED_WALK_BONUS}% walk speed",
                        $"+{combinedBonus}% walk speed");

                    // Remove our separate sitwalkingmastery entry if somehow present
                    if (__result.Contains(plainWalkingTraitName))
                    {
                        __result = __result.Replace("\n" + plainWalkingTraitName, "");
                        __result = __result.Replace(plainWalkingTraitName + "\n", "");
                        __result = __result.Replace(plainWalkingTraitName, "");
                    }
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Fleetfooted
                    __result = Lang.Get("simpleimprovingtraits:trait-fleetfooted-dynamic", walkingBonus);
                }
                else if (__result.Contains(plainWalkingTraitName))
                {
                    // We have our trait but no vanilla Fleetfooted - replace plain name with dynamic version
                    __result = __result.Replace(plainWalkingTraitName,
                        Lang.Get("simpleimprovingtraits:trait-fleetfooted-dynamic", walkingBonus));
                }
                else
                {
                    // Has other traits but no Fleetfooted at all - append our dynamic Fleetfooted
                    __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-fleetfooted-dynamic", walkingBonus);
                }
            }

            // Process armor progression (Soldier trait - armor durability and speed penalty)
            if (armorDurabilityLevel > 0 || armorWalkSpeedLevel > 0)
            {
                // Re-check hasNoTraits after walking processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                // Calculate combined bonuses
                int totalDurabilityBonus = armorDurabilityBonus;
                int totalWalkSpeedBonus = armorWalkSpeedBonus;

                if (hasVanillaSoldierArmor)
                {
                    // Class already has Soldier - update the existing armor stats
                    // Vanilla Soldier shows: +15% armor durability, -25% armor speed penalty

                    // Update armor durability if we have bonus
                    if (armorDurabilityBonus > 0)
                    {
                        int combinedDurability = SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS + armorDurabilityBonus;
                        __result = __result.Replace(
                            $"+{SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS}% armor durability",
                            $"+{combinedDurability}% armor durability");
                    }

                    // Update armor speed penalty if we have bonus
                    if (armorWalkSpeedBonus > 0)
                    {
                        int combinedSpeedPenalty = SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS + armorWalkSpeedBonus;
                        __result = __result.Replace(
                            $"-{SimpleImprovingTraitsModSystem.VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS}% armor speed penalty",
                            $"-{combinedSpeedPenalty}% armor speed penalty");
                    }
                }
                else if (hasNoTraits)
                {
                    // No traits at all - show our armor progression as a Soldier-like trait
                    __result = Lang.Get("simpleimprovingtraits:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                }
                else
                {
                    // Has other traits but no vanilla Soldier - check if we already added melee Soldier
                    // Only add if we have actual bonuses to show
                    if (totalDurabilityBonus > 0 || totalWalkSpeedBonus > 0)
                    {
                        // Check if melee progression already added a dynamic Soldier entry
                        string meleeSoldierPattern = Lang.Get("simpleimprovingtraits:trait-soldier-dynamic", meleeBonus);
                        if (meleeLevel > 0 && __result.Contains(meleeSoldierPattern))
                        {
                            // Replace the melee-only Soldier with a combined entry
                            __result = __result.Replace(meleeSoldierPattern,
                                Lang.Get("simpleimprovingtraits:trait-soldier-combined-dynamic", meleeBonus, totalDurabilityBonus, totalWalkSpeedBonus));
                        }
                        else
                        {
                            // No melee Soldier was added, add armor-only entry
                            __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                        }
                    }
                }
            }

            // Process Clothier trait (unlocked by wearing 20 unique clothes)
            bool clothierUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_CLOTHIER_UNLOCKED, false);
            if (clothierUnlocked)
            {
                string plainClothierTraitName = Lang.Get("simpleimprovingtraits:trait-sitclothiermastery");
                string dynamicClothierTrait = Lang.Get("simpleimprovingtraits:trait-clothier-dynamic");

                // Re-check hasNoTraits after armor processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicClothierTrait;
                }
                else if (__result.Contains(plainClothierTraitName))
                {
                    __result = __result.Replace(plainClothierTraitName, dynamicClothierTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicClothierTrait;
                }
            }

            // Process Mender trait (improves armor/clothing durability)
            int menderLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MENDER_LEVEL, 0);
            int menderBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MENDER_BONUS, 0);
            bool hasVanillaMender = eplr.WatchedAttributes.GetBool("sitHasVanillaMender", false);
            if (menderLevel > 0)
            {
                string plainMenderTraitName = Lang.Get("simpleimprovingtraits:trait-sitmendermastery");
                string dynamicMenderTrait = Lang.Get("simpleimprovingtraits:trait-mender-dynamic", menderBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaMender)
                {
                    // Class already has Mender trait - update the existing durability value
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_MENDER_ARMOR_DURABILITY_BONUS + menderBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_MENDER_ARMOR_DURABILITY_BONUS}% armor durability",
                        $"+{combinedBonus}% armor durability");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicMenderTrait;
                }
                else if (__result.Contains(plainMenderTraitName))
                {
                    __result = __result.Replace(plainMenderTraitName, dynamicMenderTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicMenderTrait;
                }
            }

            // Process Pilferer trait (improves rusty gear, vessel loot, vessel collection)
            int pilfererLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_PILFERER_LEVEL, 0);
            int pilfererBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_PILFERER_BONUS, 0);
            bool hasVanillaPilferer = eplr.WatchedAttributes.GetBool("sitHasVanillaPilferer", false);
            if (pilfererLevel > 0)
            {
                string plainPilfererTraitName = Lang.Get("simpleimprovingtraits:trait-sitpilferermastery");
                string dynamicPilfererTrait = Lang.Get("simpleimprovingtraits:trait-pilferer-dynamic", pilfererBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaPilferer)
                {
                    // Class already has Pilferer trait - update the existing values
                    int combinedRusty = SimpleImprovingTraitsModSystem.VANILLA_PILFERER_RUSTY_GEAR_BONUS + pilfererBonus;
                    int combinedVessel = SimpleImprovingTraitsModSystem.VANILLA_PILFERER_VESSEL_CONTENTS_BONUS + pilfererBonus;
                    int combinedCollection = SimpleImprovingTraitsModSystem.VANILLA_PILFERER_WHOLE_VESSEL_BONUS + pilfererBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_PILFERER_RUSTY_GEAR_BONUS}% rusty gear",
                        $"+{combinedRusty}% rusty gear");
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_PILFERER_VESSEL_CONTENTS_BONUS}% cracked vessel drops",
                        $"+{combinedVessel}% cracked vessel drops");
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_PILFERER_WHOLE_VESSEL_BONUS}% vessel collection",
                        $"+{combinedCollection}% vessel collection");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicPilfererTrait;
                }
                else if (__result.Contains(plainPilfererTraitName))
                {
                    __result = __result.Replace(plainPilfererTraitName, dynamicPilfererTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicPilfererTrait;
                }
            }

            // Process Resourceful trait (improves animal loot and harvesting speed)
            int resourcefulLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RESOURCEFUL_LEVEL, 0);
            int resourcefulLootBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RESOURCEFUL_LOOT_BONUS, 0);
            int resourcefulSpeedBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_RESOURCEFUL_SPEED_BONUS, 0);
            bool hasVanillaResourceful = eplr.WatchedAttributes.GetBool("sitHasVanillaResourceful", false);
            if (resourcefulLevel > 0)
            {
                string plainResourcefulTraitName = Lang.Get("simpleimprovingtraits:trait-sitresourcefulmastery");
                string dynamicResourcefulTrait = Lang.Get("simpleimprovingtraits:trait-resourceful-dynamic", resourcefulLootBonus, resourcefulSpeedBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaResourceful)
                {
                    // Class already has Resourceful trait - update the existing values
                    int combinedLoot = SimpleImprovingTraitsModSystem.VANILLA_RESOURCEFUL_LOOT_BONUS + resourcefulLootBonus;
                    int combinedSpeed = SimpleImprovingTraitsModSystem.VANILLA_RESOURCEFUL_SPEED_BONUS + resourcefulSpeedBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_RESOURCEFUL_LOOT_BONUS}% animal loot",
                        $"+{combinedLoot}% animal loot");
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_RESOURCEFUL_SPEED_BONUS}% harvesting speed",
                        $"+{combinedSpeed}% harvesting speed");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicResourcefulTrait;
                }
                else if (__result.Contains(plainResourcefulTraitName))
                {
                    __result = __result.Replace(plainResourcefulTraitName, dynamicResourcefulTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicResourcefulTrait;
                }
            }

            // Process Forager trait (improves foraging loot and wild crop drops)
            int foragerLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_FORAGER_LEVEL, 0);
            int foragerLootBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_FORAGER_LOOT_BONUS, 0);
            int foragerWildCropBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_FORAGER_WILD_CROP_BONUS, 0);
            bool hasVanillaForager = eplr.WatchedAttributes.GetBool("sitHasVanillaForager", false);
            if (foragerLevel > 0)
            {
                string plainForagerTraitName = Lang.Get("simpleimprovingtraits:trait-sitforagermastery");
                string dynamicForagerTrait = Lang.Get("simpleimprovingtraits:trait-forager-dynamic", foragerLootBonus, foragerWildCropBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaForager)
                {
                    // Class already has Forager trait - update the existing values
                    int combinedLoot = SimpleImprovingTraitsModSystem.VANILLA_FORAGER_LOOT_BONUS + foragerLootBonus;
                    int combinedWildCrop = SimpleImprovingTraitsModSystem.VANILLA_FORAGER_WILD_CROP_BONUS + foragerWildCropBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FORAGER_LOOT_BONUS}% foraging loot",
                        $"+{combinedLoot}% foraging loot");
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_FORAGER_WILD_CROP_BONUS}% wild crop drops",
                        $"+{combinedWildCrop}% wild crop drops");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicForagerTrait;
                }
                else if (__result.Contains(plainForagerTraitName))
                {
                    __result = __result.Replace(plainForagerTraitName, dynamicForagerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicForagerTrait;
                }
            }

            // Process Furtive trait (reduces animal detection range)
            int furtiveLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_FURTIVE_LEVEL, 0);
            int furtiveBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_FURTIVE_BONUS, 0);
            bool hasVanillaFurtive = eplr.WatchedAttributes.GetBool("sitHasVanillaFurtive", false);
            if (furtiveLevel > 0)
            {
                string plainFurtiveTraitName = Lang.Get("simpleimprovingtraits:trait-sitfurtivemastery");
                string dynamicFurtiveTrait = Lang.Get("simpleimprovingtraits:trait-furtive-dynamic", furtiveBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaFurtive)
                {
                    // Class already has Furtive trait - update the existing values
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_FURTIVE_DETECTION_REDUCTION + furtiveBonus;
                    __result = __result.Replace(
                        $"-{SimpleImprovingTraitsModSystem.VANILLA_FURTIVE_DETECTION_REDUCTION}% animal seeking range",
                        $"-{combinedBonus}% animal detection range");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicFurtiveTrait;
                }
                else if (__result.Contains(plainFurtiveTraitName))
                {
                    __result = __result.Replace(plainFurtiveTraitName, dynamicFurtiveTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicFurtiveTrait;
                }
            }

            // Process Precise trait (improves damage to mechanicals)
            int preciseLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_PRECISE_LEVEL, 0);
            int preciseBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_PRECISE_BONUS, 0);
            bool hasVanillaPrecise = eplr.WatchedAttributes.GetBool("sitHasVanillaPrecise", false);
            if (preciseLevel > 0)
            {
                string plainPreciseTraitName = Lang.Get("simpleimprovingtraits:trait-sitprecisemastery");
                string dynamicPreciseTrait = Lang.Get("simpleimprovingtraits:trait-precise-dynamic", preciseBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasVanillaPrecise)
                {
                    // Class already has Precise trait - update the existing values
                    int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + preciseBonus;
                    __result = __result.Replace(
                        $"+{SimpleImprovingTraitsModSystem.VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS}% damage vs mechanicals",
                        $"+{combinedBonus}% damage to mechanicals");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicPreciseTrait;
                }
                else if (__result.Contains(plainPreciseTraitName))
                {
                    __result = __result.Replace(plainPreciseTraitName, dynamicPreciseTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicPreciseTrait;
                }
            }

            // Process Hunger trait (reduces hunger rate)
            int hungerLevel = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_HUNGER_LEVEL, 0);
            int hungerBonus = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_HUNGER_BONUS, 0);
            if (hungerLevel > 0)
            {
                string plainHungerTraitName = Lang.Get("simpleimprovingtraits:trait-sithungermastery");
                string dynamicHungerTrait = Lang.Get("simpleimprovingtraits:trait-hunger-dynamic", hungerBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicHungerTrait;
                }
                else if (__result.Contains(plainHungerTraitName))
                {
                    __result = __result.Replace(plainHungerTraitName, dynamicHungerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicHungerTrait;
                }
            }

            // Process Technical unlock trait (translocator gear cost reduction)
            bool technicalUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_TECHNICAL_UNLOCKED, false);
            if (technicalUnlocked)
            {
                string plainTechnicalTraitName = Lang.Get("simpleimprovingtraits:trait-sittechnicalmastery");
                string dynamicTechnicalTrait = Lang.Get("simpleimprovingtraits:trait-technical-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicTechnicalTrait;
                }
                else if (__result.Contains(plainTechnicalTraitName))
                {
                    __result = __result.Replace(plainTechnicalTraitName, dynamicTechnicalTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicTechnicalTrait;
                }
            }

            // Process Hardy Health unlock trait (+5 HP)
            bool hardyHealthUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_HARDY_HEALTH_UNLOCKED, false);
            if (hardyHealthUnlocked)
            {
                string plainHardyHealthTraitName = Lang.Get("simpleimprovingtraits:trait-sithardyhealthmastery");
                string dynamicHardyHealthTrait = Lang.Get("simpleimprovingtraits:trait-hardyhealth-dynamic", SimpleImprovingTraitsModSystem.HardyHealthBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicHardyHealthTrait;
                }
                else if (__result.Contains(plainHardyHealthTraitName))
                {
                    __result = __result.Replace(plainHardyHealthTraitName, dynamicHardyHealthTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicHardyHealthTrait;
                }
            }

            // Process Bowyer unlock trait (crude bow crafting)
            bool bowyerUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_BOWYER_UNLOCKED, false);
            if (bowyerUnlocked)
            {
                string plainBowyerTraitName = Lang.Get("simpleimprovingtraits:trait-sitbowyermastery");
                string dynamicBowyerTrait = Lang.Get("simpleimprovingtraits:trait-bowyer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicBowyerTrait;
                }
                else if (__result.Contains(plainBowyerTraitName))
                {
                    __result = __result.Replace(plainBowyerTraitName, dynamicBowyerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicBowyerTrait;
                }
            }

            // Process Improviser unlock trait (sling crafting)
            bool improviserUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_IMPROVISER_UNLOCKED, false);
            if (improviserUnlocked)
            {
                string plainImproviserTraitName = Lang.Get("simpleimprovingtraits:trait-sitimprovisermastery");
                string dynamicImproviserTrait = Lang.Get("simpleimprovingtraits:trait-improviser-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicImproviserTrait;
                }
                else if (__result.Contains(plainImproviserTraitName))
                {
                    __result = __result.Replace(plainImproviserTraitName, dynamicImproviserTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicImproviserTrait;
                }
            }

            // Process Tinkerer unlock trait (tuning spear crafting)
            bool tinkererUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_TINKERER_UNLOCKED, false);
            if (tinkererUnlocked)
            {
                string plainTinkererTraitName = Lang.Get("simpleimprovingtraits:trait-sittinkerermastery");
                string dynamicTinkererTrait = Lang.Get("simpleimprovingtraits:trait-tinkerer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicTinkererTrait;
                }
                else if (__result.Contains(plainTinkererTraitName))
                {
                    __result = __result.Replace(plainTinkererTraitName, dynamicTinkererTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicTinkererTrait;
                }
            }

            // Process Merciless unlock trait (shortsword/shield crafting)
            bool mercilessUnlocked = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_MERCILESS_UNLOCKED, false);
            if (mercilessUnlocked)
            {
                string plainMercilessTraitName = Lang.Get("simpleimprovingtraits:trait-sitmercilessmastery");
                string dynamicMercilessTrait = Lang.Get("simpleimprovingtraits:trait-merciless-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicMercilessTrait;
                }
                else if (__result.Contains(plainMercilessTraitName))
                {
                    __result = __result.Replace(plainMercilessTraitName, dynamicMercilessTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicMercilessTrait;
                }
            }

            // Process Claustrophobic Removed trait (penalty removal)
            bool claustrophobicRemoved = eplr.WatchedAttributes.GetBool(SimpleImprovingTraitsModSystem.WATCHED_CLAUSTROPHOBIC_REMOVED, false);
            if (claustrophobicRemoved)
            {
                string plainClaustrophobicTraitName = Lang.Get("simpleimprovingtraits:trait-sitclaustrophobicremoved");
                string dynamicClaustrophobicTrait = Lang.Get("simpleimprovingtraits:trait-claustrophobic-removed-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg;

                if (hasNoTraits)
                {
                    __result = dynamicClaustrophobicTrait;
                }
                else if (__result.Contains(plainClaustrophobicTraitName))
                {
                    __result = __result.Replace(plainClaustrophobicTraitName, dynamicClaustrophobicTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicClaustrophobicTrait;
                }
            }

            // Clean up any double newlines that might have been introduced
            while (__result.Contains("\n\n"))
            {
                __result = __result.Replace("\n\n", "\n");
            }
            __result = __result.Trim();

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] Modified result: {__result}");
        }
    }

    /// <summary>
    /// Server-side Harmony patches for entity damage tracking.
    /// </summary>
    public static class EntityDamagePatches
    {
        /// <summary>
        /// Postfix for Entity.ReceiveDamage - tracks melee and ranged damage dealt by players,
        /// and damage blocked by armor when players receive damage.
        /// </summary>
        public static void ReceiveDamage_Postfix(Entity __instance, DamageSource damageSource, float damage, bool __result)
        {
            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

            // Track armor damage blocked if the entity taking damage is a player wearing armor
            TrackArmorDamageBlocked(__instance, damageSource, damage);

            // Check if this is ranged damage (projectile with CauseEntity)
            if (SimpleImprovingTraitsModSystem.IsRangedDamage(damageSource))
            {
                // For ranged: CauseEntity is the shooter, SourceEntity is the projectile
                var shooterEntity = damageSource.CauseEntity as EntityPlayer;
                if (shooterEntity == null) return;

                var shooterPlayer = shooterEntity.Player as IServerPlayer;
                if (shooterPlayer == null) return;

                // Don't count self-damage
                if (__instance == shooterEntity) return;

                // Get the weapon combination (bow+arrow, sling+stone, etc.)
                string weaponCombo = SimpleImprovingTraitsModSystem.GetRangedWeaponCombo(damageSource.SourceEntity, shooterEntity);

                if (weaponCombo != null)
                {
                    SimpleImprovingTraitsModSystem.ProcessRangedDamage(shooterPlayer, weaponCombo, damage);

                    // Also track Precise damage if target is a mechanical creature
                    if (SimpleImprovingTraitsModSystem.IsMechanicalCreature(__instance))
                    {
                        SimpleImprovingTraitsModSystem.ProcessPreciseDamage(shooterPlayer, weaponCombo, damage);
                    }
                }
                return; // Don't also count as melee
            }

            // Check if damage was dealt by a player (melee)
            if (damageSource?.SourceEntity == null) return;

            var attackerPlayer = (damageSource.SourceEntity as EntityPlayer)?.Player as IServerPlayer;
            if (attackerPlayer == null) return;

            // Don't count self-damage
            if (__instance == damageSource.SourceEntity) return;

            // Get held weapon
            var heldItem = attackerPlayer.Entity?.RightHandItemSlot?.Itemstack?.Collectible;
            if (heldItem == null) return;

            string itemCode = heldItem.Code?.ToString();
            string weaponType = SimpleImprovingTraitsModSystem.GetWeaponTypeFromCode(itemCode);

            if (weaponType != null)
            {
                SimpleImprovingTraitsModSystem.ProcessMeleeDamage(attackerPlayer, weaponType, damage);

                // Also track Precise damage if target is a mechanical creature
                if (SimpleImprovingTraitsModSystem.IsMechanicalCreature(__instance))
                {
                    SimpleImprovingTraitsModSystem.ProcessPreciseDamage(attackerPlayer, weaponType, damage);
                }
            }
        }

        /// <summary>
        /// Track damage blocked by armor when a player takes damage.
        /// Uses hit probability (50% body, 30% legs, 20% head) to distribute damage to armor pieces.
        /// </summary>
        private static void TrackArmorDamageBlocked(Entity damagedEntity, DamageSource damageSource, float finalDamage)
        {
            // Only track actual combat damage - filter out healing and non-combat damage types
            if (damageSource == null) return;

            // Filter out non-combat damage types (healing, hunger, suffocation, etc.)
            // Only count damage that armor can actually block: melee attacks and projectiles
            var damageType = damageSource.Type;
            if (damageType == EnumDamageType.Heal ||
                damageType == EnumDamageType.Hunger ||
                damageType == EnumDamageType.Suffocation ||
                damageType == EnumDamageType.Poison ||
                damageType == EnumDamageType.Gravity ||
                damageType == EnumDamageType.Fire ||
                damageType == EnumDamageType.Frost ||
                damageType == EnumDamageType.Heat ||
                damageType == EnumDamageType.Electricity)
            {
                return; // These damage types are not blocked by armor
            }

            // Only process for players
            var playerEntity = damagedEntity as EntityPlayer;
            if (playerEntity == null) return;

            var player = playerEntity.Player as IServerPlayer;
            if (player == null) return;

            // Get the player's armor using character inventory
            var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (characterInventory == null) return;

            // Find armor pieces and calculate damage blocked per piece
            // Use hit probability: 50% body, 30% legs, 20% head
            foreach (var slot in characterInventory)
            {
                if (slot?.Itemstack?.Collectible == null) continue;

                string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                string armorType = SimpleImprovingTraitsModSystem.GetArmorType(itemCode);

                if (armorType == null) continue; // Not armor

                // Get the armor's protection value from item attributes
                // Vintage Story uses protectionModifiers.relativeProtection (0-1 scale, e.g., 0.2 = 20% reduction)
                float relativeProtection = 0f;
                var itemAttributes = slot.Itemstack.Collectible.Attributes;
                if (itemAttributes != null)
                {
                    var protectionModifiers = itemAttributes["protectionModifiers"];
                    if (protectionModifiers != null && protectionModifiers.Exists)
                    {
                        relativeProtection = protectionModifiers["relativeProtection"].AsFloat(0f);
                    }
                }

                // If no protection found, give a minimum credit for wearing armor at all
                // This ensures armor that blocks any damage still gives some XP
                if (relativeProtection <= 0)
                {
                    // Default to a small protection value so armor still grants some XP
                    relativeProtection = 0.05f; // 5% minimum
                }

                // Determine hit probability based on armor slot type (from item code)
                float hitProbability = 0.5f; // Default to body
                if (itemCode.Contains("-head-") || itemCode.Contains("-helmet-"))
                    hitProbability = 0.2f;
                else if (itemCode.Contains("-legs-") || itemCode.Contains("-leggings-"))
                    hitProbability = 0.3f;
                else // body
                    hitProbability = 0.5f;

                // Calculate damage blocked by this armor piece
                // For a hit that lands on this piece: originalDamage = finalDamage / (1 - protection)
                // damageBlocked = originalDamage - finalDamage = finalDamage * protection / (1 - protection)
                // We scale by hit probability since not all hits go to this piece
                // relativeProtection is already on 0-1 scale (e.g., 0.2 = 20% reduction)
                float protection = relativeProtection;
                if (protection >= 1f) protection = 0.99f; // Prevent division by zero

                float damageBlocked = finalDamage * protection / (1f - protection) * hitProbability;

                if (damageBlocked > 0)
                {
                    SimpleImprovingTraitsModSystem.ProcessArmorDamageBlocked(player, damageBlocked, itemCode);
                }
            }
        }
    }

    /// <summary>
    /// Server-side Harmony patches for animal harvesting (Resourceful trait).
    /// </summary>
    public static class HarvestingPatches
    {
        /// <summary>
        /// Postfix for EntityBehaviorHarvestable.SetHarvested - tracks when player harvests an animal.
        /// </summary>
        public static void SetHarvested_Postfix(object __instance, IPlayer byPlayer)
        {
            try
            {
                // Only process on server
                if (byPlayer == null) return;

                var serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer == null) return;

                // Call the Resourceful progression handler
                SimpleImprovingTraitsModSystem.ProcessAnimalHarvested(serverPlayer);
            }
            catch (Exception ex)
            {
                // Silently ignore errors to avoid breaking the game
                System.Diagnostics.Debug.WriteLine($"[SimpleImprovingTraits] Error in SetHarvested_Postfix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Server-side Harmony patches for sewing kit repairs (Mender trait).
    /// </summary>
    public static class SewingKitPatches
    {
        // Track which players have recently had repairs to avoid duplicate credits
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastRepairTime =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        // Track item durabilities to detect repairs
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> ItemDurabilities =
            new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        // Minimum interval between repair credits (in ticks, 20 ticks = 1 second)
        private const long MIN_REPAIR_INTERVAL = 10;

        /// <summary>
        /// Postfix for ItemSewingKit.OnHeldInteractStop - tracks when sewing kit repair completes.
        /// </summary>
        public static void OnHeldInteractStop_Postfix(
            object __instance,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel)
        {
            try
            {
                // Only count if the repair actually happened (at least some time was spent)
                if (secondsUsed < 0.25f) return;

                // Get the player
                var playerEntity = byEntity as EntityPlayer;
                if (playerEntity == null) return;

                var player = playerEntity.Player as IServerPlayer;
                if (player == null) return;

                // Check cooldown to avoid duplicate credits
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID;

                if (LastRepairTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < MIN_REPAIR_INTERVAL * 50)
                {
                    return; // Too soon since last credit
                }

                // Update last repair time and give credit
                LastRepairTime[playerKey] = currentTick;
                SimpleImprovingTraitsModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleImprovingTraits] Error in OnHeldInteractStop_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnModifiedInInventorySlot - tracks durability changes.
        /// When a wearable item's durability increases, it's likely due to a repair.
        /// </summary>
        public static void OnModifiedInInventorySlot_Postfix(
            CollectibleObject __instance,
            IWorldAccessor world,
            ItemSlot slot,
            ItemStack extractedStack)
        {
            try
            {
                // Only process server-side
                if (world?.Side != EnumAppSide.Server) return;

                // Only track wearable items (clothing and armor)
                string itemCode = __instance.Code?.ToString();
                if (itemCode == null) return;
                if (!itemCode.Contains("clothes-") && !itemCode.Contains("armor-")) return;

                // Get the current durability
                var currentStack = slot?.Itemstack;
                if (currentStack == null) return;

                int currentDurability = currentStack.Collectible?.GetRemainingDurability(currentStack) ?? 0;
                int maxDurability = currentStack.Collectible?.GetMaxDurability(currentStack) ?? 1;

                // Create a unique key for this item instance
                string itemKey = $"{slot.Inventory?.InventoryID}_{slot.Inventory?.GetSlotId(slot)}_{itemCode}";

                // Check if durability increased (repair happened)
                if (ItemDurabilities.TryGetValue(itemKey, out int previousDurability))
                {
                    if (currentDurability > previousDurability)
                    {
                        // Durability increased - repair happened!
                        // Try to find which player owns this inventory
                        var inventory = slot.Inventory;
                        if (inventory != null)
                        {
                            // Find player by checking if this is a character or backpack inventory
                            foreach (var player in world.AllOnlinePlayers)
                            {
                                var serverPlayer = player as IServerPlayer;
                                if (serverPlayer?.InventoryManager == null) continue;

                                // Check if this inventory belongs to this player
                                var characterInv = serverPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                                var backpackInv = serverPlayer.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);

                                if (characterInv == inventory || backpackInv == inventory)
                                {
                                    // Check cooldown
                                    long currentTick = world.ElapsedMilliseconds;
                                    string playerKey = serverPlayer.PlayerUID + "_mod";

                                    if (!LastRepairTime.TryGetValue(playerKey, out long lastTime) ||
                                        currentTick - lastTime >= MIN_REPAIR_INTERVAL * 50)
                                    {
                                        LastRepairTime[playerKey] = currentTick;
                                        SimpleImprovingTraitsModSystem.ProcessMenderRepair(serverPlayer);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Update tracked durability
                ItemDurabilities[itemKey] = currentDurability;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleImprovingTraits] Error in OnModifiedInInventorySlot_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnHeldInteractStep - tracks sewing kit repairs during use.
        /// This is a fallback for when the sewing kit is used in a world interaction context.
        /// </summary>
        public static void OnHeldInteractStep_Postfix(
            CollectibleObject __instance,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool __result)
        {
            try
            {
                // Only process if interaction is still ongoing
                if (!__result) return;

                // Check if this is a sewing kit
                string itemCode = __instance.Code?.ToString();
                if (itemCode == null || !itemCode.Contains("sewingkit")) return;

                // Get the player
                var playerEntity = byEntity as EntityPlayer;
                if (playerEntity == null) return;

                var player = playerEntity.Player as IServerPlayer;
                if (player == null) return;

                // Give credit every 0.5 seconds of repair (rate-limited)
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID + "_step";

                if (LastRepairTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < 500) // 500ms cooldown
                {
                    return;
                }

                // Update last repair time and give credit
                LastRepairTime[playerKey] = currentTick;
                SimpleImprovingTraitsModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleImprovingTraits] Error in OnHeldInteractStep_Postfix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Server-side Harmony patches for translocator repairs (Technical trait).
    /// </summary>
    public static class TranslocatorPatches
    {
        /// <summary>
        /// Postfix for BlockEntityStaticTranslocator.DoRepair - tracks when player repairs a translocator.
        /// </summary>
        public static void DoRepair_Postfix(object __instance, IPlayer byPlayer)
        {
            try
            {
                // Only process on server
                if (byPlayer == null) return;

                var serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer == null) return;

                // Get the repairState and RepairInteractionsRequired via reflection
                var instanceType = __instance.GetType();
                var repairStateField = instanceType.GetField("repairState",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var repairRequiredField = instanceType.GetField("RepairInteractionsRequired",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (repairStateField == null || repairRequiredField == null) return;

                int repairState = (int)repairStateField.GetValue(__instance);
                int repairRequired = (int)repairRequiredField.GetValue(__instance);

                // Check if this repair just completed (FullyRepaired is now true)
                if (repairState >= repairRequired)
                {
                    SimpleImprovingTraitsModSystem.ProcessTranslocatorRepair(serverPlayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleImprovingTraits] Error in DoRepair_Postfix: {ex.Message}");
            }
        }
    }
}
