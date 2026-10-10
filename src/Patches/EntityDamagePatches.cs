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
    /// Server-side Harmony patches for entity damage tracking.
    /// </summary>
    public static class EntityDamagePatches
    {
        /// <summary>
        /// Postfix for Entity.ReceiveDamage - tracks melee and ranged damage dealt by players,
        /// and damage blocked by armor when players receive damage.
        /// </summary>
        public static void ReceiveDamage_Postfix(Entity __instance, DamageSource damageSource, float damage, bool __result)
        {
            // Debug: Log all damage events to diagnose CO issues (disabled by default to avoid log spam)
            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug($"[SeraphLeveling] ReceiveDamage_Postfix: target={__instance?.Code}, damage={damage}, result={__result}, SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}");
            }

            // Only process if damage was actually dealt
            if (!__result || damage <= 0) return;

            // Track armor damage blocked if the entity taking damage is a player wearing armor
            TrackArmorDamageBlocked(__instance, damageSource, damage);

            // Check if this is ranged damage (projectile with CauseEntity)
            if (SeraphLevelingModSystem.IsRangedDamage(damageSource))
            {
                // For ranged: CauseEntity is the shooter, SourceEntity is the projectile
                var shooterEntity = damageSource.CauseEntity as EntityPlayer;
                if (shooterEntity == null) return;

                var shooterPlayer = shooterEntity.Player as IServerPlayer;
                if (shooterPlayer == null) return;

                // Don't count self-damage
                if (__instance == shooterEntity) return;

                // Get the weapon combination (bow+arrow, sling+stone, etc.)
                string weaponCombo = SeraphLevelingModSystem.RangedWeaponComboOf(damageSource.SourceEntity, shooterEntity);

                if (weaponCombo != null)
                {
                    SeraphLevelingModSystem.ProcessRangedDamage(shooterPlayer, weaponCombo, damage);

                    // Also track Precise damage if target is a mechanical creature
                    if (SeraphLevelingModSystem.IsMechanicalCreature(__instance))
                    {
                        SeraphLevelingModSystem.ProcessPreciseDamage(shooterPlayer, weaponCombo, damage);
                    }
                }

                // Combat Overhaul: Also track CO ranged proficiency if enabled
                if (SeraphLevelingModSystem.IsCOCompatEnabled)
                {
                    // First, check the projectile itself for thrown weapons (javelins, thrown spears)
                    // These weapons ARE the projectile, so we detect from SourceEntity
                    string projectileCode = damageSource.SourceEntity?.Code?.ToString();
                    if (!string.IsNullOrEmpty(projectileCode))
                    {
                        var (projProficiency, projWeaponCode) = SeraphLevelingModSystem.GetCOWeaponType(projectileCode);
                        if (SeraphLevelingModSystem.DebugLoggingEnabled)
                        {
                            SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] CO ranged projectile check: '{projectileCode}' -> proficiency='{projProficiency ?? "null"}'");
                        }
                        if (projProficiency != null)
                        {
                            // Javelins proficiency is NOT in IsCORangedProficiency, so process it here
                            SeraphLevelingModSystem.ProcessCOProficiencyDamage(shooterPlayer, projProficiency, projWeaponCode, damage);
                        }
                    }

                    // Also check held ranged weapon for bows/crossbows/slings/firearms
                    var heldRangedItem = SeraphLevelingModSystem.HeldSlotOf(shooterPlayer)?.Itemstack?.Collectible;
                    if (heldRangedItem != null)
                    {
                        var (proficiencyStat, coWeaponCode) = SeraphLevelingModSystem.COWeaponTypeOf(heldRangedItem);
                        if (proficiencyStat != null && SeraphLevelingModSystem.IsCORangedProficiency(proficiencyStat))
                        {
                            SeraphLevelingModSystem.ProcessCOProficiencyDamage(shooterPlayer, proficiencyStat, coWeaponCode, damage);
                        }
                    }
                }

                return; // Don't also count as melee
            }

            // Check if damage was dealt by a player (melee)
            if (damageSource?.SourceEntity == null) return;

            var attackerPlayer = (damageSource.SourceEntity as EntityPlayer)?.Player as IServerPlayer;
            if (attackerPlayer == null) return;

            // Don't count self-damage
            if (__instance == damageSource.SourceEntity) return;

            // Get held weapon
            var heldItem = SeraphLevelingModSystem.HeldSlotOf(attackerPlayer)?.Itemstack?.Collectible;
            if (heldItem == null) return;

            string itemCode = SeraphLevelingModSystem.GearInfoOf(heldItem)?.Code;
            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] Melee hit with held item: '{itemCode}'");
            }

            string weaponType = SeraphLevelingModSystem.WeaponTypeOf(heldItem);

            if (weaponType != null)
            {
                SeraphLevelingModSystem.ProcessMeleeDamage(attackerPlayer, weaponType, damage);

                // Also track Precise damage if target is a mechanical creature
                if (SeraphLevelingModSystem.IsMechanicalCreature(__instance))
                {
                    SeraphLevelingModSystem.ProcessPreciseDamage(attackerPlayer, weaponType, damage);
                }
            }

            // Combat Overhaul: Also track CO melee proficiency if enabled
            if (SeraphLevelingModSystem.IsCOCompatEnabled)
            {
                var (proficiencyStat, coWeaponCode) = SeraphLevelingModSystem.COWeaponTypeOf(heldItem);
                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi?.Logger?.Debug($"[SeraphLeveling] CO weapon check: itemCode='{itemCode}' -> proficiency='{proficiencyStat ?? "null"}', weaponCode='{coWeaponCode ?? "null"}'");
                }
                if (proficiencyStat != null && !SeraphLevelingModSystem.IsCORangedProficiency(proficiencyStat))
                {
                    SeraphLevelingModSystem.ProcessCOProficiencyDamage(attackerPlayer, proficiencyStat, coWeaponCode, damage);
                }
            }
        }

        /// <summary>
        /// Track damage blocked by armor when a player takes damage.
        /// Uses hit probability (50% body, 30% legs, 20% head) to distribute damage to armor pieces.
        /// </summary>
        private static void TrackArmorDamageBlocked(Entity damagedEntity, DamageSource damageSource, float finalDamage)
        {
            // Only track actual combat damage - filter out healing and non-combat damage types
            if (damageSource == null) return;

            // Filter out non-combat damage types (healing, hunger, suffocation, etc.)
            // Only count damage that armor can actually block: melee attacks and projectiles
            var damageType = damageSource.Type;
            if (damageType == EnumDamageType.Heal ||
                damageType == EnumDamageType.Hunger ||
                damageType == EnumDamageType.Suffocation ||
                damageType == EnumDamageType.Poison ||
                damageType == EnumDamageType.Gravity ||
                damageType == EnumDamageType.Fire ||
                damageType == EnumDamageType.Frost ||
                damageType == EnumDamageType.Heat ||
                damageType == EnumDamageType.Electricity)
            {
                return; // These damage types are not blocked by armor
            }

            // Only process for players
            var playerEntity = damagedEntity as EntityPlayer;
            if (playerEntity == null) return;

            var player = playerEntity.Player as IServerPlayer;
            if (player == null) return;

            // Get the player's armor using character inventory
            var characterInventory = SeraphLevelingModSystem.CharacterInventoryOf(player);
            if (characterInventory == null) return;

            // Find armor pieces and calculate damage blocked per piece
            // Use hit probability: 50% body, 30% legs, 20% head
            foreach (var slot in characterInventory)
            {
                if (slot?.Itemstack?.Collectible == null) continue;

                var gear = SeraphLevelingModSystem.GearInfoOf(slot.Itemstack.Collectible);
                string itemCode = gear?.Code;
                string armorType = gear?.ArmorType;

                if (armorType == null) continue; // Not armor

                // Protection and hit probability depend only on the item type: worked
                // out on its first hit (reading the JSON attributes allocates), then cached.
                if (!gear.ArmorHitKnown)
                {
                    // Vintage Story uses protectionModifiers.relativeProtection (0-1 scale, e.g., 0.2 = 20% reduction)
                    float rel = 0f;
                    var itemAttributes = slot.Itemstack.Collectible.Attributes;
                    if (itemAttributes != null)
                    {
                        var protectionModifiers = itemAttributes["protectionModifiers"];
                        if (protectionModifiers != null && protectionModifiers.Exists)
                        {
                            rel = protectionModifiers["relativeProtection"].AsFloat(0f);
                        }
                    }
                    // No protection found: a small minimum so armour that blocks anything still grants some XP
                    if (rel <= 0) rel = 0.05f;
                    gear.ArmorProtection = rel;

                    // Hit probability by slot type (from the item code): head 20%, legs 30%, body 50%
                    if (itemCode.Contains("-head-") || itemCode.Contains("-helmet-")) gear.ArmorHitProbability = 0.2f;
                    else if (itemCode.Contains("-legs-") || itemCode.Contains("-leggings-")) gear.ArmorHitProbability = 0.3f;
                    else gear.ArmorHitProbability = 0.5f;
                    gear.ArmorHitKnown = true;
                }
                float relativeProtection = gear.ArmorProtection;
                float hitProbability = gear.ArmorHitProbability;

                // Calculate damage blocked by this armor piece
                // For a hit that lands on this piece: originalDamage = finalDamage / (1 - protection)
                // damageBlocked = originalDamage - finalDamage = finalDamage * protection / (1 - protection)
                // We scale by hit probability since not all hits go to this piece
                // relativeProtection is already on 0-1 scale (e.g., 0.2 = 20% reduction)
                float protection = relativeProtection;
                if (protection >= 1f) protection = 0.99f; // Prevent division by zero

                float damageBlocked = finalDamage * protection / (1f - protection) * hitProbability;

                if (damageBlocked > 0)
                {
                    SeraphLevelingModSystem.ProcessArmorDamageBlocked(player, damageBlocked, itemCode);
                }
            }
        }
    }
}
