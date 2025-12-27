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
        // Every (BaseBlocksPerLevel * level) blocks = +1% mining speed
        // Level 1: 100 blocks, Level 2: 300 total (100+200), Level 3: 600 total (100+200+300), etc.
        public static int BaseBlocksPerLevel = 100;
        public const float MAX_MINING_SPEED_BONUS = 0.50f; // 50% bonus = 150% total mining speed
        private const string CONFIG_SAVE_KEY = "sitConfig";

        // Storage for mining progress - keyed by player UID
        public static ConcurrentDictionary<string, long> MiningProgress = new ConcurrentDictionary<string, long>();

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
                "  /trait miningbase [value] - Get or set base blocks per level (admin)");
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
        /// Handler for /trait miningbase command.
        /// </summary>
        private TextCommandResult OnTraitMiningBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per level must be at least 1");
                }

                BaseBlocksPerLevel = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    long blocksMined = MiningProgress.GetValueOrDefault(playerUid, 0);
                    int level = CalculateMiningLevel(blocksMined);
                    ApplyMiningBonus(player, level);
                }

                return TextCommandResult.Success($"Base blocks per level set to {BaseBlocksPerLevel}. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per level: {BaseBlocksPerLevel}");
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

            // Always apply (even at level 0) to ensure WatchedAttributes are synced
            ApplyMiningBonus(byPlayer, level);

            if (level > 0)
            {
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Applied mining level {level} to player {byPlayer.PlayerName}");
            }
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// Also syncs the level and bonus to WatchedAttributes for client display,
        /// and adds/removes the mining mastery trait from extraTraits.
        /// </summary>
        private void ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            float bonus = CalculateMiningBonus(level);

            // Set the mining speed stat (persistent = false since we reapply on join)
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, 1f + bonus, false);

            // Sync level and bonus to WatchedAttributes for client-side display
            player.Entity.WatchedAttributes.SetInt(WATCHED_MINING_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MINING_BONUS, (int)(bonus * 100));

            // Add or remove the mining mastery trait from extraTraits
            UpdateExtraTrait(player.Entity, MINING_TRAIT_CODE, level > 0);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MINING_LEVEL);
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
        /// Calculate the mining level based on total blocks mined.
        /// Formula: Total blocks for level N = BaseBlocksPerLevel * N * (N + 1) / 2
        /// Inverse: Level = floor((-1 + sqrt(1 + 8 * blocks / BaseBlocksPerLevel)) / 2)
        /// </summary>
        public static int CalculateMiningLevel(long blocksMined)
        {
            if (blocksMined < BaseBlocksPerLevel) return 0;

            // Solve quadratic: n^2 + n - 2*blocks/BASE = 0
            // n = (-1 + sqrt(1 + 8*blocks/BASE)) / 2
            double discriminant = 1.0 + (8.0 * blocksMined / BaseBlocksPerLevel);
            int level = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);

            // Cap at max level
            int maxLevel = CalculateMaxLevel();
            return Math.Min(level, maxLevel);
        }

        /// <summary>
        /// Calculate the total blocks needed to reach a specific level.
        /// Formula: blocks = BaseBlocksPerLevel * level * (level + 1) / 2
        /// </summary>
        public static long CalculateBlocksForLevel(int level)
        {
            return (long)BaseBlocksPerLevel * level * (level + 1) / 2;
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

        /// <summary>
        /// Persist config to world save data.
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
                        writer.Write((byte)1); // Version
                        writer.Write(BaseBlocksPerLevel);
                    }
                    data = ms.ToArray();
                }

                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, data);
                ServerApi.Logger.Debug($"[SimpleImprovingTraits] Config saved (BaseBlocksPerLevel={BaseBlocksPerLevel})");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SimpleImprovingTraits] Failed to persist config: {ex.Message}");
            }
        }

        /// <summary>
        /// Load config from world save data.
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
                        BaseBlocksPerLevel = reader.ReadInt32();
                    }
                }

                ServerApi.Logger.Notification($"[SimpleImprovingTraits] Config loaded (BaseBlocksPerLevel={BaseBlocksPerLevel})");
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

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] getClassTraitText postfix called. Level={level}, Bonus={bonusPercent}, Result={__result}");

            if (level <= 0) return;

            // Build the trait text in vanilla format: "Hardy (+X% mining speed)"
            string miningTraitText = Lang.Get("simpleimprovingtraits:trait-hardy-dynamic", bonusPercent);

            // Check for the "no traits" message and replace it entirely
            string noTraitsMsg = Lang.Get("charactersheet-notraits");

            // Also check for the plain trait name from our trait definition
            string plainTraitName = Lang.Get("simpleimprovingtraits:trait-sitminingmastery");

            if (__result != null && __result.Contains(plainTraitName))
            {
                // Replace the plain trait name with the full dynamic version
                __result = __result.Replace(plainTraitName, miningTraitText);
            }
            else if (string.IsNullOrEmpty(__result) || __result.Contains(noTraitsMsg) || __result.Trim() == noTraitsMsg.Trim())
            {
                // No traits or only "no traits" message - replace with our trait
                __result = miningTraitText;
            }
            else if (!__result.Contains("Mining Mastery"))
            {
                // Has other traits but not ours - append
                __result = __result + "\n" + miningTraitText;
            }

            ClientApi.Logger.Debug($"[SimpleImprovingTraits] Modified result: {__result}");
        }
    }
}
