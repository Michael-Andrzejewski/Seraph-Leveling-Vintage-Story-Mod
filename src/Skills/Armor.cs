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

        // Keys for armor progression system
        public const string ARMOR_DURABILITY_STAT_CODE = "sitArmorDurabilityBonus";
        public const string ARMOR_WALKSPEED_STAT_CODE = "sitArmorWalkSpeedBonus";
        private const string ARMOR_PROGRESS_SAVE_KEY = "sitArmorProgress";

        // WatchedAttributes keys for client sync (armor)
        public const string WATCHED_ARMOR_DURABILITY_LEVEL = "sitArmorDurabilityLevel";
        public const string WATCHED_ARMOR_DURABILITY_BONUS = "sitArmorDurabilityBonusPercent";
        public const string WATCHED_ARMOR_WALKSPEED_LEVEL = "sitArmorWalkSpeedLevel";
        public const string WATCHED_ARMOR_WALKSPEED_BONUS = "sitArmorWalkSpeedBonusPercent";

        // Trait code for the armor mastery trait (Soldier)
        public const string ARMOR_TRAIT_CODE = "sitarmormastery";

        // Armor progression configuration
        // Time-based progression: 1 VS day (48 min) base, +1 VS day increment per credit (gives -1% walk speed penalty per credit)
        public static int BaseSecondsInArmorPerIncrement = 2880;  // Base seconds (1 VS day = 48 min) for first credit
        public static int ArmorTimeIncrementStep = 2880;          // How many more seconds each subsequent credit needs (1 VS day)

        // Damage-based progression: 100 damage base, +100 increment per credit (gives +1% durability per credit)
        public static int BaseDamageBlockedPerIncrement = 100;     // Base damage blocked for first credit
        public static int ArmorDamageIncrementStep = 100;          // How much more damage each subsequent credit needs

        // Repair-based progression: 1 repair base, +1 increment per credit (gives +1% durability per credit)
        public static int BaseRepairsPerIncrement = 1;             // Base repairs for first credit
        public static int ArmorRepairIncrementStep = 1;            // How many more repairs each subsequent credit needs

        // First-equip bonuses (durability):
        // +1% for light armor and chain, +2% for brigandine, +3% for scale and plate
        // Now configurable via config file
        public static int FirstEquipLightBonus = 1;
        public static int FirstEquipChainBonus = 1;
        public static int FirstEquipBrigandineBonus = 2;
        public static int FirstEquipScaleBonus = 3;
        public static int FirstEquipPlateBonus = 3;

        // First-equip bonuses (walk speed penalty reduction):
        // Same values as durability - grants walk speed bonus on first equip
        // Now configurable via config file
        public static int FirstEquipWalkSpeedLightBonus = 1;
        public static int FirstEquipWalkSpeedChainBonus = 1;
        public static int FirstEquipWalkSpeedBrigandineBonus = 2;
        public static int FirstEquipWalkSpeedScaleBonus = 3;
        public static int FirstEquipWalkSpeedPlateBonus = 3;

        // Max bonuses
        public static int MaxArmorDurabilityPercent = 50;          // 50% max armor durability bonus
        public static int MaxArmorWalkSpeedPercent = 50;           // 50% max walk speed penalty reduction

        // Optional armor features (disabled by default)
        public static bool EnableArmorHungerReduction = false;     // If true, armor time grants hunger rate reduction
        public static int MaxArmorHungerReductionPercent = 50;     // Max hunger rate reduction from armor
        public static bool EnableArmorHealingBonus = false;        // If true, armor time grants healing effectiveness
        public static int MaxArmorHealingPercent = 25;             // Max healing effectiveness from armor

        // WatchedAttributes keys for new armor stats
        public const string WATCHED_ARMOR_HUNGER_REDUCTION = "sitArmorHungerReduction";
        public const string WATCHED_ARMOR_HEALING_BONUS = "sitArmorHealingBonus";

        // Vanilla Soldier trait armor bonuses (used for cap calculations)
        public const int VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS = 15;
        public const int VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS = 25;

        // Storage for armor progress - keyed by player UID
        public static ConcurrentDictionary<string, ArmorProgressData> ArmorProgress = new ConcurrentDictionary<string, ArmorProgressData>();

        // Flag to indicate pending armor progress save
        private static volatile bool pendingArmorProgressSave = false;

        // Tracking currently equipped armor for each player (for time tracking and equip detection)
        // Armour worn last armour tick, per player, indexed by character slot (null =
        // no armour there). Updated in place so the tick allocates nothing.
        private static ConcurrentDictionary<string, CollectibleObject[]> playerEquippedArmor = new ConcurrentDictionary<string, CollectibleObject[]>();

        private static CollectibleObject[] EquippedArmorFor(string playerUid, int slots)
        {
            if (!playerEquippedArmor.TryGetValue(playerUid, out var arr) || arr.Length != slots)
            {
                var grown = new CollectibleObject[slots];
                if (arr != null) Array.Copy(arr, grown, Math.Min(arr.Length, slots));
                playerEquippedArmor[playerUid] = arr = grown;
            }
            return arr;
        }

        /// <summary>
        /// Handler for /trait armor command.
        /// </summary>
        private TextCommandResult OnTraitArmorCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int durabilityBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity as EntityPlayer);
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Armor progression:");
            sb.AppendLine($"  Durability: {progress.TotalDurabilityCredits} credits, +{durabilityBonus}% bonus (max {MaxArmorDurabilityPercent}%)");
            sb.AppendLine($"  Walk Speed Penalty Reduction: {progress.TotalWalkSpeedCredits} credits, -{walkSpeedBonus}% (max {MaxArmorWalkSpeedPercent}%)");

            if (progress.ArmorProgress.Count > 0)
            {
                sb.AppendLine("\nPer-armor progress:");
                foreach (var kvp in progress.ArmorProgress.OrderByDescending(p => p.Value.TimeCredits + p.Value.DamageCredits + p.Value.RepairCredits))
                {
                    string armorName = kvp.Key;
                    if (armorName.StartsWith("game:", StringComparison.Ordinal))
                        armorName = armorName.Substring(5);

                    var armorProg = kvp.Value;
                    sb.AppendLine($"  {armorName}:");
                    sb.AppendLine($"    Time: {armorProg.TimeCredits} credits ({armorProg.SecondsWornInIncrement:F0}/{armorProg.CurrentTimeIncrementSize}s)");
                    sb.AppendLine($"    Damage: {armorProg.DamageCredits} credits ({armorProg.DamageBlockedInIncrement:F1}/{armorProg.CurrentDamageIncrementSize})");
                    sb.AppendLine($"    Repairs: {armorProg.RepairCredits} credits ({armorProg.RepairsInIncrement}/{armorProg.CurrentRepairIncrementSize})");
                }
            }
            else
            {
                sb.AppendLine("\nNo armor progress yet. Wear armor to start!");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait armorlevel command.
        /// Gets or sets the player's armor durability credits (level) directly.
        /// Optionally specify an armor piece to set credits on that specific piece.
        /// </summary>
        private TextCommandResult OnTraitArmorLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = OptInt(args, 0);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                var progress = ArmorProgress.GetOrAdd(player.PlayerUID, _ => new ArmorProgressData());
                int currentBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity);
                return TextCommandResult.Success($"Current armor durability level: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent} (+{currentBonus}% durability)");
            }

            string toolName = (string)args[1];
            return SetArmorLevelForPlayer(player, player.PlayerUID, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedlevel command.
        /// Gets or sets the player's armor walk speed penalty reduction credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int? newCredits = OptInt(args, 0);

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity);
                return TextCommandResult.Success($"Current armor walk speed penalty reduction level: {progress.TotalWalkSpeedCredits}/{MaxArmorWalkSpeedPercent} (-{currentBonus}% penalty)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxArmorWalkSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorWalkSpeedPercent})");
            }

            progress.TotalWalkSpeedCredits = newCredits.Value;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorWalkSpeedBonusPercent(newCredits.Value, player.Entity);

            UpdateSkillActivityDay(playerUid, "armor");

            return TextCommandResult.Success($"Armor walk speed penalty reduction credits set to {newCredits.Value} (-{bonusPercent}% penalty).");
        }

        /// <summary>
        /// Handler for /trait armordurabilitymax command.
        /// </summary>
        private TextCommandResult OnTraitArmorDurabilityMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor durability percent must be at least 1");
                }

                MaxArmorDurabilityPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor durability bonus set to +{MaxArmorDurabilityPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor durability bonus: +{MaxArmorDurabilityPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedmax command.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor walk speed penalty reduction percent must be at least 1");
                }

                MaxArmorWalkSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor walk speed penalty reduction set to -{MaxArmorWalkSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor walk speed penalty reduction: -{MaxArmorWalkSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armortimebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorTimeBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds must be at least 1");
                }

                BaseSecondsInArmorPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds in armor per increment set to {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours).");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds in armor: {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours)\nIncrement step: +{ArmorTimeIncrementStep} ({ArmorTimeIncrementStep / 3600f:F1} hours)");
            }
        }

        /// <summary>
        /// Handler for /trait armordamagebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorDamageBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage must be at least 1");
                }

                BaseDamageBlockedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base damage blocked per increment set to {BaseDamageBlockedPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base damage blocked: {BaseDamageBlockedPerIncrement}\nIncrement step: +{ArmorDamageIncrementStep}");
            }
        }

        /// <summary>
        /// Handler for /trait armorrepairbase command.
        /// </summary>
        private TextCommandResult OnTraitArmorRepairBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base repairs must be at least 1");
                }

                BaseRepairsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base repairs per increment set to {BaseRepairsPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base repairs: {BaseRepairsPerIncrement}\nIncrement step: +{ArmorRepairIncrementStep}");
            }
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Soldier trait.
        /// </summary>
        private static bool PlayerHasVanillaSoldierForArmor(EntityPlayer entity)
        {
            return PlayerHasGameTrait(entity, "soldier");
        }

        /// <summary>
        /// Calculate armor durability bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+15% armor durability).
        /// </summary>
        public static int CalculateArmorDurabilityBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorDurabilityPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate armor walk speed penalty reduction bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+25% armor walk speed penalty reduction).
        /// </summary>
        public static int CalculateArmorWalkSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorWalkSpeedPercent - CapCounted(vanillaBonus));
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Apply armor bonuses to a player.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static void ApplyArmorBonusesStatic(IServerPlayer player, int durabilityCredits, int walkSpeedCredits)
        {
            if (player?.Entity == null) return;

            // Get the full armor progress data for optional features
            string playerUid = player.PlayerUID;
            ArmorProgress.TryGetValue(playerUid, out var armorProgressData);

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(playerUid);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierForArmor(player.Entity);

            // Calculate durability bonus (reduces armor damage taken)
            int durabilityBonus = CalculateArmorDurabilityBonusPercent(durabilityCredits, player.Entity);
            // Calculate walk speed penalty reduction
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(walkSpeedCredits, player.Entity);

            // Always apply stats (they're not persistent and need to be set on every join)
            // armorDurabilityLoss blends as WeightedSum over a base of 1, so this is
            // a delta and not a multiplier: vanilla Soldier stores -0.15 and vanilla
            // Mender -0.25. A negative value means less durability lost.
            float durabilityReduction = -(durabilityBonus * 0.01f);
            player.Entity.Stats.Set("armorDurabilityLoss", ARMOR_DURABILITY_STAT_CODE, durabilityReduction, false);

            // Reduce armor walk speed penalty using armorWalkSpeedAffectedness
            // Negative values reduce the penalty (e.g., -0.25 = 25% less armor penalty)
            // Base value is 1.0, so setting -0.5 gives 1.0 + (-0.5) = 0.5 (50% of penalty applied)
            float armorWalkSpeedReduction = -(walkSpeedBonus * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set(ARMOR_WALKSPEED_STAT_CODE, armorWalkSpeedReduction);

            // Apply optional armor features if enabled
            if (EnableArmorHungerReduction && armorProgressData != null)
            {
                int hungerReductionCredits = armorProgressData.TotalHungerReductionCredits;
                int hungerReductionBonus = Math.Min(hungerReductionCredits, MaxArmorHungerReductionPercent);
                // hungerrate is a multiplier: lower = slower hunger drain
                float hungerMultiplier = -(hungerReductionBonus * 0.01f);
                player.Entity.Stats.Set("hungerrate", "sitArmorHunger", hungerMultiplier, false);
                player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_HUNGER_REDUCTION, hungerReductionBonus);
            }

            if (EnableArmorHealingBonus && armorProgressData != null)
            {
                int healingCredits = armorProgressData.TotalHealingCredits;
                int healingBonus = Math.Min(healingCredits, MaxArmorHealingPercent);
                // The stat EntityPlayer registers is "healingeffectivness", missing an
                // e. Poultices, healing items and the character sheet all read that
                // spelling, so anything written to "healingeffectivenesstypical" is
                // simply never looked at.
                float healingModifier = healingBonus * 0.01f;
                player.Entity.Stats.Set("healingeffectivness", "sitArmorHealing", healingModifier, false);
                player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_HEALING_BONUS, healingBonus);
            }

            // Debug: Log the stat values
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] armorWalkSpeedAffectedness: set modifier {armorWalkSpeedReduction:F2}, blended value {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering slot modified
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldDurabilityLevel = watchedAttrs.GetInt(WATCHED_ARMOR_DURABILITY_LEVEL, -1);
            int oldWalkSpeedLevel = watchedAttrs.GetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, -1);

            bool valuesChanged = (oldDurabilityLevel != durabilityCredits) || (oldWalkSpeedLevel != walkSpeedCredits)
                || watchedAttrs.GetInt(WATCHED_ARMOR_DURABILITY_BONUS, -1) != durabilityBonus
                || watchedAttrs.GetInt(WATCHED_ARMOR_WALKSPEED_BONUS, -1) != walkSpeedBonus
                || watchedAttrs.GetBool("sitHasVanillaSoldierArmor") != hasVanillaSoldier;

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_LEVEL, durabilityCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_BONUS, durabilityBonus);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, walkSpeedCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_BONUS, walkSpeedBonus);
                watchedAttrs.SetBool("sitHasVanillaSoldierArmor", hasVanillaSoldier);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, ARMOR_TRAIT_CODE, (durabilityCredits > 0 || walkSpeedCredits > 0) && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_ARMOR_DURABILITY_LEVEL);
            }

            // Apply CO-specific bonuses if enabled (Big Head/Thick Skull, Leg Day)
            if (IsCOCompatEnabled)
            {
                ApplyCOBigHeadThickSkull(player, durabilityCredits);
                ApplyCOLegDay(player, durabilityCredits);
            }
        }

        /// <summary>
        /// Determines the armor type from an item code.
        /// Returns: "light" (leather, gambeson), "chain", "brigandine", "scale", "plate", or null if not armor.
        /// </summary>
        public static string GetArmorType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            // Strip ANY domain prefix, not only "game:". Collectible.Code.ToString()
            // always carries a domain, so the old game:-only strip meant armor from
            // the Combat Overhaul armory, Ancient Armory or any other mod never
            // counted for first-equip bonuses or armor progression (Edmmuz, May 2026).
            string codeToCheck = itemCode;
            int colon = itemCode.IndexOf(':');
            if (colon >= 0) codeToCheck = itemCode.Substring(colon + 1);

            // Check if it's armor (starts with "armor-")
            if (!codeToCheck.StartsWith("armor-", StringComparison.Ordinal)) return null;

            // Determine armor type from code
            if (codeToCheck.Contains("-plate-")) return "plate";
            if (codeToCheck.Contains("-scale-")) return "scale";
            if (codeToCheck.Contains("-brigandine-")) return "brigandine";
            if (codeToCheck.Contains("-chain-")) return "chain";
            if (codeToCheck.Contains("-lamellar-")) return "chain"; // Treat lamellar as chain for first-equip
            if (codeToCheck.Contains("-leather-") || codeToCheck.Contains("-gambeson-") ||
                codeToCheck.Contains("-jerkin-") || codeToCheck.Contains("-improvised-"))
                return "light";

            // Default to light if unrecognized armor type
            return "light";
        }

        /// <summary>
        /// Gets the first-equip durability bonus for an armor type.
        /// Values are now configurable via config file.
        /// </summary>
        public static int GetFirstEquipBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FirstEquipPlateBonus;
                case "scale": return FirstEquipScaleBonus;
                case "brigandine": return FirstEquipBrigandineBonus;
                case "chain": return FirstEquipChainBonus;
                case "light":
                default: return FirstEquipLightBonus;
            }
        }

        /// <summary>
        /// Gets the first-equip walk speed penalty reduction bonus for an armor type.
        /// Values are now configurable via config file.
        /// </summary>
        public static int GetFirstEquipWalkSpeedBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FirstEquipWalkSpeedPlateBonus;
                case "scale": return FirstEquipWalkSpeedScaleBonus;
                case "brigandine": return FirstEquipWalkSpeedBrigandineBonus;
                case "chain": return FirstEquipWalkSpeedChainBonus;
                case "light":
                default: return FirstEquipWalkSpeedLightBonus;
            }
        }

        /// <summary>
        /// Initialize armor tracking for a player by checking their currently equipped armor.
        /// </summary>
        private void InitializePlayerArmorTracking(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            // Check armor slots (head, body, legs) using character inventory
            var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            var equippedArmor = new CollectibleObject[characterInventory?.Count ?? 0];
            if (characterInventory != null)
            {
                // Armor slots are typically: 12 = head, 13 = body, 14 = legs (may vary)
                for (int slotIndex = 0; slotIndex < characterInventory.Count; slotIndex++)
                {
                    var slot = characterInventory[slotIndex];
                    if (slot?.Itemstack?.Collectible != null)
                    {
                        var gear = GearInfoOf(slot.Itemstack.Collectible);
                        string itemCode = gear?.Code;
                        string armorType = gear?.ArmorType;
                        if (armorType != null)
                        {
                            equippedArmor[slotIndex] = slot.Itemstack.Collectible;

                            // Check for first-time equip bonus
                            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                            var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                            if (!pieceProgress.HasBeenEquipped)
                            {
                                // Same grant as the OnArmorTick first-equip path:
                                // clamp both stats and give the walk-speed bonus too.
                                pieceProgress.HasBeenEquipped = true;
                                int firstEquipBonus = GetFirstEquipBonus(armorType);
                                armorProgress.TotalDurabilityCredits = Math.Min(armorProgress.TotalDurabilityCredits + firstEquipBonus, MaxArmorDurabilityPercent);
                                int walkSpeedEquipBonus = GetFirstEquipWalkSpeedBonus(armorType);
                                armorProgress.TotalWalkSpeedCredits = Math.Min(armorProgress.TotalWalkSpeedCredits + walkSpeedEquipBonus, MaxArmorWalkSpeedPercent);
                                pendingArmorProgressSave = true;
                                UpdateSkillActivityDay(playerUid, "armor");

                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} first-time equipped {itemCode}, +{firstEquipBonus}% durability bonus");

                                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                                // Check for trait unlocks that depend on armor durability
                                CheckHardyHealthUnlock(player);
                                CheckMercilessUnlock(player);
                            }
                        }
                    }
                }
            }

            playerEquippedArmor[playerUid] = equippedArmor;
        }

        /// <summary>
        /// Game tick handler for armor time tracking.
        /// Checks each player's equipped armor and accumulates time credits.
        /// Also detects armor equip/unequip for first-equip bonus.
        /// </summary>
        private void OnArmorTick(float dt)
        {
            if (ServerApi == null) return;

            // Skip armor progression if disabled
            if (IsSkillDisabled("armor")) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

                // Get the player's currently equipped armor using character inventory
                var characterInventory = CharacterInventoryOf(player);
                if (characterInventory == null) { playerEquippedArmor.TryRemove(playerUid, out _); continue; }

                // Armour per slot last tick; compared by item type, written back in place
                var previousArmor = EquippedArmorFor(playerUid, characterInventory.Count);

                // Check for newly equipped armor (first-equip bonus) and track time worn
                for (int slotIndex = 0; slotIndex < characterInventory.Count; slotIndex++)
                {
                    var collectible = characterInventory[slotIndex]?.Itemstack?.Collectible;
                    var gear = collectible != null ? GearInfoOf(collectible) : null;
                    if (gear?.ArmorType == null) { previousArmor[slotIndex] = null; continue; }
                    string itemCode = gear.Code;
                    var pieceProgress = armorProgress.GetArmorProgress(itemCode);
                    bool newInSlot = !ReferenceEquals(previousArmor[slotIndex], collectible);
                    previousArmor[slotIndex] = collectible;

                    // Check if this is new armor in this slot
                    if (newInSlot)
                    {
                        // New armor equipped - check for first-time bonus
                        if (!pieceProgress.HasBeenEquipped)
                        {
                            pieceProgress.HasBeenEquipped = true;
                            string armorType = gear.ArmorType;

                            // Grant durability bonus
                            int firstEquipBonus = GetFirstEquipBonus(armorType);
                            int oldDurability = armorProgress.TotalDurabilityCredits;
                            armorProgress.TotalDurabilityCredits = Math.Min(armorProgress.TotalDurabilityCredits + firstEquipBonus, MaxArmorDurabilityPercent);
                            int actualDurabilityBonus = armorProgress.TotalDurabilityCredits - oldDurability;

                            // Grant walk speed penalty reduction bonus (same values as durability)
                            int walkSpeedEquipBonus = GetFirstEquipWalkSpeedBonus(armorType);
                            int oldWalkSpeed = armorProgress.TotalWalkSpeedCredits;
                            armorProgress.TotalWalkSpeedCredits = Math.Min(armorProgress.TotalWalkSpeedCredits + walkSpeedEquipBonus, MaxArmorWalkSpeedPercent);
                            int actualWalkSpeedBonus = armorProgress.TotalWalkSpeedCredits - oldWalkSpeed;

                            pendingArmorProgressSave = true;
                            UpdateSkillActivityDay(playerUid, "armor");

                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Send message with both bonuses
                            if (actualDurabilityBonus > 0 || actualWalkSpeedBonus > 0)
                            {
                                NotifyLevelUp(player,
                                    Lang.Get("seraphleveling:message-armor-first-equip-both", actualDurabilityBonus, actualWalkSpeedBonus));
                            }

                            // Check for trait unlocks that depend on armor durability
                            CheckHardyHealthUnlock(player);
                            CheckMercilessUnlock(player);
                        }
                    }

                    // Track time worn for walk speed credits (only if not at max for any time-based stat)
                    bool hasRoomForWalkSpeed = armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent;
                    bool hasRoomForHunger = EnableArmorHungerReduction && armorProgress.TotalHungerReductionCredits < MaxArmorHungerReductionPercent;
                    bool hasRoomForHealing = EnableArmorHealingBonus && armorProgress.TotalHealingCredits < MaxArmorHealingPercent;

                    if (hasRoomForWalkSpeed || hasRoomForHunger || hasRoomForHealing)
                    {
                        int oldWalkSpeedCredits = armorProgress.TotalWalkSpeedCredits;
                        int oldHungerCredits = armorProgress.TotalHungerReductionCredits;
                        int oldHealingCredits = armorProgress.TotalHealingCredits;

                        // Add 1 second (tick interval) to this armor piece's time
                        // Apply sleep buff multiplier if active
                        float modifiedTime = ApplyXPMultiplier(playerUid, 1f);
                        pieceProgress.SecondsWornInIncrement += modifiedTime;

                        // Check if we've earned any new time credits
                        while (pieceProgress.SecondsWornInIncrement >= pieceProgress.CurrentTimeIncrementSize)
                        {
                            bool anyEarned = false;

                            // Award walk speed credit if not at max
                            if (armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent)
                            {
                                pieceProgress.TimeCredits++;
                                armorProgress.TotalWalkSpeedCredits++;
                                anyEarned = true;
                            }

                            // Award hunger reduction credit if feature enabled and not at max
                            if (EnableArmorHungerReduction && armorProgress.TotalHungerReductionCredits < MaxArmorHungerReductionPercent)
                            {
                                armorProgress.TotalHungerReductionCredits++;
                                anyEarned = true;
                            }

                            // Award healing credit if feature enabled and not at max
                            if (EnableArmorHealingBonus && armorProgress.TotalHealingCredits < MaxArmorHealingPercent)
                            {
                                armorProgress.TotalHealingCredits++;
                                anyEarned = true;
                            }

                            if (!anyEarned) break; // All time-based stats are at max

                            pieceProgress.SecondsWornInIncrement -= pieceProgress.CurrentTimeIncrementSize;
                            pieceProgress.CurrentTimeIncrementSize += ArmorTimeIncrementStep;

                            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned time credit {pieceProgress.TimeCredits} with {itemCode}");
                        }

                        bool creditsChanged = (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits) ||
                                               (armorProgress.TotalHungerReductionCredits > oldHungerCredits) ||
                                               (armorProgress.TotalHealingCredits > oldHealingCredits);

                        if (creditsChanged)
                        {
                            pendingArmorProgressSave = true;
                            UpdateSkillActivityDay(playerUid, "armor");
                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Notify player of level up (only for walk speed, as it's the primary stat)
                            if (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits)
                            {
                                NotifyLevelUp(player,
                                    Lang.Get("seraphleveling:message-armor-time-level-up", armorProgress.TotalWalkSpeedCredits));
                            }
                        }
                    }
                }

            }
        }

        /// <summary>
        /// Process armor damage blocked. Called from Harmony patch when player takes damage.
        /// </summary>
        public static void ProcessArmorDamageBlocked(IServerPlayer player, float damageBlocked, string armorCode)
        {
            ProcessArmorDamageBlockedCore(player, damageBlocked, armorCode);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessArmorDamageBlockedCore(IServerPlayer player, float damageBlocked, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damageBlocked);
            pieceProgress.DamageBlockedInIncrement += modifiedDamage;

            // Check if we've earned any new damage credits
            while (pieceProgress.DamageBlockedInIncrement >= pieceProgress.CurrentDamageIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.DamageCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.DamageBlockedInIncrement -= pieceProgress.CurrentDamageIncrementSize;
                pieceProgress.CurrentDamageIncrementSize += ArmorDamageIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned damage credit {pieceProgress.DamageCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                UpdateSkillActivityDay(playerUid, "armor");
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-armor-damage-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits));

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Process armor repair. Called from Harmony patch when armor is repaired.
        /// </summary>
        public static void ProcessArmorRepair(IServerPlayer player, string armorCode)
        {
            ProcessArmorRepairCore(player, armorCode);
            MarkProgressChanged(player);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessArmorRepairCore(IServerPlayer player, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            pieceProgress.RepairsInIncrement++;

            // Check if we've earned a repair credit
            while (pieceProgress.RepairsInIncrement >= pieceProgress.CurrentRepairIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.RepairCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.RepairsInIncrement -= pieceProgress.CurrentRepairIncrementSize;
                pieceProgress.CurrentRepairIncrementSize += ArmorRepairIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned repair credit {pieceProgress.RepairCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                UpdateSkillActivityDay(playerUid, "armor");
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-armor-repair-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits));

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }
    }
}
