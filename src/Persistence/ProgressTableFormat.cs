using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace SeraphLeveling
{
    /// <summary>
    /// How one save-format version of a progress table reads a row (everything
    /// after the player uid).
    /// </summary>
    public sealed class TableRowFormat<T> where T : class
    {
        public readonly Func<BinaryReader, byte, T> Read;

        /// <summary>Logged (Notification) before rows of this version are read.</summary>
        public readonly string Notice;

        /// <summary>
        /// Run after the rows are read: this is an old format, so the table's
        /// save flag is set and the next world save writes it in the current one
        /// (even when no rows were read).
        /// </summary>
        public readonly Action MarkRewrite;

        public TableRowFormat(Func<BinaryReader, byte, T> reader, string notice = null, Action markRewrite = null)
        {
            Read = reader;
            Notice = notice;
            MarkRewrite = markRewrite;
        }
    }

    /// <summary>
    /// The loaders were written at different times and handle the envelope in
    /// two slightly different ways; each table keeps its own, including its log
    /// wording. (A third style, LoadTableRangeChecked, has its own method.)
    /// </summary>
    public enum TableLoadStyle
    {
        /// <summary>Mining, melee, ranged, walking, hunger, armour: returns at once
        /// without a server API; checks the magic before reading the version;
        /// says "... data found in world save" and "... progress data format".</summary>
        Core,

        /// <summary>Furtive, precise, Combat Overhaul: reads the version together
        /// with the magic; says "... data found" and "... progress magic bytes".</summary>
        Middle,
    }

    // Save format of every progress table: three magic bytes, a version byte,
    // the row count (Int32), then per row the player uid (string) and the
    // table's own fields. The table-specific part (what one row holds, per
    // format version) lives with each table in the Persistence files.
    public partial class SeraphLevelingModSystem
    {
        /// <summary>Write one progress table into the save game's mod data.</summary>
        private static void PersistTable<T>(ConcurrentDictionary<string, T> table, string saveKey, string name,
            byte magic1, byte magic2, byte magic3, byte version, Action<BinaryWriter, T> writeRow, string persistedDebug = null)
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                // An empty dict is persisted too. Skipping it would leave the old
                // blob in the save and resurrect wiped progress on the next load.
                try
                {
                    var snapshot = table.ToArray();
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write(magic1);
                            writer.Write(magic2);
                            writer.Write(magic3);
                            writer.Write(version);

                            writer.Write(snapshot.Length);
                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                writeRow(writer, playerKvp.Value);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(saveKey, data);
                    if (persistedDebug != null) ServerApi.Logger.Debug(string.Format(persistedDebug, snapshot.Length));
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist {name} progress: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load a progress table whose versions each have their own row format
        /// (Core and Middle styles). An unknown version loads nothing.
        /// </summary>
        private static void LoadTable<T>(ConcurrentDictionary<string, T> table, string saveKey, string name,
            byte magic1, byte magic2, byte magic3, TableLoadStyle style, Dictionary<int, TableRowFormat<T>> versions,
            string corruptLabel = null, string unknownVersionMessage = null) where T : class
        {
            if (style == TableLoadStyle.Core && ServerApi == null) return;

            table.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(saveKey);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug(style == TableLoadStyle.Core
                        ? $"[SeraphLeveling] No {name} progress data found in world save"
                        : $"[SeraphLeveling] No {name} progress data found");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();
                        byte version = style == TableLoadStyle.Middle ? reader.ReadByte() : (byte)0;

                        if (b1 != magic1 || b2 != magic2 || b3 != magic3)
                        {
                            ServerApi.Logger.Warning(style == TableLoadStyle.Core
                                ? $"[SeraphLeveling] Invalid {name} progress data format"
                                : $"[SeraphLeveling] Invalid {name} progress magic bytes");
                            return;
                        }

                        if (style == TableLoadStyle.Core) version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();

                        if (!versions.TryGetValue(version, out var format))
                        {
                            ServerApi.Logger.Warning(unknownVersionMessage != null
                                ? string.Format(unknownVersionMessage, version)
                                : $"[SeraphLeveling] Unknown {name} save format version {version}");
                            return;
                        }

                        if (format.Notice != null) ServerApi.Logger.Notification(format.Notice);
                        ReadTableRows(table, reader, playerCount, version, format.Read, corruptLabel ?? name);
                        format.MarkRewrite?.Invoke();
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded {name} progress for {table.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load {name} progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Load a progress table that checks the version range before reading
        /// rows, and reads every version with one row reader that branches on
        /// the version itself.
        /// </summary>
        private static void LoadTableRangeChecked<T>(ConcurrentDictionary<string, T> table, string saveKey, string name,
            byte magic1, byte magic2, byte magic3, int maxVersion, Func<BinaryReader, byte, T> readRow,
            string corruptLabel = null) where T : class
        {
            table.Clear();
            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(saveKey);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug($"[SeraphLeveling] No {name} progress data found");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();
                        byte version = reader.ReadByte();

                        if (b1 != magic1 || b2 != magic2 || b3 != magic3)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Invalid {name} progress magic bytes");
                            return;
                        }

                        if (version < 1 || version > maxVersion)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown {name} save format version {version}; not loading");
                            return;
                        }

                        int playerCount = reader.ReadInt32();
                        ReadTableRows(table, reader, playerCount, version, readRow, corruptLabel ?? name);
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded {name} progress for {table.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load {name} progress: {ex.Message}");
            }
        }

        /// <summary>
        /// The row loop every loader shares: uid, then the row. A row that fails
        /// to read stops the load (later rows would be misaligned); rows read so
        /// far are kept.
        /// </summary>
        private static void ReadTableRows<T>(ConcurrentDictionary<string, T> table, BinaryReader reader, int playerCount,
            byte version, Func<BinaryReader, byte, T> readRow, string corruptLabel) where T : class
        {
            for (int i = 0; i < playerCount; i++)
            {
                try
                {
                    string playerUid = reader.ReadString();
                    T row = readRow(reader, version);
                    if (row != null) table[playerUid] = row;
                }
                catch (Exception innerEx)
                {
                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in {corruptLabel} data: {innerEx.Message}");
                    break;
                }
            }
        }
    }
}
