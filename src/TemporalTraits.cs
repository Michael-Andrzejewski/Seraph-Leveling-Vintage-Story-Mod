using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphLeveling
{
    /// <summary>
    /// Progress data shared by the two temporal traits (Resistance and Recharge).
    /// PermanentPercent is the accumulated trait value and persists forever.
    /// The "today" fields drive the triangular daily leveling and reset each in-game day.
    /// </summary>
    public class TemporalProgressData
    {
        /// <summary>Permanent trait value in percent (e.g. 1 = +1%). Capped by the trait's max.</summary>
        public int PermanentPercent { get; set; }

        /// <summary>Real seconds spent below the low-stability threshold during the current in-game day.</summary>
        public double SecondsTodayAtLowStability { get; set; }

        /// <summary>How many percent have already been credited from today's accumulated time.</summary>
        public int LevelsCreditedToday { get; set; }

        /// <summary>The in-game day number this data last accumulated on. Used to detect day rollover.</summary>
        public double LastDay { get; set; } = -1;
    }

    /// <summary>
    /// Server-to-client sync of the experimental feature settings. The bow draw,
    /// aim-assist and melee swing patches also run on the client for prediction, and
    /// on a dedicated server the client never reads the server's config file. Sent
    /// once on join so the client's statics match the server's.
    /// </summary>
    [ProtoContract]
    public class ExperimentalFeatureConfigMessage
    {
        [ProtoMember(1)] public bool RangedDrawSpeedEnabled { get; set; }
        [ProtoMember(2)] public int RangedDrawSpeedMaxReductionPercent { get; set; }
        [ProtoMember(3)] public int RangedDrawSpeedAtLevel { get; set; }
        [ProtoMember(4)] public bool RangedAimAssistEnabled { get; set; }
        [ProtoMember(5)] public int RangedAccuracyMaxPercent { get; set; }
        [ProtoMember(6)] public bool MeleeAttackSpeedEnabled { get; set; }
        [ProtoMember(7)] public int MeleeAttackSpeedMaxReductionPercent { get; set; }
        [ProtoMember(8)] public int MeleeAttackSpeedAtLevel { get; set; }
    }

    // Temporal traits, bow draw speed, and the knife gear-trick recharge bump,
    // ported from the Seraph Leveling Experimental fork. Everything here is OFF
    // by default and switched on in ModConfig/SeraphLeveling.json. Kept in a
    // partial of the main mod system so it integrates with existing statics
    // (ServerApi, NotifyLevelUp, serverHarmony) while keeping the edits to the
    // giant main file small.
    public partial class SeraphLevelingModSystem
    {
        // ===================================================================
        // CONFIG MIRRORS (populated from SeraphLevelingConfig in LoadConfigFile,
        // pushed to clients via ExperimentalFeatureConfigMessage)
        // ===================================================================

        // Temporal Resistance: slows temporal stability drain.
        public static bool TemporalResistanceEnabled = false;
        public static int TemporalResistanceMaxPercent = 75;
        public static bool TemporalResistanceWorksDuringStorms = false;

        // Shared "low stability" threshold that lets both temporal traits level up.
        public static float TemporalLowStabilityThreshold = 0.5f;

        // Temporal Recharge: speeds temporal stability recovery, and grants
        // passive-decay immunity at/above the immunity threshold.
        public static bool TemporalRechargeEnabled = false;
        public static int TemporalRechargeMaxPercent = 200;
        public static int TemporalRechargeGearTrickPercent = 5;
        public static int TemporalRechargePassiveImmunityAtPercent = 200;

        // Bow draw speed (rides on the existing Ranged trait's raw credits).
        public static bool RangedDrawSpeedEnabled = false;
        public static int RangedDrawSpeedMaxReductionPercent = 50;
        public static int RangedDrawSpeedAtLevel = 50;
        public static bool RangedAimAssistEnabled = false;
        public static int RangedAccuracyMaxPercent = 99;
        public static bool MeleeAttackSpeedEnabled = false;
        public static int MeleeAttackSpeedMaxReductionPercent = 50;
        public static int MeleeAttackSpeedAtLevel = 50;

        /// <summary>Builds the client sync packet from the server's current settings.</summary>
        public static ExperimentalFeatureConfigMessage BuildFeatureConfigMessage()
        {
            return new ExperimentalFeatureConfigMessage
            {
                RangedDrawSpeedEnabled = RangedDrawSpeedEnabled,
                RangedDrawSpeedMaxReductionPercent = RangedDrawSpeedMaxReductionPercent,
                RangedDrawSpeedAtLevel = RangedDrawSpeedAtLevel,
                RangedAimAssistEnabled = RangedAimAssistEnabled,
                RangedAccuracyMaxPercent = RangedAccuracyMaxPercent,
                MeleeAttackSpeedEnabled = MeleeAttackSpeedEnabled,
                MeleeAttackSpeedMaxReductionPercent = MeleeAttackSpeedMaxReductionPercent,
                MeleeAttackSpeedAtLevel = MeleeAttackSpeedAtLevel,
            };
        }

        /// <summary>
        /// Sends the current experimental settings to every online client.
        /// Join sends the packet per player; config commands call this so
        /// connected clients do not keep stale values until rejoin.
        /// </summary>
        public void SyncExperimentalConfigToClients()
        {
            if (ServerApi == null) return;
            var msg = BuildFeatureConfigMessage();
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                if (onlinePlayer is IServerPlayer sp)
                {
                    try { serverSoundChannel?.SendPacket(msg, sp); } catch { }
                }
            }
        }

        /// <summary>Applies a received sync packet to the client-side statics.</summary>
        public static void ApplyFeatureConfigMessage(ExperimentalFeatureConfigMessage msg)
        {
            if (msg == null) return;
            RangedDrawSpeedEnabled = msg.RangedDrawSpeedEnabled;
            RangedDrawSpeedMaxReductionPercent = msg.RangedDrawSpeedMaxReductionPercent;
            RangedDrawSpeedAtLevel = msg.RangedDrawSpeedAtLevel;
            RangedAimAssistEnabled = msg.RangedAimAssistEnabled;
            RangedAccuracyMaxPercent = msg.RangedAccuracyMaxPercent;
            MeleeAttackSpeedEnabled = msg.MeleeAttackSpeedEnabled;
            MeleeAttackSpeedMaxReductionPercent = msg.MeleeAttackSpeedMaxReductionPercent;
            MeleeAttackSpeedAtLevel = msg.MeleeAttackSpeedAtLevel;
        }

        // ===================================================================
        // STATE
        // ===================================================================

        private const string TEMPORAL_RESISTANCE_SAVE_KEY = "sitTemporalResistanceProgress";
        private const string TEMPORAL_RECHARGE_SAVE_KEY = "sitTemporalRechargeProgress";

        public static ConcurrentDictionary<string, TemporalProgressData> TemporalResistanceProgress = new ConcurrentDictionary<string, TemporalProgressData>();
        public static ConcurrentDictionary<string, TemporalProgressData> TemporalRechargeProgress = new ConcurrentDictionary<string, TemporalProgressData>();

        private static volatile bool pendingTemporalResistanceSave = false;
        private static volatile bool pendingTemporalRechargeSave = false;

        // Cached reference to the vanilla temporal stability system (server side).
        private static SystemTemporalStability temporalSystemRef;

        // ===================================================================
        // SHARED GUARDS
        // ===================================================================

        /// <summary>
        /// Is this player far enough into the world to be asked questions
        /// about their entity?
        ///
        /// A player who is still joining is already in AllOnlinePlayers, and
        /// their entity is already there and already reports itself alive,
        /// but the game has not built its sided properties yet. Anything that
        /// reads those, GetBehavior above all, throws a null reference on it.
        ///
        /// The window is normally a few milliseconds. It gets much wider on a
        /// slow join, which is exactly when the server logs "Delayed join,
        /// need to load one spawn chunk first", and on a heavily modded world
        /// that can last several seconds.
        ///
        /// Alive is not a substitute: it reads a watched attribute, which is
        /// present long before the entity is put together.
        /// </summary>
        internal static bool IsPlaying(IServerPlayer player)
        {
            var entity = player?.Entity;
            if (entity == null) return false;
            if (player.ConnectionState != EnumClientState.Playing) return false;

            // Belt and braces, in the order that cannot itself throw. Plain
            // fields first: SidedProperties reads World.Side to decide which
            // half to hand back, so on an entity that has a shape but no
            // world yet, asking for it is the very fault being guarded
            // against. Properties null is the case actually seen in the wild.
            if (entity.World == null || entity.Properties == null) return false;

            return entity.SidedProperties != null;
        }

        /// <summary>
        /// Registers a tick listener that cannot bring the server down.
        ///
        /// A handler registered the plain way has no error handler, and the
        /// game rethrows anything that escapes one of those. The rethrow
        /// happens before the listener records that it ran, so it is still
        /// due on the very next pass of the server loop, and it fires again,
        /// and again, as fast as the loop turns.
        ///
        /// One null reference during a slow join therefore became a hundred
        /// thousand of them in three seconds, filled the log with sixty nine
        /// megabytes of the same stack, and tripped DieAboveErrorCount, which
        /// shut the server down before the player had finished joining.
        ///
        /// Handing the game an error handler is what stops that: the listener
        /// records that it ran either way, so a fault costs one line a minute
        /// instead of the world.
        /// </summary>
        private long RegisterSafeTickListener(ICoreServerAPI api, Action<float> handler, int intervalMs, string what)
        {
            return api.Event.RegisterGameTickListener(
                handler,
                ex => ComplainAbout(api, what, ex),
                intervalMs);
        }

        /// <summary>When each tick listener last complained, so one fault is one line a minute.</summary>
        private readonly ConcurrentDictionary<string, long> lastComplaintMs = new ConcurrentDictionary<string, long>();

        private void ComplainAbout(ICoreServerAPI api, string what, Exception ex)
        {
            long now = api.World.ElapsedMilliseconds;
            long last = lastComplaintMs.TryGetValue(what, out long when) ? when : long.MinValue / 2;

            if (now - last < 60000) return;
            lastComplaintMs[what] = now;

            api.Logger.Error($"[SeraphLeveling] {what} threw and was skipped. This will be reported at most once a minute.");
            api.Logger.Error(ex);
        }

        // ===================================================================
        // INIT (called once from StartServerSide)
        // ===================================================================

        private void InitTemporalTraits(ICoreServerAPI api)
        {
            try
            {
                temporalSystemRef = api.ModLoader.GetModSystem<SystemTemporalStability>();

                api.Event.SaveGameLoaded += LoadTemporalResistanceProgress;
                api.Event.SaveGameLoaded += LoadTemporalRechargeProgress;
                api.Event.GameWorldSave += SaveTemporalProgressOnWorldSave;

                // 1 second cadence: matches the per-tick second we add while at low stability.
                RegisterSafeTickListener(api, OnTemporalTick, 1000, "the temporal traits tick");

                if (TemporalResistanceEnabled || TemporalRechargeEnabled)
                {
                    api.Logger.Notification("[SeraphLeveling] Temporal traits (Resistance/Recharge) initialized");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to init temporal traits: {ex.Message}");
            }
        }

        // ===================================================================
        // LEVELING (server tick: time spent at low temporal stability)
        // ===================================================================

        /// <summary>Largest n with n*(n+1)/2 &lt;= minutes. Inverse of the triangular cost.</summary>
        private static int LevelsFromMinutes(double minutes)
        {
            if (minutes <= 0) return 0;
            return (int)Math.Floor((Math.Sqrt(8.0 * minutes + 1.0) - 1.0) / 2.0);
        }

        /// <summary>Minutes of low-stability time required this day to reach the given level.</summary>
        private static double TriangularMinutesForLevel(int level)
        {
            if (level <= 0) return 0;
            return level * (level + 1) / 2.0;
        }

        private void OnTemporalTick(float dt)
        {
            if (ServerApi == null || isDisposed) return;
            if (!TemporalResistanceEnabled && !TemporalRechargeEnabled) return;

            int currentDay = (int)ServerApi.World.Calendar.ElapsedDays;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (!IsPlaying(player) || !player.Entity.Alive) continue;

                var beh = player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
                if (beh == null) continue;

                // Skip until vanilla has actually initialized the stability value. An unset
                // "temporalStability" reads as 0.0, which would otherwise look like low stability
                // and tick phantom time into the daily counter right after spawn.
                if (!player.Entity.WatchedAttributes.HasAttribute("temporalStability")) continue;

                // Only train while the player's own stability is below the threshold.
                if (beh.OwnStability >= TemporalLowStabilityThreshold) continue;

                string uid = player.PlayerUID;

                if (TemporalResistanceEnabled &&
                    AccrueTemporalTrait(player, uid, TemporalResistanceProgress, TemporalResistanceMaxPercent, currentDay, "Temporal Resistance", isRecharge: false))
                {
                    pendingTemporalResistanceSave = true;
                }

                if (TemporalRechargeEnabled &&
                    AccrueTemporalTrait(player, uid, TemporalRechargeProgress, TemporalRechargeMaxPercent, currentDay, "Temporal Recharge", isRecharge: true))
                {
                    pendingTemporalRechargeSave = true;
                }
            }
        }

        /// <summary>
        /// Adds one second of low-stability time, handles the daily reset, and credits any
        /// newly earned percent via the triangular schedule. Returns true if the permanent
        /// value changed (so the caller can flag a save).
        /// </summary>
        private bool AccrueTemporalTrait(IServerPlayer player, string uid,
            ConcurrentDictionary<string, TemporalProgressData> dict, int maxPercent, int currentDay,
            string traitLabel, bool isRecharge)
        {
            var p = dict.GetOrAdd(uid, _ => new TemporalProgressData());

            // Daily reset of the triangular counter (permanent value is untouched).
            if ((int)p.LastDay != currentDay)
            {
                p.LastDay = currentDay;
                p.SecondsTodayAtLowStability = 0;
                p.LevelsCreditedToday = 0;
            }

            if (p.PermanentPercent >= maxPercent) return false;

            p.SecondsTodayAtLowStability += 1.0; // 1s tick
            double minutes = p.SecondsTodayAtLowStability / 60.0;

            int eligibleToday = LevelsFromMinutes(minutes);
            if (eligibleToday <= p.LevelsCreditedToday) return false;

            int gain = eligibleToday - p.LevelsCreditedToday;
            p.LevelsCreditedToday = eligibleToday;

            int before = p.PermanentPercent;
            p.PermanentPercent = Math.Min(maxPercent, p.PermanentPercent + gain);
            if (p.PermanentPercent <= before) return false;

            if (isRecharge)
            {
                double mult = 1.0 + p.PermanentPercent / 100.0;
                NotifyLevelUp(player, $"{traitLabel} increased to {p.PermanentPercent}%. Temporal stability now recovers {mult:F2}x as fast.");
            }
            else
            {
                NotifyLevelUp(player, $"{traitLabel} increased to {p.PermanentPercent}%. Temporal stability now drains {p.PermanentPercent}% slower.");
            }
            return true;
        }

        // ===================================================================
        // EFFECT HELPERS (read by the Harmony patches)
        // ===================================================================

        public static double GetTemporalResistanceFraction(string uid)
        {
            if (!TemporalResistanceEnabled) return 0.0;
            if (uid != null && TemporalResistanceProgress.TryGetValue(uid, out var p))
                return Math.Min(TemporalResistanceMaxPercent, p.PermanentPercent) / 100.0;
            return 0.0;
        }

        public static double GetTemporalRechargeFraction(string uid)
        {
            if (!TemporalRechargeEnabled) return 0.0;
            if (uid != null && TemporalRechargeProgress.TryGetValue(uid, out var p))
                return Math.Min(TemporalRechargeMaxPercent, p.PermanentPercent) / 100.0;
            return 0.0;
        }

        public static bool HasPassiveDecayImmunity(string uid)
        {
            if (!TemporalRechargeEnabled) return false;
            if (uid != null && TemporalRechargeProgress.TryGetValue(uid, out var p))
                return p.PermanentPercent >= TemporalRechargePassiveImmunityAtPercent;
            return false;
        }

        public static bool IsTemporalStormActive()
        {
            return temporalSystemRef != null && temporalSystemRef.StormStrength > 0f;
        }

        /// <summary>Called from the knife gear-trick patch to bump recharge by the configured amount.</summary>
        public static void GrantRechargeFromGearTrick(IServerPlayer player)
        {
            if (!TemporalRechargeEnabled || player == null) return;
            string uid = player.PlayerUID;
            var p = TemporalRechargeProgress.GetOrAdd(uid, _ => new TemporalProgressData());
            int before = p.PermanentPercent;
            p.PermanentPercent = Math.Min(TemporalRechargeMaxPercent, p.PermanentPercent + TemporalRechargeGearTrickPercent);
            if (p.PermanentPercent <= before) return;
            pendingTemporalRechargeSave = true;
            double mult = 1.0 + p.PermanentPercent / 100.0;
            NotifyLevelUp(player, $"Temporal Recharge increased to {p.PermanentPercent}% from a temporal gear. Recovery is now {mult:F2}x as fast.");
        }

        // ===================================================================
        // HARMONY WIRE-UP (called from ApplyServerHarmonyPatches)
        // ===================================================================

        private void PatchTemporalStability(ICoreServerAPI api)
        {
            try
            {
                var method = AccessTools.Method(typeof(EntityBehaviorTemporalStabilityAffected), "OnGameTick");
                if (method == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find EntityBehaviorTemporalStabilityAffected.OnGameTick to patch");
                    return;
                }
                var prefix = AccessTools.Method(typeof(TemporalStabilityPatches), nameof(TemporalStabilityPatches.OnGameTick_Prefix));
                var postfix = AccessTools.Method(typeof(TemporalStabilityPatches), nameof(TemporalStabilityPatches.OnGameTick_Postfix));
                serverHarmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                api.Logger.Debug("[SeraphLeveling] Patched temporal stability for Resistance/Recharge");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch temporal stability: {ex.Message}");
            }
        }

        private void PatchBowDrawSpeed(ICoreServerAPI api)
        {
            if (BowDrawSpeedPatches.PatchedInProcess) return;
            try
            {
                var step = AccessTools.Method(typeof(ItemBow), "OnHeldInteractStep");
                var stop = AccessTools.Method(typeof(ItemBow), "OnHeldInteractStop");
                var stepPrefix = AccessTools.Method(typeof(BowDrawSpeedPatches), nameof(BowDrawSpeedPatches.OnHeldInteractStep_Prefix));
                var stopPrefix = AccessTools.Method(typeof(BowDrawSpeedPatches), nameof(BowDrawSpeedPatches.OnHeldInteractStop_Prefix));
                if (step != null) serverHarmony.Patch(step, prefix: new HarmonyMethod(stepPrefix));
                if (stop != null) serverHarmony.Patch(stop, prefix: new HarmonyMethod(stopPrefix));
                BowDrawSpeedPatches.PatchedInProcess = true;
                api.Logger.Debug("[SeraphLeveling] Patched ItemBow draw speed (server)");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch ItemBow draw speed: {ex.Message}");
            }
        }

        private void PatchKnifeGearTrick(ICoreServerAPI api)
        {
            try
            {
                var start = AccessTools.Method(typeof(ItemKnife), "OnHeldInteractStart");
                var step = AccessTools.Method(typeof(ItemKnife), "OnHeldInteractStep");
                var startPostfix = AccessTools.Method(typeof(KnifeGearTrickPatches), nameof(KnifeGearTrickPatches.OnHeldInteractStart_Postfix));
                var stepPostfix = AccessTools.Method(typeof(KnifeGearTrickPatches), nameof(KnifeGearTrickPatches.OnHeldInteractStep_Postfix));
                if (start != null) serverHarmony.Patch(start, postfix: new HarmonyMethod(startPostfix));
                if (step != null) serverHarmony.Patch(step, postfix: new HarmonyMethod(stepPostfix));
                api.Logger.Debug("[SeraphLeveling] Patched ItemKnife gear-trick for Temporal Recharge");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch ItemKnife gear-trick: {ex.Message}");
            }
        }

        // ===================================================================
        // PERSISTENCE (binary, mirrors the other trait save formats)
        // ===================================================================

        public static void PersistTemporalResistanceProgress() => PersistTemporalDict(TemporalResistanceProgress, TEMPORAL_RESISTANCE_SAVE_KEY, 0x52); // 'R'
        public static void PersistTemporalRechargeProgress() => PersistTemporalDict(TemporalRechargeProgress, TEMPORAL_RECHARGE_SAVE_KEY, 0x43); // 'C'

        private void LoadTemporalResistanceProgress() => LoadTemporalDict(TemporalResistanceProgress, TEMPORAL_RESISTANCE_SAVE_KEY, 0x52);
        private void LoadTemporalRechargeProgress() => LoadTemporalDict(TemporalRechargeProgress, TEMPORAL_RECHARGE_SAVE_KEY, 0x43);

        private void SaveTemporalProgressOnWorldSave()
        {
            if (ServerApi == null || isDisposed) return;
            if (pendingTemporalResistanceSave || !TemporalResistanceProgress.IsEmpty) PersistTemporalResistanceProgress();
            if (pendingTemporalRechargeSave || !TemporalRechargeProgress.IsEmpty) PersistTemporalRechargeProgress();
            pendingTemporalResistanceSave = false;
            pendingTemporalRechargeSave = false;
        }

        private static void PersistTemporalDict(ConcurrentDictionary<string, TemporalProgressData> dict, string saveKey, byte magic3)
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save, resurrecting progress after /trait reset.
                try
                {
                    var snapshot = dict.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x54); // 'T'
                            writer.Write(magic3);     // 'R' resistance or 'C' recharge
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var kvp in snapshot)
                            {
                                writer.Write(kvp.Key);
                                var p = kvp.Value;
                                writer.Write(p.PermanentPercent);
                                writer.Write(p.SecondsTodayAtLowStability);
                                writer.Write(p.LevelsCreditedToday);
                                writer.Write(p.LastDay);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(saveKey, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted {saveKey} for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist {saveKey}: {ex.Message}");
                }
            }
        }

        private static void LoadTemporalDict(ConcurrentDictionary<string, TemporalProgressData> dict, string saveKey, byte magic3)
        {
            if (ServerApi == null) return;

            dict.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(saveKey);
                if (data == null || data.Length == 0) return;

                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    byte b1 = reader.ReadByte();
                    byte b2 = reader.ReadByte();
                    byte b3 = reader.ReadByte();
                    if (b1 != 0x53 || b2 != 0x54 || b3 != magic3)
                    {
                        ServerApi.Logger.Warning($"[SeraphLeveling] Invalid data format for {saveKey}");
                        return;
                    }

                    byte version = reader.ReadByte();
                    if (version != 1)
                    {
                        ServerApi.Logger.Warning($"[SeraphLeveling] Unknown save version {version} for {saveKey}");
                        return;
                    }

                    int playerCount = reader.ReadInt32();
                    for (int i = 0; i < playerCount; i++)
                    {
                        try
                        {
                            string uid = reader.ReadString();
                            var p = new TemporalProgressData
                            {
                                PermanentPercent = reader.ReadInt32(),
                                SecondsTodayAtLowStability = reader.ReadDouble(),
                                LevelsCreditedToday = reader.ReadInt32(),
                                LastDay = reader.ReadDouble()
                            };
                            dict[uid] = p;
                        }
                        catch (Exception innerEx)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt entry {i + 1}/{playerCount} in {saveKey}: {innerEx.Message}");
                            break;
                        }
                    }
                }

                if (dict.Count > 0)
                {
                    ServerApi.Logger.Notification($"[SeraphLeveling] Loaded {saveKey} for {dict.Count} players");
                }
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load {saveKey}: {ex.Message}");
            }
        }

        // ===================================================================
        // VIEW COMMANDS
        // ===================================================================

        private TextCommandResult OnTraitTempResistCommand(TextCommandCallingArgs args)
        {
            if (!TemporalResistanceEnabled) return TextCommandResult.Error("Temporal Resistance is disabled. Enable TemporalResistanceEnabled in ModConfig/SeraphLeveling.json.");
            var player = args.Caller.Player;
            if (player?.Entity == null) return TextCommandResult.Error("Could not find player entity");

            var p = TemporalResistanceProgress.GetOrAdd(player.PlayerUID, _ => new TemporalProgressData());
            var sb = new StringBuilder();
            if (p.PermanentPercent >= TemporalResistanceMaxPercent) sb.AppendLine("=== MAXED OUT ===");
            sb.AppendLine($"Temporal Resistance: {p.PermanentPercent}% / {TemporalResistanceMaxPercent}%");
            sb.AppendLine($"Temporal stability drains {p.PermanentPercent}% slower.");
            sb.AppendLine($"Applies during storms: {(TemporalResistanceWorksDuringStorms ? "yes" : "no")}");
            if (p.PermanentPercent < TemporalResistanceMaxPercent)
            {
                double nextMin = TriangularMinutesForLevel(p.LevelsCreditedToday + 1);
                sb.AppendLine($"Today: +{p.LevelsCreditedToday}% so far, {p.SecondsTodayAtLowStability / 60.0:F1} min below {(int)(TemporalLowStabilityThreshold * 100)}% stability.");
                sb.AppendLine($"Next +1% today at {nextMin:F0} min total.");
            }
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        private TextCommandResult OnTraitTempRechargeCommand(TextCommandCallingArgs args)
        {
            if (!TemporalRechargeEnabled) return TextCommandResult.Error("Temporal Recharge is disabled. Enable TemporalRechargeEnabled in ModConfig/SeraphLeveling.json.");
            var player = args.Caller.Player;
            if (player?.Entity == null) return TextCommandResult.Error("Could not find player entity");

            var p = TemporalRechargeProgress.GetOrAdd(player.PlayerUID, _ => new TemporalProgressData());
            double mult = 1.0 + p.PermanentPercent / 100.0;
            var sb = new StringBuilder();
            if (p.PermanentPercent >= TemporalRechargeMaxPercent) sb.AppendLine("=== MAXED OUT ===");
            sb.AppendLine($"Temporal Recharge: {p.PermanentPercent}% / {TemporalRechargeMaxPercent}%");
            sb.AppendLine($"Temporal stability recovers {mult:F2}x as fast.");
            bool immune = p.PermanentPercent >= TemporalRechargePassiveImmunityAtPercent;
            sb.AppendLine($"Passive decay immunity: {(immune ? "ACTIVE" : $"at {TemporalRechargePassiveImmunityAtPercent}%")}");
            if (p.PermanentPercent < TemporalRechargeMaxPercent)
            {
                double nextMin = TriangularMinutesForLevel(p.LevelsCreditedToday + 1);
                sb.AppendLine($"Today: +{p.LevelsCreditedToday}% so far, {p.SecondsTodayAtLowStability / 60.0:F1} min below {(int)(TemporalLowStabilityThreshold * 100)}% stability.");
                sb.AppendLine($"Next +1% today at {nextMin:F0} min total.");
            }
            sb.AppendLine($"Gear trick: +{TemporalRechargeGearTrickPercent}% per use. Hold a knife, put a temporal gear in your offhand, and hold right-click.");
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Testing helper: gets or sets the calling player's temporal stability (0.0 to 1.0).
        /// The vanilla tick will drift it back toward the ambient value over time.
        /// </summary>
        private TextCommandResult OnTraitTempStabilityCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Could not find player entity");

            var beh = player.Entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (beh == null) return TextCommandResult.Error("This entity has no temporal stability behavior.");

            string raw = args[0] as string;
            if (string.IsNullOrWhiteSpace(raw))
                return TextCommandResult.Success($"Current temporal stability: {beh.OwnStability:F3} (0.0 to 1.0).");

            if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                return TextCommandResult.Error("Usage: /trait tempstability [0.0-1.0]");

            val = Math.Clamp(val, 0.0, 1.0);
            beh.OwnStability = val;
            player.Entity.WatchedAttributes.MarkPathDirty("temporalStability");
            return TextCommandResult.Success($"Temporal stability set to {val:F3}. It drifts back toward the local ambient value over time (faster with Temporal Recharge).");
        }

        /// <summary>Admin: set the calling player's Temporal Resistance level directly.</summary>
        private TextCommandResult OnTraitSetTempResistCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Could not find player entity");

            int? lvl = args[0] as int?;
            var p = TemporalResistanceProgress.GetOrAdd(player.PlayerUID, _ => new TemporalProgressData());
            if (lvl == null)
                return TextCommandResult.Success($"Temporal Resistance: {p.PermanentPercent}% / {TemporalResistanceMaxPercent}%. Set with /trait tempresistlevel <n>.");

            int v = Math.Clamp(lvl.Value, 0, TemporalResistanceMaxPercent);
            p.PermanentPercent = v;
            pendingTemporalResistanceSave = true;
            return TextCommandResult.Success($"Temporal Resistance set to {v}% (temporal drain {v}% slower).");
        }

        /// <summary>Admin: set the calling player's Temporal Recharge level directly.</summary>
        private TextCommandResult OnTraitSetTempRechargeCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Could not find player entity");

            int? lvl = args[0] as int?;
            var p = TemporalRechargeProgress.GetOrAdd(player.PlayerUID, _ => new TemporalProgressData());
            if (lvl == null)
                return TextCommandResult.Success($"Temporal Recharge: {p.PermanentPercent}% / {TemporalRechargeMaxPercent}%. Set with /trait temprechargelevel <n>.");

            int v = Math.Clamp(lvl.Value, 0, TemporalRechargeMaxPercent);
            p.PermanentPercent = v;
            pendingTemporalRechargeSave = true;
            double mult = 1.0 + v / 100.0;
            return TextCommandResult.Success($"Temporal Recharge set to {v}% (recovery {mult:F2}x as fast).");
        }
    }

    // =======================================================================
    // HARMONY PATCH CONTAINERS
    // =======================================================================

    /// <summary>
    /// Scales temporal stability drain (Resistance) and recovery (Recharge), and grants
    /// passive-decay immunity, by rewriting the stability delta the vanilla tick applied.
    /// Server-authoritative: the temporalStability watched attribute syncs to clients.
    /// </summary>
    public static class TemporalStabilityPatches
    {
        public static void OnGameTick_Prefix(EntityBehaviorTemporalStabilityAffected __instance, out double __state)
        {
            __state = double.NaN;
            try
            {
                if (__instance?.entity == null) return;
                if (!SeraphLevelingModSystem.TemporalResistanceEnabled && !SeraphLevelingModSystem.TemporalRechargeEnabled) return;
                __state = __instance.OwnStability;
            }
            catch
            {
                __state = double.NaN;
            }
        }

        public static void OnGameTick_Postfix(EntityBehaviorTemporalStabilityAffected __instance, double __state)
        {
            if (double.IsNaN(__state)) return;

            try
            {
                var entity = __instance?.entity;
                if (entity?.Api == null || entity.Api.Side != EnumAppSide.Server) return;

                var player = (entity as EntityPlayer)?.Player as IServerPlayer;
                if (player == null) return;

                double before = __state;
                double after = __instance.OwnStability;
                double delta = after - before;
                if (delta == 0.0) return;

                string uid = player.PlayerUID;
                bool storm = SeraphLevelingModSystem.IsTemporalStormActive();

                if (delta < 0.0)
                {
                    // Drain. Passive-decay immunity and resistance only outside storms,
                    // unless resistance is explicitly configured to work during storms.
                    if (!storm)
                    {
                        if (SeraphLevelingModSystem.HasPassiveDecayImmunity(uid))
                        {
                            __instance.OwnStability = before;
                            return;
                        }
                        double resist = SeraphLevelingModSystem.GetTemporalResistanceFraction(uid);
                        if (resist > 0.0)
                        {
                            __instance.OwnStability = before + delta * (1.0 - resist);
                        }
                    }
                    else if (SeraphLevelingModSystem.TemporalResistanceWorksDuringStorms)
                    {
                        double resist = SeraphLevelingModSystem.GetTemporalResistanceFraction(uid);
                        if (resist > 0.0)
                        {
                            __instance.OwnStability = before + delta * (1.0 - resist);
                        }
                    }
                }
                else
                {
                    // Recovery. Speed it up by the recharge fraction.
                    double recharge = SeraphLevelingModSystem.GetTemporalRechargeFraction(uid);
                    if (recharge > 0.0)
                    {
                        double boosted = before + delta * (1.0 + recharge);
                        __instance.OwnStability = Math.Min(1.0, boosted);
                    }
                }
            }
            catch
            {
                // Never let a patch exception break the vanilla stability tick.
            }
        }
    }

    /// <summary>
    /// Shortens bow draw time by inflating the secondsUsed the vanilla bow logic sees,
    /// scaled by the player's raw Ranged level (authoritative dict on the server, synced
    /// watched attribute on the client). Patched on both sides so prediction matches.
    /// </summary>
    public static class BowDrawSpeedPatches
    {
        /// <summary>
        /// True once the bow methods are patched in this process. In singleplayer the
        /// client and server run in one process with separate Harmony instances, and
        /// patching the same methods from both would run the prefix twice per call and
        /// double the speed scaling. The prefixes are side-aware, so one application
        /// covers both sides.
        /// </summary>
        public static bool PatchedInProcess;

        public static void OnHeldInteractStep_Prefix(ref float secondsUsed, EntityAgent byEntity)
        {
            secondsUsed = Scale(secondsUsed, byEntity);
            ApplyAimAssist(byEntity);
        }

        public static void OnHeldInteractStop_Prefix(ref float secondsUsed, EntityAgent byEntity)
        {
            float before = secondsUsed;
            secondsUsed = Scale(secondsUsed, byEntity);
            ApplyAimAssist(byEntity);
            LogDraw(byEntity, "Stop", before, secondsUsed);
        }

        private static float Scale(float secondsUsed, EntityAgent byEntity)
        {
            try
            {
                if (!SeraphLevelingModSystem.RangedDrawSpeedEnabled || byEntity == null) return secondsUsed;

                // Scale by the player's raw Ranged level (credits). The damage-bonus
                // percent is capped and offset by Focused, so it is not usable here.
                int level = GetRangedLevelForEntity(byEntity);
                if (level <= 0) return secondsUsed;

                int atLevel = Math.Max(1, SeraphLevelingModSystem.RangedDrawSpeedAtLevel);
                float progress = Math.Clamp(level / (float)atLevel, 0f, 1f);
                float maxReduction = SeraphLevelingModSystem.RangedDrawSpeedMaxReductionPercent / 100f;
                float reduction = Math.Clamp(progress * maxReduction, 0f, 0.99f);
                if (reduction <= 0f) return secondsUsed;

                return secondsUsed / (1f - reduction);
            }
            catch
            {
                return secondsUsed;
            }
        }

        private static int GetRangedLevelForEntity(EntityAgent byEntity)
        {
            if (byEntity == null) return 0;
            // Authoritative on the server: read the progress dict directly so it works even if
            // the watched attribute has not synced yet (e.g. right after /trait setplayer).
            if (byEntity.Api?.Side == EnumAppSide.Server)
            {
                string uid = (byEntity as EntityPlayer)?.PlayerUID;
                if (uid != null && SeraphLevelingModSystem.RangedProgress.TryGetValue(uid, out var p))
                    return p.TotalCredits;
            }
            // Client (or no server-side entry): the synced watched attribute.
            return byEntity.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, 0);
        }

        /// <summary>
        /// Raises the bow "aimingAccuracy" value (which the vanilla bow reads to compute arrow
        /// spread, and the client reticle reads to draw convergence) to a floor based on the
        /// player's Ranged level. Set in the prefix so it is in place before vanilla reads it,
        /// letting a high-Ranged player spam fire accurately without waiting for the reticle.
        /// </summary>
        private static void ApplyAimAssist(EntityAgent byEntity)
        {
            try
            {
                if (!SeraphLevelingModSystem.RangedAimAssistEnabled || byEntity == null) return;

                int level = GetRangedLevelForEntity(byEntity);
                if (level <= 0) return;

                int atLevel = Math.Max(1, SeraphLevelingModSystem.RangedDrawSpeedAtLevel);
                float progress = Math.Clamp(level / (float)atLevel, 0f, 1f);
                float floor = Math.Clamp(progress * (SeraphLevelingModSystem.RangedAccuracyMaxPercent / 100f), 0f, 1f);
                if (floor <= 0f) return;

                // Only ever raise it, never lower the player's natural aim.
                float current = byEntity.Attributes.GetFloat("aimingAccuracy", 0f);
                if (floor > current)
                {
                    byEntity.Attributes.SetFloat("aimingAccuracy", floor);
                }
            }
            catch { }
        }

        private static void LogDraw(EntityAgent byEntity, string phase, float before, float after)
        {
            if (!SeraphLevelingModSystem.DebugLoggingEnabled) return;
            try
            {
                var api = byEntity?.Api;
                if (api == null) return;
                int watched = byEntity.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, -999);
                int used = GetRangedLevelForEntity(byEntity);
                float acc = byEntity.Attributes.GetFloat("aimingAccuracy", -1f);
                api.Logger.Debug(
                    $"[SeraphLeveling][bowdraw] {phase} side={api.Side} enabled={SeraphLevelingModSystem.RangedDrawSpeedEnabled} " +
                    $"levelUsed={used} watchedLevel={watched} atLevel={SeraphLevelingModSystem.RangedDrawSpeedAtLevel} " +
                    $"maxRed%={SeraphLevelingModSystem.RangedDrawSpeedMaxReductionPercent} aimAssist={SeraphLevelingModSystem.RangedAimAssistEnabled} " +
                    $"aimingAccuracy={acc:F3} secs {before:F3}->{after:F3}");
            }
            catch { }
        }

        /// <summary>Mirror the server-side bow patch on the client so draw prediction matches.</summary>
        public static void ApplyClient(Harmony harmony, ICoreClientAPI api)
        {
            if (PatchedInProcess) return;
            try
            {
                var step = AccessTools.Method(typeof(ItemBow), "OnHeldInteractStep");
                var stop = AccessTools.Method(typeof(ItemBow), "OnHeldInteractStop");
                var stepPrefix = AccessTools.Method(typeof(BowDrawSpeedPatches), nameof(OnHeldInteractStep_Prefix));
                var stopPrefix = AccessTools.Method(typeof(BowDrawSpeedPatches), nameof(OnHeldInteractStop_Prefix));
                if (step != null) harmony.Patch(step, prefix: new HarmonyMethod(stepPrefix));
                if (stop != null) harmony.Patch(stop, prefix: new HarmonyMethod(stopPrefix));
                PatchedInProcess = true;
                api.Logger.Debug("[SeraphLeveling] Patched ItemBow draw speed (client)");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch ItemBow draw speed (client): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Client-only: forces the aim reticle to converge based on the player's Ranged level by
    /// raising "aimingAccuracy" each frame right before the vanilla reticle renderer reads it.
    /// The vanilla ramp overwrites that value every frame, so setting it only during the bow
    /// draw tick is not enough to keep the on-screen crosshair tight.
    /// </summary>
    public static class AimReticlePatches
    {
        public static ICoreClientAPI ClientApi;

        public static void OnRenderAim_Prefix()
        {
            try
            {
                if (!SeraphLevelingModSystem.RangedAimAssistEnabled) return;

                var plr = ClientApi?.World?.Player?.Entity;
                if (plr == null) return;
                if (plr.Attributes.GetInt("aiming", 0) == 0) return;

                var held = plr.RightHandItemSlot?.Itemstack?.Collectible;
                if (!(held is ItemBow)) return;

                int level = plr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, 0);
                if (level <= 0) return;

                int atLevel = Math.Max(1, SeraphLevelingModSystem.RangedDrawSpeedAtLevel);
                float progress = Math.Clamp(level / (float)atLevel, 0f, 1f);
                float floor = Math.Clamp(progress * (SeraphLevelingModSystem.RangedAccuracyMaxPercent / 100f), 0f, 1f);
                if (floor <= 0f) return;

                float current = plr.Attributes.GetFloat("aimingAccuracy", 0f);
                if (floor > current) plr.Attributes.SetFloat("aimingAccuracy", floor);
            }
            catch { }
        }

        public static void Apply(Harmony harmony, ICoreClientAPI api)
        {
            ClientApi = api;
            try
            {
                var type = AccessTools.TypeByName("Vintagestory.Client.NoObf.SystemRenderPlayerAimAcc");
                if (type == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find SystemRenderPlayerAimAcc for reticle convergence");
                    return;
                }
                var method = AccessTools.Method(type, "OnRenderFrame2DOverlay");
                if (method == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find OnRenderFrame2DOverlay for reticle convergence");
                    return;
                }
                var prefix = AccessTools.Method(typeof(AimReticlePatches), nameof(OnRenderAim_Prefix));
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                api.Logger.Debug("[SeraphLeveling] Patched aim reticle convergence (client)");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch aim reticle convergence: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Speeds up melee swings by scaling the swing animation's playback speed (smooth), rather than
    /// cutting and restarting it (which chops/vibrates). When a player's hit animation starts, swap
    /// its metadata for a clone whose AnimationSpeed is scaled by Melee level. The swing plays
    /// faster, finishes sooner (faster re-swing), and the hit timing follows automatically because
    /// getHitDamageAtFrame divides by AnimationSpeed. Patched on both sides.
    /// </summary>
    public static class MeleeAttackSpeedPatches
    {
        private static AccessTools.FieldRef<AnimationManager, Entity> animMgrEntity;

        public static void StartAnimation_Prefix(AnimationManager __instance, ref AnimationMetaData animdata)
        {
            try
            {
                if (!SeraphLevelingModSystem.MeleeAttackSpeedEnabled || animdata == null || animMgrEntity == null) return;

                Entity ent = animMgrEntity(__instance);
                if (!(ent is EntityPlayer eplr)) return;

                float mult = GetMeleeSpeedMultiplier(eplr);
                if (mult <= 1f) return;

                // Only the held hit (swing) animation, not idle/walk/use/etc.
                var coll = eplr.RightHandItemSlot?.Itemstack?.Collectible;
                string hitAnim = coll != null ? coll.GetHeldTpHitAnimation(eplr.RightHandItemSlot, eplr) : "breakhand";

                bool isHit = !string.IsNullOrEmpty(hitAnim) && (
                    string.Equals(animdata.Animation, hitAnim, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(animdata.Code, hitAnim, StringComparison.OrdinalIgnoreCase));

                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    ent.Api?.Logger?.Debug(
                        $"[SeraphLeveling][melee] StartAnimation anim='{animdata.Animation}' code='{animdata.Code}' " +
                        $"hitAnim='{hitAnim}' isHit={isHit} mult={mult:F2} side={ent.Api?.Side}");
                }

                if (!isHit) return;

                // Clone so we never mutate the shared entity-type metadata (would affect all players).
                var clone = animdata.Clone();
                clone.AnimationSpeed *= mult;
                animdata = clone;
            }
            catch { }
        }

        private static float GetMeleeSpeedMultiplier(EntityAgent byEntity)
        {
            int level = GetMeleeLevelForEntity(byEntity);
            if (level <= 0) return 1f;
            int atLevel = Math.Max(1, SeraphLevelingModSystem.MeleeAttackSpeedAtLevel);
            float progress = Math.Clamp(level / (float)atLevel, 0f, 1f);
            float reduction = Math.Clamp(progress * (SeraphLevelingModSystem.MeleeAttackSpeedMaxReductionPercent / 100f), 0f, 0.9f);
            if (reduction <= 0f) return 1f;
            return 1f / (1f - reduction); // reduction 0.5 -> 2x swing speed
        }

        private static int GetMeleeLevelForEntity(EntityAgent byEntity)
        {
            if (byEntity == null) return 0;
            if (byEntity.Api?.Side == EnumAppSide.Server)
            {
                string uid = (byEntity as EntityPlayer)?.PlayerUID;
                if (uid != null && SeraphLevelingModSystem.MeleeProgress.TryGetValue(uid, out var p))
                    return p.TotalCredits;
            }
            return byEntity.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, 0);
        }

        /// <summary>See BowDrawSpeedPatches.PatchedInProcess: one application covers both sides.</summary>
        private static bool patchedInProcess;

        /// <summary>Called from Dispose so the next world load in the same process re-patches.</summary>
        public static void ResetPatchGuard() => patchedInProcess = false;

        public static void Apply(Harmony harmony, ICoreAPI api, string side)
        {
            if (patchedInProcess) return;
            try
            {
                if (animMgrEntity == null)
                    animMgrEntity = AccessTools.FieldRefAccess<AnimationManager, Entity>("entity");

                var method = AccessTools.Method(typeof(AnimationManager), "StartAnimation", new[] { typeof(AnimationMetaData) });
                var prefix = AccessTools.Method(typeof(MeleeAttackSpeedPatches), nameof(StartAnimation_Prefix));
                if (method != null) harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                patchedInProcess = true;
                api.Logger.Debug($"[SeraphLeveling] Patched melee swing animation speed ({side})");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch melee swing animation speed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Grants Temporal Recharge progress when the player completes the vanilla knife + offhand
    /// temporal gear gesture (the point where vanilla applies the stability boost). Server only.
    /// </summary>
    public static class KnifeGearTrickPatches
    {
        private const string CreditedFlag = "seraphGearTrickCredited";

        public static void OnHeldInteractStart_Postfix(EntityAgent byEntity)
        {
            try
            {
                byEntity?.Attributes?.SetBool(CreditedFlag, false);
            }
            catch { }
        }

        public static void OnHeldInteractStep_Postfix(float secondsUsed, EntityAgent byEntity)
        {
            try
            {
                if (!SeraphLevelingModSystem.TemporalRechargeEnabled) return;
                if (byEntity?.Api == null || byEntity.Api.Side != EnumAppSide.Server) return;
                if (secondsUsed < 1.95f) return;

                // Vanilla only sets these on the gear-insert gesture, and on the server
                // stabPlayed flips true exactly when the stability boost is applied.
                if (!byEntity.Attributes.GetBool("isInsertGear", false)) return;
                if (!byEntity.Attributes.GetBool("stabPlayed", false)) return;
                if (byEntity.Attributes.GetBool(CreditedFlag, false)) return;

                byEntity.Attributes.SetBool(CreditedFlag, true);

                var player = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                if (player == null) return;

                SeraphLevelingModSystem.GrantRechargeFromGearTrick(player);
            }
            catch { }
        }
    }
}
