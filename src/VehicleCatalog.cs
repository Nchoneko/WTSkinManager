using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WtSkin
{
    /// <summary>
    /// 清单里的一条载具图标记录。清单（manifest.json）是运行期唯一数据源：
    /// 界面只按 slug 查这张表，绝不扫描图标目录，也绝不联网取图。
    /// attemptCount / lastError / lastAttemptAt / status 同时承担断点续跑与永久失败的状态，
    /// 并在同一个文件里保存等级权重的采集状态（meta* 字段），重启后不丢。
    /// </summary>
    internal class VehicleIconEntry
    {
        public string Slug = "";
        /// <summary>实际文件名；sha256 相同时指向同一份文件（去重）。</summary>
        public string File = "";
        public long Bytes;
        public string Sha256 = "";
        public string Nation = "";
        public string Branch = "";
        public int Rank;
        public string Name = "";
        /// <summary>图标采集时间 yyyy-MM-dd HH:mm:ss。</summary>
        public string Collected = "";
        /// <summary>跨任务累计请求次数，达到 5 标 permanent。</summary>
        public int AttemptCount;
        public string LastError = "";
        public string LastAttemptAt = "";
        /// <summary>ok / retrying / permanent。</summary>
        public string Status = StatusRetrying;

        // ---- 等级权重共用同一份状态记录 ----
        /// <summary>none / ok / retrying / permanent。</summary>
        public string MetaStatus = "none";
        public int MetaAttemptCount;
        public string MetaLastError = "";
        public string MetaLastAttemptAt = "";

        public const string StatusOk = "ok";
        public const string StatusRetrying = "retrying";
        public const string StatusPermanent = "permanent";

        public bool IsOk { get { return Status == StatusOk; } }
        public bool IsPermanent { get { return Status == StatusPermanent; } }
    }

    /// <summary>载具图标清单：读写、去重、覆盖率统计、缺失导出。</summary>
    internal static class VehicleCatalog
    {
        public const int MaxAttempts = 5;
        public const int MaxAttemptsPerTask = 2;
        public const int ProbeDeferAbove = 100;
        public const string ManifestName = "manifest.json";
        public const string PlaceholderName = "_placeholder.png";
        public const long MaxIconBytes = 120 * 1024;
        public const long TotalBudgetBytes = 350L * 1024 * 1024;

        private static readonly object Sync = new object();
        private static Dictionary<string, VehicleIconEntry> _bySlug =
            new Dictionary<string, VehicleIconEntry>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _bySha =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _order = new List<string>();
        private static bool _loaded;
        private static string _dir = "";
        private static string _version = "—";
        private static string _updated = "—";
        private static string _generator = "";
        private static string _lastError = "";
        private static int _schemaVersion;
        private static string _dataVersion = "";

        public static string Version { get { EnsureLoaded(); return _version; } }
        public static string Updated { get { EnsureLoaded(); return _updated; } }
        public static string Generator { get { EnsureLoaded(); return _generator; } }
        public static int SchemaVersion { get { EnsureLoaded(); return _schemaVersion; } }
        public static string DataVersion { get { EnsureLoaded(); return _dataVersion; } }
        public static string LastError { get { return _lastError; } }

        /// <summary>图标目录：优先 exe 同级的 assets\vehicles，其次安装目录。</summary>
        public static string Dir
        {
            get
            {
                lock (Sync)
                {
                    if (_dir.Length > 0)
                    {
                        return _dir;
                    }
                    _dir = ResolveDir();
                    return _dir;
                }
            }
        }

        public static string ManifestPath
        {
            get { return Path.Combine(Dir, ManifestName); }
        }

        private static string ResolveDir()
        {
            List<string> candidates = new List<string>();
            try
            {
                candidates.Add(Path.Combine(Config.ExeDir, "assets", "vehicles"));
            }
            catch
            {
            }
            try
            {
                if (!string.IsNullOrEmpty(Config.InstallDir) && !DataFiles.ExternalManifestIgnored)
                {
                    candidates.Add(Path.Combine(Config.InstallDir, "assets", "vehicles"));
                }
            }
            catch
            {
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                try
                {
                    if (File.Exists(Path.Combine(candidates[i], ManifestName)))
                    {
                        return candidates[i];
                    }
                }
                catch
                {
                }
            }
            return candidates.Count > 0 ? candidates[0] : "";
        }

        public static void ResetDir()
        {
            lock (Sync)
            {
                _dir = "";
                _loaded = false;
            }
        }

        public static void Reload()
        {
            lock (Sync)
            {
                _loaded = false;
                _bySlug = null;
                _bySha = null;
                _order = null;
            }
            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (_loaded)
                {
                    return;
                }
                _loaded = true;
                _bySlug = new Dictionary<string, VehicleIconEntry>(StringComparer.OrdinalIgnoreCase);
                _bySha = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _order = new List<string>();
                _version = "—";
                _updated = "—";
                _generator = "";
                _lastError = "";
                string path = ManifestPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    _lastError = "缺少清单 " + ManifestName;
                    return;
                }
                try
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    object root = Json.Parse(text);
                    Dictionary<string, object> obj = root as Dictionary<string, object>;
                    if (obj == null)
                    {
                        _lastError = "清单格式不正确";
                        return;
                    }
                    _version = Json.Str(obj, "version");
                    _updated = Json.Str(obj, "updated");
                    _generator = Json.Str(obj, "generator");
                    _schemaVersion = (int)Json.Num(obj, "schemaVersion");
                    _dataVersion = Json.Str(obj, "dataVersion");
                    if (_schemaVersion > 0 && _schemaVersion != DataFiles.SupportedSchemaVersion)
                    {
                        _lastError = "清单 schemaVersion=" + _schemaVersion + " 与程序支持的 " +
                                     DataFiles.SupportedSchemaVersion + " 不一致，已忽略该文件";
                        Config.Log(_lastError + "：" + path);
                        return;
                    }
                    List<object> list = Json.Arr(obj, "entries");
                    for (int i = 0; i < list.Count; i++)
                    {
                        Dictionary<string, object> row = list[i] as Dictionary<string, object>;
                        if (row == null)
                        {
                            continue;
                        }
                        VehicleIconEntry e = FromJson(row);
                        if (e.Slug.Length == 0 || _bySlug.ContainsKey(e.Slug))
                        {
                            continue;
                        }
                        _bySlug[e.Slug] = e;
                        _order.Add(e.Slug);
                        if (e.IsOk && e.Sha256.Length > 0 && e.File.Length > 0 && !_bySha.ContainsKey(e.Sha256))
                        {
                            _bySha[e.Sha256] = e.File;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _lastError = "读取清单失败：" + ex.Message;
                    Config.Log(_lastError);
                }
            }
        }

        public static int Count
        {
            get { EnsureLoaded(); lock (Sync) { return _bySlug.Count; } }
        }

        public static List<VehicleIconEntry> Snapshot()
        {
            EnsureLoaded();
            lock (Sync)
            {
                List<VehicleIconEntry> list = new List<VehicleIconEntry>(_order.Count);
                for (int i = 0; i < _order.Count; i++)
                {
                    VehicleIconEntry e = _bySlug[_order[i]];
                    list.Add(e);
                }
                return list;
            }
        }

        public static List<string> SnapshotSlugs()
        {
            EnsureLoaded();
            lock (Sync) { return new List<string>(_order); }
        }

        public static VehicleIconEntry Get(string slug)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(slug))
            {
                return null;
            }
            lock (Sync)
            {
                VehicleIconEntry e;
                return _bySlug.TryGetValue(slug.Trim(), out e) ? e : null;
            }
        }

        /// <summary>把图标文件解析为绝对路径；清单里没有或文件不存在时返回空。</summary>
        public static string ResolveFile(VehicleIconEntry entry)
        {
            if (entry == null || entry.File.Length == 0 || !entry.IsOk)
            {
                return "";
            }
            try
            {
                string path = Path.Combine(Dir, entry.File);
                return File.Exists(path) ? path : "";
            }
            catch
            {
                return "";
            }
        }

        public static string PlaceholderPath
        {
            get
            {
                try
                {
                    string path = Path.Combine(Dir, PlaceholderName);
                    return File.Exists(path) ? path : "";
                }
                catch
                {
                    return "";
                }
            }
        }

        /// <summary>把科技树采集到的载具补进清单（已存在的不动）。</summary>
        public static int MergeUnits(List<VehicleMetaUnit> units)
        {
            if (units == null || units.Count == 0)
            {
                return 0;
            }
            EnsureLoaded();
            int added = 0;
            lock (Sync)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    VehicleMetaUnit u = units[i];
                    if (u == null || string.IsNullOrEmpty(u.Slug))
                    {
                        continue;
                    }
                    VehicleIconEntry e;
                    if (_bySlug.TryGetValue(u.Slug, out e))
                    {
                        if (e.Rank == 0 && u.Rank > 0)
                        {
                            e.Rank = u.Rank;
                        }
                        if (e.Nation.Length == 0)
                        {
                            e.Nation = u.Nation;
                        }
                        if (e.Branch.Length == 0)
                        {
                            e.Branch = u.Branch;
                        }
                        if (e.Name.Length == 0)
                        {
                            e.Name = u.Name;
                        }
                        continue;
                    }
                    e = new VehicleIconEntry();
                    e.Slug = u.Slug;
                    e.Nation = u.Nation;
                    e.Branch = u.Branch;
                    e.Rank = u.Rank;
                    e.Name = u.Name;
                    e.Status = VehicleIconEntry.StatusRetrying;
                    _bySlug[u.Slug] = e;
                    _order.Add(u.Slug);
                    added++;
                }
            }
            if (added > 0)
            {
                Save();
            }
            return added;
        }

        public static VehicleIconEntry EnsureEntry(string slug, string nation, string branch, int rank, string name)
        {
            EnsureLoaded();
            lock (Sync)
            {
                VehicleIconEntry e;
                if (_bySlug.TryGetValue(slug, out e))
                {
                    return e;
                }
                e = new VehicleIconEntry();
                e.Slug = slug;
                e.Nation = nation;
                e.Branch = branch;
                e.Rank = rank;
                e.Name = name;
                _bySlug[slug] = e;
                _order.Add(slug);
                return e;
            }
        }

        public static string Sha256OfFile(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
                return "";
            }
        }

        public static string Sha256OfBytes(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 写入一张已归一化的图标。相同 sha256 只落一份文件，清单指向同一份。
        /// 返回 true 表示没有新增文件（命中去重）。
        /// </summary>
        public static bool StoreIcon(VehicleIconEntry entry, byte[] data)
        {
            EnsureLoaded();
            lock (Sync)
            {
                string sha = Sha256OfBytes(data);
                string existing;
                if (_bySha.TryGetValue(sha, out existing))
                {
                    string full = Path.Combine(Dir, existing);
                    if (File.Exists(full))
                    {
                        entry.File = existing;
                        entry.Bytes = new FileInfo(full).Length;
                        entry.Sha256 = sha;
                        entry.Status = VehicleIconEntry.StatusOk;
                        entry.AttemptCount = 0;
                        entry.LastError = "";
                        entry.Collected = Now();
                        return true;
                    }
                }
                string file = entry.Slug + ".png";
                string path = Path.Combine(Dir, file);
                Directory.CreateDirectory(Dir);
                string temp = path + ".tmp";
                File.WriteAllBytes(temp, data);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
                entry.File = file;
                entry.Bytes = data.LongLength;
                entry.Sha256 = sha;
                entry.Status = VehicleIconEntry.StatusOk;
                entry.AttemptCount = 0;
                entry.LastError = "";
                entry.Collected = Now();
                _bySha[sha] = file;
                return false;
            }
        }

        public static void MarkAttempt(VehicleIconEntry entry, bool success, string error)
        {
            lock (Sync)
            {
                entry.AttemptCount++;
                entry.LastAttemptAt = Now();
                if (success)
                {
                    entry.Status = VehicleIconEntry.StatusOk;
                    entry.AttemptCount = 0;
                    entry.LastError = "";
                }
                else
                {
                    entry.LastError = Trim(error, 240);
                    if (entry.AttemptCount >= MaxAttempts)
                    {
                        entry.Status = VehicleIconEntry.StatusPermanent;
                    }
                    else
                    {
                        entry.Status = VehicleIconEntry.StatusRetrying;
                    }
                }
            }
        }

        /// <summary>永久失败探测：只更新错误与时间，不累加计数。</summary>
        public static void MarkProbe(VehicleIconEntry entry, bool success, string error)
        {
            lock (Sync)
            {
                entry.LastAttemptAt = Now();
                if (success)
                {
                    entry.Status = VehicleIconEntry.StatusOk;
                    entry.AttemptCount = 0;
                    entry.LastError = "";
                    entry.Collected = Now();
                }
                else
                {
                    entry.Status = VehicleIconEntry.StatusPermanent;
                    entry.LastError = Trim(error, 240);
                }
            }
        }

        /// <summary>「重试失败项」：清掉计数与永久标记，等待马上请求一次。</summary>
        public static int ResetFailed()
        {
            EnsureLoaded();
            int n = 0;
            lock (Sync)
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    VehicleIconEntry e = _bySlug[_order[i]];
                    if (e.Status == VehicleIconEntry.StatusPermanent || e.Status == VehicleIconEntry.StatusRetrying)
                    {
                        e.AttemptCount = 0;
                        e.Status = VehicleIconEntry.StatusRetrying;
                        e.LastError = "";
                        n++;
                    }
                }
            }
            if (n > 0)
            {
                Save();
            }
            return n;
        }

        /// <summary>等级权重采集状态写进同一份清单（与图标共用状态记录）。</summary>
        public static void MarkMeta(string slug, bool success, string error)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return;
            }
            EnsureLoaded();
            VehicleIconEntry e = Get(slug);
            if (e == null)
            {
                return;
            }
            lock (Sync)
            {
                e.MetaLastAttemptAt = Now();
                if (success)
                {
                    e.MetaStatus = "ok";
                    e.MetaAttemptCount = 0;
                    e.MetaLastError = "";
                }
                else
                {
                    e.MetaAttemptCount++;
                    e.MetaLastError = Trim(error, 240);
                    e.MetaStatus = e.MetaAttemptCount >= MaxAttempts ? "permanent" : "retrying";
                }
            }
        }

        public static int CountStatus(string status)
        {
            EnsureLoaded();
            int n = 0;
            lock (Sync)
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    if (_bySlug[_order[i]].Status == status)
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        public static int OkCount { get { return CountStatus(VehicleIconEntry.StatusOk); } }
        public static int RetryingCount { get { return CountStatus(VehicleIconEntry.StatusRetrying); } }
        public static int PermanentCount { get { return CountStatus(VehicleIconEntry.StatusPermanent); } }

        /// <summary>清单里所有 ok 条目引用的文件去重后的字节数。</summary>
        public static long UniqueBytes()
        {
            EnsureLoaded();
            Dictionary<string, long> files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            lock (Sync)
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    VehicleIconEntry e = _bySlug[_order[i]];
                    if (e.IsOk && e.File.Length > 0 && !files.ContainsKey(e.File))
                    {
                        files[e.File] = e.Bytes;
                    }
                }
            }
            long total = 0;
            foreach (KeyValuePair<string, long> pair in files)
            {
                total += pair.Value;
            }
            return total;
        }

        public static int DuplicateCount()
        {
            EnsureLoaded();
            int files = 0;
            int ok = 0;
            lock (Sync)
            {
                HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _order.Count; i++)
                {
                    VehicleIconEntry e = _bySlug[_order[i]];
                    if (e.IsOk)
                    {
                        ok++;
                        set.Add(e.File);
                    }
                }
                files = set.Count;
            }
            return ok - files;
        }

        /// <summary>状态栏用的短句（完整版在图标窗口里）。</summary>
        public static string CoverageShort()
        {
            EnsureLoaded();
            if (_lastError.Length > 0)
            {
                return "图标不可用";
            }
            int pending = RetryingCount + PermanentCount;
            string text = "图标 " + OkCount + "/" + Count;
            if (pending > 0)
            {
                text += " · 待补 " + pending;
            }
            return text;
        }

        public static string CoverageText()
        {
            int ok = OkCount;
            int retrying = RetryingCount;
            int permanent = PermanentCount;
            return string.Format(CultureInfo.InvariantCulture,
                "图标 {0}/{1}：成功 {2} · 重试中 {3} · 永久失败 {4}（去重 {5} 张）",
                ok, Count, ok, retrying, permanent, DuplicateCount());
        }

        public static string StatusLine()
        {
            EnsureLoaded();
            if (_lastError.Length > 0)
            {
                return "图标数据不可用：" + _lastError;
            }
            return CoverageText();
        }

        /// <summary>导出缺失清单（永久失败 + 待重试）。返回写出的条数。</summary>
        public static int ExportMissing(string path)
        {
            EnsureLoaded();
            List<string> lines = new List<string>();
            lines.Add("slug\tnation\tbranch\trank\tstatus\tattemptCount\tlastError\tlastAttemptAt");
            lock (Sync)
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    VehicleIconEntry e = _bySlug[_order[i]];
                    if (e.IsOk)
                    {
                        continue;
                    }
                    lines.Add(string.Join("\t", new string[] {
                        e.Slug, e.Nation, e.Branch,
                        e.Rank.ToString(CultureInfo.InvariantCulture),
                        e.Status,
                        e.AttemptCount.ToString(CultureInfo.InvariantCulture),
                        e.LastError.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '),
                        e.LastAttemptAt }));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            return lines.Count - 1;
        }

        public static void Save()
        {
            lock (Sync)
            {
                if (string.IsNullOrEmpty(Dir))
                {
                    return;
                }
                try
                {
                    Directory.CreateDirectory(Dir);
                    StringBuilder sb = new StringBuilder();
                    sb.Append("{\n");
                    sb.Append("  \"format\": 1,\n");
                    sb.Append("  \"version\": ").Append(Json.Quote(_version)).Append(",\n");
                    sb.Append("  \"updated\": ").Append(Json.Quote(_updated)).Append(",\n");
                    sb.Append("  \"generator\": ").Append(Json.Quote(_generator)).Append(",\n");
                    sb.Append("  \"source\": \"https://wiki.warthunder.com/ 科技树（30 页 = 10 国 × 3 分支）\",\n");
                    sb.Append("  \"count\": ").Append(_order.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                    sb.Append("  \"ok\": ").Append(OkCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                    sb.Append("  \"retrying\": ").Append(RetryingCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                    sb.Append("  \"permanent\": ").Append(PermanentCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                    sb.Append("  \"entries\": [\n");
                    for (int i = 0; i < _order.Count; i++)
                    {
                        VehicleIconEntry e = _bySlug[_order[i]];
                        sb.Append("    {");
                        sb.Append("\"slug\":").Append(Json.Quote(e.Slug));
                        sb.Append(",\"file\":").Append(Json.Quote(e.File));
                        sb.Append(",\"bytes\":").Append(e.Bytes.ToString(CultureInfo.InvariantCulture));
                        sb.Append(",\"sha256\":").Append(Json.Quote(e.Sha256));
                        sb.Append(",\"nation\":").Append(Json.Quote(e.Nation));
                        sb.Append(",\"branch\":").Append(Json.Quote(e.Branch));
                        sb.Append(",\"rank\":").Append(e.Rank.ToString(CultureInfo.InvariantCulture));
                        sb.Append(",\"name\":").Append(Json.Quote(e.Name));
                        sb.Append(",\"collected\":").Append(Json.Quote(e.Collected));
                        sb.Append(",\"attemptCount\":").Append(e.AttemptCount.ToString(CultureInfo.InvariantCulture));
                        sb.Append(",\"lastError\":").Append(Json.Quote(e.LastError));
                        sb.Append(",\"lastAttemptAt\":").Append(Json.Quote(e.LastAttemptAt));
                        sb.Append(",\"status\":").Append(Json.Quote(e.Status));
                        sb.Append(",\"metaStatus\":").Append(Json.Quote(e.MetaStatus));
                        sb.Append(",\"metaAttemptCount\":").Append(e.MetaAttemptCount.ToString(CultureInfo.InvariantCulture));
                        sb.Append(",\"metaLastError\":").Append(Json.Quote(e.MetaLastError));
                        sb.Append(",\"metaLastAttemptAt\":").Append(Json.Quote(e.MetaLastAttemptAt));
                        sb.Append("}");
                        if (i + 1 < _order.Count)
                        {
                            sb.Append(',');
                        }
                        sb.Append('\n');
                    }
                    sb.Append("  ]\n}\n");
                    string temp = ManifestPath + ".tmp";
                    File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                    if (File.Exists(ManifestPath))
                    {
                        File.Delete(ManifestPath);
                    }
                    File.Move(temp, ManifestPath);
                    _lastError = "";
                }
                catch (Exception ex)
                {
                    _lastError = "写入清单失败：" + ex.Message;
                    Config.Log(_lastError);
                }
            }
        }

        public static void SetHeader(string version, string updated, string generator)
        {
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(version))
                {
                    _version = version;
                }
                if (!string.IsNullOrEmpty(updated))
                {
                    _updated = updated;
                }
                if (!string.IsNullOrEmpty(generator))
                {
                    _generator = generator;
                }
            }
        }

        public static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string Trim(string text, int max)
        {
            if (text == null)
            {
                return "";
            }
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= max ? text : text.Substring(0, max);
        }

        private static VehicleIconEntry FromJson(Dictionary<string, object> row)
        {
            VehicleIconEntry e = new VehicleIconEntry();
            e.Slug = Json.Str(row, "slug");
            e.File = Json.Str(row, "file");
            e.Bytes = (long)Json.Num(row, "bytes");
            e.Sha256 = Json.Str(row, "sha256");
            e.Nation = Json.Str(row, "nation");
            e.Branch = Json.Str(row, "branch");
            e.Rank = (int)Json.Num(row, "rank");
            e.Name = Json.Str(row, "name");
            e.Collected = Json.Str(row, "collected");
            e.AttemptCount = (int)Json.Num(row, "attemptCount");
            e.LastError = Json.Str(row, "lastError");
            e.LastAttemptAt = Json.Str(row, "lastAttemptAt");
            e.Status = Json.Str(row, "status");
            if (e.Status.Length == 0)
            {
                e.Status = e.File.Length > 0 ? VehicleIconEntry.StatusOk : VehicleIconEntry.StatusRetrying;
            }
            e.MetaStatus = Json.Str(row, "metaStatus");
            if (e.MetaStatus.Length == 0)
            {
                e.MetaStatus = "none";
            }
            e.MetaAttemptCount = (int)Json.Num(row, "metaAttemptCount");
            e.MetaLastError = Json.Str(row, "metaLastError");
            e.MetaLastAttemptAt = Json.Str(row, "metaLastAttemptAt");
            return e;
        }
    }

    /// <summary>够用就好的 JSON 读写（.NET 4 没有内置 JSON 库，不引第三方依赖）。</summary>
    internal static class Json
    {
        public static string Quote(string text)
        {
            if (text == null)
            {
                return "\"\"";
            }
            StringBuilder sb = new StringBuilder(text.Length + 2);
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static object Get(Dictionary<string, object> obj, string key)
        {
            object value;
            if (obj == null || !obj.TryGetValue(key, out value))
            {
                return null;
            }
            return value;
        }

        public static string Str(Dictionary<string, object> obj, string key)
        {
            object value;
            if (obj == null || !obj.TryGetValue(key, out value) || value == null)
            {
                return "";
            }
            return value as string ?? "";
        }

        public static double Num(Dictionary<string, object> obj, string key)
        {
            object value;
            if (obj == null || !obj.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }
            if (value is double)
            {
                return (double)value;
            }
            return 0;
        }

        public static List<object> Arr(Dictionary<string, object> obj, string key)
        {
            object value;
            if (obj != null && obj.TryGetValue(key, out value))
            {
                List<object> list = value as List<object>;
                if (list != null)
                {
                    return list;
                }
            }
            return new List<object>();
        }

        public static object Parse(string text)
        {
            int index = 0;
            object value = ParseValue(text, ref index);
            SkipSpace(text, ref index);
            return value;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length)
            {
                return null;
            }
            char c = s[i];
            if (c == '{')
            {
                i++;
                Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    SkipSpace(s, ref i);
                    if (i >= s.Length)
                    {
                        break;
                    }
                    if (s[i] == '}')
                    {
                        i++;
                        break;
                    }
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    string key = ParseString(s, ref i);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ':')
                    {
                        i++;
                    }
                    object value = ParseValue(s, ref i);
                    if (key != null)
                    {
                        obj[key] = value;
                    }
                }
                return obj;
            }
            if (c == '[')
            {
                i++;
                List<object> list = new List<object>();
                while (true)
                {
                    SkipSpace(s, ref i);
                    if (i >= s.Length)
                    {
                        break;
                    }
                    if (s[i] == ']')
                    {
                        i++;
                        break;
                    }
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    list.Add(ParseValue(s, ref i));
                }
                return list;
            }
            if (c == '"')
            {
                return ParseString(s, ref i);
            }
            if (c == 't' && i + 3 < s.Length && s.Substring(i, 4) == "true")
            {
                i += 4;
                return true;
            }
            if (c == 'f' && i + 4 < s.Length && s.Substring(i, 5) == "false")
            {
                i += 5;
                return false;
            }
            if (c == 'n' && i + 3 < s.Length && s.Substring(i, 4) == "null")
            {
                i += 4;
                return null;
            }
            int start = i;
            while (i < s.Length && "-+.eE0123456789".IndexOf(s[i]) >= 0)
            {
                i++;
            }
            if (i == start)
            {
                i++;
                return null;
            }
            double number;
            if (double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
            return null;
        }

        private static string ParseString(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length || s[i] != '"')
            {
                return null;
            }
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length)
                {
                    break;
                }
                char esc = s[i++];
                switch (esc)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                sb.Append((char)code);
                            }
                            i += 4;
                        }
                        break;
                    default: sb.Append(esc); break;
                }
            }
            return sb.ToString();
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
            {
                i++;
            }
        }
    }
}
