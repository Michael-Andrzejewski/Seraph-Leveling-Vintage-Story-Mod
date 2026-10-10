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
    public partial class SeraphLevelingModSystem : ModSystem
    {
        public static ICoreServerAPI ServerApi { get; private set; }
        public static SeraphLevelingModSystem Instance { get; private set; }

        // Disabled skills set for quick lookup (lowercase)
        public static HashSet<string> DisabledSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The part of a vanilla class-trait bonus that counts against the earnable
        /// cap. Normally all of it counts, so every class converges to the same
        /// endgame total. With EnableClassCapOffsets the class bonus stops counting:
        /// it stacks on top of the earned cap and classes stay ahead at endgame.
        /// </summary>
        private static int CapCounted(int vanillaBonus) => EnableClassCapOffsets ? 0 : vanillaBonus;
        private static float CapCounted(float vanillaBonus) => EnableClassCapOffsets ? 0f : vanillaBonus;

        // Dispose guard to prevent OnGameWorldSave from persisting empty dictionaries after Dispose()
        private static volatile bool isDisposed = false;

        // Network channel for sending level-up sounds to clients
        private static IServerNetworkChannel serverSoundChannel;

        /// <summary>
        /// How much the player's traits have already contributed to one stat.
        ///
        /// The game's CharacterSystem.applyTraitAttributes writes every trait attribute
        /// into the player's stats under the single stat code "trait", and EntityStats
        /// blends a category by summing all of its codes. Anything we write under our own
        /// stat code therefore lands on top of the trait's value rather than replacing it.
        /// Subtract this from the total we want the player to end up with, or the trait
        /// gets counted twice.
        ///
        /// This reads the value the game actually applied instead of assuming one from
        /// the character class, so it stays right whichever mod granted the trait, and it
        /// returns zero when no trait touches the stat. CharacterSystem is part of the
        /// survival mod, which loads before us, so its PlayerJoin handler has already run
        /// by the time ours does and the value is there to read.
        /// </summary>
        private static float TraitStatValue(EntityPlayer entity, string statCategory)
        {
            if (entity?.Stats == null || string.IsNullOrEmpty(statCategory)) return 0f;

            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Key != statCategory) continue;
                if (stat.Value?.ValuesByKey == null) return 0f;
                return stat.Value.ValuesByKey.TryGetValue("trait", out EntityStat<float> applied) ? applied.Value : 0f;
            }

            return 0f;
        }

        /// <summary>
        /// The penalty part of a trait's contribution to a stat, as a negative number, or
        /// zero if the trait helps rather than hurts.
        ///
        /// Used for the stats where our own value is an additive bonus rather than a total:
        /// steadyAim and the weapon proficiencies. A class that grants one of those as a
        /// perk should keep it and have our earned bonus stack on top, but a class that is
        /// penalised there, by Trembling Aim or Clumsy Hands, should be able to buy the
        /// penalty back off.
        /// </summary>
        private static float TraitStatPenalty(EntityPlayer entity, string statCategory)
        {
            // With CancelNegativeTraits off the CO penalty is left in place untouched.
            if (!CancelNegativeTraits) return 0f;
            return Math.Min(0f, TraitStatValue(entity, statCategory));
        }

        /// <summary>
        /// Check if a skill is disabled in the config.
        /// </summary>
        public static bool IsSkillDisabled(string skillName)
        {
            return DisabledSkills.Contains(skillName);
        }

        /// <summary>
        /// Server only. The client half is SeraphLevelingClientSystem; loading this
        /// system on the client too gave single player a second copy whose Dispose
        /// flushed and cleared the shared progress tables.
        /// </summary>
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;
            Instance = this;
            isDisposed = false;

            // Register network channel for level-up sound and experimental-feature config sync
            // Safety net for the handbook progress page: every 15 s, re-sync any
            // player whose report changed. The page itself asks for a fresh report
            // when opened and once a second while open (ProgressReportRequestMessage).
            api.Event.RegisterGameTickListener(dt =>
            {
                foreach (var sp in OnlinePlayers)
                    if (sp.Entity != null && ShouldPushUnasked(sp)) PushProgressReport(sp);
            }, 15000);
            api.Event.RegisterGameTickListener(_ => FlushProgressReports(), 100);

            serverSoundChannel = api.Network.RegisterChannel("seraphleveling")
                .RegisterMessageType<LevelUpSoundMessage>()
                .RegisterMessageType<ExperimentalFeatureConfigMessage>()
                .RegisterMessageType<ProgressReportMessage>()
                .RegisterMessageType<ProgressReportRequestMessage>()
                .SetMessageHandler<ProgressReportRequestMessage>(OnProgressReportRequest);

            // Load config file (sets defaults for new worlds)
            LoadConfigFile(api);

            // Detect Combat Overhaul mod
            DetectCombatOverhaul(api);

            // Register /trait command with subcommands
            api.ChatCommands.Create("trait")
                .WithDescription("Manage and view trait progression")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(OnTraitHelpCommand)
                .BeginSubCommand("mining")
                    .WithDescription("View your mining progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningCommand)
                .EndSubCommand()
                .BeginSubCommand("miningbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("mininglevel")
                    .WithDescription("Get or set your mining level (admin only). Usage: /trait mininglevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("miningmax")
                    .WithDescription("Get or set the max mining speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("miningincrement")
                    .WithDescription("Get or set the increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("melee")
                    .WithDescription("View your melee damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeCommand)
                .EndSubCommand()
                .BeginSubCommand("meleebase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("meleelevel")
                    .WithDescription("Get or set your melee level (admin only). Usage: /trait meleelevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("meleemax")
                    .WithDescription("Get or set the max melee damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("meleeincrement")
                    .WithDescription("Get or set the melee increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("ranged")
                    .WithDescription("View your ranged damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedbase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedlevel")
                    .WithDescription("Get or set your ranged level (admin only). Usage: /trait rangedlevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmax")
                    .WithDescription("Get or set the max ranged damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxacc")
                    .WithDescription("Get or set the max ranged accuracy bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxAccuracyCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxdist")
                    .WithDescription("Get or set the max ranged distance bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxDistanceCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedincrement")
                    .WithDescription("Get or set the ranged increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("walking")
                    .WithDescription("View your walking speed progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitWalkingCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("walkinglevel")
                    .WithDescription("Get or set your walking level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitWalkingLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingmax")
                    .WithDescription("Get or set the max walking speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingincrement")
                    .WithDescription("Get or set the walking increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("hunger")
                    .WithDescription("View your hunger rate progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerbase")
                    .WithDescription("Get or set the base seconds per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerlevel")
                    .WithDescription("Get or set your hunger level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("hungermax")
                    .WithDescription("Get or set the max hunger rate reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerincrement")
                    .WithDescription("Get or set the hunger increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("armor")
                    .WithDescription("View your armor progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorCommand)
                .EndSubCommand()
                .BeginSubCommand("armorlevel")
                    .WithDescription("Get or set your armor durability level (admin only). Usage: /trait armorlevel [level] [armorpiece]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("armorpiece"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedlevel")
                    .WithDescription("Get or set your armor walk speed penalty reduction level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorWalkSpeedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armordurabilitymax")
                    .WithDescription("Get or set the max armor durability bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDurabilityMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedmax")
                    .WithDescription("Get or set the max walk speed penalty reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorWalkSpeedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armortimebase")
                    .WithDescription("Get or set the base seconds in armor per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorTimeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armordamagebase")
                    .WithDescription("Get or set the base damage blocked per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDamageBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armorrepairbase")
                    .WithDescription("Get or set the base repairs per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorRepairBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("testwalkspeed")
                    .WithDescription("Apply a test walk speed modifier (admin only, use 0 to clear)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestWalkSpeedCommand)
                .EndSubCommand()
                // Clothier trait commands
                .BeginSubCommand("clothier")
                    .WithDescription("View your clothier progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierrequired")
                    .WithDescription("Get or set the required unique clothes to unlock sewing kit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitClothierRequiredCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierlevel")
                    .WithDescription("Get or set your clothier progress (unique clothes count) (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierLevelCommand)
                .EndSubCommand()
                // Mender trait commands
                .BeginSubCommand("mender")
                    .WithDescription("View your mender progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderCommand)
                .EndSubCommand()
                .BeginSubCommand("menderbase")
                    .WithDescription("Get or set the base repairs per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("menderlevel")
                    .WithDescription("Get or set your mender level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("mendermax")
                    .WithDescription("Get or set the max mender bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderMaxCommand)
                .EndSubCommand()
                // Pilferer trait commands
                .BeginSubCommand("pilferer")
                    .WithDescription("View your pilferer progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererbase")
                    .WithDescription("Get or set the base points per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("points"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererlevel")
                    .WithDescription("Get or set your pilferer level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("pilferermax")
                    .WithDescription("Get or set the max pilferer bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererMaxCommand)
                .EndSubCommand()
                // Resourceful trait commands
                .BeginSubCommand("resourceful")
                    .WithDescription("View your resourceful progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulbase")
                    .WithDescription("Get or set the base animals per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("animals"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefullevel")
                    .WithDescription("Get or set your resourceful level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulmax")
                    .WithDescription("Get or set the max resourceful loot bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulMaxCommand)
                .EndSubCommand()
                // Forager trait commands
                .BeginSubCommand("forager")
                    .WithDescription("View your forager progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerbase")
                    .WithDescription("Get or set the base crops per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("crops"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerlevel")
                    .WithDescription("Get or set your forager level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("foragermax")
                    .WithDescription("Get or set the max forager bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerMaxCommand)
                .EndSubCommand()
                // Furtive trait commands
                .BeginSubCommand("furtive")
                    .WithDescription("View your furtive (sneaking) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveCommand)
                .EndSubCommand()
                .BeginSubCommand("furtivelevel")
                    .WithDescription("Get or set your furtive level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveLevelCommand)
                .EndSubCommand()
                // Precise trait commands
                .BeginSubCommand("precise")
                    .WithDescription("View your precise (mechanical damage) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseCommand)
                .EndSubCommand()
                .BeginSubCommand("preciselevel")
                    .WithDescription("Get or set your precise level (admin only). Usage: /trait preciselevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseLevelCommand)
                .EndSubCommand()
                // Technical trait commands
                .BeginSubCommand("technical")
                    .WithDescription("View your technical trait progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalCommand)
                .EndSubCommand()
                .BeginSubCommand("technicalunlock")
                    .WithDescription("Manually unlock or lock technical trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalUnlockCommand)
                .EndSubCommand()
                // Hardy health trait commands
                .BeginSubCommand("hardyhealth")
                    .WithDescription("View your hardy health unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthCommand)
                .EndSubCommand()
                .BeginSubCommand("hardyhealthunlock")
                    .WithDescription("Manually unlock or lock hardy health trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthUnlockCommand)
                .EndSubCommand()
                // Bowyer trait commands
                .BeginSubCommand("bowyer")
                    .WithDescription("View your bowyer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerCommand)
                .EndSubCommand()
                .BeginSubCommand("bowyerunlock")
                    .WithDescription("Manually unlock or lock bowyer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerUnlockCommand)
                .EndSubCommand()
                // Improviser trait commands
                .BeginSubCommand("improviser")
                    .WithDescription("View your improviser unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserCommand)
                .EndSubCommand()
                .BeginSubCommand("improviserunlock")
                    .WithDescription("Manually unlock or lock improviser trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserUnlockCommand)
                .EndSubCommand()
                // Tinkerer trait commands
                .BeginSubCommand("tinkerer")
                    .WithDescription("View your tinkerer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererCommand)
                .EndSubCommand()
                .BeginSubCommand("tinkererunlock")
                    .WithDescription("Manually unlock or lock tinkerer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererUnlockCommand)
                .EndSubCommand()
                // Merciless trait commands
                .BeginSubCommand("merciless")
                    .WithDescription("View your merciless unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessCommand)
                .EndSubCommand()
                .BeginSubCommand("mercilessunlock")
                    .WithDescription("Manually unlock or lock merciless trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessUnlockCommand)
                .EndSubCommand()
                // Claustrophobic removal commands
                .BeginSubCommand("claustrophobic")
                    .WithDescription("View your claustrophobic removal progress (Hunter only)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicCommand)
                .EndSubCommand()
                .BeginSubCommand("claustrophobicunlock")
                    .WithDescription("Manually set claustrophobic removed status (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("removed"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicUnlockCommand)
                .EndSubCommand()
                // Reset all traits
                .BeginSubCommand("reset")
                    .WithDescription("Reset all trait progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResetCommand)
                .EndSubCommand()
                // Reset one named player's progression (admin only)
                .BeginSubCommand("resetplayer")
                    .WithDescription("Reset ALL trait progression to 0 for a named player. Works on offline players by their exact full name. (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Word("playername"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResetPlayerCommand)
                .EndSubCommand()
                // Reset every player on the server, online and offline (admin only)
                .BeginSubCommand("resetall")
                    .WithDescription("Reset ALL trait progression for EVERY player, online and offline. Type /trait resetall confirm to run it. (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("confirm"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResetAllCommand)
                .EndSubCommand()
                // Export full progression to a JSON file for cross-world transfer (admin only)
                .BeginSubCommand("export")
                    .WithDescription("Export your (or a named player's) full progression to a JSON file you can carry to another world. Admin only.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("playername"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitExportCommand)
                .EndSubCommand()
                // Import a progression JSON file onto a player, replacing current progress (admin only)
                .BeginSubCommand("import")
                    .WithDescription("Import a progression JSON file onto yourself (or a named player), replacing current progress. Admin only.")
                    .WithArgs(api.ChatCommands.Parsers.Word("filename"), api.ChatCommands.Parsers.OptionalWord("playername"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImportCommand)
                .EndSubCommand()
                // Reset all config values to defaults
                .BeginSubCommand("resetconfig")
                    .WithDescription("Reset all trait config values (base, increment, max) to defaults (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResetConfigCommand)
                .EndSubCommand()
                // Re-read ModConfig/SeraphLeveling.json without a restart
                .BeginSubCommand("reloadconfig")
                    .WithDescription("Re-read ModConfig/SeraphLeveling.json and apply it immediately (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitReloadConfigCommand)
                .EndSubCommand()
                // Diagnostic: show what every stat this mod touches is actually made of
                .BeginSubCommand("verify")
                    .WithDescription("Show the blended value and each contributing stat code for the stats this mod writes (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitVerifyCommand)
                .EndSubCommand()
                // Max all traits for testing
                .BeginSubCommand("maxall")
                    .WithDescription("Set all trait progression to maximum for testing (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMaxAllCommand)
                .EndSubCommand()
                // Test suite command
                .BeginSubCommand("testsuite")
                    .WithDescription("Run automated tests for trait calculations")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("category"))
                    .HandleWith(OnTraitTestSuiteCommand)
                .EndSubCommand()
                // Visual format probe: every progression at exactly 1 credit so every dynamic
                // trait line renders, but with values low enough that negative traits aren't
                // fully cancelled (so reduction-style displays also show).
                .BeginSubCommand("testsuite1")
                    .WithDescription("Set every progression skill to 1 credit to visually inspect dynamic trait formatting (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestSuite1Command)
                .EndSubCommand()
                // Combat Overhaul proficiency commands
                .BeginSubCommand("coproficiency")
                    .WithDescription("View all Combat Overhaul proficiency progression (requires CO mod)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOProficiencyCommand)
                .EndSubCommand()
                .BeginSubCommand("colevel")
                    .WithDescription("Set Combat Overhaul proficiency credits (admin only). Usage: /trait colevel &lt;proficiency&gt; &lt;credits&gt; [toolname]")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.Word("proficiency"), api.ChatCommands.Parsers.Int("credits"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .HandleWith(OnTraitCOLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("coreset")
                    .WithDescription("Reset all Combat Overhaul progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOResetCommand)
                .EndSubCommand()
                .BeginSubCommand("comaxall")
                    .WithDescription("Set all Combat Overhaul proficiencies to max for testing (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOMaxAllCommand)
                .EndSubCommand()
                // Per-proficiency CO commands
                .BeginSubCommand("bows")
                    .WithDescription("View or configure Bows proficiency. Usage: /trait bows [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_BOWS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("crossbows")
                    .WithDescription("View or configure Crossbows proficiency. Usage: /trait crossbows [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_CROSSBOWS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("firearms")
                    .WithDescription("View or configure Firearms proficiency. Usage: /trait firearms [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_FIREARMS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("slings")
                    .WithDescription("View or configure Slings proficiency. Usage: /trait slings [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_SLINGS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("onehandedswords")
                    .WithDescription("View or configure One-Handed Swords proficiency. Usage: /trait onehandedswords [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_ONE_HANDED_SWORDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("twohandedswords")
                    .WithDescription("View or configure Two-Handed Swords proficiency. Usage: /trait twohandedswords [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_TWO_HANDED_SWORDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("cospears")
                    .WithDescription("View or configure Spears proficiency (CO). Usage: /trait cospears [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_SPEARS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("javelins")
                    .WithDescription("View or configure Javelins proficiency. Usage: /trait javelins [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_JAVELINS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("maces")
                    .WithDescription("View or configure Maces proficiency. Usage: /trait maces [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_MACES_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("clubs")
                    .WithDescription("View or configure Clubs proficiency. Usage: /trait clubs [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_CLUBS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("halberds")
                    .WithDescription("View or configure Halberds proficiency. Usage: /trait halberds [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_HALBERDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("poleaxe")
                    .WithDescription("View or configure Poleaxe proficiency (Combat Overhaul fork). Usage: /trait poleaxe [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_POLEAXE_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("coaxes")
                    .WithDescription("View or configure Axes proficiency (CO). Usage: /trait coaxes [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_AXES_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("quarterstaff")
                    .WithDescription("View or configure Quarterstaff proficiency. Usage: /trait quarterstaff [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_QUARTERSTAFF_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("steadyaim")
                    .WithDescription("View or configure Steady Aim. Usage: /trait steadyaim [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_STEADY_AIM))
                .EndSubCommand()
                // Sleep buff and decay status commands
                .BeginSubCommand("sleepbuff")
                    .WithDescription("View your current sleep buff status")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitSleepBuffCommand)
                .EndSubCommand()
                .BeginSubCommand("decay")
                    .WithDescription("View your current skill decay status")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitDecayCommand)
                .EndSubCommand()
                .BeginSubCommand("all")
                    .WithDescription("View all trait progression at once")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitAllCommand)
                .EndSubCommand()
                .BeginSubCommand("soundvolume")
                    .WithDescription("Get or set the level-up ding volume, from 0.0 (silent) to 1.0 (full). Default 0.25. Scale is exponential, so 0.5 is close to 1.0 (admin)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("volume"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitSoundVolumeCommand)
                .EndSubCommand()
                .BeginSubCommand("testsound")
                    .WithDescription("Play the level-up ding once at a specified volume (0.0-1.0) for testing. Defaults to the current config volume (admin)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("volume"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestSoundCommand)
                .EndSubCommand()
                .BeginSubCommand("tempresist")
                    .WithDescription("View your Temporal Resistance progression (slower temporal drain; experimental, needs TemporalResistanceEnabled)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTempResistCommand)
                .EndSubCommand()
                .BeginSubCommand("temprecharge")
                    .WithDescription("View your Temporal Recharge progression (faster stability recovery; experimental, needs TemporalRechargeEnabled)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTempRechargeCommand)
                .EndSubCommand()
                .BeginSubCommand("tempstability")
                    .WithDescription("Get or set your temporal stability (0.0-1.0) for testing (admin)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("value"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTempStabilityCommand)
                .EndSubCommand()
                .BeginSubCommand("tempresistlevel")
                    .WithDescription("Set your Temporal Resistance level (admin)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitSetTempResistCommand)
                .EndSubCommand()
                .BeginSubCommand("temprechargelevel")
                    .WithDescription("Set your Temporal Recharge level (admin)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitSetTempRechargeCommand)
                .EndSubCommand()
                .BeginSubCommand("setplayer")
                    .WithDescription("Set a trait level for another player, online or offline (offline players match by exact full name). Usage: /trait setplayer &lt;playername&gt; &lt;trait&gt; &lt;level&gt; [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.Word("playername"), api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.Int("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitSetPlayerCommand)
                .EndSubCommand();

            // Hook into block breaking for mining progression
            api.Event.DidBreakBlock += OnBlockBroken;

            // Apply Harmony patches for melee damage tracking
            ApplyServerHarmonyPatches(api);

            // Hook into player join to apply saved bonuses
            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerJoin += p => { DropInventories(p?.PlayerUID); RefreshOnlinePlayers(); };
            api.Event.PlayerNowPlaying += _ => RefreshOnlinePlayers();
            api.Event.PlayerDisconnect += leaving => RefreshOnlinePlayers(leaving);

            // Hook into world save event to persist progress
            api.Event.GameWorldSave += OnGameWorldSave;

            // Load config and progress data after save game is loaded
            api.Event.SaveGameLoaded += LoadConfig;
            api.Event.SaveGameLoaded += LoadMiningProgress;
            api.Event.SaveGameLoaded += LoadMeleeProgress;
            api.Event.SaveGameLoaded += LoadRangedProgress;
            api.Event.SaveGameLoaded += LoadWalkingProgress;
            api.Event.SaveGameLoaded += LoadHungerProgress;
            api.Event.SaveGameLoaded += LoadArmorProgress;
            api.Event.SaveGameLoaded += LoadClothierProgress;
            api.Event.SaveGameLoaded += LoadMenderProgress;
            api.Event.SaveGameLoaded += LoadPilfererProgress;
            api.Event.SaveGameLoaded += LoadResourcefulProgress;
            api.Event.SaveGameLoaded += LoadForagerProgress;
            api.Event.SaveGameLoaded += LoadFurtiveProgress;
            api.Event.SaveGameLoaded += LoadPreciseProgress;
            api.Event.SaveGameLoaded += LoadTechnicalProgress;
            api.Event.SaveGameLoaded += LoadHardyHealthProgress;
            api.Event.SaveGameLoaded += LoadBowyerProgress;
            api.Event.SaveGameLoaded += LoadImproviserProgress;
            api.Event.SaveGameLoaded += LoadTinkererProgress;
            api.Event.SaveGameLoaded += LoadMercilessProgress;
            api.Event.SaveGameLoaded += LoadClaustrophobicRemovalProgress;
            api.Event.SaveGameLoaded += LoadCOProgress;
            api.Event.SaveGameLoaded += LoadSleepBuffData;

            // Register game tick listener for walking distance tracking (every 500ms)
            RegisterSafeTickListener(api, OnWalkingTick, 500, "the walking tick");

            // Experimental temporal traits: registers their save/load, world-save, and 1s leveling tick
            InitTemporalTraits(api);

            // Register game tick listener for hunger tracking (every 1000ms / 1 second)
            RegisterSafeTickListener(api, OnHungerTick, 1000, "the hunger tick");

            // Register game tick listener for armor time tracking (every 1000ms / 1 second)
            RegisterSafeTickListener(api, OnArmorTick, 1000, "the armour tick");

            // Register game tick listener for clothing tracking (every 1000ms / 1 second)
            RegisterSafeTickListener(api, OnClothingTick, 1000, "the clothing tick");

            // Register game tick listener for Mender repair tracking (every 500ms for responsive detection)
            RegisterSafeTickListener(api, OnMenderRepairTick, 500, "the mender repair tick");

            // Register game tick listener for sneaking distance tracking (every 500ms for Furtive)
            RegisterSafeTickListener(api, OnSneakingTick, 500, "the sneaking tick");

            // Register decay tick (every 10 seconds, checks for daily decay while online)
            RegisterSafeTickListener(api, OnDecayTick, 10000, "the decay tick");

            RestartAutoSaveTimer(api);

            // Hook into player disconnect to clean up position tracking and save data
            api.Event.PlayerDisconnect += OnPlayerDisconnect;

            api.Logger.Notification("[SeraphLeveling] Mod loaded");
        }

        /// <summary>
        /// Called when a player disconnects. Cleans up their position, armor tracking, and cached data.
        /// Also triggers a save of all progress data to prevent data loss.
        /// </summary>
        /// <summary>
        /// Per-UID listener we registered on the entity's "characterClass" watched
        /// attribute, kept so it can be unregistered on disconnect and replaced on rejoin.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Action> classChangeListeners = new ConcurrentDictionary<string, Action>();

        /// <summary>UIDs with a class-change reapply already scheduled, so bursts collapse into one run.</summary>
        private static readonly ConcurrentDictionary<string, bool> pendingClassChangeReapply = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// Registers (or replaces) the "characterClass" modified listener for one player.
        /// Idempotent: any previously registered listener is unregistered first, so the
        /// OnPlayerJoin call inside the class-change reapply cannot stack duplicates.
        /// </summary>
        private void RegisterClassChangeListener(IServerPlayer player)
        {
            if (player?.Entity == null) return;
            string uid = player.PlayerUID;

            if (classChangeListeners.TryRemove(uid, out var oldListener))
            {
                try { player.Entity.WatchedAttributes.UnregisterListener(oldListener); } catch { }
            }

            Action listener = () => OnCharacterClassChanged(player);
            classChangeListeners[uid] = listener;
            player.Entity.WatchedAttributes.RegisterModifiedListener("characterClass", listener);
        }

        /// <summary>
        /// Fires when the player's "characterClass" watched attribute changes. The vanilla
        /// CharacterSystem sets that attribute first and applies the class's traits, stats
        /// and gear afterwards, so defer our reapply until that has finished, then rerun
        /// the full join path: repopulate the vanilla-traits cache and reapply every bonus.
        /// </summary>
        private void OnCharacterClassChanged(IServerPlayer player)
        {
            if (ServerApi == null || isDisposed || player?.Entity == null) return;

            string uid = player.PlayerUID;
            if (!pendingClassChangeReapply.TryAdd(uid, true)) return;

            ServerApi.Event.RegisterCallback(dt =>
            {
                pendingClassChangeReapply.TryRemove(uid, out _);
                if (isDisposed || player?.Entity == null) return;
                if (player.ConnectionState == EnumClientState.Offline) return;

                try
                {
                    string newClass = player.Entity.WatchedAttributes.GetString("characterClass", "");
                    ServerApi.Logger.Notification($"[SeraphLeveling] Class change detected for {player.PlayerName} (now '{newClass}'); reapplying trait bonuses");
                    OnPlayerJoin(player);
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to reapply bonuses after class change for {player.PlayerName}: {ex.Message}");
                }
            }, 500);
        }

        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;
            string playerUid = byPlayer.PlayerUID;
            lastPlayerPositions.TryRemove(playerUid, out _);
            lastSneakingPositions.TryRemove(playerUid, out _);
            playerEquippedArmor.TryRemove(playerUid, out _);
            VanillaTraitsCache.TryRemove(playerUid, out _);
            LastDecayCheckDay.TryRemove(playerUid, out _);
            SleepMountHours.TryRemove(playerUid, out _);
            pendingClassChangeReapply.TryRemove(playerUid, out _);
            playerEquippedClothing.TryRemove(playerUid, out _);
            LastSleepBuffApplyTick.TryRemove(playerUid, out _);
            LastSentProgressReport.TryRemove(playerUid, out _);
            ProgressReportDirty.TryRemove(playerUid, out _);
            ProgressPageLastRequestMs.TryRemove(playerUid, out _);
            TrackedItemDurabilities.TryRemove(playerUid, out _);
            if (classChangeListeners.TryRemove(playerUid, out var classListener))
            {
                try { byPlayer.Entity?.WatchedAttributes.UnregisterListener(classListener); } catch { }
            }

            // No save here. SaveGame.StoreData only updates the game's in-memory
            // ModData table; the disk write happens in the game's own world save
            // (autosave and shutdown), which calls OnGameWorldSave first. Saving
            // here re-serialized every player of every skill on each logout and
            // protected nothing.
        }

        /// <summary>
        /// Called when a player joins. Applies their saved bonuses (mining, melee, ranged, walking, and hunger).
        /// </summary>
        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            // Hand the handbook progress page its first report a few seconds after
            // join, once the client has its channel handlers in place.
            ServerApi?.Event.RegisterCallback(_ => PushProgressReport(byPlayer, force: true), 4000);

            string playerUid = byPlayer.PlayerUID;

            // Populate vanilla traits cache first (before applying any bonuses)
            PopulateVanillaTraitsCache(byPlayer);

            // Watch for a mid-session class change (.charsel with allowcharselonce).
            // Without this the vanilla-traits cache built above goes stale and every
            // stat we write afterwards is computed against the old class, so the new
            // class's negative traits only take hold after a death or relog.
            RegisterClassChangeListener(byPlayer);

            // Sync the experimental feature settings to this client. Their bow/melee
            // patches also run client-side for prediction, and a dedicated server's
            // clients never see the server's config file.
            try { serverSoundChannel?.SendPacket(BuildFeatureConfigMessage(), byPlayer); } catch { }

            // Initialize decay check day for this player (online-only decay)
            if (EnableSkillDecay)
            {
                double currentDay = ServerApi.World.Calendar.TotalDays;
                LastDecayCheckDay[playerUid] = currentDay;
            }

            // Apply mining bonus (Stats always applied, WatchedAttributes only sync if changed)
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningCredits = miningProg.TotalCredits;
            ApplyMiningBonus(byPlayer, miningCredits);
            if (miningCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mining bonus {miningCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply melee bonus (Stats always applied, WatchedAttributes only sync if changed)
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            int meleeCredits = meleeProg.TotalCredits;
            ApplyMeleeBonusStatic(byPlayer, meleeCredits);
            if (meleeCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied melee bonus {meleeCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply ranged bonus (Stats always applied, WatchedAttributes only sync if changed)
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            int rangedCredits = rangedProg.TotalCredits;
            ApplyRangedBonusStatic(byPlayer, rangedCredits);
            if (rangedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied ranged bonus {rangedCredits} credits to player {byPlayer.PlayerName}");
            }

            // Apply walking bonus (Stats always applied, WatchedAttributes only sync if changed)
            var walkingProg = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
            {
                CurrentIncrementSize = BaseBlocksWalkedPerIncrement
            });
            int walkingCredits = walkingProg.TotalCredits;
            ApplyWalkingBonusStatic(byPlayer, walkingCredits);
            if (walkingCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied walking bonus {walkingCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply hunger bonus (Stats always applied, WatchedAttributes only sync if changed)
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });
            int hungerCredits = hungerProg.TotalCredits;
            ApplyHungerBonusStatic(byPlayer, hungerCredits);
            if (hungerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hunger bonus -{hungerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply armor bonuses (Stats always applied, WatchedAttributes only sync if changed)
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            ApplyArmorBonusesStatic(byPlayer, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (armorProg.TotalDurabilityCredits > 0 || armorProg.TotalWalkSpeedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied armor bonuses: +{armorProg.TotalDurabilityCredits}% durability, -{armorProg.TotalWalkSpeedCredits}% walk speed penalty to player {byPlayer.PlayerName}");
            }

            // Apply clothier bonus
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            ApplyClothierBonusStatic(byPlayer, clothierProg);
            if (clothierProg.SewingKitUnlocked)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied clothier unlock to player {byPlayer.PlayerName}");
            }

            // Apply mender bonus
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData
            {
                CurrentIncrementSize = BaseMenderRepairsPerIncrement
            });
            int menderCredits = menderProg.TotalCredits;
            ApplyMenderBonusStatic(byPlayer, menderCredits);
            if (menderCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mender bonus +{menderCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply pilferer bonus
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData
            {
                CurrentIncrementSize = BasePilfererPointsPerIncrement
            });
            int pilfererCredits = pilfererProg.TotalCredits;
            ApplyPilfererBonusStatic(byPlayer, pilfererCredits);
            if (pilfererCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied pilferer bonus +{pilfererCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply resourceful bonus
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData
            {
                CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement
            });
            int resourcefulCredits = resourcefulProg.TotalCredits;
            ApplyResourcefulBonusStatic(byPlayer, resourcefulCredits);
            if (resourcefulCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied resourceful bonus +{resourcefulCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply forager bonus
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData
            {
                CurrentIncrementSize = BaseForagerCropsPerIncrement
            });
            int foragerCredits = foragerProg.TotalCredits;
            ApplyForagerBonusStatic(byPlayer, foragerCredits);
            if (foragerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied forager bonus +{foragerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply furtive bonus
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
            {
                CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
            });
            int furtiveCredits = furtiveProg.TotalCredits;
            ApplyFurtiveBonusStatic(byPlayer, furtiveCredits);
            if (furtiveCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied furtive bonus -{furtiveCredits}% detection to player {byPlayer.PlayerName}");
            }

            // Apply precise bonus
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            int preciseCredits = preciseProg.TotalCredits;
            ApplyPreciseBonusStatic(byPlayer, preciseCredits);
            if (preciseCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied precise bonus +{preciseCredits}% mechanical damage to player {byPlayer.PlayerName}");
            }

            // Apply technical unlock
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (technicalProg.IsUnlocked)
            {
                ApplyTechnicalBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied technical unlock to player {byPlayer.PlayerName}");
            }

            // Apply hardy health unlock
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            if (hardyHealthProg.IsUnlocked)
            {
                ApplyHardyHealthBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hardy health +{HardyHealthBonus} HP to player {byPlayer.PlayerName}");
            }

            // Apply bowyer unlock
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            if (bowyerProg.IsUnlocked)
            {
                ApplyBowyerBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied bowyer unlock to player {byPlayer.PlayerName}");
            }

            // Apply improviser unlock
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            if (improviserProg.IsUnlocked)
            {
                ApplyImproviserBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied improviser unlock to player {byPlayer.PlayerName}");
            }

            // Apply tinkerer unlock
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            if (tinkererProg.IsUnlocked)
            {
                ApplyTinkererBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied tinkerer unlock to player {byPlayer.PlayerName}");
            }

            // Apply merciless unlock
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            if (mercilessProg.IsUnlocked)
            {
                ApplyMercilessBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied merciless unlock to player {byPlayer.PlayerName}");
            }

            // Apply claustrophobic removal
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            if (claustrophobicProg.IsRemoved)
            {
                ApplyClaustrophobicRemovalStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied claustrophobic removal to player {byPlayer.PlayerName}");
            }

            // Apply Combat Overhaul proficiency bonuses (if CO is loaded)
            if (IsCOCompatEnabled)
            {
                ApplyAllCOBonuses(byPlayer);
                if (COProgress.TryGetValue(playerUid, out var coProgress))
                {
                    int totalCOCredits = coProgress.SteadyAimCredits;
                    foreach (var prof in coProgress.Proficiencies)
                    {
                        totalCOCredits += prof.Value.TotalCredits;
                    }
                    if (totalCOCredits > 0)
                    {
                        ServerApi.Logger.Debug($"[SeraphLeveling] Applied CO bonuses ({totalCOCredits} total credits) to player {byPlayer.PlayerName}");
                    }
                }
            }

            // Initialize equipped armor tracking for this player
            InitializePlayerArmorTracking(byPlayer);
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// Sends a level-up notification to the player (chat message and/or sound),
        /// respecting the EnableLevelUpMessages and EnableLevelUpSound config options.
        /// </summary>
        private static void NotifyLevelUp(IServerPlayer player, string message)
        {
            if (ShouldPushUnasked(player)) PushProgressReport(player);

            if (EnableLevelUpMessages)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
            }

            if (EnableLevelUpSound && serverSoundChannel != null)
            {
                try
                {
                    serverSoundChannel.SendPacket(new LevelUpSoundMessage { SoundName = LevelUpSoundName, Volume = LevelUpSoundVolume }, player);
                }
                catch (Exception ex)
                {
                    ServerApi?.Logger.Warning($"[SeraphLeveling] Failed to send level-up sound to {player.PlayerName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adds or removes a trait from the player's extraTraits array.
        /// </summary>
        private void UpdateExtraTrait(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            // Get current extra traits
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                // Add the trait
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
                ServerApi.Logger.Debug($"[SeraphLeveling] Added trait {traitCode} to player");
            }
            else if (!shouldHave && hasTrait)
            {
                // Remove the trait
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
                ServerApi.Logger.Debug($"[SeraphLeveling] Removed trait {traitCode} from player");
            }
        }

        /// <summary>
        /// Static version of UpdateExtraTrait for use from Harmony patches.
        /// </summary>
        private static void UpdateExtraTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
        }

        public override void Dispose()
        {
            // Persist any pending progress before shutdown
            if (ServerApi != null)
            {
                if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
                {
                    PersistMiningProgress();
                }
                if (pendingMeleeProgressSave || !MeleeProgress.IsEmpty)
                {
                    PersistMeleeProgress();
                }
                if (pendingRangedProgressSave || !RangedProgress.IsEmpty)
                {
                    PersistRangedProgress();
                }
                if (pendingWalkingProgressSave || !WalkingProgress.IsEmpty)
                {
                    PersistWalkingProgress();
                }
                if (pendingHungerProgressSave || !HungerProgress.IsEmpty)
                {
                    PersistHungerProgress();
                }
                if (pendingArmorProgressSave || !ArmorProgress.IsEmpty)
                {
                    PersistArmorProgress();
                }
                if (pendingClothierProgressSave || !ClothierProgress.IsEmpty)
                {
                    PersistClothierProgress();
                }
                if (pendingMenderProgressSave || !MenderProgress.IsEmpty)
                {
                    PersistMenderProgress();
                }
                if (pendingPilfererProgressSave || !PilfererProgress.IsEmpty)
                {
                    PersistPilfererProgress();
                }
                if (pendingResourcefulProgressSave || !ResourcefulProgress.IsEmpty)
                {
                    PersistResourcefulProgress();
                }
                if (pendingForagerProgressSave || !ForagerProgress.IsEmpty)
                {
                    PersistForagerProgress();
                }
                if (pendingFurtiveProgressSave || !FurtiveProgress.IsEmpty)
                {
                    PersistFurtiveProgress();
                }
                if (pendingPreciseProgressSave || !PreciseProgress.IsEmpty)
                {
                    PersistPreciseProgress();
                }
                if (pendingTechnicalProgressSave || !TechnicalProgress.IsEmpty)
                {
                    PersistTechnicalProgress();
                }
                if (pendingHardyHealthProgressSave || !HardyHealthProgress.IsEmpty)
                {
                    PersistHardyHealthProgress();
                }
                if (pendingBowyerProgressSave || !BowyerProgress.IsEmpty)
                {
                    PersistBowyerProgress();
                }
                if (pendingImproviserProgressSave || !ImproviserProgress.IsEmpty)
                {
                    PersistImproviserProgress();
                }
                if (pendingTinkererProgressSave || !TinkererProgress.IsEmpty)
                {
                    PersistTinkererProgress();
                }
                if (pendingMercilessProgressSave || !MercilessProgress.IsEmpty)
                {
                    PersistMercilessProgress();
                }
                if (pendingClaustrophobicRemovalProgressSave || !ClaustrophobicRemovalProgress.IsEmpty)
                {
                    PersistClaustrophobicRemovalProgress();
                }

                // These two are newer than the rest and were never added to the shutdown
                // flush. OnGameWorldSave persists them, so they only went missing when
                // Dispose ran without a world save first.
                if (pendingCOProgressSave || !COProgress.IsEmpty)
                {
                    PersistCOProgress();
                }

                if (pendingSleepBuffSave || !SleepBuffExpiration.IsEmpty)
                {
                    PersistSleepBuffData();
                }

                if (pendingTemporalResistanceSave || !TemporalResistanceProgress.IsEmpty)
                {
                    PersistTemporalResistanceProgress();
                }

                if (pendingTemporalRechargeSave || !TemporalRechargeProgress.IsEmpty)
                {
                    PersistTemporalRechargeProgress();
                }

                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMeleeProgress;
                ServerApi.Event.SaveGameLoaded -= LoadRangedProgress;
                ServerApi.Event.SaveGameLoaded -= LoadWalkingProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHungerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadArmorProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClothierProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMenderProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPilfererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadResourcefulProgress;
                ServerApi.Event.SaveGameLoaded -= LoadForagerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadFurtiveProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPreciseProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTechnicalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHardyHealthProgress;
                ServerApi.Event.SaveGameLoaded -= LoadBowyerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadImproviserProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTinkererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMercilessProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClaustrophobicRemovalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadCOProgress;
                ServerApi.Event.SaveGameLoaded -= LoadSleepBuffData;
                ServerApi.Event.SaveGameLoaded -= LoadTemporalResistanceProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTemporalRechargeProgress;
                ServerApi.Event.GameWorldSave -= SaveTemporalProgressOnWorldSave;
            }

            // Mark as disposed BEFORE clearing dictionaries to prevent OnGameWorldSave
            // from persisting empty data if it fires during shutdown after Clear()
            isDisposed = true;

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("seraphleveling.server");

            // The single-process guards must reset with the unpatch, or the next
            // world load in the same process would skip re-patching.
            BowDrawSpeedPatches.PatchedInProcess = false;
            SeraphLevelingModSystem.ResetCharacterSystemCache();
            MeleeAttackSpeedPatches.ResetPatchGuard();

            MiningProgress.Clear();
            MeleeProgress.Clear();
            RangedProgress.Clear();
            WalkingProgress.Clear();
            HungerProgress.Clear();
            ArmorProgress.Clear();
            ClothierProgress.Clear();
            MenderProgress.Clear();
            PilfererProgress.Clear();
            ResourcefulProgress.Clear();
            ForagerProgress.Clear();
            FurtiveProgress.Clear();
            PreciseProgress.Clear();
            TechnicalProgress.Clear();
            HardyHealthProgress.Clear();
            BowyerProgress.Clear();
            ImproviserProgress.Clear();
            TinkererProgress.Clear();
            MercilessProgress.Clear();
            ClaustrophobicRemovalProgress.Clear();
            COProgress.Clear();
            TemporalResistanceProgress.Clear();
            TemporalRechargeProgress.Clear();
            lastPlayerPositions.Clear();
            lastSneakingPositions.Clear();
            VanillaTraitsCache.Clear();
            LastDecayCheckDay.Clear();
            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();
            LastSleepBuffApplyTick.Clear();
            playerEquippedArmor.Clear();
            playerEquippedClothing.Clear();
            TrackedItemDurabilities.Clear();
            ResetPerformanceCaches();   // Core/Caches.cs: the old world's players, items, blocks and creatures
            SleepMountHours.Clear();
            classChangeListeners.Clear();
            pendingClassChangeReapply.Clear();
            pendingSleepBuffSave = false;
            pendingMiningProgressSave = false;
            pendingMeleeProgressSave = false;
            pendingRangedProgressSave = false;
            pendingWalkingProgressSave = false;
            pendingHungerProgressSave = false;
            pendingArmorProgressSave = false;
            pendingClothierProgressSave = false;
            pendingMenderProgressSave = false;
            pendingPilfererProgressSave = false;
            pendingResourcefulProgressSave = false;
            pendingForagerProgressSave = false;
            pendingFurtiveProgressSave = false;
            pendingPreciseProgressSave = false;
            pendingTechnicalProgressSave = false;
            pendingHardyHealthProgressSave = false;
            pendingBowyerProgressSave = false;
            pendingImproviserProgressSave = false;
            pendingTinkererProgressSave = false;
            pendingMercilessProgressSave = false;
            pendingClaustrophobicRemovalProgressSave = false;
            pendingCOProgressSave = false;
            pendingTemporalResistanceSave = false;
            pendingTemporalRechargeSave = false;
            if (pendingConfigSave)
            {
                try { SaveConfigFile(); } catch (Exception ex) { ServerApi?.Logger?.Warning("[SeraphLeveling] Could not flush the config on shutdown: " + ex.Message); }
            }
            pendingConfigSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Re-apply all bonuses for a player after decay has reduced credits.
        /// </summary>
        private void ReapplyAllBonuses(IServerPlayer player)
        {
            SyncTemporalTraitsToClient(player);
            string playerUid = player.PlayerUID;

            if (MiningProgress.TryGetValue(playerUid, out var miningProg))
                ApplyMiningBonus(player, miningProg.TotalCredits);
            if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
                ApplyMeleeBonusStatic(player, meleeProg.TotalCredits);
            if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
                ApplyRangedBonusStatic(player, rangedProg.TotalCredits);
            if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
                ApplyWalkingBonusStatic(player, walkingProg.TotalCredits);
            if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
                ApplyHungerBonusStatic(player, hungerProg.TotalCredits);
            if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
                ApplyArmorBonusesStatic(player, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (MenderProgress.TryGetValue(playerUid, out var menderProg))
                ApplyMenderBonusStatic(player, menderProg.TotalCredits);
            if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
                ApplyPilfererBonusStatic(player, pilfererProg.TotalCredits);
            if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
                ApplyResourcefulBonusStatic(player, resourcefulProg.TotalCredits);
            if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
                ApplyForagerBonusStatic(player, foragerProg.TotalCredits);
            if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
                ApplyFurtiveBonusStatic(player, furtiveProg.TotalCredits);
            if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
                ApplyPreciseBonusStatic(player, preciseProg.TotalCredits);
            if (IsCOCompatEnabled && COProgress.TryGetValue(playerUid, out var coProg))
                ApplyAllCOBonuses(player);
        }

        /// <summary>
        /// Update the LastActivityDay for a skill when it's used.
        /// </summary>
        private static void UpdateSkillActivityDay(string playerUid, string skillType)
        {
            if (!EnableSkillDecay) return;
            if (ServerApi == null) return;

            double currentDay = ServerApi.World.Calendar.TotalDays;

            switch (skillType)
            {
                case "mining":
                    if (MiningProgress.TryGetValue(playerUid, out var miningProg))
                        miningProg.LastActivityDay = currentDay;
                    break;
                case "melee":
                    if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
                        meleeProg.LastActivityDay = currentDay;
                    break;
                case "ranged":
                    if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
                        rangedProg.LastActivityDay = currentDay;
                    break;
                case "walking":
                    if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
                        walkingProg.LastActivityDay = currentDay;
                    break;
                case "hunger":
                    if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
                        hungerProg.LastActivityDay = currentDay;
                    break;
                case "armor":
                    if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
                        armorProg.LastActivityDay = currentDay;
                    break;
                case "mender":
                    if (MenderProgress.TryGetValue(playerUid, out var menderProg))
                        menderProg.LastActivityDay = currentDay;
                    break;
                case "pilferer":
                    if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
                        pilfererProg.LastActivityDay = currentDay;
                    break;
                case "resourceful":
                    if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
                        resourcefulProg.LastActivityDay = currentDay;
                    break;
                case "forager":
                    if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
                        foragerProg.LastActivityDay = currentDay;
                    break;
                case "furtive":
                    if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
                        furtiveProg.LastActivityDay = currentDay;
                    break;
                case "precise":
                    if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
                        preciseProg.LastActivityDay = currentDay;
                    break;
                case "coproficiency":
                    if (COProgress.TryGetValue(playerUid, out var coProg))
                        coProg.LastActivityDay = currentDay;
                    break;
            }
        }

        // =========================================================================
        // XP MULTIPLIER (global rate, plus the sleep buff when active)
        // =========================================================================

        /// <summary>
        /// Get the current XP multiplier for a player.
        /// Returns the sleep buff multiplier if active, otherwise 1.0.
        /// </summary>
        public static float GetXPMultiplier(string playerUid)
        {
            // The global rate applies to everything; the sleep buff stacks on top.
            float global = GlobalXPRateMultiplier;

            if (!EnableSleepBuff) return global;
            if (string.IsNullOrEmpty(playerUid)) return global;
            if (ServerApi == null) return global;

            // Check if player has an active sleep buff
            if (!SleepBuffExpiration.TryGetValue(playerUid, out double expiration)) return global;
            if (!SleepBuffMultiplier.TryGetValue(playerUid, out float multiplier)) return global;

            double currentDay = ServerApi.World?.Calendar?.TotalDays ?? 0;

            // Check if buff has expired
            if (currentDay >= expiration)
            {
                // Remove expired buff
                SleepBuffExpiration.TryRemove(playerUid, out _);
                SleepBuffMultiplier.TryRemove(playerUid, out _);
                pendingSleepBuffSave = true;
                return global;
            }

            return global * multiplier;
        }

        /// <summary>
        /// Apply the XP multiplier to a value (for progress tracking).
        /// </summary>
        public static float ApplyXPMultiplier(string playerUid, float baseValue)
        {
            float multiplier = GetXPMultiplier(playerUid);
            return baseValue * multiplier;
        }

        /// <summary>Shared RNG for the fractional-gain dice roll below.</summary>
        private static readonly Random xpRoundingRandom = new Random();

        /// <summary>
        /// Apply the XP multiplier to an integer value (for progress tracking).
        /// The fractional part is granted probabilistically so sub-1x rates still
        /// work on 1-point gains: 1 point at 0.5x used to truncate to 0 forever,
        /// now it is 0 or 1 with the right long-run average.
        /// </summary>
        public static int ApplyXPMultiplier(string playerUid, int baseValue)
        {
            float scaled = baseValue * GetXPMultiplier(playerUid);
            int whole = (int)scaled;
            double fraction = scaled - whole;
            if (fraction > 0 && xpRoundingRandom.NextDouble() < fraction) whole++;
            return whole;
        }
    }
}
