using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WtSkin
{
    internal class HistoryEntry
    {
        public string Fingerprint;
        public string UnitId;
        public string Folder;
        public DateTime Time;
    }

    /// <summary>记录“这个涂装包上次导入到了哪些文件夹”，用于再次导入时打标记。</summary>
    internal static class ImportHistory
    {
        private static List<HistoryEntry> _items;
        private static bool _dirty;

        public static string FilePath
        {
            get { return Path.Combine(Config.InstallDir, "history.ini"); }
        }

        private static void EnsureLoaded()
        {
            if (_items != null)
            {
                return;
            }
            _items = new List<HistoryEntry>();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }
                    string[] parts = line.Split('|');
                    if (parts.Length < 4)
                    {
                        continue;
                    }
                    HistoryEntry entry = new HistoryEntry();
                    entry.Fingerprint = parts[0];
                    entry.UnitId = parts[1];
                    entry.Folder = parts[2];
                    DateTime time;
                    if (DateTime.TryParseExact(parts[3], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                                               DateTimeStyles.None, out time))
                    {
                        entry.Time = time;
                    }
                    else
                    {
                        entry.Time = DateTime.MinValue;
                    }
                    if (entry.Fingerprint.Length > 0 && entry.UnitId.Length > 0)
                    {
                        _items.Add(entry);
                    }
                }
            }
            catch
            {
            }
        }

        public static void Record(string fingerprint, string unitId, string folder)
        {
            if (string.IsNullOrEmpty(fingerprint) || string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(folder))
            {
                return;
            }
            EnsureLoaded();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_items[i].Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(_items[i].UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    _items.RemoveAt(i);
                }
            }
            HistoryEntry entry = new HistoryEntry();
            entry.Fingerprint = fingerprint;
            entry.UnitId = unitId;
            entry.Folder = folder;
            entry.Time = DateTime.Now;
            _items.Add(entry);
            while (_items.Count > 400)
            {
                _items.RemoveAt(0);
            }
            _dirty = true;
            Save();
        }

        public static HistoryEntry Lookup(string fingerprint, string unitId)
        {
            if (string.IsNullOrEmpty(fingerprint) || string.IsNullOrEmpty(unitId))
            {
                return null;
            }
            EnsureLoaded();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                HistoryEntry entry = _items[i];
                if (string.Equals(entry.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        public static List<string> UnitsFor(string fingerprint)
        {
            List<string> units = new List<string>();
            if (string.IsNullOrEmpty(fingerprint))
            {
                return units;
            }
            EnsureLoaded();
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                    !units.Contains(_items[i].UnitId))
                {
                    units.Add(_items[i].UnitId);
                }
            }
            return units;
        }

        public static DateTime LastTime(string fingerprint)
        {
            EnsureLoaded();
            DateTime last = DateTime.MinValue;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                    _items[i].Time > last)
                {
                    last = _items[i].Time;
                }
            }
            return last;
        }

        public static bool ContainsFolder(string folder)
        {
            EnsureLoaded();
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Folder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Save()
        {
            if (!_dirty || string.IsNullOrEmpty(Config.InstallDir))
            {
                return;
            }
            try
            {
                Config.EnsureDirs();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 战雷涂装管理器导入记录：fingerprint|unitId|folder|time");
                for (int i = 0; i < _items.Count; i++)
                {
                    HistoryEntry e = _items[i];
                    sb.Append(e.Fingerprint).Append('|')
                      .Append(e.UnitId).Append('|')
                      .Append(e.Folder).Append('|')
                      .Append(e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                      .AppendLine();
                }
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
                _dirty = false;
            }
            catch
            {
            }
        }
    }
}
