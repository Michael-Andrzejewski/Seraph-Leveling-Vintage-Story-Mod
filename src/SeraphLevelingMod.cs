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

namespace SeraphLeveling
{
    /// <summary>
    /// Configuration class for SeraphLeveling mod.
    /// Edit ModConfig/SeraphLeveling.json to change these values.
    /// </summary>
    public class SeraphLevelingConfig
    {
        // Mining progression
        public int MiningBaseBlocksPerIncrement { get; set; } = 100;
        public int MiningIncrementStep { get; set; } = 100;
        public int MiningMaxPercent { get; set; } = 50;
        public int MiningOreMultiplier { get; set; } = 5;

        // Melee progression
        public int MeleeBaseDamagePerIncrement { get; set; } = 100;
        public int MeleeIncrementStep { get; set; } = 100;
        public int MeleeMaxPercent { get; set; } = 50;

        // Ranged progression
        public int RangedBaseDamagePerIncrement { get; set; } = 100;
        public int RangedIncrementStep { get; set; } = 100;
        public int RangedMaxDamagePercent { get; set; } = 50;
        public int RangedMaxAccuracyPercent { get; set; } = 50;
        public int RangedMaxDistancePercent { get; set; } = 50;

        // Walking progression
        public int WalkingBaseBlocksPerIncrement { get; set; } = 1000;
        public int WalkingIncrementStep { get; set; } = 1000;
        public int WalkingMaxPercent { get; set; } = 15;

        // Hunger progression
        public int HungerBaseSecondsPerIncrement { get; set; } = 300;
        public int HungerIncrementStep { get; set; } = 60;
        public int HungerMaxReductionPercent { get; set; } = 25;

        // Armor progression
        public int ArmorBaseSecondsPerIncrement { get; set; } = 2880;
        public int ArmorTimeIncrementStep { get; set; } = 2880;
        public int ArmorBaseDamageBlockedPerIncrement { get; set; } = 100;
        public int ArmorDamageIncrementStep { get; set; } = 100;
        public int ArmorBaseRepairsPerIncrement { get; set; } = 1;
        public int ArmorRepairIncrementStep { get; set; } = 1;
        public int ArmorMaxDurabilityPercent { get; set; } = 50;
        public int ArmorMaxWalkSpeedPercent { get; set; } = 50;

        // Clothier progression
        public int ClothierRequiredUniqueClothes { get; set; } = 20;

        // Mender progression
        public int MenderBaseRepairsPerIncrement { get; set; } = 5;
        public int MenderIncrementStep { get; set; } = 1;
        public int MenderMaxPercent { get; set; } = 20;

        // Pilferer progression
        public int PilfererBasePointsPerIncrement { get; set; } = 10;
        public int PilfererIncrementStep { get; set; } = 10;
        public int PilfererMaxPercent { get; set; } = 20;

        // Resourceful progression
        public int ResourcefulBaseAnimalsPerIncrement { get; set; } = 10;
        public int ResourcefulIncrementStep { get; set; } = 10;
        public int ResourcefulMaxLootPercent { get; set; } = 20;
        public int ResourcefulMaxSpeedPercent { get; set; } = 25;

        // Forager progression
        public int ForagerBaseCropsPerIncrement { get; set; } = 10;
        public int ForagerIncrementStep { get; set; } = 10;
        public int ForagerMaxLootPercent { get; set; } = 20;
        public int ForagerMaxWildCropPercent { get; set; } = 20;

        // Furtive progression
        public int FurtiveBaseSneakBlocksPerIncrement { get; set; } = 100;
        public int FurtiveIncrementStep { get; set; } = 100;
        public int FurtiveMaxPercent { get; set; } = 35;

        // Precise progression
        public int PreciseBaseDamagePerIncrement { get; set; } = 100;
        public int PreciseIncrementStep { get; set; } = 100;
        public int PreciseMaxPercent { get; set; } = 30;

        // Technical progression
        public int TechnicalRequiredTranslocatorRepairs { get; set; } = 5;

        // Hardy Health progression
        public int HardyHealthMiningThreshold { get; set; } = 110;
        public int HardyHealthArmorDurabilityThreshold { get; set; } = 10;
        public int HardyHealthBonus { get; set; } = 5;

        // Auto-save settings
        /// <summary>
        /// Interval in seconds for automatic progress saving. Default 300 (5 minutes).
        /// Set to 0 to disable auto-save (saves only on world save).
        /// </summary>
        public int AutoSaveIntervalSeconds { get; set; } = 300;

        // Disabled skills
        /// <summary>
        /// List of skills to disable. Disabled skills won't track XP or apply bonuses.
        /// Valid values: mining, melee, ranged, walking, hunger, armor, clothier, mender,
        /// pilferer, resourceful, forager, furtive, precise, technical, hardyhealth
        /// </summary>
        public string[] DisabledSkills { get; set; } = Array.Empty<string>();

        // =========================================================================
        // COMBAT OVERHAUL COMPATIBILITY SETTINGS
        // These only apply when Combat Overhaul mod is installed
        // =========================================================================

        /// <summary>
        /// Enable Combat Overhaul compatibility features when CO is installed.
        /// </summary>
        public bool EnableCombatOverhaulCompat { get; set; } = true;

        /// <summary>
        /// Base damage needed for the first proficiency credit.
        /// </summary>
        public int COProficiencyBaseDamagePerIncrement { get; set; } = 100;

        /// <summary>
        /// Additional damage needed per subsequent credit (100, 200, 300...).
        /// </summary>
        public int COProficiencyIncrementStep { get; set; } = 100;

        // Proficiency max values (matching CO trait defaults)
        public float COBowsProficiencyMax { get; set; } = 0.5f;
        public float COCrossbowsProficiencyMax { get; set; } = 0.5f;
        public float COFirearmsProficiencyMax { get; set; } = 0.5f;
        public float COSlingsProficiencyMax { get; set; } = 0.3f;
        public float COOneHandedSwordsProficiencyMax { get; set; } = 0.3f;
        public float COTwoHandedSwordsProficiencyMax { get; set; } = 0.3f;
        public float COSpearsProficiencyMax { get; set; } = 0.3f;
        public float COJavelinsProficiencyMax { get; set; } = 0.3f;
        public float COMacesProficiencyMax { get; set; } = 0.3f;
        public float COClubsProficiencyMax { get; set; } = 0.3f;
        public float COHalberdsProficiencyMax { get; set; } = 0.3f;
        public float COAxesProficiencyMax { get; set; } = 0.3f;
        public float COQuarterstaffProficiencyMax { get; set; } = 0.3f;

        /// <summary>
        /// Max Steady Aim bonus (earned alongside ranged proficiencies).
        /// </summary>
        public float COSteadyAimMax { get; set; } = 0.5f;
    }

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

        public PilfererProgressData()
        {
            TotalCredits = 0;
            PointsInIncrement = 0;
            CurrentIncrementSize = 10; // Base increment size
        }

        public PilfererProgressData Clone()
        {
            return new PilfererProgressData
            {
                TotalCredits = this.TotalCredits,
                PointsInIncrement = this.PointsInIncrement,
                CurrentIncrementSize = this.CurrentIncrementSize
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
        public bool HasCOFrightenedOfMelee { get; set; }
    }

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

        public COPlayerProgressData()
        {
            Proficiencies = new Dictionary<string, COProficiencyProgressData>();
            SteadyAimCredits = 0;
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
                SteadyAimCredits = this.SteadyAimCredits
            };
            foreach (var kvp in this.Proficiencies)
            {
                clone.Proficiencies[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }

    /// <summary>
    /// Simple struct for tracking 2D positions without allocating Vec3d objects.
    /// Used in walking/sneaking tick handlers to avoid GC pressure.
    /// </summary>
    public struct Position2D
    {
        public double X;
        public double Z;

        public Position2D(double x, double z)
        {
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Main mod system for Simple Improving Traits.
    /// Provides a progression system that improves player traits through gameplay.
    /// Currently implements mining speed progression based on blocks mined.
    /// </summary>
    public class SeraphLevelingModSystem : ModSystem
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
        public static int MaxMiningSpeedPercent = 50;    // 50% max bonus
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
        public static int MaxMeleeDamagePercent = 50;     // 50% max bonus

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
        public static int MaxRangedDamagePercent = 50;           // 50% max bonus for damage
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

        // Tracking last known positions for walking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastPlayerPositions = new ConcurrentDictionary<string, Position2D>();

        // Maximum distance per tick to count (prevents teleportation from counting)
        private const float MAX_DISTANCE_PER_TICK = 10f;

        // Cache for vanilla trait checks - populated once on player join
        private static ConcurrentDictionary<string, CachedVanillaTraits> VanillaTraitsCache = new ConcurrentDictionary<string, CachedVanillaTraits>();

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
        public const string WATCHED_RAVENOUS_REMAINING = "sitRavenousRemaining";

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
        // Time-based progression: 1 VS day (48 min) base, +1 VS day increment per credit (gives -1% walk speed penalty per credit)
        public static int BaseSecondsInArmorPerIncrement = 2880;  // Base seconds (1 VS day = 48 min) for first credit
        public static int ArmorTimeIncrementStep = 2880;          // How many more seconds each subsequent credit needs (1 VS day)

        // Damage-based progression: 100 damage base, +100 increment per credit (gives +1% durability per credit)
        public static int BaseDamageBlockedPerIncrement = 100;     // Base damage blocked for first credit
        public static int ArmorDamageIncrementStep = 100;          // How much more damage each subsequent credit needs

        // Repair-based progression: 1 repair base, +1 increment per credit (gives +1% durability per credit)
        public static int BaseRepairsPerIncrement = 1;             // Base repairs for first credit
        public static int ArmorRepairIncrementStep = 1;            // How many more repairs each subsequent credit needs

        // First-equip bonuses (durability):
        // +1% for light armor and chain, +2% for brigandine, +3% for scale and plate
        public const int FIRST_EQUIP_LIGHT_BONUS = 1;
        public const int FIRST_EQUIP_CHAIN_BONUS = 1;
        public const int FIRST_EQUIP_BRIGANDINE_BONUS = 2;
        public const int FIRST_EQUIP_SCALE_BONUS = 3;
        public const int FIRST_EQUIP_PLATE_BONUS = 3;

        // First-equip bonuses (walk speed penalty reduction):
        // Same values as durability - grants walk speed bonus on first equip
        public const int FIRST_EQUIP_WALKSPEED_LIGHT_BONUS = 1;
        public const int FIRST_EQUIP_WALKSPEED_CHAIN_BONUS = 1;
        public const int FIRST_EQUIP_WALKSPEED_BRIGANDINE_BONUS = 2;
        public const int FIRST_EQUIP_WALKSPEED_SCALE_BONUS = 3;
        public const int FIRST_EQUIP_WALKSPEED_PLATE_BONUS = 3;

        // Max bonuses
        public static int MaxArmorDurabilityPercent = 50;          // 50% max armor durability bonus
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
        public const int PILFERER_VESSEL_POINTS = 2;            // Points per broken loot vessel

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

        // Tracking last known positions for sneaking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastSneakingPositions = new ConcurrentDictionary<string, Position2D>();

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

        // =========================================================================
        // NEGATIVE TRAIT CONSTANTS - Used for cancellation calculations
        // =========================================================================

        // Farsighted (Hunter): -15% melee damage
        public const int VANILLA_FARSIGHTED_MELEE_PENALTY = 15;
        public const string WATCHED_FARSIGHTED_REMAINING = "sitFarsightedRemaining";

        // Nervous (Malefactor, Clockmaker): -15% melee damage
        public const int VANILLA_NERVOUS_MELEE_PENALTY = 15;
        public const string WATCHED_NERVOUS_REMAINING = "sitNervousRemaining";

        // Nearsighted (Blackguard): -15% ranged damage
        public const int VANILLA_NEARSIGHTED_RANGED_PENALTY = 15;
        public const string WATCHED_NEARSIGHTED_REMAINING = "sitNearsightedRemaining";

        // Frail (Malefactor, Clockmaker): -2.5 HP, -25% ranged distance
        public const float VANILLA_FRAIL_HP_PENALTY = 2.5f;
        public const int VANILLA_FRAIL_DISTANCE_PENALTY = 25;
        public const string WATCHED_FRAIL_HP_REMAINING = "sitFrailHpRemaining";
        public const string WATCHED_FRAIL_DISTANCE_REMAINING = "sitFrailDistanceRemaining";
        public const string FRAIL_HP_CANCEL_STAT_CODE = "sitFrailHpCancel";

        // Civil (Tailor): -10% loot from foraging
        public const int VANILLA_CIVIL_FORAGING_PENALTY = 10;
        public const string WATCHED_CIVIL_REMAINING = "sitCivilRemaining";

        // Weak (Tailor): -2 HP, -10% mining speed
        public const int VANILLA_WEAK_HP_PENALTY = 2;
        public const int VANILLA_WEAK_MINING_PENALTY = 10;
        public const string WATCHED_WEAK_HP_REMAINING = "sitWeakHpRemaining";
        public const string WATCHED_WEAK_MINING_REMAINING = "sitWeakMiningRemaining";
        public const string WEAK_HP_CANCEL_STAT_CODE = "sitWeakHpCancel";

        // Kind (Tailor): -10% animal loot, -25% harvesting speed
        public const int VANILLA_KIND_LOOT_PENALTY = 10;
        public const int VANILLA_KIND_SPEED_PENALTY = 25;
        public const string WATCHED_KIND_LOOT_REMAINING = "sitKindLootRemaining";
        public const string WATCHED_KIND_SPEED_REMAINING = "sitKindSpeedRemaining";

        // Heavyhanded (Blackguard): -10% vessel loot, -15% foraging, -20% wild crop
        public const int VANILLA_HEAVYHANDED_VESSEL_PENALTY = 10;
        public const int VANILLA_HEAVYHANDED_FORAGING_PENALTY = 15;
        public const int VANILLA_HEAVYHANDED_WILD_CROP_PENALTY = 20;
        public const string WATCHED_HEAVYHANDED_VESSEL_REMAINING = "sitHeavyhandedVesselRemaining";
        public const string WATCHED_HEAVYHANDED_FORAGING_REMAINING = "sitHeavyhandedForagingRemaining";
        public const string WATCHED_HEAVYHANDED_WILD_CROP_REMAINING = "sitHeavyhandedWildCropRemaining";

        // Claustrophobic (Hunter): -15% ore drop, -10% mining speed - already defined above
        public const int VANILLA_CLAUSTROPHOBIC_ORE_PENALTY = 15;
        public const int VANILLA_CLAUSTROPHOBIC_MINING_PENALTY = 10;
        public const string WATCHED_CLAUSTROPHOBIC_ORE_REMAINING = "sitClaustrophobicOreRemaining";
        public const string WATCHED_CLAUSTROPHOBIC_MINING_REMAINING = "sitClaustrophobicMiningRemaining";

        private const string CONFIG_SAVE_KEY = "sitConfig";
        private const string CONFIG_FILE_NAME = "SeraphLeveling.json";

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

        // Auto-save configuration
        public static int AutoSaveIntervalSeconds = 300;  // Default 5 minutes
        private static long autoSaveTimerId = 0;

        // Disabled skills set for quick lookup (lowercase)
        public static HashSet<string> DisabledSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =========================================================================
        // COMBAT OVERHAUL COMPATIBILITY
        // =========================================================================

        /// <summary>Whether Combat Overhaul mod is loaded.</summary>
        public static bool IsCombatOverhaulLoaded { get; private set; } = false;

        /// <summary>Whether CO compatibility is enabled (mod loaded AND config enabled).</summary>
        public static bool IsCOCompatEnabled => IsCombatOverhaulLoaded && COEnableCompat;

        // CO configuration values (loaded from config)
        public static bool COEnableCompat = true;
        public static int COBaseDamagePerIncrement = 100;
        public static int COIncrementStep = 100;
        public static float COBowsProficiencyMax = 0.5f;
        public static float COCrossbowsProficiencyMax = 0.5f;
        public static float COFirearmsProficiencyMax = 0.5f;
        public static float COSlingsProficiencyMax = 0.3f;
        public static float COOneHandedSwordsProficiencyMax = 0.3f;
        public static float COTwoHandedSwordsProficiencyMax = 0.3f;
        public static float COSpearsProficiencyMax = 0.3f;
        public static float COJavelinsProficiencyMax = 0.3f;
        public static float COMacesProficiencyMax = 0.3f;
        public static float COClubsProficiencyMax = 0.3f;
        public static float COHalberdsProficiencyMax = 0.3f;
        public static float COAxesProficiencyMax = 0.3f;
        public static float COQuarterstaffProficiencyMax = 0.3f;
        public static float COSteadyAimMax = 0.5f;

        // CO proficiency stat names (must match Combat Overhaul's stat names)
        public const string CO_BOWS_PROFICIENCY = "bowsProficiency";
        public const string CO_CROSSBOWS_PROFICIENCY = "crossbowsProficiency";
        public const string CO_FIREARMS_PROFICIENCY = "firearmsProficiency";
        public const string CO_SLINGS_PROFICIENCY = "slingsProficiency";
        public const string CO_ONE_HANDED_SWORDS_PROFICIENCY = "oneHandedSwordsProficiency";
        public const string CO_TWO_HANDED_SWORDS_PROFICIENCY = "twoHandedSwordsProficiency";
        public const string CO_SPEARS_PROFICIENCY = "spearsProficiency";
        public const string CO_JAVELINS_PROFICIENCY = "javelinsProficiency";
        public const string CO_MACES_PROFICIENCY = "macesProficiency";
        public const string CO_CLUBS_PROFICIENCY = "clubsProficiency";
        public const string CO_HALBERDS_PROFICIENCY = "halberdsProficiency";
        public const string CO_AXES_PROFICIENCY = "axesProficiency";
        public const string CO_QUARTERSTAFF_PROFICIENCY = "quarterstaffProficiency";
        public const string CO_STEADY_AIM = "steadyAim";

        // CO negative trait penalty values
        public const float CO_TREMBLING_AIM_PENALTY = 0.3f;
        public const float CO_CLUMSY_HANDS_PENALTY = 0.3f;
        public const int CO_FRIGHTENED_MELEE_TIER_PENALTY = 1;

        // WatchedAttributes keys for CO (client sync)
        public const string WATCHED_CO_STEADY_AIM_CREDITS = "sitCOSteadyAimCredits";
        public const string WATCHED_CO_TREMBLING_AIM_REMAINING = "sitCOTremblingAimRemaining";
        public const string WATCHED_CO_CLUMSY_HANDS_REMAINING = "sitCOClumsyHandsRemaining";
        public const string WATCHED_CO_FRIGHTENED_MELEE_REMAINING = "sitCOFrightenedMeleeRemaining";

        // CO stat codes (prefixed to avoid collisions)
        public const string CO_STAT_PREFIX = "sitCO";

        // CO persistence
        private const string CO_PROGRESS_SAVE_KEY = "sitCOProgress";

        // Storage for CO progress - keyed by player UID
        public static ConcurrentDictionary<string, COPlayerProgressData> COProgress = new ConcurrentDictionary<string, COPlayerProgressData>();

        // Flag to indicate pending CO progress save
        private static volatile bool pendingCOProgressSave = false;

        /// <summary>
        /// All CO proficiency stat names for iteration.
        /// </summary>
        public static readonly string[] AllCOProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY,
            CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY, CO_SPEARS_PROFICIENCY,
            CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY, CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY,
            CO_AXES_PROFICIENCY, CO_QUARTERSTAFF_PROFICIENCY
        };

        /// <summary>
        /// Ranged proficiencies that also contribute to Steady Aim.
        /// </summary>
        public static readonly string[] CORangedProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY
        };

        /// <summary>
        /// Check if a skill is disabled in the config.
        /// </summary>
        public static bool IsSkillDisabled(string skillName)
        {
            return DisabledSkills.Contains(skillName);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;

            // Load config file (sets defaults for new worlds)
            LoadConfigFile(api);

            // Detect Combat Overhaul mod
            DetectCombatOverhaul(api);

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
                    .WithDescription("Get or set your mining level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your melee level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your ranged level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your walking level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your hunger level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your armor durability level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedlevel")
                    .WithDescription("Get or set your armor walk speed penalty reduction level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                .BeginSubCommand("testwalkspeed")
                    .WithDescription("Apply a test walk speed modifier (admin only, use 0 to clear)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestWalkSpeedCommand)
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
                    .WithDescription("Get or set your clothier progress (unique clothes count) (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your mender level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your pilferer level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your resourceful level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your forager level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your furtive level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                    .WithDescription("Get or set your precise level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
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
                .EndSubCommand()
                // Reset all traits
                .BeginSubCommand("reset")
                    .WithDescription("Reset all trait progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResetCommand)
                .EndSubCommand()
                // Reset all config values to defaults
                .BeginSubCommand("resetconfig")
                    .WithDescription("Reset all trait config values (base, increment, max) to defaults (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResetConfigCommand)
                .EndSubCommand()
                // Max all traits for testing
                .BeginSubCommand("maxall")
                    .WithDescription("Set all trait progression to maximum for testing (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMaxAllCommand)
                .EndSubCommand()
                // Test suite command
                .BeginSubCommand("testsuite")
                    .WithDescription("Run automated tests for trait calculations")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("category"))
                    .HandleWith(OnTraitTestSuiteCommand)
                .EndSubCommand()
                // Combat Overhaul proficiency commands
                .BeginSubCommand("coproficiency")
                    .WithDescription("View all Combat Overhaul proficiency progression (requires CO mod)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOProficiencyCommand)
                .EndSubCommand()
                .BeginSubCommand("colevel")
                    .WithDescription("Set Combat Overhaul proficiency credits (admin only). Usage: /trait colevel <proficiency> <credits>")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.Word("proficiency"), api.ChatCommands.Parsers.Int("credits"))
                    .HandleWith(OnTraitCOLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("coreset")
                    .WithDescription("Reset all Combat Overhaul progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOResetCommand)
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
            api.Event.SaveGameLoaded += LoadCOProgress;

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

            // Register auto-save timer if enabled
            if (AutoSaveIntervalSeconds > 0)
            {
                autoSaveTimerId = api.Event.RegisterGameTickListener(OnAutoSaveTick, AutoSaveIntervalSeconds * 1000);
                api.Logger.Notification($"[SeraphLeveling] Auto-save enabled every {AutoSaveIntervalSeconds} seconds");
            }

            // Hook into player disconnect to clean up position tracking and save data
            api.Event.PlayerDisconnect += OnPlayerDisconnect;

            api.Logger.Notification("[SeraphLeveling] Mod loaded");
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
                "  /trait mininglevel [level] - Get or set your mining level (admin)\n" +
                "  /trait miningmax [percent] - Get or set max mining speed bonus (admin)\n" +
                "  /trait melee - View your melee damage progression stats\n" +
                "  /trait meleebase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait meleeincrement [value] - Get or set melee increment step per credit (admin)\n" +
                "  /trait meleelevel [level] - Get or set your melee level (admin)\n" +
                "  /trait meleemax [percent] - Get or set max melee damage bonus (admin)\n" +
                "  /trait ranged - View your ranged damage progression stats\n" +
                "  /trait rangedbase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait rangedincrement [value] - Get or set ranged increment step per credit (admin)\n" +
                "  /trait rangedlevel [level] - Get or set your ranged level (admin)\n" +
                "  /trait rangedmax [percent] - Get or set max ranged damage bonus (admin)\n" +
                "  /trait rangedmaxacc [percent] - Get or set max ranged accuracy bonus (admin)\n" +
                "  /trait rangedmaxdist [percent] - Get or set max ranged distance bonus (admin)\n" +
                "  /trait walking - View your walking speed progression stats\n" +
                "  /trait walkingbase [value] - Get or set base blocks for first credit (admin)\n" +
                "  /trait walkingincrement [value] - Get or set walking increment step per credit (admin)\n" +
                "  /trait walkinglevel [level] - Get or set your walking level (admin)\n" +
                "  /trait walkingmax [percent] - Get or set max walking speed bonus (admin)\n" +
                "  /trait hunger - View your hunger rate progression stats\n" +
                "  /trait hungerbase [value] - Get or set base seconds for first credit (admin)\n" +
                "  /trait hungerincrement [value] - Get or set hunger increment step per credit (admin)\n" +
                "  /trait hungerlevel [level] - Get or set your hunger level (admin)\n" +
                "  /trait hungermax [percent] - Get or set max hunger rate reduction (admin)\n" +
                "  /trait armor - View your armor progression stats\n" +
                "  /trait armorlevel [level] - Get or set your armor durability level (admin)\n" +
                "  /trait armorwalkspeedlevel [level] - Get or set walk speed penalty reduction level (admin)\n" +
                "  /trait armordurabilitymax [percent] - Get or set max durability bonus (admin)\n" +
                "  /trait armorwalkspeedmax [percent] - Get or set max walk speed reduction (admin)\n" +
                "  /trait reset - Reset all trait progression to 0 (admin)\n" +
                "  /trait resetconfig - Reset all config values to defaults (admin)\n" +
                "  /trait maxall - Set all trait progression to maximum for testing (admin)");
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
            int maxCredits = GetMaxMiningCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mining progression: {currentCredits}% / {maxCredits}%");
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

            if (currentCredits >= maxCredits)
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
        /// Gets or sets the player's mining credits (level) directly.
        /// Note: Setting resets all per-pickaxe progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMiningLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = GetMaxMiningCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateMiningBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current mining level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% mining speed)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > maxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits})");
            }

            // Set the player's progress (clears per-pickaxe progress)
            progress.TotalCredits = newCredits.Value;
            progress.PickaxeProgress.Clear(); // Reset all pickaxe progress

            pendingMiningProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyMiningBonus(player, newCredits.Value);

            return TextCommandResult.Success($"Mining credits set to {newCredits.Value} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
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
            int maxCredits = GetMaxMeleeCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Melee progression: {currentCredits}% / {maxCredits}%");
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

            if (currentCredits >= maxCredits)
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
        /// Gets or sets the player's melee credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMeleeLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Get the player-specific max credits (accounts for Farsighted/Nervous penalties)
            int maxCredits = GetMaxMeleeCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateMeleeBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current melee level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% melee damage)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > maxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits})");
            }

            // Set the player's progress (clears per-weapon progress)
            progress.TotalCredits = newCredits.Value;
            progress.WeaponProgress.Clear(); // Reset all weapon progress

            pendingMeleeProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyMeleeBonusStatic(player, newCredits.Value);

            // Check for trait unlocks that depend on melee level
            CheckMercilessUnlock(player);

            return TextCommandResult.Success($"Melee credits set to {newCredits.Value} (+{bonusPercent}% melee damage). Per-weapon progress reset.");
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
            int maxCredits = GetMaxRangedCredits(player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Ranged progression: {currentCredits} credits / {maxCredits} max");
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

            if (currentCredits >= maxCredits)
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
        /// Gets or sets the player's ranged credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitRangedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Get the player-specific max credits (accounts for Nearsighted/Frail penalties)
            int maxCredits = GetMaxRangedCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current ranged level: {progress.TotalCredits}/{maxCredits} (+{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > maxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits})");
            }

            // Set the player's progress (clears per-weapon progress)
            progress.TotalCredits = newCredits.Value;
            progress.WeaponProgress.Clear(); // Reset all weapon progress

            pendingRangedProgressSave = true;

            // Apply the bonus
            var (newDamageBonus, newAccuracyBonus, newDistanceBonus) = ApplyRangedBonusStatic(player, newCredits.Value);

            return TextCommandResult.Success($"Ranged credits set to {newCredits.Value} (+{newDamageBonus}% damage, +{newAccuracyBonus}% accuracy, +{newDistanceBonus}% distance). Per-weapon progress reset.");
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
        /// Gets or sets the player's walking credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitWalkingLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateWalkingBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current walking level: {progress.TotalCredits}/{MaxWalkingSpeedPercent} (+{currentBonus}% walk speed)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxWalkingSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxWalkingSpeedPercent})");
            }

            // Set the player's progress
            progress.TotalCredits = newCredits.Value;
            progress.BlocksInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseBlocksWalkedPerIncrement + (newCredits.Value * WalkingIncrementStep);

            pendingWalkingProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyWalkingBonusStatic(player, newCredits.Value);

            return TextCommandResult.Success($"Walking credits set to {newCredits.Value} (+{bonusPercent}% walk speed).");
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
        /// Gets or sets the player's hunger credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitHungerLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Calculate player-specific max credits
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                bool hasRavenousCurrent = PlayerHasVanillaRavenousStatic(player.Entity);
                int currentEffectiveRate = hasRavenousCurrent ? (130 - progress.TotalCredits) : (100 - progress.TotalCredits);
                return TextCommandResult.Success($"Current hunger level: {progress.TotalCredits}/{playerMaxCredits} (-{progress.TotalCredits}% hunger rate, effective rate: {currentEffectiveRate}%)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > playerMaxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max for this player ({playerMaxCredits})");
            }

            // Set the player's progress
            progress.TotalCredits = newCredits.Value;
            progress.SecondsInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseSecondsPerIncrement + (newCredits.Value * HungerIncrementStep);

            pendingHungerProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyHungerBonusStatic(player, newCredits.Value);

            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity);
            int effectiveRate = hasRavenous ? (130 - bonusPercent) : (100 - bonusPercent);

            return TextCommandResult.Success($"Hunger credits set to {newCredits.Value}/{playerMaxCredits} (-{bonusPercent}% hunger rate, effective rate: {effectiveRate}%).");
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
        /// Gets or sets the player's armor durability credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity);
                return TextCommandResult.Success($"Current armor durability level: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent} (+{currentBonus}% durability)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxArmorDurabilityPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorDurabilityPercent})");
            }

            progress.TotalDurabilityCredits = newCredits.Value;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorDurabilityBonusPercent(newCredits.Value, player.Entity);

            // Check for trait unlocks that depend on armor durability
            CheckHardyHealthUnlock(player);
            CheckMercilessUnlock(player);

            return TextCommandResult.Success($"Armor durability credits set to {newCredits.Value} (+{bonusPercent}% durability).");
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedlevel command.
        /// Gets or sets the player's armor walk speed penalty reduction credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity);
                return TextCommandResult.Success($"Current armor walk speed penalty reduction level: {progress.TotalWalkSpeedCredits}/{MaxArmorWalkSpeedPercent} (-{currentBonus}% penalty)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxArmorWalkSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorWalkSpeedPercent})");
            }

