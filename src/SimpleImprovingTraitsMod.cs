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
using Vintagestory.API.Server;

namespace SimpleImprovingTraits
{
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
                "  /trait meleemax [percent] - Get or set max melee damage bonus (admin)");
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
        /// Calculate the melee damage bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMeleeDamagePercent.
        /// </summary>
        public static int CalculateMeleeBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMeleeDamagePercent);
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
        /// Called when a player joins. Applies their saved bonuses (mining and melee).
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

                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMeleeProgress;
            }

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("simpleimprovingtraits.server");

            MiningProgress.Clear();
            MeleeProgress.Clear();
            pendingMiningProgressSave = false;
            pendingMeleeProgressSave = false;
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
        /// Persist config to world save data.
        /// Version 4 adds melee configuration.
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
                        writer.Write((byte)4); // Version 4: adds melee config
                        writer.Write(BaseBlocksPerIncrement);
                        writer.Write(IncrementStep);
                        writer.Write(MaxMiningSpeedPercent);
                        writer.Write(OreMultiplier);
                        // Melee config
                        writer.Write(BaseDamagePerIncrement);
                        writer.Write(MeleeIncrementStep);
                        writer.Write(MaxMeleeDamagePercent);
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Config saved (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}%, Ore={OreMultiplier}x | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}%)");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist config: {ex.Message}");
            }
        }

        /// <summary>
        /// Load config from world save data.
        /// Supports versions 1-4 for backwards compatibility.
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
                            // Melee uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 3)
                        {
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            // Melee uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 4)
                        {
                            // Current format with melee config
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}%)");
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

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier}");

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
        /// Postfix for Entity.ReceiveDamage - tracks melee damage dealt by players.
        /// Calculates base weapon damage by removing the player's melee multiplier.
        /// </summary>
        public static void ReceiveDamage_Postfix(Entity __instance, DamageSource damageSource, float damage, bool __result)
        {
            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

            // Check if damage was dealt by a player
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
