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

            // Register game tick listener for walking distance tracking (every 500ms)
            api.Event.RegisterGameTickListener(OnWalkingTick, 500);

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
                "  /trait walkingmax [percent] - Get or set max walking speed bonus (admin)");
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

            // Check if player is using a pickaxe
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            if (pickaxeCode == null) return; // Not using a pickaxe, skip

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
        /// Called when a player disconnects. Cleans up their position tracking data.
        /// </summary>
        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;
            lastPlayerPositions.TryRemove(byPlayer.PlayerUID, out _);
        }

        /// <summary>
        /// Called when a player joins. Applies their saved bonuses (mining, melee, ranged, and walking).
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
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SimpleImprovingTraits] Failed to apply server Harmony patches: {ex.Message}");
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
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
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
            }
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

                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMeleeProgress;
                ServerApi.Event.SaveGameLoaded -= LoadRangedProgress;
                ServerApi.Event.SaveGameLoaded -= LoadWalkingProgress;
            }

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("simpleimprovingtraits.server");

            MiningProgress.Clear();
            MeleeProgress.Clear();
            RangedProgress.Clear();
            WalkingProgress.Clear();
            lastPlayerPositions.Clear();
            pendingMiningProgressSave = false;
            pendingMeleeProgressSave = false;
            pendingRangedProgressSave = false;
            pendingWalkingProgressSave = false;
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
        /// Persist config to world save data.
        /// Version 6 adds walking configuration.
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
                        writer.Write((byte)6); // Version 6: adds walking config
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
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Config saved (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist config: {ex.Message}");
            }
        }

        /// <summary>
        /// Load config from world save data.
        /// Supports versions 1-6 for backwards compatibility.
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
                            // Melee and Ranged use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 3)
                        {
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            // Melee and Ranged use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 4)
                        {
                            // Version 4: has melee config but not ranged
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            // Ranged uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 5)
                        {
                            // Version 5: has ranged config but not walking
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
                            // Walking uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 6)
                        {
                            // Current format with walking config
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
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to load config: {ex.Message}");
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

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier} | Ranged: Level={rangedLevel}, HasFocused={hasVanillaFocused} | Walking: Level={walkingLevel}, HasFleetfooted={hasVanillaFleetfooted}");

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
        /// Postfix for Entity.ReceiveDamage - tracks melee and ranged damage dealt by players.
        /// </summary>
        public static void ReceiveDamage_Postfix(Entity __instance, DamageSource damageSource, float damage, bool __result)
        {
            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

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
            }
        }
    }
}
