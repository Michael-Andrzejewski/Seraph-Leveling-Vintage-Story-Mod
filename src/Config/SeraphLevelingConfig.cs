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
    /// Configuration class for SeraphLeveling mod.
    /// Edit ModConfig/SeraphLeveling.json to change these values.
    /// </summary>
    public class SeraphLevelingConfig
    {
        /// <summary>
        /// Bumped when the mod needs to know the file has been through a migration.
        /// A file written before 1.19.0 has no such field and reads back as 0, which
        /// tells LoadConfig to fold the old world-save settings in exactly once.
        /// </summary>
        public int ConfigVersion { get; set; } = 0;

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
        /// <summary>
        /// Hunger progress counts while saturation is at or above this percent of the
        /// maximum (default 100 = only at exactly full). Mods that remove the saturation
        /// pause make "exactly full" unreachable; 95 or 90 keeps the skill trainable there.
        /// </summary>
        public int HungerSaturationThresholdPercent { get; set; } = 100;

        // Armor progression
        public int ArmorBaseSecondsPerIncrement { get; set; } = 2880;
        public int ArmorTimeIncrementStep { get; set; } = 2880;
        public int ArmorBaseDamageBlockedPerIncrement { get; set; } = 100;
        public int ArmorDamageIncrementStep { get; set; } = 100;
        public int ArmorBaseRepairsPerIncrement { get; set; } = 1;
        public int ArmorRepairIncrementStep { get; set; } = 1;
        public int ArmorMaxDurabilityPercent { get; set; } = 50;
        public int ArmorMaxWalkSpeedPercent { get; set; } = 50;

        // First-equip bonus configuration (durability)
        public int ArmorFirstEquipLightDurability { get; set; } = 1;
        public int ArmorFirstEquipChainDurability { get; set; } = 1;
        public int ArmorFirstEquipBrigandineDurability { get; set; } = 2;
        public int ArmorFirstEquipScaleDurability { get; set; } = 3;
        public int ArmorFirstEquipPlateDurability { get; set; } = 3;

        // First-equip bonus configuration (walk speed penalty reduction)
        public int ArmorFirstEquipLightWalkSpeed { get; set; } = 1;
        public int ArmorFirstEquipChainWalkSpeed { get; set; } = 1;
        public int ArmorFirstEquipBrigandineWalkSpeed { get; set; } = 2;
        public int ArmorFirstEquipScaleWalkSpeed { get; set; } = 3;
        public int ArmorFirstEquipPlateWalkSpeed { get; set; } = 3;

        // Armor hunger reduction (optional feature, disabled by default)
        public bool EnableArmorHungerReduction { get; set; } = false;
        public int ArmorMaxHungerReductionPercent { get; set; } = 50;

        // Armor healing effectiveness (optional feature, disabled by default)
        public bool EnableArmorHealingBonus { get; set; } = false;
        public int ArmorMaxHealingPercent { get; set; } = 25;

        // Clothier progression
        public int ClothierRequiredUniqueClothes { get; set; } = 20;
        /// <summary>
        /// List of specific clothing item codes to exclude from Clothier progression.
        /// Default excludes starting class outfits only (not Nadiyan or other variants).
        /// Uses substring matching - items containing any blacklisted string are excluded.
        /// </summary>
        public string[] ClothierBlacklistedItems { get; set; } = new string[]
        {
            // Hunter starting outfit (8 items)
            "clothes-upperbody-hunter-shirt",
            "clothes-upperbodyover-hunter-coat",
            "clothes-shoulder-hunter-poncho",
            "clothes-lowerbody-hunter-leggings",
            "clothes-foot-hunter-boots",
            "clothes-hand-hunter-gloves",
            "clothes-head-hunter-hood",
            "clothes-face-hunter-mask",
            // Tailor starting outfit (5 items)
            "clothes-upperbody-tailor-blouse",
            "clothes-foot-tailor-shoes",
            "clothes-hand-tailor-gloves",
            "clothes-waist-tailor-belt",
            "clothes-shoulder-tailor-jacket",
            // Malefactor starting outfit (5 items)
            "clothes-shoulder-malefactor-cloak",
            "clothes-foot-malefactor-boots",
            "clothes-hand-malefactor-gloves",
            "clothes-lowerbody-malefactor-trousers",
            "clothes-neck-malefactor-pendant",
            // Blackguard starting outfit (4 items)
            "clothes-foot-blackguard-shoes",
            "clothes-lowerbody-blackguard-leggings",
            "clothes-upperbody-blackguard-shirt",
            "clothes-waist-blackguard-belt",
            // Clockmaker starting outfit (4 items)
            "clothes-hand-clockmaker-wristguard",
            "clothes-foot-clockmaker-shoes",
            "clothes-upperbody-clockmaker-shirt",
            "clothes-shoulder-clockmaker-apron",
            // Commoner starting outfit (5 items)
            "clothes-upperbody-commoner-shirt",
            "clothes-upperbodyover-commoner-coat",
            "clothes-lowerbody-commoner-trousers",
            "clothes-foot-commoner-boots",
            "clothes-hand-commoner-gloves"
        };

        // Mender progression
        public int MenderBaseRepairsPerIncrement { get; set; } = 5;
        public int MenderIncrementStep { get; set; } = 1;
        public int MenderMaxPercent { get; set; } = 25;

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
        public int HardyHealthMiningThreshold { get; set; } = 10;
        public int HardyHealthArmorDurabilityThreshold { get; set; } = 10;
        public int HardyHealthBonus { get; set; } = 5;

        // Auto-save settings
        /// <summary>
        /// No longer used (kept so existing config files still load). Progress
        /// is written to disk by the game's own world save, which calls the mod's
        /// save hook first; a separate mod timer only rewrote the in-memory copy.
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

        /// <summary>
        /// Per-proficiency overrides for the first-credit damage requirement.
        /// Keyed by stat name, for example "bowsProficiency". Anything absent
        /// falls back to COProficiencyBaseDamagePerIncrement.
        /// </summary>
        public Dictionary<string, int> COProficiencyBaseOverrides { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Per-proficiency overrides for the per-credit damage step.
        /// Keyed by stat name, for example "bowsProficiency". Anything absent
        /// falls back to COProficiencyIncrementStep.
        /// </summary>
        public Dictionary<string, int> COProficiencyIncrementOverrides { get; set; } = new Dictionary<string, int>();

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
        public float COPoleaxeProficiencyMax { get; set; } = 0.3f;
        public float COAxesProficiencyMax { get; set; } = 0.3f;
        public float COQuarterstaffProficiencyMax { get; set; } = 0.3f;

        /// <summary>
        /// Max Steady Aim bonus (earned alongside ranged proficiencies).
        /// </summary>
        public float COSteadyAimMax { get; set; } = 0.5f;

        // =========================================================================
        // SKILL DECAY SETTINGS
        // =========================================================================

        /// <summary>
        /// Enable skill decay when players don't use skills for extended periods.
        /// Disabled by default.
        /// </summary>
        public bool EnableSkillDecay { get; set; } = false;

        /// <summary>
        /// Grace period in in-game days before decay starts.
        /// During this period after last activity, no decay occurs.
        /// </summary>
        public double DecayGracePeriodDays { get; set; } = 1.0;

        /// <summary>
        /// Base points lost per day past grace period.
        /// Decay increases triangularly: Day 1 = base, Day 2 = 2*base, Day 3 = 3*base, etc.
        /// </summary>
        public int DecayBasePointsPerDay { get; set; } = 10;

        /// <summary>
        /// Maximum total decay points applied per day.
        /// Prevents losing all progress after long inactivity periods.
        /// </summary>
        public int DecayMaxPointsPerDay { get; set; } = 100;

        /// <summary>
        /// Skills exempt from decay. Valid values: mining, melee, ranged, walking, hunger, armor,
        /// mender, pilferer, resourceful, forager, furtive, precise, coproficiency
        /// </summary>
        public string[] DecayExemptSkills { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Per-skill grace period overrides (in-game days). Skills not listed use DecayGracePeriodDays.
        /// </summary>
        public Dictionary<string, double> DecayGracePeriodOverrides { get; set; } = new Dictionary<string, double>
        {
            { "walking", 2.0 }, { "hunger", 2.0 }, { "furtive", 2.0 }, { "armor", 2.0 },
            { "mender", 3.0 }, { "resourceful", 3.0 },
            { "forager", 5.0 }, { "pilferer", 5.0 }, { "precise", 5.0 }
        };

        /// <summary>
        /// Per-skill base decay points per day overrides. Skills not listed use DecayBasePointsPerDay.
        /// </summary>
        public Dictionary<string, int> DecayBasePointsOverrides { get; set; } = new Dictionary<string, int>
        {
            { "walking", 5 }, { "hunger", 5 }, { "furtive", 5 }, { "armor", 5 },
            { "mender", 3 }, { "resourceful", 3 },
            { "forager", 2 }, { "pilferer", 2 }, { "precise", 2 }
        };

        /// <summary>
        /// Per-skill max decay points per day overrides. Skills not listed use DecayMaxPointsPerDay.
        /// </summary>
        public Dictionary<string, int> DecayMaxPointsOverrides { get; set; } = new Dictionary<string, int>
        {
            { "walking", 50 }, { "hunger", 50 }, { "furtive", 50 }, { "armor", 50 },
            { "mender", 30 }, { "resourceful", 30 },
            { "forager", 20 }, { "pilferer", 20 }, { "precise", 20 }
        };

        // =========================================================================
        // SLEEP BUFF SETTINGS
        // =========================================================================

        /// <summary>
        /// Enable XP multiplier buff after sleeping.
        /// Disabled by default.
        /// </summary>
        public bool EnableSleepBuff { get; set; } = false;

        /// <summary>
        /// XP multiplier when sleeping in a linen or old bed.
        /// </summary>
        public float SleepBuffLinenBedMultiplier { get; set; } = 2.0f;

        /// <summary>
        /// XP multiplier when sleeping in a hay bed.
        /// </summary>
        public float SleepBuffHayBedMultiplier { get; set; } = 1.5f;

        /// <summary>
        /// Duration of the sleep buff in in-game days.
        /// </summary>
        public double SleepBuffDurationDays { get; set; } = 1.0;

        // =========================================================================
        // DEATH PENALTY SETTINGS
        // =========================================================================

        /// <summary>
        /// Enable skill progression loss on player death.
        /// Penalty = BaseIncrement * DeathPenaltyFraction * sqrt(CurrentLevel) subcredits drained.
        /// Disabled by default.
        /// </summary>
        public bool EnableDeathPenalty { get; set; } = false;

        /// <summary>
        /// Fraction of the base increment used to compute death penalty.
        /// E.g. 0.5 means half of the first level-up cost times sqrt(credits).
        /// </summary>
        public double DeathPenaltyFraction { get; set; } = 0.5;

        /// <summary>
        /// When true (and EnableDeathPenalty is true), dying wipes ALL trait progression
        /// back to zero instead of applying the fractional penalty. Skills listed in
        /// DeathPenaltyExemptSkills or DisabledSkills keep their progress. Off by default.
        /// </summary>
        public bool DeathPenaltyFullReset { get; set; } = false;

        /// <summary>
        /// Skills exempt from death penalty. Use lowercase skill names (e.g. "mining", "melee", "armor").
        /// </summary>
        public string[] DeathPenaltyExemptSkills { get; set; } = Array.Empty<string>();

        // =========================================================================
        // NOTIFICATION SETTINGS
        // =========================================================================

        /// <summary>
        /// Enable chat messages when leveling up a trait.
        /// </summary>
        public bool EnableLevelUpMessages { get; set; } = true;

        /// <summary>
        /// Enable sound effect when leveling up a trait.
        /// </summary>
        public bool EnableLevelUpSound { get; set; } = true;

        /// <summary>
        /// Sound asset to play on level-up. Must be a valid Vintage Story sound path.
        /// </summary>
        public string LevelUpSoundName { get; set; } = "game:sounds/effect/receptionbell";

        /// <summary>
        /// How loud the level-up ding is, on a 0.0 to 1.0 scale. The default is 0.25.
        /// The scale is exponential rather than linear, so the values do not feel evenly spaced.
        /// Reference points: 1.0 is not particularly loud, 0.5 is hard to distinguish from 1.0,
        /// 0.25 is a comfortable medium, 0.05 sounds roughly half as loud as 0.25, and 0.01 is still audible.
        /// Set 0.0 to silence the ding without disabling the chat message.
        /// </summary>
        public float LevelUpSoundVolume { get; set; } = 0.25f;

        // =========================================================================
        // EXPERIMENTAL FEATURES (ported from Seraph Leveling Experimental)
        // Everything here is OFF by default. Flip the *Enabled switches to try them.
        // =========================================================================

        /// <summary>Enable the Temporal Resistance trait: time spent at low temporal stability slows your stability drain permanently. Experimental, off by default.</summary>
        public bool TemporalResistanceEnabled { get; set; } = false;

        /// <summary>Maximum Temporal Resistance in percent. At 75 the drain is 75% slower.</summary>
        public int TemporalResistanceMaxPercent { get; set; } = 75;

        /// <summary>
        /// Whether Temporal Resistance applies during temporal storms. Default false, so during a
        /// storm your stability drops at the normal storm rate regardless of resistance.
        /// </summary>
        public bool TemporalResistanceWorksDuringStorms { get; set; } = false;

        /// <summary>
        /// Stability value (0.0 to 1.0) below which the temporal traits gain progress.
        /// Shared by both Resistance and Recharge. Default 0.5.
        /// </summary>
        public float TemporalLowStabilityThreshold { get; set; } = 0.5f;

        /// <summary>Enable the Temporal Recharge trait: speeds temporal stability recovery; also leveled by the knife + temporal gear gesture. Experimental, off by default.</summary>
        public bool TemporalRechargeEnabled { get; set; } = false;

        /// <summary>Maximum Temporal Recharge in percent. At 200 recovery is 3x as fast as normal.</summary>
        public int TemporalRechargeMaxPercent { get; set; } = 200;

        /// <summary>Temporal Recharge percent gained per knife + temporal gear gesture.</summary>
        public int TemporalRechargeGearTrickPercent { get; set; } = 5;

        /// <summary>
        /// Temporal Recharge percent at or above which the player becomes immune to low-grade
        /// (non-storm) temporal decay. Default 200 (only at max).
        /// </summary>
        public int TemporalRechargePassiveImmunityAtPercent { get; set; } = 200;

        /// <summary>Enable shortening bow draw time based on the Ranged trait level. Experimental, off by default.</summary>
        public bool RangedDrawSpeedEnabled { get; set; } = false;

        /// <summary>
        /// Maximum bow draw-time reduction in percent, reached at RangedDrawSpeedAtLevel.
        /// At 50 the draw is 2x faster (half the draw time). Internally capped at 99%.
        /// </summary>
        public int RangedDrawSpeedMaxReductionPercent { get; set; } = 50;

        /// <summary>
        /// Ranged trait level (raw credits) at which bow draw reduction reaches its maximum.
        /// Reduction scales linearly from 0 at level 0 to the max at this level. Default 50.
        /// Also used as the level at which bow aim-assist accuracy reaches its maximum.
        /// </summary>
        public int RangedDrawSpeedAtLevel { get; set; } = 50;

        /// <summary>
        /// Enable bow aim-assist: a high Ranged level converges accuracy instantly so you can
        /// spam fire without waiting for the reticle to settle. Experimental, off by default.
        /// </summary>
        public bool RangedAimAssistEnabled { get; set; } = false;

        /// <summary>
        /// Maximum bow accuracy floor in percent, reached at RangedDrawSpeedAtLevel. At 99 a maxed
        /// Ranged player fires at near-perfect accuracy regardless of how long the bow was drawn.
        /// </summary>
        public int RangedAccuracyMaxPercent { get; set; } = 99;

        /// <summary>Enable speeding up melee swings based on the Melee trait level. Experimental, off by default.</summary>
        public bool MeleeAttackSpeedEnabled { get; set; } = false;

        /// <summary>
        /// Maximum melee swing speed-up, expressed as a reduction percent reached at
        /// MeleeAttackSpeedAtLevel. At 50 the swing plays 2x faster (duration halved). Capped at 90%.
        /// </summary>
        public int MeleeAttackSpeedMaxReductionPercent { get; set; } = 50;

        /// <summary>
        /// Melee trait level (raw credits) at which the swing speed-up reaches its maximum.
        /// Scales linearly from 1x at level 0 to the max at this level. Default 50.
        /// </summary>
        public int MeleeAttackSpeedAtLevel { get; set; } = 50;

        // =========================================================================
        // GLOBAL XP RATE
        // =========================================================================

        /// <summary>
        /// Global rate multiplier applied to ALL trait progression. 1.0 is normal
        /// speed, 0.5 is half speed, 2.0 is double. Stacks multiplicatively with the
        /// sleep buff. Applies to every skill without touching the per-skill
        /// base/increment values.
        /// </summary>
        public float GlobalXPRateMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// When true, a class's starting traits shift that skill's endgame ceiling
        /// instead of every class converging to the same total. Positive starting
        /// traits (Hardy, Soldier, Focused, Fleetfooted, ...) stack on top of the
        /// earned cap, so those classes end the game higher. Classes with negative
        /// starting traits no longer get extra credits to fully catch up: the trait
        /// can still be leveled away, but the levels spent doing it count against
        /// the cap, so those classes end lower. Off by default (classic behavior:
        /// everyone converges to the same endgame totals).
        /// </summary>
        public bool EnableClassCapOffsets { get; set; } = false;

        /// <summary>
        /// When false, negative class traits (Weak, Claustrophobic, Farsighted, Nervous,
        /// Nearsighted, Frail, Civil, Kind, Heavyhanded, Ravenous, and the Combat Overhaul
        /// negatives) are never cancelled or removed by leveling: bonuses stack on top of
        /// them from level 0 and the penalty stays for the life of the character. Default
        /// true (classic behavior: leveling pays the penalty off first, then adds bonus).
        /// </summary>
        public bool CancelNegativeTraits { get; set; } = true;

        // =========================================================================
        // DEBUG SETTINGS
        // =========================================================================

        /// <summary>
        /// Enable verbose debug logging for damage tracking and other systems.
        /// WARNING: This can spam server logs with hundreds of messages per minute!
        /// Only enable for testing/debugging purposes.
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Enable verbose decay logging. When enabled, players receive detailed messages
        /// showing per-tool drain amounts during daily decay and death penalty.
        /// </summary>
        public bool VerboseDecayLogging { get; set; } = false;
    }
}
