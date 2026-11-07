using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>无边框对话框基类：自带标题栏、拖动、关闭按钮、淡入。</summary>
    internal class ModalForm : Form
    {
        private bool _dragging;
        private Point _dragOffset;
        protected string Heading = "";
        protected int ContentTop = 58;
        protected Panel Content;
        protected bool ShowGear;
        protected bool ChromeHidden;

        public ModalForm(string heading, int width, int height)
        {
            Heading = heading;
            Text = heading;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.S(width), Ui.S(height));
            BackColor = Ui.Panel;
            KeyPreview = true;
            DoubleBuffered = true;
            ShowInTaskbar = false;
            Font = Ui.Body;

            ContentTop = Ui.S(58);
            Content = new BufferedPanel();
            Content.Left = Ui.S(1);
            Content.Top = ContentTop;
            Content.Width = ClientSize.Width - Ui.S(2);
            Content.Height = ClientSize.Height - ContentTop - Ui.S(1);
            Content.BackColor = Ui.Panel;
            Content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(Content);

            Shown += delegate
            {
                CenterOnScreenIfNeeded();
                KeepOnScreen();
                Native.ThemeScrollbars(this);
                Ui.SlideIn(this);
            };
        }

        private void CenterOnScreenIfNeeded()
        {
            if (Owner != null)
            {
                return;
            }
            Rectangle area = Screen.FromControl(this).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2,
                                 area.Top + (area.Height - Height) / 2);
        }

        private void KeepOnScreen()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int x = Left;
            int y = Top;
            if (Right > area.Right)
            {
                x = area.Right - Width - Ui.S(8);
            }
            if (Bottom > area.Bottom)
            {
                y = area.Bottom - Height - Ui.S(8);
            }
            if (x < area.Left)
            {
                x = area.Left + Ui.S(8);
            }
            if (y < area.Top)
            {
                y = area.Top + Ui.S(8);
            }
            Location = new Point(x, y);
        }

        protected void ResizeToDesign(int width, int height)
        {
            ClientSize = new Size(Ui.S(width), Ui.S(height));
            Content.Width = ClientSize.Width - Ui.S(2);
            Content.Height = ClientSize.Height - ContentTop - Ui.S(1);
        }

        /// <summary>嵌入主窗口作为分页时调用：隐藏自带标题栏/关闭按钮，内容铺满。</summary>
        protected void SetEmbedded()
        {
            ChromeHidden = true;
            Heading = "";
            ContentTop = 0;
            Content.SetBounds(Ui.S(1), Ui.S(1), ClientSize.Width - Ui.S(2), ClientSize.Height - Ui.S(2));
            Content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Panel))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (Pen pen = new Pen(Ui.Line, 1f))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
            if (ChromeHidden)
            {
                return;
            }
            int headingX = Ui.S(22);
            if (ShowGear)
            {
                Rectangle gearRect = new Rectangle(Ui.S(22), (ContentTop - Ui.S(22)) / 2, Ui.S(22), Ui.S(22));
                Ui.FillRound(g, gearRect, Ui.S(6), Ui.PanelActive);
                using (Pen pen = new Pen(Ui.Accent, Math.Max(1.4f, Ui.Scale * 1.6f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    int y1 = gearRect.Top + gearRect.Height * 3 / 10;
                    int y2 = gearRect.Top + gearRect.Height / 2;
                    int y3 = gearRect.Top + gearRect.Height * 7 / 10;
                    int left = gearRect.Left + Ui.S(5);
                    int right = gearRect.Right - Ui.S(5);
                    g.DrawLine(pen, left, y1, right, y1);
                    g.DrawLine(pen, left, y2, right, y2);
                    g.DrawLine(pen, left, y3, right, y3);
                    using (SolidBrush dot = new SolidBrush(Ui.Text))
                    {
                        int d = Ui.S(4);
                        g.FillEllipse(dot, left + (right - left) / 3 - d / 2, y1 - d / 2, d, d);
                        g.FillEllipse(dot, right - (right - left) / 3 - d / 2, y2 - d / 2, d, d);
                        g.FillEllipse(dot, left + (right - left) / 2 - d / 2, y3 - d / 2, d, d);
                    }
                }
                headingX = gearRect.Right + Ui.S(10);
            }
            Ui.DrawText(g, Heading, Ui.H1, Ui.Text,
                    new Rectangle(headingX, 0, Width - headingX - Ui.S(50), ContentTop),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            Rectangle close = CloseRect();
            bool hot = close.Contains(PointToClient(Cursor.Position));
            if (hot)
            {
                Ui.FillRound(g, close, Ui.S(6), Ui.T.CloseHoverBg);
            }
            using (Pen pen = new Pen(hot ? Ui.T.CloseHoverFg : Ui.TextDim, 1.6f))
            {
                int pad = Ui.S(9);
                g.DrawLine(pen, close.Left + pad, close.Top + pad, close.Right - pad, close.Bottom - pad);
                g.DrawLine(pen, close.Right - pad, close.Top + pad, close.Left + pad, close.Bottom - pad);
            }
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, Ui.S(22), ContentTop - 1, Width - Ui.S(22), ContentTop - 1);
            }
        }

        private Rectangle CloseRect()
        {
            int size = Ui.S(30);
            return new Rectangle(Width - size - Ui.S(14), (ContentTop - size) / 2, size, size);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (ChromeHidden)
            {
                base.OnMouseDown(e);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (CloseRect().Contains(e.Location))
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }
                if (e.Y < ContentTop)
                {
                    _dragging = true;
                    _dragOffset = e.Location;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (ChromeHidden)
            {
                base.OnMouseMove(e);
                return;
            }
            if (_dragging)
            {
                Point screen = PointToScreen(e.Location);
                Location = new Point(screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!ChromeHidden && e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            base.OnKeyDown(e);
        }

        protected FlatButton MakeButton(string text, int x, int y, int width, bool primary)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Left = Ui.S(x);
            button.Top = Ui.S(y);
            button.Width = Ui.S(width);
            button.Height = Ui.S(38);
            button.Primary = primary;
            Content.Controls.Add(button);
            return button;
        }

        protected Label MakeLabel(string text, int x, int y, int width, int height, Font font, Color color)
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
            Content.Controls.Add(label);
            return label;
        }

        protected Panel MakeField(int x, int y, int width, int height, out TextBox box)
        {
            Panel panel = new Panel();
            panel.Left = Ui.S(x);
            panel.Top = Ui.S(y);
            panel.Width = Ui.S(width);
            panel.Height = Ui.S(height);
            panel.BackColor = Ui.PanelActive;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Ui.DrawRound(e.Graphics, new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), Ui.S(7), Ui.Line, 1f);
            };
            Content.Controls.Add(panel);

            TextBox tb = new TextBox();
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Ui.PanelActive;
            tb.ForeColor = Ui.Text;
            tb.Font = Ui.Body;
            tb.Left = Ui.S(10);
            tb.Top = (panel.Height - tb.PreferredHeight) / 2;
            tb.Width = panel.Width - Ui.S(20);
            tb.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panel.Controls.Add(tb);
            box = tb;
            return panel;
        }
    }

    /// <summary>主题色块。</summary>
    internal class ThemeChip : Control
    {
        public int Index;
        public bool Selected;
        private bool _hover;

        public ThemeChip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Palette p = Palettes.All[Index];
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Ui.S(9), p.Bg);
            Ui.DrawRound(g, r, Ui.S(9), Selected ? Ui.Accent : (_hover ? Ui.Line : Ui.LineSoft), Selected ? 2f : 1f);

            int dot = Ui.S(12);
            int x = Ui.S(12);
            int y = Ui.S(12);
            Color[] dots = new Color[] { p.Accent, p.PanelActive, p.Text };
            for (int i = 0; i < dots.Length; i++)
            {
                using (SolidBrush brush = new SolidBrush(dots[i]))
                {
                    g.FillEllipse(brush, x + i * (dot + Ui.S(5)), y, dot, dot);
                }
            }
            Ui.DrawText(g, p.Name, Ui.SmallBold, p.Text,
                    new Rectangle(Ui.S(12), Ui.S(34), Width - Ui.S(24), Ui.S(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (Selected)
            {
                Ui.DrawText(g, "✓", Ui.SmallBold, p.Accent,
                        new Rectangle(Width - Ui.S(26), Ui.S(8), Ui.S(16), Ui.S(16)),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
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
    }

    /// <summary>设置页左侧的分页按钮。</summary>
    internal class SideTab : Control
    {
        public bool Selected;
        private bool _hover;

        public SideTab()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = Ui.S(42);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Selected)
            {
                Ui.FillRound(g, r, Ui.S(8), Ui.PanelActive);
                Ui.FillRound(g, new Rectangle(r.X, r.Y + Ui.S(8), Ui.S(3), r.Height - Ui.S(16)), Ui.S(2), Ui.Accent);
            }
            else if (_hover)
            {
                Ui.FillRound(g, r, Ui.S(8), Ui.PanelHover);
            }
            Ui.DrawText(g, Text, Ui.BodyBold, Selected ? Ui.Text : Ui.TextDim,
                    new Rectangle(Ui.S(16), 0, Width - Ui.S(20), Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
    }

    internal class SettingsForm : ModalForm
    {
        private TextBox _dirBox;
        private CheckRow _animRow;
        private CheckRow _backupRow;
        private Label _zoomLabel;
        private Label _keepLabel;
        private readonly List<ThemeChip> _chips = new List<ThemeChip>();
        private Panel _pageUi;
        private Panel _pagePrefs;
        private Panel _pageCert;
        private SideTab _tabUi;
        private SideTab _tabPrefs;
        private SideTab _tabCert;
        private Label _certState;
        private Label _certDetail;
        private FlatButton _certAdd;
        private FlatButton _certRemove;
        private int _theme;
        private int _zoom;
        private int _keepValue;

        public bool NeedRestart;

        public SettingsForm()
            : base("设置", 840, 640)
        {
            ShowGear = true;
            _theme = Config.ThemeIndex;
            _zoom = Config.ZoomPercent;
            _keepValue = Config.KeepBackups;

            int pageWidth = 608;
            int pageHeight = 512;

            _pageUi = new BufferedPanel();
            _pageUi.BackColor = Ui.Panel;
            _pageUi.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            Content.Controls.Add(_pageUi);

            _pagePrefs = new BufferedPanel();
            _pagePrefs.BackColor = Ui.Panel;
            _pagePrefs.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            _pagePrefs.Visible = false;
            Content.Controls.Add(_pagePrefs);

            _pageCert = new BufferedPanel();
            _pageCert.BackColor = Ui.Panel;
            _pageCert.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            _pageCert.Visible = false;
            Content.Controls.Add(_pageCert);

            _tabUi = new SideTab();
            _tabUi.Text = "界面设置";
            _tabUi.Selected = true;
            _tabUi.SetBounds(Ui.S(16), Ui.S(16), Ui.S(168), Ui.S(42));
            _tabUi.Click += delegate { SelectPage(0); };
            Content.Controls.Add(_tabUi);

            _tabPrefs = new SideTab();
            _tabPrefs.Text = "使用偏好";
            _tabPrefs.SetBounds(Ui.S(16), Ui.S(64), Ui.S(168), Ui.S(42));
            _tabPrefs.Click += delegate { SelectPage(1); };
            Content.Controls.Add(_tabPrefs);

            _tabCert = new SideTab();
            _tabCert.Text = "发布者签名";
            _tabCert.SetBounds(Ui.S(16), Ui.S(112), Ui.S(168), Ui.S(42));
            _tabCert.Click += delegate { SelectPage(2); };
            Content.Controls.Add(_tabCert);

            BuildUiPage();
            BuildPrefsPage();
            BuildCertPage();

            FlatButton cancel = MakeButton("取消", 610, 536, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton save = MakeButton("保存", 718, 536, 98, true);
            save.Click += delegate { SaveSettings(); };
        }

        private void SelectPage(int page)
        {
            _pageUi.Visible = page == 0;
            _pagePrefs.Visible = page == 1;
            _pageCert.Visible = page == 2;
            _tabUi.Selected = page == 0;
            _tabPrefs.Selected = page == 1;
            _tabCert.Selected = page == 2;
            _tabUi.Invalidate();
            _tabPrefs.Invalidate();
            _tabCert.Invalidate();
            if (page == 2)
            {
                RefreshCert();
            }
        }

        private Label PageLabel(Control page, string text, int x, int y, int w, int h, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Left = Ui.S(x);
            label.Top = Ui.S(y);
            label.Width = Ui.S(w);
            label.Height = Ui.S(h);
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Ui.Panel;
            label.AutoSize = false;
            page.Controls.Add(label);
            return label;
        }

        private FlatButton PageButton(Control page, string text, int x, int y, int w, bool primary)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Left = Ui.S(x);
            button.Top = Ui.S(y);
            button.Width = Ui.S(w);
            button.Height = Ui.S(38);
            button.Primary = primary;
            button.BackColor = Ui.Panel;
            page.Controls.Add(button);
            return button;
        }

        private TextBox PageField(Control page, int x, int y, int w, int h)
        {
            Panel panel = new Panel();
            panel.Left = Ui.S(x);
            panel.Top = Ui.S(y);
            panel.Width = Ui.S(w);
            panel.Height = Ui.S(h);
            panel.BackColor = Ui.PanelActive;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Ui.DrawRound(e.Graphics, new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), Ui.S(7), Ui.Line, 1f);
            };
            page.Controls.Add(panel);

            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Ui.PanelActive;
            box.ForeColor = Ui.Text;
            box.Font = Ui.Body;
            box.Left = Ui.S(10);
            box.Top = (panel.Height - box.PreferredHeight) / 2;
            box.Width = panel.Width - Ui.S(20);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panel.Controls.Add(box);
            return box;
        }

        private void BuildUiPage()
        {
            PageLabel(_pageUi, "主题", 8, 6, 120, 22, Ui.SmallBold, Ui.TextDim);
            for (int i = 0; i < Palettes.All.Length; i++)
            {
                ThemeChip chip = new ThemeChip();
                chip.Index = i;
                chip.Selected = i == _theme;
                chip.Left = Ui.S(8 + i * 150);
                chip.Top = Ui.S(32);
                chip.Width = Ui.S(140);
                chip.Height = Ui.S(62);
                chip.BackColor = Ui.Panel;
                chip.Click += delegate(object sender, EventArgs args)
                {
                    ThemeChip clicked = (ThemeChip)sender;
                    _theme = clicked.Index;
                    for (int c = 0; c < _chips.Count; c++)
                    {
                        _chips[c].Selected = _chips[c].Index == _theme;
                        _chips[c].Invalidate();
                    }
                };
                _chips.Add(chip);
                _pageUi.Controls.Add(chip);
            }

            PageLabel(_pageUi, "缩放", 8, 118, 60, 20, Ui.SmallBold, Ui.TextDim);
            FlatButton zoomOut = PageButton(_pageUi, "－", 62, 112, 38, false);
            zoomOut.Click += delegate { ChangeZoom(-5); };
            _zoomLabel = PageLabel(_pageUi, _zoom + "%", 106, 112, 70, 38, Ui.BodyBold, Ui.Text);
            _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            FlatButton zoomIn = PageButton(_pageUi, "＋", 182, 112, 38, false);
            zoomIn.Click += delegate { ChangeZoom(5); };
            PageLabel(_pageUi, "75% – 150%，修改后需要重启程序生效。", 232, 120, 360, 20, Ui.Small, Ui.TextFaint);

            _animRow = new CheckRow();
            _animRow.Title = "界面动画";
            _animRow.Subtitle = "对话框滑入、卡片悬停渐变等";
            _animRow.Checked = false;
            _animRow.Visible = false;
            _animRow.Left = Ui.S(4);
            _animRow.Top = Ui.S(166);
            _animRow.Width = Ui.S(600);
            _animRow.Height = Ui.S(48);
            _animRow.BackColor = Ui.Panel;
            _pageUi.Controls.Add(_animRow);

            PageLabel(_pageUi, "当前的界面缩放会按显示器 DPI 自动适配，高分屏觉得字发虚时可以在这里调大。",
                      8, 226, 590, 40, Ui.Small, Ui.TextFaint);
            PageLabel(_pageUi, "主题和缩放修改后需要重启程序才会完全生效。",
                      8, 266, 590, 20, Ui.Small, Ui.Accent);
        }

        private void BuildPrefsPage()
        {
            PageLabel(_pagePrefs, "游戏目录", 8, 6, 200, 22, Ui.SmallBold, Ui.TextDim);
            _dirBox = PageField(_pagePrefs, 8, 30, 592, 40);
            _dirBox.Text = Config.GameDir;

            PageButton(_pagePrefs, "自动检测", 500, 78, 90, false).Click += delegate
            {
                string best = GameLocator.FindBest();
                if (string.IsNullOrEmpty(best))
                {
                    MessageBox.Show(this, "没有找到战雷安装目录，请手动选择。", "自动检测",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _dirBox.Text = best;
            };
            PageButton(_pagePrefs, "浏览…", 402, 78, 90, false).Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择 War Thunder 安装目录";
                    dialog.SelectedPath = _dirBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _dirBox.Text = dialog.SelectedPath;
                    }
                }
            };

            PageLabel(_pagePrefs, "导入", 8, 136, 100, 22, Ui.SmallBold, Ui.TextDim);
            _backupRow = new CheckRow();
            _backupRow.Title = "导入前备份旧文件夹";
            _backupRow.Subtitle = "旧内容会复制到安装目录的 backups 里，不会写进战雷目录";
            _backupRow.Checked = Config.Backup;
            _backupRow.Left = Ui.S(4);
            _backupRow.Top = Ui.S(160);
            _backupRow.Width = Ui.S(600);
            _backupRow.Height = Ui.S(48);
            _backupRow.BackColor = Ui.Panel;
            _pagePrefs.Controls.Add(_backupRow);

            PageLabel(_pagePrefs, "保留最近几份", 8, 220, 110, 20, Ui.Small, Ui.TextFaint);
            FlatButton keepMinus = PageButton(_pagePrefs, "－", 116, 214, 34, false);
            keepMinus.Click += delegate { ChangeKeep(-1); };
            _keepLabel = PageLabel(_pagePrefs, _keepValue.ToString(), 156, 214, 54, 38, Ui.BodyBold, Ui.Text);
            _keepLabel.TextAlign = ContentAlignment.MiddleCenter;
            FlatButton keepPlus = PageButton(_pagePrefs, "＋", 216, 214, 34, false);
            keepPlus.Click += delegate { ChangeKeep(1); };
            PageLabel(_pagePrefs, "份（1–50），旧备份会被自动清理", 264, 222, 300, 20, Ui.Small, Ui.TextFaint);

            PageLabel(_pagePrefs, "安装目录", 8, 276, 120, 20, Ui.SmallBold, Ui.TextDim);
            PageLabel(_pagePrefs, Config.InstallDir, 8, 298, 420, 20, Ui.Small, Ui.TextDim);
            PageButton(_pagePrefs, "打开", 440, 288, 70, false).Click += delegate { OpenFolder(Config.InstallDir); };
            PageButton(_pagePrefs, "备份", 518, 288, 70, false).Click += delegate { OpenFolder(Config.BackupDir); };
            PageLabel(_pagePrefs, "备份、日志、缓存都在安装目录里，不会写进战雷目录。",
                      8, 330, 590, 20, Ui.Small, Ui.TextFaint);
            PageLabel(_pagePrefs, "程序 v" + AppInfo.VersionText + " · 数据 " + AppInfo.DataVersionText,
                      8, 346, 260, 18, Ui.Small, Ui.TextFaint);

            PageButton(_pagePrefs, "查看导入日志", 8, 366, 130, false).Click += delegate
            {
                OpenFolder(Config.LogDir);
            };
            PageButton(_pagePrefs, "打开安装目录", 148, 366, 130, false).Click += delegate
            {
                OpenFolder(Config.InstallDir);
            };
            // 与「安装/更新完成」窗口共用同一份更新日志数据（ReleaseLog），不另写文案。
            PageButton(_pagePrefs, "历史版本", 288, 366, 110, false).Click += delegate
            {
                using (UpdateNotesForm form = UpdateNotesForm.History())
                {
                    form.ShowDialog(this);
                }
            };
        }

        /// <summary>发布者签名页：显示当前状态，提供「补装」与「移除」（移除前二次确认）。</summary>
        private void BuildCertPage()
        {
            PageLabel(_pageCert, "发布者签名", 8, 6, 200, 22, Ui.SmallBold, Ui.TextDim);
            _certState = PageLabel(_pageCert, "", 8, 30, 580, 26, Ui.BodyBold, Ui.TextFaint);

            PageLabel(_pageCert, "签名用来告诉 Windows 这个软件是谁发布的，减少「未知发布者」的安全提示。",
                      8, 62, 590, 20, Ui.Small, Ui.TextDim);
            PageLabel(_pageCert, "签名只装到当前用户的受信任发布者列表，不需要管理员权限；移除后程序照常运行。",
                      8, 84, 590, 20, Ui.Small, Ui.TextDim);

            _certAdd = PageButton(_pageCert, "补装发布者签名", 8, 118, 150, true);
            _certAdd.Click += delegate { AddCert(); };
            _certRemove = PageButton(_pageCert, "移除发布者签名", 172, 118, 150, false);
            _certRemove.Danger = true;
            _certRemove.Click += delegate { RemoveCert(); };

            _certDetail = PageLabel(_pageCert, "", 8, 176, 590, 110, Ui.Small, Ui.TextFaint);
            PageLabel(_pageCert, "证书随程序内嵌分发（publisher.cer，只含公钥），导入前会校验 SHA-256。",
                      8, 300, 590, 20, Ui.Small, Ui.TextFaint);
            PageLabel(_pageCert, "移除后 Windows 可能重新显示「未知发布者」，不影响导入涂装等功能；之后可随时补装。",
                      8, 322, 590, 20, Ui.Small, Ui.TextFaint);
            RefreshCert();
        }

        private void RefreshCert()
        {
            if (_certState == null)
            {
                return;
            }
            X509Certificate2 found;
            bool installed = PublisherCert.TryFindInstalled(out found);
            _certState.Text = installed ? "已安装 · " + PublisherCert.PublisherName : "未安装";
            _certState.ForeColor = installed ? Ui.Good : Ui.Warn;

            byte[] bytes;
            string source;
            CertResult load = PublisherCert.LoadPackage(out bytes, out source);
            string text = "主体：" + PublisherCert.ExpectedSubject;
            text += "\n指纹：" + PublisherCert.ExpectedThumbprint;
            text += "\nSHA-256：" + PublisherCert.ExpectedSha256;
            text += "\n证书来源：" + (load.Ok ? source : "不可用（" + load.Message + "）");
            if (installed && found != null)
            {
                try
                {
                    text += "\n有效期至：" + found.NotAfter.ToString("yyyy-MM-dd");
                }
                catch
                {
                }
            }
            _certDetail.Text = text;
            _certAdd.Enabled = !installed;
            _certRemove.Enabled = installed;
        }

        private void AddCert()
        {
            InstallLog.Ensure(Config.InstallDir);
            InstallLog.Write("设置页：点击「补装发布者签名」；指纹 " + PublisherCert.ExpectedThumbprint);
            if (PublisherCert.IsInstalled())
            {
                InstallLog.Write("发布者签名：已存在，无需补装");
                MessageBox.Show(this, "发布者签名已经安装过了。", "发布者签名",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshCert();
                return;
            }

            bool installed;
            CertResult result;
            using (PublisherCertForm form = new PublisherCertForm())
            {
                form.ShowDialog(this);
                installed = form.Installed;
                result = form.Result;
            }
            if (installed && result != null)
            {
                InstallLog.Write("发布者签名：设置页补装成功 → " + result.Detail);
                MessageBox.Show(this, result.Message, "发布者签名", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result != null && !result.Ok)
            {
                InstallLog.Write("发布者签名：设置页补装失败 → " + result.Detail);
                MessageBox.Show(this, result.Message, "发布者签名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                InstallLog.Write("发布者签名：设置页补装被取消，证书未导入；指纹 " + PublisherCert.ExpectedThumbprint);
            }
            RefreshCert();
        }

        private void RemoveCert()
        {
            InstallLog.Ensure(Config.InstallDir);
            if (!PublisherCert.IsInstalled())
            {
                InstallLog.Write("发布者签名：设置页移除时发现未安装，无需移除");
                MessageBox.Show(this, "当前用户的受信任发布者里没有这个签名。", "发布者签名",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshCert();
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "要从当前用户的受信任发布者里移除「" + PublisherCert.PublisherName + "」签名吗？\n\n" +
                "移除后 Windows 可能重新显示「未知发布者」的安全提示；程序本身的功能不受影响，之后也可以随时补装。",
                "移除发布者签名 · 请确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                InstallLog.Write("发布者签名：设置页移除被取消；指纹 " + PublisherCert.ExpectedThumbprint);
                return;
            }
            CertResult result = PublisherCert.Remove();
            InstallLog.Write("发布者签名：设置页移除" + (result.Ok ? "成功" : "失败") + " → " + result.Detail);
            MessageBox.Show(this, result.Message, "发布者签名",
                            MessageBoxButtons.OK, result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            RefreshCert();
        }

        private void ChangeKeep(int delta)
        {
            _keepValue += delta;
            if (_keepValue < 1)
            {
                _keepValue = 1;
            }
            if (_keepValue > 50)
            {
                _keepValue = 50;
            }
            _keepLabel.Text = _keepValue.ToString();
        }

        private void ChangeZoom(int delta)
        {
            _zoom += delta;
            if (_zoom < 75)
            {
                _zoom = 75;
            }
            if (_zoom > 150)
            {
                _zoom = 150;
            }
            _zoomLabel.Text = _zoom + "%";
        }

        private void SaveSettings()
        {
            string dir = _dirBox.Text.Trim();
            if (!Config.GameDirLooksValid(dir))
            {
                MessageBox.Show(this, "这个目录不是有效的战雷安装目录。", "无法保存",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool zoomChanged = _zoom != Config.ZoomPercent;
            bool themeChanged = _theme != Config.ThemeIndex;

            Config.GameDir = dir;
            Config.Backup = _backupRow.Checked;
            Config.KeepBackups = _keepValue;
            Config.ThemeIndex = _theme;
            Config.ZoomPercent = _zoom;
            Config.Animations = false;
            Config.Save();
            Ui.Animations = false;

            if (zoomChanged || themeChanged)
            {
                DialogResult answer = MessageBox.Show(this,
                    "界面设置已保存，主题和缩放需要重启程序才能完全生效。\n\n现在重启吗？",
                    "需要重启", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    NeedRestart = true;
                }
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
                }
            }
            catch
            {
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _dirBox.Select(0, 0);
        }
    }

    /// <summary>安装目录写不进去时的三选一：换默认目录 / 管理员重试 / 取消。</summary>
    internal class InstallFailForm : ModalForm
    {
        public int Choice; // 1=默认目录 2=管理员重试 0=取消

        public InstallFailForm(string path, string reason)
            : base("目录不可写", 660, 400)
        {
            MakeLabel("这个目录写不进去", 24, 14, 300, 24, Ui.H1, Ui.Warn);
            MakeLabel(path, 24, 46, 610, 22, Ui.Mono, Ui.Text);

            Label reasonLabel = MakeLabel("原因：" + reason, 24, 76, 610, 40, Ui.Small, Ui.TextDim);
            reasonLabel.AutoEllipsis = true;

            MakeLabel("可能的原因：", 24, 126, 300, 20, Ui.SmallBold, Ui.TextDim);
            MakeLabel("· 桌面 / 文档等文件夹被安全软件（火绒、360 等）保护，未知程序不能写入\n" +
                      "· 该文件夹的 Windows 权限不允许当前用户修改",
                      24, 148, 610, 44, Ui.Small, Ui.TextFaint);

            MakeLabel("建议改用默认目录，或者自己在 D 盘里新建一个普通文件夹。",
                      24, 200, 610, 20, Ui.Small, Ui.Accent);

            FlatButton cancel = MakeButton("取消", 388, 288, 84, false);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton admin = MakeButton("以管理员身份重试", 480, 288, 154, false);
            admin.Click += delegate { Choice = 2; DialogResult = DialogResult.OK; Close(); };
            FlatButton useDefault = MakeButton("改用默认目录", 24, 288, 150, true);
            useDefault.Click += delegate { Choice = 1; DialogResult = DialogResult.OK; Close(); };
        }
    }

    internal class VehiclePickerForm : ModalForm
    {
        private readonly TextBox _search;
        private readonly ListBox _list;
        private readonly TextBox _manual;
        private List<Vehicle> _results = new List<Vehicle>();

        public Vehicle SelectedVehicle;
        public string ManualUnitId = "";

        public VehiclePickerForm(string suggestedId)
            : base("选择载具", 660, 640)
        {
            MakeLabel("这台涂装属于哪个载具？", 24, 12, 400, 24, Ui.H1, Ui.Text);
            MakeLabel("搜索中文名、英文名或载具 ID，也可以直接在下面输入 ID。", 24, 38, 600, 22, Ui.Small, Ui.TextDim);

            TextBox searchField;
            MakeField(24, 68, 610, 38, out searchField);
            _search = searchField;
            Native.SetCue(_search, "搜索载具，例如：直-10ME / Z-10ME / z_10me");

            _list = new ListBox();
            _list.Left = Ui.S(24);
            _list.Top = Ui.S(116);
            _list.Width = Ui.S(610);
            _list.Height = Ui.S(392);
            _list.BackColor = Ui.BgSoft;
            _list.ForeColor = Ui.Text;
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.S(46);
            _list.Font = Ui.Body;
            _list.IntegralHeight = false;
            _list.DrawItem += DrawVehicleItem;
            _list.DoubleClick += delegate { Accept(); };
            Content.Controls.Add(_list);

            MakeLabel("手动输入载具 ID（列表里找不到时用）", 24, 518, 300, 20, Ui.SmallBold, Ui.TextDim);
            TextBox manualField;
            MakeField(24, 540, 300, 36, out manualField);
            _manual = manualField;
            MakeLabel("ID 就是 .blk 文件的名字，例如 z_10me", 336, 548, 300, 20, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton("取消", 402, 540, 92, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("使用所选载具", 502, 540, 132, true);
            ok.Click += delegate { Accept(); };

            _search.TextChanged += delegate { RefreshList(_search.Text); };
            RefreshList("");
            Shown += delegate { Native.ThemeScrollbars(this); };
            Native.SetCue(_manual, string.IsNullOrEmpty(suggestedId) ? "例如 z_10me" : "例如 " + suggestedId);
        }

        private void Accept()
        {
            string manual = _manual.Text.Trim();
            if (manual.Length > 0)
            {
                SelectedVehicle = VehicleDb.Find(manual);
                ManualUnitId = manual.ToLowerInvariant();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            Vehicle selected = _list.SelectedItem as Vehicle;
            if (selected == null)
            {
                MessageBox.Show(this, "请先选择一个载具，或者在下面输入载具 ID。", "还没有选择",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SelectedVehicle = selected;
            ManualUnitId = selected.Id;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RefreshList(string query)
        {
            _results = VehicleDb.Search(query, 400);
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _results.Count; i++)
            {
                _list.Items.Add(_results[i]);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }

        private void DrawVehicleItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count)
            {
                return;
            }
            Vehicle v = (Vehicle)_list.Items[e.Index];
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle r = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            if (selected)
            {
                Ui.FillRound(g, r, Ui.S(7), Ui.PanelActive);
                Ui.FillRound(g, new Rectangle(r.X, r.Y + Ui.S(8), Ui.S(3), r.Height - Ui.S(16)), Ui.S(2), Ui.Accent);
            }
            Rectangle titleRect = new Rectangle(r.X + Ui.S(12), r.Y + Ui.S(5), r.Width - Ui.S(20), Ui.S(20));
            Ui.DrawText(g, v.Zh, Ui.BodyBold, Ui.Text, titleRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Rectangle subRect = new Rectangle(r.X + Ui.S(12), r.Y + Ui.S(24), r.Width - Ui.S(20), Ui.S(17));
            Ui.DrawText(g, v.En + "   ·   " + v.Id, Ui.Small, Ui.TextFaint, subRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>提示用户还没在游戏里创建载具文件夹，并提供“重新检查”。</summary>
    internal class MissingFoldersForm : ModalForm
    {
        private readonly List<DetectedUnit> _missing;
        private readonly ListBox _list;
        private readonly Label _hint;
        public bool AllFound;

        public MissingFoldersForm(List<DetectedUnit> missing)
            : base("还没有载具文件夹", 640, 470)
        {
            _missing = new List<DetectedUnit>(missing);
            _hint = MakeLabel("下面这些载具在 UserSkins 里还没有文件夹，游戏不会读取它们的涂装。",
                     24, 14, 592, 24, Ui.Body, Ui.Text);
            MakeLabel("先在游戏里打开该载具的「外观 → 涂装」，点一次「创建涂装示例」，游戏会生成 template_<载具ID> 文件夹；然后回来点「重新检查」。",
                     24, 40, 592, 44, Ui.Small, Ui.TextDim);

            _list = new ListBox();
            _list.Left = Ui.S(24);
            _list.Top = Ui.S(96);
            _list.Width = Ui.S(592);
            _list.Height = Ui.S(232);
            _list.BackColor = Ui.BgSoft;
            _list.ForeColor = Ui.Text;
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.S(44);
            _list.Font = Ui.Body;
            _list.IntegralHeight = false;
            _list.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= _list.Items.Count)
                {
                    return;
                }
                DetectedUnit unit = (DetectedUnit)_list.Items[e.Index];
                Ui.Smooth(e.Graphics);
                Rectangle r = new Rectangle(e.Bounds.X + Ui.S(10), e.Bounds.Y, e.Bounds.Width - Ui.S(20), e.Bounds.Height);
                Ui.DrawText(e.Graphics, unit.Title, Ui.BodyBold, Ui.Text,
                        new Rectangle(r.X, r.Y + Ui.S(4), r.Width, Ui.S(20)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                Ui.DrawText(e.Graphics, unit.UnitId + "  ·  需要一个 template_" + unit.UnitId + " 或包含 " + unit.UnitId + ".blk 的文件夹",
                        Ui.Small, Ui.TextFaint,
                        new Rectangle(r.X, r.Y + Ui.S(22), r.Width, Ui.S(18)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            Content.Controls.Add(_list);
            RefreshItems();
            Shown += delegate { Native.ThemeScrollbars(this); };

            MakeLabel("提示：在游戏里创建模板后不需要重启游戏，回到这里点重新检查即可。",
                     24, 340, 592, 20, Ui.Small, Ui.TextFaint);

            FlatButton open = MakeButton("打开 UserSkins", 24, 380, 130, false);
            open.Click += delegate
            {
                string dir = Config.UserSkinsDir;
                if (Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                }
            };
            FlatButton cancel = MakeButton("取消", 408, 380, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton recheck = MakeButton("重新检查", 514, 380, 102, true);
            recheck.Click += delegate
            {
                for (int i = _missing.Count - 1; i >= 0; i--)
                {
                    if (Importer.FindExistingFolder(Config.UserSkinsDir, _missing[i].UnitId) != null)
                    {
                        _missing.RemoveAt(i);
                    }
                }
                RefreshItems();
                if (_missing.Count == 0)
                {
                    AllFound = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _hint.Text = "还有 " + _missing.Count + " 个载具没有文件夹，请先在游戏里创建模板。";
                }
            };
        }

        private void RefreshItems()
        {
            _list.Items.Clear();
            for (int i = 0; i < _missing.Count; i++)
            {
                _list.Items.Add(_missing[i]);
            }
        }
    }

    internal class ImportPlanForm : ModalForm
    {
        private readonly CheckRow _backupRow;
        private readonly ListBox _list;
        public bool Confirmed;

        public ImportPlanForm(ImportPlan plan)
            : base("确认导入", 760, 520)
        {
            long bytes = 0;
            int folders = 0;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                if (seen.Add(plan.Targets[i].Folder))
                {
                    bytes += plan.Targets[i].Package.TotalBytes;
                    folders++;
                }
            }

            string warnings = "";
            for (int i = 0; i < plan.Warnings.Count && i < 3; i++)
            {
                warnings += "· " + plan.Warnings[i] + "\n";
            }
            if (plan.Warnings.Count > 3)
            {
                warnings += "· 还有 " + (plan.Warnings.Count - 3) + " 条提示";
            }

            int listHeight = Math.Min(280, Math.Max(56, plan.Targets.Count * 52));
            int optionsTop = 64 + listHeight + 12;
            int warnTop = optionsTop + 52;
            int warnHeight = warnings.Length > 0 ? 66 : 0;
            int buttonsTop = warnTop + warnHeight + 14;
            ResizeToDesign(760, 58 + buttonsTop + 38 + 20);

            MakeLabel(plan.Targets.Count.ToString(CultureInfo.InvariantCulture) + " 个载具 · " + folders +
                      " 个文件夹 · 约 " + Util.HumanSize(bytes),
                      24, 10, 500, 24, Ui.H1, Ui.Text);
            MakeLabel("会写进游戏已经生成的载具文件夹，先把旧内容备份到安装目录，再清空复制。",
                      24, 36, 660, 20, Ui.Small, Ui.TextDim);

            _list = new ListBox();
            _list.Left = Ui.S(24);
            _list.Top = Ui.S(64);
            _list.Width = Ui.S(712);
            _list.Height = Ui.S(listHeight);
            _list.BackColor = Ui.BgSoft;
            _list.ForeColor = Ui.Text;
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.S(52);
            _list.Font = Ui.Body;
            _list.IntegralHeight = false;
            _list.DrawItem += DrawTargetItem;
            Content.Controls.Add(_list);
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                _list.Items.Add(plan.Targets[i]);
            }
            Shown += delegate { Native.ThemeScrollbars(this); };

            _backupRow = new CheckRow();
            _backupRow.Title = "备份旧文件夹";
            _backupRow.Subtitle = "移到安装目录的 backups，可随时找回";
            _backupRow.Checked = Config.Backup;
            _backupRow.Left = Ui.S(20);
            _backupRow.Top = Ui.S(optionsTop);
            _backupRow.Width = Ui.S(700);
            _backupRow.Height = Ui.S(44);
            Content.Controls.Add(_backupRow);

            if (warnHeight > 0)
            {
                Label warn = MakeLabel(warnings, 24, warnTop, 712, warnHeight, Ui.Small, Ui.Warn);
                warn.AutoEllipsis = true;
            }

            FlatButton cancel = MakeButton("取消", 520, buttonsTop, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("开始导入", 626, buttonsTop, 110, true);
            ok.Click += delegate
            {
                Config.Backup = _backupRow.Checked;
                Config.Save();
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private void DrawTargetItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count)
            {
                return;
            }
            ImportTarget target = (ImportTarget)_list.Items[e.Index];
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            int left = r.X + Ui.S(10);
            string nationName = target.Unit.Vehicle != null ? NationInfo.Name(target.Unit.Vehicle.Nation) : "";
            if (nationName.Length > 0)
            {
                Rectangle flagRect = new Rectangle(left, r.Y + Ui.S(6), Ui.S(24), Ui.S(16));
                NationInfo.Draw(g, flagRect, target.Unit.Vehicle.Nation);
                left = flagRect.Right + Ui.S(7);
            }
            string title = target.Unit.Title + (nationName.Length > 0 ? "   ·   " + nationName : "");
            Ui.DrawText(g, title, Ui.BodyBold, Ui.Text,
                    new Rectangle(left, r.Y + Ui.S(4), Math.Max(Ui.S(80), r.Right - left - r.Width / 2), Ui.S(22)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Ui.DrawText(g, "← " + target.Package.Name, Ui.Small, Ui.TextFaint,
                    new Rectangle(r.X + r.Width / 2, r.Y + Ui.S(4), r.Width / 2 - Ui.S(10), Ui.S(22)),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Ui.DrawText(g, "写入  " + Path.GetFileName(target.Folder), Ui.Small, Ui.Good,
                    new Rectangle(r.X + Ui.S(10), r.Y + Ui.S(25), r.Width - Ui.S(20), Ui.S(18)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, r.X + Ui.S(10), r.Bottom - 1, r.Right - Ui.S(10), r.Bottom - 1);
            }
        }
    }

    internal class ProgressForm : ModalForm
    {
        private readonly Label _currentLabel;
        private readonly Label _percentLabel;
        private readonly Panel _bar;
        private readonly FlatButton _cancel;
        private long _done;
        private long _total;
        private bool _cancelRequested;

        public ImportResult Result;

        public ProgressForm(ImportPlan plan)
            : base("正在导入涂装", 580, 276)
        {
            _currentLabel = MakeLabel("准备中…", 24, 14, 520, 24, Ui.Body, Ui.Text);
            _percentLabel = MakeLabel("0%", 24, 68, 520, 22, Ui.SmallBold, Ui.TextDim);

            _bar = new Panel();
            _bar.Left = Ui.S(24);
            _bar.Top = Ui.S(96);
            _bar.Width = Ui.S(520);
            _bar.Height = Ui.S(10);
            _bar.BackColor = Color.Transparent;
            _bar.Paint += delegate(object sender, PaintEventArgs e)
            {
                Ui.Smooth(e.Graphics);
                Rectangle track = new Rectangle(0, 0, _bar.Width, _bar.Height);
                Ui.FillRound(e.Graphics, track, _bar.Height / 2, Ui.PanelActive);
                double ratio = _total <= 0 ? 0 : Math.Min(1.0, (double)_done / _total);
                int w = (int)(_bar.Width * ratio);
                if (w > 2)
                {
                    Ui.FillRound(e.Graphics, new Rectangle(0, 0, w, _bar.Height), _bar.Height / 2, Ui.Accent);
                }
            };
            Content.Controls.Add(_bar);

            MakeLabel("导入过程中请不要关闭游戏目录里的文件夹。", 24, 118, 500, 20, Ui.Small, Ui.TextFaint);
            _cancel = MakeButton("取消", 434, 150, 104, false);
            _cancel.Click += delegate
            {
                _cancelRequested = true;
                _cancel.Enabled = false;
                _cancel.Text = "正在停止…";
            };

            Shown += delegate { Start(plan); };
        }

        private void Start(ImportPlan plan)
        {
            Thread thread = new Thread(delegate()
            {
                ImportResult result = null;
                try
                {
                    result = Importer.Execute(plan, delegate(long done, long total, string current)
                    {
                        Report(done, total, current);
                    }, delegate { return _cancelRequested; });
                }
                catch (Exception ex)
                {
                    result = new ImportResult();
                    result.Failed.Add("导入失败：" + ex.Message);
                }
                Final(result);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void Report(long done, long total, string current)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _done = done;
                    _total = total;
                    if (current != null && current.Length > 0)
                    {
                        _currentLabel.Text = current;
                    }
                    double ratio = total <= 0 ? 0 : Math.Min(1.0, (double)done / total);
                    _percentLabel.Text = ((int)(ratio * 100)).ToString(CultureInfo.InvariantCulture) + "%   " +
                                         Util.HumanSize(done) + " / " + Util.HumanSize(total);
                    _bar.Invalidate();
                });
            }
            catch
            {
            }
        }

        private void Final(ImportResult result)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    Result = result;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            }
            catch
            {
            }
        }
    }

    internal class ResultForm : ModalForm
    {
        public ResultForm(ImportResult result, string title, string subtitle)
            : base(title, 620, 420)
        {
            int items = result.Done.Count + result.Failed.Count;
            int listHeight = Math.Min(260, Math.Max(68, items * 34));
            int buttonsTop = 48 + listHeight + 18;
            ResizeToDesign(620, 58 + buttonsTop + 38 + 20);

            MakeLabel(subtitle, 24, 10, 560, 24, Ui.H1,
                      result.Failed.Count == 0 ? Ui.Good : Ui.Warn);

            ListBox list = new ListBox();
            list.Left = Ui.S(24);
            list.Top = Ui.S(48);
            list.Width = Ui.S(570);
            list.Height = Ui.S(listHeight);
            list.BackColor = Ui.BgSoft;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Ui.S(34);
            list.Font = Ui.Body;
            list.IntegralHeight = false;
            list.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0)
                {
                    return;
                }
                string text = (string)list.Items[e.Index];
                Ui.Smooth(e.Graphics);
                Color color = text.StartsWith("✕", StringComparison.Ordinal) ? Ui.Bad :
                              text.StartsWith("!", StringComparison.Ordinal) ? Ui.Warn : Ui.Text;
                Ui.DrawText(e.Graphics, text, Ui.Body, color,
                        new Rectangle(e.Bounds.X + Ui.S(8), e.Bounds.Y, e.Bounds.Width - Ui.S(8), e.Bounds.Height),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            for (int i = 0; i < result.Done.Count; i++)
            {
                list.Items.Add("✓  " + result.Done[i]);
            }
            for (int i = 0; i < result.Failed.Count; i++)
            {
                list.Items.Add("✕  " + result.Failed[i]);
            }
            Content.Controls.Add(list);
            Shown += delegate { Native.ThemeScrollbars(this); };

            FlatButton open = MakeButton("打开 UserSkins", 24, buttonsTop, 150, false);
            open.Click += delegate
            {
                string dir = Config.UserSkinsDir;
                if (Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                }
            };
            FlatButton close = MakeButton("完成", 486, buttonsTop, 108, true);
            close.Click += delegate { DialogResult = DialogResult.OK; Close(); };
        }
    }
}
