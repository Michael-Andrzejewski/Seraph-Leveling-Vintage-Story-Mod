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

        /// <summary>
        /// The full report for the handbook page: the /trait all summary first,
        /// then every skill's own status text (the same text /trait mining,
        /// /trait melee and so on print), so per-tool progress and points to
        /// the next level are visible without a command.
        /// </summary>
        public static string BuildProgressReport(IPlayer player)
        {
            var sb = new StringBuilder();
            sb.AppendLine(BuildProgressSummary(player));

            var inst = Instance;
            if (inst == null) return sb.ToString().TrimEnd();

            var args = new TextCommandCallingArgs
            {
                Caller = new Caller { Player = player, FromChatGroupId = GlobalConstants.GeneralChatGroup }
            };

            void Section(string title, System.Func<TextCommandCallingArgs, TextCommandResult> handler)
            {
                try
                {
                    var result = handler(args);
                    if (result == null || string.IsNullOrWhiteSpace(result.StatusMessage)) return;
                    sb.AppendLine();
                    sb.AppendLine($"--- {title} ---");
                    sb.AppendLine(result.StatusMessage.TrimEnd());
                }
                catch (Exception ex)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- {title} ---");
                    sb.AppendLine("(could not read: " + ex.Message + ")");
                }
            }

            Section("Mining", inst.OnTraitMiningCommand);
            Section("Melee", inst.OnTraitMeleeCommand);
            Section("Ranged", inst.OnTraitRangedCommand);
            Section("Walking", inst.OnTraitWalkingCommand);
            Section("Hunger", inst.OnTraitHungerCommand);
            Section("Armor", inst.OnTraitArmorCommand);
            Section("Clothier", inst.OnTraitClothierCommand);
            Section("Mender", inst.OnTraitMenderCommand);
            Section("Pilferer", inst.OnTraitPilfererCommand);
            Section("Resourceful", inst.OnTraitResourcefulCommand);
            Section("Forager", inst.OnTraitForagerCommand);
            Section("Furtive", inst.OnTraitFurtiveCommand);
            Section("Precise", inst.OnTraitPreciseCommand);
            Section("Technical", inst.OnTraitTechnicalCommand);
            Section("Hardy Health", inst.OnTraitHardyHealthCommand);
            Section("Bowyer", inst.OnTraitBowyerCommand);
            Section("Improviser", inst.OnTraitImproviserCommand);
            Section("Tinkerer", inst.OnTraitTinkererCommand);
            Section("Merciless", inst.OnTraitMercilessCommand);
            Section("Claustrophobic", inst.OnTraitClaustrophobicCommand);
            if (IsCombatOverhaulLoaded) Section("Combat Overhaul proficiencies", inst.OnTraitCOProficiencyCommand);
            Section("Temporal Resistance", inst.OnTraitTempResistCommand);
            Section("Temporal Recharge", inst.OnTraitTempRechargeCommand);
            Section("Skill decay", inst.OnTraitDecayCommand);
            Section("Sleep buff", inst.OnTraitSleepBuffCommand);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The full progression report for a player, one line per skill. Used by
        /// /trait all and pushed to the client as a watched attribute so the
        /// handbook page "Seraph Leveling: My Progress" can show it without a
        /// command.
        /// </summary>
        public static string BuildProgressSummary(IPlayer player)
        {
            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            sb.AppendLine("=== All Trait Progression ===");

            // Progression traits
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningMax = GetMaxMiningCredits(player.Entity);
            sb.AppendLine($"Mining: {miningProg.TotalCredits}/{miningMax} (+{CalculateMiningBonusPercent(miningProg.TotalCredits)}% speed)");

            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            sb.AppendLine($"Melee: {meleeProg.TotalCredits}/{MaxMeleeDamagePercent} (+{meleeProg.TotalCredits}% damage)");

            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            sb.AppendLine($"Ranged: {rangedProg.TotalCredits}/{MaxRangedDamagePercent} (+{rangedProg.TotalCredits}% dmg, +{rangedProg.TotalCredits}% acc, +{rangedProg.TotalCredits}% dist)");

            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData { CurrentIncrementSize = BaseBlocksWalkedPerIncrement });
            sb.AppendLine($"Walking: {walkingProg.TotalCredits}/{MaxWalkingSpeedPercent} (+{walkingProg.TotalCredits}% speed)");

            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData { CurrentIncrementSize = BaseSecondsPerIncrement });
            sb.AppendLine($"Hunger: {hungerProg.TotalCredits}/{MaxHungerReductionPercent} (-{hungerProg.TotalCredits}% hunger rate)");

            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            sb.AppendLine($"Armor: +{armorProg.TotalDurabilityCredits}/{MaxArmorDurabilityPercent}% durability, -{armorProg.TotalWalkSpeedCredits}/{MaxArmorWalkSpeedPercent}% walk penalty");

            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData { CurrentIncrementSize = BaseMenderRepairsPerIncrement });
            sb.AppendLine($"Mender: {menderProg.TotalCredits}/{MaxMenderPercent} (+{menderProg.TotalCredits}% repair bonus)");

            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData { CurrentIncrementSize = BasePilfererPointsPerIncrement });
            sb.AppendLine($"Pilferer: {pilfererProg.TotalCredits}/{MaxPilfererPercent} (+{pilfererProg.TotalCredits}% vessel loot)");

            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData { CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement });
            sb.AppendLine($"Resourceful: {resourcefulProg.TotalCredits}/{MaxResourcefulLootPercent} (+{resourcefulProg.TotalCredits}% animal loot)");

            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData { CurrentIncrementSize = BaseForagerCropsPerIncrement });
            sb.AppendLine($"Forager: {foragerProg.TotalCredits}/{MaxForagerLootPercent} (+{foragerProg.TotalCredits}% foraging loot)");

            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData { CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement });
            sb.AppendLine($"Furtive: {furtiveProg.TotalCredits}/{MaxFurtivePercent} (-{furtiveProg.TotalCredits}% detection range)");

            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            sb.AppendLine($"Precise: {preciseProg.TotalCredits}/{MaxPrecisePercent} (+{preciseProg.TotalCredits}% mechanical dmg)");

            // Unlock traits
            sb.AppendLine("\n--- Unlock Traits ---");

            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            sb.AppendLine($"Clothier: {clothierProg.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes} clothes ({(clothierProg.SewingKitUnlocked ? "UNLOCKED" : "locked")})");

            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            sb.AppendLine($"Technical: {technicalProg.TranslocatorsRepaired}/{TechnicalRequiredTranslocatorRepairs} translocators ({(technicalProg.IsUnlocked ? "UNLOCKED" : "locked")})");

            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            sb.AppendLine($"Hardy Health: {(hardyHealthProg.IsUnlocked ? "UNLOCKED" : "locked")}");

            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            sb.AppendLine($"Bowyer: {(bowyerProg.IsUnlocked ? "UNLOCKED" : $"{bowyerProg.TotalBowDamage:F0} bow damage (locked)")}");

            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            sb.AppendLine($"Improviser: {(improviserProg.IsUnlocked ? "UNLOCKED" : $"{improviserProg.TotalRockDamage:F0} rock damage (locked)")}");

            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            sb.AppendLine($"Tinkerer: {(tinkererProg.IsUnlocked ? "UNLOCKED" : "locked")}");

            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            sb.AppendLine($"Merciless: {(mercilessProg.IsUnlocked ? "UNLOCKED" : "locked")}");

            return sb.ToString().TrimEnd();
        }

        // Last report sent to each online player this session, so the 15 s
        // refresh only sends when something changed. In memory on purpose: a
        // saved attribute survived restarts and made the push think the client
        // already had the report when the client had nothing.
        private static readonly ConcurrentDictionary<string, string> LastSentProgressReport = new ConcurrentDictionary<string, string>();

        // Players whose progress changed since the last push. A 100 ms server tick
        // flushes them, so a mined block is on the handbook page before the player
        // can open it (the single-player pause stops the server, so waiting for the
        // page to ask would be too late), and one hit that trains several skills
        // sends one packet instead of three.
        private static readonly ConcurrentDictionary<string, IServerPlayer> ProgressReportDirty = new ConcurrentDictionary<string, IServerPlayer>();

        // When each player's handbook page last asked for the report
        // (Environment.TickCount64). The open page asks once a second.
        private static readonly ConcurrentDictionary<string, long> ProgressPageLastRequestMs = new ConcurrentDictionary<string, long>();
        private const long ProgressPageOpenWindowMs = 3000;   // three missed polls = page closed
        private const long ProgressRequestMinGapMs = 250;     // honest clients ask once a second

        /// <summary>
        /// Whether to push the report without being asked: only to players whose
        /// handbook page asked in the last few seconds (an open page asks once a
        /// second). A closed page asks when it opens and gets the fresh report one
        /// round trip later. In single player the handbook pauses the server, so
        /// the page builds the report itself in-process instead
        /// (SeraphProgressPage.BuildInProcess). Building the report runs every
        /// /trait status handler (several KB of text), so nobody pays for it per
        /// block or hit unless they are looking at it.
        /// </summary>
        private static bool ShouldPushUnasked(IServerPlayer player)
        {
            return player?.PlayerUID != null
                && ProgressPageLastRequestMs.TryGetValue(player.PlayerUID, out long last)
                && Environment.TickCount64 - last < ProgressPageOpenWindowMs;
        }

        /// <summary>Network handler: the handbook page asks for the report (on open, then once a second).</summary>
        private static void OnProgressReportRequest(IServerPlayer player, ProgressReportRequestMessage msg)
        {
            if (player?.PlayerUID == null) return;
            long now = Environment.TickCount64;
            bool tooSoon = ProgressPageLastRequestMs.TryGetValue(player.PlayerUID, out long last) && now - last < ProgressRequestMinGapMs;
            ProgressPageLastRequestMs[player.PlayerUID] = now;
            if (tooSoon) return;   // a spamming client must not force a full build per packet
            PushProgressReport(player, force: msg?.Force ?? false);
        }

        /// <summary>Progress changed for this player: push the report on the next flush.</summary>
        public static void MarkProgressChanged(IServerPlayer player)
        {
            if (player?.PlayerUID != null && ShouldPushUnasked(player))
            {
                ProgressReportDirty[player.PlayerUID] = player;
                reportsQueued = true;
            }
        }

        // Set when a report is queued. The 100 ms flush checks only this flag:
        // ConcurrentDictionary.IsEmpty takes every lock of the table.
        private static volatile bool reportsQueued;

        private static void FlushProgressReports()
        {
            if (!reportsQueued) return;
            reportsQueued = false;
            foreach (var key in ProgressReportDirty.Keys.ToList())
            {
                if (ProgressReportDirty.TryRemove(key, out var player) && player?.Entity != null) PushProgressReport(player);
            }
        }

        /// <summary>
        /// Send the progression report to the player's client over the mod
        /// channel for the handbook page "Seraph Leveling: My Progress".
        /// force = true always sends (join); otherwise only when it changed.
        /// </summary>
        public static void PushProgressReport(IServerPlayer player, bool force = false)
        {
            try
            {
                if (player?.Entity == null || serverSoundChannel == null) return;
                string report = BuildProgressReport(player);
                if (!force && LastSentProgressReport.TryGetValue(player.PlayerUID, out string last) && last == report) return;
                serverSoundChannel.SendPacket(new ProgressReportMessage { Report = report }, player);
                LastSentProgressReport[player.PlayerUID] = report;
            }
            catch (Exception ex)
            {
                ServerApi?.Logger?.Warning($"[SeraphLeveling] Could not sync the progress report for {player?.PlayerName}: {ex.Message}");
            }
        }
    }
}
