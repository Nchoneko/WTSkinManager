using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WtSkin
{
    /// <summary>更新日志的一个版本段：版本、日期、四类变更与数据变更。</summary>
    internal class ReleaseNote
    {
        public SemVer Version;
        public string Date = "";
        public string DataVersion = "";
        public readonly List<string> Added = new List<string>();
        public readonly List<string> Fixed = new List<string>();
        public readonly List<string> Improved = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> DataChanges = new List<string>();

        public string VersionText
        {
            get { return Version == null ? "" : Version.ToDisplay(); }
        }

        public int Count
        {
            get
            {
                return Added.Count + Fixed.Count + Improved.Count + Removed.Count + DataChanges.Count;
            }
        }
    }

    /// <summary>
    /// 更新日志：解析内嵌的 updates.dat。安装完成窗口与「历史版本」界面都只读这份数据，
    /// 界面代码里不另写文案，避免两处各写一份。
    /// </summary>
    internal static class ReleaseLog
    {
        private static readonly object Sync = new object();
        private static bool _loaded;
        private static readonly List<ReleaseNote> _notes = new List<ReleaseNote>();
        private static string _error = "";

        public static string Error
        {
            get { EnsureLoaded(); return _error; }
        }

        public static List<ReleaseNote> All
        {
            get
            {
                EnsureLoaded();
                lock (Sync)
                {
                    return new List<ReleaseNote>(_notes);
                }
            }
        }

        public static ReleaseNote Latest
        {
            get
            {
                EnsureLoaded();
                lock (Sync)
                {
                    return _notes.Count > 0 ? _notes[0] : null;
                }
            }
        }

        public static ReleaseNote Find(SemVer version)
        {
            if (version == null)
            {
                return null;
            }
            EnsureLoaded();
            lock (Sync)
            {
                for (int i = 0; i < _notes.Count; i++)
                {
                    if (_notes[i].Version != null && _notes[i].Version.IsSame(version))
                    {
                        return _notes[i];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 跨版本区间 (olderExclusive, newestInclusive]，按版本倒序。
        /// olderExclusive 为空（首次安装 / 旧版本读取失败）时只给 newestInclusive 这一段。
        /// truncatedOlder 表示日志里还有更早的版本没有展开。
        /// </summary>
        public static List<ReleaseNote> Range(SemVer olderExclusive, SemVer newestInclusive, out bool truncatedOlder)
        {
            truncatedOlder = false;
            EnsureLoaded();
            List<ReleaseNote> result = new List<ReleaseNote>();
            ReleaseNote newest = Find(newestInclusive);
            if (newest == null)
            {
                newest = Latest;
            }
            if (newest == null)
            {
                return result;
            }
            if (olderExclusive == null || olderExclusive.IsUnknown)
            {
                result.Add(newest);
                return result;
            }
            lock (Sync)
            {
                for (int i = 0; i < _notes.Count; i++)
                {
                    ReleaseNote note = _notes[i];
                    if (note.Version == null)
                    {
                        continue;
                    }
                    if (note.Version.CompareTo(newest.Version) > 0)
                    {
                        continue;
                    }
                    if (note.Version.CompareTo(olderExclusive) <= 0)
                    {
                        continue;
                    }
                    result.Add(note);
                }
                if (_notes.Count > 0 && olderExclusive.CompareTo(_notes[_notes.Count - 1].Version) < 0)
                {
                    truncatedOlder = true;
                }
            }
            // 旧版本本身没有收录在日志里：也要标注「更早版本的变更未收录」。
            if (!truncatedOlder && Find(olderExclusive) == null)
            {
                truncatedOlder = true;
            }
            if (result.Count == 0)
            {
                // 降级或日志里没有区间内的版本：只展示目标版本，避免空白窗口。
                result.Add(newest);
            }
            return result;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            lock (Sync)
            {
                if (_loaded)
                {
                    return;
                }
                _loaded = true;
                try
                {
                    using (Stream stream = typeof(ReleaseLog).Assembly.GetManifestResourceStream("updates.dat"))
                    {
                        if (stream == null)
                        {
                            _error = "缺少内嵌 updates.dat";
                            return;
                        }
                        using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                        {
                            Parse(reader.ReadToEnd());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _error = "读取更新日志失败：" + ex.Message;
                }
                _notes.Sort(delegate(ReleaseNote a, ReleaseNote b)
                {
                    return b.Version.CompareTo(a.Version);
                });
            }
        }

        private static void Parse(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            ReleaseNote current = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    SemVer version;
                    current = null;
                    if (SemVer.TryParse(name, out version) && version != null)
                    {
                        current = new ReleaseNote();
                        current.Version = version;
                        _notes.Add(current);
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || current == null)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (value.Length == 0)
                {
                    continue;
                }
                switch (key)
                {
                    case "date":
                        current.Date = value;
                        break;
                    case "data":
                    case "dataversion":
                        current.DataVersion = value;
                        break;
                    case "added":
                        current.Added.Add(value);
                        break;
                    case "fixed":
                        current.Fixed.Add(value);
                        break;
                    case "improved":
                        current.Improved.Add(value);
                        break;
                    case "removed":
                        current.Removed.Add(value);
                        break;
                    case "datachanges":
                    case "datachange":
                        current.DataChanges.Add(value);
                        break;
                }
            }
        }
    }

    /// <summary>安装完成待展示的更新信息（update-pending.ini）。</summary>
    internal static class PendingNotes
    {
        public const string FileName = "update-pending.ini";

        public static string Path
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return System.IO.Path.Combine(Config.InstallDir, FileName);
            }
        }

        public static bool Exists
        {
            get
            {
                string path = Path;
                return path.Length > 0 && File.Exists(path);
            }
        }

        public static void Write(string fromVersion, string toVersion, bool fresh)
        {
            string path = Path;
            if (path.Length == 0)
            {
                return;
            }
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 安装/更新完成后待展示一次的信息；展示后删除。");
                sb.AppendLine("From=" + (fromVersion ?? ""));
                sb.AppendLine("To=" + (toVersion ?? ""));
                sb.AppendLine("Fresh=" + (fresh ? "1" : "0"));
                sb.AppendLine("DataVersion=" + AppInfo.DataVersion);
                sb.AppendLine("Created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                InstallLog.Write("写入待展示更新信息失败：" + ex.Message);
            }
        }

        public static string Read(string key)
        {
            return Ini.Read(Path, key);
        }

        public static void Clear()
        {
            string path = Path;
            if (path.Length == 0)
            {
                return;
            }
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>已读版本记录：同一版本只弹一次完成窗口。单独一个文件，不改动 settings.ini。</summary>
    internal static class NotesSeen
    {
        public const string FileName = "notes-seen.ini";

        public static string Path
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return System.IO.Path.Combine(Config.InstallDir, FileName);
            }
        }

        public static string Last
        {
            get { return Ini.Read(Path, "SeenVersion"); }
        }

        public static bool IsSeen(SemVer version)
        {
            if (version == null)
            {
                return false;
            }
            SemVer seen;
            return SemVer.TryParse(Last, out seen) && seen.IsSame(version);
        }

        public static void MarkSeen(SemVer version)
        {
            string path = Path;
            if (path.Length == 0 || version == null)
            {
                return;
            }
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 已读更新日志的版本（同一版本只弹一次完成窗口）");
                sb.AppendLine("SeenVersion=" + version.ToDisplay());
                sb.AppendLine("SeenAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
