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
    /// Server-side Harmony patches for entity death (death penalty).
    /// </summary>
    public static class EntityDeathPatches
    {
        /// <summary>
        /// Captures whether the entity was alive before Die ran, so the postfix
        /// only applies the penalty on the alive-to-dead transition. Die can be
        /// called more than once per death; without this the sqrt penalty
        /// would apply once per call.
        /// </summary>
        public static void Die_Prefix(Entity __instance, out bool __state)
        {
            __state = __instance?.Alive ?? false;
        }

        public static void Die_Postfix(Entity __instance, bool __state)
        {
            if (!__state) return;
            if (!SeraphLevelingModSystem.EnableDeathPenalty) return;
            var playerEntity = __instance as EntityPlayer;
            if (playerEntity == null) return;

            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Die_Postfix: Entity is EntityPlayer with UID={playerEntity.PlayerUID}");
            }

            // Use ServerApi.World to ensure we get the server-side player reference
            var player = SeraphLevelingModSystem.ServerApi?.World?.PlayerByUid(playerEntity.PlayerUID) as IServerPlayer;
            if (player == null)
            {
                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                        $"[SeraphLeveling] Die_Postfix: Could not get IServerPlayer for UID={playerEntity.PlayerUID}");
                }
                return;
            }

            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Die_Postfix: Applying death penalty to {player.PlayerName}");
            }

            SeraphLevelingModSystem.ApplyDeathPenalty(player);
        }
    }
}
