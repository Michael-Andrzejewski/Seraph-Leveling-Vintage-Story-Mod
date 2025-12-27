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
        /// </summary>
        public PickaxeProgressData GetPickaxeProgress(string pickaxeCode)
        {
            if (!PickaxeProgress.TryGetValue(pickaxeCode, out var progress))
            {
                progress = new PickaxeProgressData();
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
                .EndSubCommand();

            // Hook into block breaking for mining progression
            api.Event.DidBreakBlock += OnBlockBroken;

            // Hook into player join to apply saved mining bonuses
            api.Event.PlayerJoin += OnPlayerJoin;

            // Hook into world save event to persist mining progress
            api.Event.GameWorldSave += OnGameWorldSave;

            // Load config and mining progress data after save game is loaded
            api.Event.SaveGameLoaded += LoadConfig;
            api.Event.SaveGameLoaded += LoadMiningProgress;

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
                "  /trait miningbase [value] - Get or set base blocks per level (admin)\n" +
                "  /trait mininglevel <level> - Set your mining level (admin)\n" +
                "  /trait miningmax [percent] - Get or set max mining speed bonus (admin)");
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
                IncrementStep = newValue.Value; // Keep step and base synchronized
                pendingConfigSave = true;

                // Reapply bonuses for all online players (credits stay the same, just re-sync)
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
                    ApplyMiningBonus(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksPerIncrement}. New players will require this many points for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksPerIncrement}\nIncrement step: +{IncrementStep} per credit");
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
        /// Returns 5 for ore blocks, 1 for stone blocks, 0 for other blocks.
        /// </summary>
        private int GetBlockPoints(int blockId)
        {
            if (ServerApi == null) return 0;

            var block = ServerApi.World.GetBlock(blockId);
            if (block == null) return 0;

            string blockCode = block.Code?.ToString() ?? "";

            // Ore blocks: code contains "ore-" (e.g., "ore-lignite-chalk", "ore-copper-breccia")
            if (blockCode.Contains("ore-"))
            {
                return OreMultiplier;
            }

            // Stone blocks: code starts with "rock-" (e.g., "rock-granite", "rock-limestone")
            // Also include "gravel-" as it's mining-related
            if (blockCode.StartsWith("rock-") || blockCode.StartsWith("game:rock-"))
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
        /// Called when a player joins. Applies their saved mining bonus.
        /// </summary>
        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            string playerUid = byPlayer.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int credits = progress.TotalCredits;

            // Always apply (even at level 0) to ensure WatchedAttributes are synced
            ApplyMiningBonus(byPlayer, credits);

            if (credits > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied mining bonus {credits}% to player {byPlayer.PlayerName}");
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

        public override void Dispose()
        {
            // Persist any pending mining progress before shutdown
            if (ServerApi != null && (pendingMiningProgressSave || !MiningProgress.IsEmpty))
            {
                PersistMiningProgress();
            }

            if (ServerApi != null)
            {
                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
            }

            MiningProgress.Clear();
            pendingMiningProgressSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Called when the world is saved. Persist mining progress and config to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
            if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
            {
                PersistMiningProgress();
                pendingMiningProgressSave = false;
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
        /// Persist config to world save data.
        /// Version 3 adds OreMultiplier and IncrementStep.
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
                        writer.Write((byte)3); // Version 3: new config structure
                        writer.Write(BaseBlocksPerIncrement);
                        writer.Write(IncrementStep);
                        writer.Write(MaxMiningSpeedPercent);
                        writer.Write(OreMultiplier);
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Config saved (BaseBlocksPerIncrement={BaseBlocksPerIncrement}, MaxMiningSpeedPercent={MaxMiningSpeedPercent}, OreMultiplier={OreMultiplier})");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist config: {ex.Message}");
            }
        }

        /// <summary>
        /// Load config from world save data.
        /// Supports versions 1-3 for backwards compatibility.
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

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 3)
                        {
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Config loaded (BaseBlocksPerIncrement={BaseBlocksPerIncrement}, MaxMiningSpeedPercent={MaxMiningSpeedPercent}, OreMultiplier={OreMultiplier})");
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
        /// Postfix for getClassTraitText - adds dynamic mining progression info.
        /// The method has NO parameters - it's an instance method on CharacterSystem.
        /// </summary>
        public static void GetClassTraitText_Postfix(ref string __result)
        {
            // Get the player entity from the client API
            if (ClientApi == null) return;

            EntityPlayer eplr = ClientApi.World?.Player?.Entity;
            if (eplr == null) return;

            int level = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MINING_LEVEL, 0);
            int bonusPercent = eplr.WatchedAttributes.GetInt(SimpleImprovingTraitsModSystem.WATCHED_MINING_BONUS, 0);
            bool hasVanillaHardy = eplr.WatchedAttributes.GetBool("sitHasVanillaHardy", false);

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] getClassTraitText postfix called. Level={level}, Bonus={bonusPercent}, HasVanillaHardy={hasVanillaHardy}, Result={__result}");

            if (level <= 0) return;

            // Get the "no traits" message
            string noTraitsMsg = Lang.Get("charactersheet-notraits");

            // Check if we have NO real traits (only "no traits" message or empty)
            bool hasNoTraits = string.IsNullOrEmpty(__result) ||
                               __result.Trim() == noTraitsMsg.Trim() ||
                               __result == noTraitsMsg;

            // Our plain trait name from sitminingmastery (just "Hardy" with no stats)
            string plainTraitName = Lang.Get("simpleimprovingtraits:trait-sitminingmastery");

            if (hasVanillaHardy)
            {
                // Class already has Hardy (e.g., Blackguard) - update the existing Hardy's mining speed
                // bonusPercent is already capped by server, so combined = vanilla + earned
                int combinedBonus = SimpleImprovingTraitsModSystem.VANILLA_HARDY_MINING_BONUS + bonusPercent;
                __result = __result.Replace(
                    $"+{SimpleImprovingTraitsModSystem.VANILLA_HARDY_MINING_BONUS}% mining speed",
                    $"+{combinedBonus}% mining speed");

                // Remove our separate sitminingmastery entry if somehow present
                if (__result.Contains(plainTraitName))
                {
                    __result = __result.Replace("\n" + plainTraitName, "");
                    __result = __result.Replace(plainTraitName + "\n", "");
                    __result = __result.Replace(plainTraitName, "");
                }
            }
            else if (hasNoTraits)
            {
                // Commoner or other class with no traits - replace entirely with our dynamic Hardy
                __result = Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", bonusPercent);
            }
            else if (__result.Contains(plainTraitName))
            {
                // We have our trait but no vanilla Hardy - replace plain name with dynamic version
                __result = __result.Replace(plainTraitName,
                    Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", bonusPercent));
            }
            else
            {
                // Has other traits but no Hardy at all - append our dynamic Hardy
                __result = __result + "\n" + Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", bonusPercent);
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
}
