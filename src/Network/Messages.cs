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
    /// Server to client: the full progression report shown on the handbook
    /// page "Seraph Leveling: My Progress". Sent on join, on every level-up,
    /// whenever the client asks for it (page open, and once a second while
    /// it stays open), and every 15 s when it changed.
    /// </summary>
    [ProtoContract]
    public class ProgressReportMessage
    {
        [ProtoMember(1)]
        public string Report { get; set; }
    }

    /// <summary>
    /// Client to server: the handbook progress page wants the current report
    /// now. Force = send it even if nothing changed since the last push.
    /// </summary>
    [ProtoContract]
    public class ProgressReportRequestMessage
    {
        [ProtoMember(1)]
        public bool Force { get; set; }
    }

    /// <summary>
    /// Network message sent from server to client to play a level-up sound.
    /// </summary>
    [ProtoContract]
    public class LevelUpSoundMessage
    {
        [ProtoMember(1)]
        public string SoundName { get; set; }

        [ProtoMember(2)]
        public float Volume { get; set; }

        /// <summary>
        /// True when this packet originates from /trait testsound. The client logs and prints a chat
        /// confirmation showing the volume it actually received, so we can verify the network path.
        /// </summary>
        [ProtoMember(3)]
        public bool IsTest { get; set; }
    }
}
