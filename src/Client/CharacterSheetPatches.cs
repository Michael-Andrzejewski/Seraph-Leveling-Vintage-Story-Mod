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
    /// <summary>
    /// Harmony patch methods for CharacterSystem.
    /// </summary>
    public static class CharacterSystemPatches
    {
        // Reference to the client API, set during patch application
        public static ICoreClientAPI ClientApi { get; set; }

        /// <summary>
        /// Postfix for getClassTraitText - adds dynamic mining and melee progression info.
        /// The method has NO parameters - it's an instance method on CharacterSystem.
        /// </summary>
        public static void GetClassTraitText_Postfix(ref string __result)
        {
            // Get the player entity from the client API
            if (ClientApi == null) return;

            EntityPlayer eplr = ClientApi.World?.Player?.Entity;
            if (eplr == null) return;

            // Log the raw result string to see exact format (escape special chars for visibility)
            string escapedResult = __result?.Replace("\n", "\\n").Replace("\r", "\\r") ?? "NULL";
            ClientApi.Logger.Debug($"[SeraphLeveling] RAW getClassTraitText result: {escapedResult}");

            // Get mining progression data
            int miningLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_LEVEL, 0);
            int miningBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MINING_BONUS, 0);
            bool hasVanillaHardy = ClientHasVanillaTrait(eplr, "hardy", "blackguard");

            // Get melee progression data
            int meleeLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_LEVEL, 0);
            int meleeBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MELEE_BONUS, 0);
            bool hasVanillaSoldier = ClientHasVanillaTrait(eplr, "soldier", "blackguard");

            // Get ranged progression data
            int rangedLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_LEVEL, 0);
            int rangedDamageBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_DAMAGE_BONUS, 0);
            int rangedAccuracyBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_ACCURACY_BONUS, 0);
            int rangedDistanceBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RANGED_DISTANCE_BONUS, 0);
            bool hasVanillaFocused = ClientHasVanillaTrait(eplr, "focused", "hunter");

            // Get walking progression data
            int walkingLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_LEVEL, 0);
            int walkingBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WALKING_BONUS, 0);
            bool hasVanillaFleetfooted = ClientHasVanillaTrait(eplr, "fleetfooted", "hunter", "clockmaker");

            // Get armor progression data
            int armorDurabilityLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_LEVEL, 0);
            int armorDurabilityBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_DURABILITY_BONUS, 0);
            int armorWalkSpeedLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_LEVEL, 0);
            int armorWalkSpeedBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_ARMOR_WALKSPEED_BONUS, 0);
            bool hasVanillaSoldierArmor = ClientHasVanillaTrait(eplr, "soldier", "blackguard");

            ClientApi.Logger.Debug($"[SeraphLeveling] getClassTraitText postfix called. Mining: Level={miningLevel}, Bonus={miningBonus}%, HasHardy={hasVanillaHardy} | Melee: Level={meleeLevel}, Bonus={meleeBonus}%, HasSoldier={hasVanillaSoldier} | Ranged: Level={rangedLevel}, HasFocused={hasVanillaFocused} | Walking: Level={walkingLevel}, HasFleetfooted={hasVanillaFleetfooted} | Armor: Dur={armorDurabilityLevel}, Walk={armorWalkSpeedLevel}");

            // Get the "no traits" message - vanilla uses this for classes like Commoner.
            // The actual vanilla lang key is the literal phrase "No positive or negative traits"
            // (yes, the key contains spaces). The previous "charactersheet-notraits" key doesn't
            // exist in vanilla, so Lang.Get returned the key string itself, defeating the
            // localization check. Subsequent `Contains("No positive or negative traits")`
            // fallbacks below are kept as a defense-in-depth English match.
            string noTraitsMsg = Lang.Get("No positive or negative traits");

            ClientApi.Logger.Debug($"[SeraphLeveling] Original result: '{__result}', noTraitsMsg: '{noTraitsMsg}'");

            // Check if we have NO real traits (only "no traits" message or empty)
            // Use Contains to handle cases where the message might have formatting
            bool hasNoTraits = string.IsNullOrEmpty(__result) ||
                               __result.Trim() == noTraitsMsg.Trim() ||
                               __result == noTraitsMsg ||
                               __result.Contains(noTraitsMsg) ||
                               __result.Contains("No positive or negative traits");

            // Process mining progression (Hardy trait)
            // Only show Hardy when miningBonus > 0 (after negative traits are cancelled)
            if (miningBonus > 0)
            {
                string plainMiningTraitName = Lang.Get("seraphleveling:trait-sitminingmastery");

                if (hasVanillaHardy)
                {
                    // Class already has Hardy (e.g., Blackguard) - update the existing Hardy's
                    // mining speed value inline. Vanilla attribute "miningSpeedMul: 0.1" renders
                    // as "+10% mining speed" via Lang.Get("charattribute-miningSpeedMul-0.1") in
                    // the player's locale; we swap that exact string for the combined value.
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_HARDY_MINING_BONUS + miningBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "miningSpeedMul", 0.1, combinedBonus);

                    // Remove any orphan plain Hardy entry; use orphan-only matching so the
                    // vanilla Hardy line (which has its own description) is left intact.
                    __result = RemoveOrphanTraitName(__result, plainMiningTraitName);
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Hardy
                    // Use mining-only format since we don't have Hardy Health yet
                    __result = BuildLocalizedTraitLine("hardy", "seraphleveling:trait-hardy-mining-only-dynamic", miningBonus);
                    hasNoTraits = false; // We now have traits
                }
                else if (ContainsOrphanTraitName(__result, plainMiningTraitName))
                {
                    // We have our trait but no vanilla Hardy - replace orphan plain name with dynamic version
                    __result = ReplaceOrphanTraitName(__result, plainMiningTraitName,
                        BuildLocalizedTraitLine("hardy", "seraphleveling:trait-hardy-mining-only-dynamic", miningBonus));
                }
                else
                {
                    // Has other traits but no Hardy at all - append our dynamic Hardy
                    __result = __result + "\n" + BuildLocalizedTraitLine("hardy", "seraphleveling:trait-hardy-mining-only-dynamic", miningBonus);
                }
            }

            // Process melee progression (Soldier trait)
            // Only show Soldier melee when meleeBonus > 0 (after negative traits are cancelled)
            if (meleeBonus > 0)
            {
                string plainMeleeTraitName = Lang.Get("seraphleveling:trait-sitmeleemastery");

                // Re-check hasNoTraits after mining processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaSoldier)
                {
                    // Class already has Soldier - update the inline melee damage value (locale-aware).
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_SOLDIER_MELEE_BONUS + meleeBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "meleeWeaponsDamage", 0.3, combinedBonus);

                    __result = RemoveOrphanTraitName(__result, plainMeleeTraitName);
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Soldier
                    __result = BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-dynamic", meleeBonus);
                }
                else if (ContainsOrphanTraitName(__result, plainMeleeTraitName))
                {
                    // We have our trait but no vanilla Soldier - replace orphan plain name with dynamic version
                    __result = ReplaceOrphanTraitName(__result, plainMeleeTraitName,
                        BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-dynamic", meleeBonus));
                }
                else
                {
                    // Has other traits but no Soldier at all - append our dynamic Soldier
                    __result = __result + "\n" + BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-dynamic", meleeBonus);
                }
            }

            // Process ranged progression (Focused trait)
            // Only show Focused when any bonus > 0 (after negative traits are cancelled for that stat)
            if (rangedDamageBonus > 0 || rangedAccuracyBonus > 0 || rangedDistanceBonus > 0)
            {
                string plainRangedTraitName = Lang.Get("seraphleveling:trait-sitrangedmastery");

                // Re-check hasNoTraits after melee processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFocused)
                {
                    // Class already has Focused (e.g., Hunter) - update each existing inline value.
                    int combinedDamage = SeraphLevelingModSystem.VANILLA_FOCUSED_DAMAGE_BONUS + rangedDamageBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "rangedWeaponsDamage", 0.2, combinedDamage);

                    int combinedAccuracy = SeraphLevelingModSystem.VANILLA_FOCUSED_ACCURACY_BONUS + rangedAccuracyBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "rangedWeaponsAcc", 0.3, combinedAccuracy);

                    int combinedDistance = SeraphLevelingModSystem.VANILLA_FOCUSED_DISTANCE_BONUS + rangedDistanceBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "bowDrawingStrength", 0.2, combinedDistance);

                    __result = RemoveOrphanTraitName(__result, plainRangedTraitName);
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Focused
                    __result = BuildLocalizedTraitLine("focused", "seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
                else if (ContainsOrphanTraitName(__result, plainRangedTraitName))
                {
                    // We have our trait but no vanilla Focused - replace orphan plain name with dynamic version
                    __result = ReplaceOrphanTraitName(__result, plainRangedTraitName,
                        BuildLocalizedTraitLine("focused", "seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus));
                }
                else
                {
                    // Has other traits but no Focused at all - append our dynamic Focused
                    __result = __result + "\n" + BuildLocalizedTraitLine("focused", "seraphleveling:trait-focused-dynamic", rangedDamageBonus, rangedAccuracyBonus, rangedDistanceBonus);
                }
            }

            // Process walking progression (Fleetfooted trait)
            if (walkingLevel > 0)
            {
                string plainWalkingTraitName = Lang.Get("seraphleveling:trait-sitwalkingmastery");

                // Re-check hasNoTraits after ranged processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFleetfooted)
                {
                    // Class already has Fleetfooted - update the inline walk speed value (locale-aware).
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_FLEETFOOTED_WALK_BONUS + walkingBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "walkspeed", 0.1, combinedBonus);

                    __result = RemoveOrphanTraitName(__result, plainWalkingTraitName);
                }
                else if (hasNoTraits)
                {
                    // Commoner or other class with no traits - replace entirely with our dynamic Fleetfooted
                    __result = BuildLocalizedTraitLine("fleetfooted", "seraphleveling:trait-fleetfooted-dynamic", walkingBonus);
                }
                else if (ContainsOrphanTraitName(__result, plainWalkingTraitName))
                {
                    // We have our trait but no vanilla Fleetfooted - replace orphan plain name with dynamic version
                    __result = ReplaceOrphanTraitName(__result, plainWalkingTraitName,
                        BuildLocalizedTraitLine("fleetfooted", "seraphleveling:trait-fleetfooted-dynamic", walkingBonus));
                }
                else
                {
                    // Has other traits but no Fleetfooted at all - append our dynamic Fleetfooted
                    __result = __result + "\n" + BuildLocalizedTraitLine("fleetfooted", "seraphleveling:trait-fleetfooted-dynamic", walkingBonus);
                }
            }

            // Process armor progression (Soldier trait - armor durability and speed penalty)
            if (armorDurabilityLevel > 0 || armorWalkSpeedLevel > 0)
            {
                // Re-check hasNoTraits after walking processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                // Calculate combined bonuses
                int totalDurabilityBonus = armorDurabilityBonus;
                int totalWalkSpeedBonus = armorWalkSpeedBonus;

                if (hasVanillaSoldierArmor)
                {
                    // Class already has Soldier (Blackguard) - update the inline armor stats (locale-aware).
                    // Vanilla attributes:
                    //   armorDurabilityLoss: -0.15 → "+15% armor durability"
                    //   armorWalkSpeedAffectedness: -0.25 → "walk speed penalty for wearing armor reduced by 25%"
                    if (armorDurabilityBonus > 0)
                    {
                        int combinedDurability = SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS + armorDurabilityBonus;
                        __result = ReplaceVanillaCharAttribute(__result, "armorDurabilityLoss", -0.15, combinedDurability);
                    }

                    if (armorWalkSpeedBonus > 0)
                    {
                        int combinedSpeedPenalty = SeraphLevelingModSystem.VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS + armorWalkSpeedBonus;
                        __result = ReplaceVanillaCharAttribute(__result, "armorWalkSpeedAffectedness", -0.25, combinedSpeedPenalty);
                    }
                }
                else if (hasNoTraits)
                {
                    // No traits at all - show our armor progression as a Soldier-like trait
                    __result = BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                }
                else
                {
                    // Has other traits but no vanilla Soldier - check if we already added melee Soldier
                    // Only add if we have actual bonuses to show
                    if (totalDurabilityBonus > 0 || totalWalkSpeedBonus > 0)
                    {
                        // Check if melee progression already added a dynamic Soldier entry
                        string meleeSoldierPattern = BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-dynamic", meleeBonus);
                        if (meleeLevel > 0 && __result.Contains(meleeSoldierPattern))
                        {
                            // Replace the melee-only Soldier with a combined entry
                            __result = __result.Replace(meleeSoldierPattern,
                                BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-combined-dynamic", meleeBonus, totalDurabilityBonus, totalWalkSpeedBonus));
                        }
                        else
                        {
                            // No melee Soldier was added, add armor-only entry
                            __result = __result + "\n" + BuildLocalizedTraitLine("soldier", "seraphleveling:trait-soldier-armor-dynamic", totalDurabilityBonus, totalWalkSpeedBonus);
                        }
                    }
                }
            }

            // Process Clothier trait (unlocked by wearing 20 unique clothes)
            bool clothierUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_CLOTHIER_UNLOCKED, false);
            if (clothierUnlocked && !ClientHasVanillaTrait(eplr, "clothier", "tailor"))
            {
                string plainClothierTraitName = Lang.Get("seraphleveling:trait-sitclothiermastery");
                string dynamicClothierTrait = BuildLocalizedTraitLine("clothier", "seraphleveling:trait-clothier-dynamic");

                // Re-check hasNoTraits after armor processing
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicClothierTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainClothierTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainClothierTraitName, dynamicClothierTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicClothierTrait;
                }
            }

            // Process Mender trait (improves armor/clothing durability)
            int menderLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MENDER_LEVEL, 0);
            int menderBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_MENDER_BONUS, 0);
            bool hasVanillaMender = ClientHasVanillaTrait(eplr, "mender", "tailor");
            if (menderLevel > 0)
            {
                string plainMenderTraitName = Lang.Get("seraphleveling:trait-sitmendermastery");
                string dynamicMenderTrait = BuildLocalizedTraitLine("mender", "seraphleveling:trait-mender-dynamic", menderBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaMender)
                {
                    // Class already has Mender trait (Tailor) - update the inline durability value (locale-aware).
                    // Vanilla armorDurabilityLoss: -0.25 renders as "+25% armor durability".
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_MENDER_ARMOR_DURABILITY_BONUS + menderBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "armorDurabilityLoss", -0.25, combinedBonus);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicMenderTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainMenderTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainMenderTraitName, dynamicMenderTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicMenderTrait;
                }
            }

            // Process Pilferer trait (improves rusty gear, vessel loot, vessel collection).
            // Read per-stat bonuses (each stat capped independently so all classes hit the
            // same +20% per-stat total at maxall, regardless of vanilla Pilferer offsets).
            int pilfererLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_LEVEL, 0);
            int pilfererVesselBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_VESSEL_BONUS, 0);
            int pilfererRustyBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_RUSTY_BONUS, 0);
            int pilfererWholeBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PILFERER_WHOLE_BONUS, 0);
            bool hasVanillaPilferer = ClientHasVanillaTrait(eplr, "pilferer", "malefactor");
            // Only show Pilferer when any per-stat bonus > 0 (after Heavyhanded vessel penalty is cancelled for vessel)
            if (pilfererVesselBonus > 0 || pilfererRustyBonus > 0 || pilfererWholeBonus > 0)
            {
                string plainPilfererTraitName = Lang.Get("seraphleveling:trait-sitpilferermastery");
                // Dynamic format params are (vesselContents, rustyGear, wholeVessel)
                string dynamicPilfererTrait = BuildLocalizedTraitLine("pilferer", "seraphleveling:trait-pilferer-dynamic",
                    pilfererVesselBonus, pilfererRustyBonus, pilfererWholeBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaPilferer)
                {
                    // Class already has Pilferer trait (Malefactor) - replace the whole vanilla
                    // line with our combined dynamic. Substring Replace doesn't work cleanly here:
                    //   - vanilla "+15% drop rate" (vesselContentsDropRate) is too generic to
                    //     substring-match safely without risk of hitting unrelated tokens
                    //   - vanilla "12% chance to collect cracked vessels intact" doesn't follow
                    //     the "+X% <thing>" pattern, so a value-prefixed Replace target can't hit it
                    // Combine per-stat earned with per-stat vanilla so each total caps correctly.
                    int combinedVessel = SeraphLevelingModSystem.VANILLA_PILFERER_VESSEL_CONTENTS_BONUS + pilfererVesselBonus;
                    int combinedRusty = SeraphLevelingModSystem.VANILLA_PILFERER_RUSTY_GEAR_BONUS + pilfererRustyBonus;
                    int combinedWhole = SeraphLevelingModSystem.VANILLA_PILFERER_WHOLE_VESSEL_BONUS + pilfererWholeBonus;
                    string combinedPilfererTrait = BuildLocalizedTraitLine("pilferer", "seraphleveling:trait-pilferer-dynamic",
                        combinedVessel, combinedRusty, combinedWhole);
                    __result = ReplaceVanillaTraitLine(__result, "pilferer", combinedPilfererTrait);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicPilfererTrait;
                    hasNoTraits = false;
                }
                else if (ContainsOrphanTraitName(__result, plainPilfererTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainPilfererTraitName, dynamicPilfererTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicPilfererTrait;
                }
            }

            // Process Resourceful trait (improves animal loot and harvesting speed)
            int resourcefulLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_LEVEL, 0);
            int resourcefulLootBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_LOOT_BONUS, 0);
            int resourcefulSpeedBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RESOURCEFUL_SPEED_BONUS, 0);
            bool hasVanillaResourceful = ClientHasVanillaTrait(eplr, "resourceful", "hunter");
            // Only show Resourceful when any bonus > 0 (after Kind penalty is cancelled)
            if (resourcefulLootBonus > 0 || resourcefulSpeedBonus > 0)
            {
                string plainResourcefulTraitName = Lang.Get("seraphleveling:trait-sitresourcefulmastery");
                string dynamicResourcefulTrait = BuildLocalizedTraitLine("resourceful", "seraphleveling:trait-resourceful-dynamic", resourcefulLootBonus, resourcefulSpeedBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaResourceful)
                {
                    // Class already has Resourceful trait - update the inline values (locale-aware).
                    // Vanilla animalLootDropRate: 0.1 → "+10% animal loot"
                    // Vanilla animalHarvestingTime: -0.25 → "+25% animal harvesting speed"
                    int combinedLoot = SeraphLevelingModSystem.VANILLA_RESOURCEFUL_LOOT_BONUS + resourcefulLootBonus;
                    int combinedSpeed = SeraphLevelingModSystem.VANILLA_RESOURCEFUL_SPEED_BONUS + resourcefulSpeedBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "animalLootDropRate", 0.1, combinedLoot);
                    __result = ReplaceVanillaCharAttribute(__result, "animalHarvestingTime", -0.25, combinedSpeed);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicResourcefulTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainResourcefulTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainResourcefulTraitName, dynamicResourcefulTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicResourcefulTrait;
                }
            }

            // Process Forager trait (improves foraging loot and wild crop drops)
            int foragerLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_LEVEL, 0);
            int foragerLootBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_LOOT_BONUS, 0);
            int foragerWildCropBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FORAGER_WILD_CROP_BONUS, 0);
            bool hasVanillaForager = ClientHasVanillaTrait(eplr, "forager", "malefactor");
            // Only show Forager when any bonus > 0 (after Civil/Heavyhanded penalties are cancelled)
            if (foragerLootBonus > 0 || foragerWildCropBonus > 0)
            {
                string plainForagerTraitName = Lang.Get("seraphleveling:trait-sitforagermastery");
                string dynamicForagerTrait = BuildLocalizedTraitLine("forager", "seraphleveling:trait-forager-dynamic", foragerLootBonus, foragerWildCropBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaForager)
                {
                    // Class already has Forager trait - update the inline values (locale-aware).
                    // Vanilla forageDropRate: 0.1 → "+10% loot from foraging"
                    // Vanilla wildCropDropRate: 0.2 → "+20% wild crop drop rate"
                    int combinedLoot = SeraphLevelingModSystem.VANILLA_FORAGER_LOOT_BONUS + foragerLootBonus;
                    int combinedWildCrop = SeraphLevelingModSystem.VANILLA_FORAGER_WILD_CROP_BONUS + foragerWildCropBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "forageDropRate", 0.1, combinedLoot);
                    __result = ReplaceVanillaCharAttribute(__result, "wildCropDropRate", 0.2, combinedWildCrop);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicForagerTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainForagerTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainForagerTraitName, dynamicForagerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicForagerTrait;
                }
            }

            // Process Furtive trait (reduces animal detection range)
            int furtiveLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FURTIVE_LEVEL, 0);
            int furtiveBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FURTIVE_BONUS, 0);
            bool hasVanillaFurtive = ClientHasVanillaTrait(eplr, "furtive", "malefactor");
            if (furtiveLevel > 0)
            {
                string plainFurtiveTraitName = Lang.Get("seraphleveling:trait-sitfurtivemastery");
                string dynamicFurtiveTrait = BuildLocalizedTraitLine("furtive", "seraphleveling:trait-furtive-dynamic", furtiveBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaFurtive)
                {
                    // Class already has Furtive trait - update the inline detection range value (locale-aware).
                    // Vanilla animalSeekingRange: -0.35 → "-35% animal detection range"
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_FURTIVE_DETECTION_REDUCTION + furtiveBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "animalSeekingRange", -0.35, combinedBonus);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicFurtiveTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainFurtiveTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainFurtiveTraitName, dynamicFurtiveTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicFurtiveTrait;
                }
            }

            // Process Precise trait (improves damage to mechanicals)
            int preciseLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PRECISE_LEVEL, 0);
            int preciseBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_PRECISE_BONUS, 0);
            bool hasVanillaPrecise = ClientHasVanillaTrait(eplr, "precise", "clockmaker");
            if (preciseLevel > 0)
            {
                string plainPreciseTraitName = Lang.Get("seraphleveling:trait-sitprecisemastery");
                string dynamicPreciseTrait = BuildLocalizedTraitLine("precise", "seraphleveling:trait-precise-dynamic", preciseBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasVanillaPrecise)
                {
                    // Class already has Precise trait (Clockmaker) - update the inline value (locale-aware).
                    // Vanilla mechanicalsDamage: 0.4705 (calibrated) → "+25% damage against mechanicals"
                    int combinedBonus = SeraphLevelingModSystem.VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + preciseBonus;
                    __result = ReplaceVanillaCharAttribute(__result, "mechanicalsDamage", 0.4705, combinedBonus);
                }
                else if (hasNoTraits)
                {
                    __result = dynamicPreciseTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainPreciseTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainPreciseTraitName, dynamicPreciseTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicPreciseTrait;
                }
            }

            // Experimental temporal traits. The server mirrors the percent as 0 while a
            // trait is disabled and the join packet carries the switches, so a disabled
            // trait never shows in the list.
            int temporalResistPct = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_TEMPORAL_RESISTANCE_PERCENT, 0);
            if (SeraphLevelingModSystem.TemporalResistanceEnabled && temporalResistPct > 0)
            {
                AppendSeraphOnlyTrait(ref __result, ref hasNoTraits, noTraitsMsg,
                    SeraphLevelingModSystem.TEMPORAL_RESISTANCE_TRAIT_CODE, "seraphleveling:trait-temporalresistance-dynamic", temporalResistPct);
            }
            int temporalRechargePct = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_TEMPORAL_RECHARGE_PERCENT, 0);
            if (SeraphLevelingModSystem.TemporalRechargeEnabled && temporalRechargePct > 0)
            {
                AppendSeraphOnlyTrait(ref __result, ref hasNoTraits, noTraitsMsg,
                    SeraphLevelingModSystem.TEMPORAL_RECHARGE_TRAIT_CODE, "seraphleveling:trait-temporalrecharge-dynamic", temporalRechargePct);
            }

            // Process Hunger trait (reduces hunger rate).
            // For Ravenous classes (Blackguard) the raw `hungerBonus` value includes credits
            // spent cancelling the +30% Ravenous penalty. The actual stat applied (negative
            // hungerrate) cancels Ravenous and produces the same NET reduction other classes
            // get from their full bonus, so functional behavior already matches across classes.
            // The display, however, was showing the gross reduction (e.g. -55% on Blackguard
            // vs -25% on Hunter at maxall). Subtract the Ravenous penalty from the displayed
            // value so all classes converge on the same visible cap (-25% at maxall).
            int hungerLevel = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_LEVEL, 0);
            int hungerBonus = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HUNGER_BONUS, 0);
            bool hasRavenousForHunger = ClientHasVanillaTrait(eplr, "ravenous", "blackguard");
            int displayedHungerReduction = hasRavenousForHunger
                ? Math.Max(0, hungerBonus - SeraphLevelingModSystem.VANILLA_RAVENOUS_HUNGER_PENALTY)
                : hungerBonus;
            if (hungerLevel > 0 && displayedHungerReduction > 0)
            {
                string plainHungerTraitName = Lang.Get("seraphleveling:trait-sithungermastery");
                string dynamicHungerTrait = Lang.Get("seraphleveling:trait-hunger-dynamic", displayedHungerReduction);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicHungerTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainHungerTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainHungerTraitName, dynamicHungerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicHungerTrait;
                }
            }

            // Process Technical unlock trait (translocator gear cost reduction)
            bool technicalUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_TECHNICAL_UNLOCKED, false);
            if (technicalUnlocked && !ClientHasVanillaTrait(eplr, "technical", "clockmaker"))
            {
                string plainTechnicalTraitName = Lang.Get("seraphleveling:trait-sittechnicalmastery");
                string dynamicTechnicalTrait = BuildLocalizedTraitLine("technical", "seraphleveling:trait-technical-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicTechnicalTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainTechnicalTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainTechnicalTraitName, dynamicTechnicalTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicTechnicalTrait;
                }
            }

            // Process Hardy Health unlock trait (+5 HP)
            bool hardyHealthUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_HARDY_HEALTH_UNLOCKED, false);
            if (hardyHealthUnlocked)
            {
                string plainHardyHealthTraitName = Lang.Get("seraphleveling:trait-sithardyhealthmastery");
                string dynamicHardyHealthTrait = BuildLocalizedTraitLine("hardy", "seraphleveling:trait-hardyhealth-dynamic", SeraphLevelingModSystem.HardyHealthBonus);

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                // If the Mining block already added a mining-only Hardy line for this player
                // (no vanilla Hardy class), fold the health bonus INTO that line instead of
                // adding a second `• Hardy` entry that would render as a duplicate.
                var miningOnlyHardyMatch = System.Text.RegularExpressions.Regex.Match(__result,
                    @"<font color=""#84ff84"">• Hardy </font> <font opacity=""0\.6"">\(\+(?<m>\d+)% mining speed\)</font>");
                if (miningOnlyHardyMatch.Success)
                {
                    int miningPct = int.Parse(miningOnlyHardyMatch.Groups["m"].Value);
                    string combined = BuildLocalizedTraitLine("hardy", "seraphleveling:trait-hardy-dynamic", miningPct, SeraphLevelingModSystem.HardyHealthBonus);
                    __result = __result.Substring(0, miningOnlyHardyMatch.Index)
                        + combined
                        + __result.Substring(miningOnlyHardyMatch.Index + miningOnlyHardyMatch.Length);
                }
                // If vanilla Hardy line already lists health points (Blackguard), skip — the
                // bonus is already represented by the vanilla line. (Stat is still applied via
                // ApplyHardyHealthBonusStatic; this only avoids a duplicate display row.)
                else if (System.Text.RegularExpressions.Regex.IsMatch(__result,
                    @"<font color=""#84ff84"">• Hardy </font> <font opacity=""0\.6"">\([^)]*\d+ health points[^)]*\)</font>"))
                {
                    // intentional no-op
                }
                else if (hasNoTraits)
                {
                    __result = dynamicHardyHealthTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainHardyHealthTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainHardyHealthTraitName, dynamicHardyHealthTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicHardyHealthTrait;
                }
            }

            // Process Bowyer unlock trait (crude bow crafting)
            bool bowyerUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_BOWYER_UNLOCKED, false);
            if (bowyerUnlocked && !ClientHasVanillaTrait(eplr, "bowyer", "hunter"))
            {
                // Skip if the player's class already shows a vanilla Bowyer line — vanilla covers it.
                // (The unlock stat is still applied via ApplyBowyerBonusStatic; this only controls display.)
                string plainBowyerTraitName = Lang.Get("seraphleveling:trait-sitbowyermastery");
                string dynamicBowyerTrait = BuildLocalizedTraitLine("bowyer", "seraphleveling:trait-bowyer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicBowyerTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainBowyerTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainBowyerTraitName, dynamicBowyerTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicBowyerTrait;
                }
            }

            // Process Improviser unlock trait (sling crafting)
            bool improviserUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_IMPROVISER_UNLOCKED, false);
            if (improviserUnlocked && !ClientHasVanillaTrait(eplr, "improviser", "malefactor"))
            {
                string plainImproviserTraitName = Lang.Get("seraphleveling:trait-sitimprovisermastery");
                string dynamicImproviserTrait = BuildLocalizedTraitLine("improviser", "seraphleveling:trait-improviser-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicImproviserTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainImproviserTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainImproviserTraitName, dynamicImproviserTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicImproviserTrait;
                }
            }

            // Process Tinkerer unlock trait (tuning spear crafting)
            bool tinkererUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_TINKERER_UNLOCKED, false);
            if (tinkererUnlocked && !ClientHasVanillaTrait(eplr, "tinkerer", "clockmaker"))
            {
                string plainTinkererTraitName = Lang.Get("seraphleveling:trait-sittinkerermastery");
                string dynamicTinkererTrait = BuildLocalizedTraitLine("tinkerer", "seraphleveling:trait-tinkerer-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicTinkererTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainTinkererTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainTinkererTraitName, dynamicTinkererTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicTinkererTrait;
                }
            }

            // Process Merciless unlock trait (shortsword/shield crafting)
            bool mercilessUnlocked = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_MERCILESS_UNLOCKED, false);
            if (mercilessUnlocked && !ClientHasVanillaTrait(eplr, "merciless", "blackguard"))
            {
                string plainMercilessTraitName = Lang.Get("seraphleveling:trait-sitmercilessmastery");
                string dynamicMercilessTrait = BuildLocalizedTraitLine("merciless", "seraphleveling:trait-merciless-dynamic");

                // Re-check hasNoTraits
                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = dynamicMercilessTrait;
                }
                else if (ContainsOrphanTraitName(__result, plainMercilessTraitName))
                {
                    __result = ReplaceOrphanTraitName(__result, plainMercilessTraitName, dynamicMercilessTrait);
                }
                else
                {
                    __result = __result + "\n" + dynamicMercilessTrait;
                }
            }

            // Note: Claustrophobic Removed trait display was removed in favor of progressive cancellation
            // Claustrophobic is now handled in the negative trait section below - it progressively
            // decreases with mining level (1-10) and is replaced by Hardy when cancelled

            // =========================================================================
            // NEGATIVE TRAIT DISPLAY HANDLING
            // Display negative traits with remaining penalty, or remove when cancelled
            // =========================================================================

            // With CancelNegativeTraits off the server never pays a penalty down, so the
            // vanilla lines must stay exactly as the game wrote them.
            bool cancelOn = SeraphLevelingModSystem.CancelNegativeTraits;

            // Civil trait (Tailor) - foraging loot penalty
            bool hasCivil = cancelOn && ClientHasVanillaTrait(eplr, "civil", "tailor");
            int civilRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CIVIL_REMAINING, 0);
            if (hasCivil)
            {
                if (civilRemaining > 0)
                {
                    string dynamicCivilTrait = BuildLocalizedTraitLine("civil", "seraphleveling:trait-civil-dynamic", civilRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "civil", dynamicCivilTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "civil");
                }
            }

            // Weak trait (Tailor) - HP and mining speed penalty.
            // Both penalties cancelled together at mining level 10.
            bool hasWeak = cancelOn && ClientHasVanillaTrait(eplr, "weak", "tailor");
            int weakMiningRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WEAK_MINING_REMAINING, 0);
            int weakHpRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_WEAK_HP_REMAINING, 0);
            if (hasWeak)
            {
                if (weakMiningRemaining > 0 || weakHpRemaining > 0)
                {
                    string dynamicWeakTrait = BuildLocalizedTraitLine("weak", "seraphleveling:trait-weak-dynamic", weakHpRemaining, weakMiningRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "weak", dynamicWeakTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "weak");
                }
            }

            // Kind trait (Tailor) - animal loot and harvesting speed penalty.
            bool hasKind = cancelOn && ClientHasVanillaTrait(eplr, "kind", "tailor");
            int kindLootRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_KIND_LOOT_REMAINING, 0);
            int kindSpeedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_KIND_SPEED_REMAINING, 0);
            if (hasKind)
            {
                if (kindLootRemaining > 0 || kindSpeedRemaining > 0)
                {
                    string dynamicKindTrait;
                    if (kindLootRemaining > 0 && kindSpeedRemaining > 0)
                    {
                        dynamicKindTrait = BuildLocalizedTraitLine("kind", "seraphleveling:trait-kind-dynamic", kindLootRemaining, kindSpeedRemaining);
                    }
                    else if (kindLootRemaining > 0)
                    {
                        dynamicKindTrait = BuildLocalizedTraitLine("kind", "seraphleveling:trait-kind-loot-only-dynamic", kindLootRemaining);
                    }
                    else
                    {
                        dynamicKindTrait = BuildLocalizedTraitLine("kind", "seraphleveling:trait-kind-speed-only-dynamic", kindSpeedRemaining);
                    }
                    __result = ReplaceVanillaTraitLine(__result, "kind", dynamicKindTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "kind");
                }
            }

            // Farsighted trait (Hunter) - melee damage penalty
            bool hasFarsighted = cancelOn && ClientHasVanillaTrait(eplr, "farsighted", "hunter");
            int farsightedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FARSIGHTED_REMAINING, 0);
            if (hasFarsighted)
            {
                if (farsightedRemaining > 0)
                {
                    string dynamicFarsightedTrait = BuildLocalizedTraitLine("farsighted", "seraphleveling:trait-farsighted-dynamic", farsightedRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "farsighted", dynamicFarsightedTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "farsighted");
                }
            }

            // Nervous trait (Malefactor, Clockmaker) - melee damage penalty
            bool hasNervous = cancelOn && ClientHasVanillaTrait(eplr, "nervous", "malefactor", "clockmaker");
            int nervousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_NERVOUS_REMAINING, 0);
            if (hasNervous)
            {
                if (nervousRemaining > 0)
                {
                    string dynamicNervousTrait = BuildLocalizedTraitLine("nervous", "seraphleveling:trait-nervous-dynamic", nervousRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "nervous", dynamicNervousTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "nervous");
                }
            }

            // Nearsighted trait (Blackguard) - ranged damage penalty
            bool hasNearsighted = cancelOn && ClientHasVanillaTrait(eplr, "nearsighted", "blackguard");
            int nearsightedRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_NEARSIGHTED_REMAINING, 0);
            if (hasNearsighted)
            {
                if (nearsightedRemaining > 0)
                {
                    string dynamicNearsightedTrait = BuildLocalizedTraitLine("nearsighted", "seraphleveling:trait-nearsighted-dynamic", nearsightedRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "nearsighted", dynamicNearsightedTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "nearsighted");
                }
            }

            // Frail trait (Malefactor, Clockmaker) - HP and ranged distance penalty.
            // Both penalties cancelled together at ranged level 25.
            bool hasFrail = cancelOn && ClientHasVanillaTrait(eplr, "frail", "malefactor", "clockmaker");
            int frailDistanceRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_FRAIL_DISTANCE_REMAINING, 0);
            float frailHpRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_FRAIL_HP_REMAINING, 0f);
            if (hasFrail)
            {
                if (frailDistanceRemaining > 0 || frailHpRemaining > 0)
                {
                    string dynamicFrailTrait = BuildLocalizedTraitLine("frail", "seraphleveling:trait-frail-dynamic", frailHpRemaining, frailDistanceRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "frail", dynamicFrailTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "frail");
                }
            }

            // Heavyhanded trait (Blackguard) - vessel, foraging, wild crop penalties.
            bool hasHeavyhanded = cancelOn && ClientHasVanillaTrait(eplr, "heavyhanded", "blackguard");
            int heavyhandedVesselRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_VESSEL_REMAINING, 0);
            int heavyhandedForagingRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_FORAGING_REMAINING, 0);
            int heavyhandedWildCropRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, 0);
            if (hasHeavyhanded)
            {
                if (heavyhandedVesselRemaining > 0 || heavyhandedForagingRemaining > 0 || heavyhandedWildCropRemaining > 0)
                {
                    // Build partial description from localized vanilla charattribute strings so
                    // the partial line stays in the same locale as the rest of vanilla output.
                    var parts = new System.Collections.Generic.List<string>();
                    if (heavyhandedVesselRemaining > 0)
                    {
                        string s = LocalizedCharAttributeWithValue("vesselContentsDropRate", -0.1, heavyhandedVesselRemaining);
                        if (!string.IsNullOrEmpty(s)) parts.Add(s);
                    }
                    if (heavyhandedForagingRemaining > 0)
                    {
                        string s = LocalizedCharAttributeWithValue("forageDropRate", -0.15, heavyhandedForagingRemaining);
                        if (!string.IsNullOrEmpty(s)) parts.Add(s);
                    }
                    if (heavyhandedWildCropRemaining > 0)
                    {
                        string s = LocalizedCharAttributeWithValue("wildCropDropRate", -0.2, heavyhandedWildCropRemaining);
                        if (!string.IsNullOrEmpty(s)) parts.Add(s);
                    }

                    string partialDescription = string.Join(", ", parts);
                    string dynamicHeavyhandedTrait = BuildLocalizedTraitLine("heavyhanded", "seraphleveling:trait-heavyhanded-partial-dynamic", partialDescription);
                    __result = ReplaceVanillaTraitLine(__result, "heavyhanded", dynamicHeavyhandedTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "heavyhanded");
                }
            }

            // Ravenous trait (Blackguard) - hunger rate penalty
            bool hasRavenous = cancelOn && ClientHasVanillaTrait(eplr, "ravenous", "blackguard");
            int ravenousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_RAVENOUS_REMAINING, 0);
            if (hasRavenous)
            {
                if (ravenousRemaining > 0)
                {
                    string dynamicRavenousTrait = BuildLocalizedTraitLine("ravenous", "seraphleveling:trait-ravenous-dynamic", ravenousRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "ravenous", dynamicRavenousTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "ravenous");
                }
            }

            // Claustrophobic trait (Hunter) - ore drop and mining speed penalties.
            // Mining penalty decreases progressively with mining level (1-10).
            // At level 10, both mining and ore penalties are cancelled, and Hardy bonus shows instead.
            bool hasClaustrophobic = cancelOn && ClientHasVanillaTrait(eplr, "claustrophobic", "hunter");
            int claustrophobicMiningRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, 0);

            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining > 0)
                {
                    string dynamicClaustrophobicTrait = BuildLocalizedTraitLine("claustrophobic", "seraphleveling:trait-claustrophobic-dynamic",
                        SeraphLevelingModSystem.VANILLA_CLAUSTROPHOBIC_ORE_PENALTY, claustrophobicMiningRemaining);
                    __result = ReplaceVanillaTraitLine(__result, "claustrophobic", dynamicClaustrophobicTrait);
                }
                else
                {
                    __result = RemoveVanillaTraitLine(__result, "claustrophobic");
                }
            }

            // =========================================================================
            // COMBAT OVERHAUL PROFICIENCY TRAIT DISPLAY
            // Display CO proficiencies that have credits > 0
            // =========================================================================

            // Defense-in-depth: even if a save has stale CO watched attrs from a previous
            // installation, never render CO trait lines when CO itself isn't installed.
            // The Apply/cache layer should already keep these attrs at 0/false when CO is
            // unloaded (via the IsCombatOverhaulLoaded gate in PopulateVanillaTraitsCache),
            // but skipping the entire section also avoids any future code path leaking a
            // phantom CO debuff through.
            if (SeraphLevelingModSystem.IsCOCompatEnabled)
            {

            // Check if CO is enabled by looking for any CO credits
            var coProficiencies = new (string statName, string displayName, float maxBonus)[]
            {
                ("bowsProficiency", "Bows Proficiency", 0.5f),
                ("crossbowsProficiency", "Crossbows Proficiency", 0.5f),
                ("firearmsProficiency", "Firearms Proficiency", 0.5f),
                ("slingsProficiency", "Slings Proficiency", 0.3f),
                ("oneHandedSwordsProficiency", "One-Handed Swords", 0.3f),
                ("twoHandedSwordsProficiency", "Two-Handed Swords", 0.3f),
                ("spearsProficiency", "Spears Proficiency", 0.3f),
                ("javelinsProficiency", "Javelins Proficiency", 0.3f),
                ("macesProficiency", "Maces Proficiency", 0.3f),
                ("clubsProficiency", "Clubs Proficiency", 0.3f),
                ("halberdsProficiency", "Halberds Proficiency", 0.3f),
                ("poleaxeProficiency", "Poleaxe Proficiency", 0.3f),
                ("axesProficiency", "Axes Proficiency", 0.3f),
                ("quarterstaffProficiency", "Quarterstaff Proficiency", 0.3f),
            };

            foreach (var (statName, displayName, maxBonus) in coProficiencies)
            {
                string watchedKey = $"sitCO{statName}Credits";
                int credits = eplr.WatchedAttributes.GetInt(watchedKey, 0);
                if (credits > 0)
                {
                    float bonus = credits * 0.01f;
                    if (bonus > maxBonus) bonus = maxBonus;
                    string coTrait = $"<font color=\"#84ff84\">• {displayName} </font> <font opacity=\"0.6\">(+{bonus * 100:F0}% attack speed)</font>";

                    // Re-check hasNoTraits
                    hasNoTraits = string.IsNullOrEmpty(__result) ||
                                  __result.Trim() == noTraitsMsg.Trim() ||
                                  __result == noTraitsMsg ||
                                  __result.Contains(noTraitsMsg) ||
                                  __result.Contains("No positive or negative traits");

                    if (hasNoTraits)
                    {
                        __result = coTrait;
                    }
                    else
                    {
                        __result = __result + "\n" + coTrait;
                    }
                }
            }

            // Steady Aim and Trembling Aim display
            int steadyAimCredits = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_STEADY_AIM_CREDITS, 0);
            float tremblingAimRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_TREMBLING_AIM_REMAINING, 0f);
            bool hasTremblingAim = eplr.WatchedAttributes.GetBool(SeraphLevelingModSystem.WATCHED_CO_HAS_TREMBLING_AIM, false);

            // Calculate net Steady Aim bonus (after cancellation if applicable)
            float steadyAimNetBonus = 0f;
            if (hasTremblingAim)
            {
                // For players with Trembling Aim: first 30 credits cancel the penalty, rest is bonus
                int creditsForBonus = Math.Max(0, steadyAimCredits - 30);
                steadyAimNetBonus = creditsForBonus * 0.01f;
            }
            else
            {
                steadyAimNetBonus = steadyAimCredits * 0.01f;
            }
            if (steadyAimNetBonus > 0.5f) steadyAimNetBonus = 0.5f;

            // Show Steady Aim positive trait only if there's a net bonus
            if (steadyAimNetBonus > 0)
            {
                string steadyAimTrait = $"<font color=\"#84ff84\">• Steady Aim </font> <font opacity=\"0.6\">(+{steadyAimNetBonus * 100:F0}% aim stability)</font>";

                hasNoTraits = string.IsNullOrEmpty(__result) ||
                              __result.Trim() == noTraitsMsg.Trim() ||
                              __result == noTraitsMsg ||
                              __result.Contains(noTraitsMsg) ||
                              __result.Contains("No positive or negative traits");

                if (hasNoTraits)
                {
                    __result = steadyAimTrait;
                }
                else
                {
                    __result = __result + "\n" + steadyAimTrait;
                }
            }

            // Get other CO negative trait remaining values
            float clumsyHandsRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_CLUMSY_HANDS_REMAINING, 0f);
            float weakHandRemaining = eplr.WatchedAttributes.GetFloat(SeraphLevelingModSystem.WATCHED_CO_WEAK_HAND_REMAINING, 0f);
            int fearOfMeleeRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_FEAR_OF_MELEE_REMAINING, 0);
            int coNervousRemaining = eplr.WatchedAttributes.GetInt(SeraphLevelingModSystem.WATCHED_CO_NERVOUS_REMAINING, 0);

            // Show Trembling Aim negative trait only if there's remaining penalty
            if (tremblingAimRemaining > 0)
            {
                string tremblingTrait = $"<font color=\"#ff8484\">• Trembling Aim </font> <font opacity=\"0.6\">(-{tremblingAimRemaining * 100:F0}% steady aim)</font>";
                __result = __result + "\n" + tremblingTrait;
            }

            // Show Clumsy Hands negative trait only if there's remaining penalty
            if (clumsyHandsRemaining > 0)
            {
                string clumsyTrait = $"<font color=\"#ff8484\">• Clumsy Hands </font> <font opacity=\"0.6\">(-{clumsyHandsRemaining * 100:F0}% ranged proficiency)</font>";
                __result = __result + "\n" + clumsyTrait;
            }

            // Show Weak Hand negative trait only if there's remaining penalty
            if (weakHandRemaining > 0)
            {
                string weakHandTrait = $"<font color=\"#ff8484\">• Weak Hand </font> <font opacity=\"0.6\">(-{weakHandRemaining * 100:F0}% ranged proficiency)</font>";
                __result = __result + "\n" + weakHandTrait;
            }

            // Show Fear of Melee negative trait only if there's remaining penalty
            if (fearOfMeleeRemaining > 0)
            {
                string fearTrait = $"<font color=\"#ff8484\">• Fear of Melee </font> <font opacity=\"0.6\">(-{fearOfMeleeRemaining} melee tier)</font>";
                __result = __result + "\n" + fearTrait;
            }

            // Show CO Nervous negative trait only if there's remaining penalty (piercing melee)
            if (coNervousRemaining > 0)
            {
                string coNervousTrait = $"<font color=\"#ff8484\">• Nervous </font> <font opacity=\"0.6\">(-{coNervousRemaining} piercing tier)</font>";
                __result = __result + "\n" + coNervousTrait;
            }

            } // end of IsCombatOverhaulLoaded gate around CO trait display

            // Clean up any newline issues that might have been introduced
            // First normalize line endings (handle \r\n, \r, and \n)
            __result = __result.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove any lines that are empty or whitespace-only
            // Also filter out CO's native negative trait displays when we're handling them
            var lines = __result.Split('\n');
            var nonEmptyLines = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string trimmedLine = line.Trim();
                string lowerLine = trimmedLine.ToLowerInvariant();

                // Filter out CO's native negative trait displays - we handle them ourselves
                // Check for various CO trait name patterns (case-insensitive)
                bool isCONativeTrait = false;

                // Trembling Aim - CO displays as "Trembling Aim (30% more aim drift)" or similar
                if (lowerLine.Contains("trembling") && (lowerLine.Contains("aim") || lowerLine.Contains("drift")))
                {
                    // Only filter if it's NOT our display (our display has "steady aim" in it)
                    if (!lowerLine.Contains("steady aim"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Clumsy Hands - CO displays as "Clumsy Hands" with some description
                if (lowerLine.Contains("clumsy") && lowerLine.Contains("hands"))
                {
                    // Only filter if it's NOT our display (our display has "ranged proficiency" in it)
                    if (!lowerLine.Contains("ranged proficiency"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Weak Hand - CO displays as "Weak Hand" with some description
                if (lowerLine.Contains("weak") && lowerLine.Contains("hand"))
                {
                    // Only filter if it's NOT our display (our display has "ranged proficiency" in it)
                    if (!lowerLine.Contains("ranged proficiency"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Fear of Melee - CO displays as "Fear of Melee" with description
                if (lowerLine.Contains("fear") && lowerLine.Contains("melee"))
                {
                    // Only filter if it's NOT our display (our display has "melee tier" in it)
                    if (!lowerLine.Contains("melee tier"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Nervous - CO displays as "Nervous" with description about piercing damage
                if (lowerLine.Contains("nervous") && (lowerLine.Contains("piercing") || lowerLine.Contains("damage") || lowerLine.Contains("tier")))
                {
                    // Only filter if it's NOT our display (our display has "piercing tier" in it)
                    if (!lowerLine.Contains("piercing tier"))
                    {
                        isCONativeTrait = true;
                    }
                }

                // Filter out CO's native positive proficiency trait displays
                // CO displays proficiencies like "Halberds Proficiency (+30% attack speed)"
                // We replace these with our own dynamic version that shows current progress
                // Only filter if this is NOT our display (our display has green font markup #84ff84)
                if (!lowerLine.Contains("#84ff84"))
                {
                    // Check for proficiency names in the line with a percentage bonus
                    string[] proficiencyNames = new[] {
                        "bows proficiency", "crossbows proficiency", "firearms proficiency", "slings proficiency",
                        "one-handed swords", "two-handed swords", "spears proficiency", "javelins proficiency",
                        "maces proficiency", "clubs proficiency", "halberds proficiency", "poleaxe proficiency", "axes proficiency",
                        "quarterstaff proficiency"
                    };

                    foreach (var profName in proficiencyNames)
                    {
                        // Filter if line has proficiency name AND a percentage (CO's format)
                        // Match patterns like "(+30%", "(+30% attack speed)", etc.
                        if (lowerLine.Contains(profName) && (lowerLine.Contains("attack speed") ||
                            System.Text.RegularExpressions.Regex.IsMatch(lowerLine, @"\(\+\d+%")))
                        {
                            isCONativeTrait = true;
                            break;
                        }
                    }

                    // Steady Aim is separate - filter if it has a percentage but not our markup
                    // CO might display "Steady Aim (+50% reduced aim drift)" or similar
                    if (lowerLine.Contains("steady aim") &&
                        System.Text.RegularExpressions.Regex.IsMatch(lowerLine, @"\(\+\d+%"))
                    {
                        isCONativeTrait = true;
                    }
                }

                if (isCONativeTrait)
                {
                    continue; // Skip CO's native display
                }

                nonEmptyLines.Add(trimmedLine);
            }
            __result = string.Join("\n", nonEmptyLines);

            // Defensive cleanup: collapse any duplicate-bullet patterns introduced
            // by overlapping regex replacements on vanilla trait text. Catches things
            // like "• • Claustrophobic" or nested-font variants regardless of which
            // earlier replacement step produced them.
            __result = CollapseDuplicateBullets(__result);

            // Last line of defense: when another mod (Player Model Library, Gloome
            // Classes, Racial Equality) rewrites vanilla's trait text so none of the
            // orphan matches above fire, the raw key "trait-sitarmormastery" survives
            // at the top of the list. Our own rendered lines never contain that
            // literal (they are localized markup), so any line still carrying a raw
            // sit* key is a leftover and is dropped.
            __result = DropRawTraitKeyLines(__result);

            // Final trim
            __result = __result.Trim();

            ClientApi.Logger.Debug($"[SeraphLeveling] Modified result: {__result}");
        }

        private static string DropRawTraitKeyLines(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("trait-sit", StringComparison.OrdinalIgnoreCase) < 0) return text;
            var kept = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                if (line.IndexOf("trait-sit", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                kept.Add(line);
            }
            return string.Join("\n", kept);
        }

        private static readonly System.Text.RegularExpressions.Regex DuplicateBulletPair =
            new System.Text.RegularExpressions.Regex(
                @"•(?<between>(?:[ \t]|<font[^>]*>|</font>)*)•",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Cache compiled "orphan-only" regexes per plain trait name (e.g. "<font color="#84ff84">• Hardy </font>").
        // These match the plain name ONLY when it's a standalone line entry (followed by newline or
        // end-of-string), not when it's the leading name of a vanilla trait line that has its own
        // " <font opacity..."  description tag immediately after. That distinction is critical:
        // unrestricted Contains/Replace on the plain name corrupts vanilla lines (strips the name
        // or inserts our dynamic before the vanilla description, leaving both descriptions stacked).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> OrphanTraitPatternCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex>();

        private static System.Text.RegularExpressions.Regex GetOrphanTraitPattern(string plainName)
        {
            return OrphanTraitPatternCache.GetOrAdd(plainName, key =>
                new System.Text.RegularExpressions.Regex(
                    @"\n?" + System.Text.RegularExpressions.Regex.Escape(key) + @"(?=\n|$)",
                    System.Text.RegularExpressions.RegexOptions.Compiled));
        }

        /// <summary>
        /// Returns true if plainName appears as a standalone entry in text
        /// (followed by a newline or end-of-string), not as a substring of a longer vanilla line.
        /// </summary>
        private static bool ContainsOrphanTraitName(string text, string plainName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return false;
            return GetOrphanTraitPattern(plainName).IsMatch(text);
        }

        /// <summary>
        /// Replaces standalone occurrences of plainName with the given replacement.
        /// Preserves the leading newline if matched. Vanilla lines are left untouched.
        /// </summary>
        private static string ReplaceOrphanTraitName(string text, string plainName, string replacement)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return text;
            return GetOrphanTraitPattern(plainName).Replace(text, m =>
                m.Value.StartsWith("\n", StringComparison.Ordinal) ? "\n" + replacement : replacement);
        }

        /// <summary>
        /// Removes standalone occurrences of plainName (and the preceding newline if present).
        /// Vanilla lines are left untouched.
        /// </summary>
        private static string RemoveOrphanTraitName(string text, string plainName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(plainName)) return text;
            return GetOrphanTraitPattern(plainName).Replace(text, "");
        }

        /// <summary>
        /// Does the game give this player the trait? Reads the class trait list and
        /// extraTraits through SeraphLevelingModSystem.PlayerHasGameTrait. The class
        /// fallbacks are kept for readability only; the class table already covers them.
        /// </summary>
        private static bool ClientHasVanillaTrait(EntityPlayer eplr, string traitCode, params string[] classFallbacks)
        {
            return SeraphLevelingModSystem.PlayerHasGameTrait(eplr, traitCode);
        }

        // Cache for locale-aware vanilla trait line regexes. Built lazily per trait code from
        // Lang.Get("trait-{code}"), so the regex works against whatever language the client is
        // running. Without this, hardcoded English wording (Claustrophobic, "ore drop rate", etc.)
        // would fail to match a French/German/etc. client and the vanilla line would render with
        // its full original penalty wording even after the mod has functionally cancelled it.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> VanillaTraitLineRegexCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex>();

        /// <summary>
        /// Builds (or returns a cached) regex matching a full vanilla trait line in any locale.
        /// Anchors on Lang.Get("trait-{code}") (which already includes the colored font + bullet)
        /// and consumes the trailing "(...)" opacity font block. Includes an optional leading
        /// newline so the same regex can be used for both replacement and removal.
        /// Returns null if the trait lang key is unavailable.
        /// </summary>
        private static System.Text.RegularExpressions.Regex GetVanillaTraitLineRegex(string traitCode)
        {
            return VanillaTraitLineRegexCache.GetOrAdd(traitCode, code =>
            {
                string traitText = Lang.Get("trait-" + code);
                if (string.IsNullOrEmpty(traitText) || traitText == "trait-" + code)
                {
                    return null;
                }
                string escaped = System.Text.RegularExpressions.Regex.Escape(traitText);
                // Pattern: <optional newline><localized trait label><whitespace><opacity font>(<any non-tag content>)</font>
                // We don't constrain the description content because it's a localized list of
                // charattribute strings joined with ", " — different in every language.
                string pattern = @"\n?" + escaped + @"\s*<font[^>]*opacity[^>]*>[^<]*</font>";
                return new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Compiled);
            });
        }

        /// <summary>
        /// Replaces the entire vanilla trait line with the given replacement text. Preserves the
        /// leading newline if matched (so adjacent trait entries stay separated). No-op if the
        /// line isn't found.
        /// </summary>
        private static string ReplaceVanillaTraitLine(string text, string traitCode, string replacement)
        {
            var regex = GetVanillaTraitLineRegex(traitCode);
            if (regex == null) return text;
            return regex.Replace(text, m => m.Value.StartsWith("\n", StringComparison.Ordinal) ? "\n" + replacement : replacement);
        }

        /// <summary>
        /// Removes the entire vanilla trait line, including the leading newline if matched.
        /// No-op if the line isn't found.
        /// </summary>
        private static string RemoveVanillaTraitLine(string text, string traitCode)
        {
            var regex = GetVanillaTraitLineRegex(traitCode);
            if (regex == null) return text;
            return regex.Replace(text, "");
        }

        /// <summary>
        /// Replaces a single inline vanilla charattribute string (e.g., "+10% mining speed") with
        /// a new value while keeping the same locale. Looks up the localized base string via
        /// Lang.Get("charattribute-{statKey}-{baseValue}"), then substitutes the first integer in
        /// that string with newPercent. Used when a class already has a vanilla positive trait
        /// (e.g., Blackguard's Hardy +10% mining speed) and we want to combine it with our
        /// progression bonus inline rather than replacing the whole line.
        /// </summary>
        private static string ReplaceVanillaCharAttribute(string text, string statKey, double baseValue, int newPercent)
        {
            string baseLangKey = "charattribute-" + statKey + "-" +
                baseValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string baseLocalized = Lang.Get(baseLangKey);
            if (string.IsNullOrEmpty(baseLocalized) || baseLocalized == baseLangKey || !text.Contains(baseLocalized))
            {
                return text;
            }
            string substituted = SubstituteFirstNumber(baseLocalized, newPercent);
            if (substituted == baseLocalized) return text;
            return text.Replace(baseLocalized, substituted);
        }

        /// <summary>
        /// Returns the localized vanilla charattribute string with its first integer replaced by
        /// the given value. Used for building partial penalty descriptions in the player's locale
        /// (e.g., for Heavyhanded's three-stat partial display). Returns the empty string if the
        /// lang key isn't found.
        /// </summary>
        private static string LocalizedCharAttributeWithValue(string statKey, double baseValue, int newPercent)
        {
            string baseLangKey = "charattribute-" + statKey + "-" +
                baseValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string baseLocalized = Lang.Get(baseLangKey);
            if (string.IsNullOrEmpty(baseLocalized) || baseLocalized == baseLangKey)
            {
                return "";
            }
            return SubstituteFirstNumber(baseLocalized, newPercent);
        }

        /// <summary>
        /// Builds a fully localized trait line `{traitName} <font opacity="0.6">({desc})</font>`
        /// using vanilla's `traitwithattributes` template, with the trait name pulled from the
        /// vanilla `trait-{code}` lang key (so it shows up in the player's locale: "Hardy" in EN,
        /// "Robuste" in FR, etc.) and the description from one of our `seraphleveling:` lang
        /// values. The seraphleveling lang values store only the inner description text — the
        /// trait label and font wrapper come from this helper.
        /// </summary>
        /// <summary>
        /// Add a trait that has no vanilla counterpart (the temporal traits). Replaces the
        /// "no traits" message or the bare name vanilla printed for the extraTraits entry,
        /// otherwise appends a new line.
        /// </summary>
        private static void AppendSeraphOnlyTrait(ref string result, ref bool hasNoTraits, string noTraitsMsg,
            string traitCode, string descLangKey, params object[] descArgs)
        {
            string plainName = Lang.Get("seraphleveling:trait-" + traitCode);
            string line = Lang.Get("traitwithattributes",
                Lang.Get("seraphleveling:traitname-" + traitCode), Lang.Get(descLangKey, descArgs));

            hasNoTraits = string.IsNullOrEmpty(result) ||
                          result.Trim() == noTraitsMsg.Trim() ||
                          result.Contains(noTraitsMsg) ||
                          result.Contains("No positive or negative traits");
            if (hasNoTraits) result = line;
            else if (ContainsOrphanTraitName(result, plainName)) result = ReplaceOrphanTraitName(result, plainName, line);
            else result = result + "\n" + line;
            hasNoTraits = false;
        }

        private static string BuildLocalizedTraitLine(string vanillaTraitCode, string seraphDescLangKey, params object[] descArgs)
        {
            string traitName = Lang.Get("trait-" + vanillaTraitCode);
            string desc = Lang.Get(seraphDescLangKey, descArgs);
            return Lang.Get("traitwithattributes", traitName, desc);
        }

        // Pattern for finding the first integer (or decimal) in a localized charattribute string.
        // Used to swap the percentage when combining vanilla + progression bonuses, since most
        // languages place the value as the first numeric token in the string.
        private static readonly System.Text.RegularExpressions.Regex FirstNumberRegex =
            new System.Text.RegularExpressions.Regex(@"\d+(?:\.\d+)?", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string SubstituteFirstNumber(string text, int newValue)
        {
            var match = FirstNumberRegex.Match(text);
            if (!match.Success) return text;
            return text.Substring(0, match.Index) + newValue.ToString() + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        /// Collapses adjacent bullet markers ("•") separated only by whitespace
        /// and/or font open/close tags into a single bullet. Same-line only; bullets
        /// separated by a newline (i.e. different trait entries) are left alone.
        /// Iterates so runs of 3+ bullets collapse fully; bounded by a safety counter.
        /// </summary>
        private static string CollapseDuplicateBullets(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string previous;
            int safety = 0;
            do
            {
                previous = text;
                text = DuplicateBulletPair.Replace(text, m => "•" + m.Groups["between"].Value);
                safety++;
            }
            while (text != previous && safety < 8);

            return text;
        }
    }
}
