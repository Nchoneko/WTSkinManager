using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WtSkin
{
    internal class PackageEntry
    {
        public string Rel;
        public long Length;
        public int ZipIndex = -1;
        public string FullPath;
    }

    internal class DetectedUnit
    {
        public string UnitId;
        public Vehicle Vehicle;
        public int Confidence;
        public bool Manual;
        public bool Selected = true;
        public string BlkRel;
        /// <summary>导入列表去重后的简称；由 NameMap.ApplyUniqueTitles 写入。</summary>
        public string ShortTitleOverride = "";

        public string Title
        {
            get
            {
                if (Vehicle != null && !string.IsNullOrEmpty(Vehicle.Zh))
                {
                    return Vehicle.Zh;
                }
                return UnitId;
            }
        }

        /// <summary>界面显示用简称（完整名仍保留在 Title 里）。</summary>
        public string ShortTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(ShortTitleOverride))
                {
                    return ShortTitleOverride;
                }
                return BaseShortTitle;
            }
        }

        /// <summary>未做列表去重的简称。</summary>
        public string BaseShortTitle
        {
            get
            {
                if (Vehicle != null)
                {
                    string shortName = NameMap.Short(Vehicle);
                    if (!string.IsNullOrEmpty(shortName))
                    {
                        return shortName;
                    }
                }
                return Title;
            }
        }
    }

    /// <summary>一个待导入的涂装包（zip 或文件夹），负责内容枚举与载具识别。</summary>
    internal class SkinPackage
    {
        internal const int MaxFiles = 5000;
        internal const long MaxSingleBytes = 512L * 1024 * 1024;
        internal const long MaxTotalBytes = 4L * 1024 * 1024 * 1024;

        private static readonly Regex FromRegex = new Regex("from\\s*:\\s*t\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string SourcePath;
        public bool IsZip;
        public string Name = "";
        /// <summary>
        /// 导入时「涂装文件夹名」的默认值，取自包内容本身：
        /// 压缩包内唯一顶层目录名 → 压缩包文件名（去扩展名）→ 空（由用户填写）。
        /// 文件夹来源直接用它自己的文件夹名。保留原始写法，不在推导时清洗，
        /// 这样导入对话框能对非法默认名给出明确提示。
        /// </summary>
        public string TopFolderName = "";
        public List<PackageEntry> Files = new List<PackageEntry>();
        public long TotalBytes;
        public string Fingerprint = "";
        public List<DetectedUnit> Units = new List<DetectedUnit>();
        public List<string> UnknownIds = new List<string>();
        public bool Ready;
        public string Error = "";

        public string SizeText
        {
            get { return Util.HumanSize(TotalBytes); }
        }

        public string FilesText
        {
            get { return Util.Count(Files.Count) + " 个文件"; }
        }

        public static SkinPackage Analyze(string path)
        {
            SkinPackage package = new SkinPackage();
            package.SourcePath = path;
            try
            {
                if (Directory.Exists(path))
                {
                    package.IsZip = false;
                    package.AnalyzeFolder(path);
                }
                else if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".zip")
                    {
                        package.IsZip = true;
                        package.AnalyzeZip(path);
                    }
                    else
                    {
                        package.Error = "只支持 .zip 压缩包或已解压的文件夹";
                        return package;
                    }
                }
                else
                {
                    package.Error = "路径不存在";
                    return package;
                }

                if (package.Files.Count == 0)
                {
                    if (string.IsNullOrEmpty(package.Error))
                    {
                        package.Error = "包里没有可导入的文件";
                    }
                    return package;
                }
                package.DetectUnits();
                package.ComputeFingerprint();
                package.Ready = true;
            }
            catch (Exception ex)
            {
                package.Error = FriendlyError(ex);
            }
            return package;
        }

        private static string FriendlyError(Exception ex)
        {
            string message = ex.Message;
            if (ex is UnauthorizedAccessException)
            {
                return "没有权限读取这个文件";
            }
            if (ex is IOException)
            {
                return "读取失败：" + message;
            }
            if (ex is InvalidDataException)
            {
                return "压缩包已损坏或格式不受支持";
            }
            return message;
        }

        private void AnalyzeZip(string path)
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                List<PackageEntry> raw = new List<PackageEntry>();
                for (int i = 0; i < zip.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = zip.Entries[i];
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                        entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (entry.Length == 0 && entry.Name.Length == 0)
                    {
                        continue;
                    }
                    PackageEntry pe = new PackageEntry();
                    pe.Rel = NormalizeRel(FixZipName(entry.FullName));
                    pe.Length = entry.Length;
                    pe.ZipIndex = i;
                    if (IsJunk(pe.Rel))
                    {
                        continue;
                    }
                    raw.Add(pe);
                }

                string root = FindCommonRoot(raw);
                foreach (PackageEntry pe in raw)
                {
                    if (root.Length > 0)
                    {
                        pe.Rel = pe.Rel.Substring(root.Length).TrimStart('/');
                    }
                    if (pe.Rel.Length == 0)
                    {
                        continue;
                    }
                    if (!Util.IsSafeRel(pe.Rel))
                    {
                        Error = "压缩包包含不安全的路径：" + pe.Rel;
                        Files.Clear();
                        TotalBytes = 0;
                        return;
                    }
                    if (pe.Length > MaxSingleBytes || Files.Count + 1 > MaxFiles || TotalBytes + pe.Length > MaxTotalBytes)
                    {
                        Error = "压缩包内容超过安全上限";
                        Files.Clear();
                        TotalBytes = 0;
                        return;
                    }
                    Files.Add(pe);
                    TotalBytes += pe.Length;
                }

                string zipName = Path.GetFileNameWithoutExtension(path);
                Name = Util.SanitizeName(root.Length > 0 ? root : zipName);
                TopFolderName = root.Length > 0 ? root : zipName;
            }
        }

        private void AnalyzeFolder(string path)
        {
            string[] all;
            try
            {
                all = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                all = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
            }
            string basePath = path.TrimEnd('\\');
            for (int i = 0; i < all.Length; i++)
            {
                string rel = all[i].Substring(basePath.Length).TrimStart('\\');
                rel = NormalizeRel(rel);
                if (IsJunk(rel))
                {
                    continue;
                }
                PackageEntry pe = new PackageEntry();
                pe.Rel = rel;
                pe.FullPath = all[i];
                try
                {
                    pe.Length = new FileInfo(all[i]).Length;
                }
                catch
                {
                    pe.Length = 0;
                }
                if (!Util.IsSafeRel(pe.Rel))
                {
                    Error = "文件夹包含不安全的路径：" + pe.Rel;
                    Files.Clear();
                    TotalBytes = 0;
                    return;
                }
                if (pe.Length > MaxSingleBytes || Files.Count + 1 > MaxFiles || TotalBytes + pe.Length > MaxTotalBytes)
                {
                    Error = "文件夹内容超过安全上限";
                    Files.Clear();
                    TotalBytes = 0;
                    return;
                }
                Files.Add(pe);
                TotalBytes += pe.Length;
            }
            Name = Util.SanitizeName(Path.GetFileName(basePath));
            TopFolderName = Path.GetFileName(basePath);
        }

        private static string NormalizeRel(string rel)
        {
            string r = rel.Replace('\\', '/');
            while (r.StartsWith("./", StringComparison.Ordinal))
            {
                r = r.Substring(2);
            }
            return r.TrimStart('/');
        }

        private static bool IsJunk(string rel)
        {
            if (string.IsNullOrEmpty(rel))
            {
                return true;
            }
            string[] parts = rel.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0)
                {
                    return true;
                }
                if (p == "__MACOSX" || p.StartsWith("._", StringComparison.Ordinal) ||
                    p.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string FindCommonRoot(List<PackageEntry> raw)
        {
            string root = null;
            for (int i = 0; i < raw.Count; i++)
            {
                string rel = raw[i].Rel;
                int slash = rel.IndexOf('/');
                if (slash <= 0)
                {
                    return "";
                }
                string first = rel.Substring(0, slash);
                if (root == null)
                {
                    root = first;
                }
                else if (!string.Equals(root, first, StringComparison.OrdinalIgnoreCase))
                {
                    return "";
                }
            }
            return root == null ? "" : root;
        }

        /// <summary>zip 里非 UTF-8 标记的中文名常被按 CP437 解码，这里尝试还原成 GBK。</summary>
        private static string FixZipName(string name)
        {
            bool hasHigh = false;
            bool hasWide = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c > '\u00FF')
                {
                    hasWide = true;
                    break;
                }
                if (c >= '\u0080')
                {
                    hasHigh = true;
                }
            }
            if (hasWide || !hasHigh)
            {
                return name;
            }
            try
            {
                Encoding cp437 = Encoding.GetEncoding(437);
                Encoding gbk = Encoding.GetEncoding(936);
                byte[] bytes = cp437.GetBytes(name);
                string decoded = gbk.GetString(bytes);
                if (decoded.IndexOf('\uFFFD') < 0)
                {
                    return decoded;
                }
            }
            catch
            {
            }
            return name;
        }

        private void DetectUnits()
        {
            Dictionary<string, DetectedUnit> seen = new Dictionary<string, DetectedUnit>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Files.Count; i++)
            {
                PackageEntry pe = Files[i];
                if (!pe.Rel.EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string rawId = Path.GetFileNameWithoutExtension(pe.Rel).Trim();
                if (rawId.Length == 0)
                {
                    continue;
                }
                string id = rawId.ToLowerInvariant();
                Vehicle vehicle = VehicleDb.Find(id);
                int confidence = vehicle != null ? 100 : 0;
                string unitId = vehicle != null ? vehicle.Id : id;

                if (vehicle == null)
                {
                    List<string> contentIds = ReadBlkIds(pe);
                    for (int c = 0; c < contentIds.Count; c++)
                    {
                        Vehicle byContent = VehicleDb.Find(contentIds[c]);
                        if (byContent != null)
                        {
                            vehicle = byContent;
                            unitId = byContent.Id;
                            confidence = 80;
                            break;
                        }
                    }
                }
                if (vehicle == null)
                {
                    int suggestConfidence;
                    Vehicle suggestion = VehicleDb.Suggest(id, out suggestConfidence);
                    if (suggestion != null && suggestConfidence >= 84)
                    {
                        vehicle = suggestion;
                        unitId = suggestion.Id;
                        confidence = suggestConfidence;
                    }
                }

                if (vehicle == null)
                {
                    if (!UnknownIds.Contains(id))
                    {
                        UnknownIds.Add(id);
                    }
                    continue;
                }

                DetectedUnit existing;
                if (seen.TryGetValue(unitId, out existing))
                {
                    if (existing.Confidence < confidence)
                    {
                        existing.Confidence = confidence;
                        existing.BlkRel = pe.Rel;
                    }
                    continue;
                }
                DetectedUnit unit = new DetectedUnit();
                unit.UnitId = VehicleDb.CanonicalId(unitId);
                unit.Vehicle = vehicle;
                unit.Confidence = confidence;
                unit.BlkRel = pe.Rel;
                seen[unitId] = unit;
                Units.Add(unit);
            }

            Units.Sort(delegate(DetectedUnit a, DetectedUnit b)
            {
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
            });
        }

        private List<string> ReadBlkIds(PackageEntry pe)
        {
            List<string> ids = new List<string>();
            try
            {
                string text = ReadText(pe, 65536);
                MatchCollection matches = FromRegex.Matches(text);
                for (int i = 0; i < matches.Count && ids.Count < 40; i++)
                {
                    string from = matches[i].Groups[1].Value.Trim().ToLowerInvariant();
                    int star = from.IndexOf('*');
                    if (star >= 0)
                    {
                        from = from.Substring(0, star);
                    }
                    from = from.Replace('/', '_').Replace('\\', '_');
                    string[] tails = new string[] { "_c", "_n", "_add", "_add_n", "_mirror_n", "_detail", "_dmg" };
                    bool stripped = true;
                    while (stripped)
                    {
                        stripped = false;
                        for (int t = 0; t < tails.Length; t++)
                        {
                            if (from.EndsWith(tails[t], StringComparison.Ordinal) && from.Length > tails[t].Length)
                            {
                                from = from.Substring(0, from.Length - tails[t].Length);
                                stripped = true;
                            }
                        }
                    }
                    if (from.Length > 1 && !ids.Contains(from))
                    {
                        ids.Add(from);
                    }
                }
            }
            catch
            {
            }
            return ids;
        }

        private string ReadText(PackageEntry pe, int maxBytes)
        {
            Stream stream;
            if (IsZip)
            {
                using (ZipArchive zip = ZipFile.OpenRead(SourcePath))
                {
                    stream = zip.Entries[pe.ZipIndex].Open();
                    using (stream)
                    {
                        return ReadLimited(stream, maxBytes);
                    }
                }
            }
            using (FileStream fs = File.OpenRead(Util.LongPath(pe.FullPath)))
            {
                return ReadLimited(fs, maxBytes);
            }
        }

        private static string ReadLimited(Stream stream, int maxBytes)
        {
            byte[] buffer = new byte[maxBytes];
            int total = 0;
            int read;
            while (total < maxBytes && (read = stream.Read(buffer, total, maxBytes - total)) > 0)
            {
                total += read;
            }
            return Encoding.UTF8.GetString(buffer, 0, total);
        }

        private void ComputeFingerprint()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < Files.Count; i++)
            {
                lines.Add(Files[i].Rel.ToLowerInvariant() + ":" + Files[i].Length.ToString());
            }
            lines.Sort(StringComparer.Ordinal);
            try
            {
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines.ToArray())));
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("x2"));
                    }
                    Fingerprint = sb.ToString();
                }
            }
            catch
            {
                Fingerprint = "";
            }
        }

        public List<DetectedUnit> SelectedUnits()
        {
            List<DetectedUnit> result = new List<DetectedUnit>();
            for (int i = 0; i < Units.Count; i++)
            {
                if (Units[i].Selected)
                {
                    result.Add(Units[i]);
                }
            }
            return result;
        }

        /// <summary>包里第一个 .blk 的相对路径，手动指定载具时用它来改名。</summary>
        public string FirstBlkRel()
        {
            for (int i = 0; i < Files.Count; i++)
            {
                if (Files[i].Rel.EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
                {
                    return Files[i].Rel;
                }
            }
            return "";
        }
    }

    /// <summary>
    /// 「涂装文件夹名」规则：用于界面实时校验，也用于写入前再次校验（安全兜底）。
    /// 只允许一层普通文件夹名：非空、≤64 字符、不含 Windows 非法字符、不以空格/句点结尾、
    /// 不是 Windows 保留名、不包含「..」。
    /// </summary>
    internal static class SkinNameRules
    {
        public const int MaxLength = 64;

        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>返回空字符串表示合法，否则返回给界面显示的原因。</summary>
        public static string Validate(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0)
            {
                return "请填写涂装文件夹名";
            }
            if (name.Length > MaxLength)
            {
                return "涂装文件夹名不能超过 " + MaxLength.ToString() + " 个字符";
            }
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c < ' ' || c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|')
                {
                    return "不能包含 \\ / : * ? \" < > | 或控制字符";
                }
            }
            char last = name[name.Length - 1];
            if (last == ' ' || last == '.')
            {
                return "名称结尾不能是空格或句点";
            }
            if (name.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return "名称不能包含「..」";
            }
            // Windows 把「CON」「CON.txt」都当作保留名；保留名比较取第一个点之前的部分。
            string stem = name;
            int dot = stem.IndexOf('.');
            if (dot >= 0)
            {
                stem = stem.Substring(0, dot);
            }
            stem = stem.TrimEnd(' ');
            for (int i = 0; i < ReservedNames.Length; i++)
            {
                if (string.Equals(stem, ReservedNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return "不能使用 Windows 保留名（CON、PRN、AUX、NUL、COM1-9、LPT1-9）";
                }
            }
            return "";
        }

        public static bool IsValid(string name)
        {
            return Validate(name).Length == 0;
        }
    }
}
