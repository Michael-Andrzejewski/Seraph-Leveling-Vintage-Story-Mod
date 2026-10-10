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
        // CLOTHIER TRAIT - Tracks unique clothing worn to unlock sewing kit crafting
        // =========================================================================
        public const string CLOTHIER_STAT_CODE = "sitClothierBonus";
        private const string CLOTHIER_PROGRESS_SAVE_KEY = "sitClothierProgress";
        public const string WATCHED_CLOTHIER_COUNT = "sitClothierCount";
        public const string WATCHED_CLOTHIER_UNLOCKED = "sitClothierUnlocked";
        public const string CLOTHIER_TRAIT_CODE = "sitclothiermastery";

        // Clothier progression configuration
        public static int ClothierRequiredUniqueClothes = 20; // Number of unique clothes to unlock sewing kit
        public static string[] ClothierBlacklistedItems = new string[] {
            // Hunter
            "clothes-upperbody-hunter-shirt", "clothes-upperbodyover-hunter-coat", "clothes-shoulder-hunter-poncho",
            "clothes-lowerbody-hunter-leggings", "clothes-foot-hunter-boots", "clothes-hand-hunter-gloves",
            "clothes-head-hunter-hood", "clothes-face-hunter-mask",
            // Tailor
            "clothes-upperbody-tailor-blouse", "clothes-foot-tailor-shoes", "clothes-hand-tailor-gloves",
            "clothes-waist-tailor-belt", "clothes-shoulder-tailor-jacket",
            // Malefactor
            "clothes-shoulder-malefactor-cloak", "clothes-foot-malefactor-boots", "clothes-hand-malefactor-gloves",
            "clothes-lowerbody-malefactor-trousers", "clothes-neck-malefactor-pendant",
            // Blackguard
            "clothes-foot-blackguard-shoes", "clothes-lowerbody-blackguard-leggings",
            "clothes-upperbody-blackguard-shirt", "clothes-waist-blackguard-belt",
            // Clockmaker
            "clothes-hand-clockmaker-wristguard", "clothes-foot-clockmaker-shoes",
            "clothes-upperbody-clockmaker-shirt", "clothes-shoulder-clockmaker-apron",
            // Commoner
            "clothes-upperbody-commoner-shirt", "clothes-upperbodyover-commoner-coat",
            "clothes-lowerbody-commoner-trousers", "clothes-foot-commoner-boots", "clothes-hand-commoner-gloves"
        };

        // Vanilla Clothier trait (Tailor exclusive)
        public const int VANILLA_CLOTHIER_BONUS = 0; // No vanilla bonus, this is unlock-based

        // Storage for clothier progress
        public static ConcurrentDictionary<string, ClothierProgressData> ClothierProgress = new ConcurrentDictionary<string, ClothierProgressData>();
        private static volatile bool pendingClothierProgressSave = false;

        // Tracking currently equipped clothing for each player
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedClothing = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // CLOTHIER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait clothier command.
        /// </summary>
        private TextCommandResult OnTraitClothierCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());
            int uniqueCount = progress.UniqueClothesWorn.Count;
            bool unlocked = progress.SewingKitUnlocked;

            var sb = new StringBuilder();
            sb.AppendLine($"Clothier progression: {uniqueCount} / {ClothierRequiredUniqueClothes} unique clothes worn");
            if (unlocked)
            {
                sb.AppendLine("Status: Sewing kit crafting UNLOCKED!");
            }
            else
            {
                sb.AppendLine($"Status: Wear {ClothierRequiredUniqueClothes - uniqueCount} more unique clothes to unlock sewing kit");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait clothierrequired command.
        /// </summary>
        private TextCommandResult OnTraitClothierRequiredCommand(TextCommandCallingArgs args)
        {
            int? newValue = OptInt(args, 0);

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Required clothes must be at least 1.");
                ClothierRequiredUniqueClothes = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Clothier required unique clothes set to {ClothierRequiredUniqueClothes}.");
            }

            return TextCommandResult.Success($"Current clothier required: {ClothierRequiredUniqueClothes} unique clothes.");
        }

        /// <summary>
        /// Handler for /trait clothierlevel command.
        /// Gets or sets the player's clothier progress (unique clothes count).
        /// </summary>
        private TextCommandResult OnTraitClothierLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());

            int? newLevel = OptInt(args, 0);

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentLevel = progress.UniqueClothesWorn.Count;
                string status = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - currentLevel} more needed to unlock.";
                return TextCommandResult.Success($"Current clothier level: {currentLevel}/{ClothierRequiredUniqueClothes}. {status}");
            }

            if (newLevel.Value < 0)
                return TextCommandResult.Error("Level must be 0 or greater.");

            // Clear the existing clothes set
            progress.UniqueClothesWorn.Clear();

            // Add placeholder entries up to the desired level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.UniqueClothesWorn.Add($"__placeholder_cloth_{i}");
            }

            // Set unlock status based on whether we've reached the required amount
            progress.SewingKitUnlocked = newLevel.Value >= ClothierRequiredUniqueClothes;


            // Apply the bonus (this updates WatchedAttributes and extraTraits)
            ApplyClothierBonusStatic(player, progress);

            string newStatus = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - newLevel.Value} more needed to unlock.";
            return TextCommandResult.Success($"Clothier level set to {newLevel.Value}/{ClothierRequiredUniqueClothes}. {newStatus}");
        }

        /// <summary>
        /// Tick handler for clothing tracking.
        /// </summary>
        private void OnClothingTick(float dt)
        {
            if (ServerApi == null) return;

            // Skip clothier progression if disabled
            if (IsSkillDisabled("clothier")) return;

            foreach (IServerPlayer player in OnlinePlayers)
            {
                if (!IsPlaying(player)) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var clothierProgress = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());

                // Skip if already unlocked
                if (clothierProgress.SewingKitUnlocked) continue;

                // Worn clothes only change when a character slot changes
                if (!GearScanDue(InventoriesOf(player), clothing: true)) continue;

                // Get the player's currently equipped clothing using character inventory
                var characterInventory = CharacterInventoryOf(player);
                if (characterInventory != null)
                {
                    foreach (var slot in characterInventory)
                    {
                        if (slot?.Itemstack?.Collectible != null)
                        {
                            var gear = GearInfoOf(slot.Itemstack.Collectible);
                            string itemCode = gear?.Code;
                            if (gear != null && gear.IsClothing)
                            {
                                if (clothierProgress.UniqueClothesWorn.Add(itemCode))
                                {
                                    // New unique clothing worn
                                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} wore new clothing: {itemCode} ({clothierProgress.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes})");

                                    // Check if unlocked
                                    if (clothierProgress.UniqueClothesWorn.Count >= ClothierRequiredUniqueClothes && !clothierProgress.SewingKitUnlocked)
                                    {
                                        clothierProgress.SewingKitUnlocked = true;
                                        ApplyClothierBonusStatic(player, clothierProgress);
                                        NotifyLevelUp(player,
                                            Lang.Get("seraphleveling:message-clothier-unlocked"));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if an item code represents clothing (not armor) and is not blacklisted.
        /// Starting class outfits are blacklisted by default to prevent easy Clothier progression.
        /// </summary>
        private static bool IsClothingItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();

            // Check if item is blacklisted (starting class outfits)
            if (ClothierBlacklistedItems != null)
            {
                foreach (string pattern in ClothierBlacklistedItems)
                {
                    if (!string.IsNullOrEmpty(pattern) && lowerCode.Contains(pattern.ToLowerInvariant()))
                    {
                        return false;
                    }
                }
            }

            // Clothing items include clothes, not armor
            if (lowerCode.Contains("clothes-")) return true;
            if (lowerCode.Contains("shirt-")) return true;
            if (lowerCode.Contains("trousers-")) return true;
            if (lowerCode.Contains("dress-")) return true;
            if (lowerCode.Contains("hat-")) return true;
            if (lowerCode.Contains("cape-")) return true;
            if (lowerCode.Contains("cloak-")) return true;
            if (lowerCode.Contains("jacket-")) return true;
            if (lowerCode.Contains("vest-")) return true;
            if (lowerCode.Contains("skirt-")) return true;
            if (lowerCode.Contains("gloves-")) return true;
            if (lowerCode.Contains("boots-")) return true;
            if (lowerCode.Contains("shoes-")) return true;
            if (lowerCode.Contains("headband-")) return true;
            if (lowerCode.Contains("mask-")) return true;
            if (lowerCode.Contains("scarf-")) return true;

            return false;
        }

        /// <summary>
        /// Apply clothier bonus (update WatchedAttributes for client sync).
        /// Also adds "clothier" to extraTraits to unlock sewing kit recipes.
        /// </summary>
        private static void ApplyClothierBonusStatic(IServerPlayer player, ClothierProgressData progress)
        {
            if (player?.Entity == null) return;

            player.Entity.WatchedAttributes.SetInt(WATCHED_CLOTHIER_COUNT, progress.UniqueClothesWorn.Count);
            player.Entity.WatchedAttributes.SetBool(WATCHED_CLOTHIER_UNLOCKED, progress.SewingKitUnlocked);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CLOTHIER_COUNT);

            // Update extraTraits to show Clothier trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, CLOTHIER_TRAIT_CODE, progress.SewingKitUnlocked);

            // IMPORTANT: Add "clothier" to extraTraits to unlock sewing kit recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the sewing kit requires "clothier")
            UpdateExtraTraitStatic(player.Entity, "clothier", progress.SewingKitUnlocked);
        }
    }
}
