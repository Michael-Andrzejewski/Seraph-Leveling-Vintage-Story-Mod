using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SimpleImprovingTraits
{
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

        // Mining progression configuration
        // Every (BASE_BLOCKS_PER_LEVEL * level) blocks = +1% mining speed
        // Level 1: 100 blocks, Level 2: 300 total (100+200), Level 3: 600 total (100+200+300), etc.
        public const int BASE_BLOCKS_PER_LEVEL = 100;
        public const float MAX_MINING_SPEED_BONUS = 0.50f; // 50% bonus = 150% total mining speed

        // Storage for mining progress - keyed by player UID
        public static ConcurrentDictionary<string, long> MiningProgress = new ConcurrentDictionary<string, long>();

        // Lock object for persistence operations
        private static readonly object persistLock = new object();

        // Flag to indicate pending mining progress save
        private static volatile bool pendingMiningProgressSave = false;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;

            // Register chat command to check mining progress
            api.ChatCommands.Create("miningprogress")
                .WithDescription("Check your mining progress and speed bonus")
                .HandleWith(OnMiningProgressCommand);

            // Hook into block breaking for mining progression
            api.Event.DidBreakBlock += OnBlockBroken;

            // Hook into player join to apply saved mining bonuses
            api.Event.PlayerJoin += OnPlayerJoin;

            // Hook into world save event to persist mining progress
            api.Event.GameWorldSave += OnGameWorldSave;

            // Load mining progress data after save game is loaded
            api.Event.SaveGameLoaded += LoadMiningProgress;

            api.Logger.Notification("[SimpleImprovingTraits] Mod loaded");
        }

        /// <summary>
        /// Handler for the /miningprogress command.
        /// </summary>
        private TextCommandResult OnMiningProgressCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            long blocksMined = MiningProgress.GetValueOrDefault(playerUid, 0);
            int currentLevel = CalculateMiningLevel(blocksMined);
            long blocksForNextLevel = CalculateBlocksForLevel(currentLevel + 1);
            float currentBonus = CalculateMiningBonus(currentLevel);
            int maxLevel = CalculateMaxLevel();

            if (currentLevel >= maxLevel)
            {
                return TextCommandResult.Success(Lang.Get("simpleimprovingtraits:message-mining-progress-max",
                    blocksMined, currentLevel, (int)(currentBonus * 100)));
            }
            else
            {
                return TextCommandResult.Success(Lang.Get("simpleimprovingtraits:message-mining-progress",
                    blocksMined, currentLevel, (int)(currentBonus * 100), blocksForNextLevel - blocksMined));
            }
        }

        /// <summary>
        /// Called when a player breaks a block. Increments their mining progress.
        /// </summary>
        private void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return;

            string playerUid = byPlayer.PlayerUID;

            // Get current blocks mined and calculate old level
            long oldBlocksMined = MiningProgress.GetValueOrDefault(playerUid, 0);
            int oldLevel = CalculateMiningLevel(oldBlocksMined);

            // Increment blocks mined
            long newBlocksMined = oldBlocksMined + 1;
            MiningProgress[playerUid] = newBlocksMined;
            pendingMiningProgressSave = true;

            // Calculate new level
            int newLevel = CalculateMiningLevel(newBlocksMined);

            // If level increased, update the stat and notify player
            if (newLevel > oldLevel)
            {
                ApplyMiningBonus(byPlayer, newLevel);

                // Notify player of level up
                float newBonus = CalculateMiningBonus(newLevel);
                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("simpleimprovingtraits:message-mining-level-up", newLevel, (int)(newBonus * 100)),
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
            long blocksMined = MiningProgress.GetValueOrDefault(playerUid, 0);
            int level = CalculateMiningLevel(blocksMined);

            if (level > 0)
            {
                ApplyMiningBonus(byPlayer, level);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied mining level {level} to player {byPlayer.PlayerName}");
            }
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// </summary>
        private void ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            float bonus = CalculateMiningBonus(level);

            // Set the mining speed stat (persistent = false since we reapply on join)
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, 1f + bonus, false);
        }

        /// <summary>
        /// Calculate the mining level based on total blocks mined.
        /// Formula: Total blocks for level N = BASE_BLOCKS_PER_LEVEL * N * (N + 1) / 2
        /// Inverse: Level = floor((-1 + sqrt(1 + 8 * blocks / BASE_BLOCKS_PER_LEVEL)) / 2)
        /// </summary>
        public static int CalculateMiningLevel(long blocksMined)
        {
            if (blocksMined < BASE_BLOCKS_PER_LEVEL) return 0;

            // Solve quadratic: n^2 + n - 2*blocks/BASE = 0
            // n = (-1 + sqrt(1 + 8*blocks/BASE)) / 2
            double discriminant = 1.0 + (8.0 * blocksMined / BASE_BLOCKS_PER_LEVEL);
            int level = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);

            // Cap at max level
            int maxLevel = CalculateMaxLevel();
            return Math.Min(level, maxLevel);
        }

        /// <summary>
        /// Calculate the total blocks needed to reach a specific level.
        /// Formula: blocks = BASE_BLOCKS_PER_LEVEL * level * (level + 1) / 2
        /// </summary>
        public static long CalculateBlocksForLevel(int level)
        {
            return (long)BASE_BLOCKS_PER_LEVEL * level * (level + 1) / 2;
        }

        /// <summary>
        /// Calculate the mining speed bonus for a given level.
        /// Each level gives 1% (0.01) bonus, capped at MAX_MINING_SPEED_BONUS.
        /// </summary>
        public static float CalculateMiningBonus(int level)
        {
            float bonus = level * 0.01f;
            return Math.Min(bonus, MAX_MINING_SPEED_BONUS);
        }

        /// <summary>
        /// Calculate the maximum level based on the bonus cap.
        /// </summary>
        public static int CalculateMaxLevel()
        {
            return (int)(MAX_MINING_SPEED_BONUS * 100);
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
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
            }

            MiningProgress.Clear();
            pendingMiningProgressSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Called when the world is saved. Persist mining progress to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
            if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
            {
                PersistMiningProgress();
                pendingMiningProgressSave = false;
            }
        }

        /// <summary>
        /// Persist mining progress to world save data.
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
                            writer.Write((byte)1);    // Version 1

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var kvp in snapshot)
                            {
                                writer.Write(kvp.Key);   // Player UID
                                writer.Write(kvp.Value); // Blocks mined (long)
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(MINING_PROGRESS_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SimpleImprovingTraits] Persisted mining progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist mining progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load mining progress from world save data.
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

                        for (int i = 0; i < playerCount; i++)
                        {
                            string playerUid = reader.ReadString();
                            long blocksMined = reader.ReadInt64();
                            MiningProgress[playerUid] = blocksMined;
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
    }
}
