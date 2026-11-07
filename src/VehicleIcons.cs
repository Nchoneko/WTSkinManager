using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;

namespace WtSkin
{
    /// <summary>
    /// 随包分发的本地载具图标（assets\vehicles\&lt;slug&gt;.png + manifest.json）。
    /// 运行期只按清单查表、按需读磁盘，不联网、不写缓存、不做任何静默下载；
    /// 也不需要扫描目录——清单是唯一数据源。
    /// 找不到对应 slug（或文件缺失）时统一返回占位图（载具剪影 + 「暂无图标」）。
    /// 图标按需加载，最近使用的 96 张留在内存里，滚动画廊不会把整包读进内存。
    /// </summary>
    internal static class VehicleIcons
    {
        private const int CacheLimit = 96;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Bitmap> Cache =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Lru = new LinkedList<string>();
        private static Bitmap _placeholder;
        private static bool _placeholderLoaded;

        /// <summary>清单里登记的载具数量（不是内存里的图片数）。</summary>
        public static int Count
        {
            get { return VehicleCatalog.Count; }
        }

        public static Bitmap Placeholder
        {
            get
            {
                EnsurePlaceholder();
                return _placeholder;
            }
        }

        /// <summary>取本地图标；不存在或读取失败时返回统一占位图，绝不返回 null。</summary>
        public static Bitmap Get(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return Placeholder;
            }
            string slug = unitId.Trim();
            lock (Sync)
            {
                Bitmap cached;
                if (Cache.TryGetValue(slug, out cached) && cached != null)
                {
                    Touch(slug);
                    return cached;
                }
            }
            VehicleIconEntry entry = VehicleCatalog.Get(slug);
            Bitmap bitmap = null;
            if (entry != null)
            {
                string path = VehicleCatalog.ResolveFile(entry);
                if (path.Length > 0)
                {
                    bitmap = LoadBitmap(path);
                }
            }
            if (bitmap == null)
            {
                bitmap = LoadEmbedded(slug);
            }
            if (bitmap == null && slug != slug.ToLowerInvariant())
            {
                // 本地文件夹名大小写与 wiki slug 不一致时（如 ..._Gepard / ..._gepard）回退小写匹配
                bitmap = LoadEmbedded(slug.ToLowerInvariant());
            }
            if (bitmap == null)
            {
                if (entry == null)
                {
                    VehicleIcons.Unlisted++;
                }
                return Placeholder;
            }
            lock (Sync)
            {
                if (Cache.ContainsKey(slug))
                {
                    bitmap.Dispose();
                    return Cache[slug];
                }
                Cache[slug] = bitmap;
                Lru.AddFirst(slug);
                Trim();
            }
            return bitmap;
        }

        private static ZipArchive _zip;
        private static Stream _zipStream;
        private static readonly object ZipSync = new object();

        /// <summary>单文件分发时，图标从 exe 内嵌的 vehicles.zip 读取。</summary>
        private static Bitmap LoadEmbedded(string slug)
        {
            try
            {
                lock (ZipSync)
                {
                    if (_zip == null)
                    {
                        _zipStream = typeof(VehicleIcons).Assembly.GetManifestResourceStream("vehicles.zip");
                        if (_zipStream == null)
                        {
                            return null;
                        }
                        _zip = new ZipArchive(_zipStream, ZipArchiveMode.Read);
                    }
                    ZipArchiveEntry entry = _zip.GetEntry(slug + ".png");
                    if (entry == null)
                    {
                        return null;
                    }
                    using (Stream stream = entry.Open())
                    using (Bitmap src = new Bitmap(stream))
                    {
                        return new Bitmap(src);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>清单外被请求的 slug 次数（只用于排查，不影响显示）。</summary>
        public static int Unlisted;

        public static int CachedCount
        {
            get { lock (Sync) { return Cache.Count; } }
        }

        /// <summary>清空内存缓存（切换数据源后调用）。</summary>
        public static void ClearCache()
        {
            lock (Sync)
            {
                foreach (KeyValuePair<string, Bitmap> pair in Cache)
                {
                    try { pair.Value.Dispose(); } catch { }
                }
                Cache.Clear();
                Lru.Clear();
                _placeholderLoaded = false;
                if (_placeholder != null)
                {
                    try { _placeholder.Dispose(); } catch { }
                    _placeholder = null;
                }
            }
        }

        private static void Touch(string slug)
        {
            Lru.Remove(slug);
            Lru.AddFirst(slug);
        }

        private static void Trim()
        {
            while (Cache.Count > CacheLimit && Lru.Count > 0)
            {
                string last = Lru.Last.Value;
                Lru.RemoveLast();
                Bitmap bitmap;
                if (Cache.TryGetValue(last, out bitmap))
                {
                    Cache.Remove(last);
                    try { bitmap.Dispose(); } catch { }
                }
            }
        }

        private static Bitmap LoadBitmap(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                using (MemoryStream memory = new MemoryStream(data))
                using (Bitmap source = new Bitmap(memory))
                {
                    Bitmap copy = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(copy))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
                    }
                    return copy;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void EnsurePlaceholder()
        {
            if (_placeholderLoaded)
            {
                return;
            }
            lock (Sync)
            {
                if (_placeholderLoaded)
                {
                    return;
                }
                _placeholderLoaded = true;
                string path = VehicleCatalog.PlaceholderPath;
                if (path.Length > 0)
                {
                    _placeholder = LoadBitmap(path);
                }
                if (_placeholder == null)
                {
                    _placeholder = CreateFallbackPlaceholder();
                }
            }
        }

        /// <summary>
        /// 占位图缺失时的最后兜底：代码画一张同样的剪影占位图。
        /// 正常打包一定有 _placeholder.png，这里只防破图/空白。
        /// </summary>
        private static Bitmap CreateFallbackPlaceholder()
        {
            Bitmap bitmap = new Bitmap(192, 192, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Color line = Color.FromArgb(150, 158, 170);
                Color fill = Color.FromArgb(64, 150, 158, 170);
                using (SolidBrush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(line, 3f))
                {
                    g.FillRectangle(brush, 24, 104, 144, 40);
                    g.DrawRectangle(pen, 24, 104, 144, 40);
                    g.FillPolygon(brush, new Point[] {
                        new Point(32, 104), new Point(54, 84), new Point(150, 84), new Point(166, 104) });
                    g.DrawPolygon(pen, new Point[] {
                        new Point(32, 104), new Point(54, 84), new Point(150, 84), new Point(166, 104) });
                    g.FillPolygon(brush, new Point[] {
                        new Point(70, 84), new Point(84, 64), new Point(132, 64), new Point(145, 84) });
                    g.DrawPolygon(pen, new Point[] {
                        new Point(70, 84), new Point(84, 64), new Point(132, 64), new Point(145, 84) });
                    g.DrawLine(pen, 132, 70, 184, 46);
                }
                using (Font font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular, GraphicsUnit.Point))
                using (SolidBrush text = new SolidBrush(Color.FromArgb(175, 181, 190)))
                {
                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    g.DrawString("暂无图标", font, text, new RectangleF(0, 154, 192, 30), format);
                }
            }
            return bitmap;
        }
    }
}
