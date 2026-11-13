using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private FlatButton _cancel;
        private FlatButton _releases;
        private CheckRow _autoRow;
        private LinkText _allLink;
        private LinkText _expandLink;
        private BufferedPanel _summaryPanel;
        private Label _summaryLabel;
        private Spinner _spinner;
        private Label _progressLeft;
        private Label _progressRight;
        private UpdateProgressBar _bar;

        private UpdateCheckResult _result;
        private List<string> _summaryLines = new List<string>();
        private bool _checking;
        private bool _expanded;
        private bool _hasResult;

        // ---- 应用内下载状态（进度区占原按钮行，通过 HeightChanged 让更新日志顺延） ----
        private UpdateDownloader _downloader;
        private DownloadPhase _phase = DownloadPhase.Idle;
        private DownloadProgress _lastProgress;
        private bool _downloadFailed;
        private string _downloadError = "";
        private bool _ready;
        private string _readyExe = "";
        private string _readySha = "";
        private long _readySize;
        private long _partBytes;
        private long _partTotal;

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

            _download = MakeButton("下载更新", 8, 132, 120, 34, true);
            _download.Visible = false;
            _download.Click += delegate { OnPrimaryAction(); };

            // 下载中禁用主按钮，右侧出现「取消」；取消后保留 .part，主按钮变「继续下载」。
            _cancel = MakeButton("取消", 136, 132, 84, 34, false);
            _cancel.Visible = false;
            _cancel.Click += delegate { CancelDownload(); };

            _progressLeft = MakeLabel("", 8, 176, 360, 20, Ui.Small, Ui.TextDim);
            _progressLeft.Visible = false;
            _progressRight = MakeLabel("", 368, 176, 232, 20, Ui.Small, Ui.TextFaint);
            _progressRight.TextAlign = ContentAlignment.MiddleRight;
            _progressRight.Visible = false;

            _bar = new UpdateProgressBar();
            _bar.Left = Ui.S(8);
            _bar.Top = Ui.S(198);
            _bar.Width = Ui.S(592);
            _bar.Height = Ui.S(6);
            _bar.Visible = false;
            Controls.Add(_bar);

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
                // 重新打开设置页时同步「已下载」状态，但不重复弹就绪窗口。
                SyncReadyState();
                ApplyLayout();
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

            // 新的检查结果回到「未下载」起点；下载进行中则保留当前进度不动。
            if (_downloader == null)
            {
                _downloadFailed = false;
                _downloadError = "";
                _phase = DownloadPhase.Idle;
                _lastProgress = null;
                _partBytes = 0;
                _partTotal = 0;
                _progressLeft.Visible = false;
                _progressRight.Visible = false;
                _bar.Visible = false;
                _cancel.Visible = false;
                _download.Enabled = true;
                _download.Text = "下载更新";
            }
            SyncReadyState();

            ApplyLayout();
            if (ResultChanged != null)
            {
                ResultChanged(result);
            }
        }

        /// <summary>
        /// 同步「已下载并校验通过」状态：磁盘上有 <version>\WTSkinManager.exe 且大小与 ready.ini 一致
        /// 时，按钮变「立即安装」、状态行提示可直接安装（不再自动弹窗）。
        /// </summary>
        private void SyncReadyState()
        {
            if (_result == null || _result.State != UpdateState.NewVersion || _result.Latest == null)
            {
                _ready = false;
                return;
            }
            string version = _result.Latest.ToDisplay();
            string exe;
            string sha;
            long size;
            if (!UpdateDownloader.TryGetReadyPackage(version, out exe, out sha, out size))
            {
                _ready = false;
                // 上次没下完时会留下 .part：按钮直接提示「继续下载」，点下去按 Range 续传。
                long part = UpdateDownloader.PartLength(version);
                if (part > 0 && _downloader == null)
                {
                    _partBytes = part;
                    _partTotal = _result.DownloadSize;
                    _download.Text = "继续下载";
                }
                return;
            }
            _ready = true;
            _readyExe = exe;
            _readySha = sha;
            _readySize = size;
            _downloadFailed = false;
            _downloadError = "";
            _phase = DownloadPhase.Completed;
            _partBytes = size;
            _partTotal = size;
            _download.Enabled = true;
            _download.Text = "立即安装";
            _status.Text = "更新已下载，可立即安装";
            _status.ForeColor = Ui.Good;
            _cancel.Visible = false;
            _progressLeft.Text = "已下载 " + DownloadFormat.Size(size);
            _progressRight.Text = "SHA-256 校验通过";
            _progressLeft.Visible = true;
            _progressRight.Visible = true;
            _bar.SetValue(1, true);
            _bar.Visible = true;
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
                int actionTop = 130;
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
                    actionTop = 130 + (_expanded ? 140 : 90) + 4;
                    _expandLink.Left = Ui.S(536);
                    _expandLink.Top = Ui.S(actionTop + 6);
                    _expandLink.Width = Ui.S(56);
                    _expandLink.Visible = _summaryLines.Count > 5;
                }
                else
                {
                    _summaryPanel.Visible = false;
                    _expandLink.Visible = false;
                }
                _download.Visible = true;
                _download.Left = Ui.S(8);
                _download.Top = Ui.S(actionTop);
                LayoutActionBand(actionTop);
                return;
            }

            _summaryPanel.Visible = false;
            _expandLink.Visible = false;
            _download.Visible = false;
            _cancel.Visible = false;
            _bar.Visible = false;
            _progressLeft.Visible = false;
            _progressRight.Visible = false;

            if (failed)
            {
                _releases.Text = "打开 Releases 页面";
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

        /// <summary>
        /// 按钮行 + 进度区：进度区紧贴按钮行下方（占原按钮行位置），
        /// 高度增长经 SetHeight → HeightChanged 让更新日志整体下移，互不重叠。
        /// </summary>
        private void LayoutActionBand(int actionTop)
        {
            _download.Width = Ui.S(120);

            if (_downloadFailed)
            {
                // 失败兜底：一行提示 +「重试下载」+「打开 GitHub 页面」。
                _releases.Text = "打开 GitHub 页面";
                _releases.Visible = true;
                _releases.Left = Ui.S(136);
                _releases.Top = Ui.S(actionTop + 26);
                _releases.Width = Ui.S(168);
                _detail.Left = Ui.S(8);
                _detail.Top = Ui.S(actionTop);
                _detail.Width = Ui.S(592);
                _detail.Text = string.IsNullOrEmpty(_downloadError) ? "下载失败，请稍后重试" : _downloadError;
                _detail.Visible = true;
                _download.Top = Ui.S(actionTop + 26);
                _cancel.Visible = false;
                _progressLeft.Visible = false;
                _progressRight.Visible = false;
                _bar.Visible = false;
                SetHeight(actionTop + 26 + 34 + 8);
                return;
            }

            _detail.Visible = false;
            bool band = _phase != DownloadPhase.Idle && _phase != DownloadPhase.Failed;
            if (band)
            {
                _cancel.Left = Ui.S(136);
                _cancel.Top = Ui.S(actionTop);
                _cancel.Width = Ui.S(84);
                _cancel.Visible = _phase == DownloadPhase.Preparing || _phase == DownloadPhase.Downloading ||
                                  _phase == DownloadPhase.Retrying || _phase == DownloadPhase.Verifying;
                _progressLeft.Left = Ui.S(8);
                _progressLeft.Top = Ui.S(actionTop + 40);
                _progressLeft.Width = Ui.S(360);
                _progressRight.Left = Ui.S(368);
                _progressRight.Top = Ui.S(actionTop + 40);
                _progressRight.Width = Ui.S(232);
                _progressLeft.Visible = true;
                _progressRight.Visible = true;
                _bar.Left = Ui.S(8);
                _bar.Top = Ui.S(actionTop + 62);
                _bar.Width = Ui.S(592);
                _bar.Visible = true;
                SetHeight(actionTop + 62 + 6 + 10);
                return;
            }

            _cancel.Visible = false;
            _progressLeft.Visible = false;
            _progressRight.Visible = false;
            _bar.Visible = false;
            SetHeight(actionTop + 34 + 6);
        }

        // ---------------- 应用内下载 ----------------

        private void OnPrimaryAction()
        {
            if (_ready)
            {
                ShowReadyDialog();
                return;
            }
            StartDownload();
        }

        private void StartDownload()
        {
            if (_result == null || _result.State != UpdateState.NewVersion || _result.Latest == null)
            {
                return;
            }
            if (_downloader != null)
            {
                return;
            }
            string version = _result.Latest.ToDisplay();
            long existing = UpdateDownloader.PartLength(version);

            _downloadFailed = false;
            _downloadError = "";
            _lastProgress = null;
            _partBytes = existing;
            _partTotal = _result.DownloadSize;
            _phase = DownloadPhase.Preparing;
            _download.Enabled = false;
            _download.Text = "下载中…";
            _cancel.Visible = true;
            _cancel.Enabled = true;
            _cancel.Text = "取消";
            _progressLeft.Text = existing > 0 ? "继续下载…" : "准备下载…";
            _progressLeft.Visible = true;
            _progressRight.Text = "";
            _progressRight.Visible = true;
            _bar.SetValue(existing > 0 && _result.DownloadSize > 0
                ? (double)existing / _result.DownloadSize
                : 0, false);
            _bar.Visible = true;
            ApplyLayout();

            Config.Log("自更新：开始下载 v" + version +
                       (_result.DownloadUrl.Length > 0 ? "，地址 " + _result.DownloadUrl : "") +
                       (existing > 0 ? "，断点续传起始 " + existing + " 字节" : ""));
            UpdateDownloader downloader = new UpdateDownloader(version, _result.DownloadUrl, _result.Sha256,
                                                               _result.Sha256Url, _result.DownloadSize,
                                                               OnDownloadProgress, OnDownloadDone);
            _downloader = downloader;
            downloader.Start();
        }

        private void CancelDownload()
        {
            if (_downloader == null)
            {
                return;
            }
            _cancel.Enabled = false;
            _cancel.Text = "取消中…";
            _progressLeft.Text = "正在取消…";
            _progressRight.Text = "";
            Config.Log("自更新：用户点击取消，等待下载线程收尾");
            _downloader.Cancel();
        }

        private void OnDownloadProgress(DownloadProgress progress)
        {
            if (progress == null || IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }
            try
            {
                // 工作线程 → UI 线程：BeginInvoke（下载器已把刷新限制在 10 次/秒内）。
                BeginInvoke(new MethodInvoker(delegate { UpdateProgressUi(progress); }));
            }
            catch
            {
            }
        }

        private void OnDownloadDone(DownloadResult result)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(new MethodInvoker(delegate { ApplyDownloadDone(result); }));
            }
            catch
            {
            }
        }

        private void UpdateProgressUi(DownloadProgress progress)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }
            _lastProgress = progress;
            _phase = progress.Phase;
            _partBytes = progress.Received;
            if (progress.Total > 0)
            {
                _partTotal = progress.Total;
            }

            switch (progress.Phase)
            {
                case DownloadPhase.Preparing:
                    _progressLeft.Text = progress.Message.Length > 0 ? progress.Message : "准备下载…";
                    _progressRight.Text = "";
                    _bar.SetValue(Fraction(progress.Received, progress.Total), false);
                    _download.Text = "下载中…";
                    _download.Enabled = false;
                    _cancel.Visible = true;
                    break;
                case DownloadPhase.Downloading:
                    _progressLeft.Text = DescribeProgress(progress);
                    _progressRight.Text = DescribeSpeed(progress);
                    _bar.SetValue(Fraction(progress.Received, progress.Total), false);
                    _download.Text = "下载中…";
                    _download.Enabled = false;
                    _cancel.Visible = true;
                    break;
                case DownloadPhase.Retrying:
                    _progressLeft.Text = "网络中断，正在重试 " + progress.Retry + "/" + progress.RetryMax;
                    _progressRight.Text = DescribeSpeed(progress);
                    _bar.SetValue(Fraction(progress.Received, progress.Total), false);
                    _download.Text = "重试中…";
                    _download.Enabled = false;
                    _cancel.Visible = true;
                    break;
                case DownloadPhase.Verifying:
                    _progressLeft.Text = "正在校验…";
                    _progressRight.Text = "";
                    _bar.SetValue(1, false);
                    _download.Text = "校验中…";
                    _download.Enabled = false;
                    _cancel.Visible = false;
                    break;
                case DownloadPhase.Canceled:
                    _download.Text = "继续下载";
                    _download.Enabled = true;
                    _cancel.Visible = false;
                    _progressLeft.Text = progress.Received > 0
                        ? "已取消 · 已下载 " + DownloadFormat.Size(progress.Received)
                        : "已取消";
                    _progressRight.Text = "";
                    _bar.SetValue(Fraction(progress.Received, progress.Total), false);
                    break;
            }
            ApplyLayout();
        }

        private void ApplyDownloadDone(DownloadResult result)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }
            _downloader = null;
            _cancel.Enabled = true;
            _cancel.Text = "取消";
            _download.Enabled = true;

            if (result == null)
            {
                _phase = DownloadPhase.Failed;
                _downloadFailed = true;
                _downloadError = "下载已中断，请重试";
                _download.Text = "重试下载";
                _status.Text = "下载失败";
                _status.ForeColor = Ui.Bad;
                ApplyLayout();
                return;
            }

            if (result.Canceled)
            {
                string version = _result == null || _result.Latest == null ? "" : _result.Latest.ToDisplay();
                long part = result.PartBytes > 0 ? result.PartBytes : UpdateDownloader.PartLength(version);
                _downloadFailed = false;
                _downloadError = "";
                DownloadProgress canceled = new DownloadProgress();
                canceled.Phase = DownloadPhase.Canceled;
                canceled.Received = part;
                canceled.Total = _partTotal > 0 ? _partTotal : (_result == null ? 0 : _result.DownloadSize);
                UpdateProgressUi(canceled);
                Config.Log("自更新：已取消下载，保留 .part " + part + " 字节，可继续下载");
                return;
            }

            if (result.Ok)
            {
                _ready = true;
                _readyExe = result.ExePath;
                _readySha = result.Sha256;
                _readySize = result.Size;
                _phase = DownloadPhase.Completed;
                _downloadFailed = false;
                _downloadError = "";
                _download.Enabled = true;
                _download.Text = "立即安装";
                _status.Text = "更新已下载，可立即安装";
                _status.ForeColor = Ui.Good;
                _cancel.Visible = false;
                _progressLeft.Text = "已下载 " + DownloadFormat.Size(result.Size);
                _progressLeft.Visible = true;
                _progressRight.Text = "SHA-256 校验通过";
                _progressRight.Visible = true;
                _bar.SetValue(1, true);
                _bar.Visible = true;
                Config.Log("自更新：下载并校验完成 → " + result.ExePath);
                ApplyLayout();
                ShowReadyDialog();
                return;
            }

            _phase = DownloadPhase.Failed;
            _downloadFailed = true;
            _downloadError = result.MirrorHint
                ? "GitHub 直连失败，可挂梯子重试，或使用国内镜像下载"
                : (result.Error.Length > 0 ? result.Error : "下载失败，请重试");
            _download.Text = "重试下载";
            _download.Enabled = true;
            _status.Text = "下载失败";
            _status.ForeColor = Ui.Bad;
            _progressLeft.Visible = false;
            _progressRight.Visible = false;
            _bar.Visible = false;
            Config.Log("自更新：下载失败 → " + _downloadError);
            ApplyLayout();
        }

        /// <summary>就绪弹窗：模态、深色现有样式；「稍后」保留已下载文件并显示「可立即安装」。</summary>
        private void ShowReadyDialog()
        {
            if (!_ready || _result == null || _result.Latest == null)
            {
                return;
            }
            Form owner = FindForm();
            bool install;
            using (UpdateReadyDialog dialog = new UpdateReadyDialog(AppInfo.VersionText,
                                                                    _result.Latest.ToDisplay(), _readySize))
            {
                install = owner != null
                    ? dialog.ShowDialog(owner) == DialogResult.OK
                    : dialog.ShowDialog() == DialogResult.OK;
            }
            if (!install)
            {
                _status.Text = "更新已下载，可立即安装";
                _status.ForeColor = Ui.Good;
                _download.Text = "立即安装";
                Config.Log("自更新：用户选择稍后安装 v" + _result.Latest.ToDisplay());
                return;
            }

            string installDir = Path.GetDirectoryName(Config.CurrentExe);
            string error = "";
            Cursor previous = Cursor;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (UpdateInstaller.Begin(_readyExe, installDir, AppInfo.VersionText, out error))
                {
                    Config.Log("自更新：已交给替换助手，主程序退出");
                    Application.Exit();
                    return;
                }
            }
            finally
            {
                Cursor = previous;
            }

            _downloadFailed = true;
            _downloadError = "无法开始更新：" + error;
            Config.Log("自更新：无法开始更新 → " + error);
            if (owner != null)
            {
                MessageBox.Show(owner, "无法开始更新：\n\n" + error +
                                       "\n\n可以先继续使用当前版本，或到 GitHub Releases 手动下载。",
                                "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ApplyLayout();
        }

        private static string DescribeProgress(DownloadProgress progress)
        {
            if (progress.Total > 0)
            {
                int percent = (int)Math.Floor(progress.Received * 100.0 / progress.Total);
                if (percent < 0)
                {
                    percent = 0;
                }
                if (percent > 100)
                {
                    percent = 100;
                }
                return "下载中 " + percent + "% · " + DownloadFormat.Size(progress.Received) +
                       " / " + DownloadFormat.Size(progress.Total);
            }
            return "下载中 · " + DownloadFormat.Size(progress.Received) + " / 大小未知";
        }

        private static string DescribeSpeed(DownloadProgress progress)
        {
            if (!progress.SpeedReady || progress.Speed <= 0)
            {
                return "计算中…";
            }
            if (progress.EtaSeconds >= 0)
            {
                return DownloadFormat.Speed(progress.Speed) + " · 剩余 " +
                       DownloadFormat.Eta(progress.EtaSeconds);
            }
            return DownloadFormat.Speed(progress.Speed);
        }

        private static double Fraction(long received, long total)
        {
            if (received <= 0 || total <= 0)
            {
                return 0;
            }
            double value = (double)received / total;
            return value > 1 ? 1 : value;
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
