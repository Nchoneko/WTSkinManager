using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>设置页里的小字链接：手型光标、悬停下划线，不抢焦点。</summary>
    internal class LinkText : Control
    {
        private bool _hover;

        public LinkText()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            ForeColor = Ui.Accent;
            Font = Ui.Small;
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = Ui.S(20);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width, Height);
            Ui.DrawText(g, Text, Font, Enabled ? ForeColor : Ui.TextFaint, r,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (_hover)
            {
                Size size = TextRenderer.MeasureText(Text, Font);
                int width = Math.Min(size.Width, Width);
                int y = Height / 2 + Ui.S(7);
                using (Pen pen = new Pen(ForeColor, 1f))
                {
                    g.DrawLine(pen, 0, y, width, y);
                }
            }
        }
    }

    /// <summary>手动检查时的 10 秒加载转圈；静默检查不会创建/启动它。</summary>
    internal class Spinner : Control
    {
        private readonly Timer _timer;
        private int _angle;
        private bool _running;

        public Spinner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Width = Ui.S(16);
            Height = Ui.S(16);
            Visible = false;
            _timer = new Timer();
            _timer.Interval = 60;
            _timer.Tick += delegate
            {
                _angle = (_angle + 30) % 360;
                Invalidate();
            };
        }

        public void Start()
        {
            _running = true;
            Visible = true;
            _angle = 0;
            _timer.Start();
            Invalidate();
            if (Parent != null)
            {
                Parent.Invalidate();
            }
        }

        public void Stop()
        {
            _running = false;
            _timer.Stop();
            Visible = false;
            Invalidate();
            if (Parent != null)
            {
                Parent.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_running)
            {
                return;
            }
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (Pen track = new Pen(Ui.LineSoft, 1.6f))
            {
                g.DrawArc(track, r, 0, 360);
            }
            using (Pen pen = new Pen(Ui.Accent, 2f))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(pen, r, _angle, 110);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>主界面顶部「发现新版本」提示条；可关闭，点「查看」定位到设置页检查更新区块。</summary>
    internal class UpdateBanner : Control
    {
        private string _versionText = "";
        private bool _hoverView;
        private bool _hoverClose;

        public event EventHandler ViewClicked;
        public event EventHandler CloseClicked;

        public UpdateBanner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Height = Ui.S(40);
            Cursor = Cursors.Default;
        }

        public void SetVersion(string text)
        {
            _versionText = text ?? "";
            Invalidate();
        }

        private Rectangle ViewRect()
        {
            return new Rectangle(Width - Ui.S(116), 0, Ui.S(54), Height);
        }

        private Rectangle CloseRect()
        {
            return new Rectangle(Width - Ui.S(48), 0, Ui.S(40), Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Mix(Ui.Panel, Ui.Accent, 0.10f)))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillRectangle(brush, 0, 0, Ui.S(3), Height);
            }
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
            string text = "发现新版本 " + _versionText;
            Ui.DrawText(g, text, Ui.BodyBold, Ui.Text,
                    new Rectangle(Ui.S(22), 0, Math.Max(Ui.S(80), Width - Ui.S(180)), Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);

            Rectangle view = ViewRect();
            Color viewColor = _hoverView ? Ui.AccentDeep : Ui.Accent;
            Ui.DrawText(g, "查看", Ui.BodyBold, viewColor, view,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (_hoverView)
            {
                int y = view.Top + view.Height / 2 + Ui.S(9);
                int x = view.Left + view.Width / 2 - Ui.S(14);
                using (Pen pen = new Pen(viewColor, 1f))
                {
                    g.DrawLine(pen, x, y, x + Ui.S(28), y);
                }
            }

            Rectangle close = CloseRect();
            if (_hoverClose)
            {
                Ui.FillRound(g, new Rectangle(close.X + Ui.S(8), close.Y + Ui.S(8),
                                              close.Width - Ui.S(16), close.Height - Ui.S(16)),
                             Ui.S(6), Ui.PanelHover);
            }
            using (Pen pen = new Pen(_hoverClose ? Ui.Text : Ui.TextDim, 1.4f))
            {
                int cx = close.Left + close.Width / 2;
                int cy = close.Top + close.Height / 2;
                int d = Ui.S(5);
                g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                g.DrawLine(pen, cx + d, cy - d, cx - d, cy + d);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool view = ViewRect().Contains(e.Location);
            bool close = CloseRect().Contains(e.Location);
            if (view != _hoverView || close != _hoverClose)
            {
                _hoverView = view;
                _hoverClose = close;
                Cursor = (view || close) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverView = false;
            _hoverClose = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (ViewRect().Contains(e.Location))
                {
                    if (ViewClicked != null)
                    {
                        ViewClicked(this, EventArgs.Empty);
                    }
                    return;
                }
                if (CloseRect().Contains(e.Location))
                {
                    if (CloseClicked != null)
                    {
                        CloseClicked(this, EventArgs.Empty);
                    }
                    return;
                }
            }
            base.OnMouseClick(e);
        }
    }

    /// <summary>从远端 release 追加到更新日志顶部的纯文本说明。</summary>
    internal class RemoteUpdate
    {
        public SemVer Version;
        public string DateText = "";
        public string Body = "";
    }

    /// <summary>
    /// 设置页内平铺的更新日志：按版本倒序、类型彩色标签、数据变更单行、容器内滚动。
    /// 数据与安装完成窗口同源（ReleaseLog / updates.dat），不另写文案。
    /// </summary>
    internal class SettingsLogView : Control
    {
        private List<ReleaseNote> _notes = new List<ReleaseNote>();
        private int _count;
        private RemoteUpdate _remote;
        private readonly List<NoteLine> _lines = new List<NoteLine>();
        private readonly List<int> _rules = new List<int>();
        private bool _rebuilding;

        private const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top |
                                             TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                                             TextFormatFlags.TextBoxControl;
        private static readonly Regex FilePattern = new Regex(
            @"[A-Za-z0-9_\-\./]+\.(?:dat|ini|json|zip|png|cer|txt|xml|csv|bat|ps1)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public SettingsLogView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Font = Ui.Body;
            Width = Ui.S(560);
        }

        public void SetContent(List<ReleaseNote> notes, int count, RemoteUpdate remote)
        {
            _notes = notes == null ? new List<ReleaseNote>() : notes;
            _count = Math.Max(0, Math.Min(count, _notes.Count));
            _remote = remote;
            Rebuild();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_rebuilding && Width > 0)
            {
                Rebuild();
            }
        }

        public void Rebuild()
        {
            _rebuilding = true;
            try
            {
                _lines.Clear();
                _rules.Clear();
                int width = Math.Max(Ui.S(240), Width - Ui.S(2));
                Height = BuildLayout(width);
            }
            finally
            {
                _rebuilding = false;
            }
            Invalidate();
        }

        private static int Measure(string text, Font font, int width, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            Size size = TextRenderer.MeasureText(text, font, new Size(Math.Max(20, width), int.MaxValue), flags);
            return size.Height;
        }

        private static NoteLine Block(string text, Font font, Color color, int x, int y, int width, int height,
                                      TextFormatFlags flags)
        {
            NoteLine line = new NoteLine();
            line.Rect = new Rectangle(x, y, width, height);
            line.Text = text;
            line.Font = font;
            line.Color = color;
            line.Flags = flags;
            return line;
        }

        private int BuildLayout(int width)
        {
            int x = Ui.S(8);
            int contentWidth = width - Ui.S(16);
            int y = Ui.S(2);

            if (_remote != null)
            {
                y = RemoteBlock(x, y, contentWidth);
            }

            if (_notes.Count == 0 || _count == 0)
            {
                string empty = "暂无更新记录";
                int h = Measure(empty, Ui.Small, contentWidth, Wrap);
                _lines.Add(Block(empty, Ui.Small, Ui.TextFaint, x, y + Ui.S(8), contentWidth, h, Wrap));
                return y + h + Ui.S(20);
            }

            for (int i = 0; i < _count; i++)
            {
                ReleaseNote note = _notes[i];
                y += i == 0 ? 0 : Ui.S(8);
                y = NoteBlock(note, x, y, contentWidth);
            }
            return y + Ui.S(8);
        }

        private int RemoteBlock(int x, int y, int contentWidth)
        {
            string versionText = "v" + (_remote.Version == null ? "?" : _remote.Version.ToDisplay());
            string header = versionText + (_remote.DateText.Length > 0 ? " · " + _remote.DateText : "");
            int headHeight = Measure(header, Ui.BodyBold, contentWidth - Ui.S(90), Wrap);
            _lines.Add(Block(header, Ui.BodyBold, Ui.Accent, x, y, contentWidth - Ui.S(90), headHeight, Wrap));

            string chip = "可更新";
            int chipWidth = ChipWidth(chip);
            int chipX = x + Ui.S(4) + TextRenderer.MeasureText(header, Ui.BodyBold).Width;
            NoteLine chipLine = new NoteLine();
            chipLine.Rect = new Rectangle(chipX, y + Ui.S(2), chipWidth, Ui.S(18));
            chipLine.Fill = Ui.Mix(Ui.Panel, Ui.Accent, 0.22f);
            chipLine.Radius = Ui.S(4);
            chipLine.Text = chip;
            chipLine.Font = Ui.SmallBold;
            chipLine.Color = Ui.Accent;
            chipLine.Flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                             TextFormatFlags.NoPrefix;
            if (chipLine.Rect.Right < x + contentWidth - Ui.S(10))
            {
                _lines.Add(chipLine);
            }
            y += Math.Max(headHeight, Ui.S(18)) + Ui.S(4);

            string body = string.IsNullOrEmpty(_remote.Body) ? "远端没有提供发布说明" : _remote.Body;
            int bodyHeight = Measure(body, Ui.Small, contentWidth, Wrap);
            _lines.Add(Block(body, Ui.Small, Ui.TextDim, x, y, contentWidth, bodyHeight, Wrap));
            y += bodyHeight + Ui.S(8);
            _rules.Add(y);
            y += Ui.S(8);
            return y;
        }

        private int NoteBlock(ReleaseNote note, int x, int y, int contentWidth)
        {
            string version = "v" + note.VersionText +
                             (note.Date.Length > 0 ? " · " + FormatNoteDate(note.Date) : "");
            int headHeight = Measure(version, Ui.BodyBold, contentWidth, Wrap);
            _lines.Add(Block(version, Ui.BodyBold, Ui.Text, x, y, contentWidth, headHeight, Wrap));
            y += Math.Max(headHeight, Ui.S(18)) + Ui.S(2);
            _rules.Add(y);
            y += Ui.S(7);

            int groups = 0;
            y = Group("新增", note.Added, Ui.Good, Ui.GoodBg, x, y, contentWidth, ref groups);
            y = Group("修复", note.Fixed, Ui.Accent, Ui.Mix(Ui.Panel, Ui.Accent, 0.20f), x, y, contentWidth, ref groups);
            y = Group("优化", note.Improved, Ui.TextDim, Ui.PanelActive, x, y, contentWidth, ref groups);
            y = Group("移除", note.Removed, Ui.Bad, Ui.BadBg, x, y, contentWidth, ref groups);
            y = DataBlock(note, x, y, contentWidth, ref groups);

            if (groups == 0)
            {
                string empty = "这个版本没有记录变更";
                int h = Measure(empty, Ui.Small, contentWidth, Wrap);
                _lines.Add(Block(empty, Ui.Small, Ui.TextFaint, x, y, contentWidth, h, Wrap));
                y += h + Ui.S(4);
            }
            return y + Ui.S(6);
        }

        private static string FormatNoteDate(string date)
        {
            string trimmed = date.Trim();
            if (trimmed.Length == 10 && trimmed[4] == '-' && trimmed[7] == '-')
            {
                return trimmed.Replace('-', '/');
            }
            return trimmed;
        }

        private static int ChipWidth(string text)
        {
            return TextRenderer.MeasureText(text, Ui.SmallBold).Width + Ui.S(14);
        }

        private void AddChip(string title, Color color, Color fill, int x, int y)
        {
            NoteLine chip = new NoteLine();
            chip.Rect = new Rectangle(x, y, ChipWidth(title), Ui.S(18));
            chip.Fill = fill;
            chip.Radius = Ui.S(4);
            chip.Text = title;
            chip.Font = Ui.SmallBold;
            chip.Color = color;
            chip.Flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                         TextFormatFlags.NoPrefix;
            _lines.Add(chip);
        }

        private int Group(string title, List<string> items, Color color, Color fill, int x, int y, int contentWidth,
                          ref int groups)
        {
            if (items == null || items.Count == 0)
            {
                return y;
            }
            groups++;
            AddChip(title, color, fill, x, y);
            y += Ui.S(21);
            for (int i = 0; i < items.Count; i++)
            {
                string text = "· " + items[i];
                int h = Measure(text, Ui.Body, contentWidth - Ui.S(10), Wrap);
                _lines.Add(Block(text, Ui.Body, Ui.Text, x + Ui.S(10), y, contentWidth - Ui.S(10), h, Wrap));
                y += h + Ui.S(2);
            }
            return y + Ui.S(4);
        }

        private int DataBlock(ReleaseNote note, int x, int y, int contentWidth, ref int groups)
        {
            if (note.DataChanges.Count == 0)
            {
                return y;
            }
            groups++;
            AddChip("数据变更", Ui.Warn, Ui.WarnBg, x, y);
            int chipWidth = ChipWidth("数据变更");

            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < note.DataChanges.Count; i++)
            {
                MatchCollection matches = FilePattern.Matches(note.DataChanges[i]);
                for (int m = 0; m < matches.Count; m++)
                {
                    files.Add(matches[m].Value);
                }
            }
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            if (files.Count > 0)
            {
                summary.Append(files.Count).Append(" 个文件 · ");
            }
            summary.Append(note.DataChanges.Count).Append(" 条");
            _lines.Add(Block(summary.ToString(), Ui.Small, Ui.Warn,
                             x + chipWidth + Ui.S(8), y + Ui.S(1), contentWidth - chipWidth - Ui.S(8),
                             Ui.S(18), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix));
            y += Ui.S(22);
            for (int i = 0; i < note.DataChanges.Count; i++)
            {
                string text = "· " + note.DataChanges[i];
                int h = Measure(text, Ui.Small, contentWidth - Ui.S(10), Wrap);
                _lines.Add(Block(text, Ui.Small, Ui.TextDim, x + Ui.S(10), y, contentWidth - Ui.S(10), h, Wrap));
                y += h + Ui.S(2);
            }
            return y + Ui.S(4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Panel))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                NoteLine line = _lines[i];
                if (line.Fill != Color.Empty)
                {
                    Ui.FillRound(g, line.Rect, Math.Max(1, line.Radius), line.Fill);
                }
                if (line.Text.Length > 0)
                {
                    Rectangle rect = line.Rect;
                    rect.Y += Ui.S(1);
                    TextRenderer.DrawText(g, line.Text, line.Font, rect, line.Color, line.Flags);
                }
            }
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    g.DrawLine(pen, Ui.S(8), _rules[i], Width - Ui.S(10), _rules[i]);
                }
            }
        }
    }

    /// <summary>设置页「检查更新」区块：版本、最近检查时间、手动检查、自动检查开关与三态结果。</summary>
    internal class UpdateCheckSection : Panel
    {
        private Label _current;
        private Label _last;
        private Label _status;
        private Label _detail;
        private FlatButton _checkButton;
        private FlatButton _download;
        private FlatButton _releases;
        private CheckRow _autoRow;
        private LinkText _allLink;
        private LinkText _expandLink;
        private BufferedPanel _summaryPanel;
        private Label _summaryLabel;
        private Spinner _spinner;

        private UpdateCheckResult _result;
        private List<string> _summaryLines = new List<string>();
        private bool _checking;
        private bool _expanded;
        private bool _hasResult;

        public event Action<UpdateCheckResult> ResultChanged;
        public event Action HeightChanged;

        public UpdateCheckSection()
        {
            BackColor = Ui.Panel;
            Width = Ui.S(608);
            Height = Ui.S(136);
            Build();
            ApplyLayout();
        }

        private Label MakeLabel(string text, int x, int y, int width, int height, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Left = Ui.S(x);
            label.Top = Ui.S(y);
            label.Width = Ui.S(width);
            label.Height = Ui.S(height);
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Ui.Panel;
            label.AutoSize = false;
            Controls.Add(label);
            return label;
        }

        private FlatButton MakeButton(string text, int x, int y, int width, int height, bool primary)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Left = Ui.S(x);
            button.Top = Ui.S(y);
            button.Width = Ui.S(width);
            button.Height = Ui.S(height);
            button.Primary = primary;
            button.BackColor = Ui.Panel;
            Controls.Add(button);
            return button;
        }

        private void Build()
        {
            MakeLabel("检查更新", 8, 2, 200, 18, Ui.SmallBold, Ui.TextDim);
            _current = MakeLabel("当前版本 " + BuildInfo.DisplayVersion, 8, 24, 260, 22, Ui.BodyBold, Ui.Text);

            _last = MakeLabel("上次检查：尚未检查", 270, 26, 330, 20, Ui.Small, Ui.TextFaint);
            _last.TextAlign = ContentAlignment.MiddleRight;

            _checkButton = MakeButton("检查更新", 8, 54, 112, 36, true);
            _checkButton.Click += delegate { CheckNow(); };

            _autoRow = new CheckRow();
            _autoRow.Title = "启动时自动检查更新";
            _autoRow.Subtitle = "关闭后仅在手动点击时检查";
            _autoRow.Checked = Config.AutoCheckUpdate;
            _autoRow.Left = Ui.S(132);
            _autoRow.Top = Ui.S(50);
            _autoRow.Width = Ui.S(356);
            _autoRow.Height = Ui.S(44);
            _autoRow.BackColor = Ui.Panel;
            _autoRow.Click += delegate
            {
                Config.AutoCheckUpdate = _autoRow.Checked;
                Config.Save();
                Config.Log("设置：启动时自动检查更新 = " + (Config.AutoCheckUpdate ? "开" : "关"));
            };
            Controls.Add(_autoRow);

            _allLink = new LinkText();
            _allLink.Text = "查看所有版本";
            _allLink.Left = Ui.S(494);
            _allLink.Top = Ui.S(62);
            _allLink.Width = Ui.S(106);
            _allLink.Font = Ui.Small;
            _allLink.Click += delegate { UpdateService.OpenUrl(Config.ReleasesPage); };
            Controls.Add(_allLink);

            _status = MakeLabel("尚未检查更新", 8, 104, 560, 22, Ui.BodyBold, Ui.TextFaint);

            _spinner = new Spinner();
            _spinner.Left = Ui.S(580);
            _spinner.Top = Ui.S(106);
            Controls.Add(_spinner);

            _detail = MakeLabel("", 8, 130, 570, 20, Ui.Small, Ui.Warn);
            _detail.Visible = false;

            _summaryPanel = new BufferedPanel();
            _summaryPanel.Left = Ui.S(8);
            _summaryPanel.Top = Ui.S(130);
            _summaryPanel.Width = Ui.S(590);
            _summaryPanel.Height = Ui.S(90);
            _summaryPanel.BackColor = Ui.Panel;
            _summaryPanel.Visible = false;
            _summaryPanel.AutoScroll = false;
            Controls.Add(_summaryPanel);

            _summaryLabel = new Label();
            _summaryLabel.Left = 0;
            _summaryLabel.Top = 0;
            _summaryLabel.Width = Ui.S(580);
            _summaryLabel.Height = Ui.S(86);
            _summaryLabel.Font = Ui.Small;
            _summaryLabel.ForeColor = Ui.TextDim;
            _summaryLabel.BackColor = Ui.Panel;
            _summaryLabel.AutoSize = false;
            _summaryPanel.Controls.Add(_summaryLabel);

            _download = MakeButton("前往下载", 8, 132, 120, 34, true);
            _download.Visible = false;
            _download.Click += delegate
            {
                string url = _result == null ? "" : _result.DownloadUrl;
                if (!UpdateService.IsHttpUrl(url))
                {
                    url = Config.ReleasesPage;
                }
                UpdateService.OpenUrl(url);
            };

            _releases = MakeButton("打开 Releases 页面", 8, 158, 160, 34, false);
            _releases.Visible = false;
            _releases.Click += delegate { UpdateService.OpenUrl(Config.ReleasesPage); };

            _expandLink = new LinkText();
            _expandLink.Text = "展开";
            _expandLink.Left = Ui.S(536);
            _expandLink.Top = Ui.S(136);
            _expandLink.Width = Ui.S(56);
            _expandLink.Font = Ui.Small;
            _expandLink.Visible = false;
            _expandLink.Click += delegate
            {
                _expanded = !_expanded;
                _expandLink.Text = _expanded ? "收起" : "展开";
                UpdateSummary();
                ApplyLayout();
            };
            Controls.Add(_expandLink);
        }

        /// <summary>打开设置页 / 切到本页时调用：只读缓存，不发请求。</summary>
        public void Activate()
        {
            _autoRow.Checked = Config.AutoCheckUpdate;
            UpdateCache cache = UpdateService.ReadCache();
            if (cache != null && cache.LastCheck != DateTime.MinValue)
            {
                _last.Text = "上次检查：" + UpdateService.FormatTime(cache.LastCheck);
            }
            if (_hasResult)
            {
                return;
            }
            UpdateCheckResult cached = UpdateService.FromCachePublic(cache);
            if (cached != null)
            {
                ApplyResult(cached);
            }
        }

        private void CheckNow()
        {
            if (_checking)
            {
                return;
            }
            SetChecking(true);
            UpdateService.CheckAsync(false, delegate(UpdateCheckResult result)
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }
                SetChecking(false);
                ApplyResult(result);
            });
        }

        private void SetChecking(bool checking)
        {
            _checking = checking;
            _checkButton.Enabled = !checking;
            _checkButton.Text = checking ? "检查中…" : "检查更新";
            if (checking)
            {
                _spinner.Start();
                _status.Text = "正在检查更新…";
                _status.ForeColor = Ui.TextDim;
                _detail.Visible = false;
            }
            else
            {
                _spinner.Stop();
            }
        }

        private void ApplyResult(UpdateCheckResult result)
        {
            _result = result;
            _hasResult = true;
            _expanded = false;
            _expandLink.Text = "展开";

            if (result.CheckedAt != DateTime.MinValue)
            {
                _last.Text = "上次检查：" + UpdateService.FormatTime(result.CheckedAt);
            }

            _detail.Visible = false;
            _summaryPanel.Visible = false;
            _download.Visible = false;
            _releases.Visible = false;
            _expandLink.Visible = false;

            switch (result.State)
            {
                case UpdateState.NewVersion:
                    _status.Text = "发现新版本 " + result.VersionText +
                                   (result.DateText.Length > 0 ? " · " + result.DateText : "");
                    _status.ForeColor = Ui.Accent;
                    BuildSummary(result.Body);
                    _download.Visible = true;
                    break;
                case UpdateState.Failed:
                    _status.Text = "检查失败，请稍后重试";
                    _status.ForeColor = Ui.Bad;
                    _detail.Text = "原因：" + result.FailureText;
                    _detail.Visible = true;
                    _releases.Visible = true;
                    break;
                default:
                    _status.Text = "当前已是最新版本 " + BuildInfo.DisplayVersion;
                    _status.ForeColor = Ui.Good;
                    break;
            }

            ApplyLayout();
            if (ResultChanged != null)
            {
                ResultChanged(result);
            }
        }

        private void BuildSummary(string body)
        {
            _summaryLines = WrapBody(body, Ui.Small, Ui.S(574));
            bool hasBody = _summaryLines.Count > 0 && body.Trim().Length > 0;
            _summaryPanel.Visible = hasBody;
            _expandLink.Visible = hasBody && _summaryLines.Count > 5;
            if (hasBody)
            {
                UpdateSummary();
            }
        }

        private static List<string> WrapBody(string body, Font font, int width)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(body))
            {
                return lines;
            }
            string[] paragraphs = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < paragraphs.Length; i++)
            {
                string paragraph = paragraphs[i].Trim();
                if (paragraph.Length == 0)
                {
                    continue;
                }
                lines.AddRange(Ui.WrapLines(paragraph, font, width));
            }
            return lines;
        }

        private static string WithEllipsis(string text, Font font, int width)
        {
            string last = text;
            while (last.Length > 0 && TextRenderer.MeasureText(last + "…", font).Width > width)
            {
                last = last.Substring(0, last.Length - 1);
            }
            return last + "…";
        }

        private void UpdateSummary()
        {
            if (_summaryLines.Count == 0)
            {
                _summaryLabel.Text = "";
                return;
            }
            int lineHeight = TextRenderer.MeasureText("国Ag", Ui.Small).Height;
            List<string> visible;
            if (_expanded)
            {
                visible = _summaryLines;
            }
            else
            {
                visible = _summaryLines.GetRange(0, Math.Min(5, _summaryLines.Count));
                if (_summaryLines.Count > 5)
                {
                    visible[visible.Count - 1] = WithEllipsis(visible[visible.Count - 1], Ui.Small, Ui.S(574));
                }
            }
            _summaryLabel.Text = string.Join("\n", visible.ToArray());
            int labelWidth = _summaryPanel.Width - Ui.S(_expanded ? 22 : 8);
            if (labelWidth < Ui.S(100))
            {
                labelWidth = Ui.S(100);
            }
            _summaryLabel.Width = labelWidth;
            _summaryLabel.Height = Math.Max(_summaryPanel.Height, visible.Count * lineHeight + Ui.S(4));
            _summaryPanel.AutoScroll = _expanded;
            _summaryPanel.AutoScrollPosition = new Point(0, 0);
        }

        private void ApplyLayout()
        {
            bool newVersion = _result != null && _result.State == UpdateState.NewVersion;
            bool failed = _result != null && _result.State == UpdateState.Failed;
            bool hasSummary = newVersion && _summaryLines.Count > 0;

            _detail.Left = Ui.S(8);
            _detail.Top = Ui.S(130);
            _detail.Width = Ui.S(570);

            if (newVersion)
            {
                _download.Left = Ui.S(8);
                _releases.Visible = false;
                _detail.Visible = false;
                if (hasSummary)
                {
                    int summaryHeight = _expanded ? Ui.S(140) : Ui.S(90);
                    _summaryPanel.Visible = true;
                    _summaryPanel.Left = Ui.S(8);
                    _summaryPanel.Top = Ui.S(130);
                    _summaryPanel.Width = Ui.S(590);
                    _summaryPanel.Height = summaryHeight;
                    int actionTop = 130 + (_expanded ? 140 : 90) + 4;
                    _download.Top = Ui.S(actionTop);
                    _expandLink.Left = Ui.S(536);
                    _expandLink.Top = Ui.S(actionTop + 6);
                    _expandLink.Width = Ui.S(56);
                    _expandLink.Visible = _summaryLines.Count > 5;
                    SetHeight(actionTop + 34 + 6);
                }
                else
                {
                    _summaryPanel.Visible = false;
                    _download.Top = Ui.S(130);
                    _expandLink.Visible = false;
                    SetHeight(130 + 34 + 6);
                }
                return;
            }

            _summaryPanel.Visible = false;
            _expandLink.Visible = false;
            _download.Visible = false;

            if (failed)
            {
                _detail.Visible = true;
                _releases.Visible = true;
                _releases.Left = Ui.S(8);
                _releases.Top = Ui.S(156);
                _releases.Width = Ui.S(160);
                SetHeight(156 + 34 + 6);
                return;
            }

            _releases.Visible = false;
            _detail.Visible = false;
            SetHeight(136);
        }

        private void SetHeight(int height)
        {
            int value = Ui.S(height);
            if (Height == value)
            {
                return;
            }
            Height = value;
            if (HeightChanged != null)
            {
                HeightChanged();
            }
        }
    }
}
