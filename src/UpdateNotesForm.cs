using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>更新日志里的一行：文本 + 位置 + 字体/颜色；分组标题与警示框单独标记。</summary>
    internal class NoteLine
    {
        public Rectangle Rect;
        public string Text = "";
        public Font Font;
        public Color Color;
        public Color Fill = Color.Empty;
        public int Radius;
        public TextFormatFlags Flags;
    }

    /// <summary>可滚动的更新日志视图：按版本倒序分段，段内按类型分组，空分组不显示。</summary>
    internal class NotesView : Control
    {
        private readonly List<ReleaseNote> _notes = new List<ReleaseNote>();
        private readonly List<NoteLine> _lines = new List<NoteLine>();
        private readonly List<int> _rules = new List<int>();
        private string _header = "";
        private string _subHeader = "";
        private bool _sameVersion;
        private string _sameVersionText = "当前已是最新版本";
        private bool _downgrade;
        private string _downgradeText = "";
        private bool _truncatedOlder;
        private bool _rebuilding;

        public NotesView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Font = Ui.Body;
        }

        public void SetContent(string header, string subHeader, List<ReleaseNote> notes,
                               bool sameVersion, bool downgrade, string downgradeText, bool truncatedOlder)
        {
            _header = header ?? "";
            _subHeader = subHeader ?? "";
            _notes.Clear();
            if (notes != null)
            {
                _notes.AddRange(notes);
            }
            _sameVersion = sameVersion;
            _downgrade = downgrade;
            _downgradeText = downgradeText ?? "";
            _truncatedOlder = truncatedOlder;
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Panel panel = Parent as Panel;
            if (panel != null && panel.VerticalScroll != null)
            {
                int max = panel.VerticalScroll.Maximum - panel.ClientSize.Height;
                if (max < 0)
                {
                    max = 0;
                }
                int y = -panel.AutoScrollPosition.Y - e.Delta;
                if (y < 0)
                {
                    y = 0;
                }
                if (y > max)
                {
                    y = max;
                }
                panel.AutoScrollPosition = new Point(0, y);
                ((HandledMouseEventArgs)e).Handled = true;
                return;
            }
            base.OnMouseWheel(e);
        }

        private static int Measure(string text, Font font, int width, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            Size size = TextRenderer.MeasureText(text, font, new Size(Math.Max(40, width), int.MaxValue), flags);
            return size.Height;
        }

        private const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.WordBreak |
                                             TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        public void Rebuild()
        {
            _rebuilding = true;
            try
            {
                _lines.Clear();
                _rules.Clear();
                int width = Math.Max(Ui.S(240), Width - Ui.S(4));
                Height = BuildLayout(width);
            }
            finally
            {
                _rebuilding = false;
            }
            Invalidate();
        }

        private int BuildLayout(int width)
        {
            int x = Ui.S(8);
            int contentWidth = width - Ui.S(16);
            int y = Ui.S(6);

            // 当前版本 + 日期
            int headerHeight = Measure(_header, Ui.H1, contentWidth, Wrap);
            _lines.Add(Block(_header, Ui.H1, Ui.Text, x, y, contentWidth, headerHeight, Wrap));
            y += headerHeight + Ui.S(2);
            int subHeight = Measure(_subHeader, Ui.Small, contentWidth, Wrap);
            _lines.Add(Block(_subHeader, Ui.Small, Ui.TextFaint, x, y, contentWidth, subHeight, Wrap));
            y += subHeight + Ui.S(6);

            if (_sameVersion)
            {
                int h = Measure(_sameVersionText, Ui.SmallBold, contentWidth, Wrap);
                _lines.Add(Block(_sameVersionText, Ui.SmallBold, Ui.Good, x, y, contentWidth, h, Wrap));
                y += h + Ui.S(8);
            }

            if (_downgrade && _downgradeText.Length > 0)
            {
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.WordBreak |
                                        TextFormatFlags.NoPrefix;
                int h = Measure(_downgradeText, Ui.Small, contentWidth - Ui.S(20), flags);
                NoteLine box = new NoteLine();
                box.Rect = new Rectangle(x, y, contentWidth, h + Ui.S(16));
                box.Fill = Ui.WarnBg;
                box.Radius = Ui.S(6);
                box.Color = Ui.Warn;
                box.Font = Ui.Small;
                box.Text = "";
                _lines.Add(box);
                _lines.Add(Block(_downgradeText, Ui.Small, Ui.Warn,
                    x + Ui.S(10), y + Ui.S(8), contentWidth - Ui.S(20), h, flags));
                y += box.Rect.Height + Ui.S(10);
            }

            for (int i = 0; i < _notes.Count; i++)
            {
                ReleaseNote note = _notes[i];
                y += i == 0 ? 0 : Ui.S(14);

                int versionHeight = Measure("v" + note.VersionText, Ui.H1, contentWidth - Ui.S(120), Wrap);
                _lines.Add(Block("v" + note.VersionText, Ui.H1, Ui.Text, x, y, contentWidth - Ui.S(120), versionHeight, Wrap));
                if (note.Date.Length > 0)
                {
                    int dateHeight = Measure(note.Date, Ui.Small, contentWidth, TextFormatFlags.Right | TextFormatFlags.NoPrefix);
                    _lines.Add(Block(note.Date, Ui.Small, Ui.TextFaint,
                        x, y + Ui.S(4), contentWidth, dateHeight,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix));
                }
                y += Math.Max(versionHeight, Ui.S(18)) + Ui.S(2);
                _rules.Add(y);
                y += Ui.S(8);

                int groupCount = 0;
                y = Group("新增", note.Added, Ui.Good, x, y, contentWidth, ref groupCount);
                y = Group("修复", note.Fixed, Ui.Accent, x, y, contentWidth, ref groupCount);
                y = Group("改进", note.Improved, Ui.TextDim, x, y, contentWidth, ref groupCount);
                y = Group("移除", note.Removed, Ui.TextDim, x, y, contentWidth, ref groupCount);
                y = Group("数据变更", note.DataChanges, Ui.Warn, x, y, contentWidth, ref groupCount);
                if (groupCount == 0)
                {
                    string empty = "这个版本没有记录变更";
                    int h = Measure(empty, Ui.Small, contentWidth, Wrap);
                    _lines.Add(Block(empty, Ui.Small, Ui.TextFaint, x, y, contentWidth, h, Wrap));
                    y += h + Ui.S(4);
                }
            }

            if (_truncatedOlder)
            {
                string text = "更早版本的变更未收录";
                int h = Measure(text, Ui.Small, contentWidth, Wrap);
                _lines.Add(Block(text, Ui.Small, Ui.TextFaint, x, y + Ui.S(8), contentWidth, h, Wrap));
                y += h + Ui.S(12);
            }
            return y + Ui.S(12);
        }

        private static NoteLine Block(string text, Font font, Color color, int x, int y, int width, int height, TextFormatFlags flags)
        {
            NoteLine line = new NoteLine();
            line.Rect = new Rectangle(x, y, width, height);
            line.Text = text;
            line.Font = font;
            line.Color = color;
            line.Flags = flags;
            return line;
        }

        private int Group(string title, List<string> items, Color color, int x, int y, int width, ref int groupCount)
        {
            if (items == null || items.Count == 0)
            {
                return y;
            }
            groupCount++;
            int titleHeight = Measure(title, Ui.SmallBold, width, Wrap);
            _lines.Add(Block(title, Ui.SmallBold, color, x, y, width, titleHeight, Wrap));
            y += titleHeight + Ui.S(2);
            for (int i = 0; i < items.Count; i++)
            {
                string text = "· " + items[i];
                int height = Measure(text, Ui.Body, width - Ui.S(12), Wrap);
                _lines.Add(Block(text, Ui.Body, Ui.Text, x + Ui.S(12), y, width - Ui.S(12), height, Wrap));
                y += height + Ui.S(3);
            }
            return y + Ui.S(5);
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
                    g.DrawLine(pen, Ui.S(8), _rules[i], Width - Ui.S(12), _rules[i]);
                }
            }
        }
    }

    /// <summary>
    /// 「安装完成 / 更新完成 / 历史版本」窗口。内容和「历史版本」界面共用 ReleaseLog 一份数据。
    /// </summary>
    internal class UpdateNotesForm : ModalForm
    {
        private readonly List<ReleaseNote> _notes;
        private readonly bool _history;
        private readonly bool _sameVersion;
        private readonly bool _downgrade;
        private readonly string _downgradeText;
        private readonly bool _truncatedOlder;
        private NotesView _view;
        private Panel _scroll;
        private FlatButton _expand;
        private Label _hint;
        private int _expanded;

        private UpdateNotesForm(string heading, List<ReleaseNote> notes, string header, string subHeader,
                                bool sameVersion, bool downgrade, string downgradeText, bool truncatedOlder,
                                bool history)
            : base(heading, 884, 620)
        {
            _notes = notes == null ? new List<ReleaseNote>() : notes;
            _history = history;
            _sameVersion = sameVersion;
            _downgrade = downgrade;
            _downgradeText = downgradeText ?? "";
            _truncatedOlder = truncatedOlder;
            _viewHeader = header ?? "";
            _viewSub = subHeader ?? "";
            _expanded = Math.Min(3, _notes.Count);

            ShowInTaskbar = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _scroll = new BufferedPanel();
            _scroll.Left = Ui.S(22);
            _scroll.Top = Ui.S(10);
            _scroll.Width = Ui.S(840);
            _scroll.Height = Ui.S(468);
            _scroll.BackColor = Ui.Panel;
            _scroll.AutoScroll = true;
            Content.Controls.Add(_scroll);

            _view = new NotesView();
            _view.Left = 0;
            _view.Top = 0;
            _view.Width = _scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Ui.S(4);
            _scroll.Controls.Add(_view);
            _view.SetContent(_viewHeader, _viewSub, VisibleNotes(), sameVersion, downgrade, _downgradeText, truncatedOlder);

            _hint = MakeLabel("可滚动查看全部变更", 22, 492, 300, 20, Ui.Small, Ui.TextFaint);

            _expand = MakeButton("展开更早的版本", 22, 516, 190, false);
            _expand.Click += delegate
            {
                _expanded = _notes.Count;
                RefreshNotes();
            };

            if (_history)
            {
                FlatButton close = MakeButton("关闭", 758, 516, 104, true);
                close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            }
            else
            {
                FlatButton all = MakeButton("查看历史版本", 596, 516, 142, false);
                all.Click += delegate
                {
                    using (UpdateNotesForm form = History())
                    {
                        form.ShowDialog(this);
                    }
                };
                FlatButton start = MakeButton("开始使用", 748, 516, 114, true);
                start.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            }
            UpdateExpandButton();
        }

        private List<ReleaseNote> VisibleNotes()
        {
            List<ReleaseNote> list = new List<ReleaseNote>();
            int count = Math.Min(_expanded, _notes.Count);
            for (int i = 0; i < count; i++)
            {
                list.Add(_notes[i]);
            }
            return list;
        }

        private void RefreshNotes()
        {
            _view.SetContent(_viewHeader, _viewSub, VisibleNotes(), _sameVersion, _downgrade,
                             _downgradeText, _truncatedOlder);
            _scroll.AutoScrollPosition = new Point(0, 0);
            UpdateExpandButton();
        }

        private string _viewHeader = "";
        private string _viewSub = "";

        private void UpdateExpandButton()
        {
            int remaining = _notes.Count - _expanded;
            if (remaining <= 0)
            {
                _expand.Visible = false;
                _hint.Left = Ui.S(22);
            }
            else
            {
                _expand.Visible = true;
                _expand.Text = "展开更早的 " + remaining + " 个版本";
                _expand.Width = Ui.S(190);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _view.Width = _scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Ui.S(4);
            RefreshNotes();
        }

        private static string TitleFor(bool fresh, SemVer from, SemVer to, bool fromKnown)
        {
            string target = "v" + (to == null ? AppInfo.VersionText : to.ToDisplay());
            if (fresh || !fromKnown)
            {
                return (fresh ? "安装完成 · " : "更新完成 · ") + target;
            }
            if (from.IsSame(to))
            {
                return "更新完成 · " + target;
            }
            return "更新完成 · v" + from.ToDisplay() + " → " + target;
        }

        /// <summary>启动时调用：有 update-pending.ini 就返回一个待展示窗口，并标记已读、删除待展示文件。</summary>
        public static UpdateNotesForm CreatePending()
        {
            if (!PendingNotes.Exists)
            {
                return null;
            }
            string fromText = PendingNotes.Read("From");
            string toText = PendingNotes.Read("To");
            bool fresh = PendingNotes.Read("Fresh") == "1";
            SemVer to;
            if (!SemVer.TryParse(toText, out to) || to == null)
            {
                to = AppInfo.AppVersion;
            }
            SemVer from;
            bool fromKnown = SemVer.TryParse(fromText, out from) && from != null && !from.IsUnknown;
            bool same = fromKnown && from.IsSame(to);
            bool downgrade = fromKnown && from.CompareTo(to) > 0;
            bool truncated;
            List<ReleaseNote> notes = ReleaseLog.Range(fromKnown ? from : null, to, out truncated);
            string title = TitleFor(fresh, from, to, fromKnown);
            string header = "当前版本 v" + to.ToDisplay();
            ReleaseNote current = ReleaseLog.Find(to);
            if (current != null && current.Date.Length > 0)
            {
                header += "  ·  " + current.Date;
            }
            string sub = "数据版本 " + AppInfo.DataVersionText + "（schema " + DataFiles.SchemaVersion + "）";
            string downgradeText = downgrade
                ? "这是降级安装：从 v" + from.ToDisplay() + " 降到 v" + to.ToDisplay() + "。新版本的功能与修复不会出现在旧版本里。"
                : "";
            UpdateNotesForm form = new UpdateNotesForm(title, notes, header, sub, same, downgrade,
                                                      downgradeText, truncated, false);
            form._viewHeader = header;
            form._viewSub = sub;
            NotesSeen.MarkSeen(to);
            PendingNotes.Clear();
            return form;
        }

        /// <summary>「历史版本」界面：展示全部已收录版本，默认展开最近 3 段。</summary>
        public static UpdateNotesForm History()
        {
            List<ReleaseNote> notes = ReleaseLog.All;
            SemVer current = AppInfo.AppVersion;
            string header = "当前版本 v" + current.ToDisplay();
            ReleaseNote latest = ReleaseLog.Latest;
            if (latest != null && latest.Date.Length > 0)
            {
                header += "  ·  " + latest.Date;
            }
            string sub = "数据版本 " + AppInfo.DataVersionText + "（schema " + DataFiles.SchemaVersion + "）";
            UpdateNotesForm form = new UpdateNotesForm("历史版本", notes, header, sub, false, false, "", false, true);
            return form;
        }

        public static void ShowPendingIfAny()
        {
            using (UpdateNotesForm form = CreatePending())
            {
                if (form != null)
                {
                    form.ShowDialog();
                }
            }
        }
    }
}