            progress.TotalWalkSpeedCredits = newCredits.Value;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorWalkSpeedBonusPercent(newCredits.Value, player.Entity);

            return TextCommandResult.Success($"Armor walk speed penalty reduction credits set to {newCredits.Value} (-{bonusPercent}% penalty).");
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
        /// Handler for /trait testwalkspeed command.
        /// Applies a test armor walk speed penalty reduction (positive = less penalty, 0 = clear).
        /// </summary>
        private TextCommandResult OnTraitTestWalkSpeedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Player entity not found");
            }

            int? percent = (int?)args[0];

            if (!percent.HasValue)
            {
                return TextCommandResult.Success("Usage: /trait testwalkspeed <percent>\nExample: /trait testwalkspeed 99 (reduces armor penalty by 99%)\nUse 0 to clear the test modifier.");
            }

            if (percent.Value == 0)
            {
                player.Entity.Stats["armorWalkSpeedAffectedness"].Remove("sitTestPenalty");

                // Force WearableStats to recalculate
                var clearInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (clearInv != null)
                {
                    foreach (var slot in clearInv)
                    {
                        if (slot?.Itemstack != null)
                        {
                            slot.MarkDirty();
                            break;
                        }
                    }
                }

                return TextCommandResult.Success("Test armor walk speed penalty modifier cleared.");
            }

            // armorWalkSpeedAffectedness: negative values reduce the penalty
            float reduction = -(percent.Value * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set("sitTestPenalty", reduction);

            // Debug: check blended value
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] Test command: set armorWalkSpeedAffectedness modifier to {reduction:F2}, blended value is now {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering a slot change on character inventory
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                // Trigger slot modified on first slot to force WearableStats recalculation
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
                ServerApi.Logger.Debug($"[SeraphLeveling] Triggered character inventory refresh to recalculate wearable stats");
            }

            return TextCommandResult.Success($"Applied {percent.Value}% armor walk speed penalty reduction (stat value: {reduction:F2}, blended: {blendedValue:F2}). Use '/trait testwalkspeed 0' to clear.");
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

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaRavenous = cache?.HasRavenous ?? PlayerHasVanillaRavenousStatic(player.Entity);

            // Calculate max credits this player can earn
            int maxCredits = CalculateMaxHungerCredits(player.Entity);

            // Calculate bonus from level (1% per level, capped at player's max)
            int cappedLevel = Math.Min(level, maxCredits);
            float bonus = cappedLevel * 0.01f;
            int bonusPercent = (int)(bonus * 100);

            // Calculate remaining Ravenous penalty (0 when fully cancelled at level 30)
            int ravenousRemaining = hasVanillaRavenous ? CalculateRemainingPenalty(VANILLA_RAVENOUS_HUNGER_PENALTY, level) : 0;

            // Always apply stats (they're not persistent)
            // Set the hunger rate stat - this value is ADDED to the base (1.0)
            // We want to REDUCE hunger rate, so we use a negative value
            player.Entity.Stats.Set("hungerrate", HUNGER_STAT_CODE, -bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_HUNGER_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_HUNGER_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_HUNGER_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_HUNGER_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaRavenous", hasVanillaRavenous);
                watchedAttrs.SetInt("sitMaxHungerCredits", maxCredits);
                watchedAttrs.SetInt(WATCHED_RAVENOUS_REMAINING, ravenousRemaining);

                // Add our trait to extraTraits (hunger mastery is unique, doesn't replace a vanilla trait)
                UpdateExtraTraitStatic(player.Entity, HUNGER_TRAIT_CODE, level > 0);

                // Only call MarkPathDirty once (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_HUNGER_LEVEL);
            }

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
        /// Get the maximum melee credits a player can earn based on their traits.
        /// Players with Farsighted or Nervous traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMeleeCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMeleeDamagePercent;

            bool hasFarsighted = PlayerHasVanillaFarsighted(entity);
            bool hasNervous = PlayerHasVanillaNervous(entity);

            // Farsighted penalty is 15% melee damage, need 15 extra levels to cancel it
            if (hasFarsighted)
            {
                return MaxMeleeDamagePercent + VANILLA_FARSIGHTED_MELEE_PENALTY;
            }

            // Nervous penalty is 15% melee damage, need 15 extra levels to cancel it
            if (hasNervous)
            {
                return MaxMeleeDamagePercent + VANILLA_NERVOUS_MELEE_PENALTY;
            }

            return MaxMeleeDamagePercent;
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
        /// Get the maximum ranged credits a player can earn based on their traits.
        /// Players with Nearsighted or Frail traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxRangedCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxRangedDamagePercent;

            bool hasNearsighted = PlayerHasVanillaNearsighted(entity);
            bool hasFrail = PlayerHasVanillaFrail(entity);

            // Use the larger penalty to determine max credits
            int extraCredits = 0;

            // Nearsighted penalty is 15% ranged damage, need 15 extra levels to cancel it
            if (hasNearsighted)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }

            // Frail penalty is 25% ranged distance, need 25 extra levels to cancel it
            if (hasFrail)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            return MaxRangedDamagePercent + extraCredits;
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

        // =========================================================================
        // NEGATIVE TRAIT DETECTION METHODS
        // =========================================================================

        /// <summary>
        /// Checks if the player's class has the vanilla Farsighted trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaFarsighted(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("farsighted", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Nervous trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaNervous(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("nervous", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Nearsighted trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaNearsighted(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("nearsighted", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Frail trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaFrail(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("frail", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Civil trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaCivil(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("civil", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Weak trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaWeak(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("weak", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Kind trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaKind(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("kind", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Heavyhanded trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaHeavyhanded(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("heavyhanded", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Claustrophobic trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaClaustrophobic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("claustrophobic", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calculate the remaining penalty for a negative trait after applying progression bonus.
        /// Returns the remaining penalty (0 or positive), or 0 if fully cancelled.
        /// </summary>
        public static int CalculateRemainingPenalty(int basePenalty, int progressionBonus)
        {
            return Math.Max(0, basePenalty - progressionBonus);
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
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static void ApplyArmorBonusesStatic(IServerPlayer player, int durabilityCredits, int walkSpeedCredits)
        {
            if (player?.Entity == null) return;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierForArmor(player.Entity);

            // Calculate durability bonus (reduces armor damage taken)
            int durabilityBonus = CalculateArmorDurabilityBonusPercent(durabilityCredits, player.Entity);
            // Calculate walk speed penalty reduction
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(walkSpeedCredits, player.Entity);

            // Always apply stats (they're not persistent and need to be set on every join)
            // armorDurabilityLoss is a multiplier, lower = less durability lost
            float durabilityMultiplier = 1f - (durabilityBonus * 0.01f);
            player.Entity.Stats.Set("armorDurabilityLoss", ARMOR_DURABILITY_STAT_CODE, durabilityMultiplier, false);

            // Reduce armor walk speed penalty using armorWalkSpeedAffectedness
            // Negative values reduce the penalty (e.g., -0.25 = 25% less armor penalty)
            // Base value is 1.0, so setting -0.5 gives 1.0 + (-0.5) = 0.5 (50% of penalty applied)
            float armorWalkSpeedReduction = -(walkSpeedBonus * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set(ARMOR_WALKSPEED_STAT_CODE, armorWalkSpeedReduction);

            // Debug: Log the stat values
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] armorWalkSpeedAffectedness: set modifier {armorWalkSpeedReduction:F2}, blended value {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering slot modified
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldDurabilityLevel = watchedAttrs.GetInt(WATCHED_ARMOR_DURABILITY_LEVEL, -1);
            int oldWalkSpeedLevel = watchedAttrs.GetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, -1);

            bool valuesChanged = (oldDurabilityLevel != durabilityCredits) || (oldWalkSpeedLevel != walkSpeedCredits);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_LEVEL, durabilityCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_BONUS, durabilityBonus);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, walkSpeedCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_BONUS, walkSpeedBonus);
                watchedAttrs.SetBool("sitHasVanillaSoldierArmor", hasVanillaSoldier);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, ARMOR_TRAIT_CODE, (durabilityCredits > 0 || walkSpeedCredits > 0) && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_ARMOR_DURABILITY_LEVEL);
            }
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
        /// Gets the first-equip walk speed penalty reduction bonus for an armor type.
        /// </summary>
        public static int GetFirstEquipWalkSpeedBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FIRST_EQUIP_WALKSPEED_PLATE_BONUS;
                case "scale": return FIRST_EQUIP_WALKSPEED_SCALE_BONUS;
                case "brigandine": return FIRST_EQUIP_WALKSPEED_BRIGANDINE_BONUS;
                case "chain": return FIRST_EQUIP_WALKSPEED_CHAIN_BONUS;
                case "light":
                default: return FIRST_EQUIP_WALKSPEED_LIGHT_BONUS;
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

                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} first-time equipped {itemCode}, +{firstEquipBonus}% durability bonus");

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

            // Skip armor progression if disabled
            if (IsSkillDisabled("armor")) return;

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

                // Check for newly equipped armor (first-equip bonus) and track time worn
                foreach (var kvp in currentArmor)
                {
                    string slotId = kvp.Key;
                    string itemCode = kvp.Value;
                    var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                    // Check if this is new armor in this slot
                    if (!previousArmor.TryGetValue(slotId, out string prevArmor) || prevArmor != itemCode)
                    {
                        // New armor equipped - check for first-time bonus
                        if (!pieceProgress.HasBeenEquipped)
                        {
                            pieceProgress.HasBeenEquipped = true;
                            string armorType = GetArmorType(itemCode);

                            // Grant durability bonus
                            int firstEquipBonus = GetFirstEquipBonus(armorType);
                            int oldDurability = armorProgress.TotalDurabilityCredits;
                            armorProgress.TotalDurabilityCredits = Math.Min(armorProgress.TotalDurabilityCredits + firstEquipBonus, MaxArmorDurabilityPercent);
                            int actualDurabilityBonus = armorProgress.TotalDurabilityCredits - oldDurability;

                            // Grant walk speed penalty reduction bonus (same values as durability)
                            int walkSpeedEquipBonus = GetFirstEquipWalkSpeedBonus(armorType);
                            int oldWalkSpeed = armorProgress.TotalWalkSpeedCredits;
                            armorProgress.TotalWalkSpeedCredits = Math.Min(armorProgress.TotalWalkSpeedCredits + walkSpeedEquipBonus, MaxArmorWalkSpeedPercent);
                            int actualWalkSpeedBonus = armorProgress.TotalWalkSpeedCredits - oldWalkSpeed;

                            pendingArmorProgressSave = true;

                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Send message with both bonuses
                            if (actualDurabilityBonus > 0 || actualWalkSpeedBonus > 0)
                            {
                                player.SendMessage(GlobalConstants.GeneralChatGroup,
                                    Lang.Get("seraphleveling:message-armor-first-equip-both", actualDurabilityBonus, actualWalkSpeedBonus),
                                    EnumChatType.Notification);
                            }
                        }
                    }

                    // Track time worn for walk speed credits (only if not at max)
                    if (armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent)
                    {
                        int oldWalkSpeedCredits = armorProgress.TotalWalkSpeedCredits;

                        // Add 1 second (tick interval) to this armor piece's time
                        pieceProgress.SecondsWornInIncrement += 1f;

                        // Check if we've earned any new time credits
                        while (pieceProgress.SecondsWornInIncrement >= pieceProgress.CurrentTimeIncrementSize &&
                               armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent)
                        {
                            pieceProgress.TimeCredits++;
                            armorProgress.TotalWalkSpeedCredits++;
                            pieceProgress.SecondsWornInIncrement -= pieceProgress.CurrentTimeIncrementSize;
                            pieceProgress.CurrentTimeIncrementSize += ArmorTimeIncrementStep;

                            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned time credit {pieceProgress.TimeCredits} with {itemCode}");
                        }

                        if (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits)
                        {
                            pendingArmorProgressSave = true;
                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Notify player of level up
                            player.SendMessage(GlobalConstants.GeneralChatGroup,
                                Lang.Get("seraphleveling:message-armor-time-level-up", armorProgress.TotalWalkSpeedCredits),
                                EnumChatType.Notification);
                        }
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

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned damage credit {pieceProgress.DamageCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-armor-damage-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits),
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

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned repair credit {pieceProgress.RepairCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-armor-repair-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Apply walking speed bonus to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static int ApplyWalkingBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFleetfooted = cache?.HasFleetfooted ?? PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (MaxWalkingSpeedPercent - vanillaFleetfootedBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("walkspeed", WALKING_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_WALKING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_WALKING_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_WALKING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_WALKING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

                // Add our trait to extraTraits only if player doesn't already have Fleetfooted
                UpdateExtraTraitStatic(player.Entity, WALKING_TRAIT_CODE, level > 0 && !hasVanillaFleetfooted);

                watchedAttrs.MarkPathDirty(WATCHED_WALKING_LEVEL);
            }

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
            if (!IsSkillDisabled("forager") && IsWildCropBlock(oldblockId, blockSel?.Position))
            {
                ProcessWildCropBroken(byPlayer);
            }

            // Check for Pilferer progression (cracked vessels only - they can't be re-placed)
            if (!IsSkillDisabled("pilferer") && IsCrackedVesselBlock(oldblockId))
            {
                ProcessVesselBreak(byPlayer);
            }

            // Skip mining progression if disabled
            if (IsSkillDisabled("mining")) return;

            // Check if player is using a pickaxe for mining progression
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            if (pickaxeCode == null) return; // Not using a pickaxe, skip mining

            // Check block type and get points
            int points = GetBlockPoints(oldblockId);
            if (points <= 0) return; // Not a stone/ore block, skip

            string playerUid = byPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = GetMaxMiningCredits(byPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific pickaxe type
            var pickaxeProgress = playerProgress.GetPickaxeProgress(pickaxeCode);

            int oldCredits = playerProgress.TotalCredits;

            // Add points to THIS pickaxe's progress
            pickaxeProgress.BlocksInIncrement += points;

            // Check if we've earned any new credits with this pickaxe
            while (pickaxeProgress.BlocksInIncrement >= pickaxeProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                pickaxeProgress.BlocksInIncrement -= pickaxeProgress.CurrentIncrementSize;
                pickaxeProgress.CurrentIncrementSize += IncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {byPlayer.PlayerName} earned credit {playerProgress.TotalCredits} with {pickaxeCode}, next requires {pickaxeProgress.CurrentIncrementSize} points");
            }

            pendingMiningProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMiningBonus(byPlayer, playerProgress.TotalCredits);

                // Notify player of level up with the level as the bonus (the raw mining speed improvement)
                // This shows the true progress even when negative traits are still being cancelled
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-mining-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
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
            // Skip walking progression if disabled
            if (IsSkillDisabled("walking")) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;
                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last position (using Position2D struct to avoid Vec3d allocations)
                if (!lastPlayerPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastPlayerPositions[playerUid] = new Position2D(currentX, currentZ);
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentX - lastPos.X;
                double dz = currentZ - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position (no allocation - struct assignment)
                lastPlayerPositions[playerUid] = new Position2D(currentX, currentZ);

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

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned walking credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingWalkingProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyWalkingBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("seraphleveling:message-walking-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
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
            // Skip hunger progression if disabled
            if (IsSkillDisabled("hunger")) return;

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

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned hunger credit {playerProgress.TotalCredits}/{playerMaxCredits}, next requires {playerProgress.CurrentIncrementSize} seconds");
                }

                // Mark for saving if any progress was made
                if (playerProgress.SecondsInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingHungerProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyHungerBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when cancelling Ravenous)
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("seraphleveling:message-hunger-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
                        EnumChatType.Notification);
                }
            }
        }

        /// <summary>
        /// Called when a player disconnects. Cleans up their position, armor tracking, and cached data.
        /// Also triggers a save of all progress data to prevent data loss.
        /// </summary>
        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;
            string playerUid = byPlayer.PlayerUID;
            lastPlayerPositions.TryRemove(playerUid, out _);
            lastSneakingPositions.TryRemove(playerUid, out _);
            playerEquippedArmor.TryRemove(playerUid, out _);
            VanillaTraitsCache.TryRemove(playerUid, out _);

            // Save all pending progress data to prevent data loss on disconnect
            SaveAllPendingProgress();
        }

        /// <summary>
        /// Called periodically by auto-save timer to persist all pending progress.
        /// Only saves when players are online to avoid waking up idle dedicated servers.
        /// </summary>
        private void OnAutoSaveTick(float dt)
        {
            // Don't save if no players are online - this prevents waking up idle dedicated servers
            if (ServerApi?.World?.AllOnlinePlayers == null || ServerApi.World.AllOnlinePlayers.Length == 0)
            {
                return;
            }

            SaveAllPendingProgress();
        }

        /// <summary>
        /// Saves all pending progress data. Called on player disconnect and auto-save tick.
        /// </summary>
        private void SaveAllPendingProgress()
        {
            // Reuse the same logic as OnGameWorldSave
            OnGameWorldSave();
        }

        /// <summary>
        /// Populates the vanilla traits cache for a player.
        /// This reads the characterTraits array once and caches all trait booleans.
        /// </summary>
        private static void PopulateVanillaTraitsCache(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var entity = player.Entity;

            // Get character traits once
            string[] characterTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null) ?? Array.Empty<string>();
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "")?.ToLowerInvariant() ?? "";

            // Create a HashSet for O(1) lookups
            var traitSet = new HashSet<string>(characterTraits, StringComparer.OrdinalIgnoreCase);

            var cache = new CachedVanillaTraits
            {
                HasHardy = traitSet.Contains("hardy") || characterClass == "blackguard",
                HasSoldier = traitSet.Contains("soldier") || characterClass == "blackguard",
                HasFocused = traitSet.Contains("focused") || characterClass == "hunter",
                HasFleetfooted = traitSet.Contains("fleetfooted") || characterClass == "hunter" || characterClass == "clockmaker",
                HasRavenous = traitSet.Contains("ravenous") || characterClass == "blackguard",
                HasFarsighted = traitSet.Contains("farsighted") || characterClass == "hunter",
                HasNervous = traitSet.Contains("nervous") || characterClass == "malefactor" || characterClass == "clockmaker",
                HasNearsighted = traitSet.Contains("nearsighted") || characterClass == "blackguard",
                HasFrail = traitSet.Contains("frail") || characterClass == "malefactor" || characterClass == "clockmaker",
                HasCivil = traitSet.Contains("civil") || characterClass == "tailor",
                HasWeak = traitSet.Contains("weak") || characterClass == "tailor",
                HasKind = traitSet.Contains("kind") || characterClass == "tailor",
                HasHeavyhanded = traitSet.Contains("heavyhanded") || characterClass == "blackguard",
                HasClaustrophobic = traitSet.Contains("claustrophobic") || characterClass == "hunter",
                HasFurtive = traitSet.Contains("furtive") || characterClass == "malefactor",
                HasPrecise = traitSet.Contains("precise") || characterClass == "clockmaker",
                HasMender = traitSet.Contains("mender") || characterClass == "tailor",
                HasPilferer = traitSet.Contains("pilferer") || characterClass == "malefactor",
                HasResourceful = traitSet.Contains("resourceful") || characterClass == "hunter" || characterClass == "malefactor",
                HasForager = traitSet.Contains("forager") || characterClass == "hunter" || characterClass == "malefactor",

                // Combat Overhaul negative traits (using CO trait naming conventions)
                HasCOTremblingAim = traitSet.Contains("tremblingaim") || traitSet.Contains("trembling aim"),
                HasCOClumsyHands = traitSet.Contains("clumsyhands") || traitSet.Contains("clumsy hands"),
                HasCOFrightenedOfMelee = traitSet.Contains("frightenedofmelee") || traitSet.Contains("frightened of melee") || traitSet.Contains("frightened")
            };

            VanillaTraitsCache[playerUid] = cache;
        }

        /// <summary>
        /// Gets the cached vanilla traits for a player. Returns null if not cached.
        /// </summary>
        private static CachedVanillaTraits GetCachedTraits(string playerUid)
        {
            VanillaTraitsCache.TryGetValue(playerUid, out var cache);
            return cache;
        }

        /// <summary>
        /// Called when a player joins. Applies their saved bonuses (mining, melee, ranged, walking, and hunger).
        /// </summary>
        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            string playerUid = byPlayer.PlayerUID;

            // Populate vanilla traits cache first (before applying any bonuses)
            PopulateVanillaTraitsCache(byPlayer);

            // Apply mining bonus (Stats always applied, WatchedAttributes only sync if changed)
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningCredits = miningProg.TotalCredits;
            ApplyMiningBonus(byPlayer, miningCredits);
            if (miningCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mining bonus {miningCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply melee bonus (Stats always applied, WatchedAttributes only sync if changed)
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            int meleeCredits = meleeProg.TotalCredits;
            ApplyMeleeBonusStatic(byPlayer, meleeCredits);
            if (meleeCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied melee bonus {meleeCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply ranged bonus (Stats always applied, WatchedAttributes only sync if changed)
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            int rangedCredits = rangedProg.TotalCredits;
            ApplyRangedBonusStatic(byPlayer, rangedCredits);
            if (rangedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied ranged bonus {rangedCredits} credits to player {byPlayer.PlayerName}");
            }

            // Apply walking bonus (Stats always applied, WatchedAttributes only sync if changed)
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });
            int walkingCredits = walkingProg.TotalCredits;
            ApplyWalkingBonusStatic(byPlayer, walkingCredits);
            if (walkingCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied walking bonus {walkingCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply hunger bonus (Stats always applied, WatchedAttributes only sync if changed)
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });
            int hungerCredits = hungerProg.TotalCredits;
            ApplyHungerBonusStatic(byPlayer, hungerCredits);
            if (hungerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hunger bonus -{hungerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply armor bonuses (Stats always applied, WatchedAttributes only sync if changed)
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            ApplyArmorBonusesStatic(byPlayer, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (armorProg.TotalDurabilityCredits > 0 || armorProg.TotalWalkSpeedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied armor bonuses: +{armorProg.TotalDurabilityCredits}% durability, -{armorProg.TotalWalkSpeedCredits}% walk speed penalty to player {byPlayer.PlayerName}");
            }

            // Apply clothier bonus
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            ApplyClothierBonusStatic(byPlayer, clothierProg);
            if (clothierProg.SewingKitUnlocked)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied clothier unlock to player {byPlayer.PlayerName}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mender bonus +{menderCredits}% to player {byPlayer.PlayerName}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied pilferer bonus +{pilfererCredits}% to player {byPlayer.PlayerName}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied resourceful bonus +{resourcefulCredits}% to player {byPlayer.PlayerName}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied forager bonus +{foragerCredits}% to player {byPlayer.PlayerName}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied furtive bonus -{furtiveCredits}% detection to player {byPlayer.PlayerName}");
            }

            // Apply precise bonus
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            int preciseCredits = preciseProg.TotalCredits;
            ApplyPreciseBonusStatic(byPlayer, preciseCredits);
            if (preciseCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied precise bonus +{preciseCredits}% mechanical damage to player {byPlayer.PlayerName}");
            }

            // Apply technical unlock
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (technicalProg.IsUnlocked)
            {
                ApplyTechnicalBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied technical unlock to player {byPlayer.PlayerName}");
            }

            // Apply hardy health unlock
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            if (hardyHealthProg.IsUnlocked)
            {
                ApplyHardyHealthBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hardy health +{HardyHealthBonus} HP to player {byPlayer.PlayerName}");
            }

            // Apply bowyer unlock
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            if (bowyerProg.IsUnlocked)
            {
                ApplyBowyerBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied bowyer unlock to player {byPlayer.PlayerName}");
            }

            // Apply improviser unlock
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            if (improviserProg.IsUnlocked)
            {
                ApplyImproviserBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied improviser unlock to player {byPlayer.PlayerName}");
            }

            // Apply tinkerer unlock
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            if (tinkererProg.IsUnlocked)
            {
                ApplyTinkererBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied tinkerer unlock to player {byPlayer.PlayerName}");
            }

            // Apply merciless unlock
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            if (mercilessProg.IsUnlocked)
            {
                ApplyMercilessBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied merciless unlock to player {byPlayer.PlayerName}");
            }

            // Apply claustrophobic removal
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            if (claustrophobicProg.IsRemoved)
            {
                ApplyClaustrophobicRemovalStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied claustrophobic removal to player {byPlayer.PlayerName}");
            }

            // Apply Combat Overhaul proficiency bonuses (if CO is loaded)
            if (IsCOCompatEnabled)
            {
                ApplyAllCOBonuses(byPlayer);
                if (COProgress.TryGetValue(playerUid, out var coProgress))
                {
                    int totalCOCredits = coProgress.SteadyAimCredits;
                    foreach (var prof in coProgress.Proficiencies)
                    {
                        totalCOCredits += prof.Value.TotalCredits;
                    }
                    if (totalCOCredits > 0)
                    {
                        ServerApi.Logger.Debug($"[SeraphLeveling] Applied CO bonuses ({totalCOCredits} total credits) to player {byPlayer.PlayerName}");
                    }
                }
            }

            // Initialize equipped armor tracking for this player
            InitializePlayerArmorTracking(byPlayer);
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// Also handles Weak and Claustrophobic negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns the actual applied bonus percentage (0-100 scale).
        /// </summary>
        private int ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaHardy = cache?.HasHardy ?? PlayerHasVanillaHardy(player.Entity);
            bool hasWeak = cache?.HasWeak ?? PlayerHasVanillaWeak(player.Entity);
            bool hasClaustrophobic = cache?.HasClaustrophobic ?? PlayerHasVanillaClaustrophobic(player.Entity);

            int vanillaHardyBonus = hasVanillaHardy ? VANILLA_HARDY_MINING_BONUS : 0;

            // Calculate remaining negative trait penalties
            int weakMiningRemaining = hasWeak ? CalculateRemainingPenalty(VANILLA_WEAK_MINING_PENALTY, level) : 0;
            // HP penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), HP is also cancelled
            int weakHpRemaining = weakMiningRemaining > 0 ? VANILLA_WEAK_HP_PENALTY : 0;
            int claustrophobicMiningRemaining = hasClaustrophobic ? CalculateRemainingPenalty(VANILLA_CLAUSTROPHOBIC_MINING_PENALTY, level) : 0;
            // Ore penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), ore is also cancelled
            int claustrophobicOreRemaining = claustrophobicMiningRemaining > 0 ? VANILLA_CLAUSTROPHOBIC_ORE_PENALTY : 0;

            // Calculate net bonus after cancelling negative traits
            // Negative trait penalty must be fully cancelled before bonus starts showing
            int totalNegativePenalty = 0;
            if (hasWeak) totalNegativePenalty += VANILLA_WEAK_MINING_PENALTY;
            if (hasClaustrophobic) totalNegativePenalty += VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;

            int netLevel = Math.Max(0, level - totalNegativePenalty);

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMiningSpeedPercent
            int maxEarnableBonus = MaxMiningSpeedPercent - vanillaHardyBonus;
            int bonusPercent = Math.Min(netLevel, Math.Max(0, maxEarnableBonus));

            float bonus = bonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            // Set the mining speed stat
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, bonus, false);

            // When Claustrophobic mining penalty is fully cancelled, also negate the ore drop penalty
            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining == 0)
                {
                    // Negate the -15% ore drop penalty by applying +15%
                    player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicOreCancel", VANILLA_CLAUSTROPHOBIC_ORE_PENALTY * 0.01f, false);
                }
                else
                {
                    // Remove the ore cancellation stat if penalty is still active
                    player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicOreCancel");
                }
            }

            // When Weak mining penalty is fully cancelled, also negate the HP penalty
            if (hasWeak)
            {
                if (weakMiningRemaining == 0)
                {
                    // Negate the -2 HP penalty by applying +2 HP
                    player.Entity.Stats.Set("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE, VANILLA_WEAK_HP_PENALTY, false);
                }
                else
                {
                    // Remove the HP cancellation stat if penalty is still active
                    player.Entity.Stats.Remove("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE);
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MINING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MINING_BONUS, -1);
            int oldClaustoMining = watchedAttrs.GetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent) || (oldClaustoMining != claustrophobicMiningRemaining);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MINING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MINING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaHardy", hasVanillaHardy);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasWeak", hasWeak);
                watchedAttrs.SetInt(WATCHED_WEAK_MINING_REMAINING, weakMiningRemaining);
                watchedAttrs.SetInt(WATCHED_WEAK_HP_REMAINING, weakHpRemaining);
                watchedAttrs.SetBool("sitHasClaustrophobic", hasClaustrophobic);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, claustrophobicMiningRemaining);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_ORE_REMAINING, claustrophobicOreRemaining);

                // Add our trait to extraTraits only if:
                // - Player doesn't already have Hardy AND
                // - All negative mining penalties are cancelled (bonusPercent > 0)
                UpdateExtraTrait(player.Entity, MINING_TRAIT_CODE, bonusPercent > 0 && !hasVanillaHardy);

                // Only call MarkPathDirty once at the end (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_MINING_LEVEL);
            }

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
                ServerApi.Logger.Debug($"[SeraphLeveling] Added trait {traitCode} to player");
            }
            else if (!shouldHave && hasTrait)
            {
                // Remove the trait
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                ServerApi.Logger.Debug($"[SeraphLeveling] Removed trait {traitCode} from player");
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

        /// <summary>
        /// Get the maximum mining credits a player can earn based on their traits.
        /// Players with Weak or Claustrophobic traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMiningCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMiningSpeedPercent;

            bool hasWeak = PlayerHasVanillaWeak(entity);
            bool hasClaustrophobic = PlayerHasVanillaClaustrophobic(entity);

            // Weak penalty is 10% mining speed, need 10 extra levels to cancel it
            if (hasWeak)
            {
                return MaxMiningSpeedPercent + VANILLA_WEAK_MINING_PENALTY;
            }

            // Claustrophobic penalty is 10% mining speed, need 10 extra levels to cancel it
            if (hasClaustrophobic)
            {
                return MaxMiningSpeedPercent + VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;
            }

            return MaxMiningSpeedPercent;
        }

        // Server-side Harmony instance for melee damage tracking
        private Harmony serverHarmony;

        /// <summary>
        /// Apply Harmony patches for server-side melee damage tracking.
        /// </summary>
        private void ApplyServerHarmonyPatches(ICoreServerAPI api)
        {
            serverHarmony = new Harmony("seraphleveling.server");

            try
            {
                // Find Entity.ReceiveDamage method
                var entityType = typeof(Entity);
                api.Logger.Debug($"[SeraphLeveling] Looking for Entity.ReceiveDamage method in {entityType.FullName}");

                var receiveDamageMethod = entityType.GetMethod("ReceiveDamage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (receiveDamageMethod == null)
                {
                    // Try to list available methods for debugging
                    var methods = entityType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var damageMethodNames = methods.Where(m => m.Name.Contains("Damage")).Select(m => m.Name).ToArray();
                    api.Logger.Warning($"[SeraphLeveling] Could not find Entity.ReceiveDamage method. Available damage methods: {string.Join(", ", damageMethodNames)}");
                    return;
                }

                api.Logger.Debug($"[SeraphLeveling] Found Entity.ReceiveDamage: {receiveDamageMethod}");

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(EntityDamagePatches),
                    nameof(EntityDamagePatches.ReceiveDamage_Postfix));

                if (postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find ReceiveDamage_Postfix method!");
                    return;
                }

                api.Logger.Debug($"[SeraphLeveling] Found postfix method: {postfixMethod}");

                serverHarmony.Patch(receiveDamageMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched Entity.ReceiveDamage for damage tracking");

                // Patch EntityBehaviorHarvestable.SetHarvested for Resourceful trait (animal harvesting)
                PatchAnimalHarvesting(api);

                // Patch CollectibleObject.OnHeldInteractStep for Mender trait (sewing kit repairs)
                PatchSewingKitRepairs(api);

                // Patch BlockEntityStaticTranslocator.DoRepair for Technical trait (translocator repairs)
                PatchTranslocatorRepairs(api);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply server Harmony patches: {ex.Message}");
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
                    api.Logger.Warning("[SeraphLeveling] Could not find EntityBehaviorHarvestable type");
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
                    api.Logger.Warning("[SeraphLeveling] Could not find SetHarvested or SetHarvestedBy method in EntityBehaviorHarvestable");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(HarvestingPatches),
                    nameof(HarvestingPatches.SetHarvested_Postfix));

                serverHarmony.Patch(setHarvestedMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched EntityBehaviorHarvestable.SetHarvested for Resourceful trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch EntityBehaviorHarvestable: {ex.Message}");
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
                        api.Logger.Notification("[SeraphLeveling] Successfully patched ItemSewingKit.OnHeldInteractStop for Mender trait");
                        anyPatchSucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] ItemSewingKit patch attempt: {ex.Message}");
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
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnModifiedInInventorySlot for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] OnModifiedInInventorySlot patch attempt: {ex.Message}");
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
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnHeldInteractStep for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] OnHeldInteractStep patch attempt: {ex.Message}");
            }

            if (!anyPatchSucceeded)
            {
                api.Logger.Warning("[SeraphLeveling] Could not patch any method for Mender trait (sewing kit repairs)");
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
                    api.Logger.Warning("[SeraphLeveling] Could not find BlockEntityStaticTranslocator type");
                    return;
                }

                // Find the DoRepair method
                var doRepairMethod = AccessTools.Method(translocatorType, "DoRepair");
                if (doRepairMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find DoRepair method in BlockEntityStaticTranslocator");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(TranslocatorPatches),
                    nameof(TranslocatorPatches.DoRepair_Postfix));

                serverHarmony.Patch(doRepairMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched BlockEntityStaticTranslocator.DoRepair for Technical trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch BlockEntityStaticTranslocator.DoRepair: {ex.Message}");
            }
        }

        /// <summary>
        /// Process melee damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessMeleeDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponType)) return;

            // Check if melee skill is disabled
            if (IsSkillDisabled("melee")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            // Get the player-specific max credits (accounts for Farsighted/Nervous penalties)
            int maxCredits = GetMaxMeleeCredits(attackerPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon type
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            int oldCredits = playerProgress.TotalCredits;

            // Add damage to THIS weapon type's progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits with this weapon type
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += MeleeIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned melee credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingMeleeProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMeleeBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-melee-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
                    EnumChatType.Notification);

                // Check for trait unlocks that depend on melee damage
                CheckMercilessUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Static version of ApplyMeleeBonus for use from Harmony patches.
        /// Also handles Farsighted and Nervous negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        private static int ApplyMeleeBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierStatic(player.Entity);
            bool hasFarsighted = cache?.HasFarsighted ?? PlayerHasVanillaFarsighted(player.Entity);
            bool hasNervous = cache?.HasNervous ?? PlayerHasVanillaNervous(player.Entity);

            int vanillaSoldierBonus = hasVanillaSoldier ? VANILLA_SOLDIER_MELEE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int farsightedRemaining = hasFarsighted ? CalculateRemainingPenalty(VANILLA_FARSIGHTED_MELEE_PENALTY, level) : 0;
            int nervousRemaining = hasNervous ? CalculateRemainingPenalty(VANILLA_NERVOUS_MELEE_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netBonusPercent = level;
            if (hasFarsighted)
            {
                netBonusPercent = Math.Max(0, level - VANILLA_FARSIGHTED_MELEE_PENALTY);
            }
            if (hasNervous)
            {
                netBonusPercent = Math.Max(0, level - VANILLA_NERVOUS_MELEE_PENALTY);
            }

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMeleeDamagePercent
            int maxEarnableBonus = MaxMeleeDamagePercent - vanillaSoldierBonus;
            netBonusPercent = Math.Min(netBonusPercent, Math.Max(0, maxEarnableBonus));

            float bonus = netBonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("meleeWeaponsDamage", MELEE_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MELEE_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MELEE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != netBonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MELEE_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MELEE_BONUS, netBonusPercent);
                watchedAttrs.SetBool("sitHasVanillaSoldier", hasVanillaSoldier);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasFarsighted", hasFarsighted);
                watchedAttrs.SetInt(WATCHED_FARSIGHTED_REMAINING, farsightedRemaining);
                watchedAttrs.SetBool("sitHasNervous", hasNervous);
                watchedAttrs.SetInt(WATCHED_NERVOUS_REMAINING, nervousRemaining);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, MELEE_TRAIT_CODE, level > 0 && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_MELEE_LEVEL);
            }

            return netBonusPercent;
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
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponCombo)) return;

            // Check if ranged skill is disabled
            if (IsSkillDisabled("ranged")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            // Get the player-specific max credits (accounts for Nearsighted/Frail penalties)
            int maxCredits = GetMaxRangedCredits(attackerPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon combination
            var weaponProgress = playerProgress.GetWeaponProgress(weaponCombo);

            int oldCredits = playerProgress.TotalCredits;

            // Add damage to THIS weapon combination's progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits with this weapon combination
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += RangedIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned ranged credit {playerProgress.TotalCredits} with {weaponCombo}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingRangedProgressSave = true;

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyRangedBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-ranged-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits),
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
        /// Also handles Nearsighted and Frail negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as percentages.
        /// </summary>
        public static (int damage, int accuracy, int distance) ApplyRangedBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFocused = cache?.HasFocused ?? PlayerHasVanillaFocusedStatic(player.Entity);
            bool hasNearsighted = cache?.HasNearsighted ?? PlayerHasVanillaNearsighted(player.Entity);
            bool hasFrail = cache?.HasFrail ?? PlayerHasVanillaFrail(player.Entity);

            int vanillaDamage = hasVanillaFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasVanillaFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasVanillaFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int nearsightedRemaining = hasNearsighted ? CalculateRemainingPenalty(VANILLA_NEARSIGHTED_RANGED_PENALTY, level) : 0;
            int frailDistanceRemaining = hasFrail ? CalculateRemainingPenalty(VANILLA_FRAIL_DISTANCE_PENALTY, level) : 0;
            // HP penalty is tied to distance penalty - when distance penalty is cancelled (at level 25), HP is also cancelled
            float frailHpRemaining = frailDistanceRemaining > 0 ? VANILLA_FRAIL_HP_PENALTY : 0f;

            // Calculate net bonus after cancelling negative traits
            int netDamageLevel = level;
            int netDistanceLevel = level;

            if (hasNearsighted)
            {
                netDamageLevel = Math.Max(0, level - VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }
            if (hasFrail)
            {
                netDistanceLevel = Math.Max(0, level - VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            // Calculate earnable bonuses (each stat capped individually)
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - vanillaDamage);
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - vanillaAccuracy);
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - vanillaDistance);

            // Calculate actual bonuses from level (using net level after penalty cancellation)
            int damagePct = Math.Min(netDamageLevel, earnableDamage);
            int accuracyPct = Math.Min(level, earnableAccuracy);
            int distancePct = Math.Min(netDistanceLevel, earnableDistance);

            float damageBonus = damagePct * 0.01f;
            float accuracyBonus = accuracyPct * 0.01f;
            float distanceBonus = distancePct * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("rangedWeaponsDamage", RANGED_DAMAGE_STAT_CODE, damageBonus, false);
            player.Entity.Stats.Set("rangedWeaponsAcc", RANGED_ACCURACY_STAT_CODE, accuracyBonus, false);
            player.Entity.Stats.Set("bowDrawingStrength", RANGED_DISTANCE_STAT_CODE, distanceBonus, false);

            // When Frail distance penalty is fully cancelled, also negate the HP penalty
            if (hasFrail)
            {
                if (frailDistanceRemaining == 0)
                {
                    player.Entity.Stats.Set("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE, VANILLA_FRAIL_HP_PENALTY, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE);
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_RANGED_LEVEL, -1);
            int oldDamageBonus = watchedAttrs.GetInt(WATCHED_RANGED_DAMAGE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldDamageBonus != damagePct);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonuses to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_RANGED_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_RANGED_DAMAGE_BONUS, damagePct);
                watchedAttrs.SetInt(WATCHED_RANGED_ACCURACY_BONUS, accuracyPct);
                watchedAttrs.SetInt(WATCHED_RANGED_DISTANCE_BONUS, distancePct);
                watchedAttrs.SetBool("sitHasVanillaFocused", hasVanillaFocused);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasNearsighted", hasNearsighted);
                watchedAttrs.SetInt(WATCHED_NEARSIGHTED_REMAINING, nearsightedRemaining);
                watchedAttrs.SetBool("sitHasFrail", hasFrail);
                watchedAttrs.SetInt(WATCHED_FRAIL_DISTANCE_REMAINING, frailDistanceRemaining);
                watchedAttrs.SetFloat(WATCHED_FRAIL_HP_REMAINING, frailHpRemaining);

                // Add our trait to extraTraits only if player doesn't already have Focused
                UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);

                watchedAttrs.MarkPathDirty(WATCHED_RANGED_LEVEL);
            }

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

            // Remove any mod prefix (e.g., "game:", "combatoverhaul:") for checking
            string projCheck = projectileCode.Contains(":") ? projectileCode.Substring(projectileCode.IndexOf(':') + 1) : projectileCode;
            string heldCheck = heldItemCode.Contains(":") ? heldItemCode.Substring(heldItemCode.IndexOf(':') + 1) : heldItemCode;

            // Check for arrow projectiles (bows)
            if (projCheck.StartsWith("arrow-") || projCheck == "arrow" || projCheck.Contains("arrow"))
            {
                // Get bow type from held item (if still holding a bow)
                string bowCode = "unknown-bow";
                if (heldCheck.StartsWith("bow-") || heldCheck == "bow" ||
                    heldCheck.StartsWith("longbow") || heldCheck.StartsWith("recurvebow") ||
                    heldCheck.StartsWith("crudebow") || heldCheck.StartsWith("simplebow") ||
                    heldCheck.Contains("bow"))
                {
                    bowCode = heldCheck;
                }
                return $"{bowCode}+{projCheck}";
            }

            // Check for crossbow bolts/quarrels
            if (projCheck.StartsWith("bolt-") || projCheck == "bolt" || projCheck.Contains("bolt") ||
                projCheck.StartsWith("quarrel-") || projCheck == "quarrel" || projCheck.Contains("quarrel"))
            {
                // Get crossbow type from held item
                string crossbowCode = "unknown-crossbow";
                if (heldCheck.StartsWith("crossbow") || heldCheck.Contains("crossbow"))
                {
                    crossbowCode = heldCheck;
                }
                return $"{crossbowCode}+{projCheck}";
            }

            // Check for firearm projectiles (bullets, musket balls, etc.)
            if (projCheck.StartsWith("bullet-") || projCheck == "bullet" || projCheck.Contains("bullet") ||
                projCheck.StartsWith("musketball") || projCheck.Contains("musketball") ||
                projCheck.StartsWith("shot-") || projCheck.Contains("shot"))
            {
                // Get firearm type from held item
                string firearmCode = "unknown-firearm";
                if (heldCheck.StartsWith("musket") || heldCheck.StartsWith("pistol") ||
                    heldCheck.StartsWith("rifle") || heldCheck.StartsWith("blunderbuss") ||
                    heldCheck.Contains("gun") || heldCheck.Contains("firearm"))
                {
                    firearmCode = heldCheck;
                }
                return $"{firearmCode}+{projCheck}";
            }

            // Check for sling stones (thrown stones)
            if (projCheck.StartsWith("stone-") || projCheck == "stone" || projCheck.StartsWith("thrownstone") ||
                projCheck.Contains("slingstone") || projCheck.Contains("sling-stone"))
            {
                // Check if holding a sling
                string slingCode = "thrown";
                if (heldCheck.StartsWith("sling") || heldCheck.Contains("sling"))
                {
                    slingCode = heldCheck;
                }
                return $"{slingCode}+{projCheck}";
            }

            // Check for spear/javelin throws (thrown spears deal ranged damage)
            if (projCheck.StartsWith("spear-") || projCheck.StartsWith("thrownspear") ||
                projCheck.StartsWith("javelin-") || projCheck.Contains("javelin") ||
                projCheck.StartsWith("pilum-") || projCheck.Contains("throwingspear"))
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
            // Debug logging at start to diagnose CO issues
            ServerApi?.Logger.Debug($"[SeraphLeveling] IsRangedDamage called: SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}, Same={damageSource?.SourceEntity == damageSource?.CauseEntity}");

            // CauseEntity is non-null for projectile damage (it's the shooter)
            // SourceEntity is the projectile itself
            if (damageSource?.CauseEntity == null) return false;

            // For melee attacks, SourceEntity equals CauseEntity (both are the attacker).
            // For ranged attacks, SourceEntity is the projectile, CauseEntity is the shooter.
            // Combat Overhaul may set CauseEntity for melee attacks, so we check if they're
            // the same entity to distinguish melee from ranged.
            if (damageSource.SourceEntity == damageSource.CauseEntity) return false;

            // Additional check: the damage should be from a projectile type
            // PiercingAttack is typically used for arrows in vanilla
            // SlashingAttack is used by Combat Overhaul for arrows
            // BluntAttack is used for thrown stones
            return damageSource.Type == EnumDamageType.PiercingAttack ||
                   damageSource.Type == EnumDamageType.SlashingAttack ||
                   damageSource.Type == EnumDamageType.BluntAttack;
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
            serverHarmony?.UnpatchAll("seraphleveling.server");

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
            VanillaTraitsCache.Clear();
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

            // Combat Overhaul compatibility persistence
            if (pendingCOProgressSave || !COProgress.IsEmpty)
            {
                PersistCOProgress();
                pendingCOProgressSave = false;
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
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted mining progress for {snapshot.Length} players (v3 format)");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist mining progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No mining progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid mining progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (version == 1)
                        {
                            // Legacy format: convert old blocks-based progress to credits
                            ServerApi.Logger.Notification("[SeraphLeveling] Converting legacy v1 save data to v3 format...");
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
                            ServerApi.Logger.Notification("[SeraphLeveling] Converting v2 save data to v3 format...");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded mining progress for {MiningProgress.Count} players");
            }
            catch (Exception ex)
            {
                MiningProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load mining progress: {ex.Message}");
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
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted melee progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist melee progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No melee progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid melee progress data format");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown melee save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded melee progress for {MeleeProgress.Count} players");
            }
            catch (Exception ex)
            {
                MeleeProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load melee progress: {ex.Message}");
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
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted ranged progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist ranged progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No ranged progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid ranged progress data format");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown ranged save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded ranged progress for {RangedProgress.Count} players");
            }
            catch (Exception ex)
            {
                RangedProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load ranged progress: {ex.Message}");
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
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted walking progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist walking progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No walking progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid walking progress data format");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown walking save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded walking progress for {WalkingProgress.Count} players");
            }
            catch (Exception ex)
            {
                WalkingProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load walking progress: {ex.Message}");
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
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted hunger progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist hunger progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No hunger progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid hunger progress data format");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown hunger save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded hunger progress for {HungerProgress.Count} players");
            }
            catch (Exception ex)
            {
                HungerProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load hunger progress: {ex.Message}");
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Saved armor progress for {ArmorProgress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist armor progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No armor progress data found in world save");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid armor progress data format");
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
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown armor save format version {version}");
                            return;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded armor progress for {ArmorProgress.Count} players");
            }
            catch (Exception ex)
            {
                ArmorProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load armor progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Load configuration from ModConfig/SeraphLeveling.json.
        /// If the file doesn't exist, creates one with default values.
        /// These values are used as defaults for new worlds.
        /// </summary>
        private void LoadConfigFile(ICoreServerAPI api)
        {
            try
            {
                SeraphLevelingConfig config = api.LoadModConfig<SeraphLevelingConfig>(CONFIG_FILE_NAME);
                if (config == null)
                {
                    config = new SeraphLevelingConfig();
                    api.StoreModConfig(config, CONFIG_FILE_NAME);
                    api.Logger.Notification("[SeraphLeveling] Created default config file: ModConfig/" + CONFIG_FILE_NAME);
                }

                // Apply config values to static variables (these become defaults for new worlds)
                BaseBlocksPerIncrement = config.MiningBaseBlocksPerIncrement;
                IncrementStep = config.MiningIncrementStep;
                MaxMiningSpeedPercent = config.MiningMaxPercent;
                OreMultiplier = config.MiningOreMultiplier;

                BaseDamagePerIncrement = config.MeleeBaseDamagePerIncrement;
                MeleeIncrementStep = config.MeleeIncrementStep;
                MaxMeleeDamagePercent = config.MeleeMaxPercent;

                BaseRangedDamagePerIncrement = config.RangedBaseDamagePerIncrement;
                RangedIncrementStep = config.RangedIncrementStep;
                MaxRangedDamagePercent = config.RangedMaxDamagePercent;
                MaxRangedAccuracyPercent = config.RangedMaxAccuracyPercent;
                MaxRangedDistancePercent = config.RangedMaxDistancePercent;

                BaseBlocksWalkedPerIncrement = config.WalkingBaseBlocksPerIncrement;
                WalkingIncrementStep = config.WalkingIncrementStep;
                MaxWalkingSpeedPercent = config.WalkingMaxPercent;

                BaseSecondsPerIncrement = config.HungerBaseSecondsPerIncrement;
                HungerIncrementStep = config.HungerIncrementStep;
                MaxHungerReductionPercent = config.HungerMaxReductionPercent;

                BaseSecondsInArmorPerIncrement = config.ArmorBaseSecondsPerIncrement;
                ArmorTimeIncrementStep = config.ArmorTimeIncrementStep;
                BaseDamageBlockedPerIncrement = config.ArmorBaseDamageBlockedPerIncrement;
                ArmorDamageIncrementStep = config.ArmorDamageIncrementStep;
                BaseRepairsPerIncrement = config.ArmorBaseRepairsPerIncrement;
                ArmorRepairIncrementStep = config.ArmorRepairIncrementStep;
                MaxArmorDurabilityPercent = config.ArmorMaxDurabilityPercent;
                MaxArmorWalkSpeedPercent = config.ArmorMaxWalkSpeedPercent;

                ClothierRequiredUniqueClothes = config.ClothierRequiredUniqueClothes;

                BaseMenderRepairsPerIncrement = config.MenderBaseRepairsPerIncrement;
                MenderIncrementStep = config.MenderIncrementStep;
                MaxMenderPercent = config.MenderMaxPercent;

                BasePilfererPointsPerIncrement = config.PilfererBasePointsPerIncrement;
                PilfererIncrementStep = config.PilfererIncrementStep;
                MaxPilfererPercent = config.PilfererMaxPercent;

                BaseResourcefulAnimalsPerIncrement = config.ResourcefulBaseAnimalsPerIncrement;
                ResourcefulIncrementStep = config.ResourcefulIncrementStep;
                MaxResourcefulLootPercent = config.ResourcefulMaxLootPercent;
                MaxResourcefulSpeedPercent = config.ResourcefulMaxSpeedPercent;

                BaseForagerCropsPerIncrement = config.ForagerBaseCropsPerIncrement;
                ForagerIncrementStep = config.ForagerIncrementStep;
                MaxForagerLootPercent = config.ForagerMaxLootPercent;
                MaxForagerWildCropPercent = config.ForagerMaxWildCropPercent;

                BaseFurtiveSneakBlocksPerIncrement = config.FurtiveBaseSneakBlocksPerIncrement;
                FurtiveIncrementStep = config.FurtiveIncrementStep;
                MaxFurtivePercent = config.FurtiveMaxPercent;

                BasePreciseDamagePerIncrement = config.PreciseBaseDamagePerIncrement;
                PreciseIncrementStep = config.PreciseIncrementStep;
                MaxPrecisePercent = config.PreciseMaxPercent;

                TechnicalRequiredTranslocatorRepairs = config.TechnicalRequiredTranslocatorRepairs;

                HardyHealthMiningThreshold = config.HardyHealthMiningThreshold;
                HardyHealthArmorDurabilityThreshold = config.HardyHealthArmorDurabilityThreshold;
                HardyHealthBonus = config.HardyHealthBonus;

                // Auto-save configuration
                AutoSaveIntervalSeconds = config.AutoSaveIntervalSeconds;

                // Load disabled skills into HashSet for O(1) lookups
                DisabledSkills.Clear();
                if (config.DisabledSkills != null && config.DisabledSkills.Length > 0)
                {
                    foreach (var skill in config.DisabledSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DisabledSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                    if (DisabledSkills.Count > 0)
                    {
                        api.Logger.Notification($"[SeraphLeveling] Disabled skills: {string.Join(", ", DisabledSkills)}");
                    }
                }

                // Combat Overhaul compatibility configuration
                COEnableCompat = config.EnableCombatOverhaulCompat;
                COBaseDamagePerIncrement = config.COProficiencyBaseDamagePerIncrement;
                COIncrementStep = config.COProficiencyIncrementStep;
                COBowsProficiencyMax = config.COBowsProficiencyMax;
                COCrossbowsProficiencyMax = config.COCrossbowsProficiencyMax;
                COFirearmsProficiencyMax = config.COFirearmsProficiencyMax;
                COSlingsProficiencyMax = config.COSlingsProficiencyMax;
                COOneHandedSwordsProficiencyMax = config.COOneHandedSwordsProficiencyMax;
                COTwoHandedSwordsProficiencyMax = config.COTwoHandedSwordsProficiencyMax;
                COSpearsProficiencyMax = config.COSpearsProficiencyMax;
                COJavelinsProficiencyMax = config.COJavelinsProficiencyMax;
                COMacesProficiencyMax = config.COMacesProficiencyMax;
                COClubsProficiencyMax = config.COClubsProficiencyMax;
                COHalberdsProficiencyMax = config.COHalberdsProficiencyMax;
                COAxesProficiencyMax = config.COAxesProficiencyMax;
                COQuarterstaffProficiencyMax = config.COQuarterstaffProficiencyMax;
                COSteadyAimMax = config.COSteadyAimMax;

                api.Logger.Notification("[SeraphLeveling] Config loaded from ModConfig/" + CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to load config file: {ex.Message}. Using default values.");
            }
        }

        /// <summary>
        /// Detect if Combat Overhaul mod is loaded and log the result.
        /// </summary>
        private void DetectCombatOverhaul(ICoreServerAPI api)
        {
            // Check if Combat Overhaul is loaded using the mod ID
            IsCombatOverhaulLoaded = api.ModLoader.IsModEnabled("combatoverhaul");

            if (IsCombatOverhaulLoaded)
            {
                if (COEnableCompat)
                {
                    api.Logger.Notification("[SeraphLeveling] Combat Overhaul detected - proficiency progression enabled");
                }
                else
                {
                    api.Logger.Notification("[SeraphLeveling] Combat Overhaul detected but compatibility disabled in config");
                }
            }
        }

        /// <summary>
        /// Get the max proficiency value for a given CO proficiency stat.
        /// </summary>
        public static float GetCOProficiencyMax(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return COBowsProficiencyMax;
                case CO_CROSSBOWS_PROFICIENCY: return COCrossbowsProficiencyMax;
                case CO_FIREARMS_PROFICIENCY: return COFirearmsProficiencyMax;
                case CO_SLINGS_PROFICIENCY: return COSlingsProficiencyMax;
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return COOneHandedSwordsProficiencyMax;
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return COTwoHandedSwordsProficiencyMax;
                case CO_SPEARS_PROFICIENCY: return COSpearsProficiencyMax;
                case CO_JAVELINS_PROFICIENCY: return COJavelinsProficiencyMax;
                case CO_MACES_PROFICIENCY: return COMacesProficiencyMax;
                case CO_CLUBS_PROFICIENCY: return COClubsProficiencyMax;
                case CO_HALBERDS_PROFICIENCY: return COHalberdsProficiencyMax;
                case CO_AXES_PROFICIENCY: return COAxesProficiencyMax;
                case CO_QUARTERSTAFF_PROFICIENCY: return COQuarterstaffProficiencyMax;
                case CO_STEADY_AIM: return COSteadyAimMax;
                default: return 0.3f; // Safe default
            }
        }

        /// <summary>
        /// Get the max credits for a CO proficiency (max bonus * 100).
        /// </summary>
        public static int GetCOProficiencyMaxCredits(string proficiencyStat)
        {
            return (int)(GetCOProficiencyMax(proficiencyStat) * 100);
        }

        /// <summary>
        /// Calculate proficiency bonus from credits.
        /// Each credit = 0.01 bonus.
        /// </summary>
        public static float CalculateCOProficiencyBonus(int credits, float maxBonus)
        {
            int maxCredits = (int)(maxBonus * 100);
            int cappedCredits = Math.Min(credits, maxCredits);
            return cappedCredits * 0.01f;
        }

        /// <summary>
        /// Detect Combat Overhaul weapon type from item code.
        /// Returns (proficiencyStat, weaponCode) or (null, null) if not a CO weapon.
        /// </summary>
        public static (string proficiencyStat, string weaponCode) GetCOWeaponType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return (null, null);

            // Remove namespace prefix for pattern matching
            string codeToCheck = itemCode;
            if (itemCode.Contains(":"))
            {
                codeToCheck = itemCode.Substring(itemCode.IndexOf(':') + 1);
            }
            string lowerCode = codeToCheck.ToLowerInvariant();

            // Crossbows (check before bows)
            if (lowerCode.StartsWith("crossbow-") || lowerCode.StartsWith("crossbow") ||
                lowerCode.Contains("crossbow"))
                return (CO_CROSSBOWS_PROFICIENCY, itemCode);

            // Firearms
            if (lowerCode.StartsWith("musket-") || lowerCode.StartsWith("pistol-") ||
                lowerCode.StartsWith("rifle-") || lowerCode.StartsWith("blunderbuss-") ||
                lowerCode.StartsWith("arquebus-") || lowerCode.Contains("firearm") ||
                lowerCode.Contains("gun-"))
                return (CO_FIREARMS_PROFICIENCY, itemCode);

            // Two-Handed Swords (check before one-handed)
            // Combat Armory uses "sword-great-" and "sword-long-" formats
            if (lowerCode.StartsWith("greatsword-") || lowerCode.StartsWith("zweihander-") ||
                lowerCode.StartsWith("claymore-") || lowerCode.StartsWith("flamberge-") ||
                lowerCode.StartsWith("montante-") || lowerCode.StartsWith("nodachi-") ||
                lowerCode.StartsWith("2hsword-") || lowerCode.StartsWith("2h-sword-") ||
                lowerCode.StartsWith("twohandedsword-") || lowerCode.StartsWith("twohanded-sword-") ||
                lowerCode.StartsWith("sword-great-") || // Combat Armory greatswords
                lowerCode.StartsWith("sword-long-") ||  // Combat Armory longswords
                lowerCode.StartsWith("longsword-") ||   // Standard longsword prefix
                (lowerCode.Contains("twohanded") && lowerCode.Contains("sword")) ||
                (lowerCode.Contains("2h") && lowerCode.Contains("sword")))
                return (CO_TWO_HANDED_SWORDS_PROFICIENCY, itemCode);

            // One-Handed Swords
            // Note: sword-long- and longsword- are handled above as two-handed
            if (lowerCode.StartsWith("sword-") || lowerCode.StartsWith("blade-") ||
                lowerCode.StartsWith("shortsword-") || lowerCode.StartsWith("sword-short-") ||
                lowerCode.StartsWith("sword-arming-") || // Combat Armory arming swords
                lowerCode.StartsWith("saber-") || lowerCode.StartsWith("sabre-") || // Both spellings
                lowerCode.StartsWith("rapier-") || lowerCode.StartsWith("scimitar-") ||
                lowerCode.StartsWith("cutlass-") || lowerCode.StartsWith("falx-") ||
                lowerCode.StartsWith("falchion-") || lowerCode.StartsWith("dagger-") ||
                lowerCode.StartsWith("knife-") || lowerCode.StartsWith("kopis-") ||
                lowerCode.StartsWith("gladius-") || lowerCode.StartsWith("messer-"))
                return (CO_ONE_HANDED_SWORDS_PROFICIENCY, itemCode);

            // Halberds (polearms with axe heads)
            if (lowerCode.StartsWith("halberd-") || lowerCode.StartsWith("poleaxe-") ||
                lowerCode.StartsWith("glaive-") || lowerCode.StartsWith("bardiche-") ||
                lowerCode.StartsWith("voulge-") || lowerCode.StartsWith("guisarme-"))
                return (CO_HALBERDS_PROFICIENCY, itemCode);

            // Quarterstaff
            if (lowerCode.StartsWith("quarterstaff-") || lowerCode.StartsWith("staff-") ||
                lowerCode.StartsWith("bo-") || lowerCode.Contains("bo-staff"))
                return (CO_QUARTERSTAFF_PROFICIENCY, itemCode);

            // Maces
            if (lowerCode.StartsWith("mace-") || lowerCode.StartsWith("morningstar-") ||
                lowerCode.StartsWith("flail-") || lowerCode.StartsWith("warhammer-"))
                return (CO_MACES_PROFICIENCY, itemCode);

            // Clubs
            if (lowerCode.StartsWith("club-") || lowerCode.StartsWith("cudgel-") ||
                lowerCode.StartsWith("baton-") || lowerCode.StartsWith("truncheon-"))
                return (CO_CLUBS_PROFICIENCY, itemCode);

            // Axes (combat axes, not tool axes)
            if (lowerCode.StartsWith("battleaxe-") || lowerCode.StartsWith("waraxe-") ||
                lowerCode.StartsWith("handaxe-") || lowerCode.StartsWith("hatchet-") ||
                (lowerCode.StartsWith("axe-") && !lowerCode.Contains("pickaxe")))
                return (CO_AXES_PROFICIENCY, itemCode);

            // Javelins (thrown spears)
            if (lowerCode.StartsWith("javelin-") || lowerCode.StartsWith("pilum-") ||
                lowerCode.StartsWith("throwing-spear-") || lowerCode.StartsWith("thrown-spear-") ||
                lowerCode.StartsWith("dart-") || lowerCode.StartsWith("plumbata-") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("throwingspear") ||
                lowerCode.Contains("thrownspear") || lowerCode.Contains("throwing-spear"))
                return (CO_JAVELINS_PROFICIENCY, itemCode);

            // Spears (melee)
            if (lowerCode.StartsWith("spear-") || lowerCode.StartsWith("pike-") ||
                lowerCode.StartsWith("lance-") || lowerCode.StartsWith("trident-"))
                return (CO_SPEARS_PROFICIENCY, itemCode);

            // Bows (standard - after crossbows check)
            if (lowerCode.StartsWith("bow-") || lowerCode.StartsWith("longbow-") ||
                lowerCode.StartsWith("shortbow-") || lowerCode.StartsWith("recurvebow-") ||
                lowerCode.StartsWith("recurve-") || lowerCode.StartsWith("composite-bow"))
                return (CO_BOWS_PROFICIENCY, itemCode);

            // Slings
            if (lowerCode.StartsWith("sling-") || lowerCode == "sling")
                return (CO_SLINGS_PROFICIENCY, itemCode);

            // Debug logging for unmatched weapons - helps identify missing item codes
            if (lowerCode.Contains("sword") || lowerCode.Contains("blade") || lowerCode.Contains("spear") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("axe") || lowerCode.Contains("mace") ||
                lowerCode.Contains("hammer") || lowerCode.Contains("club") || lowerCode.Contains("bow") ||
                lowerCode.Contains("staff") || lowerCode.Contains("halberd") || lowerCode.Contains("pike") ||
                lowerCode.Contains("sabre") || lowerCode.Contains("weapon") || lowerCode.Contains("dagger"))
            {
                ServerApi?.Logger?.Debug($"[SeraphLeveling] CO: Unmatched weapon item code: '{itemCode}' (normalized: '{lowerCode}')");
            }

            return (null, null);
        }

        /// <summary>
        /// Check if a proficiency stat is a ranged proficiency (contributes to Steady Aim).
        /// </summary>
        public static bool IsCORangedProficiency(string proficiencyStat)
        {
            return proficiencyStat == CO_BOWS_PROFICIENCY ||
                   proficiencyStat == CO_CROSSBOWS_PROFICIENCY ||
                   proficiencyStat == CO_FIREARMS_PROFICIENCY ||
                   proficiencyStat == CO_SLINGS_PROFICIENCY;
        }

        /// <summary>
        /// Process Combat Overhaul proficiency damage dealt by a player.
        /// Called from Harmony patch when CO is enabled.
        /// </summary>
        public static void ProcessCOProficiencyDamage(IServerPlayer attackerPlayer, string proficiencyStat, string weaponCode, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(proficiencyStat) || string.IsNullOrEmpty(weaponCode)) return;

            // Skip if CO compat is disabled
            if (!IsCOCompatEnabled) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player CO progress data
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            // Get max credits for this proficiency
            int maxCredits = GetCOProficiencyMaxCredits(proficiencyStat);

            // Get or create progress for this proficiency type
            var proficiencyProgress = playerProgress.GetProficiencyProgress(proficiencyStat);

            // Skip if already at max for this proficiency
            if (proficiencyProgress.TotalCredits >= maxCredits)
            {
                // Still process Steady Aim if this is ranged and steady aim isn't maxed
                if (IsCORangedProficiency(proficiencyStat))
                {
                    ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
                }
                return;
            }

            // Get or create progress for this specific weapon
            var weaponProgress = proficiencyProgress.GetWeaponProgress(weaponCode, COBaseDamagePerIncrement);

            int oldCredits = proficiencyProgress.TotalCredits;

            // Add damage to this weapon's progress
            weaponProgress.DamageInIncrement += damage;

            // Check if we've earned any new credits with this weapon
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && proficiencyProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                proficiencyProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += COIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned CO {proficiencyStat} credit {proficiencyProgress.TotalCredits} with {weaponCode}");
            }

            pendingCOProgressSave = true;

            // If credits increased, update the stat and notify player
            if (proficiencyProgress.TotalCredits > oldCredits)
            {
                ApplyCOProficiencyBonus(attackerPlayer, proficiencyStat, proficiencyProgress.TotalCredits);

                // Notify player of level up
                float bonus = CalculateCOProficiencyBonus(proficiencyProgress.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Proficiency level up! Your {GetCOProficiencyDisplayName(proficiencyStat)} is now +{bonus:F2}.",
                    EnumChatType.Notification);
            }

            // Also process Steady Aim if this is a ranged proficiency
            if (IsCORangedProficiency(proficiencyStat))
            {
                ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
            }
        }

        /// <summary>
        /// Process Steady Aim progression (shared with ranged proficiencies).
        /// </summary>
        private static void ProcessCOSteadyAimProgress(IServerPlayer player, COPlayerProgressData playerProgress, float damage)
        {
            int maxSteadyAimCredits = GetCOProficiencyMaxCredits(CO_STEADY_AIM);

            // Skip if already at max
            if (playerProgress.SteadyAimCredits >= maxSteadyAimCredits) return;

            // Use a simple progression for Steady Aim (not per-weapon)
            // We'll use SteadyAimCredits to track total credits earned
            // For simplicity, every ranged damage point adds to a shared progress counter
            // We'll track damage in a special "steadyaim" key in the bows proficiency
            var steadyAimProgress = playerProgress.GetProficiencyProgress(CO_STEADY_AIM);
            var sharedProgress = steadyAimProgress.GetWeaponProgress("_ranged_combined", COBaseDamagePerIncrement);

            int oldCredits = playerProgress.SteadyAimCredits;

            sharedProgress.DamageInIncrement += damage;

            while (sharedProgress.DamageInIncrement >= sharedProgress.CurrentIncrementSize && playerProgress.SteadyAimCredits < maxSteadyAimCredits)
            {
                playerProgress.SteadyAimCredits++;
                sharedProgress.DamageInIncrement -= sharedProgress.CurrentIncrementSize;
                sharedProgress.CurrentIncrementSize += COIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned CO Steady Aim credit {playerProgress.SteadyAimCredits}");
            }

            if (playerProgress.SteadyAimCredits > oldCredits)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);

                float bonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Steady Aim improved! Your aim stability is now +{bonus:F2}.",
                    EnumChatType.Notification);
            }
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Delegates to ApplyCOProficiencyBonusWithCancellation for negative trait handling.
        /// </summary>
        private static void ApplyCOProficiencyBonus(IServerPlayer player, string proficiencyStat, int credits)
        {
            ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
        }

        /// <summary>
        /// Apply Combat Overhaul Steady Aim bonus to a player.
        /// Handles Trembling Aim negative trait cancellation.
        /// </summary>
        private static void ApplyCOSteadyAimBonus(IServerPlayer player, int credits)
        {
            if (player?.Entity == null) return;

            // Check for Trembling Aim negative trait
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasTremblingAim = cache?.HasCOTremblingAim ?? false;

            // Calculate remaining penalty and net bonus
            float tremblingAimRemaining = 0f;
            float netBonus = 0f;

            if (hasTremblingAim)
            {
                // Trembling Aim penalty is 0.3, cancelled by Steady Aim credits (30 credits to cancel)
                int creditsToCancel = (int)(CO_TREMBLING_AIM_PENALTY * 100); // 30
                tremblingAimRemaining = Math.Max(0, CO_TREMBLING_AIM_PENALTY - credits * 0.01f);
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), COSteadyAimMax);
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, COSteadyAimMax);
            }

            // Apply steadyAim stat
            string statCode = CO_STAT_PREFIX + CO_STEADY_AIM;
            player.Entity.Stats.Set(CO_STEADY_AIM, statCode, netBonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_STEADY_AIM_CREDITS, credits);
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, tremblingAimRemaining);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_STEADY_AIM_CREDITS);
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Handles Clumsy Hands negative trait cancellation for ranged proficiencies.
        /// </summary>
        private static void ApplyCOProficiencyBonusWithCancellation(IServerPlayer player, string proficiencyStat, int credits)
        {
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasClumsyHands = cache?.HasCOClumsyHands ?? false;
            bool hasFrightenedOfMelee = cache?.HasCOFrightenedOfMelee ?? false;

            float maxBonus = GetCOProficiencyMax(proficiencyStat);
            float netBonus = 0f;

            // Handle Clumsy Hands for ranged proficiencies
            if (hasClumsyHands && IsCORangedProficiency(proficiencyStat))
            {
                // Clumsy Hands gives -0.3 to bows, crossbows, firearms (30 credits to cancel each)
                int creditsToCancel = (int)(CO_CLUMSY_HANDS_PENALTY * 100); // 30
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);

                // Sync remaining penalty for UI
                float remaining = Math.Max(0, CO_CLUMSY_HANDS_PENALTY - credits * 0.01f);
                player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, remaining);
            }
            // Handle Frightened of Melee for melee proficiencies (tier-based, more complex)
            else if (hasFrightenedOfMelee && !IsCORangedProficiency(proficiencyStat))
            {
                // Frightened of Melee gives -1 slashing damage tier
                // This needs 100 credits to cancel (1 tier = 100 credits in our system)
                int creditsToCancel = CO_FRIGHTENED_MELEE_TIER_PENALTY * 100; // 100
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);

                // Sync remaining penalty for UI
                int remainingTiers = Math.Max(0, CO_FRIGHTENED_MELEE_TIER_PENALTY - credits / 100);
                player.Entity.WatchedAttributes.SetInt(WATCHED_CO_FRIGHTENED_MELEE_REMAINING, remainingTiers);
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, maxBonus);
            }

            // Apply stat using CO stat name with our prefix
            string statCode = CO_STAT_PREFIX + proficiencyStat;
            player.Entity.Stats.Set(proficiencyStat, statCode, netBonus, false);

            // Sync credits to WatchedAttributes
            string watchedKey = $"sitCO{proficiencyStat}Credits";
            player.Entity.WatchedAttributes.SetInt(watchedKey, credits);
            player.Entity.WatchedAttributes.MarkPathDirty(watchedKey);
        }

        /// <summary>
        /// Get a display-friendly name for a CO proficiency stat.
        /// </summary>
        public static string GetCOProficiencyDisplayName(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return "Bows Proficiency";
                case CO_CROSSBOWS_PROFICIENCY: return "Crossbows Proficiency";
                case CO_FIREARMS_PROFICIENCY: return "Firearms Proficiency";
                case CO_SLINGS_PROFICIENCY: return "Slings Proficiency";
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return "One-Handed Swords Proficiency";
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return "Two-Handed Swords Proficiency";
                case CO_SPEARS_PROFICIENCY: return "Spears Proficiency";
                case CO_JAVELINS_PROFICIENCY: return "Javelins Proficiency";
                case CO_MACES_PROFICIENCY: return "Maces Proficiency";
                case CO_CLUBS_PROFICIENCY: return "Clubs Proficiency";
                case CO_HALBERDS_PROFICIENCY: return "Halberds Proficiency";
                case CO_AXES_PROFICIENCY: return "Axes Proficiency";
                case CO_QUARTERSTAFF_PROFICIENCY: return "Quarterstaff Proficiency";
                case CO_STEADY_AIM: return "Steady Aim";
                default: return proficiencyStat;
            }
        }

        /// <summary>
        /// Apply all CO bonuses for a player (called on join/reconnect).
        /// </summary>
        public static void ApplyAllCOBonuses(IServerPlayer player)
        {
            if (!IsCOCompatEnabled || player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            if (!COProgress.TryGetValue(playerUid, out var playerProgress)) return;

            // Apply each proficiency bonus
            foreach (var proficiency in playerProgress.Proficiencies)
            {
                if (proficiency.Key != CO_STEADY_AIM)
                {
                    ApplyCOProficiencyBonus(player, proficiency.Key, proficiency.Value.TotalCredits);
                }
            }

            // Apply Steady Aim bonus
            if (playerProgress.SteadyAimCredits > 0)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);
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
                ServerApi.Logger.Debug($"[SeraphLeveling] Config saved (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist config: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No config data found, using defaults");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load config: {ex.Message}");
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
        /// Gets or sets the player's clothier progress (unique clothes count).
        /// </summary>
        private TextCommandResult OnTraitClothierLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentLevel = progress.UniqueClothesWorn.Count;
                string status = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - currentLevel} more needed to unlock.";
                return TextCommandResult.Success($"Current clothier level: {currentLevel}/{ClothierRequiredUniqueClothes}. {status}");
            }

            if (newLevel.Value < 0)
                return TextCommandResult.Error("Level must be 0 or greater.");

            // Clear the existing clothes set
            progress.UniqueClothesWorn.Clear();

            // Add placeholder entries up to the desired level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.UniqueClothesWorn.Add($"__placeholder_cloth_{i}");
            }

            // Set unlock status based on whether we've reached the required amount
            progress.SewingKitUnlocked = newLevel.Value >= ClothierRequiredUniqueClothes;

            pendingClothierProgressSave = true;

            // Apply the bonus (this updates WatchedAttributes and extraTraits)
            ApplyClothierBonusStatic(player, progress);

            string newStatus = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - newLevel.Value} more needed to unlock.";
            return TextCommandResult.Success($"Clothier level set to {newLevel.Value}/{ClothierRequiredUniqueClothes}. {newStatus}");
        }

        /// <summary>
        /// Tick handler for clothing tracking.
        /// </summary>
        private void OnClothingTick(float dt)
        {
            if (ServerApi == null) return;

            // Skip clothier progression if disabled
            if (IsSkillDisabled("clothier")) return;

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
                                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} wore new clothing: {itemCode} ({clothierProgress.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes})");

                                    // Check if unlocked
                                    if (clothierProgress.UniqueClothesWorn.Count >= ClothierRequiredUniqueClothes && !clothierProgress.SewingKitUnlocked)
                                    {
                                        clothierProgress.SewingKitUnlocked = true;
                                        ApplyClothierBonusStatic(player, clothierProgress);
                                        player.SendMessage(GlobalConstants.GeneralChatGroup,
                                            Lang.Get("seraphleveling:message-clothier-unlocked"),
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
                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} used {kitsUsed} sewing kit(s) for repair");

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
                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired {itemCode} (+{repairPercent}% durability) via durability tracking");
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
            // Skip furtive progression if disabled
            if (IsSkillDisabled("furtive")) return;

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

                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last sneaking position (using Position2D struct to avoid Vec3d allocations)
                if (!lastSneakingPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastSneakingPositions[playerUid] = new Position2D(currentX, currentZ);
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentX - lastPos.X;
                double dz = currentZ - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position (no allocation - struct assignment)
                lastSneakingPositions[playerUid] = new Position2D(currentX, currentZ);

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

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned furtive credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingFurtiveProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    ApplyFurtiveBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        Lang.Get("seraphleveling:message-furtive-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
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

            // Check if precise skill is disabled
            if (IsSkillDisabled("precise")) return;

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

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned precise credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            // Mark for saving if any progress was made
            if (damage > 0)
            {
                pendingPreciseProgressSave = true;
            }

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyPreciseBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-precise-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits),
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

            // Check if hardyhealth skill is disabled
            if (IsSkillDisabled("hardyhealth")) return;

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
                Lang.Get("seraphleveling:message-hardy-health-unlock", HardyHealthBonus),
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
                Lang.Get("seraphleveling:message-tinkerer-unlock"),
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
                Lang.Get("seraphleveling:message-merciless-unlock"),
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
                Lang.Get("seraphleveling:message-bowyer-unlock"),
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
                Lang.Get("seraphleveling:message-improviser-unlock"),
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
                Lang.Get("seraphleveling:message-claustrophobic-removed"),
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
                // Note: Stats use WeightedSum with base 1.0. Vanilla uses -0.15/-0.10, so we use +0.15/+0.10 to cancel
                player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicRemoval", 0.15f, false); // +15% to negate -15%
                player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicRemoval", 0.10f, false); // +10% to negate -10%
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
        /// Gets or sets the player's mender level.
        /// </summary>
        private TextCommandResult OnTraitMenderLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current mender level: {progress.TotalCredits}/{MaxMenderPercent} (+{currentBonus}% durability)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxMenderPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxMenderPercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.RepairsInIncrement = 0;
            progress.CurrentIncrementSize = BaseMenderRepairsPerIncrement;

            // Recalculate increment size for this level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += MenderIncrementStep;
            }

            pendingMenderProgressSave = true;

            int bonusPercent = ApplyMenderBonusStatic(player, progress.TotalCredits);
            return TextCommandResult.Success($"Mender level set to {newLevel.Value} (+{bonusPercent}% durability).");
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

            // Check if mender skill is disabled
            if (IsSkillDisabled("mender")) return;

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

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned mender credit {progress.TotalCredits}");
            }

            pendingMenderProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyMenderBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when capped)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-mender-level-up", progress.TotalCredits, progress.TotalCredits),
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
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Pilferer progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% rusty gear, vessel contents, and collection chance");
            if (hasVanillaPilferer)
            {
                sb.AppendLine($"(Has vanilla Pilferer trait)");
            }
            if (progress.TotalCredits < maxCredits)
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
        /// Gets or sets the player's pilferer level.
        /// </summary>
        private TextCommandResult OnTraitPilfererLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current pilferer level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% bonuses)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.PointsInIncrement = 0;
            progress.CurrentIncrementSize = BasePilfererPointsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += PilfererIncrementStep;
            }

            pendingPilfererProgressSave = true;

            int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);
            return TextCommandResult.Success($"Pilferer level set to {newLevel.Value} (+{bonusPercent}% bonuses).");
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
        /// Get the maximum pilferer credits a player can earn based on their traits.
        /// Players with Heavyhanded trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxPilfererCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxPilfererPercent;

            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Heavyhanded vessel penalty is 10%, need 10 extra levels to cancel it
            if (hasHeavyhanded)
            {
                return MaxPilfererPercent + VANILLA_HEAVYHANDED_VESSEL_PENALTY;
            }

            return MaxPilfererPercent;
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
        /// Also handles Heavyhanded vessel loot negative trait cancellation.
        /// </summary>
        private static int ApplyPilfererBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            // Calculate remaining Heavyhanded vessel penalty
            int heavyhandedVesselRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_VESSEL_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netLevel = level;
            if (hasHeavyhanded)
            {
                // Heavyhanded vessel penalty is cancelled first, then bonus starts
                netLevel = Math.Max(0, level - VANILLA_HEAVYHANDED_VESSEL_PENALTY);
            }

            // Apply vanilla caps if player has Pilferer trait
            int vanillaBonus = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int maxEarnable = Math.Max(0, MaxPilfererPercent - vanillaBonus);
            int bonusPercent = Math.Min(netLevel, maxEarnable);

            float bonus = bonusPercent * 0.01f;

            // Apply to pilferer-related stats
            // Note: These are additive stats where vanilla traits use values like 0.1 for +10%.
            // The game applies (1 + blended) as the multiplier. Using just the bonus value.
            player.Entity.Stats.Set("rustyGearDropRate", PILFERER_RUSTY_GEAR_STAT_CODE, bonus, false);
            player.Entity.Stats.Set("vesselContentsDropRate", PILFERER_VESSEL_CONTENTS_STAT_CODE, bonus, false);
            player.Entity.Stats.Set("wholeVesselLootChance", PILFERER_WHOLE_VESSEL_STAT_CODE, bonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPilferer", hasVanillaPilferer);

            // Sync negative trait status (Heavyhanded vessel part)
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_VESSEL_REMAINING, heavyhandedVesselRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_PILFERER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, PILFERER_TRAIT_CODE, level > 0 && !hasVanillaPilferer);

            return bonusPercent;
        }

        /// <summary>
        /// Process cracked vessel break (called from OnBlockBroken for cracked vessels).
        /// Only cracked vessels count - they can't be re-placed by players.
        /// </summary>
        public static void ProcessVesselBreak(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            progress.PointsInIncrement += PILFERER_VESSEL_POINTS;

            while (progress.PointsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.PointsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += PilfererIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned pilferer credit {progress.TotalCredits} from cracked vessel");
            }

            pendingPilfererProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyPilfererBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Heavyhanded)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-pilferer-level-up", progress.TotalCredits, progress.TotalCredits),
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
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Resourceful progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{lootBonus}% animal loot, +{speedBonus}% harvesting speed");
            if (hasVanillaResourceful)
            {
                sb.AppendLine($"(Has vanilla Resourceful trait)");
            }
            if (progress.TotalCredits < maxCredits)
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
        /// Gets or sets the player's resourceful level.
        /// </summary>
        private TextCommandResult OnTraitResourcefulLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current resourceful level: {progress.TotalCredits}/{maxCredits} (+{lootBonus}% loot)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.AnimalsInIncrement = 0;
            progress.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ResourcefulIncrementStep;
            }

            pendingResourcefulProgressSave = true;

            ApplyResourcefulBonusStatic(player, progress.TotalCredits);
            int newLootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
            return TextCommandResult.Success($"Resourceful level set to {newLevel.Value} (+{newLootBonus}% loot).");
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
        /// Get the maximum resourceful credits a player can earn based on their traits.
        /// Players with Kind trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxResourcefulCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxResourcefulLootPercent;

            bool hasKind = PlayerHasVanillaKind(entity);

            // Kind has two penalties - use the larger one (speed = 25%)
            // Loot penalty is 10%, speed penalty is 25%
            if (hasKind)
            {
                return MaxResourcefulLootPercent + VANILLA_KIND_SPEED_PENALTY;
            }

            return MaxResourcefulLootPercent;
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
        /// Also handles Kind negative trait cancellation.
        /// </summary>
        private static void ApplyResourcefulBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            // If resourceful skill is disabled, set bonus to 0 and return
            if (IsSkillDisabled("resourceful"))
            {
                player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, 0f, false);
                player.Entity.Stats.Set("harvestingSpeedMul", RESOURCEFUL_SPEED_STAT_CODE, 0f, false);
                return;
            }

            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            bool hasKind = PlayerHasVanillaKind(player.Entity);

            // Calculate remaining Kind penalties
            int kindLootRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_LOOT_PENALTY, level) : 0;
            int kindSpeedRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_SPEED_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netLootLevel = level;
            int netSpeedLevel = level;

            if (hasKind)
            {
                // Kind penalties are cancelled first, then bonuses start
                netLootLevel = Math.Max(0, level - VANILLA_KIND_LOOT_PENALTY);
                netSpeedLevel = Math.Max(0, level - VANILLA_KIND_SPEED_PENALTY);
            }

            // Apply vanilla caps if player has Resourceful trait
            int vanillaLootBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int vanillaSpeedBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_SPEED_BONUS : 0;

            int maxEarnableLoot = Math.Max(0, MaxResourcefulLootPercent - vanillaLootBonus);
            int maxEarnableSpeed = Math.Max(0, MaxResourcefulSpeedPercent - vanillaSpeedBonus);

            int lootBonusPercent = Math.Min(netLootLevel, maxEarnableLoot);
            int speedBonusPercent = Math.Min(netSpeedLevel, maxEarnableSpeed);

            float lootBonus = lootBonusPercent * 0.01f;
            float speedBonus = speedBonusPercent * 0.01f;

            // Apply to resourceful-related stats
            // Note: Stats use WeightedSum blending with a base of 1.0. Vanilla traits set values
            // like 0.1 for +10%. We set just the bonus value, not 1 + bonus.
            player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, lootBonus, false);
            player.Entity.Stats.Set("harvestingSpeedMul", RESOURCEFUL_SPEED_STAT_CODE, speedBonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LOOT_BONUS, lootBonusPercent);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_SPEED_BONUS, speedBonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaResourceful", hasVanillaResourceful);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasKind", hasKind);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_LOOT_REMAINING, kindLootRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_SPEED_REMAINING, kindSpeedRemaining);

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

            // Check if resourceful skill is disabled
            if (IsSkillDisabled("resourceful")) return;

            string playerUid = player.PlayerUID;
            var progress = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            progress.AnimalsInIncrement++;

            while (progress.AnimalsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.AnimalsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ResourcefulIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned resourceful credit {progress.TotalCredits}");
            }

            pendingResourcefulProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyResourcefulBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Kind)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-resourceful-level-up", progress.TotalCredits, progress.TotalCredits),
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
            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);
            int maxCredits = GetMaxForagerCredits(player.Entity);

            // Use net bonuses from WatchedAttributes (set by ApplyForagerBonusStatic)
            int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);

            var sb = new StringBuilder();
            sb.AppendLine($"Forager progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{netLootBonus}% foraging loot, +{netWildCropBonus}% wild crop drops");
            if (hasVanillaForager)
            {
                sb.AppendLine($"(Has vanilla Forager trait)");
            }
            if (hasCivil)
            {
                int civilRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_CIVIL_REMAINING, 0);
                if (civilRemaining > 0)
                    sb.AppendLine($"(Civil penalty remaining: -{civilRemaining}% foraging loot)");
                else
                    sb.AppendLine("(Civil penalty cancelled!)");
            }
            if (hasHeavyhanded)
            {
                int forageRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, 0);
                int wildCropRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, 0);
                if (forageRemaining > 0 || wildCropRemaining > 0)
                    sb.AppendLine($"(Heavyhanded penalties remaining: -{forageRemaining}% foraging, -{wildCropRemaining}% wild crop)");
                else
                    sb.AppendLine("(Heavyhanded penalties cancelled!)");
            }
            if (progress.TotalCredits < maxCredits)
            {
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
        /// Gets or sets the player's forager level.
        /// </summary>
        private TextCommandResult OnTraitForagerLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
                int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);
                return TextCommandResult.Success($"Current forager level: {progress.TotalCredits}/{maxCredits} (+{netLootBonus}% loot, +{netWildCropBonus}% wild crop)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.CropsInIncrement = 0;
            progress.CurrentIncrementSize = BaseForagerCropsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ForagerIncrementStep;
            }

            pendingForagerProgressSave = true;

            ApplyForagerBonusStatic(player, progress.TotalCredits);
            // Use net bonuses from WatchedAttributes which accounts for Civil/Heavyhanded penalties
            int newNetLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int newNetWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);
            return TextCommandResult.Success($"Forager level set to {newLevel.Value} (+{newNetLootBonus}% loot, +{newNetWildCropBonus}% wild crop).");
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
        /// Get the maximum forager credits a player can earn based on their traits.
        /// Players with negative traits (Civil, Heavyhanded) can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxForagerCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxForagerLootPercent;

            bool hasCivil = PlayerHasVanillaCivil(entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Civil penalty is 10% foraging loot, need 10 extra levels to cancel it
            if (hasCivil)
            {
                return MaxForagerLootPercent + VANILLA_CIVIL_FORAGING_PENALTY;
            }

            // Heavyhanded has two penalties - use the larger one (wild crop = 20%)
            if (hasHeavyhanded)
            {
                return MaxForagerWildCropPercent + VANILLA_HEAVYHANDED_WILD_CROP_PENALTY;
            }

            return MaxForagerLootPercent;
        }

        /// <summary>
        /// Apply forager bonus.
        /// Also handles Civil and Heavyhanded negative trait cancellation.
        /// </summary>
        private static void ApplyForagerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            int lootBonusPercent = CalculateForagerLootBonusPercent(level, player.Entity);
            int wildCropBonusPercent = CalculateForagerWildCropBonusPercent(level, player.Entity);

            // Calculate remaining negative trait penalties
            int civilRemaining = hasCivil ? CalculateRemainingPenalty(VANILLA_CIVIL_FORAGING_PENALTY, level) : 0;
            int heavyhandedForagingRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_FORAGING_PENALTY, level) : 0;
            int heavyhandedWildCropRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_WILD_CROP_PENALTY, level) : 0;

            // Calculate net bonus (earned bonus - remaining penalty)
            // For Civil: need to earn level > 10 to start gaining bonus
            // For Heavyhanded: need to earn level > 15 for foraging, > 20 for wild crop
            int netLootBonus = lootBonusPercent;
            int netWildCropBonus = wildCropBonusPercent;

            if (hasCivil)
            {
                // Civil penalty is cancelled first, then bonus starts
                netLootBonus = Math.Max(0, level - VANILLA_CIVIL_FORAGING_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                }
            }

            if (hasHeavyhanded)
            {
                // Heavyhanded penalties are cancelled first
                netLootBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_FORAGING_PENALTY);
                netWildCropBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_WILD_CROP_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                    netWildCropBonus = Math.Min(netWildCropBonus, MaxForagerWildCropPercent);
                }
            }

            float lootBonus = netLootBonus * 0.01f;
            float wildCropBonus = netWildCropBonus * 0.01f;

            // Apply to forager-related stats
            // Note: forageDropRate/wildCropDropRate are additive stats where vanilla traits use
            // values like 0.1 for +10%. The game applies (1 + blended) as the multiplier.
            // Using just the bonus value (not 1 + bonus) to avoid doubling.
            player.Entity.Stats.Set("forageDropRate", FORAGER_LOOT_STAT_CODE, lootBonus, false);
            player.Entity.Stats.Set("wildCropDropRate", FORAGER_WILD_CROP_STAT_CODE, wildCropBonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LOOT_BONUS, netLootBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_WILD_CROP_BONUS, netWildCropBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaForager", hasVanillaForager);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasCivil", hasCivil);
            player.Entity.WatchedAttributes.SetInt(WATCHED_CIVIL_REMAINING, civilRemaining);
            player.Entity.WatchedAttributes.SetBool("sitHasHeavyhanded", hasHeavyhanded);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, heavyhandedForagingRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, heavyhandedWildCropRemaining);

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

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            progress.CropsInIncrement++;

            while (progress.CropsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.CropsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ForagerIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned forager credit {progress.TotalCredits}");
            }

            pendingForagerProgressSave = true;

            if (progress.TotalCredits > oldCredits)
            {
                ApplyForagerBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Civil/Heavyhanded)
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-forager-level-up", progress.TotalCredits, progress.TotalCredits, progress.TotalCredits),
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
        /// Check if a block is a loot vessel / cracked vessel (for Pilferer progression).
        /// Only loot vessels count - they can't be re-placed by players, preventing exploits.
        /// Storage vessels and urns are excluded since players can place and break them repeatedly.
        /// Block code: game:lootvessel-*
        /// </summary>
        private static bool IsCrackedVesselBlock(int blockId)
        {
            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            // Only loot vessels (cracked vessels) count - they don't drop themselves when broken
            if (blockCode.Contains("lootvessel")) return true;

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
        /// Gets or sets the player's furtive level.
        /// </summary>
        private TextCommandResult OnTraitFurtiveLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
                int currentBonus = hasVanillaFurtive ? VANILLA_FURTIVE_DETECTION_REDUCTION : progress.TotalCredits;
                return TextCommandResult.Success($"Current furtive level: {progress.TotalCredits}/{MaxFurtivePercent} (-{currentBonus}% detection)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxFurtivePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxFurtivePercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.BlocksInIncrement = 0;
            progress.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement + (newLevel.Value * FurtiveIncrementStep);

            pendingFurtiveProgressSave = true;
            int bonusPercent = ApplyFurtiveBonusStatic(player, newLevel.Value);

            return TextCommandResult.Success($"Furtive level set to {newLevel.Value} (-{bonusPercent}% detection).");
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
        /// Gets or sets the player's precise level.
        /// </summary>
        private TextCommandResult OnTraitPreciseLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
                int currentBonus = hasVanillaPrecise ? VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS : progress.TotalCredits;
                return TextCommandResult.Success($"Current precise level: {progress.TotalCredits}/{MaxPrecisePercent} (+{currentBonus}% mechanical damage)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxPrecisePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxPrecisePercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.WeaponProgress.Clear();

            pendingPreciseProgressSave = true;
            int bonusPercent = ApplyPreciseBonusStatic(player, newLevel.Value);

            // Check for trait unlocks that depend on precise level
            CheckTinkererUnlock(player);

            return TextCommandResult.Success($"Precise level set to {newLevel.Value} (+{bonusPercent}% mechanical damage).");
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

            // Check if technical skill is disabled
            if (IsSkillDisabled("technical")) return;

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            // Increment translocator repairs
            progress.TranslocatorsRepaired++;
            pendingTechnicalProgressSave = true;

            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired translocator ({progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs})");

            // Check if we've reached the unlock threshold
            if (progress.TranslocatorsRepaired >= TechnicalRequiredTranslocatorRepairs)
            {
                progress.IsUnlocked = true;
                ApplyTechnicalBonusStatic(player, true);

                // Notify player
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("seraphleveling:message-technical-unlock"),
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

        /// <summary>
        /// Handler for /trait reset command.
        /// Resets all trait progression to 0 for the calling player.
        /// </summary>
        private TextCommandResult OnTraitResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;

            // Reset Mining
            if (MiningProgress.TryGetValue(playerUid, out var miningProg))
            {
                miningProg.TotalCredits = 0;
                miningProg.PickaxeProgress.Clear();
                pendingMiningProgressSave = true;
            }
            ApplyMiningBonus(player, 0);

            // Reset Melee
            if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
            {
                meleeProg.TotalCredits = 0;
                meleeProg.WeaponProgress.Clear();
                pendingMeleeProgressSave = true;
            }
            ApplyMeleeBonusStatic(player, 0);

            // Reset Ranged
            if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
            {
                rangedProg.TotalCredits = 0;
                rangedProg.WeaponProgress.Clear();
                pendingRangedProgressSave = true;
            }
            ApplyRangedBonusStatic(player, 0);

            // Reset Walking
            if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
            {
                walkingProg.TotalCredits = 0;
                walkingProg.BlocksInIncrement = 0;
                walkingProg.CurrentIncrementSize = 1000; // Default base
                pendingWalkingProgressSave = true;
            }
            ApplyWalkingBonusStatic(player, 0);

            // Reset Hunger
            if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
            {
                hungerProg.TotalCredits = 0;
                hungerProg.SecondsInIncrement = 0;
                hungerProg.CurrentIncrementSize = 300; // Default base (5 minutes)
                pendingHungerProgressSave = true;
            }
            ApplyHungerBonusStatic(player, 0);

            // Reset Armor
            if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
            {
                armorProg.TotalDurabilityCredits = 0;
                armorProg.TotalWalkSpeedCredits = 0;
                armorProg.ArmorProgress.Clear();
                pendingArmorProgressSave = true;
            }
            ApplyArmorBonusesStatic(player, 0, 0);

            // Reset Clothier
            if (ClothierProgress.TryGetValue(playerUid, out var clothierProg))
            {
                clothierProg.SewingKitUnlocked = false;
                clothierProg.UniqueClothesWorn.Clear();
                pendingClothierProgressSave = true;
            }
            ApplyClothierBonusStatic(player, clothierProg ?? new ClothierProgressData());

            // Reset Mender
            if (MenderProgress.TryGetValue(playerUid, out var menderProg))
            {
                menderProg.TotalCredits = 0;
                menderProg.RepairsInIncrement = 0;
                menderProg.CurrentIncrementSize = 5; // Default base
                pendingMenderProgressSave = true;
            }
            ApplyMenderBonusStatic(player, 0);

            // Reset Pilferer
            if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
            {
                pilfererProg.TotalCredits = 0;
                pilfererProg.PointsInIncrement = 0;
                pilfererProg.CurrentIncrementSize = 10; // Default base
                pendingPilfererProgressSave = true;
            }
            ApplyPilfererBonusStatic(player, 0);

            // Reset Resourceful
            if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
            {
                resourcefulProg.TotalCredits = 0;
                resourcefulProg.AnimalsInIncrement = 0;
                resourcefulProg.CurrentIncrementSize = 10; // Default base
                pendingResourcefulProgressSave = true;
            }
            ApplyResourcefulBonusStatic(player, 0);

            // Reset Forager
            if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
            {
                foragerProg.TotalCredits = 0;
                foragerProg.CropsInIncrement = 0;
                foragerProg.CurrentIncrementSize = 10; // Default base
                pendingForagerProgressSave = true;
            }
            ApplyForagerBonusStatic(player, 0);

            // Reset Furtive
            if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
            {
                furtiveProg.TotalCredits = 0;
                furtiveProg.BlocksInIncrement = 0;
                furtiveProg.CurrentIncrementSize = 100; // Default base
                pendingFurtiveProgressSave = true;
            }
            ApplyFurtiveBonusStatic(player, 0);

            // Reset Precise
            if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
            {
                preciseProg.TotalCredits = 0;
                preciseProg.WeaponProgress.Clear();
                pendingPreciseProgressSave = true;
            }
            ApplyPreciseBonusStatic(player, 0);

            // Reset Technical
            if (TechnicalProgress.TryGetValue(playerUid, out var technicalProg))
            {
                technicalProg.TranslocatorsRepaired = 0;
                technicalProg.IsUnlocked = false;
                pendingTechnicalProgressSave = true;
            }
            ApplyTechnicalBonusStatic(player, false);

            // Reset Hardy Health
            if (HardyHealthProgress.TryGetValue(playerUid, out var hardyHealthProg))
            {
                hardyHealthProg.IsUnlocked = false;
                pendingHardyHealthProgressSave = true;
            }
            ApplyHardyHealthBonusStatic(player, false);

            // Reset Bowyer
            if (BowyerProgress.TryGetValue(playerUid, out var bowyerProg))
            {
                bowyerProg.IsUnlocked = false;
                bowyerProg.TotalBowDamage = 0;
                pendingBowyerProgressSave = true;
            }
            ApplyBowyerBonusStatic(player, false);

            // Reset Improviser
            if (ImproviserProgress.TryGetValue(playerUid, out var improviserProg))
            {
                improviserProg.IsUnlocked = false;
                improviserProg.TotalRockDamage = 0;
                pendingImproviserProgressSave = true;
            }
            ApplyImproviserBonusStatic(player, false);

            // Reset Tinkerer
            if (TinkererProgress.TryGetValue(playerUid, out var tinkererProg))
            {
                tinkererProg.IsUnlocked = false;
                pendingTinkererProgressSave = true;
            }
            ApplyTinkererBonusStatic(player, false);

            // Reset Merciless
            if (MercilessProgress.TryGetValue(playerUid, out var mercilessProg))
            {
                mercilessProg.IsUnlocked = false;
                pendingMercilessProgressSave = true;
            }
            ApplyMercilessBonusStatic(player, false);

            // Reset Claustrophobic Removal
            if (ClaustrophobicRemovalProgress.TryGetValue(playerUid, out var claustrophobicProg))
            {
                claustrophobicProg.IsRemoved = false;
                pendingClaustrophobicRemovalProgressSave = true;
            }
            ApplyClaustrophobicRemovalStatic(player, false);

            return TextCommandResult.Success("All trait progression has been reset to 0.");
        }

        /// <summary>
        /// Handler for /trait maxall command.
        /// Sets all trait progression to maximum for testing purposes.
        /// </summary>
        private TextCommandResult OnTraitMaxAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;

            // Max Mining
            int maxMiningCredits = GetMaxMiningCredits(player.Entity);
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            miningProg.TotalCredits = maxMiningCredits;
            miningProg.PickaxeProgress.Clear();
            pendingMiningProgressSave = true;
            ApplyMiningBonus(player, CalculateMiningBonusPercent(maxMiningCredits));

            // Max Melee
            int maxMeleeCredits = GetMaxMeleeCredits(player.Entity);
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            meleeProg.TotalCredits = maxMeleeCredits;
            meleeProg.WeaponProgress.Clear();
            pendingMeleeProgressSave = true;
            ApplyMeleeBonusStatic(player, CalculateMeleeBonusPercent(maxMeleeCredits));

            // Max Ranged
            int maxRangedCredits = GetMaxRangedCredits(player.Entity);
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            rangedProg.TotalCredits = maxRangedCredits;
            rangedProg.WeaponProgress.Clear();
            pendingRangedProgressSave = true;
            ApplyRangedBonusStatic(player, maxRangedCredits);

            // Max Walking
            int maxWalkingCredits = MaxWalkingSpeedPercent;
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData());
            walkingProg.TotalCredits = maxWalkingCredits;
            walkingProg.BlocksInIncrement = 0;
            walkingProg.CurrentIncrementSize = BaseBlocksWalkedPerIncrement;
            pendingWalkingProgressSave = true;
            ApplyWalkingBonusStatic(player, maxWalkingCredits);

            // Max Hunger
            int maxHungerCredits = CalculateMaxHungerCredits(player.Entity);
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData());
            hungerProg.TotalCredits = maxHungerCredits;
            hungerProg.SecondsInIncrement = 0;
            hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
            pendingHungerProgressSave = true;
            ApplyHungerBonusStatic(player, CalculateHungerBonusPercent(maxHungerCredits, player.Entity));

            // Max Armor
            int maxArmorDurabilityCredits = MaxArmorDurabilityPercent;
            int maxArmorWalkSpeedCredits = MaxArmorWalkSpeedPercent;
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            armorProg.TotalDurabilityCredits = maxArmorDurabilityCredits;
            armorProg.TotalWalkSpeedCredits = maxArmorWalkSpeedCredits;
            armorProg.ArmorProgress.Clear();
            pendingArmorProgressSave = true;
            ApplyArmorBonusesStatic(player, maxArmorDurabilityCredits, maxArmorWalkSpeedCredits);

            // Max Clothier (unlock sewing kit)
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            clothierProg.SewingKitUnlocked = true;
            pendingClothierProgressSave = true;
            ApplyClothierBonusStatic(player, clothierProg);

            // Max Mender
            int maxMenderCredits = MaxMenderPercent;
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());
            menderProg.TotalCredits = maxMenderCredits;
            menderProg.RepairsInIncrement = 0;
            menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
            pendingMenderProgressSave = true;
            ApplyMenderBonusStatic(player, maxMenderCredits);

            // Max Pilferer
            int maxPilfererCredits = GetMaxPilfererCredits(player.Entity);
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());
            pilfererProg.TotalCredits = maxPilfererCredits;
            pilfererProg.PointsInIncrement = 0;
            pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
            pendingPilfererProgressSave = true;
            ApplyPilfererBonusStatic(player, maxPilfererCredits);

            // Max Resourceful
            int maxResourcefulCredits = GetMaxResourcefulCredits(player.Entity);
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());
            resourcefulProg.TotalCredits = maxResourcefulCredits;
            resourcefulProg.AnimalsInIncrement = 0;
            resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
            pendingResourcefulProgressSave = true;
            ApplyResourcefulBonusStatic(player, maxResourcefulCredits);

            // Max Forager
            int maxForagerCredits = GetMaxForagerCredits(player.Entity);
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());
            foragerProg.TotalCredits = maxForagerCredits;
            foragerProg.CropsInIncrement = 0;
            foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
            pendingForagerProgressSave = true;
            ApplyForagerBonusStatic(player, maxForagerCredits);

            // Max Furtive
            int maxFurtiveCredits = MaxFurtivePercent;
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            furtiveProg.TotalCredits = maxFurtiveCredits;
            furtiveProg.BlocksInIncrement = 0;
            furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
            pendingFurtiveProgressSave = true;
            ApplyFurtiveBonusStatic(player, maxFurtiveCredits);

            // Max Precise
            int maxPreciseCredits = MaxPrecisePercent;
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            preciseProg.TotalCredits = maxPreciseCredits;
            preciseProg.WeaponProgress.Clear();
            pendingPreciseProgressSave = true;
            ApplyPreciseBonusStatic(player, maxPreciseCredits);

            // Unlock Technical
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            technicalProg.TranslocatorsRepaired = TechnicalRequiredTranslocatorRepairs;
            technicalProg.IsUnlocked = true;
            pendingTechnicalProgressSave = true;
            ApplyTechnicalBonusStatic(player, true);

            // Unlock Hardy Health
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            hardyHealthProg.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, true);

            // Unlock Bowyer
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            bowyerProg.IsUnlocked = true;
            bowyerProg.TotalBowDamage = BowyerBowDamageThreshold;
            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, true);

            // Unlock Improviser
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            improviserProg.IsUnlocked = true;
            improviserProg.TotalRockDamage = ImproviserRockDamageThreshold;
            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, true);

            // Unlock Tinkerer
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            tinkererProg.IsUnlocked = true;
            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, true);

            // Unlock Merciless
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            mercilessProg.IsUnlocked = true;
            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, true);

            // Remove Claustrophobic (if applicable)
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            claustrophobicProg.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, true);

            return TextCommandResult.Success("All trait progression has been set to maximum for testing.");
        }

        /// <summary>
        /// Handler for /trait testsuite command.
        /// Runs automated tests for trait calculations.
        /// </summary>
        private TextCommandResult OnTraitTestSuiteCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string category = (string)args[0];
            string result = TraitTestSuite.RunTests(category, player);

            return TextCommandResult.Success(result);
        }

        /// <summary>
        /// Handler for /trait resetconfig command.
        /// Resets all trait configuration values (base, increment, max) to their defaults.
        /// </summary>
        private TextCommandResult OnTraitResetConfigCommand(TextCommandCallingArgs args)
        {
            // Mining defaults
            BaseBlocksPerIncrement = 100;
            IncrementStep = 100;
            MaxMiningSpeedPercent = 50;
            OreMultiplier = 5;

            // Melee defaults
            BaseDamagePerIncrement = 100;
            MeleeIncrementStep = 100;
            MaxMeleeDamagePercent = 50;

            // Ranged defaults
            BaseRangedDamagePerIncrement = 100;
            RangedIncrementStep = 100;
            MaxRangedDamagePercent = 50;
            MaxRangedAccuracyPercent = 50;
            MaxRangedDistancePercent = 50;

            // Walking defaults
            BaseBlocksWalkedPerIncrement = 1000;
            WalkingIncrementStep = 1000;
            MaxWalkingSpeedPercent = 15;

            // Hunger defaults
            BaseSecondsPerIncrement = 300;
            HungerIncrementStep = 60;
            MaxHungerReductionPercent = 25;

            // Armor defaults
            BaseSecondsInArmorPerIncrement = 2880;
            ArmorTimeIncrementStep = 2880;
            BaseDamageBlockedPerIncrement = 100;
            ArmorDamageIncrementStep = 100;
            BaseRepairsPerIncrement = 1;
            ArmorRepairIncrementStep = 1;
            MaxArmorDurabilityPercent = 50;
            MaxArmorWalkSpeedPercent = 50;

            // Clothier defaults
            ClothierRequiredUniqueClothes = 20;

            // Mender defaults
            BaseMenderRepairsPerIncrement = 5;
            MenderIncrementStep = 1;
            MaxMenderPercent = 20;

            // Pilferer defaults
            BasePilfererPointsPerIncrement = 10;
            PilfererIncrementStep = 10;
            MaxPilfererPercent = 20;

            // Resourceful defaults
            BaseResourcefulAnimalsPerIncrement = 10;
            ResourcefulIncrementStep = 10;
            MaxResourcefulLootPercent = 20;
            MaxResourcefulSpeedPercent = 25;

            // Forager defaults
            BaseForagerCropsPerIncrement = 10;
            ForagerIncrementStep = 10;
            MaxForagerLootPercent = 20;
            MaxForagerWildCropPercent = 20;

            // Furtive defaults
            BaseFurtiveSneakBlocksPerIncrement = 100;
            FurtiveIncrementStep = 100;
            MaxFurtivePercent = 35;

            // Precise defaults
            BasePreciseDamagePerIncrement = 100;
            PreciseIncrementStep = 100;
            MaxPrecisePercent = 30;

            // Technical defaults
            TechnicalRequiredTranslocatorRepairs = 5;

            // Hardy Health defaults
            HardyHealthMiningThreshold = 110;
            HardyHealthArmorDurabilityThreshold = 10;
            HardyHealthBonus = 5;

            // Save config
            pendingConfigSave = true;

            return TextCommandResult.Success("All trait configuration values have been reset to defaults.");
        }

        // =========================================================================
        // COMBAT OVERHAUL COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait coproficiency command.
        /// Shows all Combat Overhaul proficiency progression.
        /// </summary>
        private TextCommandResult OnTraitCOProficiencyCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            if (!COEnableCompat)
            {
                return TextCommandResult.Error("Combat Overhaul compatibility is disabled in config.");
            }

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            sb.AppendLine("=== Combat Overhaul Proficiency Progression ===");

            if (!COProgress.TryGetValue(playerUid, out var playerProgress))
            {
                sb.AppendLine("No proficiency progress recorded yet. Deal damage with CO weapons to earn credits.");
                return TextCommandResult.Success(sb.ToString());
            }

            // Show Steady Aim
            float steadyAimBonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
            int steadyAimMaxCredits = GetCOProficiencyMaxCredits(CO_STEADY_AIM);
            sb.AppendLine($"Steady Aim: {playerProgress.SteadyAimCredits}/{steadyAimMaxCredits} credits (+{steadyAimBonus:F2})");

            // Show each proficiency
            sb.AppendLine("\n--- Ranged Proficiencies ---");
            ShowCOProficiencyStats(sb, playerProgress, CO_BOWS_PROFICIENCY, "Bows");
            ShowCOProficiencyStats(sb, playerProgress, CO_CROSSBOWS_PROFICIENCY, "Crossbows");
            ShowCOProficiencyStats(sb, playerProgress, CO_FIREARMS_PROFICIENCY, "Firearms");
            ShowCOProficiencyStats(sb, playerProgress, CO_SLINGS_PROFICIENCY, "Slings");

            sb.AppendLine("\n--- One-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, CO_ONE_HANDED_SWORDS_PROFICIENCY, "One-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, CO_MACES_PROFICIENCY, "Maces");
            ShowCOProficiencyStats(sb, playerProgress, CO_CLUBS_PROFICIENCY, "Clubs");
            ShowCOProficiencyStats(sb, playerProgress, CO_AXES_PROFICIENCY, "Axes");

            sb.AppendLine("\n--- Two-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, CO_TWO_HANDED_SWORDS_PROFICIENCY, "Two-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, CO_HALBERDS_PROFICIENCY, "Halberds");
            ShowCOProficiencyStats(sb, playerProgress, CO_QUARTERSTAFF_PROFICIENCY, "Quarterstaff");

            sb.AppendLine("\n--- Polearms ---");
            ShowCOProficiencyStats(sb, playerProgress, CO_SPEARS_PROFICIENCY, "Spears");
            ShowCOProficiencyStats(sb, playerProgress, CO_JAVELINS_PROFICIENCY, "Javelins");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Helper to show CO proficiency stats in the command output.
        /// </summary>
        private static void ShowCOProficiencyStats(StringBuilder sb, COPlayerProgressData playerProgress, string proficiencyStat, string displayName)
        {
            if (playerProgress.Proficiencies.TryGetValue(proficiencyStat, out var prof))
            {
                float bonus = CalculateCOProficiencyBonus(prof.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                int maxCredits = GetCOProficiencyMaxCredits(proficiencyStat);
                sb.AppendLine($"  {displayName}: {prof.TotalCredits}/{maxCredits} credits (+{bonus:F2})");

                // Show per-weapon progress if any
                if (prof.WeaponProgress.Count > 0)
                {
                    foreach (var weapon in prof.WeaponProgress.Take(3)) // Show top 3 weapons
                    {
                        string shortCode = weapon.Key.Contains(":") ? weapon.Key.Substring(weapon.Key.IndexOf(':') + 1) : weapon.Key;
                        sb.AppendLine($"    {shortCode}: {weapon.Value.DamageInIncrement:F0}/{weapon.Value.CurrentIncrementSize} toward next");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  {displayName}: 0 credits (+0.00)");
            }
        }

        /// <summary>
        /// Handler for /trait colevel command.
        /// Sets Combat Overhaul proficiency credits directly.
        /// Usage: /trait colevel <proficiency> <credits>
        /// Proficiency names: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, axes, quarterstaff, steadyaim
        /// </summary>
        private TextCommandResult OnTraitCOLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string proficiencyArg = (string)args[0];
            int credits = (int)args[1];

            // Map short names to full proficiency stat names
            string proficiencyStat = proficiencyArg.ToLowerInvariant() switch
            {
                "bows" or "bow" => CO_BOWS_PROFICIENCY,
                "crossbows" or "crossbow" or "xbow" => CO_CROSSBOWS_PROFICIENCY,
                "firearms" or "firearm" or "guns" or "gun" => CO_FIREARMS_PROFICIENCY,
                "slings" or "sling" => CO_SLINGS_PROFICIENCY,
                "1hswords" or "1hsword" or "1h" or "onehanded" => CO_ONE_HANDED_SWORDS_PROFICIENCY,
                "2hswords" or "2hsword" or "2h" or "twohanded" => CO_TWO_HANDED_SWORDS_PROFICIENCY,
                "spears" or "spear" => CO_SPEARS_PROFICIENCY,
                "javelins" or "javelin" or "jav" => CO_JAVELINS_PROFICIENCY,
                "maces" or "mace" => CO_MACES_PROFICIENCY,
                "clubs" or "club" => CO_CLUBS_PROFICIENCY,
                "halberds" or "halberd" => CO_HALBERDS_PROFICIENCY,
                "axes" or "axe" => CO_AXES_PROFICIENCY,
                "quarterstaff" or "staff" or "staves" => CO_QUARTERSTAFF_PROFICIENCY,
                "steadyaim" or "steady" or "aim" => CO_STEADY_AIM,
                _ => null
            };

            if (proficiencyStat == null)
            {
                return TextCommandResult.Error($"Unknown proficiency '{proficiencyArg}'. Valid options: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, axes, quarterstaff, steadyaim");
            }

            // Get max credits for this proficiency
            int maxCredits = GetCOProficiencyMaxCredits(proficiencyStat);
            if (credits < 0 || credits > maxCredits)
            {
                return TextCommandResult.Error($"Credits must be between 0 and {maxCredits} for {GetCOProficiencyDisplayName(proficiencyStat)}.");
            }

            string playerUid = player.PlayerUID;
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            if (proficiencyStat == CO_STEADY_AIM)
            {
                playerProgress.SteadyAimCredits = credits;
                ApplyCOSteadyAimBonus(player, credits);
            }
            else
            {
                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);
                profProgress.TotalCredits = credits;
                // Reset weapon progress since we're setting directly
                profProgress.WeaponProgress.Clear();
                ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
            }

            pendingCOProgressSave = true;

            float bonus = CalculateCOProficiencyBonus(credits, GetCOProficiencyMax(proficiencyStat));
            return TextCommandResult.Success($"Set {GetCOProficiencyDisplayName(proficiencyStat)} to {credits} credits (+{bonus:F2}).");
        }

        /// <summary>
        /// Handler for /trait coreset command.
        /// Resets all Combat Overhaul progression to 0.
        /// </summary>
        private TextCommandResult OnTraitCOResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string playerUid = player.PlayerUID;

            // Reset all CO progress
            if (COProgress.TryRemove(playerUid, out _))
            {
                pendingCOProgressSave = true;
            }

            // Clear all CO stats
            foreach (var proficiencyStat in AllCOProficiencies)
            {
                string statCode = CO_STAT_PREFIX + proficiencyStat;
                player.Entity.Stats.Remove(proficiencyStat, statCode);
            }
            player.Entity.Stats.Remove(CO_STEADY_AIM, CO_STAT_PREFIX + CO_STEADY_AIM);

            return TextCommandResult.Success("All Combat Overhaul proficiency progression has been reset to 0.");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist clothier progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No clothier progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid clothier progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded clothier progress for {ClothierProgress.Count} players");
            }
            catch (Exception ex)
            {
                ClothierProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load clothier progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist mender progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No mender progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid mender progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded mender progress for {MenderProgress.Count} players");
            }
            catch (Exception ex)
            {
                MenderProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load mender progress: {ex.Message}");
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
                            writer.Write((byte)2);    // Version 2 - removed chest positions

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);
                                var progress = playerKvp.Value;
                                writer.Write(progress.TotalCredits);
                                writer.Write(progress.PointsInIncrement);
                                writer.Write(progress.CurrentIncrementSize);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(PILFERER_PROGRESS_SAVE_KEY, data);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist pilferer progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No pilferer progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid pilferer progress magic bytes");
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
                            // Version 1 had chest positions - skip them if loading old data
                            if (version == 1)
                            {
                                int chestCount = reader.ReadInt32();
                                for (int j = 0; j < chestCount; j++)
                                {
                                    reader.ReadString(); // Skip old chest position data
                                }
                            }
                            PilfererProgress[playerUid] = progress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded pilferer progress for {PilfererProgress.Count} players");
            }
            catch (Exception ex)
            {
                PilfererProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load pilferer progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist resourceful progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No resourceful progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid resourceful progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded resourceful progress for {ResourcefulProgress.Count} players");
            }
            catch (Exception ex)
            {
                ResourcefulProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load resourceful progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist forager progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No forager progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid forager progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded forager progress for {ForagerProgress.Count} players");
            }
            catch (Exception ex)
            {
                ForagerProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load forager progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist furtive progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No furtive progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid furtive progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded furtive progress for {FurtiveProgress.Count} players");
            }
            catch (Exception ex)
            {
                FurtiveProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load furtive progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist precise progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No precise progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid precise progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded precise progress for {PreciseProgress.Count} players");
            }
            catch (Exception ex)
            {
                PreciseProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load precise progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist technical progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No technical progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid technical progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded technical progress for {TechnicalProgress.Count} players");
            }
            catch (Exception ex)
            {
                TechnicalProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load technical progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist hardy health progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No hardy health progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid hardy health progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded hardy health progress for {HardyHealthProgress.Count} players");
            }
            catch (Exception ex)
            {
                HardyHealthProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load hardy health progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist bowyer progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No bowyer progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid bowyer progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded bowyer progress for {BowyerProgress.Count} players");
            }
            catch (Exception ex)
            {
                BowyerProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load bowyer progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist improviser progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No improviser progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid improviser progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded improviser progress for {ImproviserProgress.Count} players");
            }
            catch (Exception ex)
            {
                ImproviserProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load improviser progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist tinkerer progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No tinkerer progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid tinkerer progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded tinkerer progress for {TinkererProgress.Count} players");
            }
            catch (Exception ex)
            {
                TinkererProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load tinkerer progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist merciless progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No merciless progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid merciless progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded merciless progress for {MercilessProgress.Count} players");
            }
            catch (Exception ex)
            {
                MercilessProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load merciless progress: {ex.Message}");
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
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist claustrophobic removal progress: {ex.Message}");
                }
            }
        }

        // =========================================================================
        // COMBAT OVERHAUL PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist Combat Overhaul proficiency progress to world save data.
        /// </summary>
        public static void PersistCOProgress()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (COProgress.IsEmpty)
                {
                    ServerApi.WorldManager.SaveGame.StoreData(CO_PROGRESS_SAVE_KEY, null);
                    return;
                }

                try
                {
                    var snapshot = COProgress.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Magic bytes: "COB" (Combat Overhaul Bonus)
                            writer.Write((byte)0x43); // 'C'
                            writer.Write((byte)0x4F); // 'O'
                            writer.Write((byte)0x42); // 'B'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key); // Player UID
                                var playerProgress = playerKvp.Value;

                                // Write Steady Aim credits
                                writer.Write(playerProgress.SteadyAimCredits);

                                // Write proficiency count and each proficiency
                                writer.Write(playerProgress.Proficiencies.Count);
                                foreach (var profKvp in playerProgress.Proficiencies)
                                {
                                    writer.Write(profKvp.Key); // Proficiency stat name
                                    var profProgress = profKvp.Value;
                                    writer.Write(profProgress.TotalCredits);

                                    // Write weapon progress
                                    writer.Write(profProgress.WeaponProgress.Count);
                                    foreach (var weaponKvp in profProgress.WeaponProgress)
                                    {
                                        writer.Write(weaponKvp.Key); // Weapon code
                                        writer.Write(weaponKvp.Value.DamageInIncrement);
                                        writer.Write(weaponKvp.Value.CurrentIncrementSize);
                                    }
                                }
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(CO_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted CO progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist CO progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load Combat Overhaul proficiency progress from world save data.
        /// </summary>
        private void LoadCOProgress()
        {
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CO_PROGRESS_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No CO progress data found");
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

                        if (magic1 != 0x43 || magic2 != 0x4F || magic3 != 0x42)
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid CO progress magic bytes");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
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

                            COProgress[playerUid] = playerProgress;
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded CO progress for {COProgress.Count} players");
            }
            catch (Exception ex)
            {
                COProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load CO progress: {ex.Message}");
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No claustrophobic removal progress data found");
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
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid claustrophobic removal progress magic bytes");
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

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded claustrophobic removal progress for {ClaustrophobicRemovalProgress.Count} players");
            }
            catch (Exception ex)
            {
                ClaustrophobicRemovalProgress.Clear();
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load claustrophobic removal progress: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Client-side mod system that displays mining progression in the character traits dialog.
    /// Uses Harmony to patch the CharacterSystem's trait display method.
    /// </summary>
    public class SeraphLevelingClientSystem : ModSystem
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
            harmony = new Harmony("seraphleveling");
            try
            {
                ApplyPatches(api);
                api.Logger.Notification("[SeraphLeveling] Client-side mod loaded, Harmony patches applied");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply Harmony patches: {ex.Message}");
                api.Logger.Error($"[SeraphLeveling] Stack trace: {ex.StackTrace}");
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
                api.Logger.Warning("[SeraphLeveling] Could not find CharacterSystem type");
                return;
            }

            // Find the getClassTraitText method
            var targetMethod = AccessTools.Method(characterSystemType, "getClassTraitText");
            if (targetMethod == null)
            {
                api.Logger.Warning("[SeraphLeveling] Could not find getClassTraitText method");

                // List available methods for debugging
                var methods = characterSystemType.GetMethods(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                api.Logger.Debug($"[SeraphLeveling] Available methods in CharacterSystem:");
                foreach (var m in methods)
                {
                    if (m.Name.ToLower().Contains("trait"))
                    {
                        api.Logger.Debug($"  - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
                    }
                }
                return;
            }

            api.Logger.Debug($"[SeraphLeveling] Found method: {targetMethod.Name}, params: {string.Join(", ", targetMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");

            // Get our postfix method
            var postfixMethod = AccessTools.Method(typeof(CharacterSystemPatches), nameof(CharacterSystemPatches.GetClassTraitText_Postfix));

            // Apply the patch
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            api.Logger.Notification("[SeraphLeveling] Successfully patched getClassTraitText");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("seraphleveling");
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

            // Log the raw result string to see exact format (escape special chars for visibility)
            string escapedResult = __result?.Replace("\n", "\\n").Replace("\r", "\\r") ?? "NULL";
            ClientApi.Logger.Debug($"[SeraphLeveling] RAW getClassTraitText result: {escapedResult}");

            // Get mining progression data
            int miningLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, 0);
            int miningBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_BONUS, 0);
            bool hasVanillaHardy = eplr.WatchedAttributes.GetBool("sitHasVanillaHardy", false);

            // Get melee progression data
            int meleeLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, 0);
            int meleeBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_BONUS, 0);
            bool hasVanillaSoldier = eplr.WatchedAttributes.GetBool("sitHasVanillaSoldier", false);

            // Get ranged progression data
            int rangedLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, 0);
            int rangedDamageBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_DAMAGE_BONUS, 0);
            int rangedAccuracyBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_ACCURACY_BONUS, 0);
            int rangedDistanceBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_DISTANCE_BONUS, 0);
            bool hasVanillaFocused = eplr.WatchedAttributes.GetBool("sitHasVanillaFocused", false);

            // Get walking progression data
            int walkingLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, 0);
            int walkingBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, 0);
            bool hasVanillaFleetfooted = eplr.WatchedAttributes.GetBool("sitHasVanillaFleetfooted", false);

            // Get armor progression data
            int armorDurabilityLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, 0);
            int armorDurabilityBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_BONUS, 0);
            int armorWalkSpeedLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, 0);
            int armorWalkSpeedBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_BONUS, 0);
            bool hasVanillaSoldierArmor = eplr.WatchedAttributes.GetBool("sitHasVanillaSoldierArmor", false);

            ClientApi.Logger.Debug($"[SeraphLeveling] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier} | Ranged: Level={rangedLevel}, HasFocused={hasVanillaFocused} | Walking: Level={walkingLevel}, HasFleetfooted={hasVanillaFleetfooted} | Armor: Dur={armorDurabilityLevel}, Walk={armorWalkSpeedLevel}");

            // Get the "no traits" message - vanilla uses this for classes like Commoner
            string noTraitsMsg = Lang.Get("charactersheet-notraits");

            ClientApi.Logger.Debug($"[SeraphLeveling] Original result: '{__result}', noTraitsMsg: '{noTraitsMsg}'");

            // Check if we have NO real traits (only "no traits" message or empty)
            // Use Contains to handle cases where the message might have formatting
            bool hasNoTraits = string.IsNullOrEmpty(__result) ||
                               __result.Trim() == noTraitsMsg.Trim() ||
                               __result == noTraitsMsg ||
                               __result.Contains(noTraitsMsg) ||
                               __result.Contains("No positive or negative traits");

            // Process mining progression (Hardy trait)
            // Only show Hardy when miningBonus > 0 (after negative traits are cancelled)
            if (miningBonus > 0)
            {
                string plainMiningTraitName = Lang.Get("seraphleveling:trait-sitminingmastery");

                if (hasVanillaHardy)
                {
                    // Class already has Hardy (e.g., Blackguard) - update the existing Hardy's mining speed
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_HARDY_MINING_BONUS + miningBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_HARDY_MINING_BONUS}% mining speed",
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
                    // Use mining-only format since we don't have Hardy Health yet
                    __result = Lang.Get("seraphleveling:trait-hardy-mining-only-dynamic", miningBonus);
                    hasNoTraits = false; // We now have traits
                }
                else if (__result.Contains(plainMiningTraitName))
                {
                    // We have our trait but no vanilla Hardy - replace plain name with dynamic version
                    __result = __result.Replace(plainMiningTraitName,
                        Lang.Get("seraphleveling:trait-hardy-mining-only-dynamic", miningBonus));
                }
                else
                {
                    // Has other traits but no Hardy at all - append our dynamic Hardy
                    __result = __result + "\n" + Lang.Get("seraphleveling:trait-hardy-mining-only-dynamic", miningBonus);
                }
            }

            // Process melee progression (Soldier trait)
            // Only show Soldier melee when meleeBonus > 0 (after negative traits are cancelled)
            if (meleeBonus > 0)
            {
                string plainMeleeTraitName = Lang.Get("seraphleveling:trait-sitmeleemastery");

                // Re-check hasNoTraits after mining processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaSoldier)
                {
                    // Class already has Soldier (e.g., Blackguard) - update the existing Soldier's melee damage
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_SOLDIER_MELEE_BONUS + meleeBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_SOLDIER_MELEE_BONUS}% melee damage",
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
                    __result = Lang.Get("seraphleveling:trait-soldier-dynamic", meleeBonus);
                }
                else if (__result.Contains(plainMeleeTraitName))
                {
                    // We have our trait but no vanilla Soldier - replace plain name with dynamic version
                    __result = __result.Replace(plainMeleeTraitName,
                        Lang.Get("seraphleveling:trait-soldier-dynamic", meleeBonus));
                }
                else
                {
                    // Has other traits but no Soldier at all - append our dynamic Soldier
                    __result = __result + "\n" + Lang.Get("seraphleveling:trait-soldier-dynamic", meleeBonus);
                }
            }

            // Process ranged progression (Focused trait)
            // Only show Focused when any bonus > 0 (after negative traits are cancelled for that stat)
            if (rangedDamageBonus > 0 || rangedAccuracyBonus > 0 || rangedDistanceBonus > 0)
            {
                string plainRangedTraitName = Lang.Get("seraphleveling:trait-sitrangedmastery");

                // Re-check hasNoTraits after melee processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFocused)
                {
                    // Class already has Focused (e.g., Hunter) - update the existing Focused's stats
                    // Ranged damage
                    int combinedDamage = SeraphLevelingModSystem.VANILLA_FOCUSED_DAMAGE_BONUS + rangedDamageBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FOCUSED_DAMAGE_BONUS}% ranged damage",
                        $"+{combinedDamage}% ranged damage");

                    // Ranged accuracy
                    int combinedAccuracy = SeraphLevelingModSystem.VANILLA_FOCUSED_ACCURACY_BONUS + rangedAccuracyBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FOCUSED_ACCURACY_BONUS}% ranged accuracy",
                        $"+{combinedAccuracy}% ranged accuracy");

                    // Ranged distance
                    int combinedDistance = SeraphLevelingModSystem.VANILLA_FOCUSED_DISTANCE_BONUS + rangedDistanceBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FOCUSED_DISTANCE_BONUS}% ranged distance",
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
                    __result = Lang.Get("seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
                else if (__result.Contains(plainRangedTraitName))
                {
                    // We have our trait but no vanilla Focused - replace plain name with dynamic version
                    __result = __result.Replace(plainRangedTraitName,
                        Lang.Get("seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus));
                }
                else
                {
                    // Has other traits but no Focused at all - append our dynamic Focused
                    __result = __result + "\n" + Lang.Get("seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
            }

            // Process walking progression (Fleetfooted trait)
            if (walkingLevel > 0)
            {
                string plainWalkingTraitName = Lang.Get("seraphleveling:trait-sitwalkingmastery");

                // Re-check hasNoTraits after ranged processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFleetfooted)
                {
                    // Class already has Fleetfooted (e.g., Hunter, Clockmaker) - update the existing Fleetfooted's walk speed
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS + walkingBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS}% walk speed",
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
                    __result = Lang.Get("seraphleveling:trait-fleetfooted-dynamic", walkingBonus);
                }
                else if (__result.Contains(plainWalkingTraitName))
                {
                    // We have our trait but no vanilla Fleetfooted - replace plain name with dynamic version
                    __result = __result.Replace(plainWalkingTraitName,
                        Lang.Get("seraphleveling:trait-fleetfooted-dynamic", walkingBonus));
                }
                else
                {
                    // Has other traits but no Fleetfooted at all - append our dynamic Fleetfooted
                    __result = __result + "\n" + Lang.Get("seraphleveling:trait-fleetfooted-dynamic", walkingBonus);
                }
            }

            // Process armor progression (Soldier trait - armor durability and speed penalty)
            if (armorDurabilityLevel > 0 || armorWalkSpeedLevel > 0)
            {
                // Re-check hasNoTraits after walking processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
                        int combinedDurability = SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS + armorDurabilityBonus;
                        __result = __result.Replace(
                            $"+{SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS}% armor durability",
                            $"+{combinedDurability}% armor durability");
                    }

                    // Update armor speed penalty if we have bonus
                    if (armorWalkSpeedBonus > 0)
                    {
                        int combinedSpeedPenalty = SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS + armorWalkSpeedBonus;
                        __result = __result.Replace(
                            $"-{SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS}% armor speed penalty",
                            $"-{combinedSpeedPenalty}% armor speed penalty");
                    }
                }
                else if (hasNoTraits)
                {
                    // No traits at all - show our armor progression as a Soldier-like trait
                    __result = Lang.Get("seraphleveling:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                }
                else
                {
                    // Has other traits but no vanilla Soldier - check if we already added melee Soldier
                    // Only add if we have actual bonuses to show
                    if (totalDurabilityBonus > 0 || totalWalkSpeedBonus > 0)
                    {
                        // Check if melee progression already added a dynamic Soldier entry
                        string meleeSoldierPattern = Lang.Get("seraphleveling:trait-soldier-dynamic", meleeBonus);
                        if (meleeLevel > 0 && __result.Contains(meleeSoldierPattern))
                        {
                            // Replace the melee-only Soldier with a combined entry
                            __result = __result.Replace(meleeSoldierPattern,
                                Lang.Get("seraphleveling:trait-soldier-combined-dynamic", meleeBonus, totalDurabilityBonus, totalWalkSpeedBonus));
                        }
                        else
                        {
                            // No melee Soldier was added, add armor-only entry
                            __result = __result + "\n" + Lang.Get("seraphleveling:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                        }
                    }
                }
            }

            // Process Clothier trait (unlocked by wearing 20 unique clothes)
            bool clothierUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_CLOTHIER_UNLOCKED, false);
            if (clothierUnlocked)
            {
                string plainClothierTraitName = Lang.Get("seraphleveling:trait-sitclothiermastery");
                string dynamicClothierTrait = Lang.Get("seraphleveling:trait-clothier-dynamic");

                // Re-check hasNoTraits after armor processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            int menderLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MENDER_LEVEL, 0);
            int menderBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MENDER_BONUS, 0);
            bool hasVanillaMender = eplr.WatchedAttributes.GetBool("sitHasVanillaMender", false);
            if (menderLevel > 0)
            {
                string plainMenderTraitName = Lang.Get("seraphleveling:trait-sitmendermastery");
                string dynamicMenderTrait = Lang.Get("seraphleveling:trait-mender-dynamic", menderBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaMender)
                {
                    // Class already has Mender trait - update the existing durability value
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_MENDER_ARMOR_DURABILITY_BONUS + menderBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_MENDER_ARMOR_DURABILITY_BONUS}% armor durability",
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
            int pilfererLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_LEVEL, 0);
            int pilfererBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_BONUS, 0);
            bool hasVanillaPilferer = eplr.WatchedAttributes.GetBool("sitHasVanillaPilferer", false);
            // Only show Pilferer when bonus > 0 (after Heavyhanded vessel penalty is cancelled)
            if (pilfererBonus > 0)
            {
                string plainPilfererTraitName = Lang.Get("seraphleveling:trait-sitpilferermastery");
                // Pilferer uses the same bonus for all 3 stats (vessel drops, rusty gear, vessel collection)
                string dynamicPilfererTrait = Lang.Get("seraphleveling:trait-pilferer-dynamic", pilfererBonus, pilfererBonus, pilfererBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaPilferer)
                {
                    // Class already has Pilferer trait - update the existing values
                    int combinedRusty = SeraphLevelingModSystem.VANILLA_PILFERER_RUSTY_GEAR_BONUS + pilfererBonus;
                    int combinedVessel = SeraphLevelingModSystem.VANILLA_PILFERER_VESSEL_CONTENTS_BONUS + pilfererBonus;
                    int combinedCollection = SeraphLevelingModSystem.VANILLA_PILFERER_WHOLE_VESSEL_BONUS + pilfererBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_PILFERER_RUSTY_GEAR_BONUS}% rusty gear",
                        $"+{combinedRusty}% rusty gear");
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_PILFERER_VESSEL_CONTENTS_BONUS}% cracked vessel drops",
                        $"+{combinedVessel}% cracked vessel drops");
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_PILFERER_WHOLE_VESSEL_BONUS}% vessel collection",
                        $"+{combinedCollection}% vessel collection");
                }
                else if (hasNoTraits)
                {
                    __result = dynamicPilfererTrait;
                    hasNoTraits = false;
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
            int resourcefulLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_LEVEL, 0);
            int resourcefulLootBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_LOOT_BONUS, 0);
            int resourcefulSpeedBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_SPEED_BONUS, 0);
            bool hasVanillaResourceful = eplr.WatchedAttributes.GetBool("sitHasVanillaResourceful", false);
            // Only show Resourceful when any bonus > 0 (after Kind penalty is cancelled)
            if (resourcefulLootBonus > 0 || resourcefulSpeedBonus > 0)
            {
                string plainResourcefulTraitName = Lang.Get("seraphleveling:trait-sitresourcefulmastery");
                string dynamicResourcefulTrait = Lang.Get("seraphleveling:trait-resourceful-dynamic", resourcefulLootBonus, resourcefulSpeedBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaResourceful)
                {
                    // Class already has Resourceful trait - update the existing values
                    int combinedLoot = SeraphLevelingModSystem.VANILLA_RESOURCEFUL_LOOT_BONUS + resourcefulLootBonus;
                    int combinedSpeed = SeraphLevelingModSystem.VANILLA_RESOURCEFUL_SPEED_BONUS + resourcefulSpeedBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_RESOURCEFUL_LOOT_BONUS}% animal loot",
                        $"+{combinedLoot}% animal loot");
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_RESOURCEFUL_SPEED_BONUS}% harvesting speed",
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
            int foragerLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_LEVEL, 0);
            int foragerLootBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_LOOT_BONUS, 0);
            int foragerWildCropBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_WILD_CROP_BONUS, 0);
            bool hasVanillaForager = eplr.WatchedAttributes.GetBool("sitHasVanillaForager", false);
            // Only show Forager when any bonus > 0 (after Civil/Heavyhanded penalties are cancelled)
            if (foragerLootBonus > 0 || foragerWildCropBonus > 0)
            {
                string plainForagerTraitName = Lang.Get("seraphleveling:trait-sitforagermastery");
                string dynamicForagerTrait = Lang.Get("seraphleveling:trait-forager-dynamic", foragerLootBonus, foragerWildCropBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaForager)
                {
                    // Class already has Forager trait - update the existing values
                    int combinedLoot = SeraphLevelingModSystem.VANILLA_FORAGER_LOOT_BONUS + foragerLootBonus;
                    int combinedWildCrop = SeraphLevelingModSystem.VANILLA_FORAGER_WILD_CROP_BONUS + foragerWildCropBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FORAGER_LOOT_BONUS}% foraging loot",
                        $"+{combinedLoot}% foraging loot");
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_FORAGER_WILD_CROP_BONUS}% wild crop drops",
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
            int furtiveLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FURTIVE_LEVEL, 0);
            int furtiveBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FURTIVE_BONUS, 0);
            bool hasVanillaFurtive = eplr.WatchedAttributes.GetBool("sitHasVanillaFurtive", false);
            if (furtiveLevel > 0)
            {
                string plainFurtiveTraitName = Lang.Get("seraphleveling:trait-sitfurtivemastery");
                string dynamicFurtiveTrait = Lang.Get("seraphleveling:trait-furtive-dynamic", furtiveBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFurtive)
                {
                    // Class already has Furtive trait - update the existing values
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_FURTIVE_DETECTION_REDUCTION + furtiveBonus;
                    __result = __result.Replace(
                        $"-{SeraphLevelingModSystem.VANILLA_FURTIVE_DETECTION_REDUCTION}% animal seeking range",
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
            int preciseLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PRECISE_LEVEL, 0);
            int preciseBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PRECISE_BONUS, 0);
            bool hasVanillaPrecise = eplr.WatchedAttributes.GetBool("sitHasVanillaPrecise", false);
            if (preciseLevel > 0)
            {
                string plainPreciseTraitName = Lang.Get("seraphleveling:trait-sitprecisemastery");
                string dynamicPreciseTrait = Lang.Get("seraphleveling:trait-precise-dynamic", preciseBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaPrecise)
                {
                    // Class already has Precise trait - update the existing values
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + preciseBonus;
                    __result = __result.Replace(
                        $"+{SeraphLevelingModSystem.VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS}% damage vs mechanicals",
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
            int hungerLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, 0);
            int hungerBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_BONUS, 0);
            if (hungerLevel > 0)
            {
                string plainHungerTraitName = Lang.Get("seraphleveling:trait-sithungermastery");
                string dynamicHungerTrait = Lang.Get("seraphleveling:trait-hunger-dynamic", hungerBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool technicalUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_TECHNICAL_UNLOCKED, false);
            if (technicalUnlocked)
            {
                string plainTechnicalTraitName = Lang.Get("seraphleveling:trait-sittechnicalmastery");
                string dynamicTechnicalTrait = Lang.Get("seraphleveling:trait-technical-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool hardyHealthUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_HARDY_HEALTH_UNLOCKED, false);
            if (hardyHealthUnlocked)
            {
                string plainHardyHealthTraitName = Lang.Get("seraphleveling:trait-sithardyhealthmastery");
                string dynamicHardyHealthTrait = Lang.Get("seraphleveling:trait-hardyhealth-dynamic", SeraphLevelingModSystem.HardyHealthBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool bowyerUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_BOWYER_UNLOCKED, false);
            if (bowyerUnlocked)
            {
                string plainBowyerTraitName = Lang.Get("seraphleveling:trait-sitbowyermastery");
                string dynamicBowyerTrait = Lang.Get("seraphleveling:trait-bowyer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool improviserUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_IMPROVISER_UNLOCKED, false);
            if (improviserUnlocked)
            {
                string plainImproviserTraitName = Lang.Get("seraphleveling:trait-sitimprovisermastery");
                string dynamicImproviserTrait = Lang.Get("seraphleveling:trait-improviser-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool tinkererUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_TINKERER_UNLOCKED, false);
            if (tinkererUnlocked)
            {
                string plainTinkererTraitName = Lang.Get("seraphleveling:trait-sittinkerermastery");
                string dynamicTinkererTrait = Lang.Get("seraphleveling:trait-tinkerer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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
            bool mercilessUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_MERCILESS_UNLOCKED, false);
            if (mercilessUnlocked)
            {
                string plainMercilessTraitName = Lang.Get("seraphleveling:trait-sitmercilessmastery");
                string dynamicMercilessTrait = Lang.Get("seraphleveling:trait-merciless-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

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

            // Note: Claustrophobic Removed trait display was removed in favor of progressive cancellation
            // Claustrophobic is now handled in the negative trait section below - it progressively
            // decreases with mining level (1-10) and is replaced by Hardy when cancelled

            // =========================================================================
            // NEGATIVE TRAIT DISPLAY HANDLING
            // Display negative traits with remaining penalty, or remove when cancelled
            // =========================================================================

            // Civil trait (Tailor) - foraging loot penalty
            bool hasCivil = eplr.WatchedAttributes.GetBool("sitHasCivil", false);
            int civilRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CIVIL_REMAINING, 0);
            if (hasCivil)
            {
                // Vanilla format: <font color="#ff8484">• Civil </font> <font opacity="0.6">(-10% loot from foraging)</font>
                if (civilRemaining > 0)
                {
                    string dynamicCivilTrait = Lang.Get("seraphleveling:trait-civil-dynamic", civilRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Civil </font> <font opacity=""0\.6"">\(-\d+% loot from foraging\)</font>",
                        dynamicCivilTrait);
                }
                else
                {
                    // Remove Civil trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Civil </font> <font opacity=""0\.6"">\(-\d+% loot from foraging\)</font>",
                        "");
                }
            }

            // Weak trait (Tailor) - HP and mining speed penalty
            // Vanilla format: <font color="#ff8484">• Weak </font> <font opacity="0.6">(-2 health points, -10% mining speed)</font>
            // Both penalties are cancelled together at mining level 10
            bool hasWeak = eplr.WatchedAttributes.GetBool("sitHasWeak", false);
            int weakMiningRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WEAK_MINING_REMAINING, 0);
            int weakHpRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WEAK_HP_REMAINING, 0);
            if (hasWeak)
            {
                if (weakMiningRemaining > 0 || weakHpRemaining > 0)
                {
                    // Show both penalties during progression (both are cancelled at level 10)
                    string dynamicWeakTrait = Lang.Get("seraphleveling:trait-weak-dynamic", weakHpRemaining, weakMiningRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Weak </font> <font opacity=""0\.6"">\(-\d+ health points, -\d+% mining speed\)</font>",
                        dynamicWeakTrait);
                }
                else
                {
                    // Remove Weak trait completely when both penalties are cancelled
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Weak </font> <font opacity=""0\.6"">\(-\d+ health points, -\d+% mining speed\)</font>",
                        "");
                }
            }

            // Kind trait (Tailor) - animal loot and harvesting speed penalty
            // Vanilla format: <font color="#ff8484">• Kind </font> <font opacity="0.6">(-10% animal loot, -25% harvesting speed)</font>
            bool hasKind = eplr.WatchedAttributes.GetBool("sitHasKind", false);
            int kindLootRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_KIND_LOOT_REMAINING, 0);
            int kindSpeedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_KIND_SPEED_REMAINING, 0);
            if (hasKind)
            {
                if (kindLootRemaining > 0 || kindSpeedRemaining > 0)
                {
                    string dynamicKindTrait;
                    if (kindLootRemaining > 0 && kindSpeedRemaining > 0)
                    {
                        dynamicKindTrait = Lang.Get("seraphleveling:trait-kind-dynamic", kindLootRemaining, kindSpeedRemaining);
                    }
                    else if (kindLootRemaining > 0)
                    {
                        dynamicKindTrait = Lang.Get("seraphleveling:trait-kind-loot-only-dynamic", kindLootRemaining);
                    }
                    else
                    {
                        dynamicKindTrait = Lang.Get("seraphleveling:trait-kind-speed-only-dynamic", kindSpeedRemaining);
                    }
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Kind </font> <font opacity=""0\.6"">\(-\d+% animal loot, -\d+% harvesting speed\)</font>",
                        dynamicKindTrait);
                }
                else
                {
                    // Remove Kind trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Kind </font> <font opacity=""0\.6"">\(-\d+% animal loot, -\d+% harvesting speed\)</font>",
                        "");
                }
            }

            // Farsighted trait (Hunter) - melee damage penalty
            // Vanilla format: <font color="#ff8484">• Farsighted </font> <font opacity="0.6">(-15% melee damage)</font>
            bool hasFarsighted = eplr.WatchedAttributes.GetBool("sitHasFarsighted", false);
            int farsightedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FARSIGHTED_REMAINING, 0);
            if (hasFarsighted)
            {
                if (farsightedRemaining > 0)
                {
                    string dynamicFarsightedTrait = Lang.Get("seraphleveling:trait-farsighted-dynamic", farsightedRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Farsighted </font> <font opacity=""0\.6"">\(-\d+% melee damage\)</font>",
                        dynamicFarsightedTrait);
                }
                else
                {
                    // Remove Farsighted trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Farsighted </font> <font opacity=""0\.6"">\(-\d+% melee damage\)</font>",
                        "");
                }
            }

            // Nervous trait (Malefactor, Clockmaker) - melee damage penalty
            // Vanilla format: <font color="#ff8484">• Nervous </font> <font opacity="0.6">(-15% melee damage)</font>
            bool hasNervous = eplr.WatchedAttributes.GetBool("sitHasNervous", false);
            int nervousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_NERVOUS_REMAINING, 0);
            if (hasNervous)
            {
                if (nervousRemaining > 0)
                {
                    string dynamicNervousTrait = Lang.Get("seraphleveling:trait-nervous-dynamic", nervousRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Nervous </font> <font opacity=""0\.6"">\(-\d+% melee damage\)</font>",
                        dynamicNervousTrait);
                }
                else
                {
                    // Remove Nervous trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Nervous </font> <font opacity=""0\.6"">\(-\d+% melee damage\)</font>",
                        "");
                }
            }

            // Nearsighted trait (Blackguard) - ranged damage penalty
            // Vanilla format: <font color="#ff8484">• Nearsighted </font> <font opacity="0.6">(-15% ranged damage)</font>
            bool hasNearsighted = eplr.WatchedAttributes.GetBool("sitHasNearsighted", false);
            int nearsightedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_NEARSIGHTED_REMAINING, 0);
            if (hasNearsighted)
            {
                if (nearsightedRemaining > 0)
                {
                    string dynamicNearsightedTrait = Lang.Get("seraphleveling:trait-nearsighted-dynamic", nearsightedRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Nearsighted </font> <font opacity=""0\.6"">\(-\d+% ranged damage\)</font>",
                        dynamicNearsightedTrait);
                }
                else
                {
                    // Remove Nearsighted trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Nearsighted </font> <font opacity=""0\.6"">\(-\d+% ranged damage\)</font>",
                        "");
                }
            }

            // Frail trait (Malefactor, Clockmaker) - HP and ranged distance penalty
            // Vanilla format: <font color="#ff8484">• Frail </font> <font opacity="0.6">(-2.5 health points, -25% ranged distance)</font>
            // Both penalties are cancelled together at ranged level 25
            bool hasFrail = eplr.WatchedAttributes.GetBool("sitHasFrail", false);
            int frailDistanceRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FRAIL_DISTANCE_REMAINING, 0);
            float frailHpRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_FRAIL_HP_REMAINING, 0f);
            if (hasFrail)
            {
                if (frailDistanceRemaining > 0 || frailHpRemaining > 0)
                {
                    // Show both penalties during progression (both are cancelled at level 25)
                    string dynamicFrailTrait = Lang.Get("seraphleveling:trait-frail-dynamic", frailHpRemaining, frailDistanceRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Frail </font> <font opacity=""0\.6"">\(-[\d\.]+ health points, -\d+% ranged distance\)</font>",
                        dynamicFrailTrait);
                }
                else
                {
                    // Remove Frail trait completely when both penalties are cancelled
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Frail </font> <font opacity=""0\.6"">\(-[\d\.]+ health points, -\d+% ranged distance\)</font>",
                        "");
                }
            }

            // Heavyhanded trait (Blackguard) - vessel, foraging, wild crop penalties
            // Vanilla format: <font color="#ff8484">• Heavyhanded </font> <font opacity="0.6">(-10% cracked vessel loot, -15% loot from foraging, -20% wild crop drop rate)</font>
            bool hasHeavyhanded = eplr.WatchedAttributes.GetBool("sitHasHeavyhanded", false);
            int heavyhandedVesselRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_VESSEL_REMAINING, 0);
            int heavyhandedForagingRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_FORAGING_REMAINING, 0);
            int heavyhandedWildCropRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, 0);
            if (hasHeavyhanded)
            {
                if (heavyhandedVesselRemaining > 0 || heavyhandedForagingRemaining > 0 || heavyhandedWildCropRemaining > 0)
                {
                    // Build partial description
                    var parts = new System.Collections.Generic.List<string>();
                    if (heavyhandedVesselRemaining > 0) parts.Add($"-{heavyhandedVesselRemaining}% cracked vessel loot");
                    if (heavyhandedForagingRemaining > 0) parts.Add($"-{heavyhandedForagingRemaining}% loot from foraging");
                    if (heavyhandedWildCropRemaining > 0) parts.Add($"-{heavyhandedWildCropRemaining}% wild crop drop rate");

                    string partialDescription = string.Join(", ", parts);
                    string dynamicHeavyhandedTrait = Lang.Get("seraphleveling:trait-heavyhanded-partial-dynamic", partialDescription);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Heavyhanded </font> <font opacity=""0\.6"">\(-\d+% cracked vessel loot, -\d+% loot from foraging, -\d+% wild crop drop rate\)</font>",
                        dynamicHeavyhandedTrait);
                }
                else
                {
                    // Remove Heavyhanded trait completely - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Heavyhanded </font> <font opacity=""0\.6"">\(-\d+% cracked vessel loot, -\d+% loot from foraging, -\d+% wild crop drop rate\)</font>",
                        "");
                }
            }

            // Ravenous trait (Blackguard) - hunger rate penalty
            // Vanilla format: <font color="#ff8484">• Ravenous </font> <font opacity="0.6">(+30% hunger rate)</font>
            bool hasRavenous = eplr.WatchedAttributes.GetBool("sitHasVanillaRavenous", false);
            int ravenousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RAVENOUS_REMAINING, 0);
            if (hasRavenous)
            {
                if (ravenousRemaining > 0)
                {
                    // Show Ravenous with remaining penalty
                    string dynamicRavenousTrait = Lang.Get("seraphleveling:trait-ravenous-dynamic", ravenousRemaining);
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Ravenous </font> <font opacity=""0\.6"">\(\+\d+% hunger rate\)</font>",
                        dynamicRavenousTrait);
                }
                else
                {
                    // Remove Ravenous trait completely at level 30 - use empty string, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Ravenous </font> <font opacity=""0\.6"">\(\+\d+% hunger rate\)</font>",
                        "");
                }
            }

            // Claustrophobic trait (Hunter) - ore drop and mining speed penalties
            // Mining penalty decreases progressively with mining level (1-10)
            // At level 10, both mining and ore penalties are cancelled, and Hardy bonus starts showing
            bool hasClaustrophobic = eplr.WatchedAttributes.GetBool("sitHasClaustrophobic", false);
            int claustrophobicMiningRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, 0);

            ClientApi.Logger.Debug($"[SeraphLeveling] Claustrophobic check: hasClaustrophobic={hasClaustrophobic}, miningRemaining={claustrophobicMiningRemaining}");
            ClientApi.Logger.Debug($"[SeraphLeveling] Result contains 'Claustrophobic': {__result.Contains("Claustrophobic")}");

            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining > 0)
                {
                    // Show both ore drop (-15%) and mining speed penalties while being reduced
                    // Ore penalty stays at -15% until fully cancelled, mining speed decreases progressively
                    string dynamicClaustrophobicTrait = Lang.Get("seraphleveling:trait-claustrophobic-dynamic",
                        SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY, claustrophobicMiningRemaining);
                    ClientApi.Logger.Debug($"[SeraphLeveling] Replacing Claustrophobic with: {dynamicClaustrophobicTrait}");

                    // Vanilla format: <font color="#ff8484">• Claustrophobic </font> <font opacity="0.6">(-15% ore drop rate, -10% mining speed)</font>
                    string beforeReplace = __result;
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"<font color=""#ff8484"">• Claustrophobic </font> <font opacity=""0\.6"">\(-\d+% ore drop rate, -\d+% mining speed\)</font>",
                        dynamicClaustrophobicTrait);

                    if (__result == beforeReplace)
                    {
                        ClientApi.Logger.Debug("[SeraphLeveling] Primary regex did not match, trying alternative patterns");
                        // Try without bullet
                        __result = System.Text.RegularExpressions.Regex.Replace(__result,
                            @"<font color=""#ff8484"">Claustrophobic</font>.*?\(-\d+% ore drop rate, -\d+% mining speed\).*?</font>",
                            dynamicClaustrophobicTrait);
                    }

                    if (__result == beforeReplace)
                    {
                        // Try simple pattern without font tags
                        __result = System.Text.RegularExpressions.Regex.Replace(__result,
                            @"Claustrophobic.*?\(-\d+% ore drop rate, -\d+% mining speed\)",
                            dynamicClaustrophobicTrait);
                    }

                    ClientApi.Logger.Debug($"[SeraphLeveling] After replacement: {(__result != beforeReplace ? "SUCCESS" : "FAILED")}");
                }
                else
                {
                    // Both penalties cancelled at level 10 - remove Claustrophobic entirely (Hardy will show instead)
                    ClientApi.Logger.Debug("[SeraphLeveling] Trying to remove Claustrophobic (level >= 10)");
                    string beforeReplace = __result;

                    // Vanilla format: <font color="#ff8484">• Claustrophobic </font> <font opacity="0.6">(-15% ore drop rate, -10% mining speed)</font>
                    // Use empty string replacement, cleanup at end handles newlines
                    __result = System.Text.RegularExpressions.Regex.Replace(__result,
                        @"\n?<font color=""#ff8484"">• Claustrophobic </font> <font opacity=""0\.6"">\(-\d+% ore drop rate, -\d+% mining speed\)</font>",
                        "");

                    if (__result == beforeReplace)
                    {
                        ClientApi.Logger.Debug("[SeraphLeveling] Primary removal regex did not match, trying alternatives");
                        // Try broader pattern
                        __result = System.Text.RegularExpressions.Regex.Replace(__result,
                            @"\n?<font color=""#ff8484"">.*?Claustrophobic.*?</font>.*?\(-\d+% ore drop rate, -\d+% mining speed\).*?</font>",
                            "");
                    }

                    if (__result == beforeReplace)
                    {
                        // Try without font tags at all
                        __result = System.Text.RegularExpressions.Regex.Replace(__result,
                            @"\n?.*?Claustrophobic.*?\(-\d+% ore drop rate, -\d+% mining speed\).*?",
                            "");
                    }

                    ClientApi.Logger.Debug($"[SeraphLeveling] Removal result: {(__result != beforeReplace ? "SUCCESS" : "FAILED")}");
                }
            }

            // =========================================================================
            // COMBAT OVERHAUL PROFICIENCY TRAIT DISPLAY
            // Display CO proficiencies that have credits > 0
            // =========================================================================

            // Check if CO is enabled by looking for any CO credits
            var coProficiencies = new (string statName, string displayName, float maxBonus)[]
            {
                ("bowsProficiency", "Bows Proficiency", 0.5f),
                ("crossbowsProficiency", "Crossbows Proficiency", 0.5f),
                ("firearmsProficiency", "Firearms Proficiency", 0.5f),
                ("slingsProficiency", "Slings Proficiency", 0.3f),
                ("oneHandedSwordsProficiency", "One-Handed Swords", 0.3f),
                ("twoHandedSwordsProficiency", "Two-Handed Swords", 0.3f),
                ("spearsProficiency", "Spears Proficiency", 0.3f),
                ("javelinsProficiency", "Javelins Proficiency", 0.3f),
                ("macesProficiency", "Maces Proficiency", 0.3f),
                ("clubsProficiency", "Clubs Proficiency", 0.3f),
                ("halberdsProficiency", "Halberds Proficiency", 0.3f),
                ("axesProficiency", "Axes Proficiency", 0.3f),
                ("quarterstaffProficiency", "Quarterstaff Proficiency", 0.3f),
            };

            foreach (var (statName, displayName, maxBonus) in coProficiencies)
            {
                string watchedKey = $"sitCO{statName}Credits";
                int credits = eplr.WatchedAttributes.GetInt(watchedKey, 0);
                if (credits > 0)
                {
                    float bonus = credits * 0.01f;
                    if (bonus > maxBonus) bonus = maxBonus;
                    string coTrait = $"<font color=\"#84ff84\">• {displayName} </font> <font opacity=\"0.6\">(+{bonus:F2})</font>";

                    // Re-check hasNoTraits
                    hasNoTraits = string.IsNullOrEmpty(__result) ||
                                  __result.Trim() == noTraitsMsg.Trim() ||
                                  __result == noTraitsMsg ||
                                  __result.Contains(noTraitsMsg) ||
                                  __result.Contains("No positive or negative traits");

                    if (hasNoTraits)
                    {
                        __result = coTrait;
                    }
                    else
                    {
                        __result = __result + "\n" + coTrait;
                    }
                }
            }

            // Steady Aim (separate since it's a shared ranged proficiency)
            int steadyAimCredits = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_STEADY_AIM_CREDITS, 0);
            if (steadyAimCredits > 0)
            {
                float steadyAimBonus = steadyAimCredits * 0.01f;
                if (steadyAimBonus > 0.5f) steadyAimBonus = 0.5f;
                string steadyAimTrait = $"<font color=\"#84ff84\">• Steady Aim </font> <font opacity=\"0.6\">(+{steadyAimBonus:F2})</font>";

                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = steadyAimTrait;
                }
                else
                {
                    __result = __result + "\n" + steadyAimTrait;
                }
            }

            // CO Negative traits display (Trembling Aim, Clumsy Hands, etc.)
            float tremblingAimRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_TREMBLING_AIM_REMAINING, 0f);
            if (tremblingAimRemaining > 0)
            {
                string tremblingTrait = $"<font color=\"#ff8484\">• Trembling Aim </font> <font opacity=\"0.6\">(-{tremblingAimRemaining:F2} steady aim)</font>";
                __result = __result + "\n" + tremblingTrait;
            }

            // Clean up any newline issues that might have been introduced
            // First normalize line endings (handle \r\n, \r, and \n)
            __result = __result.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove any lines that are empty or whitespace-only
            var lines = __result.Split('\n');
            var nonEmptyLines = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    nonEmptyLines.Add(line.Trim());
                }
            }
            __result = string.Join("\n", nonEmptyLines);

            // Final trim
            __result = __result.Trim();

            ClientApi.Logger.Debug($"[SeraphLeveling] Modified result: {__result}");
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
            // Debug: Log all damage events to diagnose CO issues
            SeraphLevelingModSystem.ServerApi?.Logger.Debug($"[SeraphLeveling] ReceiveDamage_Postfix: target={__instance?.Code}, damage={damage}, result={__result}, SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}");

            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

            // Track armor damage blocked if the entity taking damage is a player wearing armor
            TrackArmorDamageBlocked(__instance, damageSource, damage);

            // Check if this is ranged damage (projectile with CauseEntity)
            if (SeraphLevelingModSystem.IsRangedDamage(damageSource))
            {
                // For ranged: CauseEntity is the shooter, SourceEntity is the projectile
                var shooterEntity = damageSource.CauseEntity as EntityPlayer;
                if (shooterEntity == null) return;

                var shooterPlayer = shooterEntity.Player as IServerPlayer;
                if (shooterPlayer == null) return;

                // Don't count self-damage
                if (__instance == shooterEntity) return;

                // Get the weapon combination (bow+arrow, sling+stone, etc.)
                string weaponCombo = SeraphLevelingModSystem.GetRangedWeaponCombo(damageSource.SourceEntity, shooterEntity);

                if (weaponCombo != null)
                {
                    SeraphLevelingModSystem.ProcessRangedDamage(shooterPlayer, weaponCombo, damage);

                    // Also track Precise damage if target is a mechanical creature
                    if (SeraphLevelingModSystem.IsMechanicalCreature(__instance))
                    {
                        SeraphLevelingModSystem.ProcessPreciseDamage(shooterPlayer, weaponCombo, damage);
                    }
                }

                // Combat Overhaul: Also track CO ranged proficiency if enabled
                if (SeraphLevelingModSystem.IsCOCompatEnabled)
                {
                    // First, check the projectile itself for thrown weapons (javelins, thrown spears)
                    // These weapons ARE the projectile, so we detect from SourceEntity
                    string projectileCode = damageSource.SourceEntity?.Code?.ToString();
                    if (!string.IsNullOrEmpty(projectileCode))
                    {
                        var (projProficiency, projWeaponCode) = SeraphLevelingModSystem.GetCOWeaponType(projectileCode);
                        SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] CO ranged projectile check: '{projectileCode}' -> proficiency='{projProficiency ?? "null"}'");
                        if (projProficiency != null)
                        {
                            // Javelins proficiency is NOT in IsCORangedProficiency, so process it here
                            SeraphLevelingModSystem.ProcessCOProficiencyDamage(shooterPlayer, projProficiency, projWeaponCode, damage);
                        }
                    }

                    // Also check held ranged weapon for bows/crossbows/slings/firearms
                    var heldRangedItem = shooterPlayer.Entity?.RightHandItemSlot?.Itemstack?.Collectible;
                    if (heldRangedItem != null)
                    {
                        string rangedItemCode = heldRangedItem.Code?.ToString();
                        var (proficiencyStat, coWeaponCode) = SeraphLevelingModSystem.GetCOWeaponType(rangedItemCode);
                        if (proficiencyStat != null && SeraphLevelingModSystem.IsCORangedProficiency(proficiencyStat))
                        {
                            SeraphLevelingModSystem.ProcessCOProficiencyDamage(shooterPlayer, proficiencyStat, coWeaponCode, damage);
                        }
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
            SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] Melee hit with held item: '{itemCode}'");

            string weaponType = SeraphLevelingModSystem.GetWeaponTypeFromCode(itemCode);

            if (weaponType != null)
            {
                SeraphLevelingModSystem.ProcessMeleeDamage(attackerPlayer, weaponType, damage);

                // Also track Precise damage if target is a mechanical creature
                if (SeraphLevelingModSystem.IsMechanicalCreature(__instance))
                {
                    SeraphLevelingModSystem.ProcessPreciseDamage(attackerPlayer, weaponType, damage);
                }
            }

            // Combat Overhaul: Also track CO melee proficiency if enabled
            if (SeraphLevelingModSystem.IsCOCompatEnabled)
            {
                var (proficiencyStat, coWeaponCode) = SeraphLevelingModSystem.GetCOWeaponType(itemCode);
                SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] CO weapon check: itemCode='{itemCode}' -> proficiency='{proficiencyStat ?? "null"}', weaponCode='{coWeaponCode ?? "null"}'");
                if (proficiencyStat != null && !SeraphLevelingModSystem.IsCORangedProficiency(proficiencyStat))
                {
                    SeraphLevelingModSystem.ProcessCOProficiencyDamage(attackerPlayer, proficiencyStat, coWeaponCode, damage);
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
                string armorType = SeraphLevelingModSystem.GetArmorType(itemCode);

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
                    SeraphLevelingModSystem.ProcessArmorDamageBlocked(player, damageBlocked, itemCode);
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
                SeraphLevelingModSystem.ProcessAnimalHarvested(serverPlayer);
            }
            catch (Exception ex)
            {
                // Silently ignore errors to avoid breaking the game
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in SetHarvested_Postfix: {ex.Message}");
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
                SeraphLevelingModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnHeldInteractStop_Postfix: {ex.Message}");
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
                                        SeraphLevelingModSystem.ProcessMenderRepair(serverPlayer);
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
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnModifiedInInventorySlot_Postfix: {ex.Message}");
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
                SeraphLevelingModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnHeldInteractStep_Postfix: {ex.Message}");
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
                    SeraphLevelingModSystem.ProcessTranslocatorRepair(serverPlayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in DoRepair_Postfix: {ex.Message}");
            }
        }
    }

    // =========================================================================
    // TEST SUITE
    // =========================================================================

    /// <summary>
    /// Result of a single test.
    /// </summary>
    public class TestResult
    {
        public string TestId { get; set; }
        public string Description { get; set; }
        public bool Passed { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string ErrorMessage { get; set; }

        public override string ToString()
        {
            if (Passed)
                return $"  [PASS] {TestId}: {Description}";
            else
                return $"  [FAIL] {TestId}: {Description}\n         Expected: {ExpectedValue}, Got: {ActualValue}" +
                       (string.IsNullOrEmpty(ErrorMessage) ? "" : $"\n         Error: {ErrorMessage}");
        }
    }

    /// <summary>
    /// Test suite for Seraph Leveling mod. Runs automated tests for all trait calculations.
    /// </summary>
    public static class TraitTestSuite
    {
        private static List<TestResult> results;
        private static List<TestResult> allFailedTests;
        private static int passCount;
        private static int failCount;

        /// <summary>
        /// Run all tests or a specific category.
        /// </summary>
        public static string RunTests(string category, IServerPlayer player)
        {
            results = new List<TestResult>();
            allFailedTests = new List<TestResult>();
            passCount = 0;
            failCount = 0;

            var sb = new StringBuilder();
            sb.AppendLine("[SeraphLeveling Tests] Starting test suite...\n");

            bool runAll = string.IsNullOrEmpty(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (category != null && category.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Available test categories:");
                sb.AppendLine("  mining      - Mining calculation tests");
                sb.AppendLine("  melee       - Melee damage calculation tests");
                sb.AppendLine("  ranged      - Ranged damage/accuracy/distance tests");
                sb.AppendLine("  walking     - Walking speed calculation tests");
                sb.AppendLine("  hunger      - Hunger rate calculation tests");
                sb.AppendLine("  armor       - Armor durability/walk speed tests");
                sb.AppendLine("  negative    - Negative trait cancellation tests");
                sb.AppendLine("  detection   - Block/weapon/armor detection tests");
                sb.AppendLine("  persistence - Data save/load consistency tests");
                sb.AppendLine("  all         - Run all tests");
                return sb.ToString();
            }

            // Run test categories
            if (runAll || (category != null && category.Equals("mining", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running mining tests...");
                RunMiningTests();
                sb.AppendLine(FormatCategoryResults("Mining"));
            }

            if (runAll || (category != null && category.Equals("melee", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running melee tests...");
                RunMeleeTests();
                sb.AppendLine(FormatCategoryResults("Melee"));
            }

            if (runAll || (category != null && category.Equals("ranged", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running ranged tests...");
                RunRangedTests();
                sb.AppendLine(FormatCategoryResults("Ranged"));
            }

            if (runAll || (category != null && category.Equals("walking", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running walking tests...");
                RunWalkingTests();
                sb.AppendLine(FormatCategoryResults("Walking"));
            }

            if (runAll || (category != null && category.Equals("hunger", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running hunger tests...");
                RunHungerTests();
                sb.AppendLine(FormatCategoryResults("Hunger"));
            }

            if (runAll || (category != null && category.Equals("armor", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running armor tests...");
                RunArmorTests();
                sb.AppendLine(FormatCategoryResults("Armor"));
            }

            if (runAll || (category != null && category.Equals("negative", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running negative trait tests...");
                RunNegativeTraitTests();
                sb.AppendLine(FormatCategoryResults("Negative Traits"));
            }

            if (runAll || (category != null && category.Equals("detection", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running detection tests...");
                RunDetectionTests();
                sb.AppendLine(FormatCategoryResults("Detection"));
            }

            if (runAll || (category != null && category.Equals("persistence", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("[SeraphLeveling Tests] Running persistence tests...");
                RunPersistenceTests(player);
                sb.AppendLine(FormatCategoryResults("Persistence"));
            }

            // Summary
            sb.AppendLine("\n[SeraphLeveling Tests] === SUMMARY ===");
            sb.AppendLine($"  TOTAL: {passCount}/{passCount + failCount} passed ({(passCount + failCount > 0 ? (passCount * 100 / (passCount + failCount)) : 0)}%)");

            if (failCount > 0 && allFailedTests.Count > 0)
            {
                sb.AppendLine("\nFailed tests:");
                foreach (var result in allFailedTests)
                {
                    sb.AppendLine(result.ToString());
                }
            }

            return sb.ToString();
        }

        private static string FormatCategoryResults(string category)
        {
            int catPass = results.Count(r => r.Passed);
            int catFail = results.Count(r => !r.Passed);
            int catTotal = results.Count;
            string result = $"  {category}: {catPass}/{catTotal} passed";

            // Save failed tests before clearing
            allFailedTests.AddRange(results.Where(r => !r.Passed));

            // Update totals
            passCount += catPass;
            failCount += catFail;

            // Reset for next category
            results.Clear();

            return result;
        }

        // =========================================================================
        // ASSERTION HELPERS
        // =========================================================================

        private static void AssertEqual<T>(string testId, string desc, T expected, T actual)
        {
            bool passed = EqualityComparer<T>.Default.Equals(expected, actual);
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = passed,
                ExpectedValue = expected?.ToString() ?? "null",
                ActualValue = actual?.ToString() ?? "null"
            });
        }

        private static void AssertTrue(string testId, string desc, bool condition, string expectedDesc = "true", string actualDesc = "false")
        {
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = condition,
                ExpectedValue = expectedDesc,
                ActualValue = condition ? expectedDesc : actualDesc
            });
        }

        private static void AssertInRange(string testId, string desc, int value, int min, int max)
        {
            bool passed = value >= min && value <= max;
            results.Add(new TestResult
            {
                TestId = testId,
                Description = desc,
                Passed = passed,
                ExpectedValue = $"{min}-{max}",
                ActualValue = value.ToString()
            });
        }

        // =========================================================================
        // MINING TESTS
        // =========================================================================

        private static void RunMiningTests()
        {
            int maxMining = SeraphLevelingModSystem.MaxMiningSpeedPercent;

            // MINE-001: First credit at base increment
            // With default settings: 100 blocks = 1 credit = 1%
            AssertEqual("MINE-001", "Mining bonus percent at 1 credit", 1, SeraphLevelingModSystem.CalculateMiningBonusPercent(1));

            // MINE-002: Credits capped at configured max
            AssertEqual("MINE-002", $"Mining bonus capped at max ({maxMining}%)", maxMining, SeraphLevelingModSystem.CalculateMiningBonusPercent(maxMining + 50));

            // MINE-003: Zero credits yields zero bonus
            AssertEqual("MINE-003", "Mining bonus at 0 credits", 0, SeraphLevelingModSystem.CalculateMiningBonusPercent(0));

            // MINE-004: Credits equal bonus percent (1:1 ratio)
            AssertEqual("MINE-004", "25 credits = 25% bonus", 25, SeraphLevelingModSystem.CalculateMiningBonusPercent(25));

            // MINE-005: Float bonus calculation
            float expectedFloat = 0.25f;
            float actualFloat = SeraphLevelingModSystem.CalculateMiningBonus(25);
            AssertTrue("MINE-005", "Float bonus 25 credits = 0.25", Math.Abs(expectedFloat - actualFloat) < 0.001f, "0.25", actualFloat.ToString("F3"));

            // MINE-006: Float bonus capped at configured max
            float maxFloat = maxMining / 100f;
            float actualMaxFloat = SeraphLevelingModSystem.CalculateMiningBonus(maxMining + 50);
            AssertTrue("MINE-006", $"Float bonus capped at {maxFloat:F2}", Math.Abs(maxFloat - actualMaxFloat) < 0.001f, maxFloat.ToString("F2"), actualMaxFloat.ToString("F2"));

            // MINE-007: Max credits calculation (no entity, default)
            int maxCredits = SeraphLevelingModSystem.GetMaxMiningCredits(null);
            AssertEqual("MINE-007", "Max mining credits (null entity)", maxMining, maxCredits);

            // MINE-008: CalculateMaxCredits returns MaxMiningSpeedPercent
            AssertEqual("MINE-008", "CalculateMaxCredits matches MaxMiningSpeedPercent", maxMining, SeraphLevelingModSystem.CalculateMaxCredits());

            // MINE-009: Boundary - exactly at max
            AssertEqual("MINE-009", "Exactly at max credits", maxMining, SeraphLevelingModSystem.CalculateMiningBonusPercent(maxMining));

            // MINE-010: Boundary - one over max
            AssertEqual("MINE-010", "One over max credits still capped", maxMining, SeraphLevelingModSystem.CalculateMiningBonusPercent(maxMining + 1));
        }

        // =========================================================================
        // MELEE TESTS
        // =========================================================================

        private static void RunMeleeTests()
        {
            // MELEE-001: First credit at base increment
            AssertEqual("MELEE-001", "Melee bonus percent at 1 credit", 1, SeraphLevelingModSystem.CalculateMeleeBonusPercent(1));

            // MELEE-002: Credits capped at max
            AssertEqual("MELEE-002", "Melee bonus capped at max (default 50)", SeraphLevelingModSystem.MaxMeleeDamagePercent, SeraphLevelingModSystem.CalculateMeleeBonusPercent(100));

            // MELEE-003: Zero credits yields zero bonus
            AssertEqual("MELEE-003", "Melee bonus at 0 credits", 0, SeraphLevelingModSystem.CalculateMeleeBonusPercent(0));

            // MELEE-004: Credits equal bonus percent (1:1 ratio)
            AssertEqual("MELEE-004", "25 credits = 25% bonus", 25, SeraphLevelingModSystem.CalculateMeleeBonusPercent(25));

            // MELEE-005: Max credits for null entity
            int maxCredits = SeraphLevelingModSystem.GetMaxMeleeCredits(null);
            AssertEqual("MELEE-005", "Max melee credits (null entity)", SeraphLevelingModSystem.MaxMeleeDamagePercent, maxCredits);

            // MELEE-006: Weapon detection - sword-copper
            string swordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:sword-copper");
            AssertTrue("MELEE-006", "Sword detected as valid melee weapon", swordResult != null, "not null", swordResult ?? "null");

            // MELEE-007: Weapon detection - falx-copper
            string falxResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:falx-copper");
            AssertTrue("MELEE-007", "Falx detected as valid melee weapon", falxResult != null, "not null", falxResult ?? "null");

            // MELEE-008: Weapon detection - spear-copper
            string spearResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:spear-copper");
            AssertTrue("MELEE-008", "Spear detected as valid melee weapon", spearResult != null, "not null", spearResult ?? "null");

            // MELEE-009: Weapon detection - blade variant
            string bladeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("blade-copper");
            AssertTrue("MELEE-009", "Blade detected as valid melee weapon", bladeResult != null, "not null", bladeResult ?? "null");

            // MELEE-010: Weapon detection - longsword variant
            string longswordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("longsword-iron");
            AssertTrue("MELEE-010", "Longsword detected as valid melee weapon", longswordResult != null, "not null", longswordResult ?? "null");

            // MELEE-011: Weapon detection - shortsword variant
            string shortswordResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("shortsword-iron");
            AssertTrue("MELEE-011", "Shortsword detected as valid melee weapon", shortswordResult != null, "not null", shortswordResult ?? "null");

            // MELEE-012: Invalid weapon - knife
            string knifeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("knife-copper");
            AssertTrue("MELEE-012", "Knife NOT detected as melee weapon", knifeResult == null, "null", knifeResult ?? "null");

            // MELEE-013: Invalid weapon - axe
            string axeResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("axe-copper");
            AssertTrue("MELEE-013", "Axe NOT detected as melee weapon", axeResult == null, "null", axeResult ?? "null");

            // MELEE-014: Invalid weapon - bow
            string bowResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("bow-long");
            AssertTrue("MELEE-014", "Bow NOT detected as melee weapon", bowResult == null, "null", bowResult ?? "null");

            // MELEE-015: Null input handling
            string nullResult = SeraphLevelingModSystem.GetWeaponTypeFromCode(null);
            AssertTrue("MELEE-015", "Null input returns null", nullResult == null, "null", nullResult ?? "null");

            // MELEE-016: Empty input handling
            string emptyResult = SeraphLevelingModSystem.GetWeaponTypeFromCode("");
            AssertTrue("MELEE-016", "Empty input returns null", emptyResult == null, "null", emptyResult ?? "null");

            // MELEE-017: Full code preserved
            string fullCode = SeraphLevelingModSystem.GetWeaponTypeFromCode("game:sword-copper");
            AssertEqual("MELEE-017", "Full item code preserved", "game:sword-copper", fullCode);
        }

        // =========================================================================
        // RANGED TESTS
        // =========================================================================

        private static void RunRangedTests()
        {
            int maxDmg = SeraphLevelingModSystem.MaxRangedDamagePercent;
            int maxAcc = SeraphLevelingModSystem.MaxRangedAccuracyPercent;
            int maxDist = SeraphLevelingModSystem.MaxRangedDistancePercent;

            // RANGED-001: All three stats increase with credits (null entity = no vanilla bonus)
            var (damage, accuracy, distance) = SeraphLevelingModSystem.CalculateRangedBonusPercents(25, null);
            AssertEqual("RANGED-001a", "Ranged damage at 25 credits", 25, damage);
            AssertEqual("RANGED-001b", "Ranged accuracy at 25 credits", 25, accuracy);
            AssertEqual("RANGED-001c", "Ranged distance at 25 credits", 25, distance);

            // RANGED-002: Zero credits
            var (d0, a0, dist0) = SeraphLevelingModSystem.CalculateRangedBonusPercents(0, null);
            AssertEqual("RANGED-002", "Ranged bonuses at 0 credits", 0, d0 + a0 + dist0);

            // RANGED-003: Stats capped at configured max
            var (dMax, aMax, distMax) = SeraphLevelingModSystem.CalculateRangedBonusPercents(maxDmg + 50, null);
            AssertEqual("RANGED-003a", $"Ranged damage capped at {maxDmg}", maxDmg, dMax);
            AssertEqual("RANGED-003b", $"Ranged accuracy capped at {maxAcc}", maxAcc, aMax);
            AssertEqual("RANGED-003c", $"Ranged distance capped at {maxDist}", maxDist, distMax);

            // RANGED-004: Max ranged credits for null entity
            int maxCredits = SeraphLevelingModSystem.GetMaxRangedCredits(null);
            AssertEqual("RANGED-004", "Max ranged credits (null entity)", maxDmg, maxCredits);
        }

        // =========================================================================
        // WALKING TESTS
        // =========================================================================

        private static void RunWalkingTests()
        {
            int maxWalking = SeraphLevelingModSystem.MaxWalkingSpeedPercent;

            // WALKING-001: Walking bonus calculation (null entity = no vanilla bonus)
            AssertEqual("WALKING-001", "Walking bonus at 5 credits", 5, SeraphLevelingModSystem.CalculateWalkingBonusPercent(5, null));

            // WALKING-002: Zero credits
            AssertEqual("WALKING-002", "Walking bonus at 0 credits", 0, SeraphLevelingModSystem.CalculateWalkingBonusPercent(0, null));

            // WALKING-003: Capped at configured max
            AssertEqual("WALKING-003", $"Walking bonus capped at max ({maxWalking}%)", maxWalking, SeraphLevelingModSystem.CalculateWalkingBonusPercent(maxWalking + 50, null));

            // WALKING-004: Exactly at max
            AssertEqual("WALKING-004", "Exactly at max walking credits", maxWalking, SeraphLevelingModSystem.CalculateWalkingBonusPercent(maxWalking, null));
        }

        // =========================================================================
        // HUNGER TESTS
        // =========================================================================

        private static void RunHungerTests()
        {
            int maxHunger = SeraphLevelingModSystem.MaxHungerReductionPercent;

            // HUNGER-001: Hunger bonus calculation (null entity)
            AssertEqual("HUNGER-001", "Hunger bonus at 10 credits", 10, SeraphLevelingModSystem.CalculateHungerBonusPercent(10, null));

            // HUNGER-002: Zero credits
            AssertEqual("HUNGER-002", "Hunger bonus at 0 credits", 0, SeraphLevelingModSystem.CalculateHungerBonusPercent(0, null));

            // HUNGER-003: Max hunger credits for null entity (non-Ravenous)
            int maxCredits = SeraphLevelingModSystem.CalculateMaxHungerCredits(null);
            AssertEqual("HUNGER-003", $"Max hunger credits (null = non-Ravenous)", maxHunger, maxCredits);

            // HUNGER-004: Capped at max
            AssertEqual("HUNGER-004", $"Hunger bonus capped at max ({maxCredits})", maxCredits, SeraphLevelingModSystem.CalculateHungerBonusPercent(maxCredits + 50, null));
        }

        // =========================================================================
        // ARMOR TESTS
        // =========================================================================

        private static void RunArmorTests()
        {
            // ARMOR-001: Armor durability bonus (null entity = no vanilla bonus)
            AssertEqual("ARMOR-001", "Armor durability at 25 credits", 25, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(25, null));

            // ARMOR-002: Armor walk speed bonus (null entity)
            AssertEqual("ARMOR-002", "Armor walk speed at 25 credits", 25, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(25, null));

            // ARMOR-003: Durability capped at max
            AssertEqual("ARMOR-003", "Armor durability capped", SeraphLevelingModSystem.MaxArmorDurabilityPercent, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(100, null));

            // ARMOR-004: Walk speed capped at max
            AssertEqual("ARMOR-004", "Armor walk speed capped", SeraphLevelingModSystem.MaxArmorWalkSpeedPercent, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(100, null));

            // ARMOR-005: Zero credits
            AssertEqual("ARMOR-005a", "Zero durability credits", 0, SeraphLevelingModSystem.CalculateArmorDurabilityBonusPercent(0, null));
            AssertEqual("ARMOR-005b", "Zero walk speed credits", 0, SeraphLevelingModSystem.CalculateArmorWalkSpeedBonusPercent(0, null));

            // ARMOR-006: Armor type detection - plate
            AssertEqual("ARMOR-006", "Plate armor detected", "plate", SeraphLevelingModSystem.GetArmorType("armor-body-plate-iron"));

            // ARMOR-007: Armor type detection - scale
            AssertEqual("ARMOR-007", "Scale armor detected", "scale", SeraphLevelingModSystem.GetArmorType("armor-body-scale-iron"));

            // ARMOR-008: Armor type detection - brigandine
            AssertEqual("ARMOR-008", "Brigandine armor detected", "brigandine", SeraphLevelingModSystem.GetArmorType("armor-body-brigandine-iron"));

            // ARMOR-009: Armor type detection - chain
            AssertEqual("ARMOR-009", "Chain armor detected", "chain", SeraphLevelingModSystem.GetArmorType("armor-body-chain-iron"));

            // ARMOR-010: Armor type detection - lamellar (treated as chain)
            AssertEqual("ARMOR-010", "Lamellar treated as chain", "chain", SeraphLevelingModSystem.GetArmorType("armor-body-lamellar-iron"));

            // ARMOR-011: Armor type detection - leather (light)
            AssertEqual("ARMOR-011", "Leather detected as light", "light", SeraphLevelingModSystem.GetArmorType("armor-body-leather"));

            // ARMOR-012: Armor type detection - gambeson (light)
            AssertEqual("ARMOR-012", "Gambeson detected as light", "light", SeraphLevelingModSystem.GetArmorType("armor-body-gambeson"));

            // ARMOR-013: Non-armor returns null
            string nonArmor = SeraphLevelingModSystem.GetArmorType("clothes-upperbody-shirt");
            AssertTrue("ARMOR-013", "Non-armor returns null", nonArmor == null, "null", nonArmor ?? "null");

            // ARMOR-014: Null input
            string nullResult = SeraphLevelingModSystem.GetArmorType(null);
            AssertTrue("ARMOR-014", "Null input returns null", nullResult == null, "null", nullResult ?? "null");

            // ARMOR-015: First equip bonus - plate
            AssertEqual("ARMOR-015", "Plate first equip bonus", SeraphLevelingModSystem.FIRST_EQUIP_PLATE_BONUS, SeraphLevelingModSystem.GetFirstEquipBonus("plate"));

            // ARMOR-016: First equip bonus - scale
            AssertEqual("ARMOR-016", "Scale first equip bonus", SeraphLevelingModSystem.FIRST_EQUIP_SCALE_BONUS, SeraphLevelingModSystem.GetFirstEquipBonus("scale"));

            // ARMOR-017: First equip bonus - brigandine
            AssertEqual("ARMOR-017", "Brigandine first equip bonus", SeraphLevelingModSystem.FIRST_EQUIP_BRIGANDINE_BONUS, SeraphLevelingModSystem.GetFirstEquipBonus("brigandine"));

            // ARMOR-018: First equip bonus - chain
            AssertEqual("ARMOR-018", "Chain first equip bonus", SeraphLevelingModSystem.FIRST_EQUIP_CHAIN_BONUS, SeraphLevelingModSystem.GetFirstEquipBonus("chain"));

            // ARMOR-019: First equip bonus - light (default)
            AssertEqual("ARMOR-019", "Light first equip bonus", SeraphLevelingModSystem.FIRST_EQUIP_LIGHT_BONUS, SeraphLevelingModSystem.GetFirstEquipBonus("light"));

            // ARMOR-020: Walk speed first equip bonus - plate
            AssertEqual("ARMOR-020", "Plate walk speed bonus", SeraphLevelingModSystem.FIRST_EQUIP_WALKSPEED_PLATE_BONUS, SeraphLevelingModSystem.GetFirstEquipWalkSpeedBonus("plate"));

            // ARMOR-021: Full armor code with game: prefix
            AssertEqual("ARMOR-021", "Game prefix handled", "plate", SeraphLevelingModSystem.GetArmorType("game:armor-body-plate-iron"));
        }

        // =========================================================================
        // NEGATIVE TRAIT CANCELLATION TESTS
        // =========================================================================

        private static void RunNegativeTraitTests()
        {
            // NEG-001: CalculateRemainingPenalty - basic
            AssertEqual("NEG-001", "Remaining penalty 15-10=5", 5, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 10));

            // NEG-002: CalculateRemainingPenalty - fully cancelled
            AssertEqual("NEG-002", "Remaining penalty 15-15=0", 0, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 15));

            // NEG-003: CalculateRemainingPenalty - over-cancelled stays at 0
            AssertEqual("NEG-003", "Remaining penalty 15-20=0 (not negative)", 0, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 20));

            // NEG-004: CalculateRemainingPenalty - zero progress
            AssertEqual("NEG-004", "Remaining penalty 15-0=15", 15, SeraphLevelingModSystem.CalculateRemainingPenalty(15, 0));

            // NEG-005: CalculateRemainingPenalty - negative bonus increases penalty (math: 15-(-5)=20)
            AssertEqual("NEG-005", "Remaining penalty 15-(-5)=20", 20, SeraphLevelingModSystem.CalculateRemainingPenalty(15, -5));

            // NEG-006: Farsighted penalty constant
            AssertEqual("NEG-006", "Farsighted penalty is 15", 15, SeraphLevelingModSystem.VANILLA_FARSIGHTED_MELEE_PENALTY);

            // NEG-007: Nervous penalty constant
            AssertEqual("NEG-007", "Nervous penalty is 15", 15, SeraphLevelingModSystem.VANILLA_NERVOUS_MELEE_PENALTY);

            // NEG-008: Nearsighted penalty constant
            AssertEqual("NEG-008", "Nearsighted penalty is 15", 15, SeraphLevelingModSystem.VANILLA_NEARSIGHTED_RANGED_PENALTY);

            // NEG-009: Frail distance penalty constant
            AssertEqual("NEG-009", "Frail distance penalty is 25", 25, SeraphLevelingModSystem.VANILLA_FRAIL_DISTANCE_PENALTY);

            // NEG-010: Civil foraging penalty constant
            AssertEqual("NEG-010", "Civil foraging penalty is 10", 10, SeraphLevelingModSystem.VANILLA_CIVIL_FORAGING_PENALTY);

            // NEG-011: Weak mining penalty constant
            AssertEqual("NEG-011", "Weak mining penalty is 10", 10, SeraphLevelingModSystem.VANILLA_WEAK_MINING_PENALTY);

            // NEG-012: Claustrophobic mining penalty constant
            AssertEqual("NEG-012", "Claustrophobic mining penalty is 10", 10, SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_MINING_PENALTY);

            // NEG-013: Ravenous hunger penalty constant
            AssertEqual("NEG-013", "Ravenous hunger penalty is 30", 30, SeraphLevelingModSystem.VANILLA_RAVENOUS_HUNGER_PENALTY);

            // NEG-014: Kind loot penalty constant
            AssertEqual("NEG-014", "Kind loot penalty is 10", 10, SeraphLevelingModSystem.VANILLA_KIND_LOOT_PENALTY);

            // NEG-015: Kind speed penalty constant
            AssertEqual("NEG-015", "Kind speed penalty is 25", 25, SeraphLevelingModSystem.VANILLA_KIND_SPEED_PENALTY);

            // NEG-016: Heavyhanded vessel penalty constant
            AssertEqual("NEG-016", "Heavyhanded vessel penalty is 10", 10, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_VESSEL_PENALTY);

            // NEG-017: Heavyhanded foraging penalty constant
            AssertEqual("NEG-017", "Heavyhanded foraging penalty is 15", 15, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_FORAGING_PENALTY);

            // NEG-018: Heavyhanded wild crop penalty constant
            AssertEqual("NEG-018", "Heavyhanded wild crop penalty is 20", 20, SeraphLevelingModSystem.VANILLA_HEAVYHANDED_WILD_CROP_PENALTY);

            // NEG-019: Claustrophobic ore penalty constant
            AssertEqual("NEG-019", "Claustrophobic ore penalty is 15", 15, SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY);

            // NEG-020: Partial cancellation simulation - Farsighted at level 10
            int farsightedRemaining = SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_FARSIGHTED_MELEE_PENALTY, 10);
            AssertEqual("NEG-020", "Farsighted at level 10 = 5% remaining", 5, farsightedRemaining);

            // NEG-021: Full cancellation simulation - Nervous at level 15
            int nervousRemaining = SeraphLevelingModSystem.CalculateRemainingPenalty(SeraphLevelingModSystem.VANILLA_NERVOUS_MELEE_PENALTY, 15);
            AssertEqual("NEG-021", "Nervous at level 15 = 0% remaining", 0, nervousRemaining);
        }

        // =========================================================================
        // DETECTION TESTS
        // =========================================================================

        private static void RunDetectionTests()
        {
            // DET-001: Clothing detection - clothes prefix
            AssertTrue("DET-001", "clothes- detected as clothing", IsClothingItemPublic("clothes-upperbody-shirt-linen"), "true", "false");

            // DET-002: Clothing detection - shirt prefix
            AssertTrue("DET-002", "shirt- detected as clothing", IsClothingItemPublic("shirt-linen"), "true", "false");

            // DET-003: Clothing detection - trousers prefix
            AssertTrue("DET-003", "trousers- detected as clothing", IsClothingItemPublic("trousers-linen"), "true", "false");

            // DET-004: Clothing detection - dress prefix
            AssertTrue("DET-004", "dress- detected as clothing", IsClothingItemPublic("dress-wool"), "true", "false");

            // DET-005: Clothing detection - hat prefix
            AssertTrue("DET-005", "hat- detected as clothing", IsClothingItemPublic("hat-straw"), "true", "false");

            // DET-006: Clothing detection - cape prefix
            AssertTrue("DET-006", "cape- detected as clothing", IsClothingItemPublic("cape-wool"), "true", "false");

            // DET-007: Clothing detection - boots prefix
            AssertTrue("DET-007", "boots- detected as clothing", IsClothingItemPublic("boots-leather"), "true", "false");

            // DET-008: Armor NOT detected as clothing
            AssertTrue("DET-008", "armor NOT detected as clothing", !IsClothingItemPublic("armor-body-plate-iron"), "false", "true");

            // DET-009: Armor detection
            AssertTrue("DET-009", "armor- detected as armor", IsArmorItemPublic("armor-body-plate-iron"), "true", "false");

            // DET-010: Clothing NOT detected as armor
            AssertTrue("DET-010", "clothes NOT detected as armor", !IsArmorItemPublic("clothes-upperbody-shirt"), "false", "true");

            // DET-011: Null handling - clothing
            AssertTrue("DET-011", "Null not detected as clothing", !IsClothingItemPublic(null), "false", "true");

            // DET-012: Null handling - armor
            AssertTrue("DET-012", "Null not detected as armor", !IsArmorItemPublic(null), "false", "true");

            // DET-013: Empty handling - clothing
            AssertTrue("DET-013", "Empty not detected as clothing", !IsClothingItemPublic(""), "false", "true");

            // DET-014: Empty handling - armor
            AssertTrue("DET-014", "Empty not detected as armor", !IsArmorItemPublic(""), "false", "true");

            // DET-015: Case insensitivity - clothing
            AssertTrue("DET-015", "CLOTHES- detected (case insensitive)", IsClothingItemPublic("CLOTHES-upperbody-shirt"), "true", "false");

            // DET-016: Case insensitivity - armor
            AssertTrue("DET-016", "ARMOR- detected (case insensitive)", IsArmorItemPublic("ARMOR-body-plate-iron"), "true", "false");
        }

        // =========================================================================
        // PERSISTENCE TESTS
        // =========================================================================

        private static void RunPersistenceTests(IServerPlayer player)
        {
            if (player?.Entity == null)
            {
                results.Add(new TestResult
                {
                    TestId = "PERS-000",
                    Description = "Player entity available",
                    Passed = false,
                    ExpectedValue = "not null",
                    ActualValue = "null"
                });
                return;
            }

            string playerUid = player.PlayerUID;
            var watchedAttrs = player.Entity.WatchedAttributes;

            // PERS-001: Mining data exists in dictionary
            bool hasMiningData = SeraphLevelingModSystem.MiningProgress.ContainsKey(playerUid);
            AssertTrue("PERS-001", "Mining data exists in dictionary", hasMiningData, "exists", "missing");

            // PERS-002: Mining WatchedAttributes matches dictionary
            if (hasMiningData)
            {
                var miningData = SeraphLevelingModSystem.MiningProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, -999);
                AssertEqual("PERS-002", "Mining level synced to WatchedAttributes", miningData.TotalCredits, watchedLevel);
            }

            // PERS-003: Melee data exists in dictionary
            bool hasMeleeData = SeraphLevelingModSystem.MeleeProgress.ContainsKey(playerUid);
            AssertTrue("PERS-003", "Melee data exists in dictionary", hasMeleeData, "exists", "missing");

            // PERS-004: Melee WatchedAttributes matches dictionary
            if (hasMeleeData)
            {
                var meleeData = SeraphLevelingModSystem.MeleeProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, -999);
                AssertEqual("PERS-004", "Melee level synced to WatchedAttributes", meleeData.TotalCredits, watchedLevel);
            }

            // PERS-005: Ranged data exists in dictionary
            bool hasRangedData = SeraphLevelingModSystem.RangedProgress.ContainsKey(playerUid);
            AssertTrue("PERS-005", "Ranged data exists in dictionary", hasRangedData, "exists", "missing");

            // PERS-006: Ranged WatchedAttributes matches dictionary
            if (hasRangedData)
            {
                var rangedData = SeraphLevelingModSystem.RangedProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, -999);
                AssertEqual("PERS-006", "Ranged level synced to WatchedAttributes", rangedData.TotalCredits, watchedLevel);
            }

            // PERS-007: Walking data exists in dictionary
            bool hasWalkingData = SeraphLevelingModSystem.WalkingProgress.ContainsKey(playerUid);
            AssertTrue("PERS-007", "Walking data exists in dictionary", hasWalkingData, "exists", "missing");

            // PERS-008: Walking WatchedAttributes matches dictionary
            if (hasWalkingData)
            {
                var walkingData = SeraphLevelingModSystem.WalkingProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, -999);
                AssertEqual("PERS-008", "Walking level synced to WatchedAttributes", walkingData.TotalCredits, watchedLevel);
            }

            // PERS-009: Hunger data exists in dictionary
            bool hasHungerData = SeraphLevelingModSystem.HungerProgress.ContainsKey(playerUid);
            AssertTrue("PERS-009", "Hunger data exists in dictionary", hasHungerData, "exists", "missing");

            // PERS-010: Hunger WatchedAttributes matches dictionary
            if (hasHungerData)
            {
                var hungerData = SeraphLevelingModSystem.HungerProgress[playerUid];
                int watchedLevel = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, -999);
                AssertEqual("PERS-010", "Hunger level synced to WatchedAttributes", hungerData.TotalCredits, watchedLevel);
            }

            // PERS-011: Armor data exists in dictionary
            bool hasArmorData = SeraphLevelingModSystem.ArmorProgress.ContainsKey(playerUid);
            AssertTrue("PERS-011", "Armor data exists in dictionary", hasArmorData, "exists", "missing");

            // PERS-012: Armor durability WatchedAttributes matches dictionary
            if (hasArmorData)
            {
                var armorData = SeraphLevelingModSystem.ArmorProgress[playerUid];
                int watchedDurability = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, -999);
                AssertEqual("PERS-012", "Armor durability synced to WatchedAttributes", armorData.TotalDurabilityCredits, watchedDurability);

                int watchedWalkSpeed = watchedAttrs.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, -999);
                AssertEqual("PERS-013", "Armor walk speed synced to WatchedAttributes", armorData.TotalWalkSpeedCredits, watchedWalkSpeed);
            }

            // PERS-014: Mining data structure integrity
            if (hasMiningData)
            {
                var miningData = SeraphLevelingModSystem.MiningProgress[playerUid];
                bool creditsValid = miningData.TotalCredits >= 0;
                bool pickaxeProgressValid = miningData.PickaxeProgress != null;
                AssertTrue("PERS-014", "Mining data structure valid", creditsValid && pickaxeProgressValid, "valid", "corrupted");
            }

            // PERS-015: Melee data structure integrity
            if (hasMeleeData)
            {
                var meleeData = SeraphLevelingModSystem.MeleeProgress[playerUid];
                bool creditsValid = meleeData.TotalCredits >= 0;
                bool weaponProgressValid = meleeData.WeaponProgress != null;
                AssertTrue("PERS-015", "Melee data structure valid", creditsValid && weaponProgressValid, "valid", "corrupted");
            }

            // PERS-016: Ranged data structure integrity
            if (hasRangedData)
            {
                var rangedData = SeraphLevelingModSystem.RangedProgress[playerUid];
                bool creditsValid = rangedData.TotalCredits >= 0;
                bool weaponProgressValid = rangedData.WeaponProgress != null;
                AssertTrue("PERS-016", "Ranged data structure valid", creditsValid && weaponProgressValid, "valid", "corrupted");
            }

            // PERS-017: Armor data structure integrity
            if (hasArmorData)
            {
                var armorData = SeraphLevelingModSystem.ArmorProgress[playerUid];
                bool durabilityValid = armorData.TotalDurabilityCredits >= 0;
                bool walkSpeedValid = armorData.TotalWalkSpeedCredits >= 0;
                bool armorPiecesValid = armorData.ArmorProgress != null;
                AssertTrue("PERS-017", "Armor data structure valid", durabilityValid && walkSpeedValid && armorPiecesValid, "valid", "corrupted");
            }
        }

        // Helper methods that mirror the private methods in the mod
        private static bool IsClothingItemPublic(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();

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

        private static bool IsArmorItemPublic(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();
            return lowerCode.Contains("armor-");
        }
    }
}
