using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;

namespace WtSkin
{
    internal class Vehicle
    {
        public string Id;
        public string Zh;
        public string En;
        public string Nation = "";
    }

    /// <summary>国家（阵营）名称与国旗绘制。</summary>
    internal static class NationInfo
    {
        private static Dictionary<string, string> _names;

        private static void EnsureNames()
        {
            if (_names != null)
            {
                return;
            }
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _names["usa"] = "美国";
            _names["germany"] = "德国";
            _names["ussr"] = "苏联/俄罗斯";
            _names["britain"] = "英国";
            _names["japan"] = "日本";
            _names["china"] = "中国";
            _names["italy"] = "意大利";
            _names["france"] = "法国";
            _names["sweden"] = "瑞典";
            _names["israel"] = "以色列";
        }

        /// <summary>内置国家名（映射表缺失时的兜底）。界面显示请用 Name / NameMap。</summary>
        public static string BaseName(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return "";
            }
            EnsureNames();
            string name;
            return _names.TryGetValue(code, out name) ? name : code;
        }

        /// <summary>界面显示名：优先读取 shortnames.ini 里的国家映射（苏联/俄罗斯 → 俄罗斯）。</summary>
        public static string Name(string code)
        {
            return NameMap.NationName(code);
        }

        /// <summary>主界面/导入列表的国家排序：美、德、苏、英、日、中、意、法、瑞、以。</summary>
        public static int Rank(string code)
        {
            switch ((code ?? "").ToLowerInvariant())
            {
                case "usa": return 0;
                case "germany": return 1;
                case "ussr": return 2;
                case "britain": return 3;
                case "japan": return 4;
                case "china": return 5;
                case "italy": return 6;
                case "france": return 7;
                case "sweden": return 8;
                case "israel": return 9;
                default: return 10;
            }
        }

        public static int CompareUnits(DetectedUnit a, DetectedUnit b)
        {
            string ca = a != null && a.Vehicle != null ? a.Vehicle.Nation : "";
            string cb = b != null && b.Vehicle != null ? b.Vehicle.Nation : "";
            int rank = Rank(ca).CompareTo(Rank(cb));
            if (rank != 0)
            {
                return rank;
            }
            string ta = a == null ? "" : a.Title;
            string tb = b == null ? "" : b.Title;
            return string.Compare(ta, tb, StringComparison.CurrentCulture);
        }

