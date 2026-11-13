using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WtSkin
{
    internal enum UpdateState
    {
        None,
        UpToDate,
        NewVersion,
        Failed
    }

    internal enum UpdateFailure
    {
        None,
        Network,
        RateLimit,
        Format
    }

    /// <summary>一次更新检查的结果。界面只消费这个对象，不直接碰网络或缓存。</summary>
    internal class UpdateCheckResult
    {
        public UpdateState State = UpdateState.None;
        public UpdateFailure Failure = UpdateFailure.None;
        public SemVer Current;
        public SemVer Latest;
        public string PublishedAt = "";
        public string Body = "";
        public string DownloadUrl = "";
        public string ReleaseUrl = "";
        public long DownloadSize;
        public string Sha256 = "";
        public string Sha256Url = "";
        public string ErrorDetail = "";
        public DateTime CheckedAt = DateTime.MinValue;
        public bool FromCache;

        public string VersionText
        {
            get { return Latest == null ? "" : "v" + Latest.ToDisplay(); }
        }

        public string DateText
        {
            get { return UpdateService.FormatDate(PublishedAt); }
        }

        public string FailureText
        {
            get
            {
                switch (Failure)
                {
                    case UpdateFailure.Network:
                        return "网络不可达";
                    case UpdateFailure.RateLimit:
                        return "接口限流";
                    case UpdateFailure.Format:
                        return "返回格式异常";
                }
                return "未知原因";
            }
        }
    }

    /// <summary>update-cache.ini 的内存形态。LastCheckTime 与 NotifiedVersion 同时用于去重。</summary>
    internal class UpdateCache
    {
        public DateTime LastCheck = DateTime.MinValue;
        public string LastState = "";
        public string LatestVersion = "";
        public string PublishedAt = "";
        public string Body = "";
        public string DownloadUrl = "";
        public string ReleaseUrl = "";
        public long DownloadSize;
        public string Sha256 = "";
        public string Sha256Url = "";
        public string NotifiedVersion = "";
        public string LastFailure = "";
        public string LastError = "";

        public bool IsFresh
        {
            get
            {
                return LastCheck != DateTime.MinValue &&
                       DateTime.Now - LastCheck <= TimeSpan.FromMinutes(UpdateService.CacheMinutes);
            }
        }
    }

    /// <summary>
    /// GitHub Releases 检查：只访问 settings.ini 里配置的官方端点，不经过第三方代理/镜像。
    /// 10 秒超时、必带 User-Agent、10 分钟缓存、全部异常安全降级，并把结果写进
    /// CacheDir\update-cache.ini。
    /// </summary>
    internal static class UpdateService
    {
        public const int CacheMinutes = 10;
        public const int TimeoutMs = 10000;
        public static string UserAgent
        {
            get
            {
                return "WTSkinManager/" + AppInfo.VersionText +
                       " (Windows NT 10.0; Win64; x64; +https://github.com/Nchoneko/WTSkinManager)";
            }
        }

        private static readonly object Sync = new object();
        private static readonly object CacheLock = new object();
        private static bool _running;
        private static readonly List<Action<UpdateCheckResult>> _waiters = new List<Action<UpdateCheckResult>>();

        public static string CachePath
        {
            get { return Path.Combine(Config.CacheDir, "update-cache.ini"); }
        }

        // ---------------- 异步入口 ----------------

        /// <summary>
        /// 后台线程发起检查；回调回到调用线程的 SynchronizationContext（WinForms 即 UI 线程）。
        /// 同一时刻只有一个请求，后到的调用共用这次结果。永不向调用方抛异常。
        /// </summary>
        public static void CheckAsync(bool silent, Action<UpdateCheckResult> done)
        {
            SynchronizationContext context = SynchronizationContext.Current;
            lock (Sync)
            {
                if (_running)
                {
                    if (done != null)
                    {
                        _waiters.Add(done);
                    }
                    return;
                }
                _running = true;
            }

            Thread thread = new Thread(delegate()
            {
                UpdateCheckResult result;
                try
                {
                    result = CheckCore(silent);
                }
                catch (Exception ex)
                {
                    Config.Log("更新检查：未预料的异常 → " + ex.GetType().Name + ": " + ex.Message);
                    result = Failure(UpdateFailure.Format, ex.GetType().Name + ": " + ex.Message, ReadCache());
                }

                List<Action<UpdateCheckResult>> callbacks;
                lock (Sync)
                {
                    _running = false;
                    callbacks = new List<Action<UpdateCheckResult>>(_waiters);
                    _waiters.Clear();
                }
                if (done != null)
                {
                    callbacks.Insert(0, done);
                }
                for (int i = 0; i < callbacks.Count; i++)
                {
                    Action<UpdateCheckResult> callback = callbacks[i];
                    if (context != null)
                    {
                        context.Post(delegate(object state)
                        {
                            try
                            {
                                callback(result);
                            }
                            catch (Exception ex)
                            {
                                Config.Log("更新检查：结果回调异常 → " + ex.Message);
                            }
                        }, null);
                    }
                    else
                    {
                        try
                        {
                            callback(result);
                        }
                        catch (Exception ex)
                        {
                            Config.Log("更新检查：结果回调异常 → " + ex.Message);
                        }
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "WTSkinUpdateCheck";
            thread.Start();
        }

        // ---------------- 检查主流程 ----------------

        private static UpdateCheckResult CheckCore(bool silent)
        {
            string tag = silent ? "自动检查更新" : "手动检查更新";
            UpdateCache cache = ReadCache();
            if (cache != null && cache.IsFresh)
            {
                // 手动检查遇到失败缓存时允许立刻重试；成功的缓存 10 分钟内直接复用。
                bool retryFailure = !silent && string.Equals(cache.LastState, "failed", StringComparison.OrdinalIgnoreCase);
                if (!retryFailure)
                {
                    UpdateCheckResult cached = FromCache(cache);
                    if (cached != null)
                    {
                        Config.Log("更新检查：" + tag + " 命中 10 分钟缓存 → " +
                                   (cached.State == UpdateState.Failed ? "失败/" + cached.FailureText : "版本 " + cached.VersionText));
                        return cached;
                    }
                }
            }

            string url = Config.UpdateApiLatest;
            if (!IsHttpUrl(url))
            {
                Config.Log("更新检查：" + tag + " 端点无效：" + url);
                return Failure(UpdateFailure.Format, "端点无效：" + url, cache);
            }

            Config.Log("更新检查：" + tag + " 请求 " + url);
            HttpWebResponse response = null;
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;
            }
            catch
            {
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = UserAgent;
                request.Accept = "application/vnd.github+json";
                request.Timeout = TimeoutMs;
                request.ReadWriteTimeout = TimeoutMs;
                request.AllowAutoRedirect = true;
                request.Proxy = null; // 直连官方端点，不走第三方代理/镜像
                request.KeepAlive = false;
                request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

                response = (HttpWebResponse)request.GetResponse();
                string json;
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }
                int status = (int)response.StatusCode;
                response.Close();
                response = null;
                if (status < 200 || status > 299)
                {
                    if (status == 403 || status == 429)
                    {
                        return Failure(UpdateFailure.RateLimit, "HTTP " + status, cache);
                    }
                    return Failure(UpdateFailure.Format, "HTTP " + status, cache);
                }
                return Parse(json, cache);
            }
            catch (WebException ex)
            {
                HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
                if (errorResponse != null)
                {
                    int status = 0;
                    try
                    {
                        status = (int)errorResponse.StatusCode;
                    }
                    catch
                    {
                    }
                    try
                    {
                        errorResponse.Close();
                    }
                    catch
                    {
                    }
                    if (status == 403 || status == 429)
                    {
                        return Failure(UpdateFailure.RateLimit, "HTTP " + status, cache);
                    }
                    return Failure(UpdateFailure.Format, "HTTP " + status, cache);
                }
                switch (ex.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.Timeout:
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.SecureChannelFailure:
                        return Failure(UpdateFailure.Network, ex.Status.ToString(), cache);
                }
                return Failure(UpdateFailure.Network, ex.Status.ToString() + " " + ex.Message, cache);
            }
            catch (Exception ex)
            {
                return Failure(UpdateFailure.Format, ex.GetType().Name + ": " + ex.Message, cache);
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

        private static UpdateCheckResult Parse(string json, UpdateCache previous)
        {
            object parsed = null;
            try
            {
                parsed = Json.Parse(json);
            }
            catch (Exception ex)
            {
                return Failure(UpdateFailure.Format, "JSON 解析失败：" + ex.Message, previous);
            }
            Dictionary<string, object> obj = parsed as Dictionary<string, object>;
            if (obj == null)
            {
                return Failure(UpdateFailure.Format, "JSON 根节点不是对象", previous);
            }
            string tag = Json.Str(obj, "tag_name");
            if (tag.Length == 0)
            {
                return Failure(UpdateFailure.Format, "缺少 tag_name", previous);
            }
            SemVer latest;
            if (!SemVer.TryParse(tag, out latest) || latest == null)
            {
                return Failure(UpdateFailure.Format, "tag_name 不是合法版本号：" + tag, previous);
            }
            if (!obj.ContainsKey("assets"))
            {
                return Failure(UpdateFailure.Format, "缺少 assets", previous);
            }
            List<object> assets = Json.Get(obj, "assets") as List<object>;
            if (assets == null)
            {
                return Failure(UpdateFailure.Format, "assets 不是数组", previous);
            }

            string exeUrl = "";
            string shaUrl = "";
            long exeSize = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                if (asset == null)
                {
                    continue;
                }
                string name = Json.Str(asset, "name");
                string url = Json.Str(asset, "browser_download_url");
                if (exeUrl.Length == 0 && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsHttpUrl(url))
                {
                    exeUrl = url;
                    exeSize = (long)Json.Num(asset, "size");
                }
                else if (shaUrl.Length == 0 && name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) &&
                         IsHttpUrl(url))
                {
                    shaUrl = url;
                }
            }

            string htmlUrl = Json.Str(obj, "html_url");
            string body = ConvertMarkdownToPlain(Json.Str(obj, "body"));
            string published = Json.Str(obj, "published_at");

            UpdateCheckResult result = new UpdateCheckResult();
            result.Current = CurrentVersion();
            result.Latest = latest;
            result.PublishedAt = published;
            result.Body = body;
            result.ReleaseUrl = IsHttpUrl(htmlUrl) ? htmlUrl : Config.ReleasesPage;
            result.DownloadUrl = exeUrl.Length > 0 ? exeUrl : Config.ReleasesPage;
            result.DownloadSize = exeSize > 0 ? exeSize : 0;
            result.Sha256 = ParseShaFromBody(Json.Str(obj, "body"));
            result.Sha256Url = shaUrl;
            result.CheckedAt = DateTime.Now;
            result.State = latest.CompareTo(result.Current) > 0 ? UpdateState.NewVersion : UpdateState.UpToDate;

            UpdateCache cache = previous == null ? new UpdateCache() : previous;
            cache.LastCheck = result.CheckedAt;
            cache.LastState = "ok";
            cache.LatestVersion = latest.ToDisplay();
            cache.PublishedAt = published;
            cache.Body = body;
            cache.DownloadUrl = result.DownloadUrl;
            cache.ReleaseUrl = result.ReleaseUrl;
            cache.DownloadSize = result.DownloadSize;
            cache.Sha256 = result.Sha256;
            cache.Sha256Url = result.Sha256Url;
            cache.LastFailure = "";
            cache.LastError = "";
            WriteCache(cache);

            Config.Log("更新检查：成功 → 远端 v" + latest.ToDisplay() + "，当前 " + BuildInfo.DisplayVersion +
                       "，判定 " + (result.State == UpdateState.NewVersion ? "有新版本" : "已是最新") +
                       "，下载地址 " + result.DownloadUrl);
            return result;
        }

        private static readonly Regex ShaPattern =
            new Regex(@"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])", RegexOptions.Compiled);

        /// <summary>
        /// Release 正文里的校验值：优先认「WTSkinManager.exe」同一行/相邻行的 64 位 SHA-256，
        /// 其次是带 sha256/certutil 提示的行。找不到返回空串（改为下载 .sha256 资产）。
        /// </summary>
        public static string ParseShaFromBody(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return "";
            }
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string sha = ScanSha(lines, true);
            if (sha.Length > 0)
            {
                return sha;
            }
            return ScanSha(lines, false);
        }

        private static string ScanSha(string[] lines, bool requireExe)
        {
            string previous = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match match = ShaPattern.Match(line);
                if (match.Success)
                {
                    string lower = line.ToLowerInvariant();
                    bool named = lower.Contains("wtskinmanager.exe");
                    if (!named && !requireExe)
                    {
                        named = lower.Contains("sha256") || lower.Contains("sha-256") ||
                                lower.Contains(".exe") || lower.Contains("certutil") ||
                                previous.ToLowerInvariant().Contains("sha");
                    }
                    if (named)
                    {
                        return match.Value.ToLowerInvariant();
                    }
                }
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    previous = trimmed;
                }
            }
            return "";
        }

        // ---------------- 缓存 ----------------

        public static UpdateCache ReadCache()
        {
            lock (CacheLock)
            {
                try
                {
                    string path = CachePath;
                    if (!File.Exists(path))
                    {
                        return null;
                    }
                    UpdateCache cache = new UpdateCache();
                    DateTime last;
                    string time = ReadKey(path, "lastCheckTime", "LastCheckTime");
                    if (DateTime.TryParseExact(time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                                               DateTimeStyles.None, out last))
                    {
                        cache.LastCheck = last;
                    }
                    cache.LastState = ReadKey(path, "lastState", "LastState");
                    cache.LatestVersion = ReadKey(path, "latestVersion", "LatestVersion");
                    cache.PublishedAt = ReadKey(path, "publishedAt", "PublishedAt");
                    cache.DownloadUrl = ReadKey(path, "downloadUrl", "DownloadUrl");
                    cache.ReleaseUrl = ReadKey(path, "releaseUrl", "ReleaseUrl");
                    cache.Sha256 = ReadKey(path, "sha256", "Sha256");
                    cache.Sha256Url = ReadKey(path, "sha256Url", "Sha256Url");
                    long downloadSize;
                    if (long.TryParse(ReadKey(path, "downloadSize", "DownloadSize"), NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out downloadSize))
                    {
                        cache.DownloadSize = downloadSize;
                    }
                    cache.NotifiedVersion = ReadKey(path, "notifiedVersion", "NotifiedVersion");
                    cache.LastFailure = ReadKey(path, "lastFailure", "LastFailure");
                    cache.LastError = ReadKey(path, "lastError", "LastError");
                    string body = ReadKey(path, "body", "Body");
                    if (body.Length > 0)
                    {
                        try
                        {
                            cache.Body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
                        }
                        catch
                        {
                            cache.Body = "";
                        }
                    }
                    return cache;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string ReadKey(string path, string camelCase, string pascalCase)
        {
            string value = Ini.Read(path, camelCase);
            if (value.Length > 0)
            {
                return value;
            }
            return Ini.Read(path, pascalCase);
        }

        private static void WriteCache(UpdateCache cache)
        {
            if (cache == null)
            {
                return;
            }
            lock (CacheLock)
            {
                string path = CachePath;
                string temp = path + ".tmp";
                try
                {
                    string body = "";
                    if (cache.Body.Length > 0)
                    {
                        body = Convert.ToBase64String(Encoding.UTF8.GetBytes(cache.Body));
                    }
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# 更新检查缓存；10 分钟内复用，避免重复请求 GitHub");
                    sb.AppendLine("lastCheckTime=" + (cache.LastCheck == DateTime.MinValue
                        ? ""
                        : cache.LastCheck.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                    sb.AppendLine("lastState=" + cache.LastState);
                    sb.AppendLine("latestVersion=" + cache.LatestVersion);
                    sb.AppendLine("publishedAt=" + cache.PublishedAt);
                    sb.AppendLine("body=" + body);
                    sb.AppendLine("downloadUrl=" + cache.DownloadUrl);
                    sb.AppendLine("releaseUrl=" + cache.ReleaseUrl);
                    sb.AppendLine("downloadSize=" + cache.DownloadSize.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine("sha256=" + cache.Sha256);
                    sb.AppendLine("sha256Url=" + cache.Sha256Url);
                    sb.AppendLine("notifiedVersion=" + cache.NotifiedVersion);
                    sb.AppendLine("lastFailure=" + cache.LastFailure);
                    sb.AppendLine("lastError=" + cache.LastError);
                    string text = sb.ToString();

                    Directory.CreateDirectory(Config.CacheDir);
                    File.WriteAllText(temp, text, new UTF8Encoding(false));
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(temp, path);
                }
                catch (Exception ex)
                {
                    Config.Log("更新检查：写缓存失败 → " + ex.Message);
                    try
                    {
                        if (File.Exists(temp))
                        {
                            File.Delete(temp);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static UpdateCheckResult FromCache(UpdateCache cache)
        {
            if (cache == null)
            {
                return null;
            }
            UpdateCheckResult result = new UpdateCheckResult();
            result.Current = CurrentVersion();
            result.Latest = SemVer.ParseOrUnknown(cache.LatestVersion);
            result.PublishedAt = cache.PublishedAt;
            result.Body = cache.Body;
            result.DownloadUrl = cache.DownloadUrl;
            result.ReleaseUrl = cache.ReleaseUrl;
            result.DownloadSize = cache.DownloadSize;
            result.Sha256 = cache.Sha256;
            result.Sha256Url = cache.Sha256Url;
            result.CheckedAt = cache.LastCheck;
            result.FromCache = true;
            if (string.Equals(cache.LastState, "failed", StringComparison.OrdinalIgnoreCase))
            {
                result.State = UpdateState.Failed;
                result.Failure = ParseFailure(cache.LastFailure);
                result.ErrorDetail = cache.LastError;
                return result;
            }
            if (result.Latest == null || result.Latest.IsUnknown)
            {
                return null;
            }
            result.State = result.Latest.CompareTo(result.Current) > 0
                ? UpdateState.NewVersion
                : UpdateState.UpToDate;
            return result;
        }

        /// <summary>设置页打开时读取最近一次结果，不发请求。</summary>
        public static UpdateCheckResult CachedResult()
        {
            return FromCache(ReadCache());
        }

        /// <summary>用调用方已经读到的缓存对象构造展示结果，避免重复读盘。</summary>
        public static UpdateCheckResult FromCachePublic(UpdateCache cache)
        {
            return FromCache(cache);
        }

        public static bool ShouldNotify(SemVer version)
        {
            if (version == null)
            {
                return false;
            }
            UpdateCache cache = ReadCache();
            if (cache == null || cache.NotifiedVersion.Length == 0)
            {
                return true;
            }
            SemVer notified;
            if (!SemVer.TryParse(cache.NotifiedVersion, out notified) || notified == null)
            {
                return true;
            }
            return !notified.IsSame(version);
        }

        /// <summary>关闭提示条 / 点「查看」后调用；同一版本不再提示。</summary>
        public static void MarkNotified(SemVer version)
        {
            if (version == null)
            {
                return;
            }
            UpdateCache cache = ReadCache();
            if (cache == null)
            {
                cache = new UpdateCache();
            }
            cache.NotifiedVersion = version.ToDisplay();
            WriteCache(cache);
        }

        private static UpdateFailure ParseFailure(string text)
        {
            if (string.Equals(text, "network", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateFailure.Network;
            }
            if (string.Equals(text, "ratelimit", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateFailure.RateLimit;
            }
            return UpdateFailure.Format;
        }

        private static string FailureKey(UpdateFailure failure)
        {
            switch (failure)
            {
                case UpdateFailure.Network:
                    return "network";
                case UpdateFailure.RateLimit:
                    return "ratelimit";
            }
            return "format";
        }

        private static UpdateCheckResult Failure(UpdateFailure failure, string detail, UpdateCache previous)
        {
            UpdateCheckResult result = new UpdateCheckResult();
            result.State = UpdateState.Failed;
            result.Failure = failure;
            result.Current = CurrentVersion();
            result.CheckedAt = DateTime.Now;
            result.ErrorDetail = detail == null ? "" : detail;
            if (result.ErrorDetail.Length > 300)
            {
                result.ErrorDetail = result.ErrorDetail.Substring(0, 300);
            }

            UpdateCache cache = previous == null ? new UpdateCache() : previous;
            cache.LastCheck = result.CheckedAt;
            cache.LastState = "failed";
            cache.LastFailure = FailureKey(failure);
            cache.LastError = result.ErrorDetail;
            WriteCache(cache);

            Config.Log("更新检查：失败 → " + result.FailureText + "（" + result.ErrorDetail + "）");
            return result;
        }

        // ---------------- 工具 ----------------

        public static SemVer CurrentVersion()
        {
            SemVer current;
            if (SemVer.TryParse(BuildInfo.DisplayVersion, out current) && current != null)
            {
                return current;
            }
            return AppInfo.AppVersion;
        }

        public static bool IsHttpUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }
            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
        }

        public static void OpenUrl(string url)
        {
            if (!IsHttpUrl(url))
            {
                url = Config.ReleasesPage;
            }
            if (!IsHttpUrl(url))
            {
                return;
            }
            try
            {
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = url;
                info.UseShellExecute = true;
                System.Diagnostics.Process.Start(info);
            }
            catch (Exception ex)
            {
                Config.Log("打开链接失败：" + url + " → " + ex.Message);
            }
        }

        public static string FormatDate(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "";
            }
            DateTime time;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time))
            {
                return time.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
            string trimmed = raw.Trim();
            if (trimmed.Length >= 10 && trimmed[4] == '-' && trimmed[7] == '-')
            {
                return trimmed.Substring(0, 10).Replace('-', '/');
            }
            return trimmed;
        }

        public static string FormatTime(DateTime time)
        {
            if (time == DateTime.MinValue)
            {
                return "";
            }
            return time.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        }

        // ---------------- Markdown → 纯文本 ----------------

        private static readonly Regex ImagePattern = new Regex(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex LinkPattern = new Regex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex HeadingPattern = new Regex(@"(?m)^\s{0,3}#{1,6}\s*", RegexOptions.Compiled);
        private static readonly Regex FencePattern = new Regex(@"(?m)^\s*```.*$", RegexOptions.Compiled);
        private static readonly Regex QuotePattern = new Regex(@"(?m)^\s{0,3}>\s?", RegexOptions.Compiled);
        private static readonly Regex BulletPattern = new Regex(@"(?m)^\s*[-+*]\s+", RegexOptions.Compiled);
        private static readonly Regex NumberPattern = new Regex(@"(?m)^\s*\d+[.)]\s+", RegexOptions.Compiled);
        private static readonly Regex RulePattern = new Regex(@"(?m)^\s*([-*_])\1{2,}\s*$", RegexOptions.Compiled);
        private static readonly Regex HtmlPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex ItalicUnderscore = new Regex(@"(?<!\w)_|_(?!\w)", RegexOptions.Compiled);
        private static readonly Regex MultiBlank = new Regex(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>GitHub Markdown 发布说明转纯文本；不把 HTML/Markdown 原文直接显示给用户。</summary>
        public static string ConvertMarkdownToPlain(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return "";
            }
            string text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            text = text.Replace("\uFEFF", "");
            text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
            text = ImagePattern.Replace(text, "$1");
            text = LinkPattern.Replace(text, "$1");
            text = FencePattern.Replace(text, "");
            text = HeadingPattern.Replace(text, "");
            text = QuotePattern.Replace(text, "");
            text = BulletPattern.Replace(text, "· ");
            text = NumberPattern.Replace(text, "· ");
            text = RulePattern.Replace(text, "");
            text = HtmlPattern.Replace(text, "");
            text = text.Replace("**", "").Replace("__", "");
            text = text.Replace("*", "");
            text = text.Replace("`", "");
            text = ItalicUnderscore.Replace(text, "");
            text = text.Replace("&nbsp;", " ").Replace("&amp;", "&")
                       .Replace("&lt;", "<").Replace("&gt;", ">")
                       .Replace("&quot;", "\"").Replace("&#39;", "'");
            text = MultiBlank.Replace(text, "\n\n");

            string[] lines = text.Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                // 行首残留的列表符号统一成中文间隔点；空行保留一个。
                if (line.Trim().Length == 0)
                {
                    if (sb.Length > 0 && !sb.ToString().EndsWith("\n\n", StringComparison.Ordinal))
                    {
                        sb.Append('\n');
                    }
                    continue;
                }
                sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }
    }
}
