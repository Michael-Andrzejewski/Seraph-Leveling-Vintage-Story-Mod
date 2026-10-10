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
    // Performance caches. Each one holds an answer that is expensive to work out
    // and does not change until the event named here, so the hot paths (timers,
    // block breaks, hits, inventory changes) look it up instead.
    //
    //   onlinePlayers       online IServerPlayer[]         rebuilt on join, now-playing, leave
    //   playerInventories   per player: character, hotbar,  dropped on join and leave (unsubscribes
    //                       backpack inventories, and the   the SlotModified listener)
    //                       gear-changed flags
    //   GearInfoCache       per item type: code, armour     rebuilt when the Clothier blacklist
    //                       type, armour/clothing flags,     array is replaced (config load)
    //                       weapon types, armour-hit values
    //   blockKinds          per block id: points class,     never (block ids are fixed per world)
    //                       crop/vessel/ground flags
    //   mechanicalByType    per creature type: Precise      never (fixed per world)
    //   rangedComboCache    per (projectile, held item)     never (fixed per world)
    //
    // ResetPerformanceCaches empties all of them when the mod system is disposed:
    // single player keeps these statics across worlds, and the next world has
    // different item, block and creature objects.
    public partial class SeraphLevelingModSystem
    {

        // Online players for the timers. AllOnlinePlayers runs a LINQ query and
        // allocates a new array on every call, and the timers ask several times a
        // second; this list changes only on join and leave.
        private static IServerPlayer[] onlinePlayers = new IServerPlayer[0];
        internal static IServerPlayer[] OnlinePlayers => onlinePlayers;

        // A player's own inventories, looked up once. GetOwnInventory builds a
        // "class-uid" name string per call (twice), and Entity.RightHandItemSlot
        // does PlayerByUid plus the same string build; the timers and the hit
        // paths ask for these many times a second.
        internal sealed class PlayerInventories
        {
            public IServerPlayer Player;
            public IInventory Character, Hotbar, Backpack;

            // Worn gear changed since the clothing / mender scan last looked (set by
            // the character inventory's SlotModified event). Vanilla marks the slot
            // dirty for every change that matters (equip, unequip, durability loss,
            // repair), so the scans run on change, plus a slow safety rescan.
            public volatile bool ClothingDirty = true, MenderDirty = true;
            public long ClothingScanMs, MenderScanMs;
            public Action<int> OnSlotModified;
        }

        // A full gear rescan at least this often even with no slot event, for a
        // change some mod makes without marking the slot dirty.
        private const long GearRescanMs = 10000;

        private static void DropInventories(string uid)
        {
            if (uid != null && playerInventories.TryRemove(uid, out var old) && old.OnSlotModified != null && old.Character is InventoryBase ib)
                ib.SlotModified -= old.OnSlotModified;
        }

        /// <summary>
        /// Whether a gear scan should run now for this player. Clears the dirty
        /// flag it is given. True when the gear changed or the rescan is due.
        /// </summary>
        private static bool GearScanDue(PlayerInventories inv, bool clothing)
        {
            if (inv == null) return true;
            long now = ServerApi?.World?.ElapsedMilliseconds ?? 0;
            if (clothing)
            {
                if (!inv.ClothingDirty && now - inv.ClothingScanMs < GearRescanMs) return false;
                inv.ClothingDirty = false; inv.ClothingScanMs = now;
            }
            else
            {
                if (!inv.MenderDirty && now - inv.MenderScanMs < GearRescanMs) return false;
                inv.MenderDirty = false; inv.MenderScanMs = now;
            }
            return true;
        }
        private static readonly ConcurrentDictionary<string, PlayerInventories> playerInventories = new ConcurrentDictionary<string, PlayerInventories>();

        internal static PlayerInventories InventoriesOf(IServerPlayer player)
        {
            string uid = player?.PlayerUID;
            if (uid == null || player.InventoryManager == null) return null;
            if (playerInventories.TryGetValue(uid, out var inv) && ReferenceEquals(inv.Player, player)) return inv;
            inv = new PlayerInventories
            {
                Player = player,
                Character = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
                Hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName),
                Backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName),
            };
            if (inv.Character is InventoryBase ib)
            {
                var self = inv;
                inv.OnSlotModified = _ => { self.ClothingDirty = true; self.MenderDirty = true; };
                ib.SlotModified += inv.OnSlotModified;
            }
            DropInventories(uid);
            playerInventories[uid] = inv;
            return inv;
        }

        internal static IInventory CharacterInventoryOf(IServerPlayer player) =>
            InventoriesOf(player)?.Character ?? player?.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);

        /// <summary>
        /// The player's active hotbar slot, exactly as PlayerInventoryManager.ActiveHotbarSlot
        /// works it out (slot 10 of the hotbar is the offhand; past it the number runs into
        /// the backpack), over the cached inventories.
        /// </summary>
        internal static ItemSlot HeldSlotOf(IServerPlayer player)
        {
            var inv = InventoriesOf(player);
            if (inv == null) return player?.Entity?.RightHandItemSlot;
            var hotbar = inv.Hotbar;
            int offhand = (hotbar != null && hotbar.Count > 10 && !hotbar[10].Empty) ? 1 : 0;
            int n = player.InventoryManager.ActiveHotbarSlotNumber;
            if (n >= 10 + offhand) return inv.Backpack?[n - 10 - offhand];
            return hotbar != null && n >= 0 && n < hotbar.Count ? hotbar[n] : null;
        }

        internal static void RefreshOnlinePlayers(IServerPlayer leaving = null)
        {
            if (leaving?.PlayerUID != null) DropInventories(leaving.PlayerUID);
            var all = ServerApi?.World?.AllOnlinePlayers;
            if (all == null) { onlinePlayers = new IServerPlayer[0]; return; }
            var list = new List<IServerPlayer>(all.Length);
            foreach (var p in all)
                if (p is IServerPlayer sp && !ReferenceEquals(sp, leaving)) list.Add(sp);
            onlinePlayers = list.ToArray();
        }

        /// <summary>What the worn-gear ticks need to know about one item type.</summary>
        internal sealed class GearInfo
        {
            public string Code;        // Collectible.Code.ToString(), e.g. "game:armor-body-plate-iron"
            public string ArmorType;   // GetArmorType(Code), null if not armor
            public bool IsArmor;       // IsArmorItem(Code)
            public bool IsClothing;    // IsClothingItem(Code), honours the Clothier blacklist

            // Filled on first use by the hit paths (pure functions of the code).
            public bool WeaponKnown; public string WeaponType;                       // GetWeaponTypeFromCode
            public bool COKnown; public (string proficiencyStat, string weaponCode) CO; // GetCOWeaponType
            public bool ArmorHitKnown; public float ArmorProtection, ArmorHitProbability; // TrackArmorDamageBlocked
        }

        internal static string WeaponTypeOf(CollectibleObject collectible)
        {
            var g = GearInfoOf(collectible);
            if (g == null) return null;
            if (!g.WeaponKnown) { g.WeaponType = GetWeaponTypeFromCode(g.Code); g.WeaponKnown = true; }
            return g.WeaponType;
        }

        internal static (string proficiencyStat, string weaponCode) COWeaponTypeOf(CollectibleObject collectible)
        {
            var g = GearInfoOf(collectible);
            if (g == null) return (null, null);
            if (!g.COKnown) { g.CO = GetCOWeaponType(g.Code); g.COKnown = true; }
            return g.CO;
        }

        // ---- per block type: what OnBlockBroken needs, worked out once per block id ----
        [Flags]
        private enum BlockKind : ushort
        {
            Known = 1, Ore = 2, Rock = 4, Crop = 8, CropMarkedWild = 16, Mushroom = 32,
            LootVessel = 64, Farmland = 128, NaturalGround = 256,
        }
        private static BlockKind[] blockKinds = new BlockKind[0];

        private static BlockKind KindOfBlock(int blockId)
        {
            if (blockId < 0 || ServerApi == null) return 0;
            var kinds = blockKinds;
            if (blockId < kinds.Length && kinds[blockId] != 0) return kinds[blockId];
            var block = ServerApi.World.GetBlock(blockId);
            BlockKind k = BlockKind.Known;
            if (block?.Code != null)
            {
                // Exactly the old per-call checks: points on the raw code, the
                // forage/vessel/ground checks on the lower-cased code.
                string raw = block.Code.ToString();
                string codeToCheck = raw.StartsWith("game:", StringComparison.Ordinal) ? raw.Substring(5) : raw;
                if (codeToCheck.Contains("ore-") || codeToCheck.StartsWith("meteorite", StringComparison.Ordinal) || codeToCheck.Contains("meteoriciron")) k |= BlockKind.Ore;
                else if (codeToCheck.StartsWith("rock-", StringComparison.Ordinal) || codeToCheck.StartsWith("crackedrock-", StringComparison.Ordinal)) k |= BlockKind.Rock;
                string lower = raw.ToLowerInvariant();
                if (lower.Contains("crop-")) k |= BlockKind.Crop;
                if (lower.Contains("wild")) k |= BlockKind.CropMarkedWild;
                if (lower.Contains("mushroom-")) k |= BlockKind.Mushroom;
                if (lower.Contains("lootvessel")) k |= BlockKind.LootVessel;
                if (lower.Contains("farmland")) k |= BlockKind.Farmland;
                if (lower.Contains("soil") || lower.Contains("dirt") || lower.Contains("grass") || lower.Contains("forest") ||
                    lower.Contains("peat") || lower.Contains("sand") || lower.Contains("gravel") || lower.Contains("clay")) k |= BlockKind.NaturalGround;
            }
            if (blockId >= kinds.Length)
            {
                var grown = new BlockKind[Math.Max(blockId + 1, ServerApi.World.Blocks.Count)];
                Array.Copy(kinds, grown, kinds.Length);
                blockKinds = kinds = grown;
            }
            kinds[blockId] = k;
            return k;
        }

        // ---- per creature type: Precise's mechanical check ----
        private static readonly ConcurrentDictionary<EntityProperties, bool> mechanicalByType = new ConcurrentDictionary<EntityProperties, bool>();

        // ---- per (projectile type, held item type): the ranged weapon combo ----
        private static readonly ConcurrentDictionary<(AssetLocation, CollectibleObject), string> rangedComboCache = new ConcurrentDictionary<(AssetLocation, CollectibleObject), string>();

        internal static string RangedWeaponComboOf(Entity projectile, EntityPlayer shooter)
        {
            if (projectile?.Code == null || shooter == null) return GetRangedWeaponCombo(projectile, shooter);
            var held = (shooter.Player is IServerPlayer sp ? HeldSlotOf(sp) : shooter.RightHandItemSlot)?.Itemstack?.Collectible;
            var key = (projectile.Code, held);
            if (rangedComboCache.TryGetValue(key, out string combo)) return combo;
            combo = GetRangedWeaponCombo(projectile, shooter);
            rangedComboCache[key] = combo;
            return combo;
        }

        /// <summary>Empty every cache in this file (see the table at the top). Called by Dispose.</summary>
        private static void ResetPerformanceCaches()
        {
            GearInfoCache.Clear();
            blockKinds = new BlockKind[0];
            mechanicalByType.Clear();
            rangedComboCache.Clear();
            onlinePlayers = new IServerPlayer[0];
            foreach (var uid in playerInventories.Keys.ToList()) DropInventories(uid);
        }

        // The armour, clothing and mender ticks look at every worn slot of every
        // player several times a second. Classifying an item means a ToString,
        // lower-casing and dozens of substring checks, and the answer never
        // changes for a given item type, so it is worked out once per type.
        // Keyed by the CollectibleObject itself (one instance per item type per
        // world); rebuilt if the Clothier blacklist is replaced by a config load.
        private static readonly ConcurrentDictionary<CollectibleObject, GearInfo> GearInfoCache = new ConcurrentDictionary<CollectibleObject, GearInfo>();
        private static string[] gearInfoCacheBlacklist;

        internal static GearInfo GearInfoOf(CollectibleObject collectible)
        {
            if (collectible?.Code == null) return null;
            var blacklist = ClothierBlacklistedItems;
            if (!ReferenceEquals(blacklist, gearInfoCacheBlacklist))
            {
                GearInfoCache.Clear();
                gearInfoCacheBlacklist = blacklist;
            }
            if (GearInfoCache.TryGetValue(collectible, out var info)) return info;
            string code = collectible.Code.ToString();
            info = new GearInfo
            {
                Code = code,
                ArmorType = GetArmorType(code),
                IsArmor = IsArmorItem(code),
                IsClothing = IsClothingItem(code),
            };
            GearInfoCache[collectible] = info;
            return info;
        }
    }
}
