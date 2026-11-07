using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WtSkin
{
    /// <summary>
    /// 名称映射：国家显示名、载具简称。数据来自程序目录下可编辑的 shortnames.ini。
    /// 映射表不写在界面代码里；文件缺失时从内置默认表生成一份。
    ///
    /// 简称优先级（可在 shortnames.ini 的 [rules] 里开关）：
    ///   1. [short] 手工配置（按 slug 或中文全名）；
    ///   2. 已去掉类别前缀的中文简称，且长度不超过配置上限；
    ///   3. 上一轮 Wiki 采集数据里按 slug 精确对齐的官方名称；
    ///   4. 游戏内全名去掉类别前缀、取变体斜杠前；
    ///   5. 回退全名，并写入 shortnames_unmatched.txt。
    /// </summary>
    internal static class NameMap
    {
        private static Dictionary<string, string> _nations;
        private static Dictionary<string, string> _shortById;
        private static Dictionary<string, string> _shortByName;
        private static List<string> _prefixes;
        private static bool _loaded;

        private static bool _preserveChinese = true;
        private static int _chineseMaxLength = 12;
        private static bool _useOfficialName = true;
        private static bool _uniqueTitles = true;
        private static bool _lowerVariant = true;
        private static readonly HashSet<string> _missLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _missSync = new object();

        public static string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return Path.Combine(Config.InstallDir, "shortnames.ini");
            }
        }

        /// <summary>回退全名的载具清单；只用于人工复核，不参与渲染。</summary>
        public static string UnmatchedPath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return Path.Combine(Config.InstallDir, "shortnames_unmatched.txt");
            }
        }

        /// <summary>程序目录里没有映射表时，写出内置默认表；已有文件绝不覆盖。</summary>
        public static void EnsureFile()
        {
            try
            {
                string path = FilePath;
                if (string.IsNullOrEmpty(path) || File.Exists(path))
                {
                    return;
                }
                if (!Directory.Exists(Config.InstallDir))
                {
                    return;
                }
                using (Stream stream = typeof(NameMap).Assembly.GetManifestResourceStream("shortnames.dat"))
                {
                    if (stream == null)
                    {
                        return;
                    }
                    using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static void Reload()
        {
            _loaded = false;
            UserOverrides.Reload();
            lock (_missSync)
            {
                _missLogged.Clear();
            }
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            _nations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _shortById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _shortByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _prefixes = new List<string>();
            _preserveChinese = true;
            _chineseMaxLength = 12;
            _useOfficialName = true;
            _uniqueTitles = true;
            _lowerVariant = true;

            // 先加载内置默认表，再由外部文件覆盖：这样新版本新增的映射对老安装也生效，
            // 同时保留用户对外部文件的逐条修改。
            try
            {
                using (Stream embedded = typeof(NameMap).Assembly.GetManifestResourceStream("shortnames.dat"))
                {
                    if (embedded != null)
                    {
                        using (StreamReader reader = new StreamReader(embedded, Encoding.UTF8))
                        {
                            Parse(reader.ReadToEnd().Replace("\r\n", "\n").Split('\n'));
                        }
                    }
                }
            }
            catch
            {
            }

            string path = FilePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    Parse(File.ReadAllLines(path, Encoding.UTF8));
                }
                catch
                {
                }
            }
            // 用户覆盖表优先：先读内置清单与 shortnames.ini，再用它覆盖同名条目。
            UserOverrides.ApplyNations(_nations);
            UserOverrides.ApplyShortNames(_shortById, _shortByName);
            if (_prefixes.Count == 0)
            {
                AddPrefix("120 毫米火炮履带式战斗坦克");
                AddPrefix("105 毫米火炮履带式战斗坦克");
                AddPrefix("90 毫米火炮履带式战斗坦克");
                AddPrefix("主战坦克");
                AddPrefix("中型坦克");
                AddPrefix("轻型坦克");
                AddPrefix("重型坦克");
                AddPrefix("自行火炮");
                AddPrefix("反坦克导弹发射车");
                AddPrefix("步兵战车");
                AddPrefix("装甲-坦克-主战");
                AddPrefix("装甲-步战-履带");
            }
        }

        private static void Parse(string[] lines)
        {
            string section = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }
                if (section == "prefixes")
                {
                    // 兼容旧版外部映射文件：key=value 写在 [prefixes] 之后时仍按简称映射处理
                    int legacyEq = line.IndexOf('=');
                    if (legacyEq > 0)
                    {
                        string legacyKey = line.Substring(0, legacyEq).Trim();
                        string legacyValue = line.Substring(legacyEq + 1).Trim();
                        if (legacyKey.Length > 0 && legacyValue.Length > 0)
                        {
                            _shortById[legacyKey] = legacyValue;
                        }
                        continue;
                    }
                    AddPrefix(line);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }
                if (section == "nations")
                {
                    _nations[key] = value;
                }
                else if (section == "short")
                {
                    _shortById[key] = value;
                    _shortByName[key] = value;
                }
                else if (section == "rules")
                {
                    ParseRule(key, value);
                }
            }
        }

        private static bool BoolValue(string value)
        {
            string text = (value == null ? "" : value.Trim()).ToLowerInvariant();
            return text == "1" || text == "true" || text == "yes" || text == "on" || text == "是";
        }

        private static void ParseRule(string key, string value)
        {
            string name = (key == null ? "" : key.Trim()).ToLowerInvariant();
            if (name == "preserve_chinese" || name == "chinese_short")
            {
                _preserveChinese = BoolValue(value);
            }
            else if (name == "use_official_name" || name == "official_name")
            {
                _useOfficialName = BoolValue(value);
            }
            else if (name == "unique_titles" || name == "dedupe")
            {
                _uniqueTitles = BoolValue(value);
            }
            else if (name == "variant_lower")
            {
                _lowerVariant = BoolValue(value);
            }
            else if (name == "chinese_max_length" || name == "max_chinese_length")
            {
                int max;
                if (int.TryParse(value, out max) && max >= 2 && max <= 40)
                {
                    _chineseMaxLength = max;
                }
            }
        }

        private static void AddPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return;
            }
            for (int i = 0; i < _prefixes.Count; i++)
            {
                if (string.Equals(_prefixes[i], prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            _prefixes.Add(prefix);
            _prefixes.Sort(delegate(string a, string b)
            {
                return b.Length.CompareTo(a.Length);
            });
        }

        /// <summary>国家代码对应的界面显示名；未配置时回退到内置名称。</summary>
        public static string NationName(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return "未知";
            }
            EnsureLoaded();
            string name;
            if (_nations.TryGetValue(code, out name) && !string.IsNullOrEmpty(name))
            {
                return name;
            }
            return NationInfo.BaseName(code);
        }

        /// <summary>载具简称：配置优先，其次中文简称、官方名、型号代号，最后回退全名。</summary>
        public static string Short(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                return "";
            }
            return Short(vehicle.Id, vehicle.Zh, "");
        }

        public static string Short(string unitId, string fullName, string fallback)
        {
            EnsureLoaded();
            string key = unitId == null ? "" : unitId.Trim();
            if (key.Length > 0)
            {
                string mapped;
                if (_shortById.TryGetValue(key, out mapped) && !string.IsNullOrEmpty(mapped))
                {
                    return mapped;
                }
            }
            string full = fullName == null ? "" : fullName.Trim();
            if (full.Length == 0 && fallback != null)
            {
                full = fallback.Trim();
            }
            if (full.Length > 0)
            {
                string mappedName;
                if (_shortByName.TryGetValue(full, out mappedName) && !string.IsNullOrEmpty(mappedName))
                {
                    return mappedName;
                }
            }

            string clean = Extract(full);

            // b. 已经是去掉类别前缀的短中文名：保留，避免把「苏-34」改成 Su-34。
            if (_preserveChinese && IsChineseShort(clean))
            {
                return clean;
            }

            // c. 上一轮 Wiki 采集的官方英文名（严格按本地 slug 对齐）。
            if (_useOfficialName && key.Length > 0)
            {
                string official = OfficialName(key);
                if (!string.IsNullOrEmpty(official))
                {
                    return official;
                }
            }

            // d. 从全名提取型号代号；变体斜杠只取前段，F/A-18E 这类名称里的斜杠不拆。
            string model = BeforeVariantSlash(clean);
            if (!string.IsNullOrEmpty(model) &&
                !string.Equals(model, full, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }

            // e. 回退全名，并记录到未命中清单；绝不抛错。
            string result = full;
            if (result.Length == 0)
            {
                result = fallback == null ? "" : fallback.Trim();
            }
            if (result.Length == 0)
            {
                result = key;
            }
            LogUnmatched(key, full, result);
            return result;
        }

        private static string OfficialName(string slug)
        {
            VehicleMetaEntry entry = VehicleMeta.FindBySlug(slug);
            if (entry == null)
            {
                entry = VehicleMeta.Find(slug);
            }
            if (entry == null)
            {
                return "";
            }
            string name = entry.DisplayName == null ? "" : entry.DisplayName.Trim();
            if (name.Length == 0 || name == "—")
            {
                return "";
            }
            return name;
        }

        /// <summary>去掉国家符号与装备类别前缀后的型号代号；取不到时返回原全名。</summary>
        public static string Extract(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return "";
            }
            string text = fullName.Trim();
            int start = 0;
            while (start < text.Length && !char.IsLetterOrDigit(text[start]) && text[start] != ' ')
            {
                start++;
            }
            if (start > 0)
            {
                text = text.Substring(start).Trim();
            }
            bool changed = true;
            while (changed)
            {
                changed = false;

                // 「装甲-坦克-主战 …」「装甲-步战-履带 …」这类组合类别前缀：
                // 第一个空格之前是类别，后面才是型号。
                if (text.StartsWith("装甲-", StringComparison.OrdinalIgnoreCase))
                {
                    int gap = -1;
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (text[i] == ' ' || text[i] == '　')
                        {
                            gap = i;
                            break;
                        }
                    }
                    if (gap > 0 && gap + 1 < text.Length)
                    {
                        text = text.Substring(gap + 1).TrimStart(' ', '　', '-', '/');
                        changed = true;
                        continue;
                    }
                }

                for (int i = 0; i < _prefixes.Count; i++)
                {
                    string prefix = _prefixes[i];
                    if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (text.Length == prefix.Length)
                    {
                        continue;
                    }
                    char next = text[prefix.Length];
                    if (next != ' ' && next != '　' && next != '-' && next != '/')
                    {
                        continue;
                    }
                    text = text.Substring(prefix.Length).TrimStart(' ', '　', '-', '/');
                    changed = true;
                    break;
                }
            }
            if (text.Length == 0)
            {
                return fullName.Trim();
            }
            return text;
        }

        private static bool IsChineseShort(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string value = text.Trim();
            if (value.Length == 0 || value.Length > _chineseMaxLength)
            {
                return false;
            }
            bool hasChinese = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (IsCjk(value[i]))
                {
                    hasChinese = true;
                    break;
                }
            }
            return hasChinese;
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);
        }

        /// <summary>找变体分隔斜杠：只认右侧紧邻中文字符的斜杠，避免拆开 F/A-18E。</summary>
        private static int VariantSlashIndex(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '/')
                {
                    continue;
                }
                if (i <= 0 || i + 1 >= text.Length)
                {
                    continue;
                }
                if (char.IsWhiteSpace(text[i - 1]) || char.IsWhiteSpace(text[i + 1]))
                {
                    continue;
                }
                if (IsCjk(text[i + 1]))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string BeforeVariantSlash(string text)
        {
            int slash = VariantSlashIndex(text);
            if (slash > 0)
            {
                return text.Substring(0, slash).Trim();
            }
            return text;
        }

        private static string VariantToken(string fullName)
        {
            int slash = VariantSlashIndex(fullName);
            if (slash <= 0)
            {
                return "";
            }
            string suffix = fullName.Substring(slash + 1).Trim();
            string[] generic = new string[]
            {
                "主动防御系统", "主动防御", "附加装甲系统", "附加装甲", "改进型", "早期型", "后期型"
            };
            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int i = 0; i < generic.Length; i++)
                {
                    if (suffix.EndsWith(generic[i], StringComparison.OrdinalIgnoreCase))
                    {
                        suffix = suffix.Substring(0, suffix.Length - generic[i].Length).Trim();
                        removed = true;
                    }
                }
            }
            return NormalizeKey(suffix);
        }

        private static string NormalizeKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c) || IsCjk(c))
                {
                    sb.Append(c);
                }
            }
            string value = sb.ToString();
            if (_lowerVariant)
            {
                value = value.ToLowerInvariant();
            }
            return value;
        }

        private static string ModelToken(InstalledSkin skin)
        {
            return ModelToken(skin == null ? "" : skin.UnitId);
        }

        private static string ModelToken(string unitId)
        {
            string id = unitId == null ? "" : unitId.Trim();
            if (id.Length == 0)
            {
                return "";
            }
            string[] parts = id.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return NormalizeKey(id);
            }
            return NormalizeKey(parts[parts.Length - 1]);
        }

        private static string FolderToken(InstalledSkin skin)
        {
            return FolderToken(skin == null ? "" : skin.Folder);
        }

        private static string FolderToken(string folder)
        {
            string value = folder == null ? "" : folder;
            if (value.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - InstalledSkins.BanSuffix.Length);
            }
            if (value.StartsWith("template_", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("template_".Length);
            }
            return NormalizeKey(value);
        }

        /// <summary>
        /// 给同一次列表扫描的载具分配唯一简称。冲突时优先用全名斜杠后的变体标识，
        /// 再退到机型代号差异段，最后用文件夹名兜底；保证没有两张标题完全相同。
        /// </summary>
        public static void ApplyUniqueTitles(IList<InstalledSkin> skins)
        {
            EnsureLoaded();
            if (skins == null)
            {
                return;
            }
            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i] != null)
                {
                    skins[i].ShortNameOverride = "";
                }
            }
            if (!_uniqueTitles || skins.Count < 2)
            {
                return;
            }

            Dictionary<string, List<InstalledSkin>> groups =
                new Dictionary<string, List<InstalledSkin>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < skins.Count; i++)
            {
                InstalledSkin skin = skins[i];
                if (skin == null)
                {
                    continue;
                }
                string baseName = skin.BaseShortName;
                List<InstalledSkin> group;
                if (!groups.TryGetValue(baseName, out group))
                {
                    group = new List<InstalledSkin>();
                    groups[baseName] = group;
                }
                group.Add(skin);
            }

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<List<InstalledSkin>> duplicateGroups = new List<List<InstalledSkin>>();
            foreach (KeyValuePair<string, List<InstalledSkin>> pair in groups)
            {
                if (pair.Value.Count == 1)
                {
                    used.Add(pair.Key);
                }
                else
                {
                    duplicateGroups.Add(pair.Value);
                }
            }

            for (int g = 0; g < duplicateGroups.Count; g++)
            {
                List<InstalledSkin> group = duplicateGroups[g];
                int anchor = -1;
                for (int i = 0; i < group.Count; i++)
                {
                    // 没有变体斜杠的那台保留原简称作为基准，例如 M1A2 SEPv2。
                    // 有斜杠的变体才追加「/战利品hv」之类的标识。
                    if (VariantToken(group[i].FullName).Length == 0)
                    {
                        anchor = i;
                        break;
                    }
                }
                for (int i = 0; i < group.Count; i++)
                {
                    InstalledSkin skin = group[i];
                    string baseName = skin.BaseShortName.Trim().TrimEnd('/');
                    if (i == anchor && used.Add(baseName))
                    {
                        continue;
                    }
                    string token = VariantToken(skin.FullName);
                    if (token.Length == 0)
                    {
                        token = ModelToken(skin);
                    }
                    string candidate = baseName + "/" + (token.Length == 0 ? "variant" : token);
                    if (used.Contains(candidate))
                    {
                        token = ModelToken(skin);
                        candidate = baseName + "/" + (token.Length == 0 ? NormalizeKey(skin.UnitId) : token);
                    }
                    if (used.Contains(candidate))
                    {
                        token = NormalizeKey(skin.UnitId);
                        candidate = baseName + "/" + (token.Length == 0 ? "variant" : token);
                    }
                    if (used.Contains(candidate))
                    {
                        token = FolderToken(skin);
                        candidate = baseName + "/" + (token.Length == 0 ? "variant" : token);
                    }
                    int n = 2;
                    string unique = candidate;
                    while (used.Contains(unique))
                    {
                        unique = candidate + "-" + n.ToString();
                        n++;
                    }
                    used.Add(unique);
                    skin.ShortNameOverride = unique;
                }
            }
        }

        /// <summary>
        /// 导入页的去重版本：同一涂装包内若出现同名变体，也按相同规则加变体后缀。
        /// </summary>
        public static void ApplyUniqueTitles(IList<DetectedUnit> units)
        {
            EnsureLoaded();
            if (units == null)
            {
                return;
            }
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    units[i].ShortTitleOverride = "";
                }
            }
            if (!_uniqueTitles || units.Count < 2)
            {
                return;
            }

            Dictionary<string, List<DetectedUnit>> groups =
                new Dictionary<string, List<DetectedUnit>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < units.Count; i++)
            {
                DetectedUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }
                string baseName = unit.BaseShortTitle;
                List<DetectedUnit> group;
                if (!groups.TryGetValue(baseName, out group))
                {
                    group = new List<DetectedUnit>();
                    groups[baseName] = group;
                }
                group.Add(unit);
            }

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<List<DetectedUnit>> duplicateGroups = new List<List<DetectedUnit>>();
            foreach (KeyValuePair<string, List<DetectedUnit>> pair in groups)
            {
                if (pair.Value.Count == 1)
                {
                    used.Add(pair.Key);
                }
                else
                {
                    duplicateGroups.Add(pair.Value);
                }
            }

            for (int g = 0; g < duplicateGroups.Count; g++)
            {
                List<DetectedUnit> group = duplicateGroups[g];
                int anchor = -1;
                for (int i = 0; i < group.Count; i++)
                {
                    if (VariantToken(group[i].Title).Length == 0)
                    {
                        anchor = i;
                        break;
                    }
                }
                for (int i = 0; i < group.Count; i++)
                {
                    DetectedUnit unit = group[i];
                    string baseName = unit.BaseShortTitle.Trim().TrimEnd('/');
                    if (i == anchor && used.Add(baseName))
                    {
                        continue;
                    }
                    string token = VariantToken(unit.Title);
                    if (token.Length == 0)
                    {
                        token = ModelToken(unit.UnitId);
                    }
                    string candidate = baseName + "/" + (token.Length == 0 ? "variant" : token);
                    if (used.Contains(candidate))
                    {
                        token = ModelToken(unit.UnitId);
                        candidate = baseName + "/" + (token.Length == 0 ? NormalizeKey(unit.UnitId) : token);
                    }
                    if (used.Contains(candidate))
                    {
                        token = NormalizeKey(unit.UnitId);
                        candidate = baseName + "/" + (token.Length == 0 ? "variant" : token);
                    }
                    int n = 2;
                    string unique = candidate;
                    while (used.Contains(unique))
                    {
                        unique = candidate + "-" + n.ToString();
                        n++;
                    }
                    used.Add(unique);
                    unit.ShortTitleOverride = unique;
                }
            }
        }

        private static void LogUnmatched(string unitId, string fullName, string shown)
        {
            try
            {
                string path = UnmatchedPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                string key = (unitId == null ? "" : unitId) + "\t" + (fullName == null ? "" : fullName);
                lock (_missSync)
                {
                    if (!_missLogged.Add(key))
                    {
                        return;
                    }
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 简称未命中：slug\t游戏内全名\t当前回退值");
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                }
                string line = (unitId == null ? "" : unitId) + "\t" +
                              (fullName == null ? "" : fullName) + "\t" +
                              (shown == null ? "" : shown) + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
