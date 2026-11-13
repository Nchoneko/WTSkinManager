using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>左侧的涂装包卡片：卡片 12 圆角、1px 描边，选中态用 Accent 描边。</summary>
    internal class PackageCard : Control
    {
        public SkinPackage Package;
        public bool Selected;
        public event EventHandler RemoveClicked;

        private readonly Tween _hoverT;
        private bool _closeHover;
        private bool _down;

        public PackageCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Height = Ui.S(76);
            Cursor = Cursors.Hand;
            BackColor = Ui.Page;
            TabStop = true;
            _hoverT = new Tween(this, 0f);
        }

        private Rectangle CloseRect()
        {
            int size = Ui.S(26);
            return new Rectangle(Width - size - Ui.Sp12, Ui.S(10), size, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            float t = _hoverT.Value;
            Color baseFill = Selected ? Ui.CardSel : Ui.CardBg;
            Color fill = Ui.Mix(baseFill, Selected ? Ui.PanelActive : Ui.PanelHover, t);
            if (_down)
            {
                fill = Ui.Mix(fill, Ui.Page, 0.22f);
            }
            Color border = Selected ? Ui.Accent : Ui.Mix(Ui.Stroke, Ui.TextWeak, t * 0.35f);
            Ui.FillRound(g, r, Ui.RCard, fill);
            Ui.DrawRound(g, r, Ui.RCard, border, Ui.BorderWidth);

            int badge = Ui.S(40);
            Rectangle badgeRect = new Rectangle(Ui.Sp16, (Height - badge) / 2, badge, badge);
            Color badgeColor = Ui.PanelActive;
            if (Package != null && Package.Ready)
            {
                badgeColor = Package.Units.Count > 0 ? Ui.T.BadgeGoodBg : Ui.T.BadgeWarnBg;
            }
            else if (Package != null && Package.Error.Length > 0)
            {
                badgeColor = Ui.T.BadgeBadBg;
            }
            Ui.FillRound(g, badgeRect, Ui.RButton, badgeColor);

            string badgeText = "?";
            Color badgeTextColor = Ui.TextSecondary;
            if (Package != null)
            {
                if (Package.Error.Length > 0)
                {
                    badgeText = "!";
                    badgeTextColor = Ui.Bad;
                }
                else if (Package.Units.Count > 0)
                {
                    badgeText = Package.Units.Count.ToString(CultureInfo.InvariantCulture);
                    badgeTextColor = Ui.TextPrimary;
                }
                else
                {
                    badgeText = "?";
                    badgeTextColor = Ui.Warn;
                }
            }
            Ui.DrawText(g, badgeText, Ui.H1, badgeTextColor, badgeRect, Ui.Center);

            int textX = badgeRect.Right + Ui.Sp12;
            int rightReserve = Ui.S(88);
            Rectangle nameRect = new Rectangle(textX, Ui.Sp12, Math.Max(Ui.S(40), Width - textX - rightReserve), Ui.S(22));
            string name = Package == null ? "" : Package.Name;
            Ui.DrawText(g, name, Ui.BodyBold, Ui.TextPrimary, nameRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            string meta;
            Color metaColor = Ui.TextWeak;
            if (Package == null)
            {
                meta = "";
            }
            else if (Package.Error.Length > 0)
            {
                meta = Package.Error;
                metaColor = Ui.Bad;
            }
            else
            {
                meta = Package.FilesText + "  ·  " + Package.SizeText;
                if (Package.Units.Count == 0)
                {
                    meta += "  ·  需要手动指定载具";
                    metaColor = Ui.Warn;
                }
                else if (Package.UnknownIds.Count > 0)
                {
                    meta += "  ·  " + Package.UnknownIds.Count + " 个未识别";
                    metaColor = Ui.TextSecondary;
                }
            }
            Rectangle metaRect = new Rectangle(textX, Ui.S(38), Math.Max(Ui.S(40), Width - textX - rightReserve), Ui.S(20));
            Ui.DrawText(g, meta, Ui.Small, metaColor, metaRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            Rectangle close = CloseRect();
            if (_closeHover)
            {
                Ui.FillRound(g, close, Ui.RButton, Ui.T.CloseHoverBg);
            }
            using (Pen pen = new Pen(_closeHover ? Ui.T.CloseHoverFg : Ui.TextWeak, Math.Max(1.3f, Ui.Scale * 1.4f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int pad = Ui.S(8);
                g.DrawLine(pen, close.Left + pad, close.Top + pad, close.Right - pad, close.Bottom - pad);
                g.DrawLine(pen, close.Right - pad, close.Top + pad, close.Left + pad, close.Bottom - pad);
            }

            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.RCard, Ui.TextSecondary);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool close = CloseRect().Contains(e.Location);
            if (close != _closeHover)
            {
                _closeHover = close;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hoverT.To(1f, Tok.MotionMs);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _closeHover = false;
            _down = false;
            _hoverT.To(0f, Tok.MotionMs);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _down = true;
            Focus();
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && CloseRect().Contains(e.Location))
            {
                if (RemoveClicked != null)
                {
                    RemoveClicked(this, EventArgs.Empty);
                }
                return;
            }
            base.OnMouseClick(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Space || key == Keys.Enter)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hoverT.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal class MainForm : Form
    {
        private readonly List<SkinPackage> _packages = new List<SkinPackage>();
        private readonly string[] _initialPaths;
        private SkinPackage _selected;
        private Panel _body;
        private Panel _header;
        private Panel _footer;
        private DropZone _drop;
        private FlowLayoutPanel _listPanel;
        private Panel _detail;
        private Panel _vehPanel;
        private ScrollCover _scrollCover;
        private PageTabs _tabs;
        private Panel _managerHost;
        private ManagerForm _manager;
        private int _page;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint winIni);
        private DetailCard _detailCard;
        private string _detailNameText = "";
        private string _detailMetaText = "";
        private Color _detailMetaColor = Ui.TextDim;
        private string _detailSectionText = "";
        private string _detailHintText = "";
        private Color _detailHintColor = Ui.TextFaint;
        private bool _detailRefreshVisible;
        private bool _detailManualVisible;
        private bool _detailGuide = true;
        private float _guideT = 1f;
        private float _guideFrom;
        private bool _guideAnimating;
        private Timer _guideTimer;
        private DateTime _guideStart;
        private FlatButton _manualButton;
        private FlatButton _refreshButton;
        private Label _status;
        private FlatButton _importButton;
        private Bitmap _logo;

        private bool _dragging;
        private Rectangle _versionRect;
        private Rectangle _githubRect;
        private Bitmap _githubMark;
        private bool _githubHover;
        private SiteButton _liveButton;
        private SiteButton _wtButton;
        private FlatButton _settingsButton;
        private PathChip _pathChip;
        private BuildInfoTip _buildTip;
        private Point _dragOffset;
        private int _winHover = -1;
        private Rectangle _normalBounds;
        private bool _maximizedByUser;
        private Control _edgeLeft;
        private Control _edgeRight;
        private Control _edgeTop;
        private Control _edgeBottom;
        private Control _cornerTL;
        private Control _cornerTR;
        private Control _cornerBL;
        private Control _cornerBR;
        private Panel _animLayer;
        private Bitmap _animBitmap;
        private Control _animShown;
        private Timer _animTimer;
        private double _animAlpha;
        private int _animOffset;
        private bool _openManagerPage;
        private UpdateBanner _updateBanner;
        private SemVer _bannerVersion;
        private bool _startupCheckStarted;

        public MainForm(string[] initialPaths)
        {
            _initialPaths = initialPaths;
            if (initialPaths != null && Array.IndexOf(initialPaths, "--manager") >= 0)
            {
                _openManagerPage = true;
            }
            Text = "战雷涂装管理器";
            FormBorderStyle = FormBorderStyle.None;
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            int windowWidth = Math.Min(Ui.S(1180), (int)(workArea.Width * 0.95));
            int windowHeight = Math.Min(Ui.S(780), (int)(workArea.Height * 0.95));
            ClientSize = new Size(windowWidth, windowHeight);
            MinimumSize = new Size(Math.Min(Ui.S(850), windowWidth), Math.Min(Ui.S(620), windowHeight));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Bg;
            Font = Ui.Body;
            AllowDrop = true;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.M)
                {
                    SwitchPage(1);
                }
            };
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;

            BuildHeader();
            BuildFooter();
            BuildBody();
            BuildPages();
            BuildUpdateBanner();
            BuildResizeEdges();
            BuildAnimationLayer();

            DragEnter += OnDragEnter;
            DragOver += OnDragEnter;
            DragLeave += delegate { _drop.Hot = false; };
            DragDrop += OnDragDrop;
            Resize += delegate { LayoutChrome(); };
            Shown += delegate
            {
                LayoutChrome();
                Native.ThemeScrollbars(this);
                // 主窗口不做透明度淡入：Opacity 会引入 WS_EX_LAYERED，无边框拖动会产生拖影。
                // 页面切换的滑入动画不受影响。
                BeginInvoke((MethodInvoker)delegate
                {
                    Native.BringToFrontOnce(this);
                    // 启动时不把焦点停在任何按钮上：避免次级按钮出现焦点描边、看起来像主操作。
                    ActiveControl = null;
                    // 首屏渲染完成后再静默检查更新：后台线程请求，不阻塞窗口显示。
                    BeginStartupUpdateCheck();
                });
                if (_initialPaths != null && _initialPaths.Length > 0)
                {
                    List<string> paths = new List<string>();
                    for (int i = 0; i < _initialPaths.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(_initialPaths[i]) &&
                            !_initialPaths[i].StartsWith("--", StringComparison.Ordinal))
                        {
                            paths.Add(_initialPaths[i]);
                        }
                    }
                    if (paths.Count > 0)
                    {
                        AddPaths(paths.ToArray());
                    }
                }
                if (_openManagerPage)
                {
                    SwitchPage(1);
                }
            };
            LayoutChrome();
            UpdateStatus();
            UpdateDetail();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(this);
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    Icon = icon;
                }
            }
            catch
            {
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                // 合成整个窗口后再呈现：拖动边角连续缩放时，子控件异步重绘
                // 会在旧画面上留下条状残影。恢复最小化后需要重建合成表面。
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }
            LayoutChrome();
            Invalidate(true);
        }

        private bool _wasMinimized;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // 尺寸/状态变化统一走同一条同步链：先记日志，再重排并整窗重绘。
            // 最小化恢复不一定会触发常规 Resize，因此 WM_SIZE / WM_ACTIVATE 都要补一次。
            if (m.Msg == 0x0005) // WM_SIZE
            {
                LogLayout("WM_SIZE");
                if (WindowState == FormWindowState.Minimized)
                {
                    _wasMinimized = true;
                }
                else
                {
                    if (_wasMinimized)
                    {
                        _wasMinimized = false;
                        ResetComposition();
                    }
                    LayoutChrome();
                    Invalidate(true);
                }
            }
            else if (m.Msg == 0x0047) // WM_WINDOWPOSCHANGED
            {
                LogLayout("WM_WINDOWPOSCHANGED");
            }
            else if (m.Msg == 0x0006) // WM_ACTIVATE
            {
                int active = m.WParam.ToInt32() & 0xFFFF;
                if (active != 0)
                {
                    LogLayout("WM_ACTIVATE");
                    BeginInvoke((MethodInvoker)delegate
                    {
                        // 自定义最大化没有走 WindowState，最小化恢复后窗口可能回到普通尺寸，
                        // 但 _maximizedByUser 仍为 true；这里把两者重新对齐。
                        if (_maximizedByUser)
                        {
                            Rectangle area = Screen.FromHandle(Handle).WorkingArea;
                            if (Bounds != area)
                            {
                                Bounds = area;
                                LogLayout("REAPPLY_MAXIMIZED");
                            }
                        }
                        LayoutChrome();
                        Invalidate(true);
                    });
                }
            }
        }

        /// <summary>最小化恢复后重建 WS_EX_COMPOSITED 合成表面，避免窗口尺寸已变、画面仍停留在旧尺寸。</summary>
        private void ResetComposition()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            try
            {
                int ex = Native.GetWindowLong(Handle, -20);
                Native.SetWindowLong(Handle, -20, ex & ~0x02000000);
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                                    0x0001 | 0x0002 | 0x0004 | 0x0020 | 0x0010); // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED|NOACTIVATE
                Native.SetWindowLong(Handle, -20, ex);
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                                    0x0001 | 0x0002 | 0x0004 | 0x0020 | 0x0010);
                LogLayout("RESET_COMPOSITED");
            }
            catch
            {
            }
        }

        private void LogLayout(string ev)
        {
            if (!Config.DebugLayout)
            {
                return;
            }
            try
            {
                Config.Log("LAYOUT " + ev +
                           " state=" + WindowState +
                           " client=" + ClientSize.Width + "x" + ClientSize.Height +
                           " bounds=" + Bounds.Width + "x" + Bounds.Height +
                           " dpi=" + Ui.Scale.ToString("0.00", CultureInfo.InvariantCulture));
            }
            catch
            {
            }
        }

        // ---------------- 自绘窗口按钮 / 拖动 / 缩放 ----------------

        private Rectangle WinButtonRect(int index)
        {
            int w = Ui.S(46);
            int h = Ui.S(ControlHeight38);
            int right = ClientSize.Width - Ui.S(8);
            int x = right - (3 - index) * w;
            int y = Ui.S(Row1Top) + (Ui.S(Row1Height) - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private bool HeaderHitClient(Point p)
        {
            for (int i = 0; i < 3; i++)
            {
                if (WinButtonRect(i).Contains(p))
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildResizeEdges()
        {
            _edgeLeft = MakeEdge(Cursors.SizeWE);
            _edgeRight = MakeEdge(Cursors.SizeWE);
            _edgeTop = MakeEdge(Cursors.SizeNS);
            _edgeBottom = MakeEdge(Cursors.SizeNS);
            _cornerTL = MakeCorner(Cursors.SizeNWSE);
            _cornerTR = MakeCorner(Cursors.SizeNESW);
            _cornerBL = MakeCorner(Cursors.SizeNESW);
            _cornerBR = MakeCorner(Cursors.SizeNWSE);
            _edgeLeft.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 0); };
            _edgeRight.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 1); };
            _edgeTop.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 2); };
            _edgeBottom.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 3); };
            _cornerTL.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 4); };
            _cornerTR.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 5); };
            _cornerBL.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 6); };
            _cornerBR.MouseDown += delegate(object sender, MouseEventArgs e) { BeginEdgeDrag((Control)sender, e, 7); };
        }

        private Control MakeEdge(Cursor cursor)
        {
            Panel panel = new Panel();
            panel.BackColor = Ui.Bg;
            panel.Cursor = cursor;
            Controls.Add(panel);
            panel.BringToFront();
            panel.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && _edgeMode >= 0)
                {
                    UpdateEdgeDrag();
                }
            };
            panel.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                _edgeMode = -1;
                panel.Capture = false;
            };
            return panel;
        }

        private Control MakeCorner(Cursor cursor)
        {
            Panel panel = new Panel();
            panel.BackColor = Ui.Bg;
            panel.Cursor = cursor;
            Controls.Add(panel);
            panel.BringToFront();
            panel.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && _edgeMode >= 0)
                {
                    UpdateEdgeDrag();
                }
            };
            panel.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                _edgeMode = -1;
                panel.Capture = false;
            };
            return panel;
        }

        private int _edgeMode = -1;
        private Rectangle _edgeStartBounds;
        private Point _edgeStartMouse;

        private void BeginEdgeDrag(Control source, MouseEventArgs e, int mode)
        {
            if (WindowState == FormWindowState.Maximized || _maximizedByUser)
            {
                return;
            }
            _edgeMode = mode;
            _edgeStartBounds = Bounds;
            _edgeStartMouse = Cursor.Position;
            if (source != null)
            {
                source.Capture = true;
            }
        }

        private void UpdateEdgeDrag()
        {
            Point now = Cursor.Position;
            int dx = now.X - _edgeStartMouse.X;
            int dy = now.Y - _edgeStartMouse.Y;
            Rectangle b = _edgeStartBounds;
            int minW = MinimumSize.Width;
            int minH = MinimumSize.Height;
            if (_edgeMode == 0)
            {
                int w = Math.Max(minW, b.Width - dx);
                b.X = b.Right - w;
                b.Width = w;
            }
            else if (_edgeMode == 1)
            {
                b.Width = Math.Max(minW, b.Width + dx);
            }
            else if (_edgeMode == 2)
            {
                int h = Math.Max(minH, b.Height - dy);
                b.Y = b.Bottom - h;
                b.Height = h;
            }
            else if (_edgeMode == 3)
            {
                b.Height = Math.Max(minH, b.Height + dy);
            }
            else if (_edgeMode == 4) // 左上
            {
                int w = Math.Max(minW, b.Width - dx);
                int h = Math.Max(minH, b.Height - dy);
                b.X = b.Right - w;
                b.Y = b.Bottom - h;
                b.Width = w;
                b.Height = h;
            }
            else if (_edgeMode == 5) // 右上
            {
                b.Width = Math.Max(minW, b.Width + dx);
                int h = Math.Max(minH, b.Height - dy);
                b.Y = b.Bottom - h;
                b.Height = h;
            }
            else if (_edgeMode == 6) // 左下
            {
                int w = Math.Max(minW, b.Width - dx);
                b.X = b.Right - w;
                b.Width = w;
                b.Height = Math.Max(minH, b.Height + dy);
            }
            else if (_edgeMode == 7) // 右下
            {
                b.Width = Math.Max(minW, b.Width + dx);
                b.Height = Math.Max(minH, b.Height + dy);
            }
            Bounds = b;
        }

        private void LayoutResizeEdges()
        {
            if (_edgeLeft == null)
            {
                return;
            }
            int w = Ui.S(10);
            bool show = !_maximizedByUser && WindowState != FormWindowState.Maximized;
            _edgeLeft.Visible = show;
            _edgeRight.Visible = show;
            _edgeTop.Visible = show;
            _edgeBottom.Visible = show;
            _edgeLeft.SetBounds(0, 0, w, ClientSize.Height);
            _edgeRight.SetBounds(ClientSize.Width - w, 0, w, ClientSize.Height);
            _edgeTop.SetBounds(0, 0, ClientSize.Width, w);
            _edgeBottom.SetBounds(0, ClientSize.Height - w, ClientSize.Width, w);
            _edgeLeft.BringToFront();
            _edgeRight.BringToFront();
            _edgeTop.BringToFront();
            _edgeBottom.BringToFront();
            int c = Ui.S(16);
            _cornerTL.Visible = show;
            _cornerTR.Visible = show;
            _cornerBL.Visible = show;
            _cornerBR.Visible = show;
            _cornerTL.SetBounds(0, 0, c, c);
            _cornerTR.SetBounds(ClientSize.Width - c, 0, c, c);
            _cornerBL.SetBounds(0, ClientSize.Height - c, c, c);
            _cornerBR.SetBounds(ClientSize.Width - c, ClientSize.Height - c, c, c);
            _cornerTL.BringToFront();
            _cornerTR.BringToFront();
            _cornerBL.BringToFront();
            _cornerBR.BringToFront();
        }

        private void BuildAnimationLayer()
        {
            _animLayer = new Panel();
            _animLayer.BackColor = Ui.Bg;
            _animLayer.Visible = false;
            _animLayer.Paint += PaintAnimationLayer;
            Controls.Add(_animLayer);
        }

        private void PaintAnimationLayer(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Bg))
            {
                g.FillRectangle(brush, _animLayer.ClientRectangle);
            }
            if (_animBitmap == null)
            {
                return;
            }
            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix();
            matrix.Matrix33 = (float)Math.Max(0, Math.Min(1, _animAlpha));
            using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                g.DrawImage(_animBitmap, new Rectangle(_animOffset, 0, _animBitmap.Width, _animBitmap.Height),
                            0, 0, _animBitmap.Width, _animBitmap.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private void StopPageAnimation(bool showContent)
        {
            if (_animTimer != null)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
                _animTimer = null;
            }
            if (_animLayer != null)
            {
                _animLayer.Visible = false;
            }
            if (_animBitmap != null)
            {
                _animBitmap.Dispose();
                _animBitmap = null;
            }
            if (showContent && _animShown != null && !_animShown.IsDisposed)
            {
                _animShown.Visible = true;
            }
            _animShown = null;
        }

        private void StartPageAnimation(Control show)
        {
            StopPageAnimation(true);
            _animShown = show;
            try
            {
                Bitmap bmp = new Bitmap(Math.Max(1, show.Width), Math.Max(1, show.Height));
                show.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                _animBitmap = bmp;
            }
            catch
            {
                _animBitmap = null;
            }
            if (_animBitmap == null)
            {
                return;
            }
            show.Visible = false;
            _animLayer.Bounds = show.Bounds;
            _animLayer.Visible = true;
            _animLayer.BringToFront();
            _animAlpha = 0;
            _animOffset = Ui.S(18);
            _animLayer.Invalidate();
            _animTimer = new Timer();
            _animTimer.Interval = 16;
            DateTime start = DateTime.UtcNow;
            _animTimer.Tick += delegate
            {
                if (show.IsDisposed)
                {
                    StopPageAnimation(false);
                    return;
                }
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 200.0;
                if (t >= 1)
                {
                    StopPageAnimation(true);
                    return;
                }
                double eased = 1 - Math.Pow(1 - t, 3);
                _animAlpha = eased;
                _animOffset = (int)(Ui.S(18) * (1 - eased));
                _animLayer.Invalidate();
            };
            _animTimer.Start();
        }

        private void ToggleMaximize()
        {
            if (_maximizedByUser)
            {
                _maximizedByUser = false;
                if (_normalBounds.Width > 0)
                {
                    Bounds = _normalBounds;
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                }
            }
            else
            {
                _normalBounds = Bounds;
                Rectangle area = Screen.FromHandle(Handle).WorkingArea;
                Bounds = area;
                _maximizedByUser = true;
            }
            LogLayout("ToggleMaximize");
            LayoutChrome();
            Invalidate(true);
            _header.Invalidate();
        }

        private void HeaderMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || HeaderHitClient(e.Location))
            {
                return;
            }
            if (e.Clicks >= 2)
            {
                ToggleMaximize();
                return;
            }
            if (WindowState == FormWindowState.Maximized || _maximizedByUser)
            {
                return;
            }
            _dragging = true;
            _dragOffset = e.Location;
            _header.Capture = true;
        }

        private void HeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                Point screen = PointToScreen(e.Location);
                Native.MoveWindowNoCopy(this, screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            }
            int hover = -1;
            for (int i = 0; i < 3; i++)
            {
                if (WinButtonRect(i).Contains(e.Location))
                {
                    hover = i;
                }
            }
            if (hover != _winHover)
            {
                _winHover = hover;
                _header.Invalidate();
            }
            bool ghHover = _githubRect.Width > 0 && _githubRect.Contains(e.Location);
            if (ghHover != _githubHover)
            {
                _githubHover = ghHover;
                _header.Invalidate();
            }
            _header.Cursor = ghHover ? Cursors.Hand : Cursors.Default;
            if (!_dragging && _versionRect.Width > 0 && _versionRect.Contains(e.Location))
            {
                ShowVersionTip();
            }
            else
            {
                HideVersionTip();
            }
        }

        private void ShowVersionTip()
        {
            if (_buildTip == null)
            {
                _buildTip = new BuildInfoTip();
            }
            List<string> lines = new List<string>();
            lines.Add("版本 " + BuildInfo.DisplayVersion + "（" + BuildInfo.FullVersion + "）");
            lines.Add("构建时间：" + (string.IsNullOrEmpty(BuildInfo.BuildTime) ? "未知" : BuildInfo.BuildTime));
            lines.Add("数据版本：schema " + DataFiles.SchemaVersion + " · " + DataFiles.DataVersion);
            lines.Add("安装目录：" + Config.InstallDir);
            _buildTip.SetLines(lines);
            Point point = PointToScreen(new Point(_versionRect.X, _versionRect.Bottom + Ui.S(4)));
            _buildTip.Location = point;
            if (!_buildTip.Visible)
            {
                _buildTip.Show();
            }
            _buildTip.BringToFront();
        }

        private void HideVersionTip()
        {
            if (_buildTip != null && _buildTip.Visible)
            {
                _buildTip.Hide();
            }
        }

        private void HeaderMouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
            _header.Capture = false;
        }

        private void HeaderMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_githubRect.Width > 0 && _githubRect.Contains(e.Location))
            {
                OpenUrl(Config.RepoUrl);
                return;
            }
            if (WinButtonRect(0).Contains(e.Location))
            {
                WindowState = FormWindowState.Minimized;
                return;
            }
            if (WinButtonRect(1).Contains(e.Location))
            {
                ToggleMaximize();
                return;
            }
            if (WinButtonRect(2).Contains(e.Location))
            {
                Close();
                return;
            }
        }

        private void HeaderMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (HeaderHitClient(e.Location))
            {
                return;
            }
            ToggleMaximize();
        }

        // 顶部/底部的设计栅格（设计像素，统一 8px 基准）
        private const int HeaderDesignHeight = 112;
        private const int TabsDesignHeight = 44;
        private const int Row1Top = 18;
        private const int Row1Height = 40;
        private const int Row2Top = 66;
        private const int Row2Height = 32;
        private const int ControlHeight38 = 38;

        private void LayoutChrome()
        {
            if (_header == null || _footer == null || _body == null)
            {
                return;
            }
            int headerHeight = Ui.S(HeaderDesignHeight);
            int tabHeight = Ui.S(TabsDesignHeight);
            int footerHeight = _footer.Visible ? Ui.S(76) : 0;
            int bannerHeight = _updateBanner != null && _updateBanner.Visible ? Ui.S(40) : 0;
            _header.SetBounds(0, 0, ClientSize.Width, headerHeight);
            Rectangle chip = GameChipRect();
            if (_pathChip != null)
            {
                _pathChip.Bounds = chip;
            }
            if (_liveButton != null)
            {
                // 第二行右侧：两个外链改为次级/幽灵按钮，不与第一行的主操作抢焦点。
                int pad = Ui.Sp24;
                int gap = Ui.Sp8;
                int wtW = Math.Max(Ui.S(156), TextRenderer.MeasureText(_wtButton.Text, _wtButton.Font).Width + Ui.S(56));
                int liveW = Math.Max(Ui.S(124), TextRenderer.MeasureText(_liveButton.Text, _liveButton.Font).Width + Ui.S(56));
                int right = ClientSize.Width - pad;
                int row2Top = Ui.S(Row2Top);
                int row2H = Ui.S(Row2Height);
                _liveButton.SetBounds(right - liveW - gap - wtW, row2Top, liveW, row2H);
                _wtButton.SetBounds(right - wtW, row2Top, wtW, row2H);
            }
            if (_settingsButton != null)
            {
                _settingsButton.SetBounds(chip.Left - Ui.Sp8 - Ui.S(76), chip.Top, Ui.S(76), chip.Height);
            }
            if (_tabs != null)
            {
                _tabs.SetBounds(0, headerHeight, ClientSize.Width, tabHeight);
            }
            if (_updateBanner != null)
            {
                _updateBanner.SetBounds(0, headerHeight + tabHeight, ClientSize.Width, bannerHeight);
                if (bannerHeight > 0)
                {
                    _updateBanner.BringToFront();
                }
            }
            _footer.SetBounds(0, ClientSize.Height - footerHeight, ClientSize.Width, footerHeight);
            int bodyTop = headerHeight + tabHeight + bannerHeight;
            _body.SetBounds(0, bodyTop, ClientSize.Width, ClientSize.Height - bodyTop - footerHeight);
            _body.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            if (_managerHost != null)
            {
                _managerHost.Bounds = _body.Bounds;
            }
            RefreshPathChip();
            _header.Invalidate();
            _footer.Invalidate();
            LayoutFooter();
            LayoutBody();
            LayoutResizeEdges();
        }

        private void RefreshPathChip()
        {
            if (_pathChip == null)
            {
                return;
            }
            string dir = Config.GameDir;
            _pathChip.Valid = Config.GameDirLooksValid(dir);
            _pathChip.PathText = string.IsNullOrEmpty(dir) ? "未设置游戏目录" : ShortPath(dir);
            _pathChip.Invalidate();
        }

        private void BuildHeader()
        {
            _header = new BufferedPanel();
            _header.BackColor = Ui.Bg;
            _header.Paint += PaintHeader;
            _header.Cursor = Cursors.Default;
            _header.MouseDown += HeaderMouseDown;
            _header.MouseMove += HeaderMouseMove;
            _header.MouseUp += HeaderMouseUp;
            _header.MouseClick += HeaderMouseClick;
            _header.MouseDoubleClick += HeaderMouseDoubleClick;
            _header.MouseLeave += delegate
            {
                _dragging = false;
                HideVersionTip();
                if (_winHover != -1)
                {
                    _winHover = -1;
                    _header.Invalidate();
                }
            };
            Controls.Add(_header);

            _liveButton = new SiteButton();
            _liveButton.Text = "官方涂装站";
            _liveButton.Icon = LoadSiteIcon("live.png");
            _liveButton.Click += delegate { OpenUrl("https://live.warthunder.com/feed/all/"); };
            _header.Controls.Add(_liveButton);

            _wtButton = new SiteButton();
            _wtButton.Text = "战雷涂装中文网";
            _wtButton.Icon = LoadSiteIcon("wtliker.png");
            _wtButton.Click += delegate { OpenUrl("https://wtliker.com/"); };
            _header.Controls.Add(_wtButton);

            _settingsButton = new FlatButton();
            _settingsButton.Text = "设置";
            _settingsButton.Font = Ui.SmallBold;
            _settingsButton.BackColor = Ui.Page;
            _settingsButton.Click += delegate { OpenSettings(); };
            _header.Controls.Add(_settingsButton);

            _pathChip = new PathChip();
            _pathChip.Font = Ui.CardSub;
            _pathChip.Click += delegate { OpenSettings(); };
            _header.Controls.Add(_pathChip);
        }

        private void PaintWindowButtons(Graphics g)
        {
            for (int i = 0; i < 3; i++)
            {
                Rectangle r = WinButtonRect(i);
                bool close = i == 2;
                if (_winHover == i)
                {
                    Ui.FillRound(g, r, Ui.RButton, close ? Ui.T.CloseHoverBg : Ui.PanelHover);
                }
                Color color = (_winHover == i && close) ? Ui.T.CloseHoverFg : Ui.TextSecondary;
                int cx = r.X + r.Width / 2;
                int cy = r.Y + r.Height / 2;
                float width = Math.Max(1.3f, Ui.Scale * 1.4f);
                if (i == 0)
                {
                    using (Pen pen = new Pen(color, width))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, cx - Ui.S(6), cy, cx + Ui.S(6), cy);
                    }
                }
                else if (i == 1)
                {
                    using (Pen pen = new Pen(color, width))
                    {
                        if (_maximizedByUser || WindowState == FormWindowState.Maximized)
                        {
                            Rectangle back = new Rectangle(cx - Ui.S(5), cy - Ui.S(6), Ui.S(11), Ui.S(10));
                            Rectangle front = new Rectangle(cx - Ui.S(2), cy - Ui.S(3), Ui.S(11), Ui.S(10));
                            g.DrawRectangle(pen, back);
                            using (SolidBrush fill = new SolidBrush(Ui.Page))
                            {
                                g.FillRectangle(fill, new Rectangle(front.X, front.Y, front.Width - 1, front.Height - 1));
                            }
                            g.DrawRectangle(pen, front);
                        }
                        else
                        {
                            g.DrawRectangle(pen, cx - Ui.S(6), cy - Ui.S(5), Ui.S(12), Ui.S(10));
                        }
                    }
                }
                else
                {
                    using (Pen pen = new Pen(color, width))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, cx - Ui.S(5), cy - Ui.S(5), cx + Ui.S(5), cy + Ui.S(5));
                        g.DrawLine(pen, cx + Ui.S(5), cy - Ui.S(5), cx - Ui.S(5), cy + Ui.S(5));
                    }
                }
            }
        }

        private static Bitmap LoadGithubMark()
        {
            try
            {
                using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("github.png"))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Bitmap bmp = new Bitmap(stream))
                    {
                        return new Bitmap(bmp);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap LoadSiteIcon(string resource)
        {
            try
            {
                using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Bitmap bmp = new Bitmap(stream))
                    {
                        return new Bitmap(bmp);
                    }
                }
            }
            catch
            {
                return null;
            }
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

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (Pen pen = new Pen(Ui.StrokeSoft, Ui.BorderWidth))
            {
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }

            int row1Top = Ui.S(Row1Top);
            int row1H = Ui.S(Row1Height);
            int pad = Ui.Sp24;
            int iconSize = Ui.S(40);
            Rectangle iconRect = new Rectangle(pad, row1Top, iconSize, iconSize);
            Bitmap logo = LoadLogo();
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, iconRect);
            }
            else
            {
                Ui.FillRound(g, iconRect, Ui.RCard, Ui.Accent);
            }

            // 第一行：图标 / 标题 / 版本号+构建时间（11~12px 弱化色，与标题基线对齐）。
            int tx = iconRect.Right + Ui.Sp16;
            int titleLine = Ui.LineHeight(Ui.Title);
            int titleTop = row1Top + (row1H - titleLine) / 2;
            Ui.DrawText(g, "战雷涂装管理器", Ui.Title, Ui.TextPrimary,
                    new Rectangle(tx, titleTop, Ui.S(340), titleLine),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);

            string versionText = BuildInfo.HeaderText;
            Size titleSize = TextRenderer.MeasureText("战雷涂装管理器", Ui.Title);
            int versionX = tx + titleSize.Width + Ui.Sp8;
            int versionLine = Ui.LineHeight(Ui.Small);
            int versionTop = titleTop + (int)((titleLine - versionLine) * 0.78f);
            Size versionSize = TextRenderer.MeasureText(versionText, Ui.Small);
            Rectangle chipBounds = GameChipRect();
            int versionMaxRight = chipBounds.Left - Ui.Sp12;
            if (versionX + versionSize.Width <= versionMaxRight)
            {
                Rectangle vr = new Rectangle(versionX, versionTop, versionSize.Width + Ui.S(2), versionLine);
                Ui.DrawText(g, versionText, Ui.Small, Ui.TextWeak, vr,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                _versionRect = vr;
            }
            else
            {
                _versionRect = Rectangle.Empty;
            }

            // 第二行：副标题（12px 次要色）+ GitHub 图标（弱化色，不抢焦点）。
            const string subtitle = "War Thunder 自定义涂装 · 识别 / 替换 / 备份";
            int row2Top = Ui.S(Row2Top);
            int row2H = Ui.S(Row2Height);
            Ui.DrawText(g, subtitle, Ui.CardSub, Ui.TextSecondary,
                    new Rectangle(tx, row2Top, Ui.S(430), row2H),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (_githubMark == null)
            {
                _githubMark = LoadGithubMark();
            }
            Size subtitleSize = TextRenderer.MeasureText(subtitle, Ui.CardSub);
            int ghSize = Ui.S(18);
            int ghX = tx + subtitleSize.Width + Ui.Sp8;
            int ghY = row2Top + (row2H - ghSize) / 2;
            if (_githubMark != null)
            {
                Rectangle gh = new Rectangle(ghX, ghY, ghSize, ghSize);
                Ui.DrawIconTinted(g, _githubMark, gh, _githubHover ? Ui.TextSecondary : Ui.TextWeak);
                _githubRect = gh;
            }
            else
            {
                _githubRect = Rectangle.Empty;
            }

            PaintWindowButtons(g);
        }

        private Bitmap LoadLogo()
        {
            if (_logo != null)
            {
                return _logo;
            }
            try
            {
                using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.png"))
                {
                    if (stream != null)
                    {
                        _logo = new Bitmap(stream);
                    }
                }
            }
            catch
            {
            }
            return _logo;
        }

        private Rectangle ManagerChipRect()
        {
            int width = Ui.S(92);
            int height = Ui.S(38);
            int x = GameChipRect().Left - width - Ui.S(10);
            return new Rectangle(x, (_header.Height - height) / 2 - Ui.S(2), width, height);
        }

        private void BuildPages()
        {
            _tabs = new PageTabs();
            _tabs.Left = 0;
            _tabs.Top = Ui.S(80);
            _tabs.Width = ClientSize.Width;
            _tabs.PageChanged += delegate { SwitchPage(_tabs.Active); };
            Controls.Add(_tabs);

            _managerHost = new BufferedPanel();
            _managerHost.BackColor = Ui.Bg;
            _managerHost.Visible = false;
            Controls.Add(_managerHost);
            _managerHost.BringToFront();
            _body.BringToFront();
            _tabs.BringToFront();
            _header.BringToFront();
            _footer.BringToFront();
        }

        private void BuildUpdateBanner()
        {
            _updateBanner = new UpdateBanner();
            _updateBanner.Visible = false;
            _updateBanner.ViewClicked += delegate
            {
                SemVer version = _bannerVersion;
                DismissUpdateBanner(version);
                OpenSettings(3);
            };
            _updateBanner.CloseClicked += delegate
            {
                DismissUpdateBanner(_bannerVersion);
            };
            Controls.Add(_updateBanner);
        }

        /// <summary>启动时静默检查：首屏已渲染（BeginInvoke 之后）才调用，失败只写日志。</summary>
        private void BeginStartupUpdateCheck()
        {
            if (_startupCheckStarted)
            {
                return;
            }
            _startupCheckStarted = true;
            try
            {
                if (!Config.AutoCheckUpdate)
                {
                    Config.Log("自动检查更新：设置已关闭，本次启动不发起请求");
                    return;
                }
                Config.Log("自动检查更新：开始（静默；" + UpdateService.UserAgent + "）");
                UpdateService.CheckAsync(true, delegate(UpdateCheckResult result)
                {
                    if (IsDisposed || Disposing || !IsHandleCreated)
                    {
                        return;
                    }
                    if (result.State == UpdateState.NewVersion && result.Latest != null)
                    {
                        if (UpdateService.ShouldNotify(result.Latest))
                        {
                            ShowUpdateBanner(result.Latest);
                        }
                        else
                        {
                            Config.Log("自动检查更新：v" + result.Latest.ToDisplay() + " 已提示过，不重复弹提示条");
                        }
                    }
                    else if (result.State == UpdateState.Failed)
                    {
                        Config.Log("自动检查更新：失败（" + result.FailureText + "）→ " + result.ErrorDetail);
                    }
                    else
                    {
                        Config.Log("自动检查更新：当前已是最新版本 " + BuildInfo.DisplayVersion +
                                   (result.FromCache ? "（10 分钟缓存）" : ""));
                    }
                });
            }
            catch (Exception ex)
            {
                Config.Log("自动检查更新：启动异常 → " + ex.Message);
            }
        }

        private void ShowUpdateBanner(SemVer version)
        {
            if (_updateBanner == null || version == null)
            {
                return;
            }
            _bannerVersion = version;
            _updateBanner.SetVersion("v" + version.ToDisplay());
            _updateBanner.Visible = true;
            LayoutChrome();
            Config.Log("自动检查更新：发现新版本 v" + version.ToDisplay() + "，显示顶部提示条");
        }

        private void DismissUpdateBanner(SemVer version)
        {
            if (version != null)
            {
                UpdateService.MarkNotified(version);
            }
            if (_updateBanner != null)
            {
                _updateBanner.Visible = false;
            }
            LayoutChrome();
        }

        private void EnsureManager()
        {
            if (_manager != null)
            {
                return;
            }
            _manager = new ManagerForm();
            _manager.TopLevel = false;
            _manager.FormBorderStyle = FormBorderStyle.None;
            _manager.Embed();
            _manager.Dock = DockStyle.Fill;
            _managerHost.Controls.Add(_manager);
            _manager.Show();
            _manager.ReloadNow();
            _manager.Refresh();
        }

        private static bool SystemAnimationsEnabled()
        {
            try
            {
                bool enabled = true;
                if (SystemParametersInfo(0x1042, 0, ref enabled, 0))
                {
                    return enabled;
                }
            }
            catch
            {
            }
            return true;
        }

        private void SwitchPage(int page)
        {
            if (page < 0 || page > 1 || page == _page)
            {
                return;
            }
            if (page == 1)
            {
                EnsureManager();
            }
            _footer.Visible = page == 0;
            LayoutChrome();
            if (page == 1 && _manager != null && !_manager.IsDisposed)
            {
                _manager.Refresh();
            }
            Control show = page == 1 ? (Control)_managerHost : (Control)_body;
            Control hide = page == 1 ? (Control)_body : (Control)_managerHost;
            StopPageAnimation(true);
            hide.Visible = false;
            show.Visible = true;
            show.BringToFront();
            LayoutResizeEdges();
            _tabs.Active = page;
            _tabs.Invalidate();
            _page = page;
            if (Ui.Animations && SystemAnimationsEnabled())
            {
                try
                {
                    show.Refresh();
                }
                catch
                {
                }
                StartPageAnimation(show);
            }
            else
            {
                show.Left = 0;
            }
        }

        private Rectangle GameChipRect()
        {
            int width = Ui.S(300);
            int height = Ui.S(ControlHeight38);
            int winWidth = Ui.S(46) * 3;
            int right = ClientSize.Width - Ui.S(8);
            int x = right - winWidth - Ui.Sp16 - width;
            int y = Ui.S(Row1Top) + (Ui.S(Row1Height) - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private string ShortPath(string path)
        {
            try
            {
                string trimmed = path.TrimEnd('\\');
                string name = Path.GetFileName(trimmed);
                string parent = Path.GetFileName(Path.GetDirectoryName(trimmed));
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                {
                    if (string.Equals(parent, "common", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                    return parent + "\\" + name;
                }
                return trimmed;
            }
            catch
            {
                return path;
            }
        }

        private void BuildBody()
        {
            _body = new BufferedPanel();
            _body.BackColor = Ui.Page;
            _body.Resize += delegate { LayoutBody(); };
            Controls.Add(_body);

            _drop = new DropZone();
            _drop.Main = "把涂装包拖到这里";
            _drop.Sub = "支持 .zip 压缩包或已解压的文件夹，可以一次拖入多个";
            _drop.Cursor = Cursors.Hand;
            _drop.Click += delegate { BrowseFiles(); };
            _drop.ChooseButton.Click += delegate { BrowseFiles(); };
            _drop.DragEnter += OnDragEnter;
            _drop.DragOver += OnDragEnter;
            _drop.DragLeave += delegate { _drop.Hot = false; };
            _drop.DragDrop += OnDragDrop;
            _body.Controls.Add(_drop);

            _detail = new BufferedPanel();
            _detail.BackColor = Ui.Page;
            _detail.Paint += PaintDetail;
            _detail.Resize += delegate { LayoutDetail(); };
            _detail.AllowDrop = true;
            _detail.DragEnter += OnDragEnter;
            _detail.DragOver += OnDragEnter;
            _detail.DragLeave += delegate { _drop.Hot = false; };
            _detail.DragDrop += OnDragDrop;
            _body.Controls.Add(_detail);
            BuildDetail();

            _listPanel = new FlowLayoutPanel();
            _listPanel.FlowDirection = FlowDirection.TopDown;
            _listPanel.WrapContents = false;
            _listPanel.AutoScroll = true;
            _listPanel.BackColor = Ui.Page;
            _listPanel.Padding = new Padding(0);
            _listPanel.Resize += delegate { ResizeCards(); };
            _listPanel.AllowDrop = true;
            _listPanel.DragEnter += OnDragEnter;
            _listPanel.DragOver += OnDragEnter;
            _listPanel.DragLeave += delegate { _drop.Hot = false; };
            _listPanel.DragDrop += OnDragDrop;
            _body.Controls.Add(_listPanel);
        }

        private void LayoutBody()
        {
            if (_body == null || _drop == null)
            {
                return;
            }
            // 两栏栅格：左右各占一半，容器内边距 24、卡片间距 16。
            int pad = Ui.Sp24;
            int gap = Ui.GapCard;
            int width = _body.ClientSize.Width;
            int height = _body.ClientSize.Height;
            int available = width - pad * 2 - gap;
            int rightWidth = Math.Max(Ui.S(360), available / 2);
            int leftWidth = available - rightWidth;
            if (leftWidth < Ui.S(320))
            {
                leftWidth = Ui.S(320);
                rightWidth = Math.Max(Ui.S(320), available - leftWidth);
            }
            int contentTop = pad;
            int contentHeight = Math.Max(Ui.S(200), height - pad * 2);
            bool empty = _packages.Count == 0;

            _detail.SetBounds(width - pad - rightWidth, contentTop, rightWidth, contentHeight);

            if (empty)
            {
                _drop.Empty = true;
                _drop.SetBounds(pad, contentTop, leftWidth, contentHeight);
                _listPanel.Visible = false;
            }
            else
            {
                _drop.Empty = false;
                int dropHeight = Ui.S(96);
                _drop.SetBounds(pad, contentTop, leftWidth, dropHeight);
                _listPanel.Visible = true;
                _listPanel.SetBounds(pad, contentTop + dropHeight + gap, leftWidth,
                                     Math.Max(Ui.S(80), contentHeight - dropHeight - gap));
            }
            _drop.Invalidate();
            _listPanel.Invalidate();
            _detail.Invalidate();
            LayoutDetail();
        }

        private void BuildDetail()
        {
            _vehPanel = new BufferedPanel();
            _vehPanel.AutoScroll = true;
            _vehPanel.Resize += delegate
            {
                _vehPanel.Invalidate();
                RelayoutRows();
            };
            _vehPanel.Scroll += delegate { HideDetailCard(); };
            _vehPanel.Scroll += delegate
            {
                if (_scrollCover != null)
                {
                    _scrollCover.Visible = _vehPanel.VerticalScroll.Visible;
                    _scrollCover.Invalidate();
                }
            };
            _vehPanel.BackColor = Ui.CardBg;
            _detail.Controls.Add(_vehPanel);

            _refreshButton = new FlatButton();
            _refreshButton.Text = "刷新文件夹";
            _refreshButton.BackColor = Ui.CardBg;
            _refreshButton.Height = Ui.HSecondary;
            _refreshButton.Click += delegate { RefreshFolders(true); };
            _detail.Controls.Add(_refreshButton);
            // FlatButton 在 OnParentChanged 里会用父容器背景覆盖，Add 之后必须重新指定，
            // 否则卡片内按钮的圆角四角会露出页面底色（黑色）。
            _refreshButton.BackColor = Ui.CardBg;

            _manualButton = new FlatButton();
            _manualButton.Text = "手动指定载具…";
            _manualButton.Primary = true;
            _manualButton.BackColor = Ui.CardBg;
            _manualButton.Visible = false;
            _manualButton.Click += delegate { ManualAssign(); };
            _detail.Controls.Add(_manualButton);
            _manualButton.BackColor = Ui.CardBg;
        }

        private void LayoutDetail()
        {
            if (_detail == null || _vehPanel == null)
            {
                return;
            }
            int width = _detail.ClientSize.Width;
            int height = _detail.ClientSize.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }
            int pad = Ui.Sp24;
            int buttonY = height - pad - Ui.HPrimary;
            // 按文字实际宽度给按钮留足空间，避免 125%/150% 字号放大后被裁切。
            int refreshWidth = Math.Max(Ui.S(120),
                TextRenderer.MeasureText("刷新文件夹", Ui.BodyBold).Width + Ui.S(36));
            _refreshButton.SetBounds(pad, buttonY + (Ui.HPrimary - Ui.HSecondary) / 2,
                                     refreshWidth, Ui.HSecondary);
            _manualButton.SetBounds(pad + refreshWidth + Ui.Sp12, buttonY,
                                    Math.Max(Ui.S(120), width - pad * 2 - refreshWidth - Ui.Sp12),
                                    Ui.HPrimary);

            int hintHeight = Ui.S(48);
            int hintTop = buttonY - Ui.Sp12 - hintHeight;
            int vehTop = Ui.S(176);
            int vehHeight = Math.Max(Ui.S(60), hintTop - Ui.Sp12 - vehTop);
            _vehPanel.SetBounds(Ui.S(16), vehTop, width - Ui.S(32), vehHeight);
            if (_scrollCover == null)
            {
                _scrollCover = new ScrollCover(_vehPanel);
                _detail.Controls.Add(_scrollCover);
            }
            _scrollCover.SetBounds(_vehPanel.Right - Ui.S(14), vehTop, Ui.S(14), vehHeight);
            _scrollCover.Visible = _vehPanel.VerticalScroll.Visible;
            _scrollCover.BringToFront();
            ApplyDetailChildren();
        }

        private void PaintDetail(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, _detail.Width - 1, _detail.Height - 1);
            Ui.FillRound(g, r, Ui.RCard, Ui.CardBg);
            Ui.DrawRound(g, r, Ui.RCard, Ui.Stroke, Ui.BorderWidth);

            // 标题位置在空态/内容态完全一致。
            Ui.DrawText(g, "涂装包详情", Ui.SmallBold, Ui.TextSecondary,
                    new Rectangle(Ui.Sp24, Ui.Sp24, Ui.S(200), Ui.S(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (Pen pen = new Pen(Ui.StrokeSoft, Ui.BorderWidth))
            {
                g.DrawLine(pen, Ui.Sp24, Ui.S(56), _detail.Width - Ui.Sp24, Ui.S(56));
            }

            if (_guideT > 0.002f)
            {
                DrawGuide(g, _guideT);
            }
            if (_guideT < 0.998f)
            {
                DrawDetailText(g, 1f - _guideT);
            }
        }

        private void DrawTextAlpha(Graphics g, string text, Font font, Color color, Rectangle rect,
                                   TextFormatFlags flags, float alpha)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.002f)
            {
                return;
            }
            if (alpha >= 0.998f)
            {
                Ui.DrawText(g, text, font, color, rect, flags);
                return;
            }
            int a = (int)(255 * alpha);
            StringFormat format = new StringFormat(StringFormat.GenericDefault);
            format.Trimming = StringTrimming.EllipsisCharacter;
            format.FormatFlags |= StringFormatFlags.NoWrap;
            if ((flags & TextFormatFlags.HorizontalCenter) != 0)
            {
                format.Alignment = StringAlignment.Center;
            }
            else if ((flags & TextFormatFlags.Right) != 0)
            {
                format.Alignment = StringAlignment.Far;
            }
            float y = rect.Y;
            if ((flags & TextFormatFlags.VerticalCenter) != 0)
            {
                format.LineAlignment = StringAlignment.Center;
                y = rect.Y;
            }
            else if ((flags & TextFormatFlags.Bottom) != 0)
            {
                format.LineAlignment = StringAlignment.Far;
            }
            else
            {
                format.LineAlignment = StringAlignment.Near;
            }
            TextRenderingHint oldHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(a, color)))
            {
                g.DrawString(text, font, brush, new RectangleF(rect.X, y, rect.Width, rect.Height), format);
            }
            g.TextRenderingHint = oldHint;
            format.Dispose();
        }

        private void DrawGuide(Graphics g, float alpha)
        {
            int width = _detail.ClientSize.Width;
            int pad = Ui.Sp24;
            int textWidth = width - pad * 2;
            Font introFont = Ui.Body;
            int lineHeight = (int)(Ui.LineHeight(introFont) * 1.6f);
            string intro = "导入涂装包后，这里会显示文件数、体积和可导入的载具。";
            List<string> introLines = Ui.WrapLines(intro, introFont, textWidth);
            Font itemFont = Ui.CardSub;
            int itemHeight = (int)(Ui.LineHeight(itemFont) * 1.6f);

            int introHeight = introLines.Count * lineHeight;
            int gap = Ui.Sp24;
            int itemsHeight = itemHeight * 3 + Ui.Sp8 * 2;
            int blockHeight = introHeight + gap + itemsHeight;
            int top = _detail.ClientSize.Height / 3 - blockHeight / 2;
            if (top < Ui.S(84))
            {
                top = Ui.S(84);
            }

            int y = top;
            for (int i = 0; i < introLines.Count; i++)
            {
                DrawTextAlpha(g, introLines[i], introFont, Ui.TextSecondary,
                        new Rectangle(pad, y, textWidth, lineHeight), TextFormatFlags.Left | TextFormatFlags.Top, alpha);
                y += lineHeight;
            }
            y += gap;

            string[] items = new string[]
            {
                "支持 .zip 压缩包或已解压的文件夹",
                "可以一次拖入多个涂装包",
                "识别出的载具需勾选后再导入"
            };
            Color itemColor = Ui.TextSecondary;
            for (int i = 0; i < items.Length; i++)
            {
                Rectangle iconRect = new Rectangle(pad, y + (itemHeight - Ui.S(18)) / 2, Ui.S(18), Ui.S(18));
                DrawGuideIcon(g, i, iconRect, alpha);
                DrawTextAlpha(g, items[i], itemFont, itemColor,
                        new Rectangle(pad + Ui.S(30), y, textWidth - Ui.S(30), itemHeight),
                        TextFormatFlags.Left | TextFormatFlags.Top, alpha);
                y += itemHeight + Ui.Sp8;
            }
        }

        private void DrawGuideIcon(Graphics g, int kind, Rectangle rect, float alpha)
        {
            Color color = Ui.TextWeak;
            if (alpha < 0.998f)
            {
                color = Color.FromArgb((int)(255 * alpha), color);
            }
            using (Pen pen = new Pen(color, Math.Max(1.2f, Ui.Scale * 1.3f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == 0)
                {
                    int left = rect.X + Ui.S(1);
                    int right = rect.Right - Ui.S(1);
                    int top = rect.Y + Ui.S(3);
                    int bottom = rect.Bottom - Ui.S(2);
                    g.DrawLines(pen, new Point[] {
                        new Point(left, bottom), new Point(left, top),
                        new Point(rect.X + rect.Width / 2 - Ui.S(1), top),
                        new Point(rect.X + rect.Width / 2 + Ui.S(1), top + Ui.S(3)),
                        new Point(right, top + Ui.S(3)), new Point(right, bottom),
                        new Point(left, bottom)
                    });
                }
                else if (kind == 1)
                {
                    int left = rect.X + Ui.S(2);
                    int right = rect.Right - Ui.S(2);
                    int top = rect.Y + Ui.S(6);
                    g.DrawRectangle(pen, left, top, right - left, Ui.S(8));
                    g.DrawRectangle(pen, left, top - Ui.S(4), right - left, Ui.S(8));
                }
                else
                {
                    Rectangle box = new Rectangle(rect.X + Ui.S(1), rect.Y + Ui.S(2), rect.Width - Ui.S(2), rect.Height - Ui.S(4));
                    g.DrawRectangle(pen, box);
                    g.DrawLines(pen, new Point[] {
                        new Point(box.X + Ui.S(3), box.Y + box.Height / 2),
                        new Point(box.X + Ui.S(6), box.Y + box.Height / 2 + Ui.S(3)),
                        new Point(box.Right - Ui.S(3), box.Y + Ui.S(3))
                    });
                }
            }
        }

        private void DrawDetailText(Graphics g, float alpha)
        {
            if (!_detailHasSelection())
            {
                return;
            }
            int width = _detail.ClientSize.Width;
            int pad = Ui.Sp24;
            int textWidth = width - pad * 2;
            DrawTextAlpha(g, _detailNameText, Ui.H1, Ui.TextPrimary,
                    new Rectangle(pad, Ui.S(64), textWidth, Ui.S(30)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis, alpha);

            string[] metaLines = (_detailMetaText ?? "").Split(new char[] { '\n' });
            int metaY = Ui.S(100);
            int metaLine = Ui.S(18);
            for (int i = 0; i < metaLines.Length && i < 3; i++)
            {
                DrawTextAlpha(g, metaLines[i], Ui.Small, _detailMetaColor,
                        new Rectangle(pad, metaY + i * metaLine, textWidth, metaLine),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis, alpha);
            }

            DrawTextAlpha(g, _detailSectionText, Ui.SmallBold, Ui.TextSecondary,
                    new Rectangle(pad, Ui.S(150), textWidth, Ui.S(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis, alpha);

            int hintHeight = Ui.S(48);
            int hintTop = _detail.ClientSize.Height - Ui.Sp24 - Ui.HPrimary - Ui.Sp12 - hintHeight;
            string[] hintLines = (_detailHintText ?? "").Split(new char[] { '\n' });
            int hintLine = Ui.S(20);
            for (int i = 0; i < hintLines.Length && i < 2; i++)
            {
                DrawTextAlpha(g, hintLines[i], Ui.Small, _detailHintColor,
                        new Rectangle(pad, hintTop + i * hintLine, textWidth, hintLine),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis, alpha);
            }
        }

        private bool _detailHasSelection()
        {
            return _selected != null;
        }

        private void ApplyDetailChildren()
        {
            if (_vehPanel == null)
            {
                return;
            }
            bool show = !_detailGuide && !_guideAnimating;
            _vehPanel.Visible = show;
            _refreshButton.Visible = show && _detailRefreshVisible;
            _manualButton.Visible = show && _detailManualVisible;
        }

        private void SetDetailGuide(bool guide)
        {
            if (_detailGuide == guide && !_guideAnimating)
            {
                ApplyDetailChildren();
                return;
            }
            if (_detailGuide == guide)
            {
                return;
            }
            _detailGuide = guide;
            if (!Ui.Motion || _detail.ClientSize.Width <= 0)
            {
                _guideT = guide ? 1f : 0f;
                _guideAnimating = false;
                ApplyDetailChildren();
                _detail.Invalidate();
                return;
            }
            _guideFrom = _guideT;
            _guideStart = DateTime.UtcNow;
            _guideAnimating = true;
            _vehPanel.Visible = false;
            _refreshButton.Visible = false;
            _manualButton.Visible = false;
            if (_guideTimer == null)
            {
                _guideTimer = new Timer();
                _guideTimer.Interval = 16;
                _guideTimer.Tick += delegate { StepGuideAnimation(); };
            }
            _guideTimer.Stop();
            _guideTimer.Start();
        }

        private void StepGuideAnimation()
        {
            double t = (DateTime.UtcNow - _guideStart).TotalMilliseconds / Tok.PanelMotionMs;
            float target = _detailGuide ? 1f : 0f;
            if (t >= 1 || _detail.IsDisposed)
            {
                _guideT = target;
                _guideAnimating = false;
                if (_guideTimer != null)
                {
                    _guideTimer.Stop();
                }
                ApplyDetailChildren();
            }
            else
            {
                double eased = 1 - Math.Pow(1 - t, 3);
                _guideT = _guideFrom + (target - _guideFrom) * (float)eased;
            }
            _detail.Invalidate();
        }

        private void BuildFooter()
        {
            _footer = new BufferedPanel();
            _footer.BackColor = Ui.Page;
            _footer.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Ui.StrokeSoft, Ui.BorderWidth))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
                }
            };
            Controls.Add(_footer);

            _status = new Label();
            _status.Font = Ui.Small;
            _status.ForeColor = Ui.TextWeak;
            _status.BackColor = Ui.Page;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "准备就绪";
            _footer.Controls.Add(_status);

            // 最右：强调色实心主按钮，权重最高。
            _importButton = new FlatButton();
            _importButton.Text = "导入选中";
            _importButton.Primary = true;
            _importButton.BackColor = Ui.Page;
            _importButton.Click += delegate { StartImport(); };
            _footer.Controls.Add(_importButton);

            FlatButton open = new FlatButton();
            open.Text = "打开UserSkins文件夹";
            open.Font = Ui.Body;
            open.BackColor = Ui.Page;
            open.Click += delegate { OpenUserSkins(); };
            _footer.Controls.Add(open);

            FlatButton clear = new FlatButton();
            clear.Text = "清空列表";
            clear.Font = Ui.Body;
            clear.BackColor = Ui.Page;
            clear.Click += delegate
            {
                _packages.Clear();
                _selected = null;
                RefreshList();
            };
            _footer.Controls.Add(clear);

            _footer.Resize += delegate { LayoutFooter(); };
        }

        private void LayoutFooter()
        {
            if (_footer == null || _status == null)
            {
                return;
            }
            // 底部动作行：三个按钮同高（36），最右为主操作，左侧状态与按钮组留 32 间距。
            int width = _footer.ClientSize.Width;
            int pad = Ui.Sp24;
            int height = Ui.HPrimary;
            int gap = Ui.Sp12;
            int top = (_footer.Height - height) / 2;
            int right = width - pad;
            FlatButton open = FindButton("打开UserSkins文件夹");
            FlatButton clear = FindButton("清空列表");
            int openWidth = open == null ? Ui.S(176) : Math.Max(Ui.S(176), ButtonTextWidth(open) + Ui.S(32));
            int clearWidth = clear == null ? Ui.S(96) : Math.Max(Ui.S(96), ButtonTextWidth(clear) + Ui.S(28));
            int importWidth = Math.Max(Ui.S(166), ButtonTextWidth(_importButton) + Ui.S(36));
            _importButton.SetBounds(right - importWidth, top, importWidth, height);
            int cursor = _importButton.Left;
            if (open != null)
            {
                cursor -= gap + openWidth;
                open.SetBounds(cursor, top, openWidth, height);
                cursor -= gap + clearWidth;
                if (clear != null)
                {
                    clear.SetBounds(cursor, top, clearWidth, height);
                }
            }
            int statusRight = (clear != null ? clear.Left : _importButton.Left) - Ui.Sp32;
            _status.SetBounds(pad, top, Math.Max(Ui.S(160), statusRight - pad), height);
        }

        private static int ButtonTextWidth(FlatButton button)
        {
            if (button == null)
            {
                return 0;
            }
            return TextRenderer.MeasureText(button.Text, button.Font).Width;
        }

        private FlatButton FindButton(string text)
        {
            for (int i = 0; i < _footer.Controls.Count; i++)
            {
                FlatButton button = _footer.Controls[i] as FlatButton;
                if (button != null && button.Text == text)
                {
                    return button;
                }
            }
            return null;
        }

        // ---------- 拖放与添加 ----------

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _drop.Hot = true;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            _drop.Hot = false;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null)
            {
                return;
            }
            AddPaths(paths);
        }

        private void AddPaths(string[] paths)
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            int added = 0;
            List<string> errors = new List<string>();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                bool duplicate = false;
                for (int p = 0; p < _packages.Count; p++)
                {
                    if (string.Equals(_packages[p].SourcePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    continue;
                }
                SkinPackage package = SkinPackage.Analyze(path);
                ApplySelectionDefaults(package);
                if (!package.Ready && package.Error.Length > 0)
                {
                    errors.Add(Path.GetFileName(path) + "：" + package.Error);
                }
                _packages.Add(package);
                added++;
            }
            Cursor = previous;
            if (added > 0)
            {
                _selected = _packages[_packages.Count - 1];
            }
            RefreshList();
            if (errors.Count > 0)
            {
                string message = "";
                for (int i = 0; i < errors.Count && i < 6; i++)
                {
                    message += "· " + errors[i] + "\n";
                }
                MessageBox.Show(this, "有些文件没能读取：\n\n" + message, "提示",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 默认勾选规则：找到载具文件夹的勾上，没找到的不勾。
        /// </summary>
        private static void ApplySelectionDefaults(SkinPackage package)
        {
            string skins = Config.UserSkinsDir;
            bool skinsOk = !string.IsNullOrEmpty(skins) && Directory.Exists(skins);
            for (int i = 0; i < package.Units.Count; i++)
            {
                DetectedUnit unit = package.Units[i];
                bool hasFolder = skinsOk && Importer.FindExistingFolder(skins, unit.UnitId) != null;
                unit.Selected = hasFolder;
            }
        }

        private void BrowseFiles()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择涂装包";
                dialog.Filter = "涂装包 (*.zip)|*.zip|所有文件 (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AddPaths(dialog.FileNames);
                }
            }
        }

        private void ResizeCards()
        {
            int width = _listPanel.ClientSize.Width - Ui.S(8);
            if (width < Ui.S(200))
            {
                width = Ui.S(200);
            }
            for (int i = 0; i < _listPanel.Controls.Count; i++)
            {
                PackageCard card = _listPanel.Controls[i] as PackageCard;
                if (card != null)
                {
                    card.Width = width;
                }
            }
            if (_listPanel.HorizontalScroll.Visible)
            {
                _listPanel.AutoScroll = false;
                _listPanel.AutoScroll = true;
            }
        }

        public void RefreshList()
        {
            _listPanel.SuspendLayout();
            List<Control> old = new List<Control>();
            for (int i = 0; i < _listPanel.Controls.Count; i++)
            {
                old.Add(_listPanel.Controls[i]);
            }
            for (int i = 0; i < old.Count; i++)
            {
                _listPanel.Controls.Remove(old[i]);
                old[i].Dispose();
            }

            for (int i = 0; i < _packages.Count; i++)
            {
                PackageCard card = new PackageCard();
                card.Package = _packages[i];
                card.Selected = _packages[i] == _selected;
                card.Margin = new Padding(0, 0, 0, Ui.GapCard);
                SkinPackage captured = _packages[i];
                card.Click += delegate
                {
                    _selected = captured;
                    RefreshList();
                };
                card.RemoveClicked += delegate
                {
                    _packages.Remove(captured);
                    if (_selected == captured)
                    {
                        _selected = _packages.Count > 0 ? _packages[0] : null;
                    }
                    RefreshList();
                };
                card.MouseClick += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        ShowCardMenu(captured);
                    }
                };
                _listPanel.Controls.Add(card);
            }
            _listPanel.ResumeLayout();
            ResizeCards();
            LayoutBody();
            UpdateDetail();
            UpdateStatus();
        }

        private void ShowCardMenu(SkinPackage package)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            SkinPackage captured = package;
            menu.Items.Add("指定载具…", null, delegate
            {
                _selected = captured;
                RefreshList();
                ManualAssign();
            });
            menu.Items.Add("移除", null, delegate
            {
                _packages.Remove(captured);
                if (_selected == captured)
                {
                    _selected = _packages.Count > 0 ? _packages[0] : null;
                }
                RefreshList();
            });
            menu.Show(Cursor.Position);
        }

        private void UpdateDetail()
        {
            for (int i = _vehPanel.Controls.Count - 1; i >= 0; i--)
            {
                _vehPanel.Controls[i].Dispose();
            }

            if (_selected == null)
            {
                // 清空后回到右栏引导区，不残留上一份详情。
                _detailNameText = "";
                _detailMetaText = "";
                _detailMetaColor = Ui.TextDim;
                _detailSectionText = "";
                _detailHintText = "";
                _detailHintColor = Ui.TextFaint;
                _detailRefreshVisible = false;
                _detailManualVisible = false;
                SetDetailGuide(true);
                _detail.Invalidate();
                return;
            }

            SetDetailGuide(false);
            _detailNameText = _selected.Name;
            if (_selected.Error.Length > 0)
            {
                _detailMetaText = _selected.Error;
                _detailMetaColor = Ui.Bad;
            }
            else
            {
                string sourceName = _selected.SourcePath;
                try
                {
                    sourceName = Path.GetFileName(sourceName.TrimEnd('\\'));
                }
                catch
                {
                }
                _detailMetaText = _selected.FilesText + "  ·  " + _selected.SizeText + "\n来源：" + sourceName;
                _detailMetaColor = Ui.TextDim;
            }

            List<RowData> rows = new List<RowData>();
            for (int i = 0; i < _selected.Units.Count; i++)
            {
                DetectedUnit unit = _selected.Units[i];
                RowData data = new RowData();
                data.Unit = unit;
                data.Folder = Importer.FindExistingFolder(Config.UserSkinsDir, unit.UnitId);
                data.History = ImportHistory.Lookup(_selected.Fingerprint, unit.UnitId);
                rows.Add(data);
            }
            rows.Sort(delegate(RowData a, RowData b)
            {
                int ra = a.Folder == null ? 1 : 0;
                int rb = b.Folder == null ? 1 : 0;
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                return NationInfo.CompareUnits(a.Unit, b.Unit);
            });
            List<DetectedUnit> unitsForNames = new List<DetectedUnit>();
            for (int i = 0; i < rows.Count; i++)
            {
                unitsForNames.Add(rows[i].Unit);
            }
            NameMap.ApplyUniqueTitles(unitsForNames);

            int titleMax = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int w = TextRenderer.MeasureText(rows[i].Unit.ShortTitle, Ui.BodyBold).Width;
                if (w > titleMax)
                {
                    titleMax = w;
                }
            }
            int rowWidth = _vehPanel.ClientSize.Width - Ui.S(18);
            int titleWidth = Math.Min(titleMax + Ui.S(8), (int)(rowWidth * 0.55));
            if (titleWidth < Ui.S(120))
            {
                titleWidth = Ui.S(120);
            }

            int found = 0;
            int total = rows.Count;
            int top = Ui.S(2);
            for (int i = 0; i < rows.Count; i++)
            {
                RowData data = rows[i];
                DetectedUnit unit = data.Unit;
                bool hasFolder = data.Folder != null;
                if (hasFolder)
                {
                    found++;
                }

                CheckRow row = new CheckRow();
                row.Width = _vehPanel.ClientSize.Width - Ui.S(18);
                row.Left = Ui.S(8);
                row.Top = top;
                row.BackColor = Ui.CardBg;
                row.Title = unit.ShortTitle;
                row.NationCode = unit.Vehicle != null ? unit.Vehicle.Nation : "";
                row.Country = NameMap.NationName(row.NationCode);
                row.Model = unit.UnitId;
                row.LevelWeight = VehicleMeta.Display(unit.UnitId);
                row.TitleWidth = titleWidth;
                row.Folder = hasFolder ? Path.GetFileName(data.Folder) : "";
                row.Height = Ui.S(46);
                if (!hasFolder)
                {
                    row.Badge = "未识别到文件夹";
                    row.BadgeKind = 2;
                    row.Highlight = false;
                }
                else
                {
                    row.Highlight = true;
                    if (data.History != null)
                    {
                        row.Badge = "上次导入";
                        row.BadgeKind = 1;
                    }
                    else
                    {
                        row.Badge = "准备就绪";
                        row.BadgeKind = 3;
                    }
                }
                row.Checked = unit.Selected;
                DetectedUnit captured = unit;
                CheckRow capturedRow = row;
                row.Click += delegate
                {
                    captured.Selected = capturedRow.Checked;
                    UpdateStatus();
                };
                row.MouseEnter += delegate { ShowDetailCard(capturedRow); };
                row.MouseLeave += delegate { HideDetailCard(); };
                _vehPanel.Controls.Add(row);
                top += row.Height + Ui.S(4);
            }

            if (_selected.UnknownIds.Count > 0)
            {
                Label unknown = new Label();
                unknown.Left = Ui.S(10);
                unknown.Top = top + Ui.S(4);
                unknown.Width = _vehPanel.ClientSize.Width - Ui.S(18);
                unknown.Height = Ui.S(54);
                unknown.ForeColor = Ui.Warn;
                unknown.Font = Ui.Small;
                unknown.BackColor = Ui.CardBg;
                unknown.Text = "还有 " + _selected.UnknownIds.Count + " 个 .blk 没认出来：" +
                               JoinIds(_selected.UnknownIds) + "\n可以手动指定载具。";
                _vehPanel.Controls.Add(unknown);
            }

            int selectedHere = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Unit.Selected)
                {
                    selectedHere++;
                }
            }
            _detailSectionText = total == 0
                ? "该涂装包可导入的载具（0）"
                : "该涂装包可导入的载具（" + total + "） · 可导入 " + selectedHere + "/" + total;

            bool missing = total > 0 && found < total;
            _detailManualVisible = total == 0 || _selected.UnknownIds.Count > 0;
            _detailRefreshVisible = true;
            _vehPanel.PerformLayout();
            if (_scrollCover != null)
            {
                _scrollCover.Visible = top > _vehPanel.ClientSize.Height ||
                                       _vehPanel.VerticalScroll.Visible;
                _scrollCover.BringToFront();
                _scrollCover.Invalidate();
            }
            Native.ThemeScrollbars(_vehPanel);
            Native.ThemeScrollbars(_listPanel);

            if (missing)
            {
                _detailHintText = "黄色行是找到专属文件夹、可以直接导入的；黑色行是未识别，\n需要先在游戏里给该载具创建模板，再点「刷新文件夹」。";
                _detailHintColor = Ui.Warn;
            }
            else
            {
                _detailHintText = "黄色行是找到专属文件夹、可以直接导入的；\n导入前会把旧内容备份到安装目录。";
                _detailHintColor = Ui.TextFaint;
            }
            ApplyDetailChildren();
            _detail.Invalidate();
        }

        private void ShowDetailCard(CheckRow row)
        {
            if (row == null)
            {
                return;
            }
            if (_detailCard == null)
            {
                _detailCard = new DetailCard();
            }
            string folder = row.Folder;
            bool exists = folder.Length > 0;
            if (!exists && row.Model.Length > 0)
            {
                folder = "template_" + row.Model;
            }
            Vehicle vehicle = VehicleDb.Find(row.Model);
            DetailInfo info = new DetailInfo();
            info.ShortName = row.Title;
            info.FullName = vehicle != null ? vehicle.Zh : row.Title;
            info.NationCode = vehicle != null ? vehicle.Nation : row.NationCode;
            info.Country = NameMap.NationName(info.NationCode);
            info.Model = row.Model;
            info.Folder = folder;
            info.FolderExists = exists;
            info.LevelWeight = VehicleMeta.Display(row.Model);
            info.LevelWeightModes = VehicleMeta.ModesLine(row.Model);
            info.Status = string.IsNullOrEmpty(row.Badge) ? (exists ? "已启用" : "未识别") : row.Badge;
            info.StatusKind = row.BadgeKind == 2 ? 2 : (row.BadgeKind == 1 ? 1 : (exists ? 1 : 3));
            _detailCard.UnitId = row.Model;
            _detailCard.SetData(info);
            // 只读随包资源；未知载具使用同一张占位图。
            _detailCard.SetIcon(VehicleIcons.Get(row.Model));
            Point p = row.PointToScreen(new Point(Ui.S(6), row.Height + Ui.S(6)));
            _detailCard.ShowAt(p, Bounds);
        }

        private void HideDetailCard()
        {
            if (_detailCard != null)
            {
                _detailCard.HideCard();
            }
        }

        private class RowData
        {
            public DetectedUnit Unit;
            public string Folder;
            public HistoryEntry History;
        }

        private void RelayoutRows()
        {
            if (_vehPanel == null)
            {
                return;
            }
            int width = _vehPanel.ClientSize.Width - Ui.S(26);
            int top = Ui.S(2);
            for (int i = 0; i < _vehPanel.Controls.Count; i++)
            {
                CheckRow row = _vehPanel.Controls[i] as CheckRow;
                if (row == null)
                {
                    continue;
                }
                row.Width = width;
                row.Height = Ui.S(46);
                row.Left = Ui.S(8);
                row.Top = top;
                top += row.Height + Ui.S(3);
            }
            if (_scrollCover != null)
            {
                _scrollCover.Visible = top > _vehPanel.ClientSize.Height ||
                                       _vehPanel.VerticalScroll.Visible;
                _scrollCover.BringToFront();
                _scrollCover.Invalidate();
            }
        }

        private static string JoinIds(List<string> ids)
        {
            string s = "";
            for (int i = 0; i < ids.Count && i < 4; i++)
            {
                if (i > 0)
                {
                    s += "、";
                }
                s += ids[i];
            }
            if (ids.Count > 4)
            {
                s += " 等";
            }
            return s;
        }

        private void UpdateStatus()
        {
            int selectedCount = 0;
            int missing = 0;
            for (int i = 0; i < _packages.Count; i++)
            {
                List<DetectedUnit> units = _packages[i].SelectedUnits();
                for (int u = 0; u < units.Count; u++)
                {
                    selectedCount++;
                    if (Importer.FindExistingFolder(Config.UserSkinsDir, units[u].UnitId) == null)
                    {
                        missing++;
                    }
                }
            }
            if (_packages.Count == 0)
            {
                _status.Text = "准备就绪 · 拖入涂装包开始";
            }
            else
            {
                _status.Text = "已添加 " + _packages.Count + " 个包 · 可导入 " + selectedCount + " 个载具";
                if (missing > 0)
                {
                    _status.Text += " · " + missing + " 个还没建文件夹";
                }
            }
            if (_selected != null)
            {
                int curTotal = _selected.Units.Count;
                int curSelected = 0;
                for (int i = 0; i < curTotal; i++)
                {
                    if (_selected.Units[i].Selected)
                    {
                        curSelected++;
                    }
                }
                _detailSectionText = curTotal == 0
                    ? "该涂装包可导入的载具（0）"
                    : "该涂装包可导入的载具（" + curTotal + "） · 可导入 " + curSelected + "/" + curTotal;
                _detail.Invalidate();
            }
            _importButton.Text = selectedCount > 0 ? "导入选中（" + selectedCount + "）" : "导入选中";
            _importButton.Enabled = selectedCount > 0;
            if (_footer != null && _footer.Width > 0)
            {
                LayoutFooter();
            }
        }

        private void RefreshFolders(bool notify)
        {
            UpdateDetail();
            UpdateStatus();
            if (notify)
            {
                int found = 0;
                int total = 0;
                for (int i = 0; i < _packages.Count; i++)
                {
                    List<DetectedUnit> units = _packages[i].SelectedUnits();
                    for (int u = 0; u < units.Count; u++)
                    {
                        total++;
                        if (Importer.FindExistingFolder(Config.UserSkinsDir, units[u].UnitId) != null)
                        {
                            found++;
                        }
                    }
                }
                _status.Text = "已重新检查：" + found + " / " + total + " 个载具已有文件夹";
            }
        }

        private void ManualAssign()
        {
            if (_selected == null)
            {
                return;
            }
            string suggested = _selected.UnknownIds.Count > 0 ? _selected.UnknownIds[0] : "";
            using (VehiclePickerForm picker = new VehiclePickerForm(suggested))
            {
                if (picker.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                Vehicle vehicle = picker.SelectedVehicle;
                string unitId = picker.ManualUnitId;
                if (string.IsNullOrEmpty(unitId))
                {
                    return;
                }
                for (int i = 0; i < _selected.Units.Count; i++)
                {
                    if (string.Equals(_selected.Units[i].UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, "这个载具已经在列表里了。", "提示",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }
                DetectedUnit unit = new DetectedUnit();
                unit.UnitId = unitId;
                unit.Vehicle = vehicle;
                unit.Confidence = 100;
                unit.Manual = true;
                unit.Selected = true;
                unit.BlkRel = _selected.FirstBlkRel();
                _selected.Units.Add(unit);
                _selected.UnknownIds.RemoveAll(delegate(string id)
                {
                    return string.Equals(id, unitId, StringComparison.OrdinalIgnoreCase);
                });
                RefreshList();
            }
        }

        private void StartImport()
        {
            if (string.IsNullOrEmpty(Config.UserSkinsDir) || !Directory.Exists(Config.UserSkinsDir))
            {
                MessageBox.Show(this, "还没有设置正确的游戏目录，请先在「设置」里选择 War Thunder 安装目录。",
                                "找不到 UserSkins", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OpenSettings();
                return;
            }
            List<SkinPackage> selected = new List<SkinPackage>();
            for (int i = 0; i < _packages.Count; i++)
            {
                if (_packages[i].SelectedUnits().Count > 0)
                {
                    selected.Add(_packages[i]);
                }
            }
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "还没有可导入的涂装。先拖入涂装包，并勾选载具。", "没有可导入的内容",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ValidationResult validation = ImportValidation.ClassifyAll(selected);
            if (validation.HasIllegal)
            {
                using (AbnormalFilesForm abnormal = new AbnormalFilesForm(validation))
                {
                    if (abnormal.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                }
            }

            List<DetectedUnit> missing = CollectMissing(selected);
            if (missing.Count > 0)
            {
                using (MissingFoldersForm form = new MissingFoldersForm(missing))
                {
                    form.ShowDialog(this);
                    if (!form.AllFound)
                    {
                        UpdateDetail();
                        UpdateStatus();
                        return;
                    }
                }
                RefreshFolders(false);
                missing = CollectMissing(selected);
                if (missing.Count > 0)
                {
                    return;
                }
            }

            ImportPlan plan = Importer.Build(selected);
            if (plan.Missing.Count > 0)
            {
                MessageBox.Show(this, "还有载具没有文件夹，请先在游戏里创建。", "提示",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (plan.Targets.Count == 0)
            {
                MessageBox.Show(this, "没有生成任何导入目标。", "提示",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (ImportPlanForm confirm = new ImportPlanForm(plan))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            ImportResult result = null;
            using (ProgressForm progress = new ProgressForm(plan))
            {
                progress.ShowDialog(this);
                result = progress.Result;
            }
            if (result == null)
            {
                return;
            }

            // 只记录真正成功的目标，取消/失败的不写入导入记录。
            HashSet<string> doneDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.Done.Count; i++)
            {
                int arrow = result.Done[i].LastIndexOf(" → ", StringComparison.Ordinal);
                if (arrow > 0)
                {
                    doneDirs.Add(result.Done[i].Substring(arrow + 3).Trim());
                }
            }
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                if (!doneDirs.Contains(Path.GetFileName(plan.Targets[i].Folder)))
                {
                    continue;
                }
                ImportHistory.Record(plan.Targets[i].Package.Fingerprint,
                                     plan.Targets[i].Unit.UnitId,
                                     plan.Targets[i].Folder);
            }
            Config.Log("导入 " + plan.Targets.Count + " 个载具，成功 " + result.Done.Count +
                       "，失败 " + result.Failed.Count);
            for (int i = 0; i < result.Done.Count; i++)
            {
                Config.Log("  OK  " + result.Done[i]);
            }
            for (int i = 0; i < result.Failed.Count; i++)
            {
                Config.Log("  FAIL  " + result.Failed[i]);
            }

            string subtitle = result.Failed.Count == 0
                ? "已导入 " + result.Done.Count + " 个载具文件夹"
                : "完成 " + result.Done.Count + " 个，失败 " + result.Failed.Count + " 个";
            using (ResultForm done = new ResultForm(result, "导入完成", subtitle))
            {
                done.ShowDialog(this);
            }

            if (validation.Warn.Count > 0 || validation.Illegal.Count > 0)
            {
                string note = "";
                if (validation.Warn.Count > 0)
                {
                    note += "已忽略 " + validation.Warn.Count + " 个说明文件（不会安装）：\n" +
                            string.Join("\n", validation.Warn.ToArray()) + "\n\n";
                }
                if (validation.Illegal.Count > 0)
                {
                    note += "本次包含 " + validation.Illegal.Count + " 个未通过校验的文件：\n" +
                            string.Join("\n", validation.Illegal.ToArray());
                }
                MessageBox.Show(this, note.Trim(), "导入提示", MessageBoxButtons.OK,
                                validation.Illegal.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            // 不清理列表：导入后立刻能看到“上次导入”标记和最新文件夹状态。
            RefreshList();
        }

        private static List<DetectedUnit> CollectMissing(List<SkinPackage> packages)
        {
            List<DetectedUnit> missing = new List<DetectedUnit>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < packages.Count; i++)
            {
                List<DetectedUnit> units = packages[i].SelectedUnits();
                for (int u = 0; u < units.Count; u++)
                {
                    DetectedUnit unit = units[u];
                    if (seen.Contains(unit.UnitId))
                    {
                        continue;
                    }
                    seen.Add(unit.UnitId);
                    if (Importer.FindExistingFolder(Config.UserSkinsDir, unit.UnitId) == null)
                    {
                        missing.Add(unit);
                    }
                }
            }
            return missing;
        }

        private void OpenSettings()
        {
            OpenSettings(0);
        }

        private void OpenSettings(int startPage)
        {
            using (SettingsForm settings = new SettingsForm(startPage))
            {
                DialogResult result = settings.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    _header.Invalidate();
                    RefreshPathChip();
                    UpdateDetail();
                    UpdateStatus();
                    if (settings.NeedRestart)
                    {
                        Application.Restart();
                        return;
                    }
                }
            }
        }

        private void OpenUserSkins()
        {
            string dir = Config.UserSkinsDir;
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, "UserSkins 文件夹不存在，请检查游戏目录设置。", "提示",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch
            {
            }
        }
    }
}
