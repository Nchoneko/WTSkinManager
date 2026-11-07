using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WtSkin
{
    /// <summary>
    /// 一台载具的等级（I~IX）与三档权重（AB/RB/SB）。
    /// 缺失一律为 null，绝不套用默认值或相近载具的数据。
    /// </summary>
    internal class VehicleMetaEntry
    {
        /// <summary>Wiki 单位页 slug（主键），例如 cn_ztz_99a。</summary>
        public string Slug = "";
        /// <summary>1~9；0 表示页面没有给出 Rank。</summary>
        public int Rank;
        public double? Ab;
        public double? Rb;
        public double? Sb;
        /// <summary>三档完全相同时只存一个 RB，此标记表示 AB/SB 与 RB 相同。</summary>
        public bool SameModes;
        /// <summary>命中方式：slug（主键完全相等）/ name:EN / name:ZH / 空。</summary>
        public string Source = "";
        /// <summary>按显示名匹配时，本条数据对应的本地 template_ slug。</summary>
        public string LocalSlug = "";
        /// <summary>Wiki 科技树上的显示名，便于复核。</summary>
        public string DisplayName = "";
        public string Nation = "";
        public string Branch = "";
        /// <summary>本条数据的采集时间（yyyy-MM-dd HH:mm）；内置基础数据为空。</summary>
        public string Collected = "";

        public VehicleMetaEntry Clone()
        {
            VehicleMetaEntry copy = new VehicleMetaEntry();
            copy.Slug = Slug;
            copy.Rank = Rank;
            copy.Ab = Ab;
            copy.Rb = Rb;
            copy.Sb = Sb;
            copy.SameModes = SameModes;
            copy.Source = Source;
            copy.LocalSlug = LocalSlug;
            copy.DisplayName = DisplayName;
            copy.Nation = Nation;
            copy.Branch = Branch;
            copy.Collected = Collected;
            return copy;
        }

        public bool HasData
        {
            get { return Rank > 0 || Rb.HasValue; }
        }

        /// <summary>等级/权重显示所需的最小数据：Rank + RB。</summary>
        public bool Displayable
        {
            get { return Rank > 0 && Rb.HasValue; }
        }

        public bool HasAllModes
        {
            get { return Ab.HasValue && Rb.HasValue && Sb.HasValue; }
        }

        /// <summary>三档是否真的不一致（按一位小数的显示值比较）。</summary>
        public bool ModesDiffer
        {
            get
            {
                if (!HasAllModes)
                {
                    return false;
                }
                string ab = VehicleMeta.FormatBr(Ab);
                string rb = VehicleMeta.FormatBr(Rb);
                string sb = VehicleMeta.FormatBr(Sb);
                return !(string.Equals(ab, rb, StringComparison.Ordinal) &&
                         string.Equals(rb, sb, StringComparison.Ordinal));
            }
        }
    }

    /// <summary>科技树里的一条载具（阶段一采集结果）。</summary>
    internal class VehicleMetaUnit
    {
        public string Slug = "";
        public int Rank;
        public string Name = "";
        public string Nation = "";
        public string Branch = "";
    }

    /// <summary>
    /// 载具等级/权重离线数据：内置基础数据 + 安装目录 cache\vehicle_meta.ini 缓存。
    /// 查询永远只读内存，绝不实时请求 Wiki。数据缺失时返回「—」，不影响列表加载。
    /// </summary>
    internal static class VehicleMeta
    {
        private static readonly object _sync = new object();
        private static Dictionary<string, VehicleMetaEntry> _bySlug;
        private static Dictionary<string, VehicleMetaEntry> _byLocal;
        private static bool _loaded;

        public static string Version = "—";
        public static string Updated = "—";
        public static string Source = "无";
        public static int EntryCount;
        public static int LocalCount;

        public static string CachePath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.CacheDir))
                {
                    return "";
                }
                return Path.Combine(Config.CacheDir, "vehicle_meta.ini");
            }
        }

        public static string QueuePath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.CacheDir))
                {
                    return "";
                }
                return Path.Combine(Config.CacheDir, "vehicle_meta_queue.ini");
            }
        }

        public static string FailedPath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.CacheDir))
                {
                    return "";
                }
                return Path.Combine(Config.CacheDir, "vehicle_meta_failed.ini");
            }
        }

        public static string NameMatchPath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.CacheDir))
                {
                    return "";
                }
                return Path.Combine(Config.CacheDir, "vehicle_meta_namematches.txt");
            }
        }

        public static string UnmatchedPath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return Path.Combine(Config.InstallDir, "unmatched.txt");
            }
        }

        public static void Reload()
        {
            lock (_sync)
            {
                _loaded = false;
                _bySlug = null;
                _byLocal = null;
            }
            UserOverrides.Reload();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            lock (_sync)
            {
                if (_loaded)
                {
                    return;
                }
                _loaded = true;
                _bySlug = new Dictionary<string, VehicleMetaEntry>(StringComparer.OrdinalIgnoreCase);
                _byLocal = new Dictionary<string, VehicleMetaEntry>(StringComparer.OrdinalIgnoreCase);
                Version = "—";
                Updated = "—";
                Source = "无";
                EntryCount = 0;
                LocalCount = 0;

                bool hasBuiltin = false;
                try
                {
                    using (Stream stream = typeof(VehicleMeta).Assembly.GetManifestResourceStream("vehicle_meta.dat"))
                    {
                        if (stream != null)
                        {
                            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                            {
                                hasBuiltin = Parse(reader.ReadToEnd());
                            }
                        }
                    }
                }
                catch
                {
                }

                string cache = CachePath;
                if (!string.IsNullOrEmpty(cache) && File.Exists(cache))
                {
                    try
                    {
                        string text = File.ReadAllText(cache, Encoding.UTF8);
                        if (IsCurrentFormat(text))
                        {
                            Parse(text);
                            Source = "缓存";
                        }
                    }
                    catch
                    {
                    }
                }

                if (hasBuiltin && Source == "无")
                {
                    Source = "内置";
                }
                // 用户覆盖表最后应用：先读内置清单与缓存，再覆盖同名条目。
                UserOverrides.ApplyMeta(_bySlug, _byLocal);
                RebuildCounts();
            }
        }

        private static void RebuildCounts()
        {
            int count = 0;
            int local = 0;
            foreach (KeyValuePair<string, VehicleMetaEntry> pair in _bySlug)
            {
                if (pair.Value != null)
                {
                    count++;
                }
            }
            foreach (KeyValuePair<string, VehicleMetaEntry> pair in _byLocal)
            {
                if (pair.Value != null && pair.Key.Length > 0)
                {
                    local++;
                }
            }
            EntryCount = count;
            LocalCount = local;
        }

        private static bool IsCurrentFormat(string text)
        {
            if (text == null)
            {
                return false;
            }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";"))
                {
                    continue;
                }
                if (line.StartsWith("#"))
                {
                    continue;
                }
                if (line.StartsWith("format=", StringComparison.Ordinal))
                {
                    return line.Substring("format=".Length).Trim() == "2";
                }
                return false;
            }
            return false;
        }

        private static bool Parse(string text)
        {
            if (text == null)
            {
                return false;
            }
            bool any = false;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }
                if (line.IndexOf('\t') < 0 && line.IndexOf('=') > 0)
                {
                    int eq = line.IndexOf('=');
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string value = line.Substring(eq + 1).Trim();
                    if (key == "version" && value.Length > 0 && value != "—")
                    {
                        Version = value;
                    }
                    else if ((key == "updated" || key == "update") && value.Length > 0 && value != "—")
                    {
                        Updated = value;
                    }
                    continue;
                }
                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                {
                    continue;
                }
                string slug = parts[0].Trim();
                if (slug.Length == 0)
                {
                    continue;
                }
                VehicleMetaEntry entry = new VehicleMetaEntry();
                entry.Slug = slug;
                entry.Rank = ParseRankNumber(parts[1]);
                entry.Rb = ParseBr(parts[2]);
                if (parts.Length >= 4)
                {
                    entry.SameModes = parts[3].Trim() == "1";
                }
                else
                {
                    // 旧格式只有一档：按「三档相同」解释。
                    entry.SameModes = true;
                }
                if (parts.Length >= 5)
                {
                    entry.Ab = ParseBr(parts[4]);
                }
                if (parts.Length >= 6)
                {
                    entry.Sb = ParseBr(parts[5]);
                }
                if (entry.SameModes)
                {
                    entry.Ab = entry.Rb;
                    entry.Sb = entry.Rb;
                }
                if (parts.Length >= 7)
                {
                    entry.Source = parts[6].Trim();
                }
                if (parts.Length >= 8)
                {
                    entry.LocalSlug = parts[7].Trim();
                }
                if (parts.Length >= 9)
                {
                    entry.DisplayName = parts[8];
                }
                if (parts.Length >= 10)
                {
                    entry.Nation = parts[9].Trim();
                }
                if (parts.Length >= 11)
                {
                    entry.Branch = parts[10].Trim();
                }
                if (parts.Length >= 12)
                {
                    entry.Collected = parts[11].Trim();
                }
                if (!entry.HasData && entry.DisplayName.Length == 0)
                {
                    continue;
                }
                _bySlug[slug] = entry;
                if (entry.LocalSlug.Length > 0)
                {
                    _byLocal[entry.LocalSlug] = entry;
                }
                any = true;
            }
            return any;
        }

        public static string ToRoman(int rank)
        {
            switch (rank)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                case 7: return "VII";
                case 8: return "VIII";
                case 9: return "IX";
                default: return "";
            }
        }

        public static int ParseRankNumber(string rank)
        {
            string text = (rank == null ? "" : rank.Trim()).ToUpperInvariant();
            switch (text)
            {
                case "I": case "1": return 1;
                case "II": case "2": return 2;
                case "III": case "3": return 3;
                case "IV": case "4": return 4;
                case "V": case "5": return 5;
                case "VI": case "6": return 6;
                case "VII": case "7": return 7;
                case "VIII": case "8": return 8;
                case "IX": case "9": return 9;
                default: return 0;
            }
        }

        public static double? ParseBr(string text)
        {
            string value = (text == null ? "" : text.Trim());
            if (value.Length == 0)
            {
                return null;
            }
            double parsed;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return null;
            }
            if (parsed <= 0 || parsed > 99)
            {
                return null;
            }
            return parsed;
        }

        public static string FormatBr(double? value)
        {
            if (!value.HasValue)
            {
                return "";
            }
            return value.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>按本地 template_ slug（主键优先，其次显示名映射）取数据；没有返回 null。</summary>
        public static VehicleMetaEntry Find(string unitId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }
            string key = unitId.Trim();
            VehicleMetaEntry entry;
            lock (_sync)
            {
                if (_byLocal.TryGetValue(key, out entry) && entry != null)
                {
                    return entry;
                }
                if (_bySlug.TryGetValue(key, out entry))
                {
                    return entry;
                }
            }
            return null;
        }

        public static VehicleMetaEntry FindBySlug(string slug)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(slug))
            {
                return null;
            }
            lock (_sync)
            {
                VehicleMetaEntry entry;
                if (_bySlug.TryGetValue(slug.Trim(), out entry))
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>界面显示值，例如「VIII · 12.7」；缺数据统一返回「—」。</summary>
        public static string Display(string unitId)
        {
            VehicleMetaEntry entry = Find(unitId);
            if (entry == null || !entry.Displayable)
            {
                return "—";
            }
            return ToRoman(entry.Rank) + " · " + FormatBr(entry.Rb);
        }

        /// <summary>三档不一致时浮层补充的一行；相同或缺失返回空串。</summary>
        public static string ModesLine(string unitId)
        {
            VehicleMetaEntry entry = Find(unitId);
            if (entry == null || !entry.HasAllModes || !entry.ModesDiffer)
            {
                return "";
            }
            // 直升机只显示 RB，不展开 AB/SB 三档。
            if (string.Equals(entry.Branch, "helicopters", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return "AB " + FormatBr(entry.Ab) + " / RB " + FormatBr(entry.Rb) + " / SB " + FormatBr(entry.Sb);
        }

        public static string StatusText()
        {
            EnsureLoaded();
            return "数据 " + Version + " · " + Updated;
        }

        /// <summary>详情窗口文本：版本、采集时间、来源分布、映射与未匹配数量。</summary>
        public static string InfoText()
        {
            EnsureLoaded();
            int bySlug = 0;
            int byName = 0;
            lock (_sync)
            {
                foreach (KeyValuePair<string, VehicleMetaEntry> pair in _bySlug)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }
                    if (pair.Value.Source.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                    {
                        byName++;
                    }
                    else
                    {
                        bySlug++;
                    }
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("版本：" + Version);
            sb.AppendLine("采集时间：" + Updated);
            sb.AppendLine("数据来源：" + Source);
            sb.AppendLine("条目：共 " + EntryCount + " 台（按 slug " + bySlug + " · 按显示名 " + byName + "）");
            sb.AppendLine("本地映射：" + LocalCount);
            sb.AppendLine("缓存：" + CachePath);
            sb.AppendLine("科技树队列：" + QueuePath);
            sb.AppendLine("未匹配清单：" + UnmatchedPath);
            try
            {
                if (!string.IsNullOrEmpty(QueuePath) && File.Exists(QueuePath))
                {
                    string created = ReadQueueCreated();
                    if (created.Length > 0)
                    {
                        sb.AppendLine("队列生成时间：" + created);
                    }
                }
            }
            catch
            {
            }
            string unmatched = UnmatchedPath;
            try
            {
                if (!string.IsNullOrEmpty(unmatched) && File.Exists(unmatched))
                {
                    int lines = 0;
                    string[] all = File.ReadAllLines(unmatched, Encoding.UTF8);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].Length > 0 && !all[i].StartsWith("#"))
                        {
                            lines++;
                        }
                    }
                    sb.AppendLine("未匹配条目：" + lines + " 行（见 unmatched.txt）");
                }
            }
            catch
            {
            }
            return sb.ToString();
        }

        public static Dictionary<string, VehicleMetaEntry> Snapshot()
        {
            EnsureLoaded();
            Dictionary<string, VehicleMetaEntry> copy =
                new Dictionary<string, VehicleMetaEntry>(StringComparer.OrdinalIgnoreCase);
            lock (_sync)
            {
                foreach (KeyValuePair<string, VehicleMetaEntry> pair in _bySlug)
                {
                    copy[pair.Key] = pair.Value.Clone();
                }
            }
            return copy;
        }

        /// <summary>把内存数据写成缓存（原子替换），并更新版本/时间；不重载。</summary>
        public static bool SaveCache(Dictionary<string, VehicleMetaEntry> data, string version, string updated)
        {
            string path = CachePath;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            try
            {
                Directory.CreateDirectory(Config.CacheDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 战雷涂装管理器 - 载具等级/权重缓存（Wiki 采集，自动生成）");
                sb.AppendLine("# 格式：slug<TAB>等级<TAB>RB<TAB>三档相同<TAB>AB<TAB>SB<TAB>命中方式<TAB>本地 slug<TAB>显示名<TAB>国家<TAB>分支<TAB>采集时间");
                sb.AppendLine("format=2");
                sb.AppendLine("version=" + (version == null ? "" : version));
                sb.AppendLine("updated=" + (updated == null ? "" : updated));
                if (data != null)
                {
                    List<string> keys = new List<string>(data.Keys);
                    keys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        VehicleMetaEntry entry = data[keys[i]];
                        if (entry == null || !entry.HasData)
                        {
                            continue;
                        }
                        bool same = entry.SameModes || SameBr(entry);
                        sb.Append(entry.Slug.Length > 0 ? entry.Slug : keys[i]).Append('\t')
                          .Append(entry.Rank.ToString(CultureInfo.InvariantCulture)).Append('\t')
                          .Append(FormatBr(entry.Rb)).Append('\t')
                          .Append(same ? "1" : "0").Append('\t')
                          .Append(same ? "" : FormatBr(entry.Ab)).Append('\t')
                          .Append(same ? "" : FormatBr(entry.Sb)).Append('\t')
                          .Append(entry.Source).Append('\t')
                          .Append(entry.LocalSlug).Append('\t')
                          .Append(entry.DisplayName).Append('\t')
                          .Append(entry.Nation).Append('\t')
                          .Append(entry.Branch).Append('\t')
                          .Append(entry.Collected).AppendLine();
                    }
                }
                AtomicWrite(path, sb.ToString());
                lock (_sync)
                {
                    if (!string.IsNullOrEmpty(version) && version != "—")
                    {
                        Version = version;
                    }
                    if (!string.IsNullOrEmpty(updated) && updated != "—")
                    {
                        Updated = updated;
                    }
                    Source = "缓存";
                }
                return true;
            }
            catch (Exception ex)
            {
                Config.Log("写载具数据缓存失败：" + ex.Message);
                return false;
            }
        }

        public static bool SameBr(VehicleMetaEntry entry)
        {
            if (entry == null || !entry.HasAllModes)
            {
                return false;
            }
            return !entry.ModesDiffer;
        }

        public static bool AtomicWrite(string path, string text)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch
                {
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
            }
            else
            {
                File.Move(temp, path);
            }
            return true;
        }

        /// <summary>科技树队列：阶段一的结果，续跑时直接复用，避免重复请求 30 个科技树页。</summary>
        public static List<VehicleMetaUnit> LoadQueue(out string created)
        {
            created = "";
            List<VehicleMetaUnit> list = new List<VehicleMetaUnit>();
            string path = QueuePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return list;
            }
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.IndexOf('\t') < 0 && line.IndexOf('=') > 0)
                    {
                        int eq = line.IndexOf('=');
                        string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                        if (key == "created")
                        {
                            created = line.Substring(eq + 1).Trim();
                        }
                        continue;
                    }
                    string[] parts = line.Split('\t');
                    if (parts.Length < 2)
                    {
                        continue;
                    }
                    VehicleMetaUnit unit = new VehicleMetaUnit();
                    unit.Slug = parts[0].Trim();
                    unit.Rank = ParseRankNumber(parts[1]);
                    if (parts.Length >= 3)
                    {
                        unit.Name = parts[2];
                    }
                    if (parts.Length >= 4)
                    {
                        unit.Nation = parts[3].Trim();
                    }
                    if (parts.Length >= 5)
                    {
                        unit.Branch = parts[4].Trim();
                    }
                    if (unit.Slug.Length > 0)
                    {
                        list.Add(unit);
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        public static string ReadQueueCreated()
        {
            string created;
            LoadQueue(out created);
            return created;
        }

        public static bool SaveQueue(List<VehicleMetaUnit> units, string created)
        {
            string path = QueuePath;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            try
            {
                Directory.CreateDirectory(Config.CacheDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 战雷涂装管理器 - Wiki 科技树队列（阶段一采集结果，自动生成）");
                sb.AppendLine("# 格式：slug<TAB>科技树等级<TAB>显示名<TAB>国家<TAB>分支");
                sb.AppendLine("created=" + (created == null ? "" : created));
                if (units != null)
                {
                    for (int i = 0; i < units.Count; i++)
                    {
                        VehicleMetaUnit unit = units[i];
                        if (unit == null || unit.Slug.Length == 0)
                        {
                            continue;
                        }
                        sb.Append(unit.Slug).Append('\t')
                          .Append(unit.Rank.ToString(CultureInfo.InvariantCulture)).Append('\t')
                          .Append(unit.Name).Append('\t')
                          .Append(unit.Nation).Append('\t')
                          .Append(unit.Branch).AppendLine();
                    }
                }
                AtomicWrite(path, sb.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Config.Log("写科技树队列失败：" + ex.Message);
                return false;
            }
        }

        public static void LogUnmatched(List<string> unmatched, string version, string updated)
        {
            try
            {
                string path = UnmatchedPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 载具数据未匹配清单");
                sb.AppendLine("# 版本：" + version + "  更新时间：" + updated);
                if (unmatched == null || unmatched.Count == 0)
                {
                    sb.AppendLine("（无）");
                }
                else
                {
                    for (int i = 0; i < unmatched.Count; i++)
                    {
                        sb.AppendLine(unmatched[i]);
                    }
                }
                AtomicWrite(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Config.Log("写 unmatched.txt 失败：" + ex.Message);
            }
        }

        public static void LogNameMatch(string localSlug, string wikiSlug, string field, string name)
        {
            try
            {
                string path = NameMatchPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                Directory.CreateDirectory(Config.CacheDir);
                string line = localSlug + "\t" + wikiSlug + "\t" + field + "\t" + name + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        /// <summary>按显示名做「完全相等」匹配的名字规范化：去标记符号、NBSP、大小写差异，但绝不模糊。</summary>
        public static string NormalizeName(string name)
        {
            if (name == null)
            {
                return "";
            }
            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '\u2417' || c == '\u2584' || c == '\u25CC' || c == '\u25CD' ||
                    c == '\u25C6' || c == '\u2605' || c == '\u25CF')
                {
                    continue;
                }
                if (c == '\u00A0' || c == '\u3000' || c == '\t')
                {
                    sb.Append(' ');
                    continue;
                }
                sb.Append(c);
            }
            string text = sb.ToString();
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }
            return text.Trim().ToLowerInvariant();
        }
    }

    internal class VehicleMetaUpdateResult
    {
        public bool Completed;
        public bool Paused;
        public int UpdatedCount;
        public int SkippedCount;
        public int FailedCount;
        public int UnmatchedCount;
        public int TotalPlanned;
        public string Version = "";
        public string Updated = "";
        public string Message = "";
        public string FirstError = "";
    }

    /// <summary>
    /// 手动更新：先遍历 30 个科技树页（3 分支 × 10 国家）取本线载具，
    /// 再逐个访问 /unit/&lt;slug&gt; 解析 Rank 与 AB/RB/SB。
    /// 请求间隔 ≥1 秒、超时 10 秒、单条最多重试 2 次；连续失败超过 10 条暂停。
    /// 已完成条目即时落盘，下次跳过；失败条目不阻塞，界面显示「—」。
    /// </summary>
    internal static class VehicleMetaUpdater
    {
        private static readonly string[] Nations = new string[]
        {
            "usa", "germany", "ussr", "britain", "japan",
            "china", "italy", "france", "sweden", "israel"
        };

        private static readonly string[] Branches = new string[]
        {
            "aviation", "helicopters", "ground"
        };

        private static readonly Regex RankRegex = new Regex(
            "game-unit_rank\"[^>]*>\\s*<div class=\"game-unit_card-info_value\">\\s*([IVX]+)\\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BrItemRegex = new Regex(
            "game-unit_br-item\"[^>]*>\\s*<div class=\"mode\">(AB|RB|SB)</div>\\s*<div class=\"value\">\\s*([0-9]+(?:\\.[0-9]+)?)\\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RankHeaderRegex = new Regex(
            "wt-tree_r-header_label\">Rank <span>([IVX]+)</span>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ItemStartRegex = new Regex(
            "<div class=\"wt-tree_item",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ItemLinkRegex = new Regex(
            "wt-tree_item-link\" href=\"/unit/([^\"]+)\"",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ItemNameRegex = new Regex(
            "wt-tree_item-text\"><span>([^<]*)</span>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly object _gate = new object();
        private static DateTime _lastRequest = DateTime.MinValue;

        /// <summary>最近一次科技树采集结果（供图标采集复用，避免重复请求 30 个树页）。</summary>
        public static List<VehicleMetaUnit> LastTreeUnits = new List<VehicleMetaUnit>();

        private const int MinGapMs = 1100;
        private const int TimeoutMs = 10000;
        private const int MaxRetry = 2;
        private const int MaxConsecutiveFailures = 10;
        private const int ResumeWindowHours = 24;
        private const int QueueFreshHours = 24;
        private const int PermanentFailureDays = 7;
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WTSkinManager/3.3.0";

        private class PlanItem
        {
            public string Slug = "";
            public string Local = "";
            public string Source = "";
            public VehicleMetaUnit Unit;
        }

        /// <summary>更新本机 UserSkins 里已有涂装的载具（优先，请求最少）。</summary>
        public static VehicleMetaUpdateResult UpdateLocal(Action<string> progress)
        {
            return Update(false, progress);
        }

        /// <summary>更新全部数据：遍历 30 条科技树线，后台逐个采集。</summary>
        public static VehicleMetaUpdateResult UpdateAll(Action<string> progress)
        {
            return Update(true, progress);
        }

        public static VehicleMetaUpdateResult Update(bool full, Action<string> progress)
        {
            VehicleMetaUpdateResult result = new VehicleMetaUpdateResult();
            string version = NextVersion();
            string updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            List<string> unmatched = new List<string>();

            PrepareTls();

            List<VehicleMetaUnit> queue;
            string queueCreated;
            queue = VehicleMeta.LoadQueue(out queueCreated);
            DateTime queueTime;
            bool queueFresh = queue.Count > 0 &&
                              DateTime.TryParse(queueCreated, CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out queueTime) &&
                              (DateTime.Now - queueTime).TotalHours < QueueFreshHours;
            if (!queueFresh)
            {
                queue = FetchQueue(progress, result);
                queueCreated = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                VehicleMeta.SaveQueue(queue, queueCreated);
            }
            else
            {
                Report(progress, "复用科技树队列（" + queue.Count + " 条，" + queueCreated + "）");
            }

            // 科技树采集结果同时是图标清单的新增来源：等级权重与图标共用同一份状态记录。
            LastTreeUnits = new List<VehicleMetaUnit>(queue);
            if (full)
            {
                int added = VehicleCatalog.MergeUnits(queue);
                if (added > 0)
                {
                    Config.Log("图标清单新增载具 " + added + " 条");
                }
            }

            Dictionary<string, string> namesBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Slug.Length > 0)
                {
                    namesBySlug[queue[i].Slug] = queue[i].Name;
                }
            }

            Dictionary<string, VehicleMetaEntry> data = VehicleMeta.Snapshot();
            List<string> localSlugs = LocalSlugs();
            Dictionary<string, VehicleMetaEntry> localIndex = new Dictionary<string, VehicleMetaEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localSlugs.Count; i++)
            {
                VehicleMetaEntry found = VehicleMeta.Find(localSlugs[i]);
                if (found != null)
                {
                    localIndex[localSlugs[i]] = found;
                }
            }

            List<PlanItem> plan = new List<PlanItem>();
            List<PlanItem> aliasPlans = new List<PlanItem>();
            HashSet<string> planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (full)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    string slug = queue[i].Slug;
                    if (slug.Length == 0 || planned.Contains(slug))
                    {
                        continue;
                    }
                    planned.Add(slug);
                    PlanItem item = new PlanItem();
                    item.Slug = slug;
                    item.Unit = queue[i];
                    plan.Add(item);
                }
            }
            else
            {
                for (int i = 0; i < localSlugs.Count; i++)
                {
                    string local = localSlugs[i];
                    VehicleMetaEntry known;
                    if (localIndex.TryGetValue(local, out known) && known != null && IsFresh(known))
                    {
                        result.SkippedCount++;
                        continue;
                    }
                    VehicleMetaUnit foundUnit;
                    string wikiSlug;
                    VehicleMetaUnit matchedUnit;
                    string field;
                    bool exact = TryFindUnit(queue, local, out foundUnit);
                    PlanItem item = new PlanItem();
                    if (exact)
                    {
                        wikiSlug = local;
                        item.Unit = foundUnit;
                    }
                    else if (TryMatchByName(local, queue, out wikiSlug, out matchedUnit, out field))
                    {
                        item.Unit = matchedUnit;
                        item.Local = local;
                        item.Source = "name:" + field;
                    }
                    else
                    {
                        string reason = namesBySlug.ContainsKey(local)
                            ? "科技树有同 slug 载具"
                            : "wiki 无同 slug，且显示名不匹配";
                        unmatched.Add(local + "\t本地涂装：" + reason);
                        continue;
                    }
                    item.Slug = wikiSlug;
                    if (planned.Add(wikiSlug))
                    {
                        plan.Add(item);
                    }
                    else if (item.Local.Length > 0)
                    {
                        aliasPlans.Add(item);
                    }
                }
            }

            result.TotalPlanned = plan.Count;
            result.UpdatedCount = 0;
            result.FailedCount = 0;
            int consecutive = 0;

            for (int p = 0; p < plan.Count; p++)
            {
                PlanItem item = plan[p];
                int index = p + 1;
                string failRecord;
                if (LoadFailed().TryGetValue(item.Slug, out failRecord) && IsPermanent(failRecord) &&
                    FailedRecently(item.Slug))
                {
                    result.SkippedCount++;
                    unmatched.Add(item.Slug + "\t跳过：上次失败（永久）");
                    continue;
                }
                VehicleMetaEntry existing;
                if (data.TryGetValue(item.Slug, out existing) && existing != null && IsFresh(existing))
                {
                    result.SkippedCount++;
                    continue;
                }

                Report(progress, "更新中：" + index + "/" + plan.Count + " · 成功 " + result.UpdatedCount +
                                 " · 跳过 " + result.SkippedCount + " · 失败 " + result.FailedCount);

                string url = "https://wiki.warthunder.com/unit/" + item.Slug;
                string error;
                string html = Fetch(url, out error);
                if (string.IsNullOrEmpty(html))
                {
                    consecutive++;
                    result.FailedCount++;
                    string reason = string.IsNullOrEmpty(error) ? "请求失败" : error;
                    if (result.FirstError.Length == 0)
                    {
                        result.FirstError = item.Slug + "：" + reason;
                    }
                    RecordFailed(item.Slug, reason);
                    VehicleCatalog.MarkMeta(item.Slug, false, reason);
                    unmatched.Add(item.Slug + "\t采集失败：" + reason);
                    if (consecutive > MaxConsecutiveFailures)
                    {
                        result.Paused = true;
                        break;
                    }
                    continue;
                }

                int rank;
                double? ab;
                double? rb;
                double? sb;
                bool parsed = ParseUnitPage(html, out rank, out ab, out rb, out sb);
                if (!parsed || (rank <= 0 && !rb.HasValue))
                {
                    consecutive++;
                    result.FailedCount++;
                    RecordFailed(item.Slug, "页面缺少 Rank / Battle Rating");
                    VehicleCatalog.MarkMeta(item.Slug, false, "页面缺少 Rank / Battle Rating");
                    unmatched.Add(item.Slug + "\t页面缺少 Rank / Battle Rating");
                    if (consecutive > MaxConsecutiveFailures)
                    {
                        result.Paused = true;
                        break;
                    }
                    continue;
                }

                VehicleMetaUnit unit = item.Unit;
                VehicleMetaEntry entry = new VehicleMetaEntry();
                entry.Slug = item.Slug;
                entry.Rank = rank > 0 ? rank : (unit == null ? 0 : unit.Rank);
                entry.Ab = ab;
                entry.Rb = rb;
                entry.Sb = sb;
                entry.SameModes = ab.HasValue && rb.HasValue && sb.HasValue &&
                                  string.Equals(VehicleMeta.FormatBr(ab), VehicleMeta.FormatBr(rb), StringComparison.Ordinal) &&
                                  string.Equals(VehicleMeta.FormatBr(rb), VehicleMeta.FormatBr(sb), StringComparison.Ordinal);
                entry.Source = item.Source.Length > 0 ? item.Source : "slug";
                entry.LocalSlug = item.Local;
                entry.DisplayName = unit != null ? unit.Name : "";
                entry.Nation = unit != null ? unit.Nation : "";
                entry.Branch = unit != null ? unit.Branch : "";
                entry.Collected = updatedAt;
                data[item.Slug] = entry;
                if (entry.LocalSlug.Length > 0)
                {
                    VehicleMeta.LogNameMatch(entry.LocalSlug, entry.Slug, entry.Source, entry.DisplayName);
                }
                RemoveFailed(item.Slug);
                VehicleCatalog.MarkMeta(item.Slug, true, "");
                result.UpdatedCount++;
                consecutive = 0;
                // 每完成一条立即落盘：中断后下次直接跳过。
                VehicleMeta.SaveCache(data, version, updatedAt);
            }

            // 同一 wiki slug 被多个本地 slug 引用：补别名（不再请求）。
            for (int i = 0; i < aliasPlans.Count; i++)
            {
                PlanItem aliasPlan = aliasPlans[i];
                VehicleMetaEntry source;
                if (!data.TryGetValue(aliasPlan.Slug, out source) || source == null)
                {
                    continue;
                }
                VehicleMetaEntry alias = source.Clone();
                alias.LocalSlug = aliasPlan.Local;
                alias.Source = aliasPlan.Source;
                alias.Collected = updatedAt;
                data[aliasPlan.Slug] = alias;
                VehicleMeta.LogNameMatch(aliasPlan.Local, aliasPlan.Slug, aliasPlan.Source,
                                         alias.DisplayName);
            }

            // 本机涂装按显示名映射到已采集的数据（不额外请求）。
            int aliasAdded = ApplyLocalNameAliases(data, localSlugs, queue, version, updatedAt, unmatched);

            // 本地涂装仍没有数据的，补进未匹配清单。
            for (int i = 0; i < localSlugs.Count; i++)
            {
                string local = localSlugs[i];
                bool has = HasEntry(data, local);
                if (!has && !ListContainsPrefix(unmatched, local))
                {
                    unmatched.Add(local + "\t本地涂装：未采集到数据");
                }
            }

            bool changed = result.UpdatedCount > 0 || aliasPlans.Count > 0 || aliasAdded > 0;
            if (changed)
            {
                VehicleMeta.SaveCache(data, version, updatedAt);
            }
            // 等级权重的采集状态与图标共用同一份清单，统一落盘。
            VehicleCatalog.Save();
            string effectiveVersion = changed ? version : VehicleMeta.Version;
            string effectiveUpdated = changed ? updatedAt : VehicleMeta.Updated;
            VehicleMeta.LogUnmatched(unmatched, effectiveVersion, effectiveUpdated);

            result.Version = effectiveVersion;
            result.Updated = effectiveUpdated;
            result.UnmatchedCount = unmatched.Count;
            result.Completed = !result.Paused;
            if (result.Paused)
            {
                result.Message = "连续失败超过 " + MaxConsecutiveFailures + " 条，已暂停；已完成 " +
                                 result.UpdatedCount + " 条已保存，下次从断点继续";
            }
            else
            {
                StringBuilder message = new StringBuilder();
                message.Append("更新 ").Append(result.UpdatedCount).Append(" 条");
                if (result.SkippedCount > 0)
                {
                    message.Append("，跳过 ").Append(result.SkippedCount).Append(" 条");
                }
                if (result.FailedCount > 0)
                {
                    message.Append("，失败 ").Append(result.FailedCount).Append(" 条");
                }
                if (result.UnmatchedCount > 0)
                {
                    message.Append("，未匹配 ").Append(result.UnmatchedCount).Append(" 条");
                }
                result.Message = message.ToString();
            }
            Config.Log("载具数据更新（" + (full ? "全部" : "本机涂装") + "）：" + result.Message +
                       "，版本 " + version + "，时间 " + updatedAt);
            return result;
        }

        private static string NextVersion()
        {
            string today = DateTime.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
            string current = VehicleMeta.Version;
            int seq = 0;
            if (!string.IsNullOrEmpty(current) && current.StartsWith(today + ".", StringComparison.Ordinal))
            {
                int.TryParse(current.Substring(today.Length + 1), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out seq);
            }
            return today + "." + (seq + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static bool ListContainsPrefix(List<string> list, string prefix)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].StartsWith(prefix + "\t", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryFindUnit(List<VehicleMetaUnit> queue, string slug, out VehicleMetaUnit unit)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (string.Equals(queue[i].Slug, slug, StringComparison.Ordinal))
                {
                    unit = queue[i];
                    return true;
                }
            }
            unit = null;
            return false;
        }

        /// <summary>
        /// 显示名匹配：本地载具的英文/中文名与科技树显示名规范化后完全相等；
        /// 命中唯一才采用，多个同名视为歧义（不猜、不模糊）。
        /// </summary>
        private static bool TryMatchByName(string localSlug, List<VehicleMetaUnit> queue,
                                           out string wikiSlug, out VehicleMetaUnit matched, out string field)
        {
            wikiSlug = "";
            matched = null;
            field = "";
            Vehicle vehicle = VehicleDb.Find(localSlug);
            if (vehicle == null)
            {
                return false;
            }
            string[] candidates = new string[] { vehicle.En, vehicle.Zh };
            string[] fields = new string[] { "EN", "ZH" };
            int hits = 0;
            string hitSlug = "";
            string hitField = "";
            VehicleMetaUnit hitUnit = null;
            for (int i = 0; i < queue.Count; i++)
            {
                VehicleMetaUnit unit = queue[i];
                if (string.Equals(unit.Slug, localSlug, StringComparison.Ordinal))
                {
                    continue;
                }
                string name = VehicleMeta.NormalizeName(unit.Name);
                if (name.Length == 0)
                {
                    continue;
                }
                for (int c = 0; c < candidates.Length; c++)
                {
                    string candidate = VehicleMeta.NormalizeName(candidates[c]);
                    if (candidate.Length == 0 || !string.Equals(candidate, name, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!string.Equals(hitSlug, unit.Slug, StringComparison.Ordinal))
                    {
                        hits++;
                        hitUnit = unit;
                        hitSlug = unit.Slug;
                        hitField = fields[c];
                    }
                    break;
                }
            }
            if (hits != 1)
            {
                return false;
            }
            wikiSlug = hitSlug;
            matched = hitUnit;
            field = hitField;
            return true;
        }

        private static int ApplyLocalNameAliases(Dictionary<string, VehicleMetaEntry> data,
                                                 List<string> localSlugs, List<VehicleMetaUnit> queue,
                                                 string version, string updatedAt, List<string> unmatched)
        {
            int added = 0;
            for (int i = 0; i < localSlugs.Count; i++)
            {
                string local = localSlugs[i];
                if (HasEntry(data, local))
                {
                    continue;
                }
                string wikiSlug;
                VehicleMetaUnit unit;
                string field;
                if (!TryMatchByName(local, queue, out wikiSlug, out unit, out field))
                {
                    continue;
                }
                VehicleMetaEntry source;
                if (!data.TryGetValue(wikiSlug, out source) || source == null || !source.HasData)
                {
                    continue;
                }
                VehicleMetaEntry alias = source.Clone();
                alias.LocalSlug = local;
                alias.Source = "name:" + field;
                alias.Collected = updatedAt;
                data[wikiSlug] = alias;
                VehicleMeta.LogNameMatch(local, wikiSlug, field, unit == null ? "" : unit.Name);
                VehicleMeta.SaveCache(data, version, updatedAt);
                added++;
            }
            return added;
        }

        private static bool HasEntry(Dictionary<string, VehicleMetaEntry> data, string localSlug)
        {
            VehicleMetaEntry direct;
            if (data.TryGetValue(localSlug, out direct) && direct != null && direct.HasData)
            {
                return true;
            }
            foreach (KeyValuePair<string, VehicleMetaEntry> pair in data)
            {
                if (pair.Value != null && pair.Value.HasData &&
                    string.Equals(pair.Value.LocalSlug, localSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>本机 UserSkins 顶层里 template_&lt;slug&gt; 目录的 slug（严格取目录名，去掉 .BAN 与扩展名）。</summary>
        public static List<string> LocalSlugs()
        {
            List<string> list = new List<string>();
            string dir = Config.UserSkinsDir;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return list;
            }
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(dir);
            }
            catch
            {
                return list;
            }
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.IsNullOrEmpty(name) || name.StartsWith("."))
                {
                    continue;
                }
                if (name.EndsWith(".BAN", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - 4);
                }
                if (!name.StartsWith("template_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string slug = name.Substring("template_".Length);
                int dot = slug.IndexOf('.');
                if (dot > 0)
                {
                    slug = slug.Substring(0, dot);
                }
                if (slug.Length == 0)
                {
                    continue;
                }
                bool exists = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (string.Equals(list[j], slug, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    list.Add(slug);
                }
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static bool IsFresh(VehicleMetaEntry entry)
        {
            if (entry == null || entry.Collected.Length == 0)
            {
                return false;
            }
            DateTime collected;
            if (!DateTime.TryParse(entry.Collected, CultureInfo.InvariantCulture, DateTimeStyles.None, out collected))
            {
                return false;
            }
            return (DateTime.Now - collected).TotalHours < ResumeWindowHours;
        }

        /// <summary>阶段一：遍历 30 个科技树页，取本线载具与 /unit/&lt;slug&gt; 链接。</summary>
        private static List<VehicleMetaUnit> FetchQueue(Action<string> progress, VehicleMetaUpdateResult result)
        {
            List<VehicleMetaUnit> units = new List<VehicleMetaUnit>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = Nations.Length * Branches.Length;
            int index = 0;
            int failed = 0;
            for (int b = 0; b < Branches.Length; b++)
            {
                for (int n = 0; n < Nations.Length; n++)
                {
                    index++;
                    string branch = Branches[b];
                    string nation = Nations[n];
                    Report(progress, "采集科技树 " + index + "/" + total + "：" + branch + " · " + nation);
                    string url = "https://wiki.warthunder.com/" + branch + "?v=t&t_c=" + nation;
                    string error;
                    string html = Fetch(url, out error);
                    if (string.IsNullOrEmpty(html))
                    {
                        failed++;
                        continue;
                    }
                    List<VehicleMetaUnit> line;
                    try
                    {
                        line = ParseTree(html, nation, branch);
                    }
                    catch
                    {
                        line = null;
                    }
                    if (line == null)
                    {
                        failed++;
                        continue;
                    }
                    for (int i = 0; i < line.Count; i++)
                    {
                        VehicleMetaUnit unit = line[i];
                        if (seen.Add(unit.Slug))
                        {
                            units.Add(unit);
                        }
                    }
                }
            }
            if (failed > 0)
            {
                Config.Log("科技树采集失败 " + failed + "/" + total + " 页");
            }
            result.FirstError = failed > 0 ? ("科技树页失败 " + failed + " 个") : "";
            return units;
        }

        /// <summary>解析某个国家的科技树块：Rank 头 → 下一段内的载具项（只保留带 /unit/ 链接的）。</summary>
        public static List<VehicleMetaUnit> ParseTree(string html, string nation, string branch)
        {
            List<VehicleMetaUnit> units = new List<VehicleMetaUnit>();
            if (html == null)
            {
                return units;
            }
            string marker = "data-tree-id=\"" + nation + "\"";
            int start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return units;
            }
            int next = html.IndexOf("data-tree-id=\"", start + marker.Length, StringComparison.OrdinalIgnoreCase);
            int end = next < 0 ? html.Length : next;
            string block = html.Substring(start, end - start);

            List<int> rankPositions = new List<int>();
            List<int> rankValues = new List<int>();
            MatchCollection headers = RankHeaderRegex.Matches(block);
            for (int i = 0; i < headers.Count; i++)
            {
                rankPositions.Add(headers[i].Index);
                rankValues.Add(VehicleMeta.ParseRankNumber(headers[i].Groups[1].Value));
            }

            MatchCollection items = ItemStartRegex.Matches(block);
            for (int i = 0; i < items.Count; i++)
            {
                int itemStart = items[i].Index;
                int itemEnd = i + 1 < items.Count ? items[i + 1].Index : block.Length;
                string segment = block.Substring(itemStart, itemEnd - itemStart);
                Match link = ItemLinkRegex.Match(segment);
                if (!link.Success)
                {
                    continue;
                }
                string slug = HtmlDecode(link.Groups[1].Value).Trim();
                if (slug.Length == 0)
                {
                    continue;
                }
                int rank = 0;
                for (int r = 0; r < rankPositions.Count; r++)
                {
                    if (rankPositions[r] < itemStart)
                    {
                        rank = rankValues[r];
                    }
                    else
                    {
                        break;
                    }
                }
                string name = "";
                Match nameMatch = ItemNameRegex.Match(segment);
                if (nameMatch.Success)
                {
                    name = HtmlDecode(nameMatch.Groups[1].Value).Replace('\u00A0', ' ').Trim();
                }
                VehicleMetaUnit unit = new VehicleMetaUnit();
                unit.Slug = slug;
                unit.Rank = rank;
                unit.Name = name;
                unit.Nation = nation;
                unit.Branch = branch;
                units.Add(unit);
            }
            return units;
        }

        private static string HtmlDecode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                       .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
        }

        /// <summary>解析 Rank 与三档 BR；BR 取 Rank 之后最先出现的 AB/RB/SB 三档。</summary>
        public static bool ParseUnitPage(string html, out int rank, out double? ab, out double? rb, out double? sb)
        {
            rank = 0;
            ab = null;
            rb = null;
            sb = null;
            if (string.IsNullOrEmpty(html))
            {
                return false;
            }
            Match rankMatch = RankRegex.Match(html);
            int from = 0;
            if (rankMatch.Success)
            {
                rank = VehicleMeta.ParseRankNumber(rankMatch.Groups[1].Value);
                from = rankMatch.Index;
            }
            MatchCollection matches = BrItemRegex.Matches(html);
            int found = 0;
            int scanned = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Index < from)
                {
                    continue;
                }
                scanned++;
                if (scanned > 6)
                {
                    break;
                }
                string mode = match.Groups[1].Value.ToUpperInvariant();
                double? value = VehicleMeta.ParseBr(match.Groups[2].Value);
                if (mode == "AB" && !ab.HasValue)
                {
                    ab = value;
                    found++;
                }
                else if (mode == "RB" && !rb.HasValue)
                {
                    rb = value;
                    found++;
                }
                else if (mode == "SB" && !sb.HasValue)
                {
                    sb = value;
                    found++;
                }
                if (found >= 3)
                {
                    break;
                }
            }
            return rank > 0 || rb.HasValue;
        }

        private static void Report(Action<string> progress, string text)
        {
            if (progress != null)
            {
                try
                {
                    progress(text);
                }
                catch
                {
                }
            }
        }

        private static void PrepareTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
            }
            catch
            {
            }
        }

        private static string Fetch(string url, out string error)
        {
            error = null;
            for (int attempt = 0; attempt <= MaxRetry; attempt++)
            {
                WaitGap();
                HttpWebResponse response = null;
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = TimeoutMs;
                    request.ReadWriteTimeout = TimeoutMs;
                    request.UserAgent = UserAgent;
                    request.AllowAutoRedirect = true;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    request.KeepAlive = false;
                    try
                    {
                        request.Accept = "text/html,application/xhtml+xml";
                    }
                    catch
                    {
                    }
                    response = (HttpWebResponse)request.GetResponse();
                    using (Stream stream = response.GetResponseStream())
                    {
                        if (stream == null)
                        {
                            error = "空响应";
                            continue;
                        }
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                catch (WebException ex)
                {
                    error = ex.Message;
                    HttpWebResponse failure = ex.Response as HttpWebResponse;
                    if (failure != null)
                    {
                        try
                        {
                            int code = (int)failure.StatusCode;
                            if (code == 404 || code == 403 || code == 410)
                            {
                                error = "HTTP " + code;
                                failure.Close();
                                return null;
                            }
                        }
                        catch
                        {
                        }
                        try
                        {
                            failure.Close();
                        }
                        catch
                        {
                        }
                    }
                    if (ex.Status == WebExceptionStatus.NameResolutionFailure ||
                        ex.Status == WebExceptionStatus.ProxyNameResolutionFailure ||
                        ex.Status == WebExceptionStatus.ConnectFailure)
                    {
                        error = "网络不可用";
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                finally
                {
                    if (response != null)
                    {
                        try
                        {
                            response.Close();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            return null;
        }

        private static void WaitGap()
        {
            lock (_gate)
            {
                if (_lastRequest != DateTime.MinValue)
                {
                    int elapsed = (int)(DateTime.UtcNow - _lastRequest).TotalMilliseconds;
                    int wait = MinGapMs - elapsed;
                    if (wait > 0)
                    {
                        Thread.Sleep(wait);
                    }
                }
                _lastRequest = DateTime.UtcNow;
            }
        }

        private static Dictionary<string, string> LoadFailed()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = VehicleMeta.FailedPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return map;
            }
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 3)
                    {
                        map[parts[0].Trim()] = parts[1].Trim() + "|" + parts[2].Trim();
                    }
                }
            }
            catch
            {
            }
            return map;
        }

        private static bool IsPermanent(string failure)
        {
            return failure != null &&
                   (failure.IndexOf("HTTP 404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    failure.IndexOf("HTTP 403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    failure.IndexOf("HTTP 410", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool FailedRecently(string slug)
        {
            string value;
            if (!LoadFailed().TryGetValue(slug, out value))
            {
                return false;
            }
            int bar = value.IndexOf('|');
            string time = bar > 0 ? value.Substring(0, bar) : "";
            DateTime when;
            if (!DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out when))
            {
                return false;
            }
            return (DateTime.Now - when).TotalDays < PermanentFailureDays;
        }

        private static void RecordFailed(string slug, string reason)
        {
            try
            {
                string path = VehicleMeta.FailedPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                Directory.CreateDirectory(Config.CacheDir);
                string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                List<string> lines = new List<string>();
                bool replaced = false;
                if (File.Exists(path))
                {
                    string[] existing = File.ReadAllLines(path, Encoding.UTF8);
                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i].StartsWith(slug + "\t", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!replaced)
                            {
                                lines.Add(slug + "\t" + time + "\t" + reason);
                                replaced = true;
                            }
                            continue;
                        }
                        lines.Add(existing[i]);
                    }
                }
                if (!replaced)
                {
                    lines.Add(slug + "\t" + time + "\t" + reason);
                }
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static void RemoveFailed(string slug)
        {
            try
            {
                string path = VehicleMeta.FailedPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                List<string> kept = new List<string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith(slug + "\t", StringComparison.OrdinalIgnoreCase))
                    {
                        kept.Add(lines[i]);
                    }
                }
                File.WriteAllLines(path, kept.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
