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
    /// Main mod system for Simple Improving Traits.
    /// Provides a progression system that improves player traits through gameplay.
    /// Currently implements mining speed progression based on blocks mined.
    /// </summary>
    /// <summary>
    /// A snapshot of one player's full progression across every system, used to
    /// transfer progress between worlds/servers via /trait export and /trait
    /// import. Each field is null when the player has no data for that system.
    /// Plain public fields/auto-properties so it round-trips cleanly through
    /// Newtonsoft JSON.
    /// </summary>
    public class PlayerProgressExport
    {
        public int FormatVersion = 1;
        public string SourcePlayerName;
        public string SourcePlayerUid;
        public double ExportedGameDay;

        public MiningProgressData Mining;
        public MeleeProgressData Melee;
        public RangedProgressData Ranged;
        public WalkingProgressData Walking;
        public HungerProgressData Hunger;
        public ArmorProgressData Armor;
        public ClothierProgressData Clothier;
        public MenderProgressData Mender;
        public PilfererProgressData Pilferer;
        public ResourcefulProgressData Resourceful;
        public ForagerProgressData Forager;
        public FurtiveProgressData Furtive;
        public PreciseProgressData Precise;
        public TechnicalProgressData Technical;
        public HardyHealthProgressData HardyHealth;
        public BowyerProgressData Bowyer;
        public ImproviserProgressData Improviser;
        public TinkererProgressData Tinkerer;
        public MercilessProgressData Merciless;
        public ClaustrophobicRemovalProgressData ClaustrophobicRemoval;
        public COPlayerProgressData CombatOverhaul;

        // Experimental temporal traits. Null in files exported before 1.20.0.
        public TemporalProgressData TemporalResistance;
        public TemporalProgressData TemporalRecharge;
    }
}
