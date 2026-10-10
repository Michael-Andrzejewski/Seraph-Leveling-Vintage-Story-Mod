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
        // COMBAT OVERHAUL COMPATIBILITY
        // =========================================================================

        /// <summary>Whether Combat Overhaul mod is loaded.</summary>
        public static bool IsCombatOverhaulLoaded { get; internal set; } = false;

        /// <summary>Whether specifically the 1.22 Combat Overhaul FORK is loaded.
        /// The fork adds a separate poleaxeProficiency stat the original mod lacks,
        /// so poleaxe weapons route to that stat only when this is true (original
        /// CO keeps lumping them with halberds).</summary>
        public static bool IsCombatOverhaulForkLoaded { get; internal set; } = false;

        /// <summary>Whether CO compatibility is enabled (mod loaded AND config enabled).</summary>
        public static bool IsCOCompatEnabled => IsCombatOverhaulLoaded && COEnableCompat;

        /// <summary>
        /// Mod IDs recognized as "Combat Overhaul". The original mod is
        /// "combatoverhaul"; the 1.22 community continuation is the fork
        /// "combatoverhaulfork". The fork keeps CO's stat names and trait
        /// codes (bowsProficiency, playerHeadDamageFactor, meleeDamageTierBonus*,
        /// etc.) and ships its weapons in the game domain with the same code
        /// suffixes (blade-, bow-, spear-, ...), and our weapon matching strips
        /// the domain prefix, so all proficiency tracking and stat application
        /// works for either one once it is detected here.
        /// </summary>
        public static readonly string[] CombatOverhaulModIds = { "combatoverhaul", "combatoverhaulfork" };

        /// <summary>Returns true if Combat Overhaul or its 1.22 fork is enabled.</summary>
        public static bool DetectAnyCombatOverhaul(IModLoader modLoader)
        {
            if (modLoader == null) return false;
            foreach (string id in CombatOverhaulModIds)
            {
                if (modLoader.IsModEnabled(id)) return true;
            }
            return false;
        }

        // CO configuration values (loaded from config)
        public static bool COEnableCompat = true;
        public static int COBaseDamagePerIncrement = 100;
        public static int COIncrementStep = 100;
        public static float COBowsProficiencyMax = 0.5f;
        public static float COCrossbowsProficiencyMax = 0.5f;
        public static float COFirearmsProficiencyMax = 0.5f;
        public static float COSlingsProficiencyMax = 0.3f;
        public static float COOneHandedSwordsProficiencyMax = 0.3f;
        public static float COTwoHandedSwordsProficiencyMax = 0.3f;
        public static float COSpearsProficiencyMax = 0.3f;
        public static float COJavelinsProficiencyMax = 0.3f;
        public static float COMacesProficiencyMax = 0.3f;
        public static float COClubsProficiencyMax = 0.3f;
        public static float COHalberdsProficiencyMax = 0.3f;
        public static float COPoleaxeProficiencyMax = 0.3f;
        public static float COAxesProficiencyMax = 0.3f;
        public static float COQuarterstaffProficiencyMax = 0.3f;
        public static float COSteadyAimMax = 0.5f;

        // Per-proficiency base and increment overrides (optional, falls back to global values)
        public static Dictionary<string, int> COProficiencyBaseOverrides = new Dictionary<string, int>();
        public static Dictionary<string, int> COProficiencyIncrementOverrides = new Dictionary<string, int>();

        // CO proficiency stat names (must match Combat Overhaul's stat names)
        public const string CO_BOWS_PROFICIENCY = "bowsProficiency";
        public const string CO_CROSSBOWS_PROFICIENCY = "crossbowsProficiency";
        public const string CO_FIREARMS_PROFICIENCY = "firearmsProficiency";
        public const string CO_SLINGS_PROFICIENCY = "slingsProficiency";
        public const string CO_ONE_HANDED_SWORDS_PROFICIENCY = "oneHandedSwordsProficiency";
        public const string CO_TWO_HANDED_SWORDS_PROFICIENCY = "twoHandedSwordsProficiency";
        public const string CO_SPEARS_PROFICIENCY = "spearsProficiency";
        public const string CO_JAVELINS_PROFICIENCY = "javelinsProficiency";
        public const string CO_MACES_PROFICIENCY = "macesProficiency";
        public const string CO_CLUBS_PROFICIENCY = "clubsProficiency";
        public const string CO_HALBERDS_PROFICIENCY = "halberdsProficiency";
        public const string CO_POLEAXE_PROFICIENCY = "poleaxeProficiency";
        public const string CO_AXES_PROFICIENCY = "axesProficiency";
        public const string CO_QUARTERSTAFF_PROFICIENCY = "quarterstaffProficiency";
        public const string CO_STEADY_AIM = "steadyAim";

        // CO negative trait penalty values
        public const float CO_TREMBLING_AIM_PENALTY = 0.3f;
        public const float CO_CLUMSY_HANDS_PENALTY = 0.3f;
        public const int CO_FEAR_OF_MELEE_TIER_PENALTY = 1;
        public const float CO_WEAK_HAND_PENALTY = 0.3f;  // -0.3 to ranged proficiencies (similar to Clumsy Hands)
        public const int CO_NERVOUS_TIER_PENALTY = 1;    // -1 damage tier for piercing melee

        // CO Damage Tier Stats (armor penetration)
        public const string CO_RANGED_TIER_SLASHING = "rangedDamageTierBonusSlashingAttack";
        public const string CO_RANGED_TIER_PIERCING = "rangedDamageTierBonusPiercingAttack";
        public const string CO_RANGED_TIER_BLUNT = "rangedDamageTierBonusBluntAttack";
        public const string CO_MELEE_TIER_SLASHING = "meleeDamageTierBonusSlashingAttack";
        public const string CO_MELEE_TIER_PIERCING = "meleeDamageTierBonusPiercingAttack";
        public const string CO_MELEE_TIER_BLUNT = "meleeDamageTierBonusBluntAttack";

        // CO Body Damage Stats
        public const string CO_HEAD_DAMAGE_FACTOR = "playerHeadDamageFactor";
        public const string CO_FACE_DAMAGE_FACTOR = "playerFaceDamageFactor";
        public const string CO_LEGS_DAMAGE_FACTOR = "playerLegsDamageFactor";
        public const string CO_FEET_DAMAGE_FACTOR = "playerFeetDamageFactor";
        public const string CO_JUMP_HEIGHT = "jumpHeightMul";

        // CO Big Head / Thick Skull / Leg Day penalties and bonuses
        public const float CO_BIG_HEAD_PENALTY = 0.5f;      // +50% head/face damage (Clockmaker)
        public const float CO_THICK_SKULL_BONUS = 0.5f;     // -50% head/face damage (earnable)
        public const float CO_LEG_DAY_PENALTY = 1.0f;       // +100% leg/feet damage (Blackguard)
        public const float CO_LEG_DAY_JUMP_BONUS = 0.25f;   // +25% jump height (Blackguard)
        public const int CO_MELEE_EXPERT_TIER_BONUS = 1;    // +1 melee slashing tier (Blackguard)
        public const int CO_FRIGHTENED_TIER_PENALTY = 1;    // -1 melee slashing tier (Clockmaker)

        // WatchedAttributes keys for CO (client sync)
        public const string WATCHED_CO_STEADY_AIM_CREDITS = "sitCOSteadyAimCredits";
        public const string WATCHED_CO_TREMBLING_AIM_REMAINING = "sitCOTremblingAimRemaining";
        public const string WATCHED_CO_HAS_TREMBLING_AIM = "sitCOHasTremblingAim";
        public const string WATCHED_CO_CLUMSY_HANDS_REMAINING = "sitCOClumsyHandsRemaining";
        public const string WATCHED_CO_FEAR_OF_MELEE_REMAINING = "sitCOFearOfMeleeRemaining";
        public const string WATCHED_CO_WEAK_HAND_REMAINING = "sitCOWeakHandRemaining";
        public const string WATCHED_CO_NERVOUS_REMAINING = "sitCONervousRemaining";

        // WatchedAttributes for Big Head / Thick Skull / Leg Day / Melee Expert
        public const string WATCHED_CO_BIG_HEAD_REMAINING = "sitCOBigHeadRemaining";
        public const string WATCHED_CO_LEG_DAY_REMAINING = "sitCOLegDayRemaining";
        public const string WATCHED_CO_FRIGHTENED_REMAINING = "sitCOFrightenedRemaining";
        public const string WATCHED_CO_MELEE_TIER_BONUS = "sitCOMeleeTierBonus";
        public const string WATCHED_CO_RANGED_TIER_BONUS = "sitCORangedTierBonus";

        // CO stat codes (prefixed to avoid collisions)
        public const string CO_STAT_PREFIX = "sitCO";

        // CO persistence
        private const string CO_PROGRESS_SAVE_KEY = "sitCOProgress";

        // Storage for CO progress - keyed by player UID
        public static ConcurrentDictionary<string, COPlayerProgressData> COProgress = new ConcurrentDictionary<string, COPlayerProgressData>();

        // Flag to indicate pending CO progress save
        private static volatile bool pendingCOProgressSave = false;

        /// <summary>
        /// All CO proficiency stat names for iteration.
        /// </summary>
        public static readonly string[] AllCOProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY,
            CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY, CO_SPEARS_PROFICIENCY,
            CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY, CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY,
            CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY, CO_QUARTERSTAFF_PROFICIENCY
        };

        /// <summary>
        /// Ranged proficiencies that also contribute to Steady Aim.
        /// </summary>
        public static readonly string[] CORangedProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY
        };

        /// <summary>
        /// Detect if Combat Overhaul mod is loaded and log the result.
        /// </summary>
        private void DetectCombatOverhaul(ICoreServerAPI api)
        {
            // Accept the original Combat Overhaul AND the 1.22 fork
            // ("combatoverhaulfork"). The fork keeps CO's stat/trait names, so
            // detecting it re-enables proficiency progression, the bonus stat
            // application, and the /trait co* commands.
            IsCombatOverhaulLoaded = DetectAnyCombatOverhaul(api.ModLoader);
            IsCombatOverhaulForkLoaded = api.ModLoader.IsModEnabled("combatoverhaulfork");

            if (IsCombatOverhaulLoaded)
            {
                string which = IsCombatOverhaulForkLoaded
                    ? "Combat Overhaul (1.22 fork)" : "Combat Overhaul";
                if (COEnableCompat)
                {
                    api.Logger.Notification($"[SeraphLeveling] {which} detected - proficiency progression enabled");
                }
                else
                {
                    api.Logger.Notification($"[SeraphLeveling] {which} detected but compatibility disabled in config");
                }
            }
        }

        /// <summary>
        /// Get the max proficiency value for a given CO proficiency stat.
        /// </summary>
        public static float GetCOProficiencyMax(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return COBowsProficiencyMax;
                case CO_CROSSBOWS_PROFICIENCY: return COCrossbowsProficiencyMax;
                case CO_FIREARMS_PROFICIENCY: return COFirearmsProficiencyMax;
                case CO_SLINGS_PROFICIENCY: return COSlingsProficiencyMax;
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return COOneHandedSwordsProficiencyMax;
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return COTwoHandedSwordsProficiencyMax;
                case CO_SPEARS_PROFICIENCY: return COSpearsProficiencyMax;
                case CO_JAVELINS_PROFICIENCY: return COJavelinsProficiencyMax;
                case CO_MACES_PROFICIENCY: return COMacesProficiencyMax;
                case CO_CLUBS_PROFICIENCY: return COClubsProficiencyMax;
                case CO_HALBERDS_PROFICIENCY: return COHalberdsProficiencyMax;
                case CO_POLEAXE_PROFICIENCY: return COPoleaxeProficiencyMax;
                case CO_AXES_PROFICIENCY: return COAxesProficiencyMax;
                case CO_QUARTERSTAFF_PROFICIENCY: return COQuarterstaffProficiencyMax;
                case CO_STEADY_AIM: return COSteadyAimMax;
                default: return 0.3f; // Safe default
            }
        }

        /// <summary>
        /// Get the max credits for a CO proficiency (max bonus * 100).
        /// </summary>
        public static int GetCOProficiencyMaxCredits(string proficiencyStat)
        {
            return (int)(GetCOProficiencyMax(proficiencyStat) * 100);
        }

        /// <summary>
        /// Get max Steady Aim credits for a player, accounting for Trembling Aim.
        /// Players with Trembling Aim get extra credits to compensate for the penalty.
        /// </summary>
        public static int GetCOSteadyAimMaxCreditsForPlayer(string playerUid)
        {
            int baseMax = GetCOProficiencyMaxCredits(CO_STEADY_AIM); // 50
            var cache = GetCachedTraits(playerUid);
            if (cache?.HasCOTremblingAim == true)
            {
                // Add 30 credits to cancel Trembling Aim penalty (0.30 * 100 = 30)
                return baseMax + (int)(CO_TREMBLING_AIM_PENALTY * 100); // 50 + 30 = 80
            }
            return baseMax;
        }

        /// <summary>
        /// Get max credits for a CO proficiency for a specific player, accounting for negative traits.
        /// Players with negative traits (Clumsy Hands, Weak Hand, Fear of Melee, Nervous) get extra
        /// credits to compensate for their penalties before earning positive bonuses.
        /// </summary>
        public static int GetCOProficiencyMaxCreditsForPlayer(string playerUid, string proficiencyStat)
        {
            int baseMax = GetCOProficiencyMaxCredits(proficiencyStat);
            var cache = GetCachedTraits(playerUid);
            if (cache == null) return baseMax;

            bool isRanged = IsCORangedProficiency(proficiencyStat);
            bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

            // Clumsy Hands: affects ranged proficiencies (bows, crossbows, firearms, slings)
            if (isRanged && cache.HasCOClumsyHands)
            {
                return baseMax + (int)(CO_CLUMSY_HANDS_PENALTY * 100); // +30
            }
            // Weak Hand: affects ranged proficiencies (similar to Clumsy Hands)
            else if (isRanged && cache.HasCOWeakHand)
            {
                return baseMax + (int)(CO_WEAK_HAND_PENALTY * 100); // +30
            }
            // Fear of Melee: affects melee proficiencies (non-ranged)
            else if (!isRanged && cache.HasCOFearOfMelee)
            {
                return baseMax + CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // +100
            }
            // Nervous: affects piercing proficiencies (spears, javelins)
            else if (isPiercing && cache.HasCONervous)
            {
                return baseMax + CO_NERVOUS_TIER_PENALTY * 100; // +100
            }

            return baseMax;
        }

        /// <summary>
        /// Calculate proficiency bonus from credits.
        /// Each credit = 0.01 bonus.
        /// </summary>
        public static float CalculateCOProficiencyBonus(int credits, float maxBonus)
        {
            int maxCredits = (int)(maxBonus * 100);
            int cappedCredits = Math.Min(credits, maxCredits);
            return cappedCredits * 0.01f;
        }

        /// <summary>
        /// Detect Combat Overhaul weapon type from item code.
        /// Returns (proficiencyStat, weaponCode) or (null, null) if not a CO weapon.
        /// </summary>
        public static (string proficiencyStat, string weaponCode) GetCOWeaponType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return (null, null);

            // Remove namespace prefix for pattern matching
            string codeToCheck = itemCode;
            if (itemCode.Contains(":"))
            {
                codeToCheck = itemCode.Substring(itemCode.IndexOf(':') + 1);
            }
            string lowerCode = codeToCheck.ToLowerInvariant();

            // Crossbows (check before bows)
            if (lowerCode.StartsWith("crossbow-", StringComparison.Ordinal) || lowerCode.StartsWith("crossbow", StringComparison.Ordinal) ||
                lowerCode.Contains("crossbow"))
                return (CO_CROSSBOWS_PROFICIENCY, itemCode);

            // Firearms
            if (lowerCode.StartsWith("musket-", StringComparison.Ordinal) || lowerCode.StartsWith("pistol-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("rifle-", StringComparison.Ordinal) || lowerCode.StartsWith("blunderbuss-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("arquebus-", StringComparison.Ordinal) || lowerCode.Contains("firearm") ||
                lowerCode.Contains("gun-"))
                return (CO_FIREARMS_PROFICIENCY, itemCode);

            // Two-Handed Swords (check before one-handed)
            // Combat Armory uses "sword-great-" and "sword-long-" formats
            // Ancient Armory uses "aa-blade-claymore-" and "aa-blade-longsword-" formats
            if (lowerCode.StartsWith("greatsword-", StringComparison.Ordinal) || lowerCode.StartsWith("zweihander-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("claymore-", StringComparison.Ordinal) || lowerCode.StartsWith("flamberge-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("montante-", StringComparison.Ordinal) || lowerCode.StartsWith("nodachi-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("2hsword-", StringComparison.Ordinal) || lowerCode.StartsWith("2h-sword-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("twohandedsword-", StringComparison.Ordinal) || lowerCode.StartsWith("twohanded-sword-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("sword-great-", StringComparison.Ordinal) || // Combat Armory greatswords
                lowerCode.StartsWith("sword-long-", StringComparison.Ordinal) ||  // Combat Armory longswords
                lowerCode.StartsWith("longsword-", StringComparison.Ordinal) ||   // Standard longsword prefix
                lowerCode.StartsWith("aa-blade-claymore-", StringComparison.Ordinal) ||  // Ancient Armory claymore
                lowerCode.StartsWith("aa-blade-longsword-", StringComparison.Ordinal) || // Ancient Armory longsword
                (lowerCode.Contains("twohanded") && lowerCode.Contains("sword")) ||
                (lowerCode.Contains("2h") && lowerCode.Contains("sword")))
                return (CO_TWO_HANDED_SWORDS_PROFICIENCY, itemCode);

            // One-Handed Swords
            // Note: sword-long- and longsword- are handled above as two-handed
            // Ancient Armory: aa-blade-gladius, aa-blade-arming, aa-blade-sabre, aa-blade-falchion, aa-knife-*
            if (lowerCode.StartsWith("sword-", StringComparison.Ordinal) || lowerCode.StartsWith("blade-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("shortsword-", StringComparison.Ordinal) || lowerCode.StartsWith("sword-short-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("sword-arming-", StringComparison.Ordinal) || // Combat Armory arming swords
                lowerCode.StartsWith("saber-", StringComparison.Ordinal) || lowerCode.StartsWith("sabre-", StringComparison.Ordinal) || // Both spellings
                lowerCode.StartsWith("rapier-", StringComparison.Ordinal) || lowerCode.StartsWith("scimitar-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("cutlass-", StringComparison.Ordinal) || lowerCode.StartsWith("falx-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("falchion-", StringComparison.Ordinal) || lowerCode.StartsWith("dagger-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("knife-", StringComparison.Ordinal) || lowerCode.StartsWith("kopis-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("gladius-", StringComparison.Ordinal) || lowerCode.StartsWith("messer-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("aa-blade-gladius-", StringComparison.Ordinal) ||   // Ancient Armory gladius
                lowerCode.StartsWith("aa-blade-arming-", StringComparison.Ordinal) ||    // Ancient Armory arming sword
                lowerCode.StartsWith("aa-blade-sabre-", StringComparison.Ordinal) ||     // Ancient Armory sabre
                lowerCode.StartsWith("aa-blade-falchion-", StringComparison.Ordinal) ||  // Ancient Armory falchion
                lowerCode.StartsWith("aa-knife-", StringComparison.Ordinal))             // Ancient Armory knives (dagger, stiletto, khanjar, baselard)
                return (CO_ONE_HANDED_SWORDS_PROFICIENCY, itemCode);

            // Poleaxes: the CO fork tracks these under a separate poleaxeProficiency
            // stat. The original CO has no such stat, so there we fall back to
            // halberds, preserving the previous behavior.
            if (lowerCode.StartsWith("poleaxe-", StringComparison.Ordinal))
                return (IsCombatOverhaulForkLoaded ? CO_POLEAXE_PROFICIENCY : CO_HALBERDS_PROFICIENCY, itemCode);

            // Halberds (polearms with axe heads)
            // Ancient Armory: aa-spear-voulge is a halberd-type weapon
            if (lowerCode.StartsWith("halberd-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("glaive-", StringComparison.Ordinal) || lowerCode.StartsWith("bardiche-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("voulge-", StringComparison.Ordinal) || lowerCode.StartsWith("guisarme-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("aa-spear-voulge-", StringComparison.Ordinal))  // Ancient Armory voulge
                return (CO_HALBERDS_PROFICIENCY, itemCode);

            // Quarterstaff
            if (lowerCode.StartsWith("quarterstaff-", StringComparison.Ordinal) || lowerCode.StartsWith("staff-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("bo-", StringComparison.Ordinal) || lowerCode.Contains("bo-staff"))
                return (CO_QUARTERSTAFF_PROFICIENCY, itemCode);

            // Maces
            // Ancient Armory: aa-club-* (flanged, morningstar, spiked, warhammer)
            if (lowerCode.StartsWith("mace-", StringComparison.Ordinal) || lowerCode.StartsWith("morningstar-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("flail-", StringComparison.Ordinal) || lowerCode.StartsWith("warhammer-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("aa-club-", StringComparison.Ordinal))  // Ancient Armory clubs/maces
                return (CO_MACES_PROFICIENCY, itemCode);

            // Clubs
            if (lowerCode.StartsWith("club-", StringComparison.Ordinal) || lowerCode.StartsWith("cudgel-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("baton-", StringComparison.Ordinal) || lowerCode.StartsWith("truncheon-", StringComparison.Ordinal))
                return (CO_CLUBS_PROFICIENCY, itemCode);

            // Axes (combat axes, not tool axes)
            // Ancient Armory: aa-axe-* (bearded, battle, bardiche)
            if (lowerCode.StartsWith("battleaxe-", StringComparison.Ordinal) || lowerCode.StartsWith("waraxe-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("handaxe-", StringComparison.Ordinal) || lowerCode.StartsWith("hatchet-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("aa-axe-", StringComparison.Ordinal) ||  // Ancient Armory battle axes
                (lowerCode.StartsWith("axe-", StringComparison.Ordinal) && !lowerCode.Contains("pickaxe")))
                return (CO_AXES_PROFICIENCY, itemCode);

            // Javelins (thrown spears)
            if (lowerCode.StartsWith("javelin-", StringComparison.Ordinal) || lowerCode.StartsWith("pilum-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("throwing-spear-", StringComparison.Ordinal) || lowerCode.StartsWith("thrown-spear-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("dart-", StringComparison.Ordinal) || lowerCode.StartsWith("plumbata-", StringComparison.Ordinal) ||
                lowerCode.Contains("javelin") || lowerCode.Contains("throwingspear") ||
                lowerCode.Contains("thrownspear") || lowerCode.Contains("throwing-spear"))
                return (CO_JAVELINS_PROFICIENCY, itemCode);

            // Spears (melee)
            // Ancient Armory: aa-spear-boar, aa-spear-fork, aa-spear-ranseur (but NOT voulge - that's a halberd)
            if (lowerCode.StartsWith("spear-", StringComparison.Ordinal) || lowerCode.StartsWith("pike-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("lance-", StringComparison.Ordinal) || lowerCode.StartsWith("trident-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("aa-spear-boar-", StringComparison.Ordinal) ||     // Ancient Armory boar spear
                lowerCode.StartsWith("aa-spear-fork-", StringComparison.Ordinal) ||     // Ancient Armory fork
                lowerCode.StartsWith("aa-spear-ranseur-", StringComparison.Ordinal))    // Ancient Armory ranseur
                return (CO_SPEARS_PROFICIENCY, itemCode);

            // Bows (standard - after crossbows check)
            if (lowerCode.StartsWith("bow-", StringComparison.Ordinal) || lowerCode.StartsWith("longbow-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("shortbow-", StringComparison.Ordinal) || lowerCode.StartsWith("recurvebow-", StringComparison.Ordinal) ||
                lowerCode.StartsWith("recurve-", StringComparison.Ordinal) || lowerCode.StartsWith("composite-bow", StringComparison.Ordinal))
                return (CO_BOWS_PROFICIENCY, itemCode);

            // Slings
            if (lowerCode.StartsWith("sling-", StringComparison.Ordinal) || lowerCode == "sling")
                return (CO_SLINGS_PROFICIENCY, itemCode);

            // Debug logging for unmatched weapons - helps identify missing item codes
            if (DebugLoggingEnabled &&
                (lowerCode.Contains("sword") || lowerCode.Contains("blade") || lowerCode.Contains("spear") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("axe") || lowerCode.Contains("mace") ||
                lowerCode.Contains("hammer") || lowerCode.Contains("club") || lowerCode.Contains("bow") ||
                lowerCode.Contains("staff") || lowerCode.Contains("halberd") || lowerCode.Contains("pike") ||
                lowerCode.Contains("sabre") || lowerCode.Contains("weapon") || lowerCode.Contains("dagger")))
            {
                ServerApi?.Logger?.Debug($"[SeraphLeveling] CO: Unmatched weapon item code: '{itemCode}' (normalized: '{lowerCode}')");
            }

            return (null, null);
        }

        /// <summary>
        /// Check if a proficiency stat is a ranged proficiency (contributes to Steady Aim).
        /// </summary>
        public static bool IsCORangedProficiency(string proficiencyStat)
        {
            return proficiencyStat == CO_BOWS_PROFICIENCY ||
                   proficiencyStat == CO_CROSSBOWS_PROFICIENCY ||
                   proficiencyStat == CO_FIREARMS_PROFICIENCY ||
                   proficiencyStat == CO_SLINGS_PROFICIENCY;
        }

        /// <summary>
        /// Process Combat Overhaul proficiency damage dealt by a player.
        /// Called from Harmony patch when CO is enabled.
        /// </summary>
        public static void ProcessCOProficiencyDamage(IServerPlayer attackerPlayer, string proficiencyStat, string weaponCode, float damage)
        {
            ProcessCOProficiencyDamageCore(attackerPlayer, proficiencyStat, weaponCode, damage);
            MarkProgressChanged(attackerPlayer);   // handbook page: push the new numbers within 100 ms
        }

        public static void ProcessCOProficiencyDamageCore(IServerPlayer attackerPlayer, string proficiencyStat, string weaponCode, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(proficiencyStat) || string.IsNullOrEmpty(weaponCode)) return;

            // Skip if CO compat is disabled
            if (!IsCOCompatEnabled) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player CO progress data
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            // Get max credits for this proficiency (player-aware, accounts for negative traits)
            int maxCredits = GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);

            // Get or create progress for this proficiency type
            var proficiencyProgress = playerProgress.GetProficiencyProgress(proficiencyStat);

            // Skip if already at max for this proficiency
            if (proficiencyProgress.TotalCredits >= maxCredits)
            {
                // Still process Steady Aim if this is ranged and steady aim isn't maxed
                if (IsCORangedProficiency(proficiencyStat))
                {
                    ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
                }
                return;
            }

            // Get or create progress for this specific weapon (using per-proficiency base)
            int profBase = GetCOProficiencyBase(proficiencyStat);
            int profIncrement = GetCOProficiencyIncrement(proficiencyStat);
            var weaponProgress = proficiencyProgress.GetWeaponProgress(weaponCode, profBase);

            int oldCredits = proficiencyProgress.TotalCredits;

            // Add damage to this weapon's progress (apply sleep buff multiplier if active)
            float modifiedDamage = ApplyXPMultiplier(attackerPlayer.PlayerUID, damage);
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && proficiencyProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                proficiencyProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += profIncrement;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned CO {proficiencyStat} credit {proficiencyProgress.TotalCredits} with {weaponCode}");
            }


            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "coproficiency");

            // If credits increased, update the stat and notify player
            if (proficiencyProgress.TotalCredits > oldCredits)
            {
                ApplyCOProficiencyBonus(attackerPlayer, proficiencyStat, proficiencyProgress.TotalCredits);

                // Update negative trait remaining display based on all proficiencies
                UpdateCONegativeTraitRemaining(attackerPlayer);

                // Notify player of level up
                float bonus = CalculateCOProficiencyBonus(proficiencyProgress.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Proficiency level up! Your {GetCOProficiencyDisplayName(proficiencyStat)} is now +{bonus * 100:F0}%.",
                    EnumChatType.Notification);
            }

            // Also process Steady Aim if this is a ranged proficiency
            if (IsCORangedProficiency(proficiencyStat))
            {
                ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
            }
        }

        /// <summary>
        /// Process Steady Aim progression (shared with ranged proficiencies).
        /// </summary>
        private static void ProcessCOSteadyAimProgress(IServerPlayer player, COPlayerProgressData playerProgress, float damage)
        {
            // Use player-aware max that accounts for Trembling Aim
            int maxSteadyAimCredits = GetCOSteadyAimMaxCreditsForPlayer(player.PlayerUID);

            // Skip if already at max
            if (playerProgress.SteadyAimCredits >= maxSteadyAimCredits) return;

            // Use a simple progression for Steady Aim (not per-weapon)
            // We'll use SteadyAimCredits to track total credits earned
            // For simplicity, every ranged damage point adds to a shared progress counter
            // We'll track damage in a special "steadyaim" key in the bows proficiency
            int steadyAimBase = GetCOProficiencyBase(CO_STEADY_AIM);
            int steadyAimIncrement = GetCOProficiencyIncrement(CO_STEADY_AIM);
            var steadyAimProgress = playerProgress.GetProficiencyProgress(CO_STEADY_AIM);
            var sharedProgress = steadyAimProgress.GetWeaponProgress("_ranged_combined", steadyAimBase);

            int oldCredits = playerProgress.SteadyAimCredits;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(player.PlayerUID, damage);
            sharedProgress.DamageInIncrement += modifiedDamage;
            // Flag here, not just in the caller: the at-max early-return path
            // reaches this function without setting it.
            pendingCOProgressSave = true;

            while (sharedProgress.DamageInIncrement >= sharedProgress.CurrentIncrementSize && playerProgress.SteadyAimCredits < maxSteadyAimCredits)
            {
                playerProgress.SteadyAimCredits++;
                sharedProgress.DamageInIncrement -= sharedProgress.CurrentIncrementSize;
                sharedProgress.CurrentIncrementSize += steadyAimIncrement;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned CO Steady Aim credit {playerProgress.SteadyAimCredits}");
            }

            if (playerProgress.SteadyAimCredits > oldCredits)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);

                float bonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Steady Aim improved! Your aim stability is now +{bonus * 100:F0}%.",
                    EnumChatType.Notification);
            }
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Delegates to ApplyCOProficiencyBonusWithCancellation for negative trait handling.
        /// </summary>
        private static void ApplyCOProficiencyBonus(IServerPlayer player, string proficiencyStat, int credits)
        {
            ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
        }

        /// <summary>
        /// Apply Combat Overhaul Steady Aim bonus to a player.
        /// Handles Trembling Aim negative trait cancellation.
        /// </summary>
        private static void ApplyCOSteadyAimBonus(IServerPlayer player, int credits)
        {
            if (player?.Entity == null) return;

            // Check for Trembling Aim negative trait
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasTremblingAim = cache?.HasCOTremblingAim ?? false;

            // Calculate remaining penalty and net bonus
            float tremblingAimRemaining = 0f;
            float netBonus = 0f;

            if (hasTremblingAim)
            {
                // Trembling Aim penalty is 0.3, cancelled by Steady Aim credits (30 credits to cancel)
                int creditsToCancel = (int)(CO_TREMBLING_AIM_PENALTY * 100); // 30
                tremblingAimRemaining = Math.Max(0, CO_TREMBLING_AIM_PENALTY - credits * 0.01f);
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), COSteadyAimMax);
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, COSteadyAimMax);
            }

            // Trembling Aim's penalty is already in this stat under the code "trait", and
            // stat values sum, so withholding our own bonus for the first 30 credits never
            // removed it. Those credits bought nothing. Cancel it explicitly instead: add
            // back the part of the penalty the player has paid off, and keep our earned
            // bonus on top. A class perk on steadyAim is positive and is left alone.
            float steadyAimTraitPenalty = TraitStatPenalty(player.Entity, CO_STEADY_AIM);
            float steadyAimRemaining = Math.Max(0f, -steadyAimTraitPenalty - credits * 0.01f);
            string statCode = CO_STAT_PREFIX + CO_STEADY_AIM;
            player.Entity.Stats.Set(CO_STEADY_AIM, statCode, netBonus - steadyAimRemaining - steadyAimTraitPenalty, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_STEADY_AIM_CREDITS, credits);
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, tremblingAimRemaining);
            player.Entity.WatchedAttributes.SetBool(WATCHED_CO_HAS_TREMBLING_AIM, hasTremblingAim);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_STEADY_AIM_CREDITS);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_TREMBLING_AIM_REMAINING);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_HAS_TREMBLING_AIM);
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Handles negative trait cancellation for various proficiencies.
        /// </summary>
        private static void ApplyCOProficiencyBonusWithCancellation(IServerPlayer player, string proficiencyStat, int credits)
        {
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasClumsyHands = cache?.HasCOClumsyHands ?? false;
            bool hasWeakHand = cache?.HasCOWeakHand ?? false;
            bool hasFearOfMelee = cache?.HasCOFearOfMelee ?? false;
            bool hasNervous = cache?.HasCONervous ?? false;

            float maxBonus = GetCOProficiencyMax(proficiencyStat);
            float netBonus = 0f;
            bool isRanged = IsCORangedProficiency(proficiencyStat);
            bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

            // Handle Clumsy Hands for ranged proficiencies (bows, crossbows, firearms)
            if (hasClumsyHands && isRanged)
            {
                // Clumsy Hands gives -0.3 to bows, crossbows, firearms (30 credits to cancel each)
                int creditsToCancel = (int)(CO_CLUMSY_HANDS_PENALTY * 100); // 30
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Weak Hand for ranged proficiencies (similar to Clumsy Hands)
            else if (hasWeakHand && isRanged)
            {
                // Weak Hand gives -0.3 to ranged proficiencies (30 credits to cancel each)
                int creditsToCancel = (int)(CO_WEAK_HAND_PENALTY * 100); // 30
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Fear of Melee for melee proficiencies (tier-based)
            else if (hasFearOfMelee && !isRanged)
            {
                // Fear of Melee gives -1 slashing damage tier
                // This needs 100 credits to cancel (1 tier = 100 credits in our system)
                int creditsToCancel = CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // 100
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Nervous for piercing melee (spears, javelins) - tier-based
            else if (hasNervous && isPiercing)
            {
                // Nervous gives -1 damage tier for piercing melee
                // This needs 100 credits to cancel (1 tier = 100 credits in our system)
                int creditsToCancel = CO_NERVOUS_TIER_PENALTY * 100; // 100
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, maxBonus);
            }

            // A trait penalty on this proficiency, Clumsy Hands being the one Combat Overhaul
            // ships, is already in the stat under the code "trait", and stat values sum, so
            // withholding our own bonus above never removed it. Cancel it explicitly: add back
            // the part the player has paid off, then stack our earned bonus on top. Reading
            // the applied value means this covers whichever proficiencies the penalty really
            // touches, rather than every stat IsCORangedProficiency happens to return true for.
            // A class perk on a proficiency is positive and is left alone to stack.
            float profTraitPenalty = TraitStatPenalty(player.Entity, proficiencyStat);
            float profPenaltyRemaining = Math.Max(0f, -profTraitPenalty - credits * 0.01f);

            // Apply stat using CO stat name with our prefix
            string statCode = CO_STAT_PREFIX + proficiencyStat;
            player.Entity.Stats.Set(proficiencyStat, statCode, netBonus - profPenaltyRemaining - profTraitPenalty, false);

            // Sync credits to WatchedAttributes
            string watchedKey = $"sitCO{proficiencyStat}Credits";
            player.Entity.WatchedAttributes.SetInt(watchedKey, credits);
            player.Entity.WatchedAttributes.MarkPathDirty(watchedKey);
        }

        /// <summary>
        /// Update the remaining penalty display for CO negative traits.
        /// This calculates the MAXIMUM remaining penalty across all affected proficiencies
        /// to show the worst-case remaining penalty in the UI.
        /// Should be called after all proficiency bonuses are applied.
        /// </summary>
        private static void UpdateCONegativeTraitRemaining(IServerPlayer player)
        {
            if (!IsCOCompatEnabled || player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var cache = GetCachedTraits(playerUid);
            if (cache == null) return;

            if (!COProgress.TryGetValue(playerUid, out var playerProgress)) return;

            // Clumsy Hands: affects bows, crossbows, firearms, slings (all ranged)
            if (cache.HasCOClumsyHands)
            {
                float maxRemaining = CO_CLUMSY_HANDS_PENALTY; // Start with full penalty
                var rangedProficiencies = new[] { CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY };

                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_CLUMSY_HANDS_PENALTY - profData.TotalCredits * 0.01f);
                        maxRemaining = Math.Max(maxRemaining, remaining);
                    }
                }

                // Find the minimum remaining (most progress made on any proficiency)
                float minRemaining = CO_CLUMSY_HANDS_PENALTY;
                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_CLUMSY_HANDS_PENALTY - profData.TotalCredits * 0.01f);
                        minRemaining = Math.Min(minRemaining, remaining);
                    }
                }

                player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, minRemaining);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_CLUMSY_HANDS_REMAINING);
            }

            // Weak Hand: similar to Clumsy Hands, affects ranged proficiencies
            if (cache.HasCOWeakHand)
            {
                var rangedProficiencies = new[] { CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY };

                float minRemaining = CO_WEAK_HAND_PENALTY;
                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_WEAK_HAND_PENALTY - profData.TotalCredits * 0.01f);
                        minRemaining = Math.Min(minRemaining, remaining);
                    }
                }

                player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_WEAK_HAND_REMAINING, minRemaining);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_WEAK_HAND_REMAINING);
            }

            // Fear of Melee: affects all melee proficiencies (tier-based)
            if (cache.HasCOFearOfMelee)
            {
                var meleeProficiencies = new[] {
                    CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY,
                    CO_SPEARS_PROFICIENCY, CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY,
                    CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY, CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY,
                    CO_QUARTERSTAFF_PROFICIENCY
                };

                int minRemainingTiers = CO_FEAR_OF_MELEE_TIER_PENALTY;
                foreach (var prof in meleeProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        int remainingTiers = Math.Max(0, CO_FEAR_OF_MELEE_TIER_PENALTY - profData.TotalCredits / 100);
                        minRemainingTiers = Math.Min(minRemainingTiers, remainingTiers);
                    }
                }

                player.Entity.WatchedAttributes.SetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, minRemainingTiers);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_FEAR_OF_MELEE_REMAINING);
            }

            // Nervous: affects piercing melee (spears, javelins) - tier-based
            if (cache.HasCONervous)
            {
                var piercingProficiencies = new[] { CO_SPEARS_PROFICIENCY, CO_JAVELINS_PROFICIENCY };

                int minRemainingTiers = CO_NERVOUS_TIER_PENALTY;
                foreach (var prof in piercingProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        int remainingTiers = Math.Max(0, CO_NERVOUS_TIER_PENALTY - profData.TotalCredits / 100);
                        minRemainingTiers = Math.Min(minRemainingTiers, remainingTiers);
                    }
                }

                player.Entity.WatchedAttributes.SetInt(WATCHED_CO_NERVOUS_REMAINING, minRemainingTiers);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_NERVOUS_REMAINING);
            }
        }

        /// <summary>
        /// Apply Big Head / Thick Skull handling for Combat Overhaul.
        /// Clockmaker has Big Head (+50% head/face damage), cancelled by armor credits.
        /// Malefactor has Thick Skull (-50% head/face damage) from start.
        /// Other classes can earn Thick Skull via armor credits.
        /// </summary>
        private static void ApplyCOBigHeadThickSkull(IServerPlayer player, int armorCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasBigHead = cache?.HasCOBigHead ?? false;     // Clockmaker
            bool hasThickSkull = cache?.HasCOThickSkull ?? false; // Malefactor

            float headFactor = 0f;
            float faceFactor = 0f;
            float remainingPenalty = 0f;

            if (hasBigHead)
            {
                // Big Head: +0.5 head/face damage, cancelled by 50 armor credits
                // After cancellation, can earn Thick Skull bonus (up to -0.5)
                remainingPenalty = Math.Max(0, CO_BIG_HEAD_PENALTY - armorCredits * 0.01f);

                if (armorCredits >= 50)
                {
                    // Penalty cancelled, now earning bonus
                    int bonusCredits = armorCredits - 50;
                    float bonus = Math.Min(bonusCredits * 0.01f, CO_THICK_SKULL_BONUS);
                    headFactor = -bonus;
                    faceFactor = -bonus;
                }
                else
                {
                    headFactor = remainingPenalty;
                    faceFactor = remainingPenalty;
                }
            }
            else if (hasThickSkull)
            {
                // Malefactor already has Thick Skull (-0.5), can't earn more
                headFactor = -CO_THICK_SKULL_BONUS;
                faceFactor = -CO_THICK_SKULL_BONUS;
            }
            else
            {
                // Other classes: can earn Thick Skull (up to -0.5)
                float bonus = Math.Min(armorCredits * 0.01f, CO_THICK_SKULL_BONUS);
                headFactor = -bonus;
                faceFactor = -bonus;
            }

            // headFactor and faceFactor above are the totals we want the player to end up
            // with. Big Head and Thick Skull have already put their own value into these
            // stats under the code "trait", and stat values sum, so write the difference.
            // See TraitStatValue.
            player.Entity.Stats.Set(CO_HEAD_DAMAGE_FACTOR, CO_STAT_PREFIX + "headDamage",
                headFactor - TraitStatValue(player.Entity, CO_HEAD_DAMAGE_FACTOR), false);
            player.Entity.Stats.Set(CO_FACE_DAMAGE_FACTOR, CO_STAT_PREFIX + "faceDamage",
                faceFactor - TraitStatValue(player.Entity, CO_FACE_DAMAGE_FACTOR), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_BIG_HEAD_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_BIG_HEAD_REMAINING);
        }

        /// <summary>
        /// Apply Frightened of Melee / Melee Expert handling for Combat Overhaul.
        /// Clockmaker has Frightened of Melee (-1 melee slashing tier), cancelled by melee credits.
        /// Blackguard has Melee Expert (+1 melee slashing tier) from start.
        /// Other classes can earn up to +1 melee slashing tier via melee credits.
        /// </summary>
        private static void ApplyCOMeleeTier(IServerPlayer player, int meleeCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasFrightenedOfMelee = cache?.HasCOFearOfMelee ?? false; // Clockmaker
            bool hasMeleeExpert = cache?.HasCOMeleeExpert ?? false;        // Blackguard

            int netTier = 0;
            int remainingPenalty = 0;

            if (hasFrightenedOfMelee)
            {
                // -1 tier penalty, 100 credits to cancel
                remainingPenalty = Math.Max(0, CO_FRIGHTENED_TIER_PENALTY - meleeCredits / 100);

                if (meleeCredits >= 100)
                {
                    // Penalty cancelled, can earn positive tier
                    int bonusCredits = meleeCredits - 100;
                    int earnedTier = Math.Min(bonusCredits / 100, 1);
                    netTier = earnedTier;
                }
                else
                {
                    netTier = -remainingPenalty;
                }
            }
            else if (hasMeleeExpert)
            {
                // Blackguard has +1 tier from Melee Expert. Classic behavior: that IS
                // the cap. With class cap offsets they can earn one more on top.
                netTier = CO_MELEE_EXPERT_TIER_BONUS + (EnableClassCapOffsets ? Math.Min(meleeCredits / 100, 1) : 0);
            }
            else
            {
                // Other classes: can earn up to +1 tier
                netTier = Math.Min(meleeCredits / 100, 1);
            }

            // netTier is the tier bonus we want in total. Melee Expert and Frightened of
            // Melee have already put theirs into this stat under the code "trait", so
            // subtract whatever is actually there. See TraitStatValue.
            player.Entity.Stats.Set(CO_MELEE_TIER_SLASHING, CO_STAT_PREFIX + "meleeTierSlashing",
                netTier - TraitStatValue(player.Entity, CO_MELEE_TIER_SLASHING), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_FRIGHTENED_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_MELEE_TIER_BONUS, netTier);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_FRIGHTENED_REMAINING);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_MELEE_TIER_BONUS);
        }

        /// <summary>
        /// Apply Leg Day handling for Combat Overhaul.
        /// Blackguard has Leg Day (+100% leg/feet damage, +25% jump height).
        /// The leg damage penalty can be reduced by armor credits (100 credits = cancel).
        /// Jump height bonus is always applied (it's a benefit).
        /// </summary>
        private static void ApplyCOLegDay(IServerPlayer player, int armorCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasLegDay = cache?.HasCOLegDay ?? false; // Blackguard

            if (!hasLegDay) return;

            // Leg Day: +1.0 leg/feet damage, +0.25 jump height
            // Leg damage penalty reduced by armor credits (100 credits = cancel)
            float remainingPenalty = Math.Max(0, CO_LEG_DAY_PENALTY - armorCredits * 0.01f);

            // The Leg Day trait has already applied its own values under the stat code
            // "trait", so write the difference between what we want and what it gave.
            // See TraitStatValue.
            player.Entity.Stats.Set(CO_LEGS_DAMAGE_FACTOR, CO_STAT_PREFIX + "legsDamage",
                remainingPenalty - TraitStatValue(player.Entity, CO_LEGS_DAMAGE_FACTOR), false);
            player.Entity.Stats.Set(CO_FEET_DAMAGE_FACTOR, CO_STAT_PREFIX + "feetDamage",
                remainingPenalty - TraitStatValue(player.Entity, CO_FEET_DAMAGE_FACTOR), false);
            // The jump bonus is a benefit the player keeps in full, so we only top up
            // whatever the trait did not already provide.
            player.Entity.Stats.Set(CO_JUMP_HEIGHT, CO_STAT_PREFIX + "jumpHeight",
                CO_LEG_DAY_JUMP_BONUS - TraitStatValue(player.Entity, CO_JUMP_HEIGHT), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_LEG_DAY_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_LEG_DAY_REMAINING);
        }

        /// <summary>
        /// Check if a proficiency is a piercing melee proficiency (affected by Nervous trait).
        /// </summary>
        private static bool IsCOPiercingProficiency(string proficiencyStat)
        {
            return proficiencyStat == CO_SPEARS_PROFICIENCY || proficiencyStat == CO_JAVELINS_PROFICIENCY;
        }

        /// <summary>
        /// Get a display-friendly name for a CO proficiency stat.
        /// </summary>
        public static string GetCOProficiencyDisplayName(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return "Bows Proficiency";
                case CO_CROSSBOWS_PROFICIENCY: return "Crossbows Proficiency";
                case CO_FIREARMS_PROFICIENCY: return "Firearms Proficiency";
                case CO_SLINGS_PROFICIENCY: return "Slings Proficiency";
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return "One-Handed Swords Proficiency";
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return "Two-Handed Swords Proficiency";
                case CO_SPEARS_PROFICIENCY: return "Spears Proficiency";
                case CO_JAVELINS_PROFICIENCY: return "Javelins Proficiency";
                case CO_MACES_PROFICIENCY: return "Maces Proficiency";
                case CO_CLUBS_PROFICIENCY: return "Clubs Proficiency";
                case CO_HALBERDS_PROFICIENCY: return "Halberds Proficiency";
                case CO_POLEAXE_PROFICIENCY: return "Poleaxe Proficiency";
                case CO_AXES_PROFICIENCY: return "Axes Proficiency";
                case CO_QUARTERSTAFF_PROFICIENCY: return "Quarterstaff Proficiency";
                case CO_STEADY_AIM: return "Steady Aim";
                default: return proficiencyStat;
            }
        }

        /// <summary>
        /// Apply all CO bonuses for a player (called on join/reconnect).
        /// </summary>
        public static void ApplyAllCOBonuses(IServerPlayer player)
        {
            if (!IsCOCompatEnabled || player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            // Initialize CO negative trait WatchedAttributes even if no progress exists yet.
            // This ensures the UI shows the correct penalties for new players.
            InitializeCONegativeTraitDefaults(player);

            if (!COProgress.TryGetValue(playerUid, out var playerProgress)) return;

            // Apply each proficiency bonus
            foreach (var proficiency in playerProgress.Proficiencies)
            {
                if (proficiency.Key != CO_STEADY_AIM)
                {
                    ApplyCOProficiencyBonus(player, proficiency.Key, proficiency.Value.TotalCredits);
                }
            }

            // Apply Steady Aim bonus
            if (playerProgress.SteadyAimCredits > 0)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);
            }

            // Update negative trait remaining display based on all proficiencies
            UpdateCONegativeTraitRemaining(player);
        }

        /// <summary>
        /// Initialize CO negative trait WatchedAttributes to their full penalty values
        /// for players who have those traits but no CO progress yet.
        /// This prevents the UI from showing 0 remaining (which hides the penalties).
        /// </summary>
        private static void InitializeCONegativeTraitDefaults(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            if (cache == null) return;

            var watchedAttrs = player.Entity.WatchedAttributes;

            if (cache.HasCOTremblingAim)
            {
                // Only set defaults if not already initialized (avoids overwriting earned progress)
                if (watchedAttrs.GetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, CO_TREMBLING_AIM_PENALTY);
                    watchedAttrs.SetBool(WATCHED_CO_HAS_TREMBLING_AIM, true);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_TREMBLING_AIM_REMAINING);
                }
            }

            if (cache.HasCOClumsyHands)
            {
                if (watchedAttrs.GetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, CO_CLUMSY_HANDS_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_CLUMSY_HANDS_REMAINING);
                }
            }

            if (cache.HasCOWeakHand)
            {
                if (watchedAttrs.GetFloat(WATCHED_CO_WEAK_HAND_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_WEAK_HAND_REMAINING, CO_WEAK_HAND_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_WEAK_HAND_REMAINING);
                }
            }

            if (cache.HasCOFearOfMelee)
            {
                if (watchedAttrs.GetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, -1) < 0)
                {
                    watchedAttrs.SetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, CO_FEAR_OF_MELEE_TIER_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_FEAR_OF_MELEE_REMAINING);
                }
            }

            if (cache.HasCONervous)
            {
                if (watchedAttrs.GetInt(WATCHED_CO_NERVOUS_REMAINING, -1) < 0)
                {
                    watchedAttrs.SetInt(WATCHED_CO_NERVOUS_REMAINING, CO_NERVOUS_TIER_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_NERVOUS_REMAINING);
                }
            }
        }

        // =========================================================================
        // COMBAT OVERHAUL COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait coproficiency command.
        /// Shows all Combat Overhaul proficiency progression.
        /// </summary>
        private TextCommandResult OnTraitCOProficiencyCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            if (!COEnableCompat)
            {
                return TextCommandResult.Error("Combat Overhaul compatibility is disabled in config.");
            }

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            sb.AppendLine("=== Combat Overhaul Proficiency Progression ===");

            if (!COProgress.TryGetValue(playerUid, out var playerProgress))
            {
                sb.AppendLine("No proficiency progress recorded yet. Deal damage with CO weapons to earn credits.");
                return TextCommandResult.Success(sb.ToString());
            }

            // Show Steady Aim (use player-aware max for Trembling Aim)
            var cache = GetCachedTraits(playerUid);
            bool hasTremblingAim = cache?.HasCOTremblingAim == true;
            int steadyAimMaxCredits = GetCOSteadyAimMaxCreditsForPlayer(playerUid);

            // Calculate net bonus (after cancellation if applicable)
            float steadyAimNetBonus;
            if (hasTremblingAim)
            {
                int creditsForBonus = Math.Max(0, playerProgress.SteadyAimCredits - 30);
                steadyAimNetBonus = Math.Min(creditsForBonus * 0.01f, COSteadyAimMax);
            }
            else
            {
                steadyAimNetBonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
            }
            sb.AppendLine($"Steady Aim: {playerProgress.SteadyAimCredits}/{steadyAimMaxCredits} credits (net +{steadyAimNetBonus * 100:F0}%)");

            // Show each proficiency
            sb.AppendLine("\n--- Ranged Proficiencies ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_BOWS_PROFICIENCY, "Bows");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_CROSSBOWS_PROFICIENCY, "Crossbows");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_FIREARMS_PROFICIENCY, "Firearms");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_SLINGS_PROFICIENCY, "Slings");

            sb.AppendLine("\n--- One-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_ONE_HANDED_SWORDS_PROFICIENCY, "One-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_MACES_PROFICIENCY, "Maces");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_CLUBS_PROFICIENCY, "Clubs");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_AXES_PROFICIENCY, "Axes");

            sb.AppendLine("\n--- Two-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_TWO_HANDED_SWORDS_PROFICIENCY, "Two-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_HALBERDS_PROFICIENCY, "Halberds");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_POLEAXE_PROFICIENCY, "Poleaxe");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_QUARTERSTAFF_PROFICIENCY, "Quarterstaff");

            sb.AppendLine("\n--- Polearms ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_SPEARS_PROFICIENCY, "Spears");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_JAVELINS_PROFICIENCY, "Javelins");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Helper to show CO proficiency stats in the command output.
        /// </summary>
        private static void ShowCOProficiencyStats(StringBuilder sb, COPlayerProgressData playerProgress, string playerUid, string proficiencyStat, string displayName)
        {
            if (playerProgress.Proficiencies.TryGetValue(proficiencyStat, out var prof))
            {
                float bonus = CalculateCOProficiencyBonus(prof.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                int maxCredits = GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);
                sb.AppendLine($"  {displayName}: {prof.TotalCredits}/{maxCredits} credits (+{bonus * 100:F0}%)");

                // Show per-weapon progress if any
                if (prof.WeaponProgress.Count > 0)
                {
                    foreach (var weapon in prof.WeaponProgress.Take(3)) // Show top 3 weapons
                    {
                        string shortCode = weapon.Key.Contains(":") ? weapon.Key.Substring(weapon.Key.IndexOf(':') + 1) : weapon.Key;
                        sb.AppendLine($"    {shortCode}: {weapon.Value.DamageInIncrement:F0}/{weapon.Value.CurrentIncrementSize} toward next");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  {displayName}: 0 credits (+0.00)");
            }
        }

        /// <summary>
        /// Handler for /trait colevel command.
        /// Sets Combat Overhaul proficiency credits directly.
        /// Usage: /trait colevel <proficiency> <credits>
        /// Proficiency names: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, axes, quarterstaff, steadyaim
        /// </summary>
        private TextCommandResult OnTraitCOLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string proficiencyArg = (string)args[0];
            int credits = (int)args[1];
            string toolName = (string)args[2];

            // Map short names to full proficiency stat names
            string proficiencyStat = proficiencyArg.ToLowerInvariant() switch
            {
                "bows" or "bow" => CO_BOWS_PROFICIENCY,
                "crossbows" or "crossbow" or "xbow" => CO_CROSSBOWS_PROFICIENCY,
                "firearms" or "firearm" or "guns" or "gun" => CO_FIREARMS_PROFICIENCY,
                "slings" or "sling" => CO_SLINGS_PROFICIENCY,
                "1hswords" or "1hsword" or "1h" or "onehanded" => CO_ONE_HANDED_SWORDS_PROFICIENCY,
                "2hswords" or "2hsword" or "2h" or "twohanded" => CO_TWO_HANDED_SWORDS_PROFICIENCY,
                "spears" or "spear" => CO_SPEARS_PROFICIENCY,
                "javelins" or "javelin" or "jav" => CO_JAVELINS_PROFICIENCY,
                "maces" or "mace" => CO_MACES_PROFICIENCY,
                "clubs" or "club" => CO_CLUBS_PROFICIENCY,
                "halberds" or "halberd" => CO_HALBERDS_PROFICIENCY,
                "poleaxe" or "poleaxes" => CO_POLEAXE_PROFICIENCY,
                "axes" or "axe" => CO_AXES_PROFICIENCY,
                "quarterstaff" or "staff" or "staves" => CO_QUARTERSTAFF_PROFICIENCY,
                "steadyaim" or "steady" or "aim" => CO_STEADY_AIM,
                _ => null
            };

            if (proficiencyStat == null)
            {
                return TextCommandResult.Error($"Unknown proficiency '{proficiencyArg}'. Valid options: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, poleaxe, axes, quarterstaff, steadyaim");
            }

            return SetCOLevelForPlayer(player, proficiencyStat, credits, toolName);
        }

        /// <summary>
        /// Handler for /trait coreset command.
        /// Resets all Combat Overhaul progression to 0. Also works when CO is uninstalled,
        /// to clear lingering progress data saved on the player.
        /// </summary>
        private TextCommandResult OnTraitCOResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool coLoaded = IsCombatOverhaulLoaded;
            ResetCOProgressForPlayer(player);

            return TextCommandResult.Success(coLoaded
                ? "All Combat Overhaul proficiency progression has been reset to 0."
                : "Combat Overhaul is not currently installed; cleared any lingering proficiency data saved on the player.");
        }

        /// <summary>
        /// Shared helper for resetting Combat Overhaul progression. Used by both /trait coreset
        /// and /trait reset. Runs even when CO isn't loaded so stale watched attributes /
        /// progress dictionary entries left over from a prior install can still be cleaned up
        /// (otherwise the display postfix keeps rendering ghost CO traits forever).
        /// </summary>
        private void ResetCOProgressForPlayer(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            if (COProgress.TryRemove(playerUid, out _))
            {
                pendingCOProgressSave = true;
            }

            // Clear all CO stats and reapply with 0 credits (properly recalculates negative trait penalties)
            foreach (var proficiencyStat in AllCOProficiencies)
            {
                string statCode = CO_STAT_PREFIX + proficiencyStat;
                player.Entity.Stats.Remove(proficiencyStat, statCode);

                // Reapply with 0 credits to properly set remaining penalty values
                ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, 0);
            }
            player.Entity.Stats.Remove(CO_STEADY_AIM, CO_STAT_PREFIX + CO_STEADY_AIM);

            // Reapply Steady Aim with 0 credits to properly set Trembling Aim remaining
            ApplyCOSteadyAimBonus(player, 0);
        }

        /// <summary>
        /// Handler for /trait comaxall command.
        /// Sets all Combat Overhaul proficiencies to max for testing.
        /// Dynamically adapts based on player's class and negative traits.
        /// </summary>
        private TextCommandResult OnTraitCOMaxAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string playerUid = player.PlayerUID;
            var cache = GetCachedTraits(playerUid);
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            var sb = new StringBuilder();
            sb.AppendLine("=== Setting all CO proficiencies to max ===");

            // Set each proficiency to max (accounting for negative traits)
            foreach (var proficiencyStat in AllCOProficiencies)
            {
                int maxCredits = GetCOProficiencyMaxCredits(proficiencyStat);

                // Add extra credits for negative trait cancellation
                bool isRanged = IsCORangedProficiency(proficiencyStat);
                bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

                if (isRanged && (cache?.HasCOClumsyHands == true))
                {
                    maxCredits += (int)(CO_CLUMSY_HANDS_PENALTY * 100); // +30
                }
                else if (isRanged && (cache?.HasCOWeakHand == true))
                {
                    maxCredits += (int)(CO_WEAK_HAND_PENALTY * 100); // +30
                }
                else if (!isRanged && (cache?.HasCOFearOfMelee == true))
                {
                    maxCredits += CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // +100
                }
                else if (isPiercing && (cache?.HasCONervous == true))
                {
                    maxCredits += CO_NERVOUS_TIER_PENALTY * 100; // +100
                }

                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);
                profProgress.TotalCredits = maxCredits;
                profProgress.WeaponProgress.Clear();
                ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, maxCredits);

                float bonus = CalculateCOProficiencyBonus(maxCredits, GetCOProficiencyMax(proficiencyStat));
                sb.AppendLine($"  {GetCOProficiencyDisplayName(proficiencyStat)}: {maxCredits} credits (+{bonus * 100:F0}%)");
            }

            // Set Steady Aim to max (accounting for Trembling Aim)
            int steadyAimMax = GetCOSteadyAimMaxCreditsForPlayer(playerUid);
            playerProgress.SteadyAimCredits = steadyAimMax;
            ApplyCOSteadyAimBonus(player, steadyAimMax);

            float steadyAimBonus;
            if (cache?.HasCOTremblingAim == true)
            {
                int creditsForBonus = Math.Max(0, steadyAimMax - 30);
                steadyAimBonus = Math.Min(creditsForBonus * 0.01f, COSteadyAimMax);
            }
            else
            {
                steadyAimBonus = CalculateCOProficiencyBonus(steadyAimMax, COSteadyAimMax);
            }
            sb.AppendLine($"  Steady Aim: {steadyAimMax} credits (net +{steadyAimBonus * 100:F0}%)");


            sb.AppendLine("\nAll CO proficiencies set to max!");
            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Unified handler for per-proficiency CO commands.
        /// Handles viewing stats and configuring base/increment/level/max.
        /// Usage: /trait [proficiency] [action] [value]
        /// Actions: base, increment, level, max (admin only for setting values)
        /// </summary>
        private TextCommandResult OnTraitCOProficiencyConfigCommand(TextCommandCallingArgs args, string proficiencyStat)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string action = args[0] as string;
            int? value = args[1] as int?;

            string displayName = GetCOProficiencyDisplayName(proficiencyStat);
            string playerUid = player.PlayerUID;
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            // No action = view stats
            if (string.IsNullOrEmpty(action))
            {
                return ShowCOProficiencyStatsForPlayer(player, playerProgress, proficiencyStat, displayName);
            }

            // Check admin privilege for configuration actions
            bool isAdmin = player.HasPrivilege(Privilege.controlserver);
            action = action.ToLowerInvariant();

            switch (action)
            {
                case "base":
                    if (!value.HasValue)
                    {
                        int currentBase = GetCOProficiencyBase(proficiencyStat);
                        int currentIncrement = GetCOProficiencyIncrement(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} base damage per increment: {currentBase}\nIncrement step: +{currentIncrement} per credit");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting base requires admin privileges.");
                    if (value.Value < 1) return TextCommandResult.Error("Base damage must be at least 1.");
                    SetCOProficiencyBase(proficiencyStat, value.Value);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} base damage per increment set to {value.Value}.");

                case "increment":
                    if (!value.HasValue)
                    {
                        int currentBase = GetCOProficiencyBase(proficiencyStat);
                        int currentIncrement = GetCOProficiencyIncrement(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} increment step: +{currentIncrement} per credit\nBase damage: {currentBase}");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting increment requires admin privileges.");
                    if (value.Value < 0) return TextCommandResult.Error("Increment step cannot be negative.");
                    SetCOProficiencyIncrement(proficiencyStat, value.Value);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} increment step set to +{value.Value} per credit.");

                case "level":
                    if (!value.HasValue)
                    {
                        int credits = proficiencyStat == CO_STEADY_AIM
                            ? playerProgress.SteadyAimCredits
                            : playerProgress.GetProficiencyProgress(proficiencyStat).TotalCredits;
                        return TextCommandResult.Success($"{displayName} current level: {credits} credits");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting level requires admin privileges.");
                    return SetCOLevelForPlayer(player, proficiencyStat, value.Value, null);

                case "max":
                    if (!value.HasValue)
                    {
                        float currentMax = GetCOProficiencyMax(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} max bonus: +{currentMax * 100:F0}%");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting max requires admin privileges.");
                    // Max is stored as float (0.5 = +0.5 bonus), but user enters as percentage points (50 = +0.5)
                    float newMax = value.Value / 100f;
                    if (newMax < 0) return TextCommandResult.Error("Max bonus cannot be negative.");
                    SetCOProficiencyMax(proficiencyStat, newMax);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} max bonus set to +{newMax * 100:F0}% ({value.Value} credits).");

                default:
                    return TextCommandResult.Error($"Unknown action '{action}'. Valid actions: base, increment, level, max");
            }
        }

        /// <summary>
        /// Show detailed stats for a specific CO proficiency.
        /// </summary>
        private TextCommandResult ShowCOProficiencyStatsForPlayer(IServerPlayer player, COPlayerProgressData playerProgress, string proficiencyStat, string displayName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {displayName} ===");

            int credits;
            float maxBonus = GetCOProficiencyMax(proficiencyStat);
            int maxCredits = (int)(maxBonus * 100);

            if (proficiencyStat == CO_STEADY_AIM)
            {
                credits = playerProgress.SteadyAimCredits;
                var steadyAimProgress = playerProgress.GetProficiencyProgress(CO_STEADY_AIM);

                sb.AppendLine($"Credits: {credits} / {maxCredits}");
                sb.AppendLine($"Bonus: +{CalculateCOProficiencyBonus(credits, maxBonus) * 100:F0}%");
                sb.AppendLine($"Base damage: {GetCOProficiencyBase(proficiencyStat)}");
                sb.AppendLine($"Increment step: +{GetCOProficiencyIncrement(proficiencyStat)} per credit");

                if (steadyAimProgress.WeaponProgress.Count > 0)
                {
                    sb.AppendLine("\nWeapon progress:");
                    foreach (var kvp in steadyAimProgress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                    {
                        string weaponName = kvp.Key;
                        if (weaponName.Contains(":"))
                            weaponName = weaponName.Substring(weaponName.IndexOf(':') + 1);
                        sb.AppendLine($"  {weaponName}: {kvp.Value.DamageInIncrement:F0}/{kvp.Value.CurrentIncrementSize} damage");
                    }
                }
            }
            else
            {
                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);
                credits = profProgress.TotalCredits;

                sb.AppendLine($"Credits: {credits} / {maxCredits}");
                sb.AppendLine($"Bonus: +{CalculateCOProficiencyBonus(credits, maxBonus) * 100:F0}%");
                sb.AppendLine($"Base damage: {GetCOProficiencyBase(proficiencyStat)}");
                sb.AppendLine($"Increment step: +{GetCOProficiencyIncrement(proficiencyStat)} per credit");

                if (profProgress.WeaponProgress.Count > 0)
                {
                    sb.AppendLine("\nWeapon progress:");
                    foreach (var kvp in profProgress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                    {
                        string weaponName = kvp.Key;
                        if (weaponName.Contains(":"))
                            weaponName = weaponName.Substring(weaponName.IndexOf(':') + 1);
                        sb.AppendLine($"  {weaponName}: {kvp.Value.DamageInIncrement:F0}/{kvp.Value.CurrentIncrementSize} damage");
                    }
                }
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Get the base damage per increment for a specific CO proficiency.
        /// Returns per-proficiency value if set, otherwise falls back to global default.
        /// </summary>
        private static int GetCOProficiencyBase(string proficiencyStat)
        {
            if (COProficiencyBaseOverrides.TryGetValue(proficiencyStat, out int value))
                return value;
            return COBaseDamagePerIncrement;
        }

        /// <summary>
        /// Set the base damage per increment for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyBase(string proficiencyStat, int value)
        {
            COProficiencyBaseOverrides[proficiencyStat] = value;
        }

        /// <summary>
        /// Get the increment step for a specific CO proficiency.
        /// Returns per-proficiency value if set, otherwise falls back to global default.
        /// </summary>
        private static int GetCOProficiencyIncrement(string proficiencyStat)
        {
            if (COProficiencyIncrementOverrides.TryGetValue(proficiencyStat, out int value))
                return value;
            return COIncrementStep;
        }

        /// <summary>
        /// Set the increment step for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyIncrement(string proficiencyStat, int value)
        {
            COProficiencyIncrementOverrides[proficiencyStat] = value;
        }

        /// <summary>
        /// Set the max bonus for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyMax(string proficiencyStat, float value)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: COBowsProficiencyMax = value; break;
                case CO_CROSSBOWS_PROFICIENCY: COCrossbowsProficiencyMax = value; break;
                case CO_FIREARMS_PROFICIENCY: COFirearmsProficiencyMax = value; break;
                case CO_SLINGS_PROFICIENCY: COSlingsProficiencyMax = value; break;
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: COOneHandedSwordsProficiencyMax = value; break;
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: COTwoHandedSwordsProficiencyMax = value; break;
                case CO_SPEARS_PROFICIENCY: COSpearsProficiencyMax = value; break;
                case CO_JAVELINS_PROFICIENCY: COJavelinsProficiencyMax = value; break;
                case CO_MACES_PROFICIENCY: COMacesProficiencyMax = value; break;
                case CO_CLUBS_PROFICIENCY: COClubsProficiencyMax = value; break;
                case CO_HALBERDS_PROFICIENCY: COHalberdsProficiencyMax = value; break;
                case CO_POLEAXE_PROFICIENCY: COPoleaxeProficiencyMax = value; break;
                case CO_AXES_PROFICIENCY: COAxesProficiencyMax = value; break;
                case CO_QUARTERSTAFF_PROFICIENCY: COQuarterstaffProficiencyMax = value; break;
                case CO_STEADY_AIM: COSteadyAimMax = value; break;
            }
        }
    }
}
