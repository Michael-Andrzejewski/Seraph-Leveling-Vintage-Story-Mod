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

        // Cache for vanilla trait checks - populated once on player join
        private static ConcurrentDictionary<string, CachedVanillaTraits> VanillaTraitsCache = new ConcurrentDictionary<string, CachedVanillaTraits>();

        // =========================================================================
        // NEGATIVE TRAIT CONSTANTS - Used for cancellation calculations
        // =========================================================================

        // Farsighted (Hunter): -15% melee damage
        public const int VANILLA_FARSIGHTED_MELEE_PENALTY = 15;
        public const string WATCHED_FARSIGHTED_REMAINING = "sitFarsightedRemaining";

        // Nervous (Malefactor, Clockmaker): -15% melee damage
        public const int VANILLA_NERVOUS_MELEE_PENALTY = 15;
        public const string WATCHED_NERVOUS_REMAINING = "sitNervousRemaining";

        // Nearsighted (Blackguard): -15% ranged damage
        public const int VANILLA_NEARSIGHTED_RANGED_PENALTY = 15;
        public const string WATCHED_NEARSIGHTED_REMAINING = "sitNearsightedRemaining";

        // Frail (Malefactor, Clockmaker): -2.5 HP, -25% ranged distance
        public const float VANILLA_FRAIL_HP_PENALTY = 2.5f;
        public const int VANILLA_FRAIL_DISTANCE_PENALTY = 25;
        public const string WATCHED_FRAIL_HP_REMAINING = "sitFrailHpRemaining";
        public const string WATCHED_FRAIL_DISTANCE_REMAINING = "sitFrailDistanceRemaining";
        public const string FRAIL_HP_CANCEL_STAT_CODE = "sitFrailHpCancel";

        // Civil (Tailor): -10% loot from foraging
        public const int VANILLA_CIVIL_FORAGING_PENALTY = 10;
        public const string WATCHED_CIVIL_REMAINING = "sitCivilRemaining";

        // Weak (Tailor): -2 HP, -10% mining speed
        public const int VANILLA_WEAK_HP_PENALTY = 2;
        public const int VANILLA_WEAK_MINING_PENALTY = 10;
        public const string WATCHED_WEAK_HP_REMAINING = "sitWeakHpRemaining";
        public const string WATCHED_WEAK_MINING_REMAINING = "sitWeakMiningRemaining";
        public const string WEAK_HP_CANCEL_STAT_CODE = "sitWeakHpCancel";

        // Kind (Tailor): -10% animal loot, -25% harvesting speed
        public const int VANILLA_KIND_LOOT_PENALTY = 10;
        public const int VANILLA_KIND_SPEED_PENALTY = 25;
        public const string WATCHED_KIND_LOOT_REMAINING = "sitKindLootRemaining";
        public const string WATCHED_KIND_SPEED_REMAINING = "sitKindSpeedRemaining";

        // Heavyhanded (Blackguard): -10% vessel loot, -15% foraging, -20% wild crop
        public const int VANILLA_HEAVYHANDED_VESSEL_PENALTY = 10;
        public const int VANILLA_HEAVYHANDED_FORAGING_PENALTY = 15;
        public const int VANILLA_HEAVYHANDED_WILD_CROP_PENALTY = 20;
        public const string WATCHED_HEAVYHANDED_VESSEL_REMAINING = "sitHeavyhandedVesselRemaining";
        public const string WATCHED_HEAVYHANDED_FORAGING_REMAINING = "sitHeavyhandedForagingRemaining";
        public const string WATCHED_HEAVYHANDED_WILD_CROP_REMAINING = "sitHeavyhandedWildCropRemaining";

        // Claustrophobic (Hunter): -15% ore drop, -10% mining speed - already defined above
        public const int VANILLA_CLAUSTROPHOBIC_ORE_PENALTY = 15;
        public const int VANILLA_CLAUSTROPHOBIC_MINING_PENALTY = 10;
        public const string WATCHED_CLAUSTROPHOBIC_ORE_REMAINING = "sitClaustrophobicOreRemaining";
        public const string WATCHED_CLAUSTROPHOBIC_MINING_REMAINING = "sitClaustrophobicMiningRemaining";

        // =====================================================================
        // GAME TRAIT LOOKUP
        // The game never writes a "characterTraits" attribute. Class traits live in
        // CharacterSystem.characterClassesByCode[characterClass].Traits (which is
        // also where class mods register theirs) and traits other mods grant live
        // in the "extraTraits" attribute. Every "does this player have vanilla
        // trait X" question goes through here.
        // =====================================================================

        private static readonly Dictionary<string, string[]> VanillaClassTraitTable =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["commoner"] = Array.Empty<string>(),
            ["hunter"] = new[] { "focused", "resourceful", "fleetfooted", "bowyer", "farsighted", "claustrophobic" },
            ["malefactor"] = new[] { "forager", "pilferer", "furtive", "improviser", "frail", "nervous" },
            ["clockmaker"] = new[] { "precise", "technical", "fleetfooted", "frail", "nervous", "tinkerer" },
            ["blackguard"] = new[] { "soldier", "hardy", "merciless", "ravenous", "nearsighted", "heavyhanded" },
            ["tailor"] = new[] { "clothier", "mender", "civil", "weak", "kind" },
        };

        private static CharacterSystem serverCharacterSystem;
        private static CharacterSystem clientCharacterSystem;

        private static CharacterSystem CharacterSystemFor(ICoreAPI api)
        {
            if (api == null) return null;
            if (api.Side == EnumAppSide.Server)
                return serverCharacterSystem ??= api.ModLoader.GetModSystem<CharacterSystem>();
            return clientCharacterSystem ??= api.ModLoader.GetModSystem<CharacterSystem>();
        }

        /// <summary>Forget the cached CharacterSystem; a new world gets a new one.</summary>
        public static void ResetCharacterSystemCache()
        {
            serverCharacterSystem = null;
            clientCharacterSystem = null;
        }

        /// <summary>
        /// Every trait the game itself grants this player: the class's trait list
        /// (from CharacterSystem, so class mods count; the vanilla table is only a
        /// fallback for a class the game does not know), the "extraTraits" attribute,
        /// and a "characterTraits" attribute in case another mod writes one. Our own
        /// sit* codes sit in extraTraits too but never collide with a game code.
        /// </summary>
        public static List<string> GameTraitsOf(EntityPlayer entity)
        {
            var result = new List<string>();
            if (entity == null) return result;
            var wa = entity.WatchedAttributes;
            string cls = wa.GetString("characterClass", "") ?? "";
            var cs = CharacterSystemFor(entity.Api);
            if (cs != null && cls.Length > 0 && cs.characterClassesByCode.TryGetValue(cls, out var cc) && cc?.Traits != null)
            {
                result.AddRange(cc.Traits);
            }
            else if (VanillaClassTraitTable.TryGetValue(cls, out var fallback))
            {
                result.AddRange(fallback);
            }
            string[] extra = wa.GetStringArray("extraTraits", null);
            if (extra != null) result.AddRange(extra);
            string[] listed = wa.GetStringArray("characterTraits", null);
            if (listed != null) result.AddRange(listed);
            return result;
        }

        /// <summary>True when the game itself gives this player the trait.</summary>
        public static bool PlayerHasGameTrait(EntityPlayer entity, string traitCode)
        {
            if (entity == null || string.IsNullOrEmpty(traitCode)) return false;
            // Same sources as GameTraitsOf, read in place: this runs on hunger
            // ticks, hits and level checks, so it should not build a list each call.
            var wa = entity.WatchedAttributes;
            string cls = wa.GetString("characterClass", "") ?? "";
            var cs = CharacterSystemFor(entity.Api);
            if (cs != null && cls.Length > 0 && cs.characterClassesByCode.TryGetValue(cls, out var cc) && cc?.Traits != null)
            {
                if (ContainsTrait(cc.Traits, traitCode)) return true;
            }
            else if (VanillaClassTraitTable.TryGetValue(cls, out var fallback))
            {
                if (ContainsTrait(fallback, traitCode)) return true;
            }
            return ContainsTrait(wa.GetStringArray("extraTraits", null), traitCode)
                || ContainsTrait(wa.GetStringArray("characterTraits", null), traitCode);
        }

        private static bool ContainsTrait(string[] traits, string traitCode)
        {
            if (traits == null) return false;
            foreach (string trait in traits)
            {
                if (string.Equals(trait, traitCode, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// A negative trait as far as cancellation is concerned: false whenever
        /// CancelNegativeTraits is off, so no code path pays it off, grants extra
        /// credits for it, or writes a counter-stat. The character sheet asks
        /// PlayerHasGameTrait for the real answer.
        /// </summary>
        public static bool PlayerHasCancellableTrait(EntityPlayer entity, string traitCode)
        {
            return CancelNegativeTraits && PlayerHasGameTrait(entity, traitCode);
        }

        // =========================================================================
        // NEGATIVE TRAIT DETECTION METHODS
        // =========================================================================

        /// <summary>
        /// Checks if the player's class has the vanilla Farsighted trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaFarsighted(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "farsighted");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Nervous trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaNervous(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "nervous");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Nearsighted trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaNearsighted(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "nearsighted");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Frail trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaFrail(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "frail");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Civil trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaCivil(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "civil");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Weak trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaWeak(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "weak");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Kind trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaKind(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "kind");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Heavyhanded trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaHeavyhanded(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "heavyhanded");
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Claustrophobic trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaClaustrophobic(EntityPlayer entity)
        {
            return PlayerHasCancellableTrait(entity, "claustrophobic");
        }

        /// <summary>
        /// Calculate the remaining penalty for a negative trait after applying progression bonus.
        /// Returns the remaining penalty (0 or positive), or 0 if fully cancelled.
        /// </summary>
        public static int CalculateRemainingPenalty(int basePenalty, int progressionBonus)
        {
            return Math.Max(0, basePenalty - progressionBonus);
        }

        /// <summary>
        /// Populates the vanilla traits cache for a player.
        /// This reads the characterTraits array once and caches all trait booleans.
        /// </summary>
        private static void PopulateVanillaTraitsCache(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var entity = player.Entity;

            // Get character traits once
            string[] characterTraits = GameTraitsOf(entity).ToArray();
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "")?.ToLowerInvariant() ?? "";

            // Debug logging for trait detection
            ServerApi?.Logger?.Debug($"[SeraphLeveling] PopulateVanillaTraitsCache for {player.PlayerName}: class='{characterClass}', traits=[{string.Join(", ", characterTraits)}]");

            // Create a HashSet for O(1) lookups
            var traitSet = new HashSet<string>(characterTraits, StringComparer.OrdinalIgnoreCase);

            var cache = new CachedVanillaTraits
            {
                HasHardy = traitSet.Contains("hardy"),
                HasSoldier = traitSet.Contains("soldier"),
                HasFocused = traitSet.Contains("focused"),
                HasFleetfooted = traitSet.Contains("fleetfooted"),
                HasRavenous = traitSet.Contains("ravenous"),
                HasFarsighted = traitSet.Contains("farsighted"),
                HasNervous = traitSet.Contains("nervous"),
                HasNearsighted = traitSet.Contains("nearsighted"),
                HasFrail = traitSet.Contains("frail"),
                HasCivil = traitSet.Contains("civil"),
                HasWeak = traitSet.Contains("weak"),
                HasKind = traitSet.Contains("kind"),
                HasHeavyhanded = traitSet.Contains("heavyhanded"),
                HasClaustrophobic = traitSet.Contains("claustrophobic"),
                HasFurtive = traitSet.Contains("furtive"),
                HasPrecise = traitSet.Contains("precise"),
                HasMender = traitSet.Contains("mender"),
                HasPilferer = traitSet.Contains("pilferer"),
                HasResourceful = traitSet.Contains("resourceful"),
                HasForager = traitSet.Contains("forager"),

                // Combat Overhaul traits — gated on IsCombatOverhaulLoaded. CO traits don't
                // exist in the game world unless CO itself is loaded, so the class fallbacks
                // (e.g. "Blackguard always has Trembling Aim") should only fire when CO is
                // actually installed. Otherwise the cache reports phantom CO traits and downstream
                // Apply* functions write penalty values into watched attrs that the postfix then
                // renders as ghost debuffs (the "Trembling Aim -30%" symptom on Blackguard with
                // CO uninstalled).
                HasCOTremblingAim = IsCombatOverhaulLoaded && (traitSet.Contains("tremblingaim") || traitSet.Contains("trembling aim") || characterClass == "blackguard"),
                HasCOClumsyHands = IsCombatOverhaulLoaded && (traitSet.Contains("clumsyhands") || traitSet.Contains("clumsy hands")),
                HasCOFearOfMelee = IsCombatOverhaulLoaded && (traitSet.Contains("fearofmelee") || traitSet.Contains("fear of melee") || traitSet.Contains("frightenedofmelee") || traitSet.Contains("frightened of melee") || characterClass == "clockmaker"),
                HasCOWeakHand = IsCombatOverhaulLoaded && (traitSet.Contains("weakhand") || traitSet.Contains("weak hand")),
                HasCONervous = IsCombatOverhaulLoaded && (traitSet.Contains("nervous") || characterClass == "malefactor" || characterClass == "clockmaker"),

                // Combat Overhaul mixed/positive traits (Big Head, Thick Skull, Leg Day, Melee Expert, Self Defence)
                HasCOBigHead = IsCombatOverhaulLoaded && (traitSet.Contains("bighead") || traitSet.Contains("big head") || characterClass == "clockmaker"),
                HasCOThickSkull = IsCombatOverhaulLoaded && (traitSet.Contains("thickskull") || traitSet.Contains("thick skull") || characterClass == "malefactor"),
                HasCOLegDay = IsCombatOverhaulLoaded && (traitSet.Contains("legday") || traitSet.Contains("leg day") || characterClass == "blackguard"),
                HasCOMeleeExpert = IsCombatOverhaulLoaded && (traitSet.Contains("meleeexpert") || traitSet.Contains("melee expert") || traitSet.Contains("expert in melee") || characterClass == "blackguard"),
                HasCOSelfDefence = IsCombatOverhaulLoaded && (traitSet.Contains("selfdefence") || traitSet.Contains("self defence") || characterClass == "tailor")
            };

            // CancelNegativeTraits off: every Apply* function reads these flags to decide
            // how much penalty to pay off before bonus shows and whether to write a
            // counter-stat. Reporting "no negative traits" makes them skip both, so the
            // vanilla penalty stays and earned bonus applies from level 0.
            if (!CancelNegativeTraits)
            {
                cache.HasFarsighted = false;
                cache.HasNervous = false;
                cache.HasNearsighted = false;
                cache.HasFrail = false;
                cache.HasCivil = false;
                cache.HasWeak = false;
                cache.HasKind = false;
                cache.HasHeavyhanded = false;
                cache.HasClaustrophobic = false;
                cache.HasRavenous = false;
                cache.HasCOTremblingAim = false;
                cache.HasCOClumsyHands = false;
                cache.HasCOFearOfMelee = false;
                cache.HasCOWeakHand = false;
                cache.HasCONervous = false;
            }

            VanillaTraitsCache[playerUid] = cache;

            ServerApi?.Logger?.Debug($"[SeraphLeveling] Cached traits: HasClaustrophobic={cache.HasClaustrophobic}, HasFocused={cache.HasFocused}, HasFleetfooted={cache.HasFleetfooted}, HasFarsighted={cache.HasFarsighted}");
        }

        /// <summary>
        /// Gets the cached vanilla traits for a player. Returns null if not cached.
        /// </summary>
        private static CachedVanillaTraits GetCachedTraits(string playerUid)
        {
            VanillaTraitsCache.TryGetValue(playerUid, out var cache);
            return cache;
        }

        /// <summary>
        /// Check the vanilla characterTraits array for a trait code, with a
        /// class-name fallback. Same detection pattern as PopulateVanillaTraitsCache.
        /// </summary>
        private static bool PlayerHasVanillaTraitOrClassStatic(EntityPlayer entity, string traitCode, string classCode)
        {
            // classCode is kept for the callers; the class table already answers it.
            return PlayerHasGameTrait(entity, traitCode);
        }

        /// <summary>
        /// Check if player is the Hunter class.
        /// </summary>
        private static bool PlayerIsHunterStatic(EntityPlayer entity)
        {
            // The removal exists to cancel the Claustrophobic penalty, so gate on
            // that trait (Hunter carries it in vanilla; custom classes can too).
            return PlayerHasVanillaTraitOrClassStatic(entity, "claustrophobic", "hunter");
        }
    }
}
