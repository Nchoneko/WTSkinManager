using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>左侧的涂装包卡片。</summary>
    internal class PackageCard : Control
    {
        public SkinPackage Package;
        public bool Selected;
        public event EventHandler RemoveClicked;

        private float _hoverT;
        private bool _hoverTarget;
        private bool _closeHover;
        private Timer _timer;

        public PackageCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Ui.S(76);
            Cursor = Cursors.Hand;
            BackColor = Ui.Bg;
        }

        private Rectangle CloseRect()
        {
            int size = Ui.S(26);
            return new Rectangle(Width - size - Ui.S(10), Ui.S(10), size, size);
        }

        private void Animate()
        {
            if (!Ui.Animations)
            {
                _hoverT = _hoverTarget ? 1f : 0f;
                Invalidate();
                return;
            }
            if (_timer == null)
            {
                _timer = new Timer();
                _timer.Interval = 16;
                _timer.Tick += delegate
                {
                    float target = _hoverTarget ? 1f : 0f;
                    float step = 0.16f;
                    if (Math.Abs(_hoverT - target) <= step)
                    {
                        _hoverT = target;
                        _timer.Stop();
                    }
                    else
                    {
                        _hoverT += _hoverT < target ? step : -step;
                    }
                    Invalidate();
                };
            }
            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color baseFill = Selected ? Ui.CardSel : Ui.CardBg;
            Color fill = Ui.Mix(baseFill, Selected ? Ui.PanelActive : Ui.PanelHover, _hoverT);
            Color border = Selected ? Ui.Accent : Ui.Mix(Ui.LineSoft, Ui.Line, _hoverT);
            Ui.FillRound(g, r, Ui.S(10), fill);
            Ui.DrawRound(g, r, Ui.S(10), border, Selected ? 1.6f : 1f);

            int badge = Ui.S(38);
            Rectangle badgeRect = new Rectangle(Ui.S(14), (Height - badge) / 2, badge, badge);
            Color badgeColor = Ui.PanelActive;
            if (Package != null && Package.Ready)
            {
                badgeColor = Package.Units.Count > 0 ? Ui.T.BadgeGoodBg : Ui.T.BadgeWarnBg;
            }
            else if (Package != null && Package.Error.Length > 0)
            {
                badgeColor = Ui.T.BadgeBadBg;
            }
            Ui.FillRound(g, badgeRect, Ui.S(9), badgeColor);

            string badgeText = "?";
            Color badgeTextColor = Ui.TextDim;
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
                    badgeTextColor = Ui.Accent;
                }
                else
                {
                    badgeText = "?";
                    badgeTextColor = Ui.Warn;
                }
            }
            Ui.DrawText(g, badgeText, Ui.H1, badgeTextColor, badgeRect, Ui.Center);

            int textX = badgeRect.Right + Ui.S(13);
            int rightReserve = Ui.S(96);
            Rectangle nameRect = new Rectangle(textX, Ui.S(12), Width - textX - rightReserve, Ui.S(24));
            string name = Package == null ? "" : Package.Name;
            Ui.DrawText(g, name, Ui.BodyBold, Ui.Text, nameRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            string meta;
            Color metaColor = Ui.TextFaint;
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
                    metaColor = Ui.TextDim;
                }
            }
            Rectangle metaRect = new Rectangle(textX, Ui.S(39), Width - textX - rightReserve, Ui.S(20));
            Ui.DrawText(g, meta, Ui.Small, metaColor, metaRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            Rectangle close = CloseRect();
            if (_closeHover)
            {
                Ui.FillRound(g, close, Ui.S(6), Ui.T.CloseHoverBg);
            }
            using (Pen pen = new Pen(_closeHover ? Ui.T.CloseHoverFg : Ui.TextFaint, 1.5f))
            {
                int pad = Ui.S(8);
                g.DrawLine(pen, close.Left + pad, close.Top + pad, close.Right - pad, close.Bottom - pad);
                g.DrawLine(pen, close.Right - pad, close.Top + pad, close.Left + pad, close.Bottom - pad);
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
            _hoverTarget = true;
            Animate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverTarget = false;
            _closeHover = false;
            Animate();
            base.OnMouseLeave(e);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null)
            {
                _timer.Dispose();
                _timer = null;
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
        private Label _detailName;
        private Label _detailMeta;
        private Label _detailSection;
        private Label _detailHint;
        private FlatButton _manualButton;
        private FlatButton _refreshButton;
        private Label _status;
        private FlatButton _importButton;
        private Label _emptyLabel;
        private Bitmap _logo;

        private bool _dragging;
        private Rectangle _versionRect;
        private Rectangle _githubRect;
        private Bitmap _githubMark;
        private bool _githubHover;
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
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }

        // ---------------- 自绘窗口按钮 / 拖动 / 缩放 ----------------

        private Rectangle WinButtonRect(int index)
        {
            int w = Ui.S(46);
            int h = Ui.S(36);
            int right = ClientSize.Width - Ui.S(8);
            int x = right - (3 - index) * w;
            return new Rectangle(x, (Ui.S(80) - h) / 2, w, h);
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
            if (GameChipRect().Contains(p))
            {
                return true;
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
            Native.ResizeWindowNoCopy(this, b);
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
            LayoutChrome();
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
            if (GameChipRect().Contains(e.Location))
            {
                OpenSettings();
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

        private void LayoutChrome()
        {
            if (_header == null)
            {
                return;
            }
            int headerHeight = Ui.S(80);
            int tabHeight = Ui.S(44);
            int footerHeight = _footer.Visible ? Ui.S(76) : 0;
            int bannerHeight = _updateBanner != null && _updateBanner.Visible ? Ui.S(40) : 0;
            _header.SetBounds(0, 0, ClientSize.Width, headerHeight);
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
            _header.Invalidate();
            _footer.Invalidate();
            LayoutFooter();
            LayoutBody();
            LayoutResizeEdges();
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
        }

        private void PaintWindowButtons(Graphics g)
        {
            for (int i = 0; i < 3; i++)
            {
                Rectangle r = WinButtonRect(i);
                bool close = i == 2;
                if (_winHover == i)
                {
                    Ui.FillRound(g, r, Ui.S(6), close ? Ui.T.CloseHoverBg : Ui.PanelHover);
                }
                Color color = (_winHover == i && close) ? Ui.T.CloseHoverFg : Ui.TextDim;
                if (i == 0)
                {
                    using (Pen pen = new Pen(color, Math.Max(1.2f, Ui.Scale * 1.4f)))
                    {
                        g.DrawLine(pen, r.X + Ui.S(17), r.Y + r.Height / 2, r.Right - Ui.S(17), r.Y + r.Height / 2);
                    }
                }
                else if (i == 1)
                {
                    using (Pen pen = new Pen(color, Math.Max(1.2f, Ui.Scale * 1.4f)))
                    {
                        if (_maximizedByUser || WindowState == FormWindowState.Maximized)
                        {
                            Rectangle back = new Rectangle(r.X + Ui.S(16), r.Y + Ui.S(12), Ui.S(12), Ui.S(11));
                            Rectangle front = new Rectangle(r.X + Ui.S(19), r.Y + Ui.S(15), Ui.S(12), Ui.S(11));
                            g.DrawRectangle(pen, back);
                            using (SolidBrush fill = new SolidBrush(Ui.Bg))
                            {
                                g.FillRectangle(fill, new Rectangle(front.X, front.Y, front.Width - 1, front.Height - 1));
                            }
                            g.DrawRectangle(pen, front);
                        }
                        else
                        {
                            g.DrawRectangle(pen, r.X + Ui.S(17), r.Y + Ui.S(12), Ui.S(12), Ui.S(11));
                        }
                    }
                }
                else
                {
                    using (Pen pen = new Pen(color, Math.Max(1.3f, Ui.Scale * 1.5f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, r.X + Ui.S(17), r.Y + Ui.S(12), r.Right - Ui.S(17), r.Bottom - Ui.S(12));
                        g.DrawLine(pen, r.Right - Ui.S(17), r.Y + Ui.S(12), r.X + Ui.S(17), r.Bottom - Ui.S(12));
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
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, Ui.S(24), _header.Height - 1, _header.Width - Ui.S(24), _header.Height - 1);
            }

            int iconSize = Ui.S(40);
            int ix = Ui.S(24);
            int iy = (_header.Height - iconSize) / 2 - Ui.S(2);
            Rectangle iconRect = new Rectangle(ix, iy, iconSize, iconSize);
            Bitmap logo = LoadLogo();
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, iconRect);
            }
            else
            {
                Ui.FillRound(g, iconRect, Ui.S(10), Ui.Accent);
            }

            int tx = iconRect.Right + Ui.S(14);
            Ui.DrawText(g, "战雷涂装管理器", Ui.Title, Ui.Text,
                    new Rectangle(tx, iy - Ui.S(1), Ui.S(320), Ui.S(28)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            const string subtitle = "War Thunder 自定义涂装 · 识别 / 替换 / 备份";
            Ui.DrawText(g, subtitle, Ui.Small, Ui.TextDim,
                    new Rectangle(tx, iy + Ui.S(27), Ui.S(430), Ui.S(18)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (_githubMark == null)
            {
                _githubMark = LoadGithubMark();
            }
            Size subtitleSize = TextRenderer.MeasureText(subtitle, Ui.Small);
            int ghSize = Ui.S(18);
            int ghX = tx + subtitleSize.Width + Ui.S(8);
            int ghY = iy + Ui.S(27) + (Ui.S(18) - ghSize) / 2;
            if (_githubMark != null)
            {
                Rectangle gh = new Rectangle(ghX, ghY, ghSize, ghSize);
                Color ghColor = _githubHover ? Ui.Accent : Ui.TextDim;
                ImageAttributes attrs = new ImageAttributes();
                ColorMatrix matrix = new ColorMatrix(new float[][]
                {
                    new float[] { ghColor.R / 255f, 0, 0, 0, 0 },
                    new float[] { 0, ghColor.G / 255f, 0, 0, 0 },
                    new float[] { 0, 0, ghColor.B / 255f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });
                attrs.SetColorMatrix(matrix);
                g.DrawImage(_githubMark, gh, 0, 0, _githubMark.Width, _githubMark.Height, GraphicsUnit.Pixel, attrs);
                attrs.Dispose();
                _githubRect = gh;
            }
            else
            {
                _githubRect = Rectangle.Empty;
            }

            // 版本号 + 构建时间：紧接标题右侧，同一行，弱化色
            string versionText = BuildInfo.HeaderText;
            Size titleSize = TextRenderer.MeasureText("战雷涂装管理器", Ui.Title);
            int versionX = tx + titleSize.Width + Ui.S(8);
            Size versionSize = TextRenderer.MeasureText(versionText, Ui.Small);
            Rectangle versionBound = GameChipRect();
            int versionMaxRight = versionBound.Width > 0 ? versionBound.Left - Ui.S(12) : _header.Width - Ui.S(24);
            if (versionX + versionSize.Width <= versionMaxRight)
            {
                Ui.DrawText(g, versionText, Ui.Small, Ui.TextFaint,
                        new Rectangle(versionX, iy, versionSize.Width + Ui.S(2), Ui.S(28)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                _versionRect = new Rectangle(versionX, iy, versionSize.Width + Ui.S(2), Ui.S(28));
            }
            else
            {
                _versionRect = Rectangle.Empty;
            }

            Rectangle chip = GameChipRect();
            if (chip.Width >= Ui.S(180) && _header.Width >= Ui.S(1120))
            {
                Ui.FillRound(g, chip, Ui.S(9), Ui.Panel);
                Ui.DrawRound(g, chip, Ui.S(9), Ui.LineSoft, 1f);
                using (SolidBrush brush = new SolidBrush(Config.GameDirLooksValid(Config.GameDir) ? Ui.Good : Ui.Bad))
                {
                    g.FillEllipse(brush, chip.X + Ui.S(13), chip.Y + chip.Height / 2 - Ui.S(4), Ui.S(8), Ui.S(8));
                }
                string dir = Config.GameDir;
                string label = string.IsNullOrEmpty(dir) ? "未设置游戏目录" : ShortPath(dir);
                Ui.DrawText(g, label, Ui.Small, Ui.Text, new Rectangle(chip.X + Ui.S(28), chip.Y, chip.Width - Ui.S(74), chip.Height),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                Ui.DrawText(g, "更改", Ui.Small, Ui.Accent, new Rectangle(chip.Right - Ui.S(52), chip.Y, Ui.S(44), chip.Height),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
            int height = Ui.S(38);
            return new Rectangle(_header.Width - width - Ui.S(156), (_header.Height - height) / 2 - Ui.S(2), width, height);
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
            _body.BackColor = Ui.Bg;
            _body.Resize += delegate { LayoutBody(); };
            Controls.Add(_body);

            _drop = new DropZone();
            _drop.Main = "把涂装包拖到这里";
            _drop.Sub = "支持 .zip 压缩包或已解压的文件夹，可以一次拖入多个";
            _drop.Cursor = Cursors.Hand;
            _drop.Click += delegate { BrowseFiles(); };
            _body.Controls.Add(_drop);

            _detail = new BufferedPanel();
            _detail.BackColor = Ui.Bg;
            _detail.Paint += PaintDetail;
            _detail.Resize += delegate { LayoutDetail(); };
            _body.Controls.Add(_detail);
            BuildDetail();

            _listPanel = new FlowLayoutPanel();
            _listPanel.FlowDirection = FlowDirection.TopDown;
            _listPanel.WrapContents = false;
            _listPanel.AutoScroll = true;
            _listPanel.BackColor = Ui.Bg;
            _listPanel.Padding = new Padding(0, Ui.S(14), 0, 0);
            _listPanel.Resize += delegate { ResizeCards(); };
            _body.Controls.Add(_listPanel);

            _emptyLabel = new Label();
            _emptyLabel.ForeColor = Ui.TextFaint;
            _emptyLabel.Font = Ui.Body;
            _emptyLabel.AutoSize = false;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.BackColor = Ui.Bg;
            _emptyLabel.Text = "还没有添加涂装包。\n\n拖入 .zip，或点击上面的区域选择文件。\n程序会自动认出涂装属于哪台载具，并在导入前询问你。";
            _listPanel.Controls.Add(_emptyLabel);
        }

        private void LayoutBody()
        {
            if (_body == null || _drop == null)
            {
                return;
            }
            int padX = Ui.S(24);
            int padTop = Ui.S(16);
            int gap = Ui.S(18);
            int width = _body.ClientSize.Width;
            int height = _body.ClientSize.Height;
            int detailWidth = Math.Min(Ui.S(560), Math.Max(Ui.S(380), width - padX * 2 - gap - Ui.S(320)));
            int leftWidth = width - padX * 2 - detailWidth - gap;
            if (leftWidth < Ui.S(300))
            {
                leftWidth = Ui.S(300);
                detailWidth = Math.Max(Ui.S(300), width - padX * 2 - leftWidth - gap);
            }
            int dropHeight = Ui.S(92);
            _drop.SetBounds(padX, padTop, leftWidth, dropHeight);
            _listPanel.SetBounds(padX, padTop + dropHeight + Ui.S(12), leftWidth,
                                 Math.Max(Ui.S(80), height - padTop * 2 - dropHeight - Ui.S(12)));
            _detail.SetBounds(width - padX - detailWidth, Ui.S(2), detailWidth, Math.Max(Ui.S(200), height - Ui.S(6)));
            _drop.Invalidate();
            _listPanel.Invalidate();
            _detail.Invalidate();
            LayoutDetail();
        }

        private void BuildDetail()
        {
            _detailName = new Label();
            _detailName.Font = Ui.H1;
            _detailName.ForeColor = Ui.Text;
            _detailName.BackColor = Ui.CardBg;
            _detailName.AutoEllipsis = true;
            _detail.Controls.Add(_detailName);

            _detailMeta = new Label();
            _detailMeta.Font = Ui.Small;
            _detailMeta.ForeColor = Ui.TextDim;
            _detailMeta.BackColor = Ui.CardBg;
            _detailMeta.AutoEllipsis = true;
            _detail.Controls.Add(_detailMeta);

            _detailSection = new Label();
            _detailSection.Font = Ui.SmallBold;
            _detailSection.ForeColor = Ui.TextDim;
            _detailSection.BackColor = Ui.CardBg;
            _detail.Controls.Add(_detailSection);

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

            _detailHint = new Label();
            _detailHint.Font = Ui.Small;
            _detailHint.ForeColor = Ui.TextFaint;
            _detailHint.BackColor = Ui.CardBg;
            _detail.Controls.Add(_detailHint);

            _refreshButton = new FlatButton();
            _refreshButton.Text = "刷新文件夹";
            _refreshButton.BackColor = Ui.CardBg;
            _refreshButton.Click += delegate { RefreshFolders(true); };
            _detail.Controls.Add(_refreshButton);

            _manualButton = new FlatButton();
            _manualButton.Text = "手动指定载具…";
            _manualButton.Primary = true;
            _manualButton.BackColor = Ui.CardBg;
            _manualButton.Visible = false;
            _manualButton.Click += delegate { ManualAssign(); };
            _detail.Controls.Add(_manualButton);
        }

        private void LayoutDetail()
        {
            if (_detail == null || _detailName == null)
            {
                return;
            }
            int width = _detail.ClientSize.Width;
            int height = _detail.ClientSize.Height;
            if (width <= 0)
            {
                return;
            }
            _detailName.SetBounds(Ui.S(20), Ui.S(40), width - Ui.S(40), Ui.S(46));
            _detailMeta.SetBounds(Ui.S(20), Ui.S(90), width - Ui.S(40), Ui.S(42));
            _detailSection.SetBounds(Ui.S(20), Ui.S(148), width - Ui.S(40), Ui.S(22));
            int hintTop = height - Ui.S(146);
            _detailHint.SetBounds(Ui.S(20), hintTop, width - Ui.S(40), Ui.S(52));
            _refreshButton.SetBounds(Ui.S(20), height - Ui.S(84), Ui.S(132), Ui.S(38));
            _manualButton.SetBounds(Ui.S(160), height - Ui.S(84), width - Ui.S(180), Ui.S(38));
            int vehTop = Ui.S(176);
            int vehHeight = Math.Max(Ui.S(60), hintTop - vehTop - Ui.S(10));
            _vehPanel.SetBounds(Ui.S(10), vehTop, width - Ui.S(20), vehHeight);
            if (_scrollCover == null)
            {
                _scrollCover = new ScrollCover(_vehPanel);
                _detail.Controls.Add(_scrollCover);
            }
            _scrollCover.SetBounds(_vehPanel.Right - Ui.S(15), vehTop, Ui.S(15), vehHeight);
            _scrollCover.Visible = _vehPanel.VerticalScroll.Visible;
            _scrollCover.BringToFront();
        }

        private void PaintDetail(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, Ui.S(12), _detail.Width - 1, _detail.Height - Ui.S(18));
            Ui.FillRound(g, r, Ui.S(12), Ui.CardBg);
            Ui.DrawRound(g, r, Ui.S(12), Ui.LineSoft, 1f);

            Ui.DrawText(g, "涂装包详情", Ui.SmallBold, Ui.TextFaint,
                    new Rectangle(Ui.S(20), r.Y + Ui.S(12), Ui.S(200), Ui.S(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (_selected == null)
            {
                Ui.DrawText(g, "选择左侧的涂装包查看识别结果", Ui.Body, Ui.TextFaint,
                        new Rectangle(Ui.S(20), r.Y + r.Height / 2 - Ui.S(20), _detail.Width - Ui.S(40), Ui.S(40)),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, Ui.S(20), r.Y + Ui.S(128), _detail.Width - Ui.S(20), r.Y + Ui.S(128));
            }
        }

        private void BuildFooter()
        {
            _footer = new BufferedPanel();
            _footer.BackColor = Ui.Bg;
            _footer.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Ui.LineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, Ui.S(24), 0, _footer.Width - Ui.S(24), 0);
                }
            };
            Controls.Add(_footer);

            _status = new Label();
            _status.Font = Ui.Small;
            _status.ForeColor = Ui.TextDim;
            _status.BackColor = Ui.Bg;
            _status.Text = "准备就绪";
            _footer.Controls.Add(_status);

            _importButton = new FlatButton();
            _importButton.Text = "导入选中";
            _importButton.Primary = true;
            _importButton.BackColor = Ui.Bg;
            _importButton.Click += delegate { StartImport(); };
            _footer.Controls.Add(_importButton);

            FlatButton open = new FlatButton();
            open.Text = "打开UserSkins文件夹";
            open.BackColor = Ui.Bg;
            open.Click += delegate { OpenUserSkins(); };
            _footer.Controls.Add(open);

            FlatButton settings = new FlatButton();
            settings.Text = "设置";
            settings.BackColor = Ui.Bg;
            settings.Click += delegate { OpenSettings(); };
            _footer.Controls.Add(settings);

            FlatButton clear = new FlatButton();
            clear.Text = "清空列表";
            clear.BackColor = Ui.Bg;
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
            int width = _footer.ClientSize.Width;
            int top = Ui.S(19);
            _status.SetBounds(Ui.S(24), Ui.S(22), Math.Max(Ui.S(160), width - Ui.S(850)), Ui.S(32));
            _importButton.SetBounds(width - Ui.S(190), top, Ui.S(166), Ui.S(40));
            FlatButton open = FindButton("打开UserSkins文件夹");
            if (open != null)
            {
                open.SetBounds(width - Ui.S(370), top, Ui.S(160), Ui.S(40));
            }
            FlatButton settings = FindButton("设置");
            if (settings != null)
            {
                settings.SetBounds(width - Ui.S(460), top, Ui.S(80), Ui.S(40));
            }
            FlatButton clear = FindButton("清空列表");
            if (clear != null)
            {
                clear.SetBounds(width - Ui.S(570), top, Ui.S(100), Ui.S(40));
            }
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
            if (_emptyLabel != null && !_emptyLabel.IsDisposed)
            {
                _emptyLabel.Size = new Size(width, Math.Max(Ui.S(160), _listPanel.ClientSize.Height - Ui.S(40)));
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
            _emptyLabel = null;

            if (_packages.Count == 0)
            {
                _emptyLabel = new Label();
                _emptyLabel.ForeColor = Ui.TextFaint;
                _emptyLabel.Font = Ui.Body;
                _emptyLabel.AutoSize = false;
                _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
                _emptyLabel.BackColor = Ui.Bg;
                _emptyLabel.Margin = new Padding(0);
                _emptyLabel.Text = "还没有添加涂装包。\n\n拖入 .zip，或点击上面的区域选择文件。\n程序会自动认出涂装属于哪台载具，并在导入前询问你。";
                _listPanel.Controls.Add(_emptyLabel);
            }
            else
            {
                for (int i = 0; i < _packages.Count; i++)
                {
                    PackageCard card = new PackageCard();
                    card.Package = _packages[i];
                    card.Selected = _packages[i] == _selected;
                    card.Margin = new Padding(0, 0, 0, Ui.S(8));
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
            }
            _listPanel.ResumeLayout();
            ResizeCards();
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
                _detailName.Text = "";
                _detailMeta.Text = "";
                _detailSection.Text = "";
                _detailHint.Text = "";
                _manualButton.Visible = false;
                _refreshButton.Visible = false;
                _detail.Invalidate();
                return;
            }

            _detailName.Text = _selected.Name;
            if (_selected.Error.Length > 0)
            {
                _detailMeta.Text = _selected.Error;
                _detailMeta.ForeColor = Ui.Bad;
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
                _detailMeta.Text = _selected.FilesText + "  ·  " + _selected.SizeText + "\n来源：" + sourceName;
                _detailMeta.ForeColor = Ui.TextDim;
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
            int rowWidth = _vehPanel.ClientSize.Width - Ui.S(26);
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
                row.Width = _vehPanel.ClientSize.Width - Ui.S(26);
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
                top += row.Height + Ui.S(3);
            }

            if (_selected.UnknownIds.Count > 0)
            {
                Label unknown = new Label();
                unknown.Left = Ui.S(10);
                unknown.Top = top + Ui.S(4);
                unknown.Width = _vehPanel.ClientSize.Width - Ui.S(26);
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
            _detailSection.Text = total == 0
                ? "该涂装包可导入的载具（0）"
                : "该涂装包可导入的载具（" + total + "） · 可导入 " + selectedHere + "/" + total;

            bool missing = total > 0 && found < total;
            _manualButton.Visible = total == 0 || _selected.UnknownIds.Count > 0;
            _refreshButton.Visible = true;
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
                _detailHint.Text = "黄色行是找到专属文件夹、可以直接导入的；黑色行是未识别，\n需要先在游戏里给该载具创建模板，再点「刷新文件夹」。";
                _detailHint.ForeColor = Ui.Warn;
            }
            else
            {
                _detailHint.Text = "黄色行是找到专属文件夹、可以直接导入的；\n导入前会把旧内容备份到安装目录。";
                _detailHint.ForeColor = Ui.TextFaint;
            }
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
            if (_selected != null && _detailSection != null)
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
                _detailSection.Text = curTotal == 0
                    ? "该涂装包可导入的载具（0）"
                    : "该涂装包可导入的载具（" + curTotal + "） · 可导入 " + curSelected + "/" + curTotal;
            }
            _importButton.Text = selectedCount > 0 ? "导入选中（" + selectedCount + "）" : "导入选中";
            _importButton.Enabled = selectedCount > 0;
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
