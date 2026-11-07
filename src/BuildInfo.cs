using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>构建时写入的版本与构建时间（唯一来源：assets\build-info.ini → 内嵌资源）。</summary>
    internal static class BuildInfo
    {
        private static bool _loaded;
        private static string _full = "";
        private static string _buildTime = "";
        private static string _version = "";

        public static string DisplayVersion
        {
            get { EnsureLoaded(); return _version; }
        }

        public static string BuildTime
        {
            get { EnsureLoaded(); return _buildTime; }
        }

        public static string FullVersion
        {
            get { EnsureLoaded(); return _full; }
        }

        public static string HeaderText
        {
            get
            {
                EnsureLoaded();
                if (string.IsNullOrEmpty(_buildTime))
                {
                    return _version;
                }
                return _version + " · " + _buildTime;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                using (Stream stream = typeof(BuildInfo).Assembly.GetManifestResourceStream("build-info.dat"))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                int eq = line.IndexOf('=');
                                if (eq <= 0)
                                {
                                    continue;
                                }
                                string key = line.Substring(0, eq).Trim();
                                string value = line.Substring(eq + 1).Trim();
                                if (key == "version")
                                {
                                    _full = value;
                                }
                                else if (key == "buildTime")
                                {
                                    _buildTime = value;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(_full))
            {
                try
                {
                    _full = typeof(BuildInfo).Assembly.GetName().Version.ToString();
                }
                catch
                {
                    _full = "0.0.0.0";
                }
            }
            string[] parts = _full.Split('.');
            string shortVersion = parts.Length >= 3 ? parts[0] + "." + parts[1] + "." + parts[2] : _full;
            _version = "v" + shortVersion;
        }
    }

    /// <summary>标题栏版本信息的悬停浮层。</summary>
    internal class BuildInfoTip : Form
    {
        private readonly List<string> _lines = new List<string>();

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public BuildInfoTip()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Ui.Panel;
            DoubleBuffered = true;
            Padding = new Padding(Ui.S(14));
        }

        public void SetLines(IEnumerable<string> lines)
        {
            _lines.Clear();
            _lines.AddRange(lines);
            using (Graphics g = CreateGraphics())
            {
                int width = 0;
                Font body = Ui.Small;
                for (int i = 0; i < _lines.Count; i++)
                {
                    width = Math.Max(width, TextRenderer.MeasureText(_lines[i], body).Width);
                }
                Width = width + Ui.S(30);
                Height = _lines.Count * Ui.S(22) + Ui.S(24);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Ui.S(10), Ui.Panel);
            Ui.DrawRound(g, r, Ui.S(10), Ui.Line, 1f);
            int y = Ui.S(10);
            for (int i = 0; i < _lines.Count; i++)
            {
                Color color = i == 0 ? Ui.Text : Ui.TextDim;
                Ui.DrawText(g, _lines[i], i == 0 ? Ui.SmallBold : Ui.Small, color,
                        new Rectangle(Ui.S(14), y, Width - Ui.S(28), Ui.S(20)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                y += Ui.S(22);
            }
        }
    }
}
