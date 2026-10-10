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

        // Sleep tracking - key is playerUid, value is Calendar.TotalHours when the player mounted a bed.
        // Written by DidMount_Postfix, consumed by DidUnmount_Postfix to verify real sleep.
        internal static ConcurrentDictionary<string, double> SleepMountHours = new ConcurrentDictionary<string, double>();

        // Sleep buff settings
        public static bool EnableSleepBuff = false;
        public static float SleepBuffLinenBedMultiplier = 2.0f;
        public static float SleepBuffHayBedMultiplier = 1.5f;
        public static double SleepBuffDurationDays = 1.0;

        // Sleep buff tracking - maps player UID to their buff expiration time (in game days)
        public static ConcurrentDictionary<string, double> SleepBuffExpiration = new ConcurrentDictionary<string, double>();
        public static ConcurrentDictionary<string, float> SleepBuffMultiplier = new ConcurrentDictionary<string, float>();
        private const string SLEEP_BUFF_SAVE_KEY = "sitSleepBuff";
        internal static volatile bool pendingSleepBuffSave = false;

        // Dedup tracking for sleep buff messages (prevents double message from head+foot bed parts)
        internal static ConcurrentDictionary<string, long> LastSleepBuffApplyTick = new ConcurrentDictionary<string, long>();

        /// <summary>
        /// Handler for /trait sleepbuff command. Shows sleep buff status.
        /// </summary>
        private TextCommandResult OnTraitSleepBuffCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();

            if (!EnableSleepBuff)
            {
                sb.AppendLine("Sleep Buff: DISABLED (not enabled in config)");
                return TextCommandResult.Success(sb.ToString());
            }

            sb.AppendLine("Sleep Buff: ENABLED");
            sb.AppendLine($"  Linen/Old bed multiplier: {SleepBuffLinenBedMultiplier:F1}x");
            sb.AppendLine($"  Hay bed multiplier: {SleepBuffHayBedMultiplier:F1}x");
            sb.AppendLine($"  Duration: {SleepBuffDurationDays:F1} in-game days");
            sb.AppendLine();

            string playerUid = player.PlayerUID;
            double currentDay = ServerApi?.World?.Calendar?.TotalDays ?? 0;

            if (SleepBuffExpiration.TryGetValue(playerUid, out double expiration) &&
                SleepBuffMultiplier.TryGetValue(playerUid, out float multiplier) &&
                currentDay < expiration)
            {
                double remainingDays = expiration - currentDay;
                double remainingHours = remainingDays * 24;
                sb.AppendLine($"  Status: ACTIVE");
                sb.AppendLine($"  Current multiplier: {multiplier:F1}x");
                sb.AppendLine($"  Time remaining: {remainingDays:F2} days ({remainingHours:F1} in-game hours)");
            }
            else
            {
                sb.AppendLine($"  Status: INACTIVE");
                sb.AppendLine($"  Sleep in a bed to activate the buff!");
            }

            return TextCommandResult.Success(sb.ToString());
        }
    }
}
