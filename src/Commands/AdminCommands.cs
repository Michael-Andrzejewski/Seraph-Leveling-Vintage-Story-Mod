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
        /// Handler for /trait command (shows help).
        /// </summary>
        /// <summary>
        /// Value of an optional integer argument, or null when the player left it
        /// out. The engine's IntArgParser.GetValue() returns a boxed default (0)
        /// for a missing optional argument instead of null, so "OptInt(args, 0)"
        /// always has a value and every "show or set" command took the set branch:
        /// a bare /trait mininglevel wiped the caller's mining credits to zero.
        /// </summary>
        internal static int? OptInt(TextCommandCallingArgs args, int index)
        {
            if (args?.Parsers == null || index >= args.Parsers.Count) return null;
            if (args.Parsers[index] is ArgumentParserBase p && p.IsMissing) return null;
            return (int?)args[index];
        }

        /// <summary>
        /// The /trait config commands that show or set one number: "/trait x"
        /// replies with showReply, "/trait x N" checks N against minimum (error
        /// belowMinimumError), stores it, marks the config for saving, runs
        /// afterSet (for example to reapply everyone's bonuses) and replies with
        /// setReply. Replies are built after the value is stored, so they show it.
        /// </summary>
        private TextCommandResult ShowOrSetConfig(TextCommandCallingArgs args, int minimum, string belowMinimumError,
            Action<int> set, Func<string> setReply, Func<string> showReply, Action afterSet = null)
        {
            int? newValue = OptInt(args, 0);
            if (!newValue.HasValue) return TextCommandResult.Success(showReply());
            if (newValue.Value < minimum) return TextCommandResult.Error(belowMinimumError);
            set(newValue.Value);
            pendingConfigSave = true;
            afterSet?.Invoke();
            return TextCommandResult.Success(setReply());
        }

        private TextCommandResult OnTraitHelpCommand(TextCommandCallingArgs args)
        {
            // Built from the registered subcommands so the list can never fall
            // behind the commands again (the hand-written list covered 45 of 114).
            var cmd = ServerApi?.ChatCommands.Get("trait");
            if (cmd?.AllSubcommands == null || cmd.AllSubcommands.Count == 0)
                return TextCommandResult.Success("Usage: /trait &lt;subcommand&gt;. Try /trait all for your progress.");

            static string Esc(string t) => (t ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
            var player = new StringBuilder();
            var admin = new StringBuilder();
            foreach (var kv in cmd.AllSubcommands.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string desc = kv.Value.Description ?? "";
                bool isAdmin = desc.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0;
                (isAdmin ? admin : player).Append("  /trait ").Append(kv.Key).Append(" - ").Append(Esc(desc)).Append('\n');
            }
            return TextCommandResult.Success(
                "Usage: /trait &lt;subcommand&gt;. The handbook page \"Seraph Leveling: My Progress\" shows everything at once.\n" +
                "Player commands:\n" + player +
                "Admin commands:\n" + admin.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait soundvolume command.
        /// Gets or sets the level-up ding volume (0.0 to 1.0) and persists it to the config file.
        /// </summary>
        private TextCommandResult OnTraitSoundVolumeCommand(TextCommandCallingArgs args)
        {
            string raw = args[0] as string;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return TextCommandResult.Success(
                    $"Current level-up sound volume: {LevelUpSoundVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} (0.0 silent, 1.0 full, default 0.25). The scale is exponential, so 0.5 is close to 1.0 and 0.05 is about half as loud as 0.25. Set it with /trait soundvolume 0.25");
            }

            if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float newVolume))
            {
                return TextCommandResult.Error("Volume must be a number between 0.0 and 1.0. For example: /trait soundvolume 0.25");
            }

            if (newVolume < 0f || newVolume > 1f)
            {
                return TextCommandResult.Error("Volume must be between 0.0 and 1.0. The scale is exponential: 0.25 is a comfortable medium, 0.05 is quiet, 0.5 sounds close to 1.0.");
            }

            LevelUpSoundVolume = newVolume;
            SaveLevelUpSoundVolumeToConfig();

            return TextCommandResult.Success(
                $"Level-up sound volume set to {LevelUpSoundVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}. This has been saved to the config file.");
        }

        /// <summary>
        /// Handler for /trait testsound command.
        /// Sends a single level-up sound packet to the calling player at the requested volume
        /// (or the currently configured volume if no value is given). Marks the packet as a test
        /// so the client logs and prints what it actually received, isolating whether the volume
        /// reached the audio engine intact.
        /// </summary>
        private TextCommandResult OnTraitTestSoundCommand(TextCommandCallingArgs args)
        {
            string raw = args[0] as string;
            float volume = LevelUpSoundVolume;
            bool overridden = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out volume))
                {
                    return TextCommandResult.Error("Volume must be a number between 0.0 and 1.0. For example: /trait testsound 0.1");
                }
                if (volume < 0f || volume > 1f)
                {
                    return TextCommandResult.Error("Volume must be between 0.0 and 1.0. Use 0.0 for silent, 0.5 for half, 1.0 for full.");
                }
                overridden = true;
            }

            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null)
            {
                return TextCommandResult.Error("This command must be run by a player.");
            }

            if (serverSoundChannel == null)
            {
                return TextCommandResult.Error("Sound network channel not initialized. Cannot send test sound.");
            }

            string soundName = LevelUpSoundName;
            try
            {
                serverSoundChannel.SendPacket(new LevelUpSoundMessage
                {
                    SoundName = soundName,
                    Volume = volume,
                    IsTest = true
                }, caller);
            }
            catch (Exception ex)
            {
                return TextCommandResult.Error($"Failed to send test sound: {ex.Message}");
            }

            string volStr = volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string source = overridden ? "supplied" : "current config";
            ServerApi?.Logger.Notification($"[SeraphLeveling] /trait testsound by {caller.PlayerName}: sound={soundName}, volume={volStr} ({source})");
            return TextCommandResult.Success(
                $"Sent test sound '{soundName}' at volume {volStr} ({source}). Run /trait testsound 0.05 then /trait testsound 1.0 back to back to A/B test, and watch your chat for the volume the client reports receiving.");
        }

        /// <summary>
        /// Handler for /trait all command. Shows all trait progression in a single message.
        /// </summary>
        private TextCommandResult OnTraitAllCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }
            return TextCommandResult.Success(BuildProgressSummary(player));
        }

        /// <summary>
        /// Resolves a player by name (case-insensitive partial match).
        /// Returns null if not found online.
        /// </summary>
        private IServerPlayer ResolvePlayerByName(string playerName, bool exactOnly = false)
        {
            if (ServerApi?.World?.AllOnlinePlayers == null) return null;

            // Try exact match first (case-insensitive)
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                var sp = onlinePlayer as IServerPlayer;
                if (sp != null && string.Equals(sp.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                    return sp;
            }
            // Commands that wipe or overwrite progress never guess from a partial name.
            if (exactOnly) return null;

            // Try partial match
            IServerPlayer match = null;
            int matches = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                var sp = onlinePlayer as IServerPlayer;
                if (sp != null && sp.PlayerName.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = sp;
                    matches++;
                }
            }
            return matches == 1 ? match : null;
        }

        /// <summary>
        /// Handler for /trait setplayer command. Sets a trait level for a target player.
        /// Usage: /trait setplayer PlayerName trait level
        /// </summary>
        private TextCommandResult OnTraitSetPlayerCommand(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string traitName = ((string)args[1]).ToLowerInvariant();
            int level = (int)args[2];
            string toolName = (string)args[3];

            if (level < 0)
                return TextCommandResult.Error("Level cannot be negative.");

            // Online first (partial name match allowed). Offline players are matched
            // by their exact last known name only, so a typo cannot edit the wrong
            // player; their stored progress is edited directly and the live stats
            // catch up when they next join.
            var targetPlayer = ResolvePlayerByName(playerName, exactOnly: true);
            string targetUid;
            string targetName;
            if (targetPlayer != null)
            {
                targetUid = targetPlayer.PlayerUID;
                targetName = targetPlayer.PlayerName;
            }
            else
            {
                var offlineData = ServerApi?.PlayerData?.GetPlayerDataByLastKnownName(playerName);
                if (offlineData == null || string.IsNullOrEmpty(offlineData.PlayerUID))
                    return TextCommandResult.Error($"No online player matches '{playerName}' and no offline player has that exact name. Offline players must be matched by their full name.");
                targetUid = offlineData.PlayerUID;
                targetName = offlineData.LastKnownPlayername;
            }

            // Traits with per-tool support delegate to shared helpers
            switch (traitName)
            {
                case "mining":
                    return SetMiningLevelForPlayer(targetPlayer, targetUid, level, toolName);
                case "melee":
                    return SetMeleeLevelForPlayer(targetPlayer, targetUid, level, toolName);
                case "ranged":
                    return SetRangedLevelForPlayer(targetPlayer, targetUid, level, toolName);
                case "precise":
                    return SetPreciseLevelForPlayer(targetPlayer, targetUid, level, toolName);
                case "armor":
                    return SetArmorLevelForPlayer(targetPlayer, targetUid, level, toolName);
            }

            // Traits without per-tool support — reject toolName if provided
            if (toolName != null)
                return TextCommandResult.Error($"The '{traitName}' trait does not support per-tool level setting.");

            string result;

            switch (traitName)
            {
                case "walking":
                {
                    if (level > MaxWalkingSpeedPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxWalkingSpeedPercent}).");
                    var progress = WalkingProgress.GetOrAdd(targetUid, _ => new WalkingProgressData { CurrentIncrementSize = BaseBlocksWalkedPerIncrement });
                    progress.TotalCredits = level;
                    progress.BlocksInIncrement = 0;
                    progress.CurrentIncrementSize = BaseBlocksWalkedPerIncrement + (level * WalkingIncrementStep);
                    ApplyWalkingBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "walking");
                    result = $"Walking level set to {level} (+{level}% speed) for {targetName}.";
                    break;
                }
                case "tempresist":
                case "temporalresistance":
                {
                    if (level > TemporalResistanceMaxPercent) return TextCommandResult.Error($"Level cannot exceed max ({TemporalResistanceMaxPercent}).");
                    var progress = TemporalResistanceProgress.GetOrAdd(targetUid, _ => new TemporalProgressData());
                    progress.PermanentPercent = level;
                    result = $"Temporal Resistance set to {level}% (drain {level}% slower) for {targetName}.";
                    break;
                }
                case "temprecharge":
                case "temporalrecharge":
                {
                    if (level > TemporalRechargeMaxPercent) return TextCommandResult.Error($"Level cannot exceed max ({TemporalRechargeMaxPercent}).");
                    var progress = TemporalRechargeProgress.GetOrAdd(targetUid, _ => new TemporalProgressData());
                    progress.PermanentPercent = level;
                    result = $"Temporal Recharge set to {level}% (recovery {(1.0 + level / 100.0):F2}x) for {targetName}.";
                    break;
                }
                case "hunger":
                {
                    // Ravenous players have a higher per-player cap; use it when the
                    // entity is available (offline falls back to the global max).
                    int maxHungerCredits = targetPlayer?.Entity != null ? CalculateMaxHungerCredits(targetPlayer.Entity) : MaxHungerReductionPercent;
                    if (level > maxHungerCredits) return TextCommandResult.Error($"Level cannot exceed max ({maxHungerCredits}).");
                    var progress = HungerProgress.GetOrAdd(targetUid, _ => new HungerProgressData { CurrentIncrementSize = BaseSecondsPerIncrement });
                    progress.TotalCredits = level;
                    progress.SecondsInIncrement = 0;
                    progress.CurrentIncrementSize = BaseSecondsPerIncrement + (level * HungerIncrementStep);
                    ApplyHungerBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "hunger");
                    result = $"Hunger level set to {level} (-{level}% hunger rate) for {targetName}.";
                    break;
                }
                case "mender":
                {
                    if (level > MaxMenderPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxMenderPercent}).");
                    var progress = MenderProgress.GetOrAdd(targetUid, _ => new MenderProgressData { CurrentIncrementSize = BaseMenderRepairsPerIncrement });
                    progress.TotalCredits = level;
                    progress.RepairsInIncrement = 0;
                    progress.CurrentIncrementSize = BaseMenderRepairsPerIncrement + (level * MenderIncrementStep);
                    ApplyMenderBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "mender");
                    result = $"Mender level set to {level} (+{level}% repair) for {targetName}.";
                    break;
                }
                case "pilferer":
                {
                    if (level > MaxPilfererPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxPilfererPercent}).");
                    var progress = PilfererProgress.GetOrAdd(targetUid, _ => new PilfererProgressData { CurrentIncrementSize = BasePilfererPointsPerIncrement });
                    progress.TotalCredits = level;
                    progress.PointsInIncrement = 0;
                    progress.CurrentIncrementSize = BasePilfererPointsPerIncrement + (level * PilfererIncrementStep);
                    ApplyPilfererBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "pilferer");
                    result = $"Pilferer level set to {level} for {targetName}.";
                    break;
                }
                case "resourceful":
                {
                    if (level > MaxResourcefulLootPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxResourcefulLootPercent}).");
                    var progress = ResourcefulProgress.GetOrAdd(targetUid, _ => new ResourcefulProgressData { CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement });
                    progress.TotalCredits = level;
                    progress.AnimalsInIncrement = 0;
                    progress.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement + (level * ResourcefulIncrementStep);
                    ApplyResourcefulBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "resourceful");
                    result = $"Resourceful level set to {level} for {targetName}.";
                    break;
                }
                case "forager":
                {
                    if (level > MaxForagerLootPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxForagerLootPercent}).");
                    var progress = ForagerProgress.GetOrAdd(targetUid, _ => new ForagerProgressData { CurrentIncrementSize = BaseForagerCropsPerIncrement });
                    progress.TotalCredits = level;
                    progress.CropsInIncrement = 0;
                    progress.CurrentIncrementSize = BaseForagerCropsPerIncrement + (level * ForagerIncrementStep);
                    ApplyForagerBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "forager");
                    result = $"Forager level set to {level} for {targetName}.";
                    break;
                }
                case "furtive":
                {
                    if (level > MaxFurtivePercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxFurtivePercent}).");
                    var progress = FurtiveProgress.GetOrAdd(targetUid, _ => new FurtiveProgressData { CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement });
                    progress.TotalCredits = level;
                    progress.BlocksInIncrement = 0;
                    progress.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement + (level * FurtiveIncrementStep);
                    ApplyFurtiveBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "furtive");
                    result = $"Furtive level set to {level} (-{level}% detection) for {targetName}.";
                    break;
                }
                default:
                    return TextCommandResult.Error($"Unknown trait '{traitName}'. Valid traits: mining, melee, ranged, precise, armor, walking, hunger, mender, pilferer, resourceful, forager, furtive, tempresist, temprecharge");
            }

            if (targetPlayer == null)
            {
                result += " Offline player; the change applies when they next join.";
            }

            return TextCommandResult.Success(result);
        }

        /// <summary>
        /// Calculates credits earned by a tool from its CurrentIncrementSize.
        /// Credits = (currentIncrementSize - baseIncrement) / incrementStep
        /// </summary>
        private static int CalculateToolCredits(int currentIncrementSize, int baseIncrement, int incrementStep)
        {
            if (incrementStep <= 0) return 0;
            int credits = (currentIncrementSize - baseIncrement) / incrementStep;
            return Math.Max(0, credits);
        }

        /// <summary>
        /// Recalculates TotalCredits by summing credits from all per-tool entries.
        /// </summary>
        private static int RecalculateTotalCreditsFromTools<T>(
            Dictionary<string, T> toolDict,
            System.Func<T, int> getIncrementSize,
            int baseIncrement, int incrementStep)
        {
            if (incrementStep <= 0) return 0;
            int total = 0;
            foreach (var kvp in toolDict)
            {
                int toolCredits = (getIncrementSize(kvp.Value) - baseIncrement) / incrementStep;
                if (toolCredits > 0) total += toolCredits;
            }
            return total;
        }

        /// <summary>
        /// Sets per-tool credits for mining. Returns the result or null if the caller should proceed with total-level setting.
        /// </summary>
        private TextCommandResult SetMiningLevelForPlayer(IServerPlayer player, string playerUid, int level, string toolName)
        {
            int maxCredits = GetMaxMiningCredits(player?.Entity);
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-tool mode: set credits on a specific pickaxe without clearing others
                int oldToolCredits = 0;
                if (progress.PickaxeProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseBlocksPerIncrement, IncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.PickaxeProgress.Remove(toolName);
                }
                else
                {
                    var pickaxeProgress = progress.GetPickaxeProgress(toolName);
                    pickaxeProgress.CurrentIncrementSize = BaseBlocksPerIncrement + (level * IncrementStep);
                    pickaxeProgress.BlocksInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.PickaxeProgress, p => p.CurrentIncrementSize,
                    BaseBlocksPerIncrement, IncrementStep);

                UpdateSkillActivityDay(playerUid, "mining");
                if (player == null)
                    return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits}. Offline player; applies on next join.");

                int bonusPercent = ApplyMiningBonus(player, progress.TotalCredits);
                CheckHardyHealthUnlock(player);
                CheckClaustrophobicRemoval(player);

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{bonusPercent}% mining speed).");
            }
            else
            {
                // Total mode: set TotalCredits directly and clear per-tool progress
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.PickaxeProgress.Clear();

                UpdateSkillActivityDay(playerUid, "mining");
                if (player == null)
                    return TextCommandResult.Success($"Mining credits set to {level}. Per-pickaxe progress reset. Offline player; applies on next join.");

                int bonusPercent = ApplyMiningBonus(player, level);
                CheckHardyHealthUnlock(player);
                CheckClaustrophobicRemoval(player);

                return TextCommandResult.Success($"Mining credits set to {level} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for melee.
        /// </summary>
        private TextCommandResult SetMeleeLevelForPlayer(IServerPlayer player, string playerUid, int level, string toolName)
        {
            int maxCredits = GetMaxMeleeCredits(player?.Entity);
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseDamagePerIncrement, MeleeIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BaseDamagePerIncrement + (level * MeleeIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BaseDamagePerIncrement, MeleeIncrementStep);

                UpdateSkillActivityDay(playerUid, "melee");
                if (player == null)
                    return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits}. Offline player; applies on next join.");

                int bonusPercent = ApplyMeleeBonusStatic(player, progress.TotalCredits);
                CheckMercilessUnlock(player);

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{bonusPercent}% melee damage).");
            }
            else
            {
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                UpdateSkillActivityDay(playerUid, "melee");
                if (player == null)
                    return TextCommandResult.Success($"Melee credits set to {level}. Per-weapon progress reset. Offline player; applies on next join.");

                int bonusPercent = ApplyMeleeBonusStatic(player, level);
                CheckMercilessUnlock(player);

                return TextCommandResult.Success($"Melee credits set to {level} (+{bonusPercent}% melee damage). Per-weapon progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for ranged.
        /// </summary>
        private TextCommandResult SetRangedLevelForPlayer(IServerPlayer player, string playerUid, int level, string toolName)
        {
            int maxCredits = GetMaxRangedCredits(player?.Entity);
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseRangedDamagePerIncrement, RangedIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BaseRangedDamagePerIncrement + (level * RangedIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BaseRangedDamagePerIncrement, RangedIncrementStep);

                UpdateSkillActivityDay(playerUid, "ranged");
                if (player == null)
                    return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits}. Offline player; applies on next join.");

                var (dmg, acc, dist) = ApplyRangedBonusStatic(player, progress.TotalCredits);
                CheckBowyerUnlock(player);
                CheckImproviserUnlock(player);

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{dmg}% damage, +{acc}% accuracy, +{dist}% distance).");
            }
            else
            {
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                UpdateSkillActivityDay(playerUid, "ranged");
                if (player == null)
                    return TextCommandResult.Success($"Ranged credits set to {level}. Per-weapon progress reset. Offline player; applies on next join.");

                var (dmg, acc, dist) = ApplyRangedBonusStatic(player, level);
                CheckBowyerUnlock(player);
                CheckImproviserUnlock(player);

                return TextCommandResult.Success($"Ranged credits set to {level} (+{dmg}% damage, +{acc}% accuracy, +{dist}% distance). Per-weapon progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for precise.
        /// </summary>
        private TextCommandResult SetPreciseLevelForPlayer(IServerPlayer player, string playerUid, int level, string toolName)
        {
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            if (level < 0 || level > MaxPrecisePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxPrecisePercent}.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BasePreciseDamagePerIncrement, PreciseIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > MaxPrecisePercent)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({MaxPrecisePercent}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BasePreciseDamagePerIncrement + (level * PreciseIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BasePreciseDamagePerIncrement, PreciseIncrementStep);

                UpdateSkillActivityDay(playerUid, "precise");
                if (player == null)
                    return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{MaxPrecisePercent}. Offline player; applies on next join.");

                int bonusPercent = ApplyPreciseBonusStatic(player, progress.TotalCredits);
                CheckTinkererUnlock(player);

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{MaxPrecisePercent} (+{bonusPercent}% mechanical damage).");
            }
            else
            {
                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                UpdateSkillActivityDay(playerUid, "precise");
                if (player == null)
                    return TextCommandResult.Success($"Precise level set to {level}. Offline player; applies on next join.");

                int bonusPercent = ApplyPreciseBonusStatic(player, level);
                CheckTinkererUnlock(player);

                return TextCommandResult.Success($"Precise level set to {level} (+{bonusPercent}% mechanical damage).");
            }
        }

        /// <summary>
        /// Sets per-tool credits for armor durability.
        /// Armor has 3 credit streams (time, damage, repair) per piece, so per-piece setting
        /// distributes credits equally across all 3 streams for the specified armor piece.
        /// </summary>
        private TextCommandResult SetArmorLevelForPlayer(IServerPlayer player, string playerUid, int level, string toolName)
        {
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-piece mode: set durability credits on a specific armor piece.
                // Each credit stream (time, damage, repair) for this piece is set to the given level.
                // TotalDurabilityCredits = sum of all per-piece (time + damage + repair) credits.
                int oldPieceCredits = 0;
                if (progress.ArmorProgress.TryGetValue(toolName, out var existingPiece))
                {
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentTimeIncrementSize, BaseSecondsInArmorPerIncrement, ArmorTimeIncrementStep);
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentDamageIncrementSize, BaseDamageBlockedPerIncrement, ArmorDamageIncrementStep);
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentRepairIncrementSize, BaseRepairsPerIncrement, ArmorRepairIncrementStep);
                }

                // Each of the 3 streams gets 'level' credits, so total piece contribution is level * 3
                int newPieceCredits = level * 3;
                int projectedTotal = progress.TotalDurabilityCredits - oldPieceCredits + newPieceCredits;
                if (projectedTotal > MaxArmorDurabilityPercent)
                    return TextCommandResult.Error($"Setting {level} credits per stream on {toolName} would result in {projectedTotal} total durability credits, exceeding max ({MaxArmorDurabilityPercent}).");

                if (level == 0)
                {
                    progress.ArmorProgress.Remove(toolName);
                }
                else
                {
                    var pieceProgress = progress.GetArmorProgress(toolName);
                    pieceProgress.CurrentTimeIncrementSize = BaseSecondsInArmorPerIncrement + (level * ArmorTimeIncrementStep);
                    pieceProgress.SecondsWornInIncrement = 0;
                    pieceProgress.TimeCredits = level;
                    pieceProgress.CurrentDamageIncrementSize = BaseDamageBlockedPerIncrement + (level * ArmorDamageIncrementStep);
                    pieceProgress.DamageBlockedInIncrement = 0;
                    pieceProgress.DamageCredits = level;
                    pieceProgress.CurrentRepairIncrementSize = BaseRepairsPerIncrement + (level * ArmorRepairIncrementStep);
                    pieceProgress.RepairsInIncrement = 0;
                    pieceProgress.RepairCredits = level;
                    pieceProgress.HasBeenEquipped = true;
                }

                // Recalculate total durability credits from all armor pieces
                int total = 0;
                foreach (var kvp in progress.ArmorProgress)
                {
                    total += CalculateToolCredits(kvp.Value.CurrentTimeIncrementSize, BaseSecondsInArmorPerIncrement, ArmorTimeIncrementStep);
                    total += CalculateToolCredits(kvp.Value.CurrentDamageIncrementSize, BaseDamageBlockedPerIncrement, ArmorDamageIncrementStep);
                    total += CalculateToolCredits(kvp.Value.CurrentRepairIncrementSize, BaseRepairsPerIncrement, ArmorRepairIncrementStep);
                }
                progress.TotalDurabilityCredits = total;

                pendingArmorProgressSave = true;
                UpdateSkillActivityDay(playerUid, "armor");
                if (player == null)
                    return TextCommandResult.Success($"Set {level} credits per stream on {toolName}. Total durability: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent}. Offline player; applies on next join.");

                ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                int bonusPercent = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity);
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);

                return TextCommandResult.Success($"Set {level} credits per stream on {toolName}. Total durability: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent} (+{bonusPercent}% durability).");
            }
            else
            {
                if (level > MaxArmorDurabilityPercent)
                    return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorDurabilityPercent}).");

                progress.TotalDurabilityCredits = level;
                pendingArmorProgressSave = true;
                UpdateSkillActivityDay(playerUid, "armor");
                if (player == null)
                    return TextCommandResult.Success($"Armor durability credits set to {level}. Offline player; applies on next join.");

                ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                int bonusPercent = CalculateArmorDurabilityBonusPercent(level, player.Entity);
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);

                return TextCommandResult.Success($"Armor durability credits set to {level} (+{bonusPercent}% durability).");
            }
        }

        /// <summary>
        /// Sets per-tool credits for a Combat Overhaul proficiency.
        /// </summary>
        private TextCommandResult SetCOLevelForPlayer(IServerPlayer player, string proficiencyStat, int credits, string toolName)
        {
            string playerUid = player.PlayerUID;
            int maxCredits = proficiencyStat == CO_STEADY_AIM
                ? GetCOSteadyAimMaxCreditsForPlayer(playerUid)
                : GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);

            if (credits < 0 || credits > maxCredits)
                return TextCommandResult.Error($"Credits must be between 0 and {maxCredits} for {GetCOProficiencyDisplayName(proficiencyStat)}.");

            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            if (proficiencyStat == CO_STEADY_AIM)
            {
                if (toolName != null)
                    return TextCommandResult.Error("Steady Aim does not support per-tool level setting.");

                playerProgress.SteadyAimCredits = credits;
                ApplyCOSteadyAimBonus(player, credits);
            }
            else
            {
                int profBase = GetCOProficiencyBase(proficiencyStat);
                int profIncrement = GetCOProficiencyIncrement(proficiencyStat);
                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);

                if (toolName != null)
                {
                    int oldToolCredits = 0;
                    if (profProgress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                        oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, profBase, profIncrement);

                    int projectedTotal = profProgress.TotalCredits - oldToolCredits + credits;
                    if (projectedTotal > maxCredits)
                        return TextCommandResult.Error($"Setting {credits} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                    if (credits == 0)
                    {
                        profProgress.WeaponProgress.Remove(toolName);
                    }
                    else
                    {
                        var weaponProgress = profProgress.GetWeaponProgress(toolName, profBase);
                        weaponProgress.CurrentIncrementSize = profBase + (credits * profIncrement);
                        weaponProgress.DamageInIncrement = 0;
                    }

                    profProgress.TotalCredits = RecalculateTotalCreditsFromTools(
                        profProgress.WeaponProgress, w => w.CurrentIncrementSize,
                        profBase, profIncrement);

                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, profProgress.TotalCredits);
                }
                else
                {
                    profProgress.TotalCredits = credits;
                    profProgress.WeaponProgress.Clear();
                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
                }
            }

            UpdateSkillActivityDay(playerUid, "coproficiency");

            int finalCredits = proficiencyStat == CO_STEADY_AIM
                ? playerProgress.SteadyAimCredits
                : playerProgress.GetProficiencyProgress(proficiencyStat).TotalCredits;
            float bonus = CalculateCOProficiencyBonus(finalCredits, GetCOProficiencyMax(proficiencyStat));
            string toolSuffix = toolName != null ? $" on {toolName}. Total: {finalCredits}" : $" to {credits}";
            return TextCommandResult.Success($"Set {GetCOProficiencyDisplayName(proficiencyStat)}{toolSuffix} credits (+{bonus * 100:F0}%).");
        }

        /// <summary>
        /// Handler for /trait testwalkspeed command.
        /// Applies a test armor walk speed penalty reduction (positive = less penalty, 0 = clear).
        /// </summary>
        private TextCommandResult OnTraitTestWalkSpeedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Player entity not found");
            }

            int? percent = OptInt(args, 0);

            if (!percent.HasValue)
            {
                return TextCommandResult.Success("Usage: /trait testwalkspeed [percent]\nExample: /trait testwalkspeed 99 (reduces armor penalty by 99%)\nUse 0 to clear the test modifier.");
            }

            if (percent.Value == 0)
            {
                player.Entity.Stats["armorWalkSpeedAffectedness"].Remove("sitTestPenalty");

                // Force WearableStats to recalculate
                var clearInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (clearInv != null)
                {
                    foreach (var slot in clearInv)
                    {
                        if (slot?.Itemstack != null)
                        {
                            slot.MarkDirty();
                            break;
                        }
                    }
                }

                return TextCommandResult.Success("Test armor walk speed penalty modifier cleared.");
            }

            // armorWalkSpeedAffectedness: negative values reduce the penalty
            float reduction = -(percent.Value * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set("sitTestPenalty", reduction);

            // Debug: check blended value
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] Test command: set armorWalkSpeedAffectedness modifier to {reduction:F2}, blended value is now {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering a slot change on character inventory
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                // Trigger slot modified on first slot to force WearableStats recalculation
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
                ServerApi.Logger.Debug($"[SeraphLeveling] Triggered character inventory refresh to recalculate wearable stats");
            }

            return TextCommandResult.Success($"Applied {percent.Value}% armor walk speed penalty reduction (stat value: {reduction:F2}, blended: {blendedValue:F2}). Use '/trait testwalkspeed 0' to clear.");
        }

        /// <summary>
        /// Handler for /trait reset command.
        /// Resets all trait progression to 0 for the calling player.
        /// </summary>
        private TextCommandResult OnTraitResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            ResetProgressForPlayer(player);

            string coNote = IsCombatOverhaulLoaded ? " (including Combat Overhaul proficiencies)" : "";
            return TextCommandResult.Success($"All trait progression has been reset to 0{coNote}.");
        }

        /// <summary>
        /// Removes every stored progression entry for one UID and flags all systems
        /// for save. Used for offline players: their stats and extra traits are
        /// recomputed from the (now missing) stored data when they next join.
        /// </summary>
        private static void RemoveStoredProgressForUid(string uid)
        {
            MiningProgress.TryRemove(uid, out _);
            MeleeProgress.TryRemove(uid, out _);
            RangedProgress.TryRemove(uid, out _);
            WalkingProgress.TryRemove(uid, out _);
            HungerProgress.TryRemove(uid, out _);
            ArmorProgress.TryRemove(uid, out _);
            ClothierProgress.TryRemove(uid, out _);
            MenderProgress.TryRemove(uid, out _);
            PilfererProgress.TryRemove(uid, out _);
            ResourcefulProgress.TryRemove(uid, out _);
            ForagerProgress.TryRemove(uid, out _);
            FurtiveProgress.TryRemove(uid, out _);
            PreciseProgress.TryRemove(uid, out _);
            TechnicalProgress.TryRemove(uid, out _);
            HardyHealthProgress.TryRemove(uid, out _);
            BowyerProgress.TryRemove(uid, out _);
            ImproviserProgress.TryRemove(uid, out _);
            TinkererProgress.TryRemove(uid, out _);
            MercilessProgress.TryRemove(uid, out _);
            ClaustrophobicRemovalProgress.TryRemove(uid, out _);
            COProgress.TryRemove(uid, out _);
            TemporalResistanceProgress.TryRemove(uid, out _);
            TemporalRechargeProgress.TryRemove(uid, out _);
            SleepBuffExpiration.TryRemove(uid, out _);
            SleepBuffMultiplier.TryRemove(uid, out _);

            pendingSleepBuffSave = true;
            pendingMiningProgressSave = true;
            pendingMeleeProgressSave = true;
            pendingRangedProgressSave = true;
            pendingWalkingProgressSave = true;
            pendingHungerProgressSave = true;
            pendingArmorProgressSave = true;
            pendingClothierProgressSave = true;
            pendingMenderProgressSave = true;
            pendingPilfererProgressSave = true;
            pendingResourcefulProgressSave = true;
            pendingForagerProgressSave = true;
            pendingFurtiveProgressSave = true;
            pendingPreciseProgressSave = true;
            pendingTechnicalProgressSave = true;
            pendingHardyHealthProgressSave = true;
            pendingBowyerProgressSave = true;
            pendingImproviserProgressSave = true;
            pendingTinkererProgressSave = true;
            pendingMercilessProgressSave = true;
            pendingClaustrophobicRemovalProgressSave = true;
            pendingCOProgressSave = true;
            pendingTemporalResistanceSave = true;
            pendingTemporalRechargeSave = true;
        }

        /// <summary>
        /// Handler for /trait resetplayer command.
        /// Resets all trait progression to 0 for a named player. Online players get
        /// their live stats stripped immediately. Offline players are matched by
        /// their exact last known name in the server's player database and have
        /// their stored progress removed; the reset takes effect on their next join.
        /// </summary>
        private TextCommandResult OnTraitResetPlayerCommand(TextCommandCallingArgs args)
        {
            string nameArg = args[0] as string;
            if (string.IsNullOrWhiteSpace(nameArg))
                return TextCommandResult.Error("Usage: /trait resetplayer &lt;playername&gt;");

            // Online first: full reset with a live stat strip.
            IServerPlayer target = ResolvePlayerByName(nameArg, exactOnly: true);
            if (target?.Entity != null)
            {
                ResetProgressForPlayer(target);
                SaveAllPendingProgress();

                target.SendMessage(GlobalConstants.GeneralChatGroup,
                    "An admin has reset all of your trait progression to 0.", EnumChatType.Notification);
                return TextCommandResult.Success($"All trait progression has been reset to 0 for {target.PlayerName}.");
            }

            // Offline: exact last-known-name match only, so a typo or partial name
            // cannot wipe the wrong player.
            var offlineData = ServerApi?.PlayerData?.GetPlayerDataByLastKnownName(nameArg);
            if (offlineData == null || string.IsNullOrEmpty(offlineData.PlayerUID))
                return TextCommandResult.Error($"No online player matches '{nameArg}' and no offline player has that exact name. Offline players must be matched by their full name.");

            RemoveStoredProgressForUid(offlineData.PlayerUID);
            SaveAllPendingProgress();

            return TextCommandResult.Success(
                $"Removed all stored trait progression for offline player {offlineData.LastKnownPlayername}. The reset takes effect when they next join.");
        }

        /// <summary>
        /// Handler for /trait resetall command.
        /// Wipes every progression system for every player, online and offline.
        /// Online players get their live stats stripped and reapplied; offline
        /// players simply have no stored data left, and stats are recomputed from
        /// that on their next join. Requires the literal word "confirm".
        /// </summary>
        private TextCommandResult OnTraitResetAllCommand(TextCommandCallingArgs args)
        {
            if (ServerApi == null) return TextCommandResult.Error("Server API not available.");

            string confirmArg = args[0] as string;
            if (!string.Equals(confirmArg, "confirm", StringComparison.OrdinalIgnoreCase))
            {
                return TextCommandResult.Error(
                    "This wipes ALL trait progression for EVERY player on this server, online and offline. There is no undo. Run /trait resetall confirm to proceed.");
            }

            // Strip live stats and traits from everyone online.
            int onlineReset = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                if (onlinePlayer is IServerPlayer sp && sp.Entity != null)
                {
                    ResetProgressForPlayer(sp);
                    onlineReset++;
                }
            }

            // Clear every stored entry, which also covers offline players. Stats are
            // not persistent; they are recomputed from these dicts on join, so an
            // offline player whose entries are gone comes back with zero bonuses.
            MiningProgress.Clear();
            MeleeProgress.Clear();
            RangedProgress.Clear();
            WalkingProgress.Clear();
            HungerProgress.Clear();
            ArmorProgress.Clear();
            ClothierProgress.Clear();
            MenderProgress.Clear();
            PilfererProgress.Clear();
            ResourcefulProgress.Clear();
            ForagerProgress.Clear();
            FurtiveProgress.Clear();
            PreciseProgress.Clear();
            TechnicalProgress.Clear();
            HardyHealthProgress.Clear();
            BowyerProgress.Clear();
            ImproviserProgress.Clear();
            TinkererProgress.Clear();
            MercilessProgress.Clear();
            ClaustrophobicRemovalProgress.Clear();
            COProgress.Clear();
            TemporalResistanceProgress.Clear();
            TemporalRechargeProgress.Clear();
            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();

            pendingSleepBuffSave = true;
            pendingMiningProgressSave = true;
            pendingMeleeProgressSave = true;
            pendingRangedProgressSave = true;
            pendingWalkingProgressSave = true;
            pendingHungerProgressSave = true;
            pendingArmorProgressSave = true;
            pendingClothierProgressSave = true;
            pendingMenderProgressSave = true;
            pendingPilfererProgressSave = true;
            pendingResourcefulProgressSave = true;
            pendingForagerProgressSave = true;
            pendingFurtiveProgressSave = true;
            pendingPreciseProgressSave = true;
            pendingTechnicalProgressSave = true;
            pendingHardyHealthProgressSave = true;
            pendingBowyerProgressSave = true;
            pendingImproviserProgressSave = true;
            pendingTinkererProgressSave = true;
            pendingMercilessProgressSave = true;
            pendingClaustrophobicRemovalProgressSave = true;
            pendingCOProgressSave = true;
            pendingTemporalResistanceSave = true;
            pendingTemporalRechargeSave = true;

            SaveAllPendingProgress();

            ServerApi.Logger.Notification($"[SeraphLeveling] /trait resetall by {(args.Caller.Player?.PlayerName ?? "console")}: wiped all progression ({onlineReset} online players reset live).");
            return TextCommandResult.Success(
                $"Wiped ALL trait progression for every player. {onlineReset} online player(s) were reset live; offline players are reset the next time they join.");
        }

        /// <summary>
        /// Clears every progression system back to class defaults for one player
        /// and strips the applied stat/trait bonuses. Shared by /trait reset and
        /// the pre-step of /trait import. Sends no chat message.
        /// </summary>
        private void ResetProgressForPlayer(IServerPlayer player)
        {
            string playerUid = player.PlayerUID;

            // Reset Mining
            if (MiningProgress.TryGetValue(playerUid, out var miningProg))
            {
                miningProg.TotalCredits = 0;
                miningProg.PickaxeProgress.Clear();
                pendingMiningProgressSave = true;
            }
            ApplyMiningBonus(player, 0);

            // Reset Melee
            if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
            {
                meleeProg.TotalCredits = 0;
                meleeProg.WeaponProgress.Clear();
                pendingMeleeProgressSave = true;
            }
            ApplyMeleeBonusStatic(player, 0);

            // Reset Ranged
            if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
            {
                rangedProg.TotalCredits = 0;
                rangedProg.WeaponProgress.Clear();
                pendingRangedProgressSave = true;
            }
            ApplyRangedBonusStatic(player, 0);

            // Reset Walking
            if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
            {
                walkingProg.TotalCredits = 0;
                walkingProg.BlocksInIncrement = 0;
                walkingProg.CurrentIncrementSize = BaseBlocksWalkedPerIncrement;
                pendingWalkingProgressSave = true;
            }
            ApplyWalkingBonusStatic(player, 0);

            // Reset Hunger
            if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
            {
                hungerProg.TotalCredits = 0;
                hungerProg.SecondsInIncrement = 0;
                hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
                pendingHungerProgressSave = true;
            }
            ApplyHungerBonusStatic(player, 0);

            // Reset Armor
            if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
            {
                armorProg.TotalDurabilityCredits = 0;
                armorProg.TotalWalkSpeedCredits = 0;
                armorProg.TotalHungerReductionCredits = 0;
                armorProg.TotalHealingCredits = 0;
                // Zero every piece's counters but keep HasBeenEquipped: clearing the
                // records let the join path hand the first-equip credits straight back
                // to a player who was wearing the armor when the reset happened.
                foreach (var piece in armorProg.ArmorProgress.Values)
                {
                    piece.SecondsWornInIncrement = 0;
                    piece.TimeCredits = 0;
                    piece.DamageBlockedInIncrement = 0;
                    piece.DamageCredits = 0;
                    piece.RepairsInIncrement = 0;
                    piece.RepairCredits = 0;
                }
                pendingArmorProgressSave = true;
            }
            ApplyArmorBonusesStatic(player, 0, 0);

            // Reset Clothier
            if (ClothierProgress.TryGetValue(playerUid, out var clothierProg))
            {
                clothierProg.SewingKitUnlocked = false;
                clothierProg.UniqueClothesWorn.Clear();
                pendingClothierProgressSave = true;
            }
            ApplyClothierBonusStatic(player, clothierProg ?? new ClothierProgressData());

            // Reset Mender
            if (MenderProgress.TryGetValue(playerUid, out var menderProg))
            {
                menderProg.TotalCredits = 0;
                menderProg.RepairsInIncrement = 0;
                menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
                pendingMenderProgressSave = true;
            }
            ApplyMenderBonusStatic(player, 0);

            // Reset Pilferer
            if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
            {
                pilfererProg.TotalCredits = 0;
                pilfererProg.PointsInIncrement = 0;
                pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
                pendingPilfererProgressSave = true;
            }
            ApplyPilfererBonusStatic(player, 0);

            // Reset Resourceful
            if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
            {
                resourcefulProg.TotalCredits = 0;
                resourcefulProg.AnimalsInIncrement = 0;
                resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
                pendingResourcefulProgressSave = true;
            }
            ApplyResourcefulBonusStatic(player, 0);

            // Reset Forager
            if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
            {
                foragerProg.TotalCredits = 0;
                foragerProg.CropsInIncrement = 0;
                foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
                pendingForagerProgressSave = true;
            }
            ApplyForagerBonusStatic(player, 0);

            // Reset Furtive
            if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
            {
                furtiveProg.TotalCredits = 0;
                furtiveProg.BlocksInIncrement = 0;
                furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
                pendingFurtiveProgressSave = true;
            }
            ApplyFurtiveBonusStatic(player, 0);

            // Reset Precise
            if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
            {
                preciseProg.TotalCredits = 0;
                preciseProg.WeaponProgress.Clear();
                pendingPreciseProgressSave = true;
            }
            ApplyPreciseBonusStatic(player, 0);

            // Reset Technical
            if (TechnicalProgress.TryGetValue(playerUid, out var technicalProg))
            {
                technicalProg.TranslocatorsRepaired = 0;
                technicalProg.IsUnlocked = false;
                pendingTechnicalProgressSave = true;
            }
            ApplyTechnicalBonusStatic(player, false);

            // Reset Hardy Health
            if (HardyHealthProgress.TryGetValue(playerUid, out var hardyHealthProg))
            {
                hardyHealthProg.IsUnlocked = false;
                pendingHardyHealthProgressSave = true;
            }
            ApplyHardyHealthBonusStatic(player, false);

            // Reset Bowyer
            if (BowyerProgress.TryGetValue(playerUid, out var bowyerProg))
            {
                bowyerProg.IsUnlocked = false;
                bowyerProg.TotalBowDamage = 0;
                pendingBowyerProgressSave = true;
            }
            ApplyBowyerBonusStatic(player, false);

            // Reset Improviser
            if (ImproviserProgress.TryGetValue(playerUid, out var improviserProg))
            {
                improviserProg.IsUnlocked = false;
                improviserProg.TotalRockDamage = 0;
                pendingImproviserProgressSave = true;
            }
            ApplyImproviserBonusStatic(player, false);

            // Reset Tinkerer
            if (TinkererProgress.TryGetValue(playerUid, out var tinkererProg))
            {
                tinkererProg.IsUnlocked = false;
                pendingTinkererProgressSave = true;
            }
            ApplyTinkererBonusStatic(player, false);

            // Reset Merciless
            if (MercilessProgress.TryGetValue(playerUid, out var mercilessProg))
            {
                mercilessProg.IsUnlocked = false;
                pendingMercilessProgressSave = true;
            }
            ApplyMercilessBonusStatic(player, false);

            // Reset Claustrophobic Removal
            if (ClaustrophobicRemovalProgress.TryGetValue(playerUid, out var claustrophobicProg))
            {
                claustrophobicProg.IsRemoved = false;
                pendingClaustrophobicRemovalProgressSave = true;
            }
            ApplyClaustrophobicRemovalStatic(player, false);

            // Clear sleep buff
            SleepBuffExpiration.TryRemove(playerUid, out _);
            SleepBuffMultiplier.TryRemove(playerUid, out _);
            pendingSleepBuffSave = true;

            // Also reset Combat Overhaul proficiency progression so no stale per-weapon
            // bonuses linger after a full reset. No-op if CO isn't loaded.
            ResetCOProgressForPlayer(player);

            // Reset the experimental temporal traits too
            TemporalResistanceProgress.TryRemove(playerUid, out _);
            TemporalRechargeProgress.TryRemove(playerUid, out _);
            pendingTemporalResistanceSave = true;
            pendingTemporalRechargeSave = true;
        }

        // ============================================================
        //  Cross-world progression transfer (/trait export, /trait import)
        //  File-based JSON so a full character (which can be many KB) is not
        //  limited by chat length, and the file persists outside any single
        //  world save. Files live in a global data folder so a different world
        //  can read them. Admin-only (single-player has this privilege).
        // ============================================================

        /// <summary>Reduce a player name to a safe, world-portable file stem.</summary>
        private static string SanitizeExportName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "player";
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? "player" : s;
        }

        /// <summary>Global (not per-world) folder where transfer files are stored.</summary>
        private string GetTransferDirectory()
        {
            return ServerApi.GetOrCreateDataPath("SeraphLeveling");
        }

        /// <summary>Snapshot every progression system this player has into one container.</summary>
        private PlayerProgressExport BuildProgressExport(IServerPlayer player)
        {
            string uid = player.PlayerUID;
            var ex = new PlayerProgressExport
            {
                FormatVersion = 1,
                SourcePlayerName = player.PlayerName,
                SourcePlayerUid = uid,
                ExportedGameDay = ServerApi.World.Calendar.TotalDays,
            };

            if (MiningProgress.TryGetValue(uid, out var mining)) ex.Mining = mining;
            if (MeleeProgress.TryGetValue(uid, out var melee)) ex.Melee = melee;
            if (RangedProgress.TryGetValue(uid, out var ranged)) ex.Ranged = ranged;
            if (WalkingProgress.TryGetValue(uid, out var walking)) ex.Walking = walking;
            if (HungerProgress.TryGetValue(uid, out var hunger)) ex.Hunger = hunger;
            if (ArmorProgress.TryGetValue(uid, out var armor)) ex.Armor = armor;
            if (ClothierProgress.TryGetValue(uid, out var clothier)) ex.Clothier = clothier;
            if (MenderProgress.TryGetValue(uid, out var mender)) ex.Mender = mender;
            if (PilfererProgress.TryGetValue(uid, out var pilferer)) ex.Pilferer = pilferer;
            if (ResourcefulProgress.TryGetValue(uid, out var resourceful)) ex.Resourceful = resourceful;
            if (ForagerProgress.TryGetValue(uid, out var forager)) ex.Forager = forager;
            if (FurtiveProgress.TryGetValue(uid, out var furtive)) ex.Furtive = furtive;
            if (PreciseProgress.TryGetValue(uid, out var precise)) ex.Precise = precise;
            if (TechnicalProgress.TryGetValue(uid, out var technical)) ex.Technical = technical;
            if (HardyHealthProgress.TryGetValue(uid, out var hardy)) ex.HardyHealth = hardy;
            if (BowyerProgress.TryGetValue(uid, out var bowyer)) ex.Bowyer = bowyer;
            if (ImproviserProgress.TryGetValue(uid, out var improviser)) ex.Improviser = improviser;
            if (TinkererProgress.TryGetValue(uid, out var tinkerer)) ex.Tinkerer = tinkerer;
            if (MercilessProgress.TryGetValue(uid, out var merciless)) ex.Merciless = merciless;
            if (ClaustrophobicRemovalProgress.TryGetValue(uid, out var claustro)) ex.ClaustrophobicRemoval = claustro;
            if (COProgress.TryGetValue(uid, out var co)) ex.CombatOverhaul = co;
            if (TemporalResistanceProgress.TryGetValue(uid, out var tempResist)) ex.TemporalResistance = tempResist;
            if (TemporalRechargeProgress.TryGetValue(uid, out var tempRecharge)) ex.TemporalRecharge = tempRecharge;

            return ex;
        }

        /// <summary>Install imported progression under a UID and flag each system for save.</summary>
        private void ApplyImportedProgress(string uid, PlayerProgressExport ex)
        {
            if (ex.Mining != null) { MiningProgress[uid] = ex.Mining; pendingMiningProgressSave = true; }
            if (ex.Melee != null) { MeleeProgress[uid] = ex.Melee; pendingMeleeProgressSave = true; }
            if (ex.Ranged != null) { RangedProgress[uid] = ex.Ranged; pendingRangedProgressSave = true; }
            if (ex.Walking != null) { WalkingProgress[uid] = ex.Walking; pendingWalkingProgressSave = true; }
            if (ex.Hunger != null) { HungerProgress[uid] = ex.Hunger; pendingHungerProgressSave = true; }
            if (ex.Armor != null) { ArmorProgress[uid] = ex.Armor; pendingArmorProgressSave = true; }
            if (ex.Clothier != null) { ClothierProgress[uid] = ex.Clothier; pendingClothierProgressSave = true; }
            if (ex.Mender != null) { MenderProgress[uid] = ex.Mender; pendingMenderProgressSave = true; }
            if (ex.Pilferer != null) { PilfererProgress[uid] = ex.Pilferer; pendingPilfererProgressSave = true; }
            if (ex.Resourceful != null) { ResourcefulProgress[uid] = ex.Resourceful; pendingResourcefulProgressSave = true; }
            if (ex.Forager != null) { ForagerProgress[uid] = ex.Forager; pendingForagerProgressSave = true; }
            if (ex.Furtive != null) { FurtiveProgress[uid] = ex.Furtive; pendingFurtiveProgressSave = true; }
            if (ex.Precise != null) { PreciseProgress[uid] = ex.Precise; pendingPreciseProgressSave = true; }
            if (ex.Technical != null) { TechnicalProgress[uid] = ex.Technical; pendingTechnicalProgressSave = true; }
            if (ex.HardyHealth != null) { HardyHealthProgress[uid] = ex.HardyHealth; pendingHardyHealthProgressSave = true; }
            if (ex.Bowyer != null) { BowyerProgress[uid] = ex.Bowyer; pendingBowyerProgressSave = true; }
            if (ex.Improviser != null) { ImproviserProgress[uid] = ex.Improviser; pendingImproviserProgressSave = true; }
            if (ex.Tinkerer != null) { TinkererProgress[uid] = ex.Tinkerer; pendingTinkererProgressSave = true; }
            if (ex.Merciless != null) { MercilessProgress[uid] = ex.Merciless; pendingMercilessProgressSave = true; }
            if (ex.ClaustrophobicRemoval != null) { ClaustrophobicRemovalProgress[uid] = ex.ClaustrophobicRemoval; pendingClaustrophobicRemovalProgressSave = true; }
            if (ex.CombatOverhaul != null) { COProgress[uid] = ex.CombatOverhaul; pendingCOProgressSave = true; }
            if (ex.TemporalResistance != null) { TemporalResistanceProgress[uid] = ex.TemporalResistance; pendingTemporalResistanceSave = true; }
            if (ex.TemporalRecharge != null) { TemporalRechargeProgress[uid] = ex.TemporalRecharge; pendingTemporalRechargeSave = true; }
        }

        private TextCommandResult OnTraitExportCommand(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller?.Entity == null) return TextCommandResult.Error("Player not found.");

            IServerPlayer target = caller;
            string nameArg = args[0] as string;
            if (!string.IsNullOrWhiteSpace(nameArg))
            {
                target = ResolvePlayerByName(nameArg);
                if (target == null) return TextCommandResult.Error($"Could not find online player matching '{nameArg}'.");
            }
            if (target?.Entity == null) return TextCommandResult.Error("Target player not found.");

            try
            {
                var ex = BuildProgressExport(target);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented);
                string stem = SanitizeExportName(target.PlayerName);
                string dir = GetTransferDirectory();
                string path = Path.Combine(dir, stem + ".json");
                File.WriteAllText(path, json);

                return TextCommandResult.Success(
                    $"Exported {target.PlayerName}'s progression to:\n{path}\nIn another world, run: /trait import {stem}");
            }
            catch (Exception e)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Export failed: {e}");
                return TextCommandResult.Error($"Export failed: {e.Message}");
            }
        }

        private TextCommandResult OnTraitImportCommand(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller?.Entity == null) return TextCommandResult.Error("Player not found.");

            string fileArg = args[0] as string;
            if (string.IsNullOrWhiteSpace(fileArg))
                return TextCommandResult.Error("Usage: /trait import &lt;filename&gt; [playername]");

            IServerPlayer target = caller;
            string nameArg = args[1] as string;
            if (!string.IsNullOrWhiteSpace(nameArg))
            {
                target = ResolvePlayerByName(nameArg);
                if (target == null) return TextCommandResult.Error($"Could not find online player matching '{nameArg}'.");
            }
            if (target?.Entity == null) return TextCommandResult.Error("Target player not found or not online.");

            // Resolve the file strictly inside the transfer folder (no path traversal).
            string stem = SanitizeExportName(Path.GetFileNameWithoutExtension(fileArg));
            string dir = GetTransferDirectory();
            string path = Path.Combine(dir, stem + ".json");
            if (!File.Exists(path))
                return TextCommandResult.Error($"No export file named '{stem}.json' in {dir}. Run /trait export first.");

            PlayerProgressExport ex;
            try
            {
                string json = File.ReadAllText(path);
                ex = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerProgressExport>(json);
            }
            catch (Exception e)
            {
                return TextCommandResult.Error($"Failed to read or parse '{stem}.json': {e.Message}");
            }
            if (ex == null) return TextCommandResult.Error("Export file was empty or invalid.");

            string uid = target.PlayerUID;

            // 1. Strip the target's current progression and applied bonuses.
            ResetProgressForPlayer(target);
            // 2. Install the imported data under the target's UID, flag for save.
            ApplyImportedProgress(uid, ex);
            // 3. Re-apply all bonuses live (the same routine that runs on join).
            OnPlayerJoin(target);

            string origin = string.IsNullOrEmpty(ex.SourcePlayerName) ? "" : $" (originally {ex.SourcePlayerName})";
            return TextCommandResult.Success(
                $"Imported progression from '{stem}.json'{origin} onto {target.PlayerName}. Previous progress was replaced.");
        }

        /// <summary>
        /// Handler for /trait maxall command.
        /// Sets all trait progression to maximum for testing purposes.
        /// </summary>
        private TextCommandResult OnTraitMaxAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;

            // Max Mining
            // Pass raw credits to ApplyMiningBonus, NOT CalculateMiningBonusPercent(credits).
            // ApplyMiningBonus expects the raw credit/level value and subtracts the negative-
            // trait penalty internally (Hunter's Claustrophobic -10%, Tailor's Weak -10%).
            // Passing the already-capped bonus percent caused the penalty to be subtracted
            // twice, so Hunter would land at +40% mining instead of the intended +50%.
            int maxMiningCredits = GetMaxMiningCredits(player.Entity);
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            miningProg.TotalCredits = maxMiningCredits;
            miningProg.PickaxeProgress.Clear();
            pendingMiningProgressSave = true;
            ApplyMiningBonus(player, maxMiningCredits);

            // Max Melee — same fix as Mining (pass raw credits so Farsighted/Nervous penalties
            // don't get subtracted twice and Hunter/Malefactor/Clockmaker can hit +50%).
            int maxMeleeCredits = GetMaxMeleeCredits(player.Entity);
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            meleeProg.TotalCredits = maxMeleeCredits;
            meleeProg.WeaponProgress.Clear();
            pendingMeleeProgressSave = true;
            ApplyMeleeBonusStatic(player, maxMeleeCredits);

            // Max Ranged
            int maxRangedCredits = GetMaxRangedCredits(player.Entity);
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            rangedProg.TotalCredits = maxRangedCredits;
            rangedProg.WeaponProgress.Clear();
            pendingRangedProgressSave = true;
            ApplyRangedBonusStatic(player, maxRangedCredits);

            // Max Walking
            int maxWalkingCredits = MaxWalkingSpeedPercent;
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData());
            walkingProg.TotalCredits = maxWalkingCredits;
            walkingProg.BlocksInIncrement = 0;
            walkingProg.CurrentIncrementSize = BaseBlocksWalkedPerIncrement;
            pendingWalkingProgressSave = true;
            ApplyWalkingBonusStatic(player, maxWalkingCredits);

            // Max Hunger
            int maxHungerCredits = CalculateMaxHungerCredits(player.Entity);
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData());
            hungerProg.TotalCredits = maxHungerCredits;
            hungerProg.SecondsInIncrement = 0;
            hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
            pendingHungerProgressSave = true;
            ApplyHungerBonusStatic(player, CalculateHungerBonusPercent(maxHungerCredits, player.Entity));

            // Max Armor
            int maxArmorDurabilityCredits = MaxArmorDurabilityPercent;
            int maxArmorWalkSpeedCredits = MaxArmorWalkSpeedPercent;
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            armorProg.TotalDurabilityCredits = maxArmorDurabilityCredits;
            armorProg.TotalWalkSpeedCredits = maxArmorWalkSpeedCredits;
            armorProg.ArmorProgress.Clear();
            pendingArmorProgressSave = true;
            ApplyArmorBonusesStatic(player, maxArmorDurabilityCredits, maxArmorWalkSpeedCredits);

            // Max Clothier (unlock sewing kit)
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            clothierProg.SewingKitUnlocked = true;
            pendingClothierProgressSave = true;
            ApplyClothierBonusStatic(player, clothierProg);

            // Max Mender
            int maxMenderCredits = MaxMenderPercent;
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());
            menderProg.TotalCredits = maxMenderCredits;
            menderProg.RepairsInIncrement = 0;
            menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
            pendingMenderProgressSave = true;
            ApplyMenderBonusStatic(player, maxMenderCredits);

            // Max Pilferer
            int maxPilfererCredits = GetMaxPilfererCredits(player.Entity);
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());
            pilfererProg.TotalCredits = maxPilfererCredits;
            pilfererProg.PointsInIncrement = 0;
            pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
            pendingPilfererProgressSave = true;
            ApplyPilfererBonusStatic(player, maxPilfererCredits);

            // Max Resourceful
            int maxResourcefulCredits = GetMaxResourcefulCredits(player.Entity);
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());
            resourcefulProg.TotalCredits = maxResourcefulCredits;
            resourcefulProg.AnimalsInIncrement = 0;
            resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
            pendingResourcefulProgressSave = true;
            ApplyResourcefulBonusStatic(player, maxResourcefulCredits);

            // Max Forager
            int maxForagerCredits = GetMaxForagerCredits(player.Entity);
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());
            foragerProg.TotalCredits = maxForagerCredits;
            foragerProg.CropsInIncrement = 0;
            foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
            pendingForagerProgressSave = true;
            ApplyForagerBonusStatic(player, maxForagerCredits);

            // Max Furtive
            int maxFurtiveCredits = MaxFurtivePercent;
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            furtiveProg.TotalCredits = maxFurtiveCredits;
            furtiveProg.BlocksInIncrement = 0;
            furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
            pendingFurtiveProgressSave = true;
            ApplyFurtiveBonusStatic(player, maxFurtiveCredits);

            // Max Precise
            int maxPreciseCredits = MaxPrecisePercent;
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            preciseProg.TotalCredits = maxPreciseCredits;
            preciseProg.WeaponProgress.Clear();
            pendingPreciseProgressSave = true;
            ApplyPreciseBonusStatic(player, maxPreciseCredits);

            // Unlock Technical
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            technicalProg.TranslocatorsRepaired = TechnicalRequiredTranslocatorRepairs;
            technicalProg.IsUnlocked = true;
            pendingTechnicalProgressSave = true;
            ApplyTechnicalBonusStatic(player, true);

            // Unlock Hardy Health
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            hardyHealthProg.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, true);

            // Unlock Bowyer
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            bowyerProg.IsUnlocked = true;
            bowyerProg.TotalBowDamage = BowyerBowDamageThreshold;
            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, true);

            // Unlock Improviser
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            improviserProg.IsUnlocked = true;
            improviserProg.TotalRockDamage = ImproviserRockDamageThreshold;
            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, true);

            // Unlock Tinkerer
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            tinkererProg.IsUnlocked = true;
            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, true);

            // Unlock Merciless
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            mercilessProg.IsUnlocked = true;
            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, true);

            // Remove Claustrophobic (if applicable)
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            claustrophobicProg.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, true);

            return TextCommandResult.Success("All trait progression has been set to maximum for testing.");
        }

        /// <summary>
        /// Handler for /trait testsuite1 command.
        /// Sets every progression skill to exactly 1 credit. Used to visually verify the
        /// dynamic trait display formatting at the smallest non-zero progression: every
        /// dynamic line renders, but values stay low enough that negative-trait cancellation
        /// branches also show their reduced-penalty format.
        ///
        /// Does NOT touch the binary unlock traits (Hardy Health, Bowyer, Improviser,
        /// Tinkerer, Merciless, Technical, Sewing Kit, Claustrophobic Removal). Those are
        /// already covered by individual /trait &lt;name&gt;unlock commands.
        /// </summary>
        private TextCommandResult OnTraitTestSuite1Command(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            const int CREDITS = 1;

            // Mining (pass raw credits — Apply* handles negative-trait subtraction internally)
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            miningProg.TotalCredits = CREDITS;
            miningProg.PickaxeProgress.Clear();
            pendingMiningProgressSave = true;
            ApplyMiningBonus(player, CREDITS);

            // Melee
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            meleeProg.TotalCredits = CREDITS;
            meleeProg.WeaponProgress.Clear();
            pendingMeleeProgressSave = true;
            ApplyMeleeBonusStatic(player, CREDITS);

            // Ranged
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            rangedProg.TotalCredits = CREDITS;
            rangedProg.WeaponProgress.Clear();
            pendingRangedProgressSave = true;
            ApplyRangedBonusStatic(player, CREDITS);

            // Walking
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData());
            walkingProg.TotalCredits = CREDITS;
            walkingProg.BlocksInIncrement = 0;
            walkingProg.CurrentIncrementSize = BaseBlocksWalkedPerIncrement;
            pendingWalkingProgressSave = true;
            ApplyWalkingBonusStatic(player, CREDITS);

            // Hunger
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData());
            hungerProg.TotalCredits = CREDITS;
            hungerProg.SecondsInIncrement = 0;
            hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
            pendingHungerProgressSave = true;
            ApplyHungerBonusStatic(player, CalculateHungerBonusPercent(CREDITS, player.Entity));

            // Armor (both durability and walkspeed tracks)
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            armorProg.TotalDurabilityCredits = CREDITS;
            armorProg.TotalWalkSpeedCredits = CREDITS;
            armorProg.ArmorProgress.Clear();
            pendingArmorProgressSave = true;
            ApplyArmorBonusesStatic(player, CREDITS, CREDITS);

            // Mender
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());
            menderProg.TotalCredits = CREDITS;
            menderProg.RepairsInIncrement = 0;
            menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
            pendingMenderProgressSave = true;
            ApplyMenderBonusStatic(player, CREDITS);

            // Pilferer
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());
            pilfererProg.TotalCredits = CREDITS;
            pilfererProg.PointsInIncrement = 0;
            pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
            pendingPilfererProgressSave = true;
            ApplyPilfererBonusStatic(player, CREDITS);

            // Resourceful
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());
            resourcefulProg.TotalCredits = CREDITS;
            resourcefulProg.AnimalsInIncrement = 0;
            resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
            pendingResourcefulProgressSave = true;
            ApplyResourcefulBonusStatic(player, CREDITS);

            // Forager
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());
            foragerProg.TotalCredits = CREDITS;
            foragerProg.CropsInIncrement = 0;
            foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
            pendingForagerProgressSave = true;
            ApplyForagerBonusStatic(player, CREDITS);

            // Furtive
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            furtiveProg.TotalCredits = CREDITS;
            furtiveProg.BlocksInIncrement = 0;
            furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
            pendingFurtiveProgressSave = true;
            ApplyFurtiveBonusStatic(player, CREDITS);

            // Precise
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            preciseProg.TotalCredits = CREDITS;
            preciseProg.WeaponProgress.Clear();
            pendingPreciseProgressSave = true;
            ApplyPreciseBonusStatic(player, CREDITS);

            // Combat Overhaul proficiencies (only if CO is loaded)
            string coNote = "";
            if (IsCombatOverhaulLoaded)
            {
                var coProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());
                foreach (var proficiencyStat in AllCOProficiencies)
                {
                    var profProgress = coProgress.GetProficiencyProgress(proficiencyStat);
                    profProgress.TotalCredits = CREDITS;
                    profProgress.WeaponProgress.Clear();
                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, CREDITS);
                }
                coProgress.SteadyAimCredits = CREDITS;
                ApplyCOSteadyAimBonus(player, CREDITS);
                pendingCOProgressSave = true;
                coNote = " (including Combat Overhaul proficiencies)";
            }

            return TextCommandResult.Success($"All progression skills set to 1 credit{coNote}. Open the character sheet to inspect dynamic trait formatting.");
        }

        /// <summary>
        /// Handler for /trait testsuite command.
        /// Runs automated tests for trait calculations.
        /// </summary>
        private TextCommandResult OnTraitTestSuiteCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string category = (string)args[0];
            string result = TraitTestSuite.RunTests(category, player);

            return TextCommandResult.Success(result);
        }

        /// <summary>
        /// Every stat category this mod writes to. Used by /trait verify.
        /// </summary>
        private static readonly string[] VerifyVanillaStats =
        {
            "miningSpeedMul", "oreDropRate", "meleeWeaponsDamage", "rangedWeaponsDamage",
            "rangedWeaponsAcc", "bowDrawingStrength", "walkspeed", "hungerrate",
            "healingeffectivness", "maxhealthExtraPoints", "armorDurabilityLoss",
            "armorWalkSpeedAffectedness", "animalSeekingRange", "mechanicalsDamage",
            "animalLootDropRate", "animalHarvestingTime", "forageDropRate", "wildCropDropRate",
            "vesselContentsDropRate", "rustyGearDropRate", "wholeVesselLootChance",
            "temporalGearTLRepairCost"
        };

        /// <summary>Combat Overhaul stat categories, only listed when Combat Overhaul is loaded.</summary>
        private static readonly string[] VerifyCOStats =
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY,
            CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY, CO_SPEARS_PROFICIENCY,
            CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY, CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY,
            CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY, CO_QUARTERSTAFF_PROFICIENCY, CO_STEADY_AIM,
            CO_MELEE_TIER_SLASHING, CO_RANGED_TIER_SLASHING, CO_HEAD_DAMAGE_FACTOR, CO_FACE_DAMAGE_FACTOR,
            CO_LEGS_DAMAGE_FACTOR, CO_FEET_DAMAGE_FACTOR, CO_JUMP_HEIGHT
        };

        /// <summary>
        /// Handler for /trait verify command.
        ///
        /// Prints, for each stat this mod writes, the value the game will actually use and
        /// the individual contributions it is made of. A stat blends by summing its codes
        /// onto a base of 1, so "base 1, trait 0.5, sitCOheadDamage 0.5" means the game sees
        /// 2.0. That makes a trait being counted twice, or a bonus written to a stat nothing
        /// reads, visible at a glance. Stats with nothing but their base are skipped.
        /// </summary>
        private TextCommandResult OnTraitVerifyCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();
            sb.AppendLine($"Stat contributions for {player.PlayerName}.");
            sb.AppendLine("Each stat sums its codes onto a base of 1. 'blended' is the number the game uses.");

            int shown = 0;
            foreach (string category in VerifyVanillaStats)
            {
                shown += AppendStatBreakdown(sb, player.Entity, category) ? 1 : 0;
            }

            if (IsCombatOverhaulLoaded)
            {
                sb.AppendLine("Combat Overhaul stats:");
                foreach (string category in VerifyCOStats)
                {
                    shown += AppendStatBreakdown(sb, player.Entity, category) ? 1 : 0;
                }
            }

            if (shown == 0)
            {
                return TextCommandResult.Success("No stat this mod writes has any contribution yet. Earn some progress first.");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Appends one stat's blended value and per-code contributions. Returns false and
        /// appends nothing when the stat is absent or holds nothing but its base of 1.
        /// </summary>
        private static bool AppendStatBreakdown(StringBuilder sb, EntityPlayer entity, string category)
        {
            EntityFloatStats stats = null;
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Key == category) { stats = stat.Value; break; }
            }
            if (stats?.ValuesByKey == null) return false;

            bool hasContribution = false;
            bool fromTrait = false;
            bool fromThisMod = false;
            foreach (KeyValuePair<string, EntityStat<float>> entry in stats.ValuesByKey)
            {
                if (entry.Key == "base") continue;
                if (Math.Abs(entry.Value.Value) > 0.0001f) hasContribution = true;
                if (entry.Key == "trait") fromTrait = true;
                else if (entry.Key.StartsWith("sit", StringComparison.Ordinal)) fromThisMod = true;
            }
            if (!hasContribution) return false;

            string overlap = fromTrait && fromThisMod ? "  (a trait and this mod both contribute)" : "";
            sb.AppendLine($"{category}: blended {entity.Stats.GetBlended(category):0.###} [{stats.BlendType}]{overlap}");
            foreach (KeyValuePair<string, EntityStat<float>> entry in stats.ValuesByKey)
            {
                sb.AppendLine($"   {entry.Key} = {entry.Value.Value:0.###}");
            }
            return true;
        }

        /// <summary>
        /// Handler for /trait reloadconfig command.
        /// Re-reads ModConfig/SeraphLeveling.json and reapplies every online
        /// player's bonuses against the new caps, so an admin can edit the file on
        /// a dedicated server and see it take hold without restarting.
        /// </summary>
        private TextCommandResult OnTraitReloadConfigCommand(TextCommandCallingArgs args)
        {
            if (ServerApi == null) return TextCommandResult.Error("Server API not available.");

            LoadConfigFile(ServerApi);
            RestartAutoSaveTimer(ServerApi);

            int reapplied = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                if (onlinePlayer is IServerPlayer player && player.Entity != null)
                {
                    // CancelNegativeTraits is baked into this cache at join.
                    PopulateVanillaTraitsCache(player);
                    // Decay turned on at runtime needs a start day for players already online.
                    if (EnableSkillDecay && !LastDecayCheckDay.ContainsKey(player.PlayerUID))
                        LastDecayCheckDay[player.PlayerUID] = ServerApi.World.Calendar.TotalDays;
                    ReapplyAllBonuses(player);
                    reapplied++;
                }
            }

            // The file we just read is already what is on disk, so nothing needs
            // writing back. Drop any queued save so the next world save cannot
            // overwrite the file with values from before the reload.
            pendingConfigSave = false;

            SyncExperimentalConfigToClients();

            return TextCommandResult.Success(
                $"Reloaded ModConfig/{CONFIG_FILE_NAME} and reapplied bonuses for {reapplied} online player(s).");
        }

        /// <summary>
        /// Handler for /trait resetconfig command.
        /// Resets all trait configuration values (base, increment, max) to their defaults.
        /// </summary>
        private TextCommandResult OnTraitResetConfigCommand(TextCommandCallingArgs args)
        {
            if (ServerApi == null) return TextCommandResult.Error("Server API not available.");

            // Write a fresh default file and load it through the normal path, so
            // every config-backed static returns to its default. The old version
            // reset a hand-maintained list and missed 47 of them.
            var defaults = new SeraphLevelingConfig { ConfigVersion = CURRENT_CONFIG_VERSION };
            ServerApi.StoreModConfig(defaults, CONFIG_FILE_NAME);
            LoadConfigFile(ServerApi);
            RestartAutoSaveTimer(ServerApi);

            int reapplied = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                if (onlinePlayer is IServerPlayer player && player.Entity != null)
                {
                    PopulateVanillaTraitsCache(player);
                    ReapplyAllBonuses(player);
                    reapplied++;
                }
            }
            pendingConfigSave = false;
            SyncExperimentalConfigToClients();
            return TextCommandResult.Success($"All trait configuration values reset to defaults and written to ModConfig/{CONFIG_FILE_NAME}. Reapplied bonuses for {reapplied} online player(s).");
        }
    }
}
