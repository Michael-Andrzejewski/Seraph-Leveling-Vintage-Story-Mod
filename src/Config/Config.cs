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

        private const string CONFIG_SAVE_KEY = "sitConfig";
        private const string CONFIG_FILE_NAME = "SeraphLeveling.json";

        /// <summary>Version stamped into the config file. 1 means the world-save blob has been folded in.</summary>
        private const int CURRENT_CONFIG_VERSION = 1;

        /// <summary>ConfigVersion read from the file this run. Zero for files written before 1.19.0.</summary>
        private static int LoadedConfigVersion = 0;

        // Flag to indicate pending config save
        private static volatile bool pendingConfigSave = false;

        // Auto-save configuration
        public static int AutoSaveIntervalSeconds = 300;  // Default 5 minutes
        private static long autoSaveTimerId = 0;

        // Debug logging (disabled by default to avoid log spam)
        public static bool DebugLoggingEnabled = false;

        // Notification settings
        public static float GlobalXPRateMultiplier = 1.0f;
        public static bool EnableClassCapOffsets = false;
        public static bool CancelNegativeTraits = true;

        public static bool EnableLevelUpMessages = true;
        public static bool EnableLevelUpSound = true;
        public static string LevelUpSoundName = "game:sounds/effect/receptionbell";
        public static float LevelUpSoundVolume = 0.25f;

        /// <summary>
        /// Writes the current level-up sound volume back to the JSON config file so it survives a restart.
        /// </summary>
        private void SaveLevelUpSoundVolumeToConfig()
        {
            SaveConfigFile();
        }

        /// <summary>
        /// The mod used to run its own auto-save tick here. It is gone:
        /// SaveGame.StoreData only fills the game's in-memory ModData, and the
        /// game's own autosave (serverconfig, default 5 min) and shutdown call
        /// OnGameWorldSave just before they write to disk, so the mod timer
        /// re-serialized everything on the main thread for no added safety.
        /// Still called at startup and on reloadconfig / resetconfig, so it
        /// also removes a timer left by an older build.
        /// </summary>
        private void RestartAutoSaveTimer(ICoreServerAPI api)
        {
            if (autoSaveTimerId != 0)
            {
                try { api.Event.UnregisterGameTickListener(autoSaveTimerId); } catch { }
                autoSaveTimerId = 0;
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
                    // A brand new install has no old world settings to fold in, so
                    // stamp it as already migrated.
                    config = new SeraphLevelingConfig { ConfigVersion = CURRENT_CONFIG_VERSION };
                    api.StoreModConfig(config, CONFIG_FILE_NAME);
                    api.Logger.Notification("[SeraphLeveling] Created default config file: ModConfig/" + CONFIG_FILE_NAME);
                }

                LoadedConfigVersion = config.ConfigVersion;

                // Progression bases and increment steps must be at least 1. Zero or
                // negative values freeze already-leveled players at their current
                // per-level cost, break credit derivation ((size - base) / step) in
                // /trait set and the death penalty, and a zero base can hang the
                // absolute-position math. Clamp with a warning instead of obeying.
                int AtLeastOne(int value, string name)
                {
                    if (value >= 1) return value;
                    api.Logger.Warning($"[SeraphLeveling] Config value {name} must be at least 1 but is {value}. Using 1 instead. To make leveling flat, set the increment step to 1, not 0.");
                    return 1;
                }

                // Apply config values to static variables
                BaseBlocksPerIncrement = AtLeastOne(config.MiningBaseBlocksPerIncrement, "MiningBaseBlocksPerIncrement");
                IncrementStep = AtLeastOne(config.MiningIncrementStep, "MiningIncrementStep");
                MaxMiningSpeedPercent = config.MiningMaxPercent;
                OreMultiplier = config.MiningOreMultiplier;

                BaseDamagePerIncrement = AtLeastOne(config.MeleeBaseDamagePerIncrement, "MeleeBaseDamagePerIncrement");
                MeleeIncrementStep = AtLeastOne(config.MeleeIncrementStep, "MeleeIncrementStep");
                MaxMeleeDamagePercent = config.MeleeMaxPercent;

                BaseRangedDamagePerIncrement = AtLeastOne(config.RangedBaseDamagePerIncrement, "RangedBaseDamagePerIncrement");
                RangedIncrementStep = AtLeastOne(config.RangedIncrementStep, "RangedIncrementStep");
                MaxRangedDamagePercent = config.RangedMaxDamagePercent;
                MaxRangedAccuracyPercent = config.RangedMaxAccuracyPercent;
                MaxRangedDistancePercent = config.RangedMaxDistancePercent;

                BaseBlocksWalkedPerIncrement = AtLeastOne(config.WalkingBaseBlocksPerIncrement, "WalkingBaseBlocksPerIncrement");
                WalkingIncrementStep = AtLeastOne(config.WalkingIncrementStep, "WalkingIncrementStep");
                MaxWalkingSpeedPercent = config.WalkingMaxPercent;

                BaseSecondsPerIncrement = AtLeastOne(config.HungerBaseSecondsPerIncrement, "HungerBaseSecondsPerIncrement");
                HungerIncrementStep = AtLeastOne(config.HungerIncrementStep, "HungerIncrementStep");
                MaxHungerReductionPercent = config.HungerMaxReductionPercent;
                HungerSaturationThresholdPercent = Math.Clamp(config.HungerSaturationThresholdPercent, 1, 100);

                BaseSecondsInArmorPerIncrement = AtLeastOne(config.ArmorBaseSecondsPerIncrement, "ArmorBaseSecondsPerIncrement");
                ArmorTimeIncrementStep = AtLeastOne(config.ArmorTimeIncrementStep, "ArmorTimeIncrementStep");
                BaseDamageBlockedPerIncrement = AtLeastOne(config.ArmorBaseDamageBlockedPerIncrement, "ArmorBaseDamageBlockedPerIncrement");
                ArmorDamageIncrementStep = AtLeastOne(config.ArmorDamageIncrementStep, "ArmorDamageIncrementStep");
                BaseRepairsPerIncrement = AtLeastOne(config.ArmorBaseRepairsPerIncrement, "ArmorBaseRepairsPerIncrement");
                ArmorRepairIncrementStep = AtLeastOne(config.ArmorRepairIncrementStep, "ArmorRepairIncrementStep");
                MaxArmorDurabilityPercent = config.ArmorMaxDurabilityPercent;
                MaxArmorWalkSpeedPercent = config.ArmorMaxWalkSpeedPercent;

                // First-equip bonus configuration
                FirstEquipLightBonus = config.ArmorFirstEquipLightDurability;
                FirstEquipChainBonus = config.ArmorFirstEquipChainDurability;
                FirstEquipBrigandineBonus = config.ArmorFirstEquipBrigandineDurability;
                FirstEquipScaleBonus = config.ArmorFirstEquipScaleDurability;
                FirstEquipPlateBonus = config.ArmorFirstEquipPlateDurability;

                FirstEquipWalkSpeedLightBonus = config.ArmorFirstEquipLightWalkSpeed;
                FirstEquipWalkSpeedChainBonus = config.ArmorFirstEquipChainWalkSpeed;
                FirstEquipWalkSpeedBrigandineBonus = config.ArmorFirstEquipBrigandineWalkSpeed;
                FirstEquipWalkSpeedScaleBonus = config.ArmorFirstEquipScaleWalkSpeed;
                FirstEquipWalkSpeedPlateBonus = config.ArmorFirstEquipPlateWalkSpeed;

                // Optional armor features
                EnableArmorHungerReduction = config.EnableArmorHungerReduction;
                MaxArmorHungerReductionPercent = config.ArmorMaxHungerReductionPercent;
                EnableArmorHealingBonus = config.EnableArmorHealingBonus;
                MaxArmorHealingPercent = config.ArmorMaxHealingPercent;

                ClothierRequiredUniqueClothes = config.ClothierRequiredUniqueClothes;
                if (config.ClothierBlacklistedItems != null)
                {
                    ClothierBlacklistedItems = config.ClothierBlacklistedItems;
                }

                BaseMenderRepairsPerIncrement = AtLeastOne(config.MenderBaseRepairsPerIncrement, "MenderBaseRepairsPerIncrement");
                MenderIncrementStep = AtLeastOne(config.MenderIncrementStep, "MenderIncrementStep");
                MaxMenderPercent = config.MenderMaxPercent;

                BasePilfererPointsPerIncrement = AtLeastOne(config.PilfererBasePointsPerIncrement, "PilfererBasePointsPerIncrement");
                PilfererIncrementStep = AtLeastOne(config.PilfererIncrementStep, "PilfererIncrementStep");
                MaxPilfererPercent = config.PilfererMaxPercent;

                BaseResourcefulAnimalsPerIncrement = AtLeastOne(config.ResourcefulBaseAnimalsPerIncrement, "ResourcefulBaseAnimalsPerIncrement");
                ResourcefulIncrementStep = AtLeastOne(config.ResourcefulIncrementStep, "ResourcefulIncrementStep");
                MaxResourcefulLootPercent = config.ResourcefulMaxLootPercent;
                MaxResourcefulSpeedPercent = config.ResourcefulMaxSpeedPercent;

                BaseForagerCropsPerIncrement = AtLeastOne(config.ForagerBaseCropsPerIncrement, "ForagerBaseCropsPerIncrement");
                ForagerIncrementStep = AtLeastOne(config.ForagerIncrementStep, "ForagerIncrementStep");
                MaxForagerLootPercent = config.ForagerMaxLootPercent;
                MaxForagerWildCropPercent = config.ForagerMaxWildCropPercent;

                BaseFurtiveSneakBlocksPerIncrement = AtLeastOne(config.FurtiveBaseSneakBlocksPerIncrement, "FurtiveBaseSneakBlocksPerIncrement");
                FurtiveIncrementStep = AtLeastOne(config.FurtiveIncrementStep, "FurtiveIncrementStep");
                MaxFurtivePercent = config.FurtiveMaxPercent;

                BasePreciseDamagePerIncrement = AtLeastOne(config.PreciseBaseDamagePerIncrement, "PreciseBaseDamagePerIncrement");
                PreciseIncrementStep = AtLeastOne(config.PreciseIncrementStep, "PreciseIncrementStep");
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
                COBaseDamagePerIncrement = AtLeastOne(config.COProficiencyBaseDamagePerIncrement, "COProficiencyBaseDamagePerIncrement");
                COIncrementStep = AtLeastOne(config.COProficiencyIncrementStep, "COProficiencyIncrementStep");
                COProficiencyBaseOverrides = config.COProficiencyBaseOverrides ?? new Dictionary<string, int>();
                COProficiencyIncrementOverrides = config.COProficiencyIncrementOverrides ?? new Dictionary<string, int>();
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
                COPoleaxeProficiencyMax = config.COPoleaxeProficiencyMax;
                COAxesProficiencyMax = config.COAxesProficiencyMax;
                COQuarterstaffProficiencyMax = config.COQuarterstaffProficiencyMax;
                COSteadyAimMax = config.COSteadyAimMax;

                // Skill decay settings
                EnableSkillDecay = config.EnableSkillDecay;
                DecayGracePeriodDays = config.DecayGracePeriodDays;
                DecayBasePointsPerDay = config.DecayBasePointsPerDay;
                DecayMaxPointsPerDay = config.DecayMaxPointsPerDay;
                DecayExemptSkills.Clear();
                if (config.DecayExemptSkills != null)
                {
                    foreach (var skill in config.DecayExemptSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DecayExemptSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                }

                // Load per-skill decay override dictionaries
                DecayGracePeriodOverrides = config.DecayGracePeriodOverrides ?? new Dictionary<string, double>();
                DecayBasePointsOverrides = config.DecayBasePointsOverrides ?? new Dictionary<string, int>();
                DecayMaxPointsOverrides = config.DecayMaxPointsOverrides ?? new Dictionary<string, int>();

                // Sleep buff settings
                EnableSleepBuff = config.EnableSleepBuff;
                SleepBuffLinenBedMultiplier = config.SleepBuffLinenBedMultiplier;
                SleepBuffHayBedMultiplier = config.SleepBuffHayBedMultiplier;
                SleepBuffDurationDays = config.SleepBuffDurationDays;

                // Death penalty settings
                EnableDeathPenalty = config.EnableDeathPenalty;
                DeathPenaltyFraction = config.DeathPenaltyFraction;
                DeathPenaltyFullReset = config.DeathPenaltyFullReset;
                DeathPenaltyExemptSkills.Clear();
                if (config.DeathPenaltyExemptSkills != null)
                {
                    foreach (var skill in config.DeathPenaltyExemptSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DeathPenaltyExemptSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                }

                // Notification settings
                EnableLevelUpMessages = config.EnableLevelUpMessages;
                EnableLevelUpSound = config.EnableLevelUpSound;
                LevelUpSoundName = config.LevelUpSoundName;
                LevelUpSoundVolume = Math.Clamp(config.LevelUpSoundVolume, 0f, 1f);

                // Experimental features (all off by default)
                TemporalResistanceEnabled = config.TemporalResistanceEnabled;
                TemporalResistanceMaxPercent = config.TemporalResistanceMaxPercent;
                TemporalResistanceWorksDuringStorms = config.TemporalResistanceWorksDuringStorms;
                TemporalLowStabilityThreshold = Math.Clamp(config.TemporalLowStabilityThreshold, 0f, 1f);
                TemporalRechargeEnabled = config.TemporalRechargeEnabled;
                TemporalRechargeMaxPercent = config.TemporalRechargeMaxPercent;
                TemporalRechargeGearTrickPercent = config.TemporalRechargeGearTrickPercent;
                TemporalRechargePassiveImmunityAtPercent = config.TemporalRechargePassiveImmunityAtPercent;
                RangedDrawSpeedEnabled = config.RangedDrawSpeedEnabled;
                RangedDrawSpeedMaxReductionPercent = config.RangedDrawSpeedMaxReductionPercent;
                RangedDrawSpeedAtLevel = config.RangedDrawSpeedAtLevel;
                RangedAimAssistEnabled = config.RangedAimAssistEnabled;
                RangedAccuracyMaxPercent = config.RangedAccuracyMaxPercent;
                MeleeAttackSpeedEnabled = config.MeleeAttackSpeedEnabled;
                MeleeAttackSpeedMaxReductionPercent = config.MeleeAttackSpeedMaxReductionPercent;
                MeleeAttackSpeedAtLevel = config.MeleeAttackSpeedAtLevel;
                {
                    var enabledExperiments = new List<string>();
                    if (TemporalResistanceEnabled) enabledExperiments.Add("TemporalResistance");
                    if (TemporalRechargeEnabled) enabledExperiments.Add("TemporalRecharge");
                    if (RangedDrawSpeedEnabled) enabledExperiments.Add("RangedDrawSpeed");
                    if (RangedAimAssistEnabled) enabledExperiments.Add("RangedAimAssist");
                    if (MeleeAttackSpeedEnabled) enabledExperiments.Add("MeleeAttackSpeed");
                    if (enabledExperiments.Count > 0)
                    {
                        api.Logger.Notification($"[SeraphLeveling] Experimental features enabled: {string.Join(", ", enabledExperiments)}");
                    }
                }

                EnableClassCapOffsets = config.EnableClassCapOffsets;
                CancelNegativeTraits = config.CancelNegativeTraits;
                if (EnableClassCapOffsets)
                {
                    api.Logger.Notification("[SeraphLeveling] Class cap offsets ENABLED: starting class traits shift each skill's endgame ceiling.");
                }

                GlobalXPRateMultiplier = config.GlobalXPRateMultiplier;
                if (GlobalXPRateMultiplier < 0f)
                {
                    api.Logger.Warning($"[SeraphLeveling] GlobalXPRateMultiplier cannot be negative (got {GlobalXPRateMultiplier}). Using 1.0.");
                    GlobalXPRateMultiplier = 1.0f;
                }
                if (GlobalXPRateMultiplier != 1.0f)
                {
                    api.Logger.Notification($"[SeraphLeveling] Global XP rate multiplier: {GlobalXPRateMultiplier}x");
                }

                // Debug settings
                DebugLoggingEnabled = config.EnableDebugLogging;
                VerboseDecayLogging = config.VerboseDecayLogging;
                if (DebugLoggingEnabled)
                {
                    api.Logger.Warning("[SeraphLeveling] Debug logging is ENABLED - this can spam server logs!");
                }

                if (EnableSkillDecay)
                {
                    api.Logger.Notification($"[SeraphLeveling] Skill decay ENABLED: {DecayGracePeriodDays} day grace, {DecayBasePointsPerDay} base decay/day, max {DecayMaxPointsPerDay}/day (online-only, per-skill overrides active)");
                }

                if (EnableSleepBuff)
                {
                    api.Logger.Notification($"[SeraphLeveling] Sleep buff ENABLED: linen bed {SleepBuffLinenBedMultiplier}x, hay bed {SleepBuffHayBedMultiplier}x, duration {SleepBuffDurationDays} days");
                }

                if (EnableDeathPenalty)
                {
                    api.Logger.Notification($"[SeraphLeveling] Death penalty ENABLED: {(DeathPenaltyFullReset ? "FULL RESET on death" : $"fraction={DeathPenaltyFraction}")}, exempt skills: {(DeathPenaltyExemptSkills.Count > 0 ? string.Join(", ", DeathPenaltyExemptSkills) : "none")}");
                }

                api.Logger.Notification("[SeraphLeveling] Config loaded from ModConfig/" + CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to load config file: {ex.Message}. Using default values.");
            }
        }

        /// <summary>
        /// Writes the current settings back out to ModConfig/SeraphLeveling.json.
        /// The file is the single source of truth: LoadConfigFile reads it at
        /// startup, and every /trait command that changes a value ends up here on
        /// the next world save. That way an edit made in the file and an edit made
        /// in game can never disagree with each other.
        ///
        /// The existing file is loaded first rather than starting from a fresh
        /// SeraphLevelingConfig, so any setting this method forgets to write keeps
        /// the admin's value instead of quietly snapping back to its default.
        /// This is the exact inverse of LoadConfigFile; the two must stay in step.
        /// </summary>
        private void SaveConfigFile()
        {
            if (ServerApi == null) return;

            try
            {
                SeraphLevelingConfig config =
                    ServerApi.LoadModConfig<SeraphLevelingConfig>(CONFIG_FILE_NAME) ?? new SeraphLevelingConfig();

                config.ConfigVersion = CURRENT_CONFIG_VERSION;
                LoadedConfigVersion = CURRENT_CONFIG_VERSION;

                config.MiningBaseBlocksPerIncrement = BaseBlocksPerIncrement;
                config.MiningIncrementStep = IncrementStep;
                config.MiningMaxPercent = MaxMiningSpeedPercent;
                config.MiningOreMultiplier = OreMultiplier;

                config.MeleeBaseDamagePerIncrement = BaseDamagePerIncrement;
                config.MeleeIncrementStep = MeleeIncrementStep;
                config.MeleeMaxPercent = MaxMeleeDamagePercent;

                config.RangedBaseDamagePerIncrement = BaseRangedDamagePerIncrement;
                config.RangedIncrementStep = RangedIncrementStep;
                config.RangedMaxDamagePercent = MaxRangedDamagePercent;
                config.RangedMaxAccuracyPercent = MaxRangedAccuracyPercent;
                config.RangedMaxDistancePercent = MaxRangedDistancePercent;

                config.WalkingBaseBlocksPerIncrement = BaseBlocksWalkedPerIncrement;
                config.WalkingIncrementStep = WalkingIncrementStep;
                config.WalkingMaxPercent = MaxWalkingSpeedPercent;

                config.HungerBaseSecondsPerIncrement = BaseSecondsPerIncrement;
                config.HungerIncrementStep = HungerIncrementStep;
                config.HungerMaxReductionPercent = MaxHungerReductionPercent;
                config.HungerSaturationThresholdPercent = HungerSaturationThresholdPercent;

                config.ArmorBaseSecondsPerIncrement = BaseSecondsInArmorPerIncrement;
                config.ArmorTimeIncrementStep = ArmorTimeIncrementStep;
                config.ArmorBaseDamageBlockedPerIncrement = BaseDamageBlockedPerIncrement;
                config.ArmorDamageIncrementStep = ArmorDamageIncrementStep;
                config.ArmorBaseRepairsPerIncrement = BaseRepairsPerIncrement;
                config.ArmorRepairIncrementStep = ArmorRepairIncrementStep;
                config.ArmorMaxDurabilityPercent = MaxArmorDurabilityPercent;
                config.ArmorMaxWalkSpeedPercent = MaxArmorWalkSpeedPercent;

                config.ArmorFirstEquipLightDurability = FirstEquipLightBonus;
                config.ArmorFirstEquipChainDurability = FirstEquipChainBonus;
                config.ArmorFirstEquipBrigandineDurability = FirstEquipBrigandineBonus;
                config.ArmorFirstEquipScaleDurability = FirstEquipScaleBonus;
                config.ArmorFirstEquipPlateDurability = FirstEquipPlateBonus;

                config.ArmorFirstEquipLightWalkSpeed = FirstEquipWalkSpeedLightBonus;
                config.ArmorFirstEquipChainWalkSpeed = FirstEquipWalkSpeedChainBonus;
                config.ArmorFirstEquipBrigandineWalkSpeed = FirstEquipWalkSpeedBrigandineBonus;
                config.ArmorFirstEquipScaleWalkSpeed = FirstEquipWalkSpeedScaleBonus;
                config.ArmorFirstEquipPlateWalkSpeed = FirstEquipWalkSpeedPlateBonus;

                config.EnableArmorHungerReduction = EnableArmorHungerReduction;
                config.ArmorMaxHungerReductionPercent = MaxArmorHungerReductionPercent;
                config.EnableArmorHealingBonus = EnableArmorHealingBonus;
                config.ArmorMaxHealingPercent = MaxArmorHealingPercent;

                config.ClothierRequiredUniqueClothes = ClothierRequiredUniqueClothes;
                config.ClothierBlacklistedItems = ClothierBlacklistedItems;

                config.MenderBaseRepairsPerIncrement = BaseMenderRepairsPerIncrement;
                config.MenderIncrementStep = MenderIncrementStep;
                config.MenderMaxPercent = MaxMenderPercent;

                config.PilfererBasePointsPerIncrement = BasePilfererPointsPerIncrement;
                config.PilfererIncrementStep = PilfererIncrementStep;
                config.PilfererMaxPercent = MaxPilfererPercent;

                config.ResourcefulBaseAnimalsPerIncrement = BaseResourcefulAnimalsPerIncrement;
                config.ResourcefulIncrementStep = ResourcefulIncrementStep;
                config.ResourcefulMaxLootPercent = MaxResourcefulLootPercent;
                config.ResourcefulMaxSpeedPercent = MaxResourcefulSpeedPercent;

                config.ForagerBaseCropsPerIncrement = BaseForagerCropsPerIncrement;
                config.ForagerIncrementStep = ForagerIncrementStep;
                config.ForagerMaxLootPercent = MaxForagerLootPercent;
                config.ForagerMaxWildCropPercent = MaxForagerWildCropPercent;

                config.FurtiveBaseSneakBlocksPerIncrement = BaseFurtiveSneakBlocksPerIncrement;
                config.FurtiveIncrementStep = FurtiveIncrementStep;
                config.FurtiveMaxPercent = MaxFurtivePercent;

                config.PreciseBaseDamagePerIncrement = BasePreciseDamagePerIncrement;
                config.PreciseIncrementStep = PreciseIncrementStep;
                config.PreciseMaxPercent = MaxPrecisePercent;

                config.TechnicalRequiredTranslocatorRepairs = TechnicalRequiredTranslocatorRepairs;

                config.HardyHealthMiningThreshold = HardyHealthMiningThreshold;
                config.HardyHealthArmorDurabilityThreshold = HardyHealthArmorDurabilityThreshold;
                config.HardyHealthBonus = HardyHealthBonus;

                config.AutoSaveIntervalSeconds = AutoSaveIntervalSeconds;

                config.DisabledSkills = DisabledSkills.ToArray();

                config.EnableCombatOverhaulCompat = COEnableCompat;
                config.COProficiencyBaseDamagePerIncrement = COBaseDamagePerIncrement;
                config.COProficiencyIncrementStep = COIncrementStep;
                config.COProficiencyBaseOverrides = new Dictionary<string, int>(COProficiencyBaseOverrides);
                config.COProficiencyIncrementOverrides = new Dictionary<string, int>(COProficiencyIncrementOverrides);
                config.COBowsProficiencyMax = COBowsProficiencyMax;
                config.COCrossbowsProficiencyMax = COCrossbowsProficiencyMax;
                config.COFirearmsProficiencyMax = COFirearmsProficiencyMax;
                config.COSlingsProficiencyMax = COSlingsProficiencyMax;
                config.COOneHandedSwordsProficiencyMax = COOneHandedSwordsProficiencyMax;
                config.COTwoHandedSwordsProficiencyMax = COTwoHandedSwordsProficiencyMax;
                config.COSpearsProficiencyMax = COSpearsProficiencyMax;
                config.COJavelinsProficiencyMax = COJavelinsProficiencyMax;
                config.COMacesProficiencyMax = COMacesProficiencyMax;
                config.COClubsProficiencyMax = COClubsProficiencyMax;
                config.COHalberdsProficiencyMax = COHalberdsProficiencyMax;
                config.COPoleaxeProficiencyMax = COPoleaxeProficiencyMax;
                config.COAxesProficiencyMax = COAxesProficiencyMax;
                config.COQuarterstaffProficiencyMax = COQuarterstaffProficiencyMax;
                config.COSteadyAimMax = COSteadyAimMax;

                config.EnableSkillDecay = EnableSkillDecay;
                config.DecayGracePeriodDays = DecayGracePeriodDays;
                config.DecayBasePointsPerDay = DecayBasePointsPerDay;
                config.DecayMaxPointsPerDay = DecayMaxPointsPerDay;
                config.DecayExemptSkills = DecayExemptSkills.ToArray();
                config.DecayGracePeriodOverrides = new Dictionary<string, double>(DecayGracePeriodOverrides);
                config.DecayBasePointsOverrides = new Dictionary<string, int>(DecayBasePointsOverrides);
                config.DecayMaxPointsOverrides = new Dictionary<string, int>(DecayMaxPointsOverrides);

                config.EnableSleepBuff = EnableSleepBuff;
                config.SleepBuffLinenBedMultiplier = SleepBuffLinenBedMultiplier;
                config.SleepBuffHayBedMultiplier = SleepBuffHayBedMultiplier;
                config.SleepBuffDurationDays = SleepBuffDurationDays;

                config.EnableDeathPenalty = EnableDeathPenalty;
                config.DeathPenaltyFraction = DeathPenaltyFraction;
                config.DeathPenaltyFullReset = DeathPenaltyFullReset;
                config.DeathPenaltyExemptSkills = DeathPenaltyExemptSkills.ToArray();

                config.EnableLevelUpMessages = EnableLevelUpMessages;
                config.EnableLevelUpSound = EnableLevelUpSound;
                config.LevelUpSoundName = LevelUpSoundName;
                config.LevelUpSoundVolume = LevelUpSoundVolume;

                config.GlobalXPRateMultiplier = GlobalXPRateMultiplier;
                config.EnableClassCapOffsets = EnableClassCapOffsets;
                config.CancelNegativeTraits = CancelNegativeTraits;

                config.TemporalResistanceEnabled = TemporalResistanceEnabled;
                config.TemporalResistanceMaxPercent = TemporalResistanceMaxPercent;
                config.TemporalResistanceWorksDuringStorms = TemporalResistanceWorksDuringStorms;
                config.TemporalLowStabilityThreshold = TemporalLowStabilityThreshold;
                config.TemporalRechargeEnabled = TemporalRechargeEnabled;
                config.TemporalRechargeMaxPercent = TemporalRechargeMaxPercent;
                config.TemporalRechargeGearTrickPercent = TemporalRechargeGearTrickPercent;
                config.TemporalRechargePassiveImmunityAtPercent = TemporalRechargePassiveImmunityAtPercent;
                config.RangedDrawSpeedEnabled = RangedDrawSpeedEnabled;
                config.RangedDrawSpeedMaxReductionPercent = RangedDrawSpeedMaxReductionPercent;
                config.RangedDrawSpeedAtLevel = RangedDrawSpeedAtLevel;
                config.RangedAimAssistEnabled = RangedAimAssistEnabled;
                config.RangedAccuracyMaxPercent = RangedAccuracyMaxPercent;
                config.MeleeAttackSpeedEnabled = MeleeAttackSpeedEnabled;
                config.MeleeAttackSpeedMaxReductionPercent = MeleeAttackSpeedMaxReductionPercent;
                config.MeleeAttackSpeedAtLevel = MeleeAttackSpeedAtLevel;

                config.EnableDebugLogging = DebugLoggingEnabled;
                config.VerboseDecayLogging = VerboseDecayLogging;

                ServerApi.StoreModConfig(config, CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to write config file: {ex.Message}");
            }
        }
        /// <summary>
        /// Persist the current settings. Everything now lives in
        /// ModConfig/SeraphLeveling.json, so this just writes that file.
        ///
        /// Settings used to be written into the world save under CONFIG_SAVE_KEY
        /// instead, which had two problems. The blob was reloaded on every
        /// SaveGameLoaded and overwrote whatever the admin had put in the config
        /// file, so edits to the file appeared to do nothing on any world that had
        /// ever run a /trait command. And the blob only ever stored the mining,
        /// melee, ranged, walking, hunger, armor and Combat Overhaul values, so
        /// in-game changes to the other systems, Clothier and Mender and the decay
        /// settings among them, were silently dropped on restart.
        ///
        /// LoadConfig migrates any surviving blob into the file and then erases it.
        /// </summary>
        private void PersistConfig()
        {
            if (ServerApi == null) return;

            SaveConfigFile();
            ServerApi.Logger.Debug($"[SeraphLeveling] Config saved to ModConfig/{CONFIG_FILE_NAME} (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | CO: {COProficiencyBaseOverrides.Count} base overrides, {COProficiencyIncrementOverrides.Count} increment overrides)");
        }

        /// <summary>
        /// One-time migration of the old world-save config blob into
        /// ModConfig/SeraphLeveling.json. Runs on SaveGameLoaded, after
        /// LoadConfigFile has already applied the file.
        ///
        /// Worlds saved by 1.18.1 and earlier carry a binary snapshot of the
        /// tuning values under CONFIG_SAVE_KEY, and that snapshot used to win over
        /// the config file on every load. Read it one last time so the world keeps
        /// playing exactly as it did, write those values into the file, then erase
        /// the blob. From the next load on there is nothing here to read and the
        /// file alone decides. Supports blob versions 1-10.
        /// </summary>
        private void LoadConfig()
        {
            if (ServerApi == null) return;

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CONFIG_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No legacy world config to migrate, using ModConfig/" + CONFIG_FILE_NAME);
                    return;
                }

                if (LoadedConfigVersion >= CURRENT_CONFIG_VERSION)
                {
                    // Already migrated on an earlier run. The erase below only reaches
                    // disk on the next world save, so a server that was killed before
                    // saving still has the blob sitting there. Reapplying it would undo
                    // whatever the admin has since put in the file, so just clear it.
                    ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, Array.Empty<byte>());
                    ServerApi.Logger.Debug("[SeraphLeveling] Discarded a stale legacy world config; ModConfig/" + CONFIG_FILE_NAME + " already owns these settings");
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
                            // Version 8: has armor config but not CO config
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
                            // CO config uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 9)
                        {
                            // Version 9: has global CO config but no per-proficiency overrides
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
                            COBaseDamagePerIncrement = reader.ReadInt32();
                            COIncrementStep = reader.ReadInt32();
                            // Per-proficiency overrides use defaults (empty)

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 10)
                        {
                            // Current format with per-proficiency CO config
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
                            COBaseDamagePerIncrement = reader.ReadInt32();
                            COIncrementStep = reader.ReadInt32();
                            // Per-proficiency base overrides
                            int baseCount = reader.ReadInt32();
                            COProficiencyBaseOverrides.Clear();
                            for (int i = 0; i < baseCount; i++)
                            {
                                string key = reader.ReadString();
                                int val = reader.ReadInt32();
                                COProficiencyBaseOverrides[key] = val;
                            }
                            // Per-proficiency increment overrides
                            int incCount = reader.ReadInt32();
                            COProficiencyIncrementOverrides.Clear();
                            for (int i = 0; i < incCount; i++)
                            {
                                string key = reader.ReadString();
                                int val = reader.ReadInt32();
                                COProficiencyIncrementOverrides[key] = val;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");

                // Fold the world's values into the config file and drop the blob, so
                // this world never reads from the save game again. An empty array is
                // what GetData returns for a missing key, so older builds of the mod
                // would also treat this as "nothing stored" if the world is opened by
                // one of them again.
                SaveConfigFile();
                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, Array.Empty<byte>());
                pendingConfigSave = false;
                ServerApi.Logger.Notification(
                    $"[SeraphLeveling] Migrated this world's saved settings into ModConfig/{CONFIG_FILE_NAME}. " +
                    "That file is now the only place settings are read from, so edits to it take effect on the next restart, or immediately with /trait reloadconfig.");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load config: {ex.Message}");
            }
        }
    }
}
