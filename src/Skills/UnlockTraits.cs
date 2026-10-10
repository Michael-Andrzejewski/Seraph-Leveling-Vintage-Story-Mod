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

        // =========================================================================
        // HARDY HEALTH TRAIT - Unlocks +5 HP after reaching mining and armor thresholds
        // =========================================================================
        public const string HARDY_HEALTH_STAT_CODE = "sitHardyHealthBonus";
        public const int VANILLA_HARDY_HP_BONUS = 5;                 // vanilla Hardy: maxhealthExtraPoints 5
        private const string HARDY_HEALTH_PROGRESS_SAVE_KEY = "sitHardyHealthProgress";
        public const string WATCHED_HARDY_HEALTH_UNLOCKED = "sitHardyHealthUnlocked";
        public const string HARDY_HEALTH_TRAIT_CODE = "sithardyhealthmastery";

        // Hardy health unlock thresholds
        public static int HardyHealthMiningThreshold = 10;           // 10% mining speed bonus required (10 credits)
        public static int HardyHealthArmorDurabilityThreshold = 10;  // 10% armor durability bonus required
        public static int HardyHealthBonus = 5;                      // +5 HP bonus

        // Storage for hardy health progress
        public static ConcurrentDictionary<string, HardyHealthProgressData> HardyHealthProgress = new ConcurrentDictionary<string, HardyHealthProgressData>();
        private static volatile bool pendingHardyHealthProgressSave = false;

        // =========================================================================
        // BOWYER TRAIT - Unlocks crude bow/arrows after ranged damage + bow damage
        // =========================================================================
        public const string BOWYER_STAT_CODE = "sitBowyerBonus";
        private const string BOWYER_PROGRESS_SAVE_KEY = "sitBowyerProgress";
        public const string WATCHED_BOWYER_UNLOCKED = "sitBowyerUnlocked";
        public const string WATCHED_BOWYER_BOW_DAMAGE = "sitBowyerBowDamage";
        public const string BOWYER_TRAIT_CODE = "sitbowyermastery";

        // Bowyer unlock thresholds
        public static int BowyerRangedDamageThreshold = 10;          // 10% ranged damage bonus required
        public static int BowyerBowDamageThreshold = 300;            // 300 total bow damage required

        // Storage for bowyer progress
        public static ConcurrentDictionary<string, BowyerProgressData> BowyerProgress = new ConcurrentDictionary<string, BowyerProgressData>();
        private static volatile bool pendingBowyerProgressSave = false;

        // =========================================================================
        // IMPROVISER TRAIT - Unlocks sling after thrown rock damage
        // =========================================================================
        public const string IMPROVISER_STAT_CODE = "sitImproviserBonus";
        private const string IMPROVISER_PROGRESS_SAVE_KEY = "sitImproviserProgress";
        public const string WATCHED_IMPROVISER_UNLOCKED = "sitImproviserUnlocked";
        public const string WATCHED_IMPROVISER_ROCK_DAMAGE = "sitImproviserRockDamage";
        public const string IMPROVISER_TRAIT_CODE = "sitimprovisermastery";

        // Improviser unlock threshold
        public static int ImproviserRockDamageThreshold = 300;       // 300 total thrown rock damage required

        // Storage for improviser progress
        public static ConcurrentDictionary<string, ImproviserProgressData> ImproviserProgress = new ConcurrentDictionary<string, ImproviserProgressData>();
        private static volatile bool pendingImproviserProgressSave = false;

        // =========================================================================
        // TINKERER TRAIT - Unlocks tuning spear after Technical + Precise threshold
        // =========================================================================
        public const string TINKERER_STAT_CODE = "sitTinkererBonus";
        private const string TINKERER_PROGRESS_SAVE_KEY = "sitTinkererProgress";
        public const string WATCHED_TINKERER_UNLOCKED = "sitTinkererUnlocked";
        public const string TINKERER_TRAIT_CODE = "sittinkerermastery";

        // Tinkerer unlock threshold
        public static int TinkererPreciseThreshold = 10;              // 10% Precise damage bonus required (plus Technical)

        // Storage for tinkerer progress
        public static ConcurrentDictionary<string, TinkererProgressData> TinkererProgress = new ConcurrentDictionary<string, TinkererProgressData>();
        private static volatile bool pendingTinkererProgressSave = false;

        // =========================================================================
        // MERCILESS TRAIT - Unlocks shortsword/shield after armor + melee thresholds
        // =========================================================================
        public const string MERCILESS_STAT_CODE = "sitMercilessBonus";
        private const string MERCILESS_PROGRESS_SAVE_KEY = "sitMercilessProgress";
        public const string WATCHED_MERCILESS_UNLOCKED = "sitMercilessUnlocked";
        public const string MERCILESS_TRAIT_CODE = "sitmercilessmastery";

        // Merciless unlock thresholds
        public static int MercilessArmorDurabilityThreshold = 10;    // 10% armor durability bonus required
        public static int MercilessMeleeDamageThreshold = 15;        // 15% melee damage bonus required

        // Storage for merciless progress
        public static ConcurrentDictionary<string, MercilessProgressData> MercilessProgress = new ConcurrentDictionary<string, MercilessProgressData>();
        private static volatile bool pendingMercilessProgressSave = false;

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL - Removes trait after reaching mining threshold (Hunter)
        // =========================================================================
        private const string CLAUSTROPHOBIC_REMOVAL_PROGRESS_SAVE_KEY = "sitClaustrophobicRemovalProgress";
        public const string WATCHED_CLAUSTROPHOBIC_REMOVED = "sitClaustrophobicRemoved";
        public const string CLAUSTROPHOBIC_REMOVED_TRAIT_CODE = "sitclaustrophobicremoved";

        // Claustrophobic removal threshold
        public static int ClaustrophobicRemovalMiningThreshold = 100;  // 100% mining speed bonus required

        // Storage for claustrophobic removal progress
        public static ConcurrentDictionary<string, ClaustrophobicRemovalProgressData> ClaustrophobicRemovalProgress = new ConcurrentDictionary<string, ClaustrophobicRemovalProgressData>();
        private static volatile bool pendingClaustrophobicRemovalProgressSave = false;

        // =========================================================================
        // UNLOCK CHECKING METHODS
        // =========================================================================

        /// <summary>
        /// Check and apply Hardy health unlock if thresholds are met.
        /// Requires 110% mining speed and 10% armor durability.
        /// </summary>
        private static void CheckHardyHealthUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if hardyhealth skill is disabled
            if (IsSkillDisabled("hardyhealth")) return;

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < HardyHealthMiningThreshold) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < HardyHealthArmorDurabilityThreshold) return;

            // Both thresholds met - unlock Hardy health!
            progress.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;

            // Apply the health bonus
            ApplyHardyHealthBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-hardy-health-unlock", HardyHealthBonus));
        }

        /// <summary>
        /// Apply Hardy health bonus (+5 HP).
        /// </summary>
        private static void ApplyHardyHealthBonusStatic(IServerPlayer player, bool unlocked)
        {
            // Blackguard already has vanilla Hardy (+5 health). By default every class
            // ends at the same total, so the vanilla points count against the bonus;
            // with EnableClassCapOffsets the class keeps its head start.
            int vanillaHardyHp = PlayerHasGameTrait(player.Entity, "hardy") ? VANILLA_HARDY_HP_BONUS : 0;
            int hardyHealthPoints = Math.Max(0, HardyHealthBonus - CapCounted(vanillaHardyHp));
            if (unlocked && hardyHealthPoints > 0)
            {
                player.Entity.Stats.Set("maxhealthExtraPoints", HARDY_HEALTH_STAT_CODE, hardyHealthPoints, false);
            }
            else
            {
                player.Entity.Stats.Remove("maxhealthExtraPoints", HARDY_HEALTH_STAT_CODE);
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_HARDY_HEALTH_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, HARDY_HEALTH_TRAIT_CODE, unlocked);
        }

        /// <summary>
        /// Check and apply Tinkerer unlock if thresholds are met.
        /// Requires Technical trait AND 10% Precise damage bonus.
        /// </summary>
        private static void CheckTinkererUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check Technical trait
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (!technicalProgress.IsUnlocked) return;

            // Check Precise threshold
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            if (preciseProgress.TotalCredits < TinkererPreciseThreshold) return;

            // Both conditions met - unlock Tinkerer!
            progress.IsUnlocked = true;
            pendingTinkererProgressSave = true;

            // Apply the trait
            ApplyTinkererBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-tinkerer-unlock"));
        }

        /// <summary>
        /// Apply Tinkerer trait (unlocks tuning spear crafting).
        /// Also adds "tinkerer" to extraTraits to unlock tuning spear recipes.
        /// </summary>
        private static void ApplyTinkererBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_TINKERER_UNLOCKED, unlocked);

            // Update extraTraits to show Tinkerer trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, TINKERER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "tinkerer" to extraTraits to unlock tuning spear recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the tuning spear requires "tinkerer")
            UpdateExtraTraitStatic(player.Entity, "tinkerer", unlocked);
        }

        /// <summary>
        /// Check and apply Merciless unlock if thresholds are met.
        /// Requires 10% armor durability AND 15% melee damage.
        /// </summary>
        private static void CheckMercilessUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < MercilessArmorDurabilityThreshold) return;

            // Check melee damage threshold
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            if (meleeProgress.TotalCredits < MercilessMeleeDamageThreshold) return;

            // Both thresholds met - unlock Merciless!
            progress.IsUnlocked = true;
            pendingMercilessProgressSave = true;

            // Apply the trait
            ApplyMercilessBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-merciless-unlock"));
        }

        /// <summary>
        /// Apply Merciless trait (unlocks shortsword/shield crafting).
        /// </summary>
        private static void ApplyMercilessBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_MERCILESS_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, MERCILESS_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "merciless" to extraTraits to unlock shortsword/shield recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., shortsword/shield require "merciless")
            UpdateExtraTraitStatic(player.Entity, "merciless", unlocked);
        }

        /// <summary>
        /// Check and apply Bowyer unlock if thresholds are met.
        /// Requires 10% ranged damage AND 300 damage with simple bow/longbow.
        /// </summary>
        private static void CheckBowyerUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check ranged damage threshold
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            if (rangedProgress.TotalCredits < BowyerRangedDamageThreshold) return;

            // Check bow damage threshold
            if (progress.TotalBowDamage < BowyerBowDamageThreshold) return;

            // Both thresholds met - unlock Bowyer!
            progress.IsUnlocked = true;
            pendingBowyerProgressSave = true;

            // Apply the trait
            ApplyBowyerBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-bowyer-unlock"));
        }

        /// <summary>
        /// Apply Bowyer trait (unlocks crude bow/arrows crafting).
        /// Also adds "bowyer" to extraTraits to unlock crude bow/arrows recipes.
        /// </summary>
        private static void ApplyBowyerBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_BOWYER_UNLOCKED, unlocked);

            // Update extraTraits to show Bowyer trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, BOWYER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "bowyer" to extraTraits to unlock crude bow/arrows recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., crude bow/arrows require "bowyer")
            UpdateExtraTraitStatic(player.Entity, "bowyer", unlocked);
        }

        /// <summary>
        /// Check and apply Improviser unlock if threshold is met.
        /// Requires 300 damage with thrown rocks.
        /// </summary>
        private static void CheckImproviserUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check rock damage threshold
            if (progress.TotalRockDamage < ImproviserRockDamageThreshold) return;

            // Threshold met - unlock Improviser!
            progress.IsUnlocked = true;
            pendingImproviserProgressSave = true;

            // Apply the trait
            ApplyImproviserBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-improviser-unlock"));
        }

        /// <summary>
        /// Apply Improviser trait (unlocks sling crafting).
        /// Also adds "improviser" to extraTraits to unlock sling recipes.
        /// </summary>
        private static void ApplyImproviserBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_IMPROVISER_UNLOCKED, unlocked);

            // Update extraTraits to show Improviser trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, IMPROVISER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "improviser" to extraTraits to unlock sling recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., sling requires "improviser")
            UpdateExtraTraitStatic(player.Entity, "improviser", unlocked);
        }

        /// <summary>
        /// Check and apply Claustrophobic removal if threshold is met (Hunter only).
        /// Requires 100% mining speed.
        /// </summary>
        private static void CheckClaustrophobicRemoval(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Only applies to Hunter class
            if (!PlayerIsHunterStatic(player.Entity)) return;

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());

            // Already removed
            if (progress.IsRemoved) return;

            // Servers that keep negative traits for life never remove Claustrophobic.
            if (!CancelNegativeTraits) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < ClaustrophobicRemovalMiningThreshold) return;

            // Threshold met - remove Claustrophobic!
            progress.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;

            // Apply the removal (negate the Claustrophobic penalties)
            ApplyClaustrophobicRemovalStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-claustrophobic-removed"));
        }

        /// <summary>
        /// Apply Claustrophobic removal (negates ore drop and mining speed penalties).
        /// </summary>
        private static void ApplyClaustrophobicRemovalStatic(IServerPlayer player, bool removed)
        {
            if (removed)
            {
                // Negate Claustrophobic penalties: -15% ore drop, -10% mining speed
                // By adding positive stats to counteract them
                // Note: Stats use WeightedSum with base 1.0. Vanilla uses -0.15/-0.10, so we use +0.15/+0.10 to cancel
                player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicRemoval", 0.15f, false); // +15% to negate -15%
                player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicRemoval", 0.10f, false); // +10% to negate -10%
            }
            else
            {
                player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicRemoval");
                player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicRemoval");
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_CLAUSTROPHOBIC_REMOVED, removed);
            UpdateExtraTraitStatic(player.Entity, CLAUSTROPHOBIC_REMOVED_TRAIT_CODE, removed);
        }

        /// <summary>
        /// Handler for /trait hardyhealth command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Hardy Health trait: {(progress.IsUnlocked ? $"UNLOCKED (+{HardyHealthBonus} HP)" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Mining level: {miningProgress.TotalCredits} / {HardyHealthMiningThreshold} ({(miningProgress.TotalCredits >= HardyHealthMiningThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {HardyHealthArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= HardyHealthArmorDurabilityThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait bowyer command.
        /// </summary>
        private TextCommandResult OnTraitBowyerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Bowyer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Ranged level: {rangedProgress.TotalCredits} / {BowyerRangedDamageThreshold} ({(rangedProgress.TotalCredits >= BowyerRangedDamageThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Bow damage: {progress.TotalBowDamage:F0} / {BowyerBowDamageThreshold} ({(progress.TotalBowDamage >= BowyerBowDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait improviser command.
        /// </summary>
        private TextCommandResult OnTraitImproviserCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Improviser trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Rock damage: {progress.TotalRockDamage:F0} / {ImproviserRockDamageThreshold} ({(progress.TotalRockDamage >= ImproviserRockDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait tinkerer command.
        /// </summary>
        private TextCommandResult OnTraitTinkererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Tinkerer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Technical trait: {(technicalProgress.IsUnlocked ? "UNLOCKED ✓" : "Locked ✗")}");
            sb.AppendLine($"  Precise level: {preciseProgress.TotalCredits} / {TinkererPreciseThreshold} ({(preciseProgress.TotalCredits >= TinkererPreciseThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait merciless command.
        /// </summary>
        private TextCommandResult OnTraitMercilessCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Merciless trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {MercilessArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= MercilessArmorDurabilityThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Melee level: {meleeProgress.TotalCredits} / {MercilessMeleeDamageThreshold} ({(meleeProgress.TotalCredits >= MercilessMeleeDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait claustrophobic command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Check if player is Hunter
            if (!PlayerIsHunterStatic(player.Entity))
            {
                return TextCommandResult.Success("Claustrophobic removal is only available for the Hunter class.");
            }

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Claustrophobic trait: {(progress.IsRemoved ? "REMOVED" : "Active")}");
            sb.AppendLine($"Mining level: {miningProgress.TotalCredits} / {ClaustrophobicRemovalMiningThreshold} ({(miningProgress.TotalCredits >= ClaustrophobicRemovalMiningThreshold ? "✓" : "✗")})");
            if (!progress.IsRemoved)
            {
                int remaining = ClaustrophobicRemovalMiningThreshold - miningProgress.TotalCredits;
                sb.AppendLine($"Reach {remaining}% more mining level to remove Claustrophobic!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait hardyhealthunlock command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            progress.IsUnlocked = unlock;

            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, unlock);

            return TextCommandResult.Success($"Hardy Health trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait bowyerunlock command.
        /// </summary>
        private TextCommandResult OnTraitBowyerUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            progress.IsUnlocked = unlock;

            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, unlock);

            return TextCommandResult.Success($"Bowyer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait improviserunlock command.
        /// </summary>
        private TextCommandResult OnTraitImproviserUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            progress.IsUnlocked = unlock;

            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, unlock);

            return TextCommandResult.Success($"Improviser trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait tinkererunlock command.
        /// </summary>
        private TextCommandResult OnTraitTinkererUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            progress.IsUnlocked = unlock;

            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, unlock);

            return TextCommandResult.Success($"Tinkerer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait mercilessunlock command.
        /// </summary>
        private TextCommandResult OnTraitMercilessUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            progress.IsUnlocked = unlock;

            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, unlock);

            return TextCommandResult.Success($"Merciless trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait claustrophobicunlock command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool removed = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            progress.IsRemoved = removed;

            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, removed);

            return TextCommandResult.Success($"Claustrophobic trait {(removed ? "removed" : "restored")}.");
        }
    }
}
