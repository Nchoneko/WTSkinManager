using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WtSkin
{
    /// <summary>应用内下载的阶段；界面只按阶段切换文案，不自己推断状态。</summary>
    internal enum DownloadPhase
    {
        Idle,
        Preparing,
        Downloading,
        Retrying,
        Verifying,
        Completed,
        Canceled,
        Failed
    }

    /// <summary>一次下载进度快照（工作线程生成，界面只读，不回调回来改）。</summary>
    internal class DownloadProgress
    {
        public DownloadPhase Phase = DownloadPhase.Idle;
        public long Received;
        public long Total;
        public double Speed;
        public bool SpeedReady;
        public long EtaSeconds = -1;
        public int Retry;
        public int RetryMax;
        public string Message = "";

        public DownloadProgress Copy()
        {
            DownloadProgress copy = new DownloadProgress();
            copy.Phase = Phase;
            copy.Received = Received;
            copy.Total = Total;
            copy.Speed = Speed;
            copy.SpeedReady = SpeedReady;
            copy.EtaSeconds = EtaSeconds;
            copy.Retry = Retry;
            copy.RetryMax = RetryMax;
            copy.Message = Message;
            return copy;
        }
    }

    /// <summary>下载结束状态：成功（已校验并落盘）/ 取消（保留 .part）/ 失败。</summary>
    internal class DownloadResult
    {
        public bool Ok;
        public bool Canceled;
        public string ExePath = "";
        public string Sha256 = "";
        public string Sha256Actual = "";
        public long Size;
        public long PartBytes;
        public string Error = "";
        public bool MirrorHint;
        public bool ShaMismatch;
        public string Url = "";
    }

    /// <summary>下载地址白名单：只允许 https，本机回环放行 http（仅用于自测）。</summary>
    internal static class UpdateUrls
    {
        private static readonly string[] FixedHosts = new string[]
        {
            "github.com",
            "objects.githubusercontent.com",
            "release-assets.githubusercontent.com",
            "api.github.com",
            "codeload.github.com",
            // 默认镜像前缀的域名（DownloadMirrors 被清空时也仍然放行这三条）
            "ghfast.top",
            "gh-proxy.com",
            "ghproxy.net"
        };

        public static bool IsAllowed(string url)
        {
            Uri uri;
            if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }
            bool loopback = IsLoopback(uri.Host);
            if (uri.Scheme == Uri.UriSchemeHttp)
            {
                return loopback;
            }
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            if (loopback)
            {
                return true;
            }
            string host = uri.Host.ToLowerInvariant();
            for (int i = 0; i < FixedHosts.Length; i++)
            {
                if (Match(host, FixedHosts[i]))
                {
                    return true;
                }
            }
            List<string> mirrors = MirrorHosts();
            for (int i = 0; i < mirrors.Count; i++)
            {
                if (Match(host, mirrors[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsLoopback(string host)
        {
            string value = (host ?? "").Trim().ToLowerInvariant();
            return value == "127.0.0.1" || value == "localhost" || value == "::1" || value == "[::1]";
        }

        private static bool Match(string host, string pattern)
        {
            string p = (pattern ?? "").Trim().ToLowerInvariant();
            if (p.Length == 0)
            {
                return false;
            }
            if (p.StartsWith("*.", StringComparison.Ordinal))
            {
                string suffix = p.Substring(1);
                return host.EndsWith(suffix, StringComparison.Ordinal) || host == p.Substring(2);
            }
            return host == p;
        }

        /// <summary>配置里的镜像模板全部按「逗号分隔」处理，空项与非法项直接跳过。</summary>
        public static List<string> MirrorTemplates()
        {
            List<string> result = new List<string>();
            string raw = Config.DownloadMirrors;
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (item.Length == 0)
                {
                    continue;
                }
                if (item.IndexOf("{version}", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                result.Add(item);
            }
            return result;
        }

        public static List<string> MirrorHosts()
        {
            List<string> hosts = new List<string>();
            List<string> templates = MirrorTemplates();
            for (int i = 0; i < templates.Count; i++)
            {
                Uri uri;
                string probe = ReplaceVersion(templates[i], "0.0.0");
                if (Uri.TryCreate(probe, UriKind.Absolute, out uri) && uri.Host.Length > 0)
                {
                    if (!hosts.Contains(uri.Host.ToLowerInvariant()))
                    {
                        hosts.Add(uri.Host.ToLowerInvariant());
                    }
                }
            }
            return hosts;
        }

        public static string ReplaceVersion(string template, string versionText)
        {
            return (template ?? "").Replace("{version}", versionText ?? "");
        }

        /// <summary>镜像地址（已替换版本号、已过多余空白）。调用方负责按顺序探测。</summary>
        public static List<string> MirrorUrls(string versionText)
        {
            List<string> urls = new List<string>();
            List<string> templates = MirrorTemplates();
            for (int i = 0; i < templates.Count; i++)
            {
                string url = ReplaceVersion(templates[i], versionText);
                if (url.Length > 0 && !urls.Contains(url))
                {
                    urls.Add(url);
                }
            }
            return urls;
        }
    }

    /// <summary>
    /// 自更新相关目录的唯一来源：%LOCALAPPDATA%\WTSkinManager\{updates,backups,update.log}。
    /// 环境变量 WTSM_UPDATE_ROOT 仅用于自测隔离（正常运行时不会设置）。
    /// </summary>
    internal static class UpdatePaths
    {
        public static string Root
        {
            get
            {
                string over = Environment.GetEnvironmentVariable("WTSM_UPDATE_ROOT");
                if (!string.IsNullOrEmpty(over))
                {
                    return over;
                }
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "WTSkinManager");
            }
        }

        public static string Updates
        {
            get { return Path.Combine(Root, "updates"); }
        }

        public static string Backups
        {
            get { return Path.Combine(Root, "backups"); }
        }

        public static string UpdateLog
        {
            get { return Path.Combine(Updates, "update.log"); }
        }

        public static string RollbackNotice
        {
            get { return Path.Combine(Updates, "rollback.ini"); }
        }
    }

    /// <summary>下载界面的数字格式化：大小一位小数、速度两位小数（B/s → KB/s → MB/s）。</summary>
    internal static class DownloadFormat
    {
        public static string Size(long bytes)
        {
            if (bytes < 0)
            {
                bytes = 0;
            }
            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            }
            double kb = bytes / 1024.0;
            if (kb < 1024)
            {
                return kb.ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            }
            double mb = kb / 1024.0;
            if (mb < 1024)
            {
                return mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            }
            return (mb / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        public static string Speed(double bytesPerSecond)
        {
            if (bytesPerSecond < 0)
            {
                bytesPerSecond = 0;
            }
            if (bytesPerSecond < 1024)
            {
                return ((long)Math.Round(bytesPerSecond)).ToString(CultureInfo.InvariantCulture) + " B/s";
            }
            double kb = bytesPerSecond / 1024.0;
            if (kb < 1024)
            {
                return kb.ToString("0.00", CultureInfo.InvariantCulture) + " KB/s";
            }
            return (kb / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB/s";
        }

        public static string Eta(long seconds)
        {
            if (seconds < 0)
            {
                return "计算中…";
            }
            if (seconds < 60)
            {
                return Math.Max(1, seconds).ToString(CultureInfo.InvariantCulture) + " 秒";
            }
            long minutes = seconds / 60;
            if (minutes < 60)
            {
                return minutes.ToString(CultureInfo.InvariantCulture) + " 分 " +
                       (seconds % 60).ToString("00", CultureInfo.InvariantCulture) + " 秒";
            }
            return (minutes / 60).ToString(CultureInfo.InvariantCulture) + " 小时 " +
                   (minutes % 60).ToString(CultureInfo.InvariantCulture) + " 分";
        }
    }

    /// <summary>
    /// 应用内更新下载：单连接流式、Range 续传（206 续写，200/416 重下）、
    /// 连接 15 秒 / 读 30 秒、失败重试 3 次（1/2/4 秒退避）、镜像按序探测、
    /// SHA-256 校验、.part 原子改名。全部异常安全降级为结果对象，不向界面抛异常。
    /// </summary>
    internal class UpdateDownloader
    {
        public const int ConnectTimeoutMs = 15000;
        public const int ReadTimeoutMs = 30000;
        public const int MaxRetries = 3;
        public const int ProbeTimeoutMs = 3000;
        private const int BufferSize = 64 * 1024;
        private const int ReportIntervalMs = 100;

        private sealed class Sample
        {
            public DateTime At;
            public long Bytes;
        }

        private readonly string _versionText;
        private readonly string _primaryUrl;
        private readonly string _sha256;
        private readonly string _sha256Url;
        private readonly long _expectedSize;
        private readonly Action<DownloadProgress> _report;
        private readonly Action<DownloadResult> _done;

        private Thread _thread;
        private volatile bool _cancel;
        private volatile HttpWebRequest _activeRequest;
        private readonly object _sync = new object();
        private readonly List<Sample> _samples = new List<Sample>();
        private double _ema;
        private bool _hasEma;
        private DateTime _lastReportAt = DateTime.MinValue;
        private long _lastReportBytes = -1;
        private DownloadPhase _lastReportPhase = DownloadPhase.Idle;

        public UpdateDownloader(string versionText, string primaryUrl, string sha256, string sha256Url,
                                long expectedSize, Action<DownloadProgress> report, Action<DownloadResult> done)
        {
            _versionText = versionText ?? "";
            _primaryUrl = primaryUrl ?? "";
            _sha256 = sha256 ?? "";
            _sha256Url = sha256Url ?? "";
            _expectedSize = expectedSize;
            _report = report;
            _done = done;
        }

        public static string UserAgent
        {
            get
            {
                return "WTSkinManager/" + AppInfo.VersionText +
                       " (Windows NT 10.0; Win64; x64; +https://github.com/Nchoneko/WTSkinManager)";
            }
        }

        public static string AppDataRoot
        {
            get { return UpdatePaths.Updates; }
        }

        public static string VersionDir(string versionText)
        {
            return Path.Combine(AppDataRoot, string.IsNullOrEmpty(versionText) ? "unknown" : versionText);
        }

        public static string PartPath(string versionText)
        {
            return Path.Combine(VersionDir(versionText), "WTSkinManager.exe.part");
        }

        public static string ExePath(string versionText)
        {
            return Path.Combine(VersionDir(versionText), "WTSkinManager.exe");
        }

        /// <summary>就绪标记：version / sha256 / size / time，用于「更新已下载，可立即安装」。</summary>
        public static string ReadyPath(string versionText)
        {
            return Path.Combine(VersionDir(versionText), "ready.ini");
        }

        public static long PartLength(string versionText)
        {
            try
            {
                string part = PartPath(versionText);
                return File.Exists(part) ? new FileInfo(part).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>已下载并校验通过、可以安装的包（exe 存在且大小与标记一致）。</summary>
        public static bool TryGetReadyPackage(string versionText, out string exePath, out string sha256, out long size)
        {
            exePath = "";
            sha256 = "";
            size = 0;
            try
            {
                string ready = ReadyPath(versionText);
                string exe = ExePath(versionText);
                if (!File.Exists(ready) || !File.Exists(exe))
                {
                    return false;
                }
                long recorded;
                if (!long.TryParse(Ini.Read(ready, "size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out recorded))
                {
                    return false;
                }
                long actual = new FileInfo(exe).Length;
                if (recorded <= 0 || actual != recorded)
                {
                    return false;
                }
                exePath = exe;
                sha256 = Ini.Read(ready, "sha256");
                size = actual;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>写入就绪标记（下载器在 .part 改名后调用）。</summary>
        public static void WriteReady(string versionText, string sha256, long size)
        {
            try
            {
                string dir = VersionDir(versionText);
                Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 应用内更新：已下载并通过 SHA-256 校验");
                sb.AppendLine("version=" + (versionText ?? ""));
                sb.AppendLine("sha256=" + (sha256 ?? ""));
                sb.AppendLine("size=" + size.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                File.WriteAllText(ReadyPath(versionText), sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Config.Log("自更新：写就绪标记失败 → " + ex.Message);
            }
        }

        /// <summary>把 64 位十六进制校验值整理成小写；不是合法长度返回空串。</summary>
        public static string NormalizeSha(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            StringBuilder sb = new StringBuilder(64);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (hex)
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            string text = sb.ToString();
            return text.Length == 64 ? text : "";
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Name = "WTSkinUpdateDownload";
            _thread.Start();
        }

        public void Cancel()
        {
            _cancel = true;
            HttpWebRequest request = _activeRequest;
            if (request != null)
            {
                try
                {
                    request.Abort();
                }
                catch
                {
                }
            }
        }

        // ---------------- 工作线程 ----------------

        private void Run()
        {
            DownloadResult result = new DownloadResult();
            try
            {
                Download(result);
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
                Config.Log("自更新：下载异常 → " + result.Error);
            }
            if (_cancel && !result.Ok)
            {
                result.Canceled = true;
            }
            if (result.Canceled)
            {
                DownloadProgress progress = new DownloadProgress();
                progress.Phase = DownloadPhase.Canceled;
                progress.Total = _expectedSize;
                progress.Received = result.PartBytes;
                Report(progress, true);
            }
            if (_done != null)
            {
                try
                {
                    _done(result);
                }
                catch (Exception ex)
                {
                    Config.Log("自更新：下载回调异常 → " + ex.Message);
                }
            }
        }

        private void Download(DownloadResult result)
        {
            string version = _versionText;
            string dir = VersionDir(version);
            string part = PartPath(version);
            result.ExePath = ExePath(version);
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                result.Error = "无法创建更新目录：" + ex.Message;
                ReportPhase(DownloadPhase.Failed, result.Error);
                return;
            }

            ReportPhase(DownloadPhase.Preparing, "准备下载…");

            string sha = NormalizeSha(_sha256);
            if (sha.Length == 0 && _sha256Url.Length > 0)
            {
                sha = NormalizeSha(FetchSha(_sha256Url));
            }
            if (sha.Length == 0)
            {
                result.Error = "未获取到 SHA-256 校验值，已停止下载";
                ReportPhase(DownloadPhase.Failed, result.Error);
                Config.Log("自更新：缺少 SHA-256 校验值，取消下载");
                return;
            }

            long expected = _expectedSize;
            if (expected <= 0)
            {
                // 拿不到包体大小时，用已有 .part 当已知量，磁盘检查退化为「至少留 1 GB」。
                expected = PartLength(version);
            }
            if (!HasEnoughSpace(dir, expected))
            {
                result.Error = "磁盘空间不足：需要约 " + DownloadFormat.Size(Math.Max(expected, 1) * 2) + "，请清理后重试";
                ReportPhase(DownloadPhase.Failed, result.Error);
                Config.Log("自更新：" + result.Error);
                return;
            }

            List<string> urls = new List<string>();
            if (_primaryUrl.Length > 0 && UpdateUrls.IsAllowed(_primaryUrl))
            {
                urls.Add(_primaryUrl);
            }
            else if (_primaryUrl.Length > 0)
            {
                Config.Log("自更新：主地址不在白名单，已跳过 " + _primaryUrl);
            }

            for (int i = 0; i < urls.Count && !_cancel; i++)
            {
                if (TryUrl(urls[i], part, expected, sha, result))
                {
                    return;
                }
                if (result.Canceled || result.ShaMismatch)
                {
                    return;
                }
            }

            if (_cancel)
            {
                result.Canceled = true;
                result.PartBytes = PartLength(version);
                return;
            }

            // 直连失败：按序探测镜像（Range 0-0，3 秒），第一个可用的继续下载。
            List<string> mirrors = UpdateUrls.MirrorUrls(version);
            for (int i = 0; i < mirrors.Count && !_cancel; i++)
            {
                string mirror = mirrors[i];
                if (!UpdateUrls.IsAllowed(mirror))
                {
                    Config.Log("自更新：镜像不在白名单，已跳过 " + mirror);
                    continue;
                }
                Config.Log("自更新：直连失败，探测镜像 " + mirror);
                if (!Probe(mirror))
                {
                    Config.Log("自更新：镜像不可用 " + mirror);
                    continue;
                }
                if (TryUrl(mirror, part, expected, sha, result))
                {
                    return;
                }
                if (result.Canceled || result.ShaMismatch)
                {
                    return;
                }
            }

            result.MirrorHint = true;
            if (result.Error.Length == 0)
            {
                result.Error = "GitHub 直连失败，可挂梯子重试，或使用国内镜像下载";
            }
            ReportPhase(DownloadPhase.Failed, result.Error);
            Config.Log("自更新：下载失败 → " + result.Error);
        }

        /// <summary>单个地址：最多 3 次重试（1/2/4 秒退避），成功即校验并落盘。</summary>
        private bool TryUrl(string url, string part, long expectedSize, string sha, DownloadResult result)
        {
            int attempt = 0;
            while (true)
            {
                if (_cancel)
                {
                    result.Canceled = true;
                    result.PartBytes = PartLength(_versionText);
                    return false;
                }
                attempt++;
                try
                {
                    DownloadOnce(url, part, expectedSize, sha);
                    result.Ok = true;
                    result.Url = url;
                    result.Size = new FileInfo(result.ExePath).Length;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    result.Canceled = true;
                    result.PartBytes = PartLength(_versionText);
                    return false;
                }
                catch (HashMismatchException)
                {
                    result.Error = "文件校验失败，请重试或到 GitHub 下载";
                    result.MirrorHint = false;
                    result.ShaMismatch = true;
                    ReportPhase(DownloadPhase.Failed, result.Error);
                    return false;
                }
                catch (Exception ex)
                {
                    if (_cancel)
                    {
                        result.Canceled = true;
                        result.PartBytes = PartLength(_versionText);
                        return false;
                    }
                    if (attempt > MaxRetries)
                    {
                        result.Error = DescribeError(ex);
                        Config.Log("自更新：地址失败（已重试 " + MaxRetries + " 次）" + url + " → " + result.Error);
                        // 还有镜像可试：这里不报「失败」，最终结果由 Download 统一上报，避免界面闪烁。
                        return false;
                    }
                    int wait = attempt == 1 ? 1 : (attempt == 2 ? 2 : 4);
                    DownloadProgress progress = new DownloadProgress();
                    progress.Phase = DownloadPhase.Retrying;
                    progress.Retry = attempt;
                    progress.RetryMax = MaxRetries;
                    progress.Received = PartLength(_versionText);
                    progress.Total = expectedSize;
                    progress.Message = "网络中断，正在重试 " + attempt + "/" + MaxRetries;
                    Report(progress, true);
                    Config.Log("自更新：下载中断，" + wait + " 秒后重试 " + attempt + "/" + MaxRetries + " → " + DescribeError(ex));
                    if (!SleepInterruptible(wait * 1000))
                    {
                        result.Canceled = true;
                        result.PartBytes = PartLength(_versionText);
                        return false;
                    }
                }
            }
        }

        private sealed class HashMismatchException : Exception
        {
        }

        /// <summary>单次连接：206 追加续写，200 从零重下，416 删除 .part 重下。</summary>
        private void DownloadOnce(string url, string part, long expectedSize, string sha)
        {
            long existing = 0;
            try
            {
                if (File.Exists(part))
                {
                    existing = new FileInfo(part).Length;
                }
            }
            catch
            {
                existing = 0;
            }
            if (expectedSize > 0 && existing > expectedSize)
            {
                TryDelete(part);
                existing = 0;
            }
            if (existing > 0 && expectedSize > 0 && existing == expectedSize)
            {
                // 上次已经下完，只差改名：直接进入校验。
                VerifyAndPublish(part, sha, expectedSize, url);
                return;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Timeout = ConnectTimeoutMs;
            request.ReadWriteTimeout = ReadTimeoutMs;
            request.AllowAutoRedirect = true;
            request.KeepAlive = false;
            request.AutomaticDecompression = DecompressionMethods.None;
            if (existing > 0)
            {
                request.AddRange(existing);
            }
            _activeRequest = request;

            HttpWebResponse response = null;
            FileStream file = null;
            try
            {
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException ex)
                {
                    HttpWebResponse error = ex.Response as HttpWebResponse;
                    int code = 0;
                    if (error != null)
                    {
                        try
                        {
                            code = (int)error.StatusCode;
                        }
                        catch
                        {
                        }
                        try
                        {
                            error.Close();
                        }
                        catch
                        {
                        }
                    }
                    if (code == 416)
                    {
                        // 本地 .part 已经不小于远端：删掉重下。
                        TryDelete(part);
                        throw new IOException("服务器要求重新下载（HTTP 416）");
                    }
                    throw;
                }

                int status = (int)response.StatusCode;
                bool append;
                if (status == 206 && existing > 0)
                {
                    append = true;
                }
                else if (status == 200)
                {
                    append = false;
                    existing = 0;
                }
                else if (status == 416)
                {
                    TryDelete(part);
                    throw new IOException("服务器要求重新下载（HTTP 416）");
                }
                else
                {
                    throw new WebException("服务器返回 HTTP " + status);
                }

                long length = -1;
                try
                {
                    length = response.ContentLength;
                }
                catch
                {
                    length = -1;
                }
                long total = length >= 0 ? existing + length : expectedSize;

                file = new FileStream(part, append ? FileMode.Append : FileMode.Create,
                                      FileAccess.Write, FileShare.ReadWrite, BufferSize);
                ReadStream(response.GetResponseStream(), file, existing, total);
                file.Flush();
                file.Close();
                file = null;
                response.Close();
                response = null;

                long size = PartLength(_versionText);
                if (total > 0 && size != total)
                {
                    throw new IOException("下载不完整：" + DownloadFormat.Size(size) + " / " + DownloadFormat.Size(total));
                }
                VerifyAndPublish(part, sha, size, url);
            }
            finally
            {
                if (file != null)
                {
                    try
                    {
                        file.Close();
                    }
                    catch
                    {
                    }
                }
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
                _activeRequest = null;
            }
        }

        private void ReadStream(Stream stream, FileStream file, long startOffset, long total)
        {
            byte[] buffer = new byte[BufferSize];
            long received = startOffset;
            RecordSample(received);
            ReportProgress(received, total, true);
            try
            {
                while (true)
                {
                    if (_cancel)
                    {
                        throw new OperationCanceledException();
                    }
                    int read;
                    try
                    {
                        read = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (Exception ex)
                    {
                        if (_cancel)
                        {
                            throw new OperationCanceledException();
                        }
                        if (ex is IOException || ex is WebException || ex is ObjectDisposedException)
                        {
                            throw new IOException("读取中断：" + ex.Message);
                        }
                        throw;
                    }
                    if (read <= 0)
                    {
                        break;
                    }
                    file.Write(buffer, 0, read);
                    received += read;
                    RecordSample(received);
                    ReportProgress(received, total, false);
                }
            }
            finally
            {
                ReportProgress(received, total, true);
            }
        }

        private void VerifyAndPublish(string part, string sha, long size, string url)
        {
            DownloadProgress verifying = new DownloadProgress();
            verifying.Phase = DownloadPhase.Verifying;
            verifying.Received = size;
            verifying.Total = size;
            verifying.Message = "正在校验…";
            Report(verifying, true);

            string actual = HashFile(part);
            if (actual.Length == 0 || !string.Equals(actual, sha, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(part);
                Config.Log("自更新：SHA-256 校验失败，已删除下载文件；期望 " + sha + "，实际 " + actual);
                throw new HashMismatchException();
            }

            string target = ExePath(_versionText);
            long actualSize = size;
            try
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
                File.Move(part, target);
                actualSize = new FileInfo(target).Length;
            }
            catch (Exception ex)
            {
                throw new IOException("无法完成下载文件改名：" + ex.Message);
            }
            WriteReady(_versionText, actual, actualSize);

            DownloadProgress done = new DownloadProgress();
            done.Phase = DownloadPhase.Completed;
            done.Received = actualSize;
            done.Total = actualSize;
            done.SpeedReady = true;
            done.Speed = 0;
            done.EtaSeconds = 0;
            done.Message = "下载完成";
            Report(done, true);
            Config.Log("自更新：下载完成 → " + target + "（" + DownloadFormat.Size(actualSize) + "，sha256 " + actual + "）");
        }

        // ---------------- 镜像探测 ----------------

        /// <summary>Range 0-0 探测：3 秒内拿到 200/206 才算可用。</summary>
        internal static bool ProbeUrl(string url)
        {
            if (!UpdateUrls.IsAllowed(url))
            {
                return false;
            }
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = UserAgent;
                request.Timeout = ProbeTimeoutMs;
                request.ReadWriteTimeout = ProbeTimeoutMs;
                request.AllowAutoRedirect = true;
                request.KeepAlive = false;
                request.AddRange(0, 0);
                response = (HttpWebResponse)request.GetResponse();
                int status = (int)response.StatusCode;
                return status == 200 || status == 206;
            }
            catch
            {
                return false;
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
                if (request != null)
                {
                    try
                    {
                        request.Abort();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private bool Probe(string url)
        {
            if (_cancel)
            {
                return false;
            }
            return ProbeUrl(url);
        }

        /// <summary>读取 .sha256 资产（没有正文校验值时用）。失败返回空串，只写日志。</summary>
        private static string FetchSha(string url)
        {
            if (!UpdateUrls.IsAllowed(url))
            {
                Config.Log("自更新：校验文件地址不在白名单，跳过 " + url);
                return "";
            }
            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = UserAgent;
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                request.AllowAutoRedirect = true;
                request.KeepAlive = false;
                response = (HttpWebResponse)request.GetResponse();
                string text;
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }
                string sha = ParseShaText(text);
                if (sha.Length == 0)
                {
                    Config.Log("自更新：校验文件里没有 64 位 SHA-256");
                }
                return sha;
            }
            catch (Exception ex)
            {
                Config.Log("自更新：读取 .sha256 失败 → " + ex.Message);
                return "";
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

        internal static string ParseShaText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string sha = ExtractSha(lines[i]);
                if (sha.Length == 64)
                {
                    return sha;
                }
            }
            return "";
        }

        /// <summary>从一行文本里取出第一个完整的 64 位十六进制串（忽略文件名、空格与前后缀）。</summary>
        internal static string ExtractSha(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            int start = -1;
            for (int i = 0; i < text.Length; i++)
            {
                bool hex = IsHex(text[i]);
                if (hex && start < 0)
                {
                    start = i;
                }
                else if (!hex && start >= 0)
                {
                    if (i - start == 64)
                    {
                        return text.Substring(start, 64).ToLowerInvariant();
                    }
                    start = -1;
                }
            }
            if (start >= 0 && text.Length - start == 64)
            {
                return text.Substring(start, 64).ToLowerInvariant();
            }
            return "";
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        // ---------------- 速度 / 进度 ----------------

        private void RecordSample(long received)
        {
            DateTime now = DateTime.UtcNow;
            lock (_sync)
            {
                _samples.Add(new Sample());
                Sample sample = _samples[_samples.Count - 1];
                sample.At = now;
                sample.Bytes = received;
                while (_samples.Count > 2 && (now - _samples[0].At).TotalSeconds > 1.0)
                {
                    _samples.RemoveAt(0);
                }
                if (_samples.Count < 2)
                {
                    return;
                }
                Sample first = _samples[0];
                double seconds = (now - first.At).TotalSeconds;
                if (seconds < 0.25)
                {
                    return;
                }
                double instant = (received - first.Bytes) / seconds;
                if (instant < 0)
                {
                    instant = 0;
                }
                _ema = _hasEma ? _ema + 0.3 * (instant - _ema) : instant;
                _hasEma = true;
            }
        }

        private double CurrentSpeed()
        {
            lock (_sync)
            {
                return _hasEma ? _ema : 0;
            }
        }

        private bool SpeedReady
        {
            get
            {
                lock (_sync)
                {
                    return _hasEma;
                }
            }
        }

        private void ReportProgress(long received, long total, bool force)
        {
            DownloadProgress progress = new DownloadProgress();
            progress.Phase = DownloadPhase.Downloading;
            progress.Received = received;
            progress.Total = total;
            progress.Speed = CurrentSpeed();
            progress.SpeedReady = SpeedReady;
            if (progress.SpeedReady && total > received && progress.Speed > 0)
            {
                progress.EtaSeconds = (long)Math.Round((total - received) / progress.Speed);
            }
            Report(progress, force);
        }

        private void ReportPhase(DownloadPhase phase, string message)
        {
            DownloadProgress progress = new DownloadProgress();
            progress.Phase = phase;
            progress.Message = message ?? "";
            if (phase == DownloadPhase.Failed)
            {
                progress.Received = PartLength(_versionText);
            }
            Report(progress, true);
        }

        /// <summary>界面刷新不超过 10 次/秒：同一阶段 100 毫秒内且字节没变就丢掉。</summary>
        private void Report(DownloadProgress progress, bool force)
        {
            if (_report == null)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (!force && progress.Phase == DownloadPhase.Downloading &&
                (now - _lastReportAt).TotalMilliseconds < ReportIntervalMs &&
                progress.Received == _lastReportBytes)
            {
                return;
            }
            if (!force && progress.Phase == _lastReportPhase &&
                (now - _lastReportAt).TotalMilliseconds < ReportIntervalMs)
            {
                return;
            }
            _lastReportAt = now;
            _lastReportBytes = progress.Received;
            _lastReportPhase = progress.Phase;
            try
            {
                _report(progress.Copy());
            }
            catch
            {
            }
        }

        // ---------------- 工具 ----------------

        /// <summary>退避等待：每 100 毫秒检查一次取消，取消时立刻返回 false。</summary>
        private bool SleepInterruptible(int milliseconds)
        {
            int left = milliseconds;
            while (left > 0)
            {
                if (_cancel)
                {
                    return false;
                }
                Thread.Sleep(Math.Min(100, left));
                left -= 100;
            }
            return !_cancel;
        }

        private bool HasEnoughSpace(string dir, long expectedSize)
        {
            if (expectedSize <= 0)
            {
                return true;
            }
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(dir));
                if (string.IsNullOrEmpty(root))
                {
                    return true;
                }
                DriveInfo drive = new DriveInfo(root);
                long free = drive.AvailableFreeSpace;
                long need = expectedSize * 2;
                if (free < need)
                {
                    Config.Log("自更新：磁盘剩余 " + DownloadFormat.Size(free) + "，需要 " + DownloadFormat.Size(need));
                    return false;
                }
                return true;
            }
            catch
            {
                // 拿不到磁盘信息（网络盘/权限）时不阻断下载，只写日志。
                Config.Log("自更新：无法读取磁盘剩余空间，跳过检查");
                return true;
            }
        }

        internal static string HashFile(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                                          BufferSize))
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
            catch (Exception ex)
            {
                Config.Log("自更新：计算哈希失败 → " + ex.Message);
                return "";
            }
        }

        private static void TryDelete(string path)
        {
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

        private static string DescribeError(Exception ex)
        {
            WebException web = ex as WebException;
            if (web != null)
            {
                switch (web.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                        return "域名解析失败";
                    case WebExceptionStatus.ConnectFailure:
                        return "连接失败";
                    case WebExceptionStatus.Timeout:
                        return "连接或读取超时";
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.ReceiveFailure:
                    case WebExceptionStatus.SendFailure:
                        return "连接被中断";
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.SecureChannelFailure:
                        return "TLS 握手失败";
                }
                if (!string.IsNullOrEmpty(web.Message))
                {
                    return web.Message;
                }
            }
            if (!string.IsNullOrEmpty(ex.Message))
            {
                return ex.Message;
            }
            return ex.GetType().Name;
        }
    }
}
