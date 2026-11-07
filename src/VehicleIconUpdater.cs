using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace WtSkin
{
    internal class IconUpdateResult
    {
        public bool Completed;
        public bool Paused;
        public int Collected;
        public int Deduped;
        public int Skipped;
        public int Failed;
        public int Probed;
        public int Recovered;
        public int ProbesDeferred;
        /// <summary>本次任务实际发出的 HTTP 请求次数（失败探测也算）。</summary>
        public int Requests;
        public int ConsecutiveFailures;
        public string Message = "";
        public string FirstError = "";
    }

    /// <summary>
    /// 图标采集任务（只在用户点「更新数据 / 更新全部数据」时运行，渲染路径永不调用）：
    /// · 请求间隔 ≥1s、超时 10s；单条单任务最多 2 次（首次 + 1 次重试，间隔 ≥3s）；
    /// · 跨任务累计 attemptCount 上限 5，达到即 permanent，仍留在清单里可见；
    /// · 每次任务开始对 permanent 条目各探测 1 次（不重试、不计数）；超过 100 条时排到主任务之后；
    /// · 连续失败 &gt; 10 条自动暂停，清单即时落盘，下次从断点继续；
    /// · 已存在且 sha256 与清单一致的条目直接跳过，不重复请求；
    /// · 相同 sha256 只存一份文件，清单复用同一文件。
    /// </summary>
    internal static class VehicleIconUpdater
    {
        public const int MinGapMs = 1100;
        public const int TimeoutMs = 10000;
        public const int RetryDelayMs = 3000;
        public const int MaxConsecutiveFailures = 10;
        public const int MaxIconSide = 384;
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) WTSkinManager/3.5";
        private const string MainUrl = "https://static.encyclopedia.warthunder.com/images/{0}.png";
        private const string SlotUrl = "https://static.encyclopedia.warthunder.com/slots/{0}.png";

        private static readonly object Gate = new object();
        private static DateTime _lastRequest = DateTime.MinValue;

        /// <summary>测试用：替换取图函数（返回原始图片字节，失败返回 null）。</summary>
        internal static Func<string, byte[]> FetchOverride;

        /// <summary>测试用：为 true 时不做请求间隔/重试等待。</summary>
        internal static bool FastMode;

        public static IconUpdateResult Update(bool full, IList<string> scope, Action<string> progress)
        {
            IconUpdateResult result = new IconUpdateResult();
            PrepareTls();
            VehicleCatalog.EnsureLoaded();
            if (VehicleCatalog.Count == 0)
            {
                result.Message = "图标清单为空：" + VehicleCatalog.ManifestPath;
                result.Completed = true;
                return result;
            }

            List<VehicleIconEntry> all = VehicleCatalog.Snapshot();
            List<VehicleIconEntry> probe = new List<VehicleIconEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Status == VehicleIconEntry.StatusPermanent)
                {
                    probe.Add(all[i]);
                }
            }
            bool deferProbe = probe.Count > VehicleCatalog.ProbeDeferAbove;
            result.ProbesDeferred = deferProbe ? probe.Count : 0;

            if (!deferProbe && probe.Count > 0)
            {
                ProbeAll(probe, result, progress);
            }

            List<VehicleIconEntry> work = Filter(all, scope);
            int done = 0;
            int consecutive = 0;
            for (int i = 0; i < work.Count; i++)
            {
                VehicleIconEntry entry = work[i];
                done++;
                if (!NeedsWork(entry))
                {
                    result.Skipped++;
                    continue;
                }
                if (entry.Status == VehicleIconEntry.StatusPermanent &&
                    entry.AttemptCount >= VehicleCatalog.MaxAttempts)
                {
                    // 已达上限且本次探测被延后：主任务不再请求。
                    result.Skipped++;
                    continue;
                }
                bool ok = CollectOne(entry, result, progress, done, work.Count);
                if (ok)
                {
                    consecutive = 0;
                }
                else
                {
                    consecutive++;
                    result.ConsecutiveFailures = consecutive;
                    if (consecutive > MaxConsecutiveFailures)
                    {
                        result.Paused = true;
                        result.Message = "连续失败 " + consecutive + " 条，已自动暂停。已完成的条目不会重复请求。";
                        VehicleCatalog.Save();
                        Config.Log("图标采集暂停：" + result.Message);
                        return result;
                    }
                }
                if (done % 10 == 0)
                {
                    VehicleCatalog.Save();
                }
            }

            if (deferProbe)
            {
                ProbeAll(probe, result, progress);
            }

            VehicleCatalog.Save();
            result.Completed = true;
            result.Message = string.Format(CultureInfo.InvariantCulture,
                "图标：新增 {0} · 跳过 {1} · 失败 {2} · 恢复 {3} · 探测 {4}",
                result.Collected, result.Skipped, result.Failed, result.Recovered, result.Probed);
            return result;
        }

        /// <summary>「重试失败项」：清零计数并立即各请求一次。</summary>
        public static IconUpdateResult RetryFailed(Action<string> progress)
        {
            int n = VehicleCatalog.ResetFailed();
            IconUpdateResult result = new IconUpdateResult();
            result.Message = "已重置 " + n + " 条失败记录。";
            if (n == 0)
            {
                result.Completed = true;
                return result;
            }
            List<VehicleIconEntry> failed = new List<VehicleIconEntry>();
            List<VehicleIconEntry> all = VehicleCatalog.Snapshot();
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsOk)
                {
                    failed.Add(all[i]);
                }
            }
            PrepareTls();
            int consecutive = 0;
            for (int i = 0; i < failed.Count; i++)
            {
                VehicleIconEntry entry = failed[i];
                Report(progress, "重试失败项 " + (i + 1) + "/" + failed.Count + "：" + entry.Slug);
                bool ok = CollectOnce(entry, result);
                if (!ok)
                {
                    consecutive++;
                    if (consecutive > MaxConsecutiveFailures)
                    {
                        result.Paused = true;
                        break;
                    }
                }
                else
                {
                    consecutive = 0;
                }
            }
            VehicleCatalog.Save();
            result.Completed = !result.Paused;
            result.Message = string.Format(CultureInfo.InvariantCulture,
                "重试失败项：成功 {0} · 仍失败 {1} · 请求 {2} 次",
                result.Collected, result.Failed, result.Requests);
            return result;
        }

        private static List<VehicleIconEntry> Filter(List<VehicleIconEntry> all, IList<string> scope)
        {
            if (scope == null || scope.Count == 0)
            {
                return all;
            }
            HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < scope.Count; i++)
            {
                if (!string.IsNullOrEmpty(scope[i]))
                {
                    wanted.Add(scope[i].Trim());
                }
            }
            List<VehicleIconEntry> list = new List<VehicleIconEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (wanted.Contains(all[i].Slug))
                {
                    list.Add(all[i]);
                }
            }
            return list;
        }

        /// <summary>ok 且文件在、sha 对得上才算完成；否则需要重采。</summary>
        private static bool NeedsWork(VehicleIconEntry entry)
        {
            if (!entry.IsOk)
            {
                return true;
            }
            string path = VehicleCatalog.ResolveFile(entry);
            if (path.Length == 0)
            {
                entry.Status = VehicleIconEntry.StatusRetrying;
                entry.LastError = "清单登记的文件不存在";
                entry.AttemptCount = Math.Max(entry.AttemptCount, 1);
                return true;
            }
            string sha = VehicleCatalog.Sha256OfFile(path);
            if (!string.Equals(sha, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = VehicleIconEntry.StatusRetrying;
                entry.LastError = "文件 sha256 与清单不一致";
                entry.AttemptCount = Math.Max(entry.AttemptCount, 1);
                return true;
            }
            return false;
        }

        private static bool CollectOne(VehicleIconEntry entry, IconUpdateResult result, Action<string> progress,
                                       int done, int total)
        {
            int attempts = 0;
            while (attempts < VehicleCatalog.MaxAttemptsPerTask &&
                   entry.AttemptCount < VehicleCatalog.MaxAttempts)
            {
                attempts++;
                bool ok = CollectOnce(entry, result);
                VehicleCatalog.Save();
                if (ok)
                {
                    return true;
                }
                if (attempts < VehicleCatalog.MaxAttemptsPerTask &&
                    entry.AttemptCount < VehicleCatalog.MaxAttempts)
                {
                    Report(progress, string.Format(CultureInfo.InvariantCulture,
                        "图标 {0}/{1} · {2} 首次失败，{3}s 后重试（累计 {4}/{5}）",
                        done, total, entry.Slug, RetryDelayMs / 1000,
                        entry.AttemptCount, VehicleCatalog.MaxAttempts));
                    Sleep(RetryDelayMs);
                }
            }
            return false;
        }

        private static bool CollectOnce(VehicleIconEntry entry, IconUpdateResult result)
        {
            string error = null;
            byte[] raw = null;
            try
            {
                raw = FetchImage(entry.Slug, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                raw = null;
            }
            result.Requests++;
            if (raw != null)
            {
                byte[] data;
                string normalizeError;
                data = Normalize(raw, out normalizeError);
                if (data != null)
                {
                    bool deduped = VehicleCatalog.StoreIcon(entry, data);
                    VehicleCatalog.MarkAttempt(entry, true, "");
                    if (deduped)
                    {
                        result.Deduped++;
                    }
                    else
                    {
                        result.Collected++;
                    }
                    return true;
                }
                error = normalizeError;
            }
            if (string.IsNullOrEmpty(error))
            {
                error = "取图失败";
            }
            VehicleCatalog.MarkAttempt(entry, false, error);
            if (result.FirstError.Length == 0)
            {
                result.FirstError = entry.Slug + "：" + error;
            }
            result.Failed++;
            Config.Log("图标失败 " + entry.Slug + "（累计 " + entry.AttemptCount + "）：" + error);
            return false;
        }

        /// <summary>永久失败探测：每条只请求一次，不重试、不累加计数。</summary>
        private static void ProbeAll(List<VehicleIconEntry> list, IconUpdateResult result, Action<string> progress)
        {
            for (int i = 0; i < list.Count; i++)
            {
                VehicleIconEntry entry = list[i];
                Report(progress, "探测永久失败 " + (i + 1) + "/" + list.Count + "：" + entry.Slug);
                string error = null;
                byte[] raw = null;
                try
                {
                    raw = FetchImage(entry.Slug, out error);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                result.Requests++;
                result.Probed++;
                if (raw != null)
                {
                    string normalizeError;
                    byte[] data = Normalize(raw, out normalizeError);
                    if (data != null)
                    {
                        bool deduped = VehicleCatalog.StoreIcon(entry, data);
                        VehicleCatalog.MarkProbe(entry, true, "");
                        result.Recovered++;
                        result.Collected += deduped ? 0 : 1;
                        continue;
                    }
                    error = normalizeError;
                }
                VehicleCatalog.MarkProbe(entry, false, error == null ? "探测失败" : error);
            }
            VehicleCatalog.Save();
        }

        private static void Report(Action<string> progress, string text)
        {
            if (progress == null)
            {
                return;
            }
            try
            {
                progress(text);
            }
            catch
            {
            }
        }

        private static void Sleep(int ms)
        {
            if (FastMode)
            {
                return;
            }
            Thread.Sleep(ms);
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

        private static void WaitGap()
        {
            if (FastMode)
            {
                return;
            }
            lock (Gate)
            {
                TimeSpan elapsed = DateTime.Now - _lastRequest;
                int wait = MinGapMs - (int)elapsed.TotalMilliseconds;
                if (wait > 0)
                {
                    Thread.Sleep(wait);
                }
                _lastRequest = DateTime.Now;
            }
        }

        /// <summary>取一张图：先取详情页主图，404 时退回 67×66 缩略图；每次尝试只算一条请求。</summary>
        private static byte[] FetchImage(string slug, out string error)
        {
            error = null;
            if (FetchOverride != null)
            {
                WaitGap();
                return FetchOverride(slug);
            }
            string[] urls = new string[]
            {
                string.Format(CultureInfo.InvariantCulture, MainUrl, slug),
                string.Format(CultureInfo.InvariantCulture, SlotUrl, slug)
            };
            for (int i = 0; i < urls.Length; i++)
            {
                WaitGap();
                byte[] data = HttpGet(urls[i], out error);
                if (data != null)
                {
                    return data;
                }
                if (error != null && error.StartsWith("HTTP 404", StringComparison.Ordinal))
                {
                    continue;
                }
                return null;
            }
            return null;
        }

        private static byte[] HttpGet(string url, out string error)
        {
            error = null;
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
                request.Accept = "image/png,image/*;q=0.8,*/*;q=0.5";
                response = (HttpWebResponse)request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null)
                    {
                        error = "空响应";
                        return null;
                    }
                    using (MemoryStream memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[16384];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memory.Write(buffer, 0, read);
                        }
                        return memory.ToArray();
                    }
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse failure = ex.Response as HttpWebResponse;
                if (failure != null)
                {
                    error = "HTTP " + (int)failure.StatusCode;
                    try { failure.Close(); } catch { }
                }
                else
                {
                    error = ex.Status == WebExceptionStatus.Timeout ? "超时" : ex.Message;
                }
                return null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
            finally
            {
                if (response != null)
                {
                    try { response.Close(); } catch { }
                }
            }
        }

        /// <summary>
        /// 归一化：裁掉透明外边距 → 等比缩到最长边 ≤384 → PNG；超过 120KB 就继续降尺寸。
        /// 绝不拉伸、绝不裁掉有像素的内容。
        /// </summary>
        internal static byte[] Normalize(byte[] raw, out string error)
        {
            error = null;
            if (raw == null || raw.Length < 128)
            {
                error = "图片为空";
                return null;
            }
            if (raw[0] == '<')
            {
                error = "返回的是网页而不是图片";
                return null;
            }
            try
            {
                using (MemoryStream memory = new MemoryStream(raw))
                using (Bitmap source = new Bitmap(memory))
                {
                    Rectangle box = ContentBox(source);
                    using (Bitmap cropped = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(cropped))
                        {
                            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            g.DrawImage(source, new Rectangle(0, 0, cropped.Width, cropped.Height), box, GraphicsUnit.Pixel);
                        }
                        int side = MaxIconSide;
                        for (int round = 0; round < 6; round++)
                        {
                            double factor = Math.Min(1.0, (double)side / Math.Max(cropped.Width, cropped.Height));
                            int w = Math.Max(1, (int)Math.Round(cropped.Width * factor));
                            int h = Math.Max(1, (int)Math.Round(cropped.Height * factor));
                            byte[] png;
                            using (Bitmap resized = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                            {
                                using (Graphics g = Graphics.FromImage(resized))
                                {
                                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.DrawImage(cropped, new Rectangle(0, 0, w, h));
                                }
                                png = EncodePng(resized);
                            }
                            if (png != null && png.LongLength <= VehicleCatalog.MaxIconBytes)
                            {
                                return png;
                            }
                            side = (int)(side * 0.75);
                            if (side < 96)
                            {
                                break;
                            }
                        }
                        error = "压缩后仍超过 120KB";
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                error = "图片解析失败：" + ex.Message;
                return null;
            }
        }

        private static Rectangle ContentBox(Bitmap bitmap)
        {
            Rectangle full = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            try
            {
                BitmapData data = bitmap.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] buffer = new byte[stride * bitmap.Height];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                    int minX = bitmap.Width;
                    int minY = bitmap.Height;
                    int maxX = -1;
                    int maxY = -1;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        int row = y * stride;
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            if (buffer[row + x * 4 + 3] > 8)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }
                    if (maxX < minX || maxY < minY)
                    {
                        return full;
                    }
                    return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
            catch
            {
                return full;
            }
        }

        private static byte[] EncodePng(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }
    }
}
