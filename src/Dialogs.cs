using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    /// <summary>网盘下载按钮：左侧单色图标底（与页面背景区分），右侧文字。</summary>
    internal class DownloadLinkButton : Control
    {
        public event EventHandler Open;
        private bool _hover;
        private bool _down;
        private string _badge = "";
        private string _title = "";

        public string Badge
        {
            get { return _badge; }
            set { _badge = value ?? ""; Invalidate(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; Invalidate(); }
        }

        public DownloadLinkButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Open != null)
            {
                Open(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _down ? Ui.PanelActive : (_hover ? Ui.PanelHover : Ui.BgSoft);
            Ui.FillRound(g, r, Ui.S(10), fill);
            Ui.DrawRound(g, r, Ui.S(10), _hover ? Ui.Accent : Ui.LineSoft, Ui.BorderWidth);
            int box = Ui.S(30);
            Rectangle icon = new Rectangle(Ui.S(12), (Height - box) / 2, box, box);
            Ui.FillRound(g, icon, Ui.S(8), Ui.PanelActive);
            Ui.DrawText(g, _badge, Ui.SmallBold, Ui.Accent, icon,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            Rectangle text = new Rectangle(icon.Right + Ui.S(10), 0,
                    Math.Max(Ui.S(20), Width - icon.Right - Ui.S(20)), Height);
            Ui.DrawText(g, _title, Ui.BodyBold, Ui.Text, text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>删除图标包确认：深色弹窗，默认焦点在取消上。</summary>
    internal class DeleteIconsConfirmForm : ModalForm
    {
        public DeleteIconsConfirmForm(string path)
            : base(Lang.T("删除图标包", "Delete icon pack"), 560, 280)
        {
            MakeLabel(Lang.T("确定删除图标包？", "Delete the icon pack?"), 24, 16, 512, 26, Ui.BodyBold, Ui.Text);
            Label pathLabel = MakeLabel(path, 24, 48, 512, 22, Ui.Mono, Ui.TextDim);
            pathLabel.AutoEllipsis = true;
            MakeLabel(Lang.T("删除并重启后，载具图标会显示为占位图；之后可以重新拖入或从网盘下载。", "Vehicle icons will show as placeholders after deleting and restarting. You can drag in the pack again or download it later."),
                      24, 84, 512, 40, Ui.Small, Ui.TextDim);

            FlatButton cancel = MakeButton(Lang.T(Lang.T("取消", "Cancel"), "Cancel"), 300, 190, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton delete = MakeButton(Lang.T("删除", "Delete"), 408, 190, 120, false);
            delete.Danger = true;
            delete.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Shown += delegate
            {
                int y = Content.Height - Ui.S(18) - Ui.S(38);
                delete.SetBounds(Content.Width - Ui.S(24) - Ui.S(120), y, Ui.S(120), Ui.S(38));
                cancel.SetBounds(delete.Left - Ui.S(10) - Ui.S(96), y, Ui.S(96), Ui.S(38));
                cancel.Focus();
            };
        }
    }

    /// <summary>深色信息提示：单一「确定」按钮。</summary>
    internal class InfoDialog : ModalForm
    {
        public InfoDialog(string title, string message)
            : base(title, 560, 260)
        {
            Label text = MakeLabel(message, 24, 16, 512, 120, Ui.Body, Ui.Text);
            text.AutoEllipsis = true;
            FlatButton ok = MakeButton(Lang.T("确定", "OK"), 440, 180, 96, true);
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Shown += delegate
            {
                int y = Content.Height - Ui.S(18) - Ui.S(38);
                ok.SetBounds(Content.Width - Ui.S(24) - Ui.S(96), y, Ui.S(96), Ui.S(38));
                ok.Focus();
            };
        }
    }

    /// <summary>需要重启时的二选一：稍后重启（默认）或立刻重启。</summary>
    internal class RestartChoiceDialog : ModalForm
    {
        public RestartChoiceDialog(string message)
            : base(Lang.T("需要重启", "Restart required"), 520, 250)
        {
            Label text = MakeLabel(message, 24, 16, 472, 88, Ui.Body, Ui.Text);
            text.AutoEllipsis = true;
            MakeLabel(Lang.T("选择「立刻重启」后约 2 秒自动重新打开程序。", "The program will reopen automatically about 2 seconds after choosing Restart now."),
                      24, 112, 472, 20, Ui.Small, Ui.TextFaint);

            FlatButton later = MakeButton(Lang.T("稍后重启", "Restart later"), 278, 160, 100, false);
            later.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton now = MakeButton(Lang.T("立刻重启", "Restart now"), 390, 160, 100, true);
            now.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Shown += delegate
            {
                int y = Content.Height - Ui.S(18) - Ui.S(38);
                now.SetBounds(Content.Width - Ui.S(24) - Ui.S(100), y, Ui.S(100), Ui.S(38));
                later.SetBounds(now.Left - Ui.S(10) - Ui.S(100), y, Ui.S(100), Ui.S(38));
                later.Focus();
            };
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
        private Panel _pageUpdate;
        private SideTab _tabUi;
        private SideTab _tabPrefs;
        private SideTab _tabCert;
        private SideTab _tabUpdate;
        private Panel _pageDonate;
        private SideTab _tabDonate;
        private Image _donateAlipay;
        private Image _donateWechat;
        private Label _certState;
        private Label _certDetail;
        private FlatButton _certAdd;
        private FlatButton _certRemove;
        private UpdateCheckSection _updateSection;
        private Panel _logScroll;
        private SettingsLogView _logView;
        private Label _logTitle;
        private Label _logHint;
        private Label _logCount;
        private Panel _logDivider;
        private FlatButton _logMore;
        private RemoteUpdate _remoteUpdate;
        private int _shownVersions = 5;
        private int _startPage;
        private int _theme;
        private int _zoom;
        private int _keepValue;
        private int _missingPolicy;
        private FlatButton _policyAsk;
        private FlatButton _policyAuto;
        private FlatButton _policyOff;
        private BufferedPanel _pageIcons;
        private SideTab _tabIcons;

        public bool NeedRestart;

        public SettingsForm()
            : this(0)
        {
        }

        public SettingsForm(int startPage)
            : base(Lang.T("设置", "Settings"), 840, 640)
        {
            ShowGear = true;
            _theme = Config.ThemeIndex;
            _zoom = Config.ZoomPercent;
            _keepValue = Config.KeepBackups;
            _missingPolicy = Config.MissingFolderPolicy;

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

            _pageUpdate = new BufferedPanel();
            _pageUpdate.BackColor = Ui.Panel;
            _pageUpdate.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            _pageUpdate.Visible = false;
            Content.Controls.Add(_pageUpdate);

            _pageIcons = new BufferedPanel();
            _pageIcons.BackColor = Ui.Panel;
            _pageIcons.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            _pageIcons.Visible = false;
            Content.Controls.Add(_pageIcons);

            _pageDonate = new BufferedPanel();
            _pageDonate.BackColor = Ui.Panel;
            _pageDonate.SetBounds(Ui.S(200), Ui.S(12), Ui.S(pageWidth), Ui.S(pageHeight));
            _pageDonate.Visible = false;
            Content.Controls.Add(_pageDonate);

            _tabUi = new SideTab();
            _tabUi.Text = Lang.T("界面设置", "Appearance");
            _tabUi.Selected = true;
            _tabUi.SetBounds(Ui.S(16), Ui.S(16), Ui.S(168), Ui.S(42));
            _tabUi.Click += delegate { SelectPage(0); };
            Content.Controls.Add(_tabUi);

            _tabPrefs = new SideTab();
            _tabPrefs.Text = Lang.T("使用偏好", "Preferences");
            _tabPrefs.SetBounds(Ui.S(16), Ui.S(64), Ui.S(168), Ui.S(42));
            _tabPrefs.Click += delegate { SelectPage(1); };
            Content.Controls.Add(_tabPrefs);

            _tabCert = new SideTab();
            _tabCert.Text = Lang.T(Lang.T("发布者签名", "Publisher signature"), "Publisher");
            _tabCert.SetBounds(Ui.S(16), Ui.S(112), Ui.S(168), Ui.S(42));
            _tabCert.Click += delegate { SelectPage(2); };
            Content.Controls.Add(_tabCert);

            _tabIcons = new SideTab();
            _tabIcons.Text = Lang.T("涂装包", "Icon pack");
            _tabIcons.SetBounds(Ui.S(16), Ui.S(208), Ui.S(168), Ui.S(42));
            _tabIcons.Click += delegate { SelectPage(5); };
            Content.Controls.Add(_tabIcons);

            _tabUpdate = new SideTab();
            _tabUpdate.Text = Lang.T("更新与日志", "Updates");
            _tabUpdate.SetBounds(Ui.S(16), Ui.S(160), Ui.S(168), Ui.S(42));
            _tabUpdate.Click += delegate { SelectPage(3); };
            Content.Controls.Add(_tabUpdate);

            _tabDonate = new SideTab();
            _tabDonate.Text = Lang.T(Lang.T("关于", "About"), "About");
            _tabDonate.SetBounds(Ui.S(16), Content.Height - Ui.S(58), Ui.S(168), Ui.S(42));
            _tabDonate.Click += delegate { SelectPage(4); };
            Content.Controls.Add(_tabDonate);

            BuildUiPage();
            BuildPrefsPage();
            BuildCertPage();
            BuildUpdatePage();
            BuildDonatePage();
            BuildIconsPage();

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 610, 536, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton save = MakeButton(Lang.T("保存", "Save"), 718, 536, 98, true);
            save.Click += delegate { SaveSettings(); };

            // 分页切换放到 OnShown：此时句柄/滚动容器都已创建，避免在构造函数里提前布局。
            _startPage = startPage;
        }

        private void SelectPage(int page)
        {
            _pageUi.Visible = page == 0;
            _pagePrefs.Visible = page == 1;
            _pageCert.Visible = page == 2;
            _pageUpdate.Visible = page == 3;
            _pageDonate.Visible = page == 4;
            _pageIcons.Visible = page == 5;
            _tabUi.Selected = page == 0;
            _tabPrefs.Selected = page == 1;
            _tabCert.Selected = page == 2;
            _tabUpdate.Selected = page == 3;
            _tabDonate.Selected = page == 4;
            _tabIcons.Selected = page == 5;
            _tabUi.Invalidate();
            _tabPrefs.Invalidate();
            _tabCert.Invalidate();
            _tabUpdate.Invalidate();
            _tabDonate.Invalidate();
            _tabIcons.Invalidate();
            if (page == 2)
            {
                RefreshCert();
            }
            else if (page == 3)
            {
                if (_updateSection != null)
                {
                    _updateSection.Activate();
                }
                LayoutUpdatePage();
                if (_logScroll != null)
                {
                    Native.ThemeScrollbars(_logScroll);
                    _logScroll.AutoScrollPosition = new Point(0, 0);
                }
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
            PageLabel(_pageUi, Lang.T("主题", "Theme"), 8, 6, 120, 22, Ui.SmallBold, Ui.TextDim);
            int chipCount = Palettes.All.Length;
            int chipMargin = Ui.S(8);
            int chipGap = Ui.S(10);
            int chipWidth = Math.Max(Ui.S(96),
                (_pageUi.Width - chipMargin * 2 - chipGap * (chipCount - 1)) / Math.Max(1, chipCount));
            for (int i = 0; i < chipCount; i++)
            {
                ThemeChip chip = new ThemeChip();
                chip.Index = i;
                chip.Selected = i == _theme;
                chip.Left = chipMargin + i * (chipWidth + chipGap);
                chip.Top = Ui.S(32);
                chip.Width = chipWidth;
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

            PageLabel(_pageUi, Lang.T("缩放", "Zoom"), 8, 118, 60, 20, Ui.SmallBold, Ui.TextDim);
            int zoomValueWidth = Math.Max(Ui.S(70), TextRenderer.MeasureText("150%", Ui.BodyBold).Width + Ui.S(24));
            int zoomButtonWidth = Ui.S(38);
            // “缩放”标签宽 60，按钮从 72 开始，避免标签的实底盖住按钮左边缘。
            int zoomX = Ui.S(72);
            FlatButton zoomOut = PageButton(_pageUi, "－", 72, 112, 38, false);
            zoomOut.Click += delegate { ChangeZoom(-5); };
            zoomX += zoomButtonWidth + Ui.S(4);
            _zoomLabel = PageLabel(_pageUi, _zoom + "%", 0, 112, 70, 38, Ui.BodyBold, Ui.Text);
            _zoomLabel.Left = zoomX;
            _zoomLabel.Width = zoomValueWidth;
            _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            zoomX += zoomValueWidth + Ui.S(4);
            FlatButton zoomIn = PageButton(_pageUi, "＋", 0, 112, 38, false);
            zoomIn.Left = zoomX;
            zoomIn.Click += delegate { ChangeZoom(5); };
            zoomX += zoomButtonWidth + Ui.S(12);
            // 这里传入的是已缩放的物理坐标，直接设 Left/Width，不能再经 PageLabel 二次 Ui.S。
            Label zoomHint = PageLabel(_pageUi, Lang.T("75% – 150%，修改后需要重启程序生效。", "75% – 150%; restart the program to apply."),
                      0, 120, 10, 20, Ui.Small, Ui.TextFaint);
            zoomHint.Left = zoomX;
            zoomHint.Width = Math.Max(Ui.S(120), _pageUi.Width - zoomX - Ui.S(8));

            _animRow = new CheckRow();
            _animRow.Title = Lang.T("界面动画", "UI animations");
            _animRow.Subtitle = Lang.T("对话框滑入、卡片悬停渐变等", "Dialog slides and card hover fades");
            _animRow.Checked = false;
            _animRow.Visible = false;
            _animRow.Left = Ui.S(4);
            _animRow.Top = Ui.S(166);
            _animRow.Width = Ui.S(600);
            _animRow.Height = Ui.S(48);
            _animRow.BackColor = Ui.Panel;
            _pageUi.Controls.Add(_animRow);

            PageLabel(_pageUi, Lang.T("当前的界面缩放会按显示器 DPI 自动适配，高分屏觉得字发虚时可以在这里调大。", "UI scale follows the display DPI automatically; increase it if text looks blurry on high-DPI screens."),
                      8, 226, 590, 40, Ui.Small, Ui.TextFaint);
            PageLabel(_pageUi, Lang.T("主题和缩放修改后需要重启程序才会完全生效。", "Theme and zoom changes take full effect after restarting the program."),
                      8, 266, 590, 20, Ui.Small, Ui.Accent);
        }

        private void BuildPrefsPage()
        {
            PageLabel(_pagePrefs, Lang.T("游戏目录", "Game folder"), 8, 6, 200, 22, Ui.SmallBold, Ui.TextDim);
            _dirBox = PageField(_pagePrefs, 8, 30, 592, 40);
            _dirBox.Text = Config.GameDir;

            PageButton(_pagePrefs, Lang.T("自动检测", "Auto-detect"), 500, 78, 90, false).Click += delegate
            {
                string best = GameLocator.FindBest();
                if (string.IsNullOrEmpty(best))
                {
                    MessageBox.Show(this, Lang.T("没有找到战雷安装目录，请手动选择。", "War Thunder installation not found. Please select it manually."), Lang.T("自动检测", "Auto-detect"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _dirBox.Text = best;
            };
            PageButton(_pagePrefs, Lang.T("浏览…", "Browse…"), 402, 78, 90, false).Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = Lang.T("选择 War Thunder 安装目录", "Select the War Thunder install folder");
                    dialog.SelectedPath = _dirBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _dirBox.Text = dialog.SelectedPath;
                    }
                }
            };

            PageLabel(_pagePrefs, Lang.T("导入", "Import"), 8, 136, 100, 22, Ui.SmallBold, Ui.TextDim);
            _backupRow = new CheckRow();
            _backupRow.Title = Lang.T("导入前备份旧文件夹", "Back up old folders before import");
            _backupRow.Subtitle = Lang.T("旧内容会复制到安装目录的 backups 里，不会写进战雷目录", "Old content is copied to backups in the install folder, never into the game folder");
            _backupRow.Checked = Config.Backup;
            _backupRow.Left = Ui.S(4);
            _backupRow.Top = Ui.S(160);
            _backupRow.Width = Ui.S(600);
            _backupRow.Height = Ui.S(48);
            _backupRow.BackColor = Ui.Panel;
            _pagePrefs.Controls.Add(_backupRow);

            PageLabel(_pagePrefs, Lang.T("保留最近几份", "Keep latest"), 8, 220, 110, 20, Ui.Small, Ui.TextFaint);
            FlatButton keepMinus = PageButton(_pagePrefs, "－", 116, 214, 34, false);
            keepMinus.Click += delegate { ChangeKeep(-1); };
            _keepLabel = PageLabel(_pagePrefs, _keepValue.ToString(), 156, 214, 54, 38, Ui.BodyBold, Ui.Text);
            _keepLabel.TextAlign = ContentAlignment.MiddleCenter;
            FlatButton keepPlus = PageButton(_pagePrefs, "＋", 216, 214, 34, false);
            keepPlus.Click += delegate { ChangeKeep(1); };
            PageLabel(_pagePrefs, Lang.T("份（1–50），旧备份会被自动清理", "copies (1–50); older backups are cleaned up automatically"), 264, 222, 300, 20, Ui.Small, Ui.TextFaint);

            PageLabel(_pagePrefs, Lang.T("安装目录", "Install folder"), 8, 276, 120, 20, Ui.SmallBold, Ui.TextDim);
            PageLabel(_pagePrefs, Config.InstallDir, 8, 298, 420, 20, Ui.Small, Ui.TextDim);
            PageButton(_pagePrefs, Lang.T("打开", "Open"), 440, 288, 70, false).Click += delegate { OpenFolder(Config.InstallDir); };
            PageButton(_pagePrefs, Lang.T("备份", "Back up"), 518, 288, 70, false).Click += delegate { OpenFolder(Config.BackupDir); };
            PageLabel(_pagePrefs, Lang.T("备份、日志、缓存都在安装目录里，不会写进战雷目录。", "Backups, logs and cache live in the install folder, never in the game folder."),
                      8, 330, 590, 20, Ui.Small, Ui.TextFaint);
            PageLabel(_pagePrefs, Lang.T("程序 v", "App v") + AppInfo.VersionText + Lang.T(" · 数据 ", " · data ") + AppInfo.DataVersionText,
                      8, 346, 260, 18, Ui.Small, Ui.TextFaint);

            PageButton(_pagePrefs, Lang.T("查看导入日志", "View import log"), 8, 366, 130, false).Click += delegate
            {
                OpenFolder(Config.LogDir);
            };
            PageButton(_pagePrefs, Lang.T("打开安装目录", "Open install folder"), 148, 366, 130, false).Click += delegate
            {
                OpenFolder(Config.InstallDir);
            };
            // 与「安装/更新完成」窗口共用同一份更新日志数据（ReleaseLog），不另写文案。
            PageButton(_pagePrefs, Lang.T("历史版本", "Version history"), 288, 366, 110, false).Click += delegate
            {
                using (UpdateNotesForm form = UpdateNotesForm.History())
                {
                    form.ShowDialog(this);
                }
            };

            PageLabel(_pagePrefs, Lang.T("缺少涂装目录时", "When a skin folder is missing"), 8, 406, 180, 20, Ui.SmallBold, Ui.TextDim);
            _policyAsk = PageButton(_pagePrefs, Lang.T("每次询问", "Ask every time"), 8, 428, 124, false);
            _policyAsk.Click += delegate { SetMissingPolicy(0); };
            _policyAuto = PageButton(_pagePrefs, Lang.T("自动创建", "Create automatically"), 140, 428, 124, false);
            _policyAuto.Click += delegate { SetMissingPolicy(1); };
            _policyOff = PageButton(_pagePrefs, Lang.T("关闭", "Off"), 272, 428, 90, false);
            _policyOff.Click += delegate { SetMissingPolicy(2); };
            RefreshPolicyButtons();
        }

        private void SetMissingPolicy(int policy)
        {
            _missingPolicy = Math.Max(0, Math.Min(2, policy));
            RefreshPolicyButtons();
        }

        private void RefreshPolicyButtons()
        {
            if (_policyAsk == null)
            {
                return;
            }
            _policyAsk.Primary = _missingPolicy == 0;
            _policyAuto.Primary = _missingPolicy == 1;
            _policyOff.Primary = _missingPolicy == 2;
            _policyAsk.Invalidate();
            _policyAuto.Invalidate();
            _policyOff.Invalidate();
        }

        /// <summary>
        /// 「更新与日志」页：上方检查更新，下方平铺更新日志。日志与安装完成窗口同源
        /// （assets\data\updates.dat → ReleaseLog），界面不另写版本文案。
        /// </summary>
        private void BuildDonatePage()
        {
            _donateAlipay = LoadDonateImage("donate_alipay.jpg");
            _donateWechat = LoadDonateImage("donate_wechat.png");
            PageLabel(_pageDonate, Lang.T("关于", "About"), 8, 4, 240, 30, Ui.H1, Ui.Text);
            PageLabel(_pageDonate, Lang.T("战雷涂装管理器  ", "WTSkinManager  ") + BuildInfo.HeaderText, 8, 40, 580, 22, Ui.Small, Ui.TextDim);
            PageLabel(_pageDonate, Lang.T("War Thunder 自定义涂装的识别、导入与管理工具。个人开发，与 Gaijin Entertainment 无关联。", "A tool to detect, import and manage War Thunder custom skins. Individually developed; not affiliated with Gaijin Entertainment."),
                      8, 64, 580, 40, Ui.Small, Ui.TextDim);
            PageButton(_pageDonate, Lang.T("GitHub 仓库", "GitHub"), 8, 106, 118, true).Click += delegate { OpenUrl(Config.RepoUrl); };
            PageButton(_pageDonate, Lang.T("问题反馈", "Issues"), 134, 106, 92, false).Click += delegate { OpenUrl(Config.RepoUrl + "/issues"); };
            PageButton(_pageDonate, Lang.T("开源协议", "License"), 234, 106, 92, false).Click += delegate { OpenUrl(Config.RepoUrl + "/blob/main/LICENSE"); };
            PageLabel(_pageDonate, Lang.T("赞助我", "Support me"), 8, 152, 200, 24, Ui.BodyBold, Ui.Text);
            PageLabel(_pageDonate, Lang.T("如果这个工具帮到了你，可以扫码支持一下；完全自愿，不影响任何功能。", "If this tool helps you, you can scan to support it. Completely optional and does not affect any feature."),
                      8, 176, 580, 20, Ui.Small, Ui.TextDim);
            AddDonateCard(_pageDonate, _donateAlipay, Lang.T("支付宝", "Alipay"), 24, 204);
            AddDonateCard(_pageDonate, _donateWechat, Lang.T("微信支付", "WeChat Pay"), 320, 204);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void AddDonateCard(Panel page, Image image, string caption, int x, int y)
        {
            Panel card = new Panel();
            card.BackColor = Ui.Panel;
            card.SetBounds(Ui.S(x), Ui.S(y), Ui.S(252), Ui.S(276));
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Ui.Smooth(g);
                Rectangle r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                Ui.FillRound(g, r, Ui.S(10), Ui.BgSoft);
                Ui.DrawRound(g, r, Ui.S(10), Ui.LineSoft, 1f);
                if (image != null)
                {
                    Rectangle area = new Rectangle(Ui.S(12), Ui.S(12), card.Width - Ui.S(24), card.Height - Ui.S(68));
                    float scale = Math.Min((float)area.Width / image.Width, (float)area.Height / image.Height);
                    int w = Math.Max(1, (int)(image.Width * scale));
                    int h = Math.Max(1, (int)(image.Height * scale));
                    Rectangle target = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                    System.Drawing.Drawing2D.InterpolationMode old = g.InterpolationMode;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, target);
                    g.InterpolationMode = old;
                }
                Ui.DrawText(g, caption, Ui.BodyBold, Ui.Text,
                        new Rectangle(0, card.Height - Ui.S(50), card.Width, Ui.S(24)), Ui.Center);
                Ui.DrawText(g, Lang.T("扫码赞助", "Scan to support"), Ui.Small, Ui.TextFaint,
                        new Rectangle(0, card.Height - Ui.S(26), card.Width, Ui.S(20)), Ui.Center);
            };
            page.Controls.Add(card);
        }

        private static Image LoadDonateImage(string resource)
        {
            try
            {
                using (Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        using (Bitmap bmp = new Bitmap(ms))
                        {
                            return new Bitmap(bmp);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>「涂装包」页：三个网盘并排下载入口 + 拖入 vehicles.zip 一键导入。</summary>
        private void BuildIconsPage()
        {
            PageLabel(_pageIcons, Lang.T("涂装包下载", "Icon pack download"), 8, 2, 240, 30, Ui.H1, Ui.Text);
            PageLabel(_pageIcons, Lang.T("载具图标包属于第三方素材，不随软件、发行 zip 或 GitHub 提供，需要单独下载。", "Vehicle icons are third-party artwork and are not included in the app, release zip or GitHub. Download them separately."),
                      8, 36, 592, 22, Ui.Small, Ui.TextDim);
            PageLabel(_pageIcons, Lang.T("解压后把 vehicles.zip 拖到下方虚线区域，或放到程序目录后重启程序。", "Extract vehicles.zip below, or place it in the program folder and restart the app."),
                      8, 60, 592, 22, Ui.Small, Ui.TextDim);

            PageLabel(_pageIcons, Lang.T("下载地址", "Download links"), 8, 96, 140, 22, Ui.SmallBold, Ui.TextDim);
            DownloadLinkButton baidu = new DownloadLinkButton();
            baidu.Badge = Lang.T("百", "B");
            baidu.Title = Lang.T("百度网盘", "Baidu Pan");
            baidu.SetBounds(Ui.S(8), Ui.S(122), Ui.S(139), Ui.S(56));
            baidu.Open += delegate { OpenUrl("https://pan.baidu.com/s/1xxmhGCyle5F7KNd9OykdXA?pwd=97yx"); };
            _pageIcons.Controls.Add(baidu);
            DownloadLinkButton quark = new DownloadLinkButton();
            quark.Badge = Lang.T("夸", "Q");
            quark.Title = Lang.T("夸克网盘", "Quark");
            quark.SetBounds(Ui.S(159), Ui.S(122), Ui.S(139), Ui.S(56));
            quark.Open += delegate { OpenUrl("https://pan.quark.cn/s/dad0fa59501a"); };
            _pageIcons.Controls.Add(quark);
            DownloadLinkButton pan123 = new DownloadLinkButton();
            pan123.Badge = "123";
            pan123.Title = Lang.T("123 云盘", "123 Pan");
            pan123.SetBounds(Ui.S(310), Ui.S(122), Ui.S(139), Ui.S(56));
            pan123.Open += delegate { OpenUrl("https://1816316610.share.123pan.cn/123pan/mqhqVv-fDAG3?pwd=amUI#"); };
            _pageIcons.Controls.Add(pan123);
            DownloadLinkButton google = new DownloadLinkButton();
            google.Badge = "G";
            google.Title = Lang.T("谷歌云盘", "Google Drive");
            google.SetBounds(Ui.S(461), Ui.S(122), Ui.S(139), Ui.S(56));
            google.Open += delegate { OpenUrl("https://drive.google.com/file/d/1Xgo_oAew1j6c37l1pP0UnnIR8sXJ6fiU/view?usp=sharing"); };
            _pageIcons.Controls.Add(google);

            PageLabel(_pageIcons, Lang.T("图标包约 283 MB（单文件，非分卷）：下载后解压得到 vehicles.zip，可放到程序目录或直接拖入下方区域。", "The icon pack is about 283 MB as a single archive: extract it to get vehicles.zip, then place it in the program folder or drop it below."),
                      8, 190, 592, 22, Ui.Small, Ui.TextFaint);

            Panel drop = new Panel();
            drop.BackColor = Ui.Panel;
            drop.SetBounds(Ui.S(8), Ui.S(226), Ui.S(592), Ui.S(116));
            drop.AllowDrop = true;
            drop.Cursor = Cursors.Hand;
            bool hot = false;
            drop.Paint += delegate(object sender, PaintEventArgs e)
            {
                Ui.Smooth(e.Graphics);
                Rectangle r = new Rectangle(0, 0, drop.Width - 1, drop.Height - 1);
                Ui.FillRound(e.Graphics, r, Ui.S(10), hot ? Ui.PanelHover : Ui.BgSoft);
                using (Pen pen = new Pen(hot ? Ui.Accent : Ui.Stroke, Math.Max(1f, Ui.BorderWidth)))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    Ui.DrawRound(e.Graphics, r, Ui.S(10), pen.Color, (int)pen.Width);
                }
                Ui.DrawText(e.Graphics, Lang.T("把 vehicles.zip 拖到这里导入", "Drop vehicles.zip here to import"), Ui.BodyBold,
                        Ui.Text, new Rectangle(Ui.S(12), Ui.S(20), drop.Width - Ui.S(24), Ui.S(24)),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                Ui.DrawText(e.Graphics, Lang.T("会自动复制到程序目录；旧图标包会保留为 .bak 备份。", "It is copied to the program folder; the old pack is kept as a .bak backup."),
                        Ui.Small, Ui.TextFaint,
                        new Rectangle(Ui.S(12), Ui.S(52), drop.Width - Ui.S(24), Ui.S(20)),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                Ui.DrawText(e.Graphics, Lang.T("也可以直接点击这里选择文件。", "You can also click here to choose a file."),
                        Ui.Small, Ui.TextFaint,
                        new Rectangle(Ui.S(12), Ui.S(76), drop.Width - Ui.S(24), Ui.S(20)),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            };
            drop.DragEnter += delegate(object sender, DragEventArgs e)
            {
                hot = e.Data.GetDataPresent(DataFormats.FileDrop);
                e.Effect = hot ? DragDropEffects.Copy : DragDropEffects.None;
                drop.Invalidate();
            };
            drop.DragLeave += delegate { hot = false; drop.Invalidate(); };
            drop.DragDrop += delegate(object sender, DragEventArgs e)
            {
                hot = false;
                drop.Invalidate();
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    ImportIconPackage(files[0]);
                }
            };
            drop.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = Lang.T("选择涂装包 vehicles.zip", "Select icon pack vehicles.zip");
                    dialog.Filter = Lang.T("图标包 (*.zip)|*.zip|所有文件 (*.*)|*.*", "Icon pack (*.zip)|*.zip|All files (*.*)|*.*");
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        ImportIconPackage(dialog.FileName);
                    }
                }
            };
            _pageIcons.Controls.Add(drop);

            PageLabel(_pageIcons, Lang.T("缺少图标包时程序仍可正常使用，只是显示占位图；导入后重启程序生效。", "The app still works without the icon pack and shows placeholders; restart after importing."),
                      8, 354, 592, 20, Ui.Small, Ui.TextFaint);
            PageButton(_pageIcons, Lang.T("打开程序目录", "Open program folder"), 8, 380, 140, false).Click += delegate
            {
                OpenFolder(Config.InstallDir);
            };
            FlatButton deleteIcons = PageButton(_pageIcons, Lang.T("删除图标包", "Delete icon pack"), 160, 380, 140, false);
            deleteIcons.Danger = true;
            deleteIcons.Click += delegate
            {
                string target = Path.Combine(Config.ExeDir, "vehicles.zip");
                if (!File.Exists(target))
                {
                    MessageBox.Show(this, Lang.T("程序目录下没有 vehicles.zip。", "vehicles.zip was not found in the program folder."), Lang.T("删除图标包", "Delete icon pack"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                bool confirmed;
                using (DeleteIconsConfirmForm form = new DeleteIconsConfirmForm(target))
                {
                    confirmed = form.ShowDialog(this) == DialogResult.OK;
                }
                if (!confirmed)
                {
                    return;
                }
                try
                {
                    VehicleIcons.CloseCache();
                    File.Delete(target);
                    Config.Log("删除图标包：" + target);
                    using (InfoDialog info = new InfoDialog(Lang.T("删除图标包", "Delete icon pack"),
                        Lang.T("已删除：", "Deleted: ") + target + Lang.T("\r\n\r\n重启程序后图标会变为占位图，可随时重新拖入或从网盘下载。", "\r\n\r\nIcons will show placeholders after restart; you can drag the pack in again or download it later.")))
                    {
                        info.ShowDialog(this);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Lang.T("删除失败：", "Delete failed: ") + ex.Message, Lang.T("删除图标包", "Delete icon pack"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        /// <summary>校验并导入拖入的 vehicles.zip：必须是合法图标包，复制到程序目录并备份旧包。</summary>
        private void ImportIconPackage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, Lang.T("请拖入解压后的 vehicles.zip；.7z 等压缩包需要先用 7-Zip 解压。", "Drop the extracted vehicles.zip; other archives such as .7z must be extracted with 7-Zip first."),
                                Lang.T("导入图标包", "Import icon pack"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                using (System.IO.Compression.ZipArchive zip =
                       System.IO.Compression.ZipFile.OpenRead(path))
                {
                    bool manifest = false;
                    int icons = 0;
                    for (int i = 0; i < zip.Entries.Count; i++)
                    {
                        string name = zip.Entries[i].FullName;
                        if (string.Equals(name, "manifest.json", StringComparison.OrdinalIgnoreCase))
                        {
                            manifest = true;
                        }
                        else if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            icons++;
                        }
                    }
                    if (!manifest || icons == 0)
                    {
                        MessageBox.Show(this, Lang.T("这不是有效的涂装包：缺少 manifest.json 或图标文件。", "This is not a valid icon pack: manifest.json or icons are missing."),
                                        Lang.T("导入图标包", "Import icon pack"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("无法读取该 zip：", "Cannot read this zip: ") + ex.Message, Lang.T("导入图标包", "Import icon pack"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string target = Path.Combine(Config.ExeDir, "vehicles.zip");
            try
            {
                if (File.Exists(target))
                {
                    string backup = target + ".bak";
                    try
                    {
                        if (File.Exists(backup))
                        {
                            File.Delete(backup);
                        }
                        File.Copy(target, backup, true);
                    }
                    catch
                    {
                    }
                }
                string temp = target + ".new";
                File.Copy(path, temp, true);
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
                File.Move(temp, target);
                Config.Log("导入图标包：" + path + " → " + target);
                bool restart;
                using (RestartChoiceDialog dialog = new RestartChoiceDialog(
                    Lang.T("图标包已导入到：\r\n", "Icon pack imported to:\r\n") + target + Lang.T("\r\n\r\n重启程序后图标才会生效。", "\r\n\r\nRestart the app for icons to appear.")))
                {
                    restart = dialog.ShowDialog(this) == DialogResult.OK;
                }
                if (restart)
                {
                    // 延迟 2 秒再启动新进程，先让当前进程完全退出，避免单实例检查拦截。
                    AppRestart.Restart();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("导入失败：", "Import failed: ") + ex.Message, Lang.T("导入图标包", "Import icon pack"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildUpdatePage()
        {
            _updateSection = new UpdateCheckSection();
            _updateSection.Left = 0;
            _updateSection.Top = 0;
            _updateSection.Width = Ui.S(608);
            _pageUpdate.Controls.Add(_updateSection);
            _updateSection.SendToBack();

            _logDivider = new Panel();
            _logDivider.BackColor = Ui.LineSoft;
            _logDivider.Left = Ui.S(8);
            _logDivider.Top = Ui.S(146);
            _logDivider.Width = Ui.S(592);
            _logDivider.Height = 1;
            _pageUpdate.Controls.Add(_logDivider);

            _logTitle = PageLabel(_pageUpdate, Lang.T("更新日志", "Update log"), 8, 154, 200, 20, Ui.SmallBold, Ui.TextDim);
            _logHint = PageLabel(_pageUpdate, Lang.T("updates.dat · 与安装完成窗口同源", "updates.dat · shared with the install-finished window"), 292, 156, 300, 18,
                                 Ui.Small, Ui.TextFaint);
            _logHint.TextAlign = ContentAlignment.MiddleRight;

            _logScroll = new BufferedPanel();
            _logScroll.Left = Ui.S(8);
            _logScroll.Top = Ui.S(176);
            _logScroll.Width = Ui.S(592);
            _logScroll.Height = Ui.S(294);
            _logScroll.BackColor = Ui.Panel;
            _logScroll.AutoScroll = true;
            _pageUpdate.Controls.Add(_logScroll);

            _logView = new SettingsLogView();
            _logView.Left = 0;
            _logView.Top = 0;
            _logView.Width = _logScroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Ui.S(4);
            _logScroll.Controls.Add(_logView);

            _logMore = PageButton(_pageUpdate, Lang.T("加载更早的版本", "Load older versions"), 8, 474, 160, false);
            _logMore.Height = Ui.S(30);
            _logMore.Click += delegate
            {
                List<ReleaseNote> all = ReleaseLog.All;
                _shownVersions = Math.Min(_shownVersions + 5, Math.Max(all.Count, 5));
                RefreshLog();
            };

            _logCount = PageLabel(_pageUpdate, "", 300, 480, 292, 18, Ui.Small, Ui.TextFaint);
            _logCount.TextAlign = ContentAlignment.MiddleRight;

            _updateSection.HeightChanged += delegate { LayoutUpdatePage(); };
            _updateSection.ResultChanged += OnUpdateResult;
            RefreshLog();
            _updateSection.Activate();
            LayoutUpdatePage();
        }

        private void OnUpdateResult(UpdateCheckResult result)
        {
            if (result != null && result.State == UpdateState.NewVersion)
            {
                RemoteUpdate remote = new RemoteUpdate();
                remote.Version = result.Latest;
                remote.DateText = result.DateText;
                remote.Body = result.Body;
                _remoteUpdate = remote;
            }
            else
            {
                _remoteUpdate = null;
            }
            RefreshLog();
        }

        private void RefreshLog()
        {
            List<ReleaseNote> all = ReleaseLog.All;
            if (_shownVersions < 5)
            {
                _shownVersions = 5;
            }
            int keepScroll = 0;
            try
            {
                keepScroll = _logScroll.VerticalScroll.Value;
                // 重建前先把滚动位置归零：否则 FitLogView 里的 Top=0 会把
                // 已滚动的偏移叠加到新布局上，表现为可以无限往上滑。
                _logScroll.AutoScrollPosition = new Point(0, 0);
            }
            catch
            {
            }
            _logView.SetContent(all, Math.Min(_shownVersions, all.Count), _remoteUpdate);
            int remaining = all.Count - Math.Min(_shownVersions, all.Count);
            _logMore.Visible = remaining > 0;
            _logCount.Visible = all.Count > 0;
            if (all.Count == 0)
            {
                _logCount.Text = "";
            }
            else if (remaining > 0)
            {
                _logCount.Text = Lang.T("已显示 ", "Showing ") + Math.Min(_shownVersions, all.Count) + Lang.T(" / ", " / ") + all.Count + Lang.T(" 个版本", " versions");
            }
            else
            {
                _logCount.Text = Lang.T("已显示全部 ", "Showing all ") + all.Count + Lang.T(" 个版本", " versions");
            }
            FitLogView();
            LayoutUpdatePage();
            try
            {
                int maxScroll = Math.Max(0, _logView.Height - _logScroll.ClientSize.Height);
                _logScroll.AutoScrollPosition = new Point(0, Math.Min(keepScroll, maxScroll));
            }
            catch
            {
            }
        }

        private void FitLogView()
        {
            if (_logScroll == null || _logView == null)
            {
                return;
            }
            // 固定用滚动容器的 Width（不是 ClientSize）：滚动条出现/消失不会反过来改视图宽度，
            // 否则宽度→高度→滚动条可能形成反复布局。
            int width = _logScroll.Width - SystemInformation.VerticalScrollBarWidth - Ui.S(6);
            if (width > Ui.S(200) && _logView.Width != width)
            {
                _logView.Width = width;
            }
            _logView.Left = 0;
            _logView.Top = 0;
        }

        private void LayoutUpdatePage()
        {
            if (_updateSection == null || _logScroll == null)
            {
                return;
            }
            int divider = _updateSection.Height + Ui.S(10);
            _logDivider.Top = divider;
            _logTitle.Top = divider + Ui.S(8);
            _logHint.Top = divider + Ui.S(10);
            _logScroll.Top = divider + Ui.S(30);
            _logScroll.Height = Math.Max(Ui.S(80), Ui.S(470) - _logScroll.Top);
            _logMore.Top = Ui.S(474);
            _logCount.Top = Ui.S(480);
            FitLogView();
        }

        /// <summary>发布者签名页：显示当前状态，提供「补装」与「移除」（移除前二次确认）。</summary>
        private void BuildCertPage()
        {
            PageLabel(_pageCert, Lang.T("发布者签名", "Publisher signature"), 8, 6, 200, 22, Ui.SmallBold, Ui.TextDim);
            _certState = PageLabel(_pageCert, "", 8, 30, 580, 26, Ui.BodyBold, Ui.TextFaint);

            PageLabel(_pageCert, Lang.T("签名用来告诉 Windows 这个软件是谁发布的，减少「未知发布者」的安全提示。", "A signature tells Windows who published this app and reduces unknown-publisher warnings."),
                      8, 62, 590, 20, Ui.Small, Ui.TextDim);
            PageLabel(_pageCert, Lang.T("签名只装到当前用户的受信任发布者列表，不需要管理员权限；移除后程序照常运行。", "The signature is installed for the current user only and needs no admin rights; the app still runs if removed."),
                      8, 84, 590, 20, Ui.Small, Ui.TextDim);

            _certAdd = PageButton(_pageCert, Lang.T("补装发布者签名", "Install signature"), 8, 118, 150, true);
            _certAdd.Click += delegate { AddCert(); };
            _certRemove = PageButton(_pageCert, Lang.T("移除发布者签名", "Remove signature"), 172, 118, 150, false);
            _certRemove.Danger = true;
            _certRemove.Click += delegate { RemoveCert(); };

            _certDetail = PageLabel(_pageCert, "", 8, 176, 590, 110, Ui.Small, Ui.TextFaint);
            PageLabel(_pageCert, Lang.T("证书随程序内嵌分发（publisher.cer，只含公钥），导入前会校验 SHA-256。", "The certificate is embedded (publisher.cer, public key only); SHA-256 is verified before installing."),
                      8, 300, 590, 20, Ui.Small, Ui.TextFaint);
            PageLabel(_pageCert, Lang.T("移除后 Windows 可能重新显示「未知发布者」，不影响导入涂装等功能；之后可随时补装。", "Windows may show unknown publisher again after removal; this does not affect skin importing, and you can reinstall it anytime."),
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
            _certState.Text = installed ? Lang.T("已安装 · ", "Installed · ") + PublisherCert.PublisherName : Lang.T("未安装", "Not installed");
            _certState.ForeColor = installed ? Ui.Good : Ui.Warn;

            byte[] bytes;
            string source;
            CertResult load = PublisherCert.LoadPackage(out bytes, out source);
            string text = Lang.T("主体：", "Subject: ") + PublisherCert.ExpectedSubject;
            text += Lang.T("\n指纹：", "\nThumbprint: ") + PublisherCert.ExpectedThumbprint;
            text += "\nSHA-256：" + PublisherCert.ExpectedSha256;
            text += Lang.T("\n证书来源：", "\nCertificate source: ") + (load.Ok ? source : Lang.T("不可用（", "unavailable (") + load.Message + "）");
            if (installed && found != null)
            {
                try
                {
                    text += Lang.T("\n有效期至：", "\nValid until: ") + found.NotAfter.ToString("yyyy-MM-dd");
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
                MessageBox.Show(this, Lang.T("发布者签名已经安装过了。", "The publisher signature is already installed."), Lang.T("发布者签名", "Publisher signature"),
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
                MessageBox.Show(this, result.Message, Lang.T("发布者签名", "Publisher signature"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result != null && !result.Ok)
            {
                InstallLog.Write("发布者签名：设置页补装失败 → " + result.Detail);
                MessageBox.Show(this, result.Message, Lang.T("发布者签名", "Publisher signature"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, Lang.T("当前用户的受信任发布者里没有这个签名。", "This signature is not in the current user's trusted publishers."), Lang.T("发布者签名", "Publisher signature"),
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshCert();
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                Lang.T("要从当前用户的受信任发布者里移除「", "Remove the current user's trusted publisher \"") + PublisherCert.PublisherName + Lang.T("」签名吗？\n\n", "\"?\n\n") +
                Lang.T("移除后 Windows 可能重新显示「未知发布者」的安全提示；程序本身的功能不受影响，之后也可以随时补装。", "Windows may show unknown-publisher warnings again; app features are unaffected and you can reinstall it anytime."),
                Lang.T("移除发布者签名 · 请确认", "Remove publisher signature · confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                InstallLog.Write("发布者签名：设置页移除被取消；指纹 " + PublisherCert.ExpectedThumbprint);
                return;
            }
            CertResult result = PublisherCert.Remove();
            InstallLog.Write("发布者签名：设置页移除" + (result.Ok ? "成功" : "失败") + " → " + result.Detail);
            MessageBox.Show(this, result.Message, Lang.T("发布者签名", "Publisher signature"),
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
                MessageBox.Show(this, Lang.T("这个目录不是有效的战雷安装目录。", "This is not a valid War Thunder install folder."), Lang.T("无法保存", "Cannot save"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool zoomChanged = _zoom != Config.ZoomPercent;
            bool themeChanged = _theme != Config.ThemeIndex;

            Config.GameDir = dir;
            Config.Backup = _backupRow.Checked;
            Config.KeepBackups = _keepValue;
            Config.MissingFolderPolicy = _missingPolicy;
            Config.ThemeIndex = _theme;
            Config.ZoomPercent = _zoom;
            Config.Animations = false;
            Config.Save();
            Ui.Animations = false;

            if (zoomChanged || themeChanged)
            {
                bool restart;
                using (RestartChoiceDialog dialog = new RestartChoiceDialog(
                    Lang.T("界面设置已保存，主题和缩放需要重启程序才能完全生效。", "Settings saved. Theme and zoom take full effect after restarting.")))
                {
                    restart = dialog.ShowDialog(this) == DialogResult.OK;
                }
                if (restart)
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
            bool updateStart = _startPage != 0;
            if (updateStart)
            {
                int page = _startPage;
                _startPage = 0;
                // 等窗口真正显示完再切页：效果与用户点击左侧分页一致。
                BeginInvoke((MethodInvoker)delegate { SelectPage(page); });
            }
            if (!updateStart)
            {
                // 保持原行为：普通打开设置页时聚焦游戏目录输入框。
                _dirBox.Select(0, 0);
            }
        }
    }

    /// <summary>安装目录写不进去时的三选一：换默认目录 / 管理员重试 / 取消。</summary>
    internal class InstallFailForm : ModalForm
    {
        public int Choice; // 1=默认目录 2=管理员重试 0=取消

        public InstallFailForm(string path, string reason)
            : base(Lang.T("目录不可写", "Folder not writable"), 660, 400)
        {
            MakeLabel(Lang.T("这个目录写不进去", "Cannot write to this folder"), 24, 14, 300, 24, Ui.H1, Ui.Warn);
            MakeLabel(path, 24, 46, 610, 22, Ui.Mono, Ui.Text);

            Label reasonLabel = MakeLabel(Lang.T("原因：", "Reason: ") + reason, 24, 76, 610, 40, Ui.Small, Ui.TextDim);
            reasonLabel.AutoEllipsis = true;

            MakeLabel(Lang.T("可能的原因：", "Possible reasons:"), 24, 126, 300, 20, Ui.SmallBold, Ui.TextDim);
            MakeLabel(Lang.T("· 桌面 / 文档等文件夹被安全软件（火绒、360 等）保护，未知程序不能写入\n", "· Desktop/Documents are protected by security software and block unknown programs\n") +
                      Lang.T("· 该文件夹的 Windows 权限不允许当前用户修改", "· Windows permissions do not allow the current user to modify this folder"),
                      24, 148, 610, 44, Ui.Small, Ui.TextFaint);

            MakeLabel(Lang.T("建议改用默认目录，或者自己在 D 盘里新建一个普通文件夹。", "Use the default folder, or create a normal folder on another drive."),
                      24, 200, 610, 20, Ui.Small, Ui.Accent);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 388, 288, 84, false);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton admin = MakeButton(Lang.T("以管理员身份重试", "Retry as admin"), 480, 288, 154, false);
            admin.Click += delegate { Choice = 2; DialogResult = DialogResult.OK; Close(); };
            FlatButton useDefault = MakeButton(Lang.T("改用默认目录", "Use default folder"), 24, 288, 150, true);
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
            : base(Lang.T("选择载具", "Select vehicle"), 660, 640)
        {
            MakeLabel(Lang.T("这台涂装属于哪个载具？", "Which vehicle is this skin for?"), 24, 12, 400, 24, Ui.H1, Ui.Text);
            MakeLabel(Lang.T("搜索中文名、英文名或载具 ID，也可以直接在下面输入 ID。", "Search by Chinese/English name or vehicle ID, or enter an ID below."), 24, 38, 600, 22, Ui.Small, Ui.TextDim);

            TextBox searchField;
            MakeField(24, 68, 610, 38, out searchField);
            _search = searchField;
            Native.SetCue(_search, Lang.T("搜索载具，例如：直-10ME / Z-10ME / z_10me", "Search vehicles, e.g. Z-10ME / z_10me"));

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

            MakeLabel(Lang.T("手动输入载具 ID（列表里找不到时用）", "Enter vehicle ID manually (if not in the list)"), 24, 518, 300, 20, Ui.SmallBold, Ui.TextDim);
            TextBox manualField;
            MakeField(24, 540, 300, 36, out manualField);
            _manual = manualField;
            MakeLabel(Lang.T("ID 就是 .blk 文件的名字，例如 z_10me", "The ID is the .blk filename, e.g. z_10me"), 336, 548, 300, 20, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 402, 540, 92, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton(Lang.T("使用所选载具", "Use selected vehicle"), 502, 540, 132, true);
            ok.Click += delegate { Accept(); };

            _search.TextChanged += delegate { RefreshList(_search.Text); };
            RefreshList("");
            Shown += delegate { Native.ThemeScrollbars(this); };
            Native.SetCue(_manual, string.IsNullOrEmpty(suggestedId) ? Lang.T("例如 z_10me", "e.g. z_10me") : Lang.T("例如 ", "e.g. ") + suggestedId);
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
                MessageBox.Show(this, Lang.T("请先选择一个载具，或者在下面输入载具 ID。", "Select a vehicle or enter a vehicle ID below."), Lang.T("还没有选择", "No selection"),
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
            : base(Lang.T("还没有载具文件夹", "Missing vehicle folders"), 640, 470)
        {
            _missing = new List<DetectedUnit>(missing);
            _hint = MakeLabel(Lang.T("下面这些载具在 UserSkins 里还没有文件夹，游戏不会读取它们的涂装。", "These vehicles have no folder under UserSkins, so the game cannot read their skins."),
                     24, 14, 592, 24, Ui.Body, Ui.Text);
            MakeLabel(Lang.T("先在游戏里打开该载具的「外观 → 涂装」，点一次「创建涂装示例」，游戏会生成 template_<载具ID> 文件夹；然后回来点「重新检查」。", "In game, open Appearance → Skins for this vehicle and create a skin sample so the game creates template_<vehicle ID>; then click Recheck."),
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
                Ui.DrawText(e.Graphics, unit.UnitId + Lang.T("  ·  需要一个 template_", "  ·  needs a template_") + unit.UnitId + Lang.T(" 或包含 ", " or a folder containing ") + unit.UnitId + Lang.T(".blk 的文件夹", ".blk"),
                        Ui.Small, Ui.TextFaint,
                        new Rectangle(r.X, r.Y + Ui.S(22), r.Width, Ui.S(18)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            Content.Controls.Add(_list);
            RefreshItems();
            Shown += delegate { Native.ThemeScrollbars(this); };

            MakeLabel(Lang.T("提示：在游戏里创建模板后不需要重启游戏，回到这里点重新检查即可。", "Creating the template in game does not require a restart; return here and click Recheck."),
                     24, 340, 592, 20, Ui.Small, Ui.TextFaint);

            FlatButton open = MakeButton(Lang.T("打开 UserSkins", "Open UserSkins"), 24, 380, 130, false);
            open.Click += delegate
            {
                string dir = Config.UserSkinsDir;
                if (Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                }
            };
            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 408, 380, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton recheck = MakeButton(Lang.T("重新检查", "Recheck"), 514, 380, 102, true);
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
                    _hint.Text = Lang.T("还有 ", "Still ") + _missing.Count + Lang.T(" 个载具没有文件夹，请先在游戏里创建模板。", " vehicles have no folder. Create templates in game first.");
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

    /// <summary>缺少 template_&lt;代号&gt; 目录时的创建确认窗口。</summary>
    internal class CreateFoldersForm : ModalForm
    {
        public bool Created;
        private readonly List<DetectedUnit> _units;
        private readonly bool _simple;
        private readonly ListBox _list;
        private readonly Label _hint;
        private readonly FlatButton _expand;
        private readonly Label _bottomHint;
        private FlatButton _cancel;
        private FlatButton _create;

        public CreateFoldersForm(List<DetectedUnit> units, bool simple)
            : base(Lang.T("创建涂装目录", "Create skin folder"), 640, 500)
        {
            _units = new List<DetectedUnit>(units);
            _simple = simple;

            string path = Config.UserSkinsDir;
            string first = _units.Count > 0 ? _units[0].Title : "";
            _hint = MakeLabel(simple
                    ? Lang.T("载具「", "Vehicle \"") + first + Lang.T("」缺少涂装目录，是否创建？", "\" is missing a skin folder. Create it?")
                    : Lang.T("检测到 ", "Detected ") + _units.Count + Lang.T(" 台载具缺少涂装目录，是否创建？", " vehicles missing skin folders. Create them?"),
                24, 14, 592, 26, Ui.Body, Ui.Text);
            MakeLabel(simple
                    ? Lang.T("只会创建一个空目录，不写任何文件；取消不会改动 UserSkins。", "Only an empty folder is created; cancel leaves UserSkins untouched.")
                    : Lang.T("将创建以下空目录（与现有涂装同级），不会覆盖或删除任何已有内容。", "These empty folders will be created next to existing skins; nothing is overwritten or deleted."),
                24, 42, 592, 22, Ui.Small, Ui.TextDim);

            int listTop = 72;
            if (!simple)
            {
                MakeLabel(Lang.T("目标位置：", "Target: ") + path, 24, 66, 592, 22, Ui.Small, Ui.TextFaint);
                listTop = 94;
            }

            _list = new ListBox();
            _list.Left = Ui.S(24);
            _list.Top = Ui.S(listTop);
            _list.Width = Ui.S(592);
            _list.Height = Ui.S(258);
            _list.BackColor = Ui.BgSoft;
            _list.ForeColor = Ui.Text;
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.S(40);
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
                        new Rectangle(r.X, r.Y + Ui.S(3), r.Width, Ui.S(19)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                string dir = TemplateFolders.TargetName(unit.UnitId);
                Ui.DrawText(e.Graphics, dir, Ui.Small, Ui.TextFaint,
                        new Rectangle(r.X, r.Y + Ui.S(20), r.Width, Ui.S(17)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            Content.Controls.Add(_list);

            _expand = MakeButton(Lang.T("展开全部", "Show all"), 24, listTop + 266, 110, false);
            _expand.Click += delegate
            {
                for (int i = _list.Items.Count; i < _units.Count; i++)
                {
                    _list.Items.Add(_units[i]);
                }
                _expand.Visible = false;
            };
            for (int i = 0; i < _units.Count && i < 10; i++)
            {
                _list.Items.Add(_units[i]);
            }
            _expand.Visible = _units.Count > 10;

            _bottomHint = MakeLabel(Lang.T("创建目录后，需要在游戏内重新打开该载具的涂装界面才会出现。", "Reopen the vehicle's skin menu in game for the folder to appear."),
                      24, 372, 592, 20, Ui.Small, Ui.TextDim);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 398, 394, 96, false);
            _cancel = cancel;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton create = MakeButton(Lang.T("创建", "Create"), 504, 394, 112, true);
            _create = create;
            create.Click += delegate
            {
                Created = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Shown += delegate
            {
                Native.ThemeScrollbars(this);
                // 底部控件按 Content 实际高度倒排，避免 DPI/缩放后按钮被裁切或互相重叠。
                int buttonH = Ui.S(38);
                int buttonY = Math.Max(Ui.S(220), Content.Height - Ui.S(18) - buttonH);
                _create.SetBounds(Content.Width - Ui.S(24) - Ui.S(112), buttonY, Ui.S(112), buttonH);
                _cancel.SetBounds(_create.Left - Ui.S(12) - Ui.S(96), buttonY, Ui.S(96), buttonH);
                _bottomHint.Top = buttonY - Ui.S(26);
                _bottomHint.Left = Ui.S(24);
                _bottomHint.Width = Math.Max(Ui.S(120), Content.Width - Ui.S(48));
                int expandY = _bottomHint.Top - Ui.S(40);
                _expand.Top = expandY;
                int listH = Math.Max(Ui.S(80), expandY - _list.Top - Ui.S(8));
                _list.Height = listH;
                cancel.Focus();
            };
        }
    }

    internal class ImportPlanForm : ModalForm
    {
        private readonly ImportPlan _plan;
        private readonly CheckRow _backupRow;
        private readonly ListBox _list;
        private readonly TextBox _nameBox;
        private readonly Label _errorLabel;
        private readonly Label _pathLabel;
        private readonly Label _conflictStatus;
        private readonly FlatButton _ok;
        private readonly FlatButton _resetName;
        private readonly FlatButton _choiceRename;
        private readonly FlatButton _choiceOverwrite;
        private readonly FlatButton _choiceSkip;
        private readonly Dictionary<ImportTarget, string> _effective = new Dictionary<ImportTarget, string>();
        private readonly string _defaultName = "";
        private SkinConflictPolicy _policy = SkinConflictPolicy.Rename;
        private bool _updating;
        public bool Confirmed;

        public ImportPlanForm(ImportPlan plan)
            : base(Lang.T("确认导入", "Confirm import"), 760, 700)
        {
            _plan = plan;

            string defaultName = "";
            if (plan.Targets.Count > 0 && plan.Targets[0].Package != null &&
                plan.Targets[0].Package.TopFolderName != null)
            {
                defaultName = plan.Targets[0].Package.TopFolderName;
            }
            _defaultName = defaultName;
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                plan.Targets[i].SkinName = defaultName;
            }

            string warnings = "";
            for (int i = 0; i < plan.Warnings.Count && i < 3; i++)
            {
                warnings += "· " + plan.Warnings[i] + Environment.NewLine;
            }
            if (plan.Warnings.Count > 3)
            {
                warnings += Lang.T("· 还有 ", "· ") + (plan.Warnings.Count - 3) + Lang.T(" 条提示", " more notices");
            }

            int listHeight = Math.Min(240, Math.Max(56, plan.Targets.Count * 52));
            int listTop = 228;
            int optionsTop = listTop + listHeight + 10;
            int warnTop = optionsTop + 50;
            int warnHeight = warnings.Length > 0 ? 72 : 0;
            int buttonsTop = warnTop + warnHeight + 10;
            ResizeToDesign(760, 58 + buttonsTop + 38 + 20);

            MakeLabel(plan.Targets.Count.ToString(CultureInfo.InvariantCulture) + Lang.T(" 个载具 · 写入 ", " vehicles · write ") +
                      plan.Targets.Count.ToString(CultureInfo.InvariantCulture) + Lang.T(" 个涂装子文件夹 · 约 ", " skin subfolders · about ") +
                      Util.HumanSize(plan.TotalBytes),
                      24, 10, 500, 24, Ui.H1, Ui.Text);
            MakeLabel(Lang.T("新涂装会写进 UserSkins 下对应载具的 template 文件夹里的子文件夹，不会清空模板文件夹里已有的内容。", "New skins are written into subfolders of each vehicle's template folder and do not clear existing content."),
                      24, 36, 712, 20, Ui.Small, Ui.TextDim);

            MakeLabel(Lang.T("涂装文件夹名", "Skin folder name"), 24, 62, 200, 20, Ui.SmallBold, Ui.TextDim);
            TextBox nameBox;
            MakeField(24, 84, 400, 34, out nameBox);
            _nameBox = nameBox;
            _nameBox.Text = defaultName;

            _resetName = MakeButton(Lang.T("使用原文件夹名", "Use original name"), 432, 84, 132, false);
            _resetName.Height = Ui.S(34);
            _resetName.Font = Ui.SmallBold;

            _errorLabel = MakeLabel("", 24, 120, 712, 18, Ui.Small, Ui.TextFaint);
            _errorLabel.AutoEllipsis = true;
            _pathLabel = MakeLabel("", 24, 140, 712, 20, Ui.Small, Ui.TextFaint);
            _pathLabel.AutoEllipsis = true;

            MakeLabel(Lang.T("同名涂装文件夹已存在时：", "If a skin folder already exists:"), 24, 166, 280, 20, Ui.SmallBold, Ui.TextDim);
            _choiceRename = MakeChoice(Lang.T("改名", "Rename"), 24, 188);
            _choiceOverwrite = MakeChoice(Lang.T("覆盖", "Overwrite"), 114, 188);
            _choiceSkip = MakeChoice(Lang.T("跳过", "Skip"), 204, 188);
            _conflictStatus = MakeLabel("", 306, 190, 430, 20, Ui.Small, Ui.TextFaint);
            _conflictStatus.AutoEllipsis = true;

            _list = new ListBox();
            _list.Left = Ui.S(24);
            _list.Top = Ui.S(listTop);
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

            _backupRow = new CheckRow();
            _backupRow.Title = Lang.T("覆盖前备份旧涂装", "Back up old skin before overwrite");
            _backupRow.Subtitle = Lang.T("复制到安装目录 backups 下，按 载具文件夹 / 涂装名 保留两级结构", "Copied to backups in the install folder, keeping vehicle/skin folders");
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

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 520, buttonsTop, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _ok = MakeButton(Lang.T("开始导入", "Start import"), 626, buttonsTop, 110, true);

            _nameBox.TextChanged += delegate
            {
                if (_updating)
                {
                    return;
                }
                ApplyName(_nameBox.Text);
                RefreshState();
            };
            _resetName.Click += delegate
            {
                _updating = true;
                _nameBox.Text = _defaultName;
                _updating = false;
                ApplyName(_defaultName);
                RefreshState();
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
            _choiceRename.Click += delegate { SelectPolicy(SkinConflictPolicy.Rename); };
            _choiceOverwrite.Click += delegate { SelectPolicy(SkinConflictPolicy.Overwrite); };
            _choiceSkip.Click += delegate { SelectPolicy(SkinConflictPolicy.Skip); };
            _backupRow.Click += delegate
            {
                UpdateChoiceAvailability();
                RefreshState();
            };
            _ok.Click += delegate { Confirm(); };

            UpdateChoiceAvailability();
            ApplyName(defaultName);
            RefreshState();

            Shown += delegate
            {
                Native.ThemeScrollbars(this);
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }

        private FlatButton MakeChoice(string text, int x, int y)
        {
            FlatButton button = MakeButton(text, x, y, 82, false);
            button.Height = Ui.S(32);
            button.Font = Ui.SmallBold;
            return button;
        }

        private void ApplyName(string name)
        {
            for (int i = 0; i < _plan.Targets.Count; i++)
            {
                _plan.Targets[i].SkinName = name != null ? name : "";
            }
        }

        private void SelectPolicy(SkinConflictPolicy policy)
        {
            if (policy == SkinConflictPolicy.Overwrite && !_backupRow.Checked)
            {
                return;
            }
            _policy = policy;
            StyleChoices();
            RefreshState();
        }

        private void UpdateChoiceAvailability()
        {
            _choiceOverwrite.Enabled = _backupRow.Checked;
            if (!_backupRow.Checked && _policy == SkinConflictPolicy.Overwrite)
            {
                _policy = SkinConflictPolicy.Rename;
            }
            StyleChoices();
        }

        private void StyleChoices()
        {
            _choiceRename.Primary = _policy == SkinConflictPolicy.Rename;
            _choiceOverwrite.Primary = _policy == SkinConflictPolicy.Overwrite;
            _choiceSkip.Primary = _policy == SkinConflictPolicy.Skip;
        }

        private void RefreshState()
        {
            if (_updating)
            {
                return;
            }
            string error = SkinNameRules.Validate(_nameBox.Text);
            _effective.Clear();
            int conflicts = 0;
            if (error.Length == 0)
            {
                Dictionary<string, HashSet<string>> usedByTemplate =
                    new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _plan.Targets.Count; i++)
                {
                    ImportTarget target = _plan.Targets[i];
                    string dir;
                    if (!Importer.TryTargetDir(target, out dir))
                    {
                        if (error.Length == 0)
                        {
                            error = Lang.T("目标路径不安全，请检查载具文件夹和涂装文件夹名", "Unsafe target path; check the vehicle and skin folder names");
                        }
                        _effective[target] = target.SkinName;
                        continue;
                    }
                    bool exists = Directory.Exists(dir) || File.Exists(dir);
                    string effective = target.SkinName;
                    HashSet<string> used;
                    if (!usedByTemplate.TryGetValue(target.Folder, out used))
                    {
                        used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        usedByTemplate[target.Folder] = used;
                    }
                    if (exists)
                    {
                        conflicts++;
                        if (_policy == SkinConflictPolicy.Rename)
                        {
                            effective = Importer.UniqueSkinName(target.Folder, target.SkinName, used);
                        }
                    }
                    used.Add(effective);
                    _effective[target] = effective;
                }
            }

            if (error.Length > 0)
            {
                _errorLabel.Text = "✗ " + error;
                _errorLabel.ForeColor = Ui.Bad;
            }
            else
            {
                _errorLabel.Text = Lang.T("✓ 名称可用，会直接作为子文件夹名。", "✓ Name is valid and will be used as the subfolder name.");
                _errorLabel.ForeColor = Ui.TextFaint;
            }
            _ok.Enabled = error.Length == 0;

            _pathLabel.Text = BuildPathPreview(error.Length == 0);
            UpdateConflictStatus(conflicts);
            _list.Invalidate();
        }

        private string BuildPathPreview(bool valid)
        {
            if (_plan.Targets.Count == 0)
            {
                return "";
            }
            ImportTarget first = _plan.Targets[0];
            string templateName;
            try
            {
                templateName = Path.GetFileName(first.Folder.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch
            {
                templateName = Lang.T("template_<载具ID>", "template_<vehicle ID>");
            }
            string name = _nameBox.Text;
            string effective;
            if (_effective.TryGetValue(first, out effective) && effective.Length > 0)
            {
                name = effective;
            }
            string sep = Path.DirectorySeparatorChar.ToString();
            string path = "UserSkins" + sep + templateName + sep +
                          (valid && name.Length > 0 ? name : Lang.T("<涂装文件夹名>", "<skin folder name>"));
            string text = Lang.T("写入预览：", "Write preview: ") + path;
            if (_plan.Targets.Count > 1)
            {
                text += Lang.T("     （其余 ", "     (the other ") + (_plan.Targets.Count - 1) + Lang.T(" 个载具写入各自的 template 文件夹）", " vehicles write to their own template folders)");
            }
            return text;
        }

        private void UpdateConflictStatus(int conflicts)
        {
            if (conflicts == 0)
            {
                _conflictStatus.Text = Lang.T("当前没有同名涂装文件夹。", "No conflicting skin folder.");
                _conflictStatus.ForeColor = Ui.TextFaint;
                return;
            }
            string rule;
            if (_policy == SkinConflictPolicy.Rename)
            {
                rule = Lang.T("自动改名后写入", "write with auto-renamed folder");
            }
            else if (_policy == SkinConflictPolicy.Overwrite)
            {
                rule = Lang.T("先备份旧涂装再覆盖", "back up and overwrite");
            }
            else
            {
                rule = Lang.T("跳过，不改动", "skip without changes");
            }
            _conflictStatus.Text = Lang.T("有 ", "There are ") + conflicts.ToString(CultureInfo.InvariantCulture) +
                                   Lang.T(" 个同名涂装文件夹：", " conflicting skin folders: ") + rule + "。";
            _conflictStatus.ForeColor = Ui.Warn;
        }

        private void Confirm()
        {
            string error = SkinNameRules.Validate(_nameBox.Text);
            if (error.Length > 0)
            {
                RefreshState();
                _nameBox.Focus();
                return;
            }
            for (int i = 0; i < _plan.Targets.Count; i++)
            {
                ImportTarget target = _plan.Targets[i];
                string effective;
                if (_effective.TryGetValue(target, out effective) && effective.Length > 0)
                {
                    target.SkinName = effective;
                }
                string nameError = SkinNameRules.Validate(target.SkinName);
                if (nameError.Length > 0)
                {
                    _errorLabel.Text = "✗ " + nameError;
                    _errorLabel.ForeColor = Ui.Bad;
                    _ok.Enabled = false;
                    return;
                }
            }
            _plan.Conflict = _policy;
            Config.Backup = _backupRow.Checked;
            Config.Save();
            _plan.TotalBytes = Importer.EstimateBytes(_plan);
            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
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

            string templateName = Path.GetFileName(target.Folder.TrimEnd(Path.DirectorySeparatorChar));
            string effective;
            if (!_effective.TryGetValue(target, out effective) || effective.Length == 0)
            {
                effective = _nameBox.Text;
            }
            bool conflict = false;
            string dir;
            if (effective.Length > 0 && Importer.TryTargetDir(target, out dir))
            {
                conflict = Directory.Exists(dir) || File.Exists(dir);
            }
            string sep = Path.DirectorySeparatorChar.ToString();
            string line = "→ " + templateName + sep + (effective.Length > 0 ? effective : Lang.T("<涂装文件夹名>", "<skin folder name>"));
            Ui.DrawText(g, line, Ui.Small, conflict ? Ui.Warn : Ui.Good,
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
            : base(Lang.T("正在导入涂装", "Importing skins"), 580, 276)
        {
            _currentLabel = MakeLabel(Lang.T("准备中…", "Preparing…"), 24, 14, 520, 24, Ui.Body, Ui.Text);
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

            MakeLabel(Lang.T("导入过程中请不要关闭游戏目录里的文件夹。", "Do not close folders in the game directory during import."), 24, 118, 500, 20, Ui.Small, Ui.TextFaint);
            _cancel = MakeButton(Lang.T("取消", "Cancel"), 434, 150, 104, false);
            _cancel.Click += delegate
            {
                _cancelRequested = true;
                _cancel.Enabled = false;
                _cancel.Text = Lang.T("正在停止…", "Stopping…");
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
                    result.Failed.Add(Lang.T("导入失败：", "Import failed: ") + ex.Message);
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
                                         Util.HumanSize(done) + Lang.T(" / ", " / ") + Util.HumanSize(total);
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

            FlatButton open = MakeButton(Lang.T("打开 UserSkins", "Open UserSkins"), 24, buttonsTop, 150, false);
            open.Click += delegate
            {
                string dir = Config.UserSkinsDir;
                if (Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                }
            };
            FlatButton close = MakeButton(Lang.T("完成", "Done"), 486, buttonsTop, 108, true);
            close.Click += delegate { DialogResult = DialogResult.OK; Close(); };
        }
    }
}