        public static bool Known(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }
            EnsureNames();
            return _names.ContainsKey(code);
        }

        /// <summary>从载具 ID 里猜国家，作为对照表缺失时的兜底。</summary>
        public static string GuessFromId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "";
            }
            string lower = id.ToLowerInvariant();
            string[] codes = new string[] { "usa", "germany", "ussr", "britain", "japan", "china", "italy", "france", "sweden", "israel" };
            for (int i = 0; i < codes.Length; i++)
            {
                if (lower.IndexOf("_" + codes[i] + "_", StringComparison.Ordinal) >= 0)
                {
                    return codes[i];
                }
            }
            if (lower.StartsWith("ussr_") || lower.StartsWith("rus_"))
            {
                return "ussr";
            }
            if (lower.StartsWith("germ_"))
            {
                return "germany";
            }
            if (lower.StartsWith("us_") && !lower.StartsWith("ussr_"))
            {
                return "usa";
            }
            if (lower.StartsWith("uk_") || lower.StartsWith("brit_"))
            {
                return "britain";
            }
            if (lower.StartsWith("jp_"))
            {
                return "japan";
            }
            if (lower.StartsWith("cn_"))
            {
                return "china";
            }
            if (lower.StartsWith("fr_"))
            {
                return "france";
            }
            if (lower.StartsWith("it_"))
            {
                return "italy";
            }
            if (lower.StartsWith("sw_"))
            {
                return "sweden";
            }
            if (lower.StartsWith("il_"))
            {
                return "israel";
            }
            return "";
        }

        private static readonly Dictionary<string, Bitmap> FlagCache = new Dictionary<string, Bitmap>();

        /// <summary>画一面长方形小旗（4 倍超级采样后缩小，红色底上的星仍能分辨）。</summary>
        public static void Draw(Graphics g, Rectangle rect, string code)
        {
            if (rect.Width < 8 || rect.Height < 6)
            {
                return;
            }
            Bitmap flag = GetFlag(code, rect.Width, rect.Height);
            if (flag == null)
            {
                return;
            }
            System.Drawing.Drawing2D.InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(flag, rect);
            g.InterpolationMode = old;
        }

        private static Bitmap GetFlag(string code, int width, int height)
        {
            string key = (code ?? "").ToLowerInvariant() + "|" + width + "x" + height;
            lock (FlagCache)
            {
                Bitmap cached;
                if (FlagCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }
            Bitmap rendered = RenderFlag(code ?? "", width, height);
            lock (FlagCache)
            {
                FlagCache[key] = rendered;
            }
            return rendered;
        }

        private static Bitmap RenderFlag(string code, int width, int height)
        {
            const int SuperSample = 4;
            int bigW = Math.Max(8, width * SuperSample);
            int bigH = Math.Max(8, height * SuperSample);
            Bitmap canvas = new Bitmap(bigW, bigH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (GraphicsPath clip = new GraphicsPath())
                {
                    clip.AddRectangle(new Rectangle(0, 0, bigW, bigH));
                    g.SetClip(clip);
                    DrawField(g, new Rectangle(0, 0, bigW, bigH), code);
                }
                g.ResetClip();
                float ring = Math.Max(1f, Math.Min(bigW, bigH) * 0.045f);
                using (Pen pen = new Pen(Color.FromArgb(95, 255, 255, 255), ring))
                {
                    g.DrawRectangle(pen, ring / 2f, ring / 2f, bigW - ring, bigH - ring);
                }
            }

            Bitmap result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(canvas, new Rectangle(0, 0, width, height));
            }
            canvas.Dispose();
            return result;
        }

        private static void DrawField(Graphics g, Rectangle inner, string code)
        {
            switch ((code ?? "").ToLowerInvariant())
            {
                case "germany":
                    Band(g, inner, 0, 3, Color.FromArgb(24, 24, 24));
                    Band(g, inner, 1, 3, Color.FromArgb(200, 38, 44));
                    Band(g, inner, 2, 3, Color.FromArgb(240, 190, 40));
                    break;
                case "france":
                    VBand(g, inner, 0, 3, Color.FromArgb(32, 72, 160));
                    VBand(g, inner, 1, 3, Color.FromArgb(245, 245, 245));
                    VBand(g, inner, 2, 3, Color.FromArgb(210, 50, 60));
                    break;
                case "italy":
                    VBand(g, inner, 0, 3, Color.FromArgb(40, 140, 80));
                    VBand(g, inner, 1, 3, Color.FromArgb(245, 245, 245));
                    VBand(g, inner, 2, 3, Color.FromArgb(200, 50, 60));
                    break;
                case "usa":
                    for (int i = 0; i < 7; i++)
                    {
                        Band(g, inner, i, 7, i % 2 == 0 ? Color.FromArgb(200, 40, 50) : Color.FromArgb(245, 245, 245));
                    }
                    using (SolidBrush canton = new SolidBrush(Color.FromArgb(38, 66, 140)))
                    {
                        g.FillRectangle(canton, inner.X, inner.Y, inner.Width / 2, inner.Height * 0.52f);
                    }
                    using (SolidBrush dot = new SolidBrush(Color.FromArgb(245, 245, 245)))
                    {
                        float d = Math.Max(1f, inner.Width * 0.035f);
                        for (int row = 0; row < 3; row++)
                        {
                            for (int col = 0; col < 3; col++)
                            {
                                float dx = inner.X + inner.Width * (0.09f + col * 0.14f);
                                float dy = inner.Y + inner.Height * (0.09f + row * 0.15f);
                                g.FillEllipse(dot, dx, dy, d, d);
                            }
                        }
                    }
                    break;
                case "ussr":
                    Band(g, inner, 0, 3, Color.FromArgb(245, 245, 245));
                    Band(g, inner, 1, 3, Color.FromArgb(0, 57, 166));
                    Band(g, inner, 2, 3, Color.FromArgb(213, 43, 30));
                    break;
                case "china":
                    Fill(g, inner, Color.FromArgb(200, 36, 40));
                    Star(g, new PointF(inner.X + inner.Width * 0.28f, inner.Y + inner.Height * 0.30f),
                         inner.Height * 0.18f, Color.FromArgb(250, 210, 60));
                    float[] sx = new float[] { 0.50f, 0.60f, 0.60f, 0.50f };
                    float[] sy = new float[] { 0.18f, 0.30f, 0.44f, 0.54f };
                    for (int i = 0; i < 4; i++)
                    {
                        Star(g, new PointF(inner.X + inner.Width * sx[i], inner.Y + inner.Height * sy[i]),
                             inner.Height * 0.075f, Color.FromArgb(250, 210, 60));
                    }
                    break;
                case "japan":
                    Fill(g, inner, Color.FromArgb(245, 245, 245));
                    using (SolidBrush sun = new SolidBrush(Color.FromArgb(200, 40, 50)))
                    {
                        int d = (int)(inner.Height * 0.60f);
                        g.FillEllipse(sun, inner.X + (inner.Width - d) / 2, inner.Y + (inner.Height - d) / 2, d, d);
                    }
                    break;
                case "britain":
                    Fill(g, inner, Color.FromArgb(30, 50, 110));
                    using (Pen white = new Pen(Color.FromArgb(245, 245, 245), Math.Max(1.2f, inner.Height * 0.16f)))
                    using (Pen red = new Pen(Color.FromArgb(200, 45, 55), Math.Max(1f, inner.Height * 0.07f)))
                    {
                        g.DrawLine(white, inner.Left, inner.Top, inner.Right, inner.Bottom);
                        g.DrawLine(white, inner.Right, inner.Top, inner.Left, inner.Bottom);
                        g.DrawLine(red, inner.Left, inner.Top, inner.Right, inner.Bottom);
                        g.DrawLine(red, inner.Right, inner.Top, inner.Left, inner.Bottom);
                        g.DrawLine(white, inner.Left + inner.Width / 2, inner.Top, inner.Left + inner.Width / 2, inner.Bottom);
                        g.DrawLine(white, inner.Left, inner.Top + inner.Height / 2, inner.Right, inner.Top + inner.Height / 2);
                        g.DrawLine(red, inner.Left + inner.Width / 2, inner.Top, inner.Left + inner.Width / 2, inner.Bottom);
                        g.DrawLine(red, inner.Left, inner.Top + inner.Height / 2, inner.Right, inner.Top + inner.Height / 2);
                    }
                    break;
                case "sweden":
                    Fill(g, inner, Color.FromArgb(40, 90, 160));
                    using (Pen cross = new Pen(Color.FromArgb(245, 205, 60), Math.Max(1.4f, inner.Height * 0.20f)))
                    {
                        g.DrawLine(cross, inner.Left + inner.Width * 0.34f, inner.Top, inner.Left + inner.Width * 0.34f, inner.Bottom);
                        g.DrawLine(cross, inner.Left, inner.Top + inner.Height * 0.5f, inner.Right, inner.Top + inner.Height * 0.5f);
                    }
                    break;
                case "israel":
                    Fill(g, inner, Color.FromArgb(245, 245, 245));
                    Band(g, inner, 1, 6, Color.FromArgb(40, 80, 170));
                    Band(g, inner, 5, 6, Color.FromArgb(40, 80, 170));
                    StarOfDavid(g, new PointF(inner.X + inner.Width * 0.5f, inner.Y + inner.Height * 0.5f),
                                inner.Height * 0.20f, Color.FromArgb(40, 80, 170));
                    break;
                default:
                    using (SolidBrush unknown = new SolidBrush(Color.FromArgb(90, 98, 108)))
                    {
                        g.FillRectangle(unknown, inner);
                    }
                    break;
            }
        }

        private static void StarOfDavid(Graphics g, PointF c, float r, Color color)
        {
            using (Pen pen = new Pen(color, Math.Max(1f, r * 0.24f)))
            {
                PointF[] up = new PointF[]
                {
                    new PointF(c.X, c.Y - r),
                    new PointF(c.X - r * 0.87f, c.Y + r * 0.5f),
                    new PointF(c.X + r * 0.87f, c.Y + r * 0.5f)
                };
                PointF[] down = new PointF[]
                {
                    new PointF(c.X, c.Y + r),
                    new PointF(c.X - r * 0.87f, c.Y - r * 0.5f),
                    new PointF(c.X + r * 0.87f, c.Y - r * 0.5f)
                };
                g.DrawPolygon(pen, up);
                g.DrawPolygon(pen, down);
            }
        }

        private static void Fill(Graphics g, Rectangle r, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, r);
            }
        }

        private static void Band(Graphics g, Rectangle r, int index, int count, Color color)
        {
            int top = r.Top + (int)Math.Round(r.Height * (double)index / count);
            int bottom = r.Top + (int)Math.Round(r.Height * (double)(index + 1) / count);
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, r.Left, top, r.Width, Math.Max(1, bottom - top));
            }
        }

        private static void VBand(Graphics g, Rectangle r, int index, int count, Color color)
        {
            int left = r.Left + (int)Math.Round(r.Width * (double)index / count);
            int right = r.Left + (int)Math.Round(r.Width * (double)(index + 1) / count);
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, left, r.Top, Math.Max(1, right - left), r.Height);
            }
        }

        private static void Star(Graphics g, PointF center, float radius, Color color)
        {
            PointF[] points = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float r = (i % 2 == 0) ? radius : radius * 0.42f;
                points[i] = new PointF(center.X + (float)(Math.Cos(angle) * r), center.Y + (float)(Math.Sin(angle) * r));
            }
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, points);
            }
        }
    }

    /// <summary>内置载具表。</summary>
    internal static class VehicleDb
    {
        private static Dictionary<string, Vehicle> _byId;
        private static List<Vehicle> _all;
        private static bool _loaded;

        public static int Count
        {
            get { EnsureLoaded(); return _all.Count; }
        }

        public static IList<Vehicle> All
        {
            get { EnsureLoaded(); return _all; }
        }

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            _byId = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
            _all = new List<Vehicle>();
            try
            {
                using (Stream stream = typeof(VehicleDb).Assembly.GetManifestResourceStream("vehicles.dat"))
                {
                    if (stream == null)
                    {
                        return;
                    }
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0)
                            {
                                continue;
                            }
                            if (line[0] == '#' || line[0] == ';')
                            {
                                continue;
                            }
                            string[] parts = line.Split('\t');
                            if (parts.Length < 3)
                            {
                                continue;
                            }
                            Vehicle v = new Vehicle();
                            v.Id = parts[0];
                            v.Zh = parts[1];
                            v.En = parts[2];
                            _all.Add(v);
                            _byId[v.Id] = v;
                        }
                    }
                }
            }
            catch
            {
            }

            Dictionary<string, string> nations = LoadNations();
            for (int i = 0; i < _all.Count; i++)
            {
                Vehicle v = _all[i];
                string code;
                if (!nations.TryGetValue(v.Id, out code))
                {
                    code = NationInfo.GuessFromId(v.Id);
                }
                v.Nation = code ?? "";
            }

            // 用户覆盖表最后应用：先读内置清单，再覆盖同名条目。
            UserOverrides.ApplyVehicles(_byId, _all);

            _all.Sort(delegate(Vehicle a, Vehicle b)
            {
                return string.Compare(a.Zh, b.Zh, StringComparison.CurrentCulture);
            });
        }

        private static Dictionary<string, string> LoadNations()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (Stream stream = typeof(VehicleDb).Assembly.GetManifestResourceStream("nations.dat"))
                {
                    if (stream == null)
                    {
                        return map;
                    }
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                            {
                                continue;
                            }
                            int tab = line.IndexOf('\t');
                            if (tab <= 0)
                            {
                                continue;
                            }
                            string id = line.Substring(0, tab).Trim();
                            string code = line.Substring(tab + 1).Trim();
                            if (id.Length > 0 && code.Length > 0)
                            {
                                map[id] = code;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return map;
        }

        public static Vehicle Find(string unitId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }
            Vehicle v;
            if (_byId.TryGetValue(unitId, out v))
            {
                return v;
            }
            return null;
        }

        public static List<Vehicle> Search(string query, int limit)
        {
            EnsureLoaded();
            List<Vehicle> result = new List<Vehicle>();
            string q = (query == null ? "" : query.Trim()).ToLowerInvariant();
            if (q.Length == 0)
            {
                for (int i = 0; i < _all.Count && result.Count < limit; i++)
                {
                    result.Add(_all[i]);
                }
                return result;
            }

            List<Vehicle> starts = new List<Vehicle>();
            List<Vehicle> contains = new List<Vehicle>();
            for (int i = 0; i < _all.Count; i++)
            {
                Vehicle v = _all[i];
                string zh = v.Zh.ToLowerInvariant();
                string en = v.En.ToLowerInvariant();
                string id = v.Id.ToLowerInvariant();
                if (zh.StartsWith(q) || en.StartsWith(q) || id.StartsWith(q))
                {
                    starts.Add(v);
                }
                else if (zh.IndexOf(q, StringComparison.Ordinal) >= 0 ||
                         en.IndexOf(q, StringComparison.Ordinal) >= 0 ||
                         id.IndexOf(q, StringComparison.Ordinal) >= 0)
                {
                    contains.Add(v);
                }
                if (starts.Count >= limit)
                {
                    break;
                }
            }
            for (int i = 0; i < starts.Count && result.Count < limit; i++)
            {
                result.Add(starts[i]);
            }
            for (int i = 0; i < contains.Count && result.Count < limit; i++)
            {
                result.Add(contains[i]);
            }
            return result;
        }

        public static Vehicle Suggest(string rawId, out int confidence)
        {
            EnsureLoaded();
            confidence = 0;
            if (string.IsNullOrEmpty(rawId))
            {
                return null;
            }
            string id = Normalize(rawId);
            Vehicle best = null;
            int bestScore = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                Vehicle v = _all[i];
                int score = Similarity(id, Normalize(v.Id));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            confidence = bestScore;
            if (bestScore < 62)
            {
                return null;
            }
            return best;
        }

        private static string Normalize(string id)
        {
            return id.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        private static int Similarity(string a, string b)
        {
            if (a == b)
            {
                return 100;
            }
            if (a.StartsWith(b) || b.StartsWith(a))
            {
                int diff = Math.Abs(a.Length - b.Length);
                return 92 - Math.Min(20, diff);
            }
            string[] ta = a.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            string[] tb = b.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (ta.Length == 0 || tb.Length == 0)
            {
                return 0;
            }
            int common = 0;
            for (int i = 0; i < ta.Length; i++)
            {
                for (int j = 0; j < tb.Length; j++)
                {
                    if (ta[i] == tb[j])
                    {
                        common++;
                        break;
                    }
                }
            }
            int union = ta.Length + tb.Length - common;
            int jaccard = (int)(60.0 * common / Math.Max(1, union));
            int prefix = 0;
            int min = Math.Min(a.Length, b.Length);
            while (prefix < min && a[prefix] == b[prefix])
            {
                prefix++;
            }
            int prefixScore = (int)(30.0 * prefix / Math.Max(1, Math.Max(a.Length, b.Length)));
            return Math.Max(jaccard, prefixScore);
        }
    }
}
