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

        // Server-side Harmony instance for melee damage tracking
        private Harmony serverHarmony;

        /// <summary>
        /// Apply Harmony patches for server-side melee damage tracking.
        /// </summary>
        private void ApplyServerHarmonyPatches(ICoreServerAPI api)
        {
            serverHarmony = new Harmony("seraphleveling.server");

            try
            {
                PatchDamageTracking(api);

                // Patch EntityBehaviorHarvestable.SetHarvested for Resourceful trait (animal harvesting)
                PatchAnimalHarvesting(api);

                // Patch CollectibleObject.OnHeldInteractStep for Mender trait (sewing kit repairs)
                PatchSewingKitRepairs(api);

                // Patch BlockEntityStaticTranslocator.DoRepair for Technical trait (translocator repairs)
                PatchTranslocatorRepairs(api);

                // Patch BEBehaviorBed.DidSleep for sleep buff system
                PatchBedSleeping(api);

                // Patch Entity.Die for death penalty system
                PatchEntityDeath(api);

                // Experimental (config-gated, off by default): temporal traits effect,
                // bow draw speed, knife gear-trick recharge, melee swing speed. The
                // patches are always installed but no-op while their switch is off.
                PatchTemporalStability(api);
                PatchBowDrawSpeed(api);
                PatchKnifeGearTrick(api);
                MeleeAttackSpeedPatches.Apply(serverHarmony, api, "server");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply server Harmony patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Entity.ReceiveDamage for melee and ranged damage tracking. Its own
        /// method so a failed lookup only skips this patch instead of every patch
        /// registered after it.
        /// </summary>
        private void PatchDamageTracking(ICoreServerAPI api)
        {
            try
            {
                // Find Entity.ReceiveDamage method
                var entityType = typeof(Entity);
                api.Logger.Debug($"[SeraphLeveling] Looking for Entity.ReceiveDamage method in {entityType.FullName}");

                var receiveDamageMethod = entityType.GetMethod("ReceiveDamage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (receiveDamageMethod == null)
                {
                    // Try to list available methods for debugging
                    var methods = entityType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var damageMethodNames = methods.Where(m => m.Name.Contains("Damage")).Select(m => m.Name).ToArray();
                    api.Logger.Warning($"[SeraphLeveling] Could not find Entity.ReceiveDamage method. Available damage methods: {string.Join(", ", damageMethodNames)}");
                    return;
                }

                api.Logger.Debug($"[SeraphLeveling] Found Entity.ReceiveDamage: {receiveDamageMethod}");

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(EntityDamagePatches),
                    nameof(EntityDamagePatches.ReceiveDamage_Postfix));

                if (postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find ReceiveDamage_Postfix method!");
                    return;
                }

                api.Logger.Debug($"[SeraphLeveling] Found postfix method: {postfixMethod}");

                serverHarmony.Patch(receiveDamageMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched Entity.ReceiveDamage for damage tracking");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to patch Entity.ReceiveDamage: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch BlockEntityBed.DidUnmount to apply sleep buff when players wake up.
        /// </summary>
        private void PatchBedSleeping(ICoreServerAPI api)
        {
            // Always patch; the postfixes check EnableSleepBuff at runtime, so a
            // config reload that enables the buff works without a restart.
            try
            {
                // BlockEntityBed is in Vintagestory.GameContent (already imported)
                var bedBehaviorType = typeof(BlockEntityBed);
                if (bedBehaviorType == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find BlockEntityBed type for sleep buff");
                    return;
                }

                var didMountMethod = AccessTools.Method(bedBehaviorType, "DidMount");
                var didUnmountMethod = AccessTools.Method(bedBehaviorType, "DidUnmount");
                if (didMountMethod == null || didUnmountMethod == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find BlockEntityBed.DidMount/DidUnmount methods for sleep buff");
                    return;
                }

                var mountPostfix = AccessTools.Method(typeof(BedSleepPatches), nameof(BedSleepPatches.DidMount_Postfix));
                var unmountPostfix = AccessTools.Method(typeof(BedSleepPatches), nameof(BedSleepPatches.DidUnmount_Postfix));
                if (mountPostfix == null || unmountPostfix == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find DidMount_Postfix/DidUnmount_Postfix method!");
                    return;
                }

                serverHarmony.Patch(didMountMethod, postfix: new HarmonyMethod(mountPostfix));
                serverHarmony.Patch(didUnmountMethod, postfix: new HarmonyMethod(unmountPostfix));
                api.Logger.Notification("[SeraphLeveling] Successfully patched BlockEntityBed.DidMount/DidUnmount for sleep buff");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch BlockEntityBed sleep methods: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Entity.Die to apply death penalty when players die.
        /// </summary>
        private void PatchEntityDeath(ICoreServerAPI api)
        {
            // Always patch; Die_Postfix checks EnableDeathPenalty at runtime.
            // A load-time gate here would leave the patch missing when the legacy
            // world-blob config migration enables the penalty after startup.
            try
            {
                var entityType = typeof(Entity);
                var dieMethod = entityType.GetMethod("Die",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (dieMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find Entity.Die method for death penalty");
                    return;
                }

                var prefixMethod = AccessTools.Method(typeof(EntityDeathPatches),
                    nameof(EntityDeathPatches.Die_Prefix));
                var postfixMethod = AccessTools.Method(typeof(EntityDeathPatches),
                    nameof(EntityDeathPatches.Die_Postfix));
                if (prefixMethod == null || postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find Die_Prefix/Die_Postfix method!");
                    return;
                }

                serverHarmony.Patch(dieMethod, prefix: new HarmonyMethod(prefixMethod), postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched Entity.Die for death penalty");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch Entity.Die: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch EntityBehaviorHarvestable.SetHarvested to track animal harvesting for Resourceful trait.
        /// </summary>
        private void PatchAnimalHarvesting(ICoreServerAPI api)
        {
            try
            {
                // Find the EntityBehaviorHarvestable type in VSSurvivalMod
                var harvestableType = AccessTools.TypeByName("Vintagestory.GameContent.EntityBehaviorHarvestable");
                if (harvestableType == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find EntityBehaviorHarvestable type");
                    return;
                }

                // Find the SetHarvested method
                var setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvested");
                if (setHarvestedMethod == null)
                {
                    // Try alternative method name
                    setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvestedBy");
                }
                if (setHarvestedMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find SetHarvested or SetHarvestedBy method in EntityBehaviorHarvestable");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(HarvestingPatches),
                    nameof(HarvestingPatches.SetHarvested_Postfix));

                serverHarmony.Patch(setHarvestedMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched EntityBehaviorHarvestable.SetHarvested for Resourceful trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch EntityBehaviorHarvestable: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch methods to track sewing kit repairs for Mender trait.
        /// Vanilla has exactly two repair paths:
        /// 1. Merging a sewing kit onto damaged clothing raises its "condition"
        ///    attribute and consumes one kit (CollectibleBehaviorWearable.TryMergeStacks).
        /// 2. Grid "repair" recipes restore durability on the crafted output.
        /// Path 1 gets a precise prefix/postfix pair. Path 2 is detected by the
        /// durability-increase watcher on OnModifiedInInventorySlot.
        /// </summary>
        private void PatchSewingKitRepairs(ICoreServerAPI api)
        {
            bool anyPatchSucceeded = false;

            // Approach 1: The sewing-kit-onto-clothing merge. This is the primary
            // repair mechanic; one credit per kit actually consumed by a repair.
            // Patch the vanilla behavior AND every loaded subclass that declares
            // its own override (Overhaullib-style mods replace wearable behaviors
            // with subclasses; a patch on only the base method never fires for
            // an overriding subclass).
            try
            {
                var wearableBehaviorType = AccessTools.TypeByName("Vintagestory.GameContent.CollectibleBehaviorWearable");
                if (wearableBehaviorType != null)
                {
                    var prefixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.TryMergeStacks_Prefix));
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.TryMergeStacks_Postfix));

                    var paramTypes = new[] { typeof(ItemStackMergeOperation), typeof(EnumHandling).MakeByRefType() };
                    int patched = 0;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type[] types;
                        try { types = asm.GetTypes(); }
                        catch { continue; }
                        foreach (var type in types)
                        {
                            if (!wearableBehaviorType.IsAssignableFrom(type)) continue;
                            var m = type.GetMethod("TryMergeStacks",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                                null, paramTypes, null);
                            if (m == null || m.DeclaringType != type) continue;
                            serverHarmony.Patch(m, prefix: new HarmonyMethod(prefixMethod), postfix: new HarmonyMethod(postfixMethod));
                            api.Logger.Notification($"[SeraphLeveling] Patched {type.FullName}.TryMergeStacks for Mender trait");
                            patched++;
                        }
                    }
                    anyPatchSucceeded = patched > 0;
                    if (patched == 0)
                    {
                        api.Logger.Warning("[SeraphLeveling] Found no TryMergeStacks declarations to patch for Mender trait");
                    }
                }
                else
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find CollectibleBehaviorWearable for Mender trait");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] TryMergeStacks patch attempt: {ex.Message}");
            }

            // Approach 2: Patch CollectibleObject.OnModifiedInInventorySlot to detect durability restoration
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onModifiedMethod = AccessTools.Method(collectibleType, "OnModifiedInInventorySlot");
                if (onModifiedMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.OnModifiedInInventorySlot_Postfix));
                    serverHarmony.Patch(onModifiedMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnModifiedInInventorySlot for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] OnModifiedInInventorySlot patch attempt: {ex.Message}");
            }

            if (!anyPatchSucceeded)
            {
                api.Logger.Warning("[SeraphLeveling] Could not patch any method for Mender trait (sewing kit repairs)");
            }
        }

        /// <summary>
        /// Patch BlockEntityStaticTranslocator.DoRepair to track translocator repairs for Technical trait.
        /// </summary>
        private void PatchTranslocatorRepairs(ICoreServerAPI api)
        {
            try
            {
                // Find the BlockEntityStaticTranslocator type
                var translocatorType = AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityStaticTranslocator");
                if (translocatorType == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find BlockEntityStaticTranslocator type");
                    return;
                }

                // Find the DoRepair method
                var doRepairMethod = AccessTools.Method(translocatorType, "DoRepair");
                if (doRepairMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find DoRepair method in BlockEntityStaticTranslocator");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(TranslocatorPatches),
                    nameof(TranslocatorPatches.DoRepair_Postfix));

                serverHarmony.Patch(doRepairMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched BlockEntityStaticTranslocator.DoRepair for Technical trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch BlockEntityStaticTranslocator.DoRepair: {ex.Message}");
            }
        }
    }
}
