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
    /// Server-side Harmony patches for animal harvesting (Resourceful trait).
    /// </summary>
    public static class HarvestingPatches
    {
        /// <summary>
        /// Postfix for EntityBehaviorHarvestable.SetHarvested - tracks when player harvests an animal.
        /// </summary>
        public static void SetHarvested_Postfix(object __instance, IPlayer byPlayer)
        {
            try
            {
                // Only process on server
                if (byPlayer == null) return;

                var serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer == null) return;

                // Call the Resourceful progression handler
                SeraphLevelingModSystem.ProcessAnimalHarvested(serverPlayer);
            }
            catch (Exception ex)
            {
                // Silently ignore errors to avoid breaking the game
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in SetHarvested_Postfix: {ex.Message}");
            }
        }
    }
}
