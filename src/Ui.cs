using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>一套界面配色。</summary>
    internal class Palette
    {
        public string Name;
        public bool Dark;

        public Color Bg;
        public Color BgSoft;
        public Color Panel;
        public Color PanelHover;
        public Color PanelActive;
        public Color Line;
        public Color LineSoft;
        public Color Text;
        public Color TextDim;
        public Color TextFaint;
        public Color Accent;
        public Color AccentSoft;
        public Color AccentDeep;
        public Color OnAccent;
        public Color Good;
        public Color Warn;
        public Color Bad;
        public Color GoodBg;
        public Color WarnBg;
        public Color BadBg;
        public Color CardBg;
        public Color CardSel;
        public Color BtnFillHover;
        public Color BtnDisabledFill;
        public Color BtnDisabledText;
        public Color DangerFill;
        public Color DangerFillHover;
        public Color DangerBorder;
        public Color DangerText;
        public Color DropHotFill;
        public Color DropIconBg;
        public Color DropIconFg;
        public Color BadgeGoodBg;
        public Color BadgeWarnBg;
        public Color BadgeBadBg;
        public Color CloseHoverBg;
        public Color CloseHoverFg;
        public Color CheckMark;
    }

    internal static class Palettes
    {
        public static readonly Palette[] All = new Palette[]
        {
            Amber(),
            Military(),
            Ocean(),
            Paper()
        };

        private static Palette Amber()
        {
            Palette p = new Palette();
            p.Name = "暗夜琥珀";
            p.Dark = true;
            // 与 Tok 中的三层表面/描边/三级文字对齐：页面底 #0d0f12、面板 #14171c、卡片 #1a1e24。
            p.Bg = Tok.Page;
            p.BgSoft = C(20, 24, 29);
            p.Panel = Tok.Panel;
            p.PanelHover = C(30, 35, 42);
            p.PanelActive = C(37, 43, 52);
            p.Line = Tok.Stroke;
            p.LineSoft = Tok.StrokeSoft;
            p.Text = Tok.TextPrimary;
            p.TextDim = Tok.TextSecondary;
            p.TextFaint = Tok.TextWeak;
            p.Accent = C(233, 164, 62);
            p.AccentSoft = C(255, 196, 108);
            p.AccentDeep = C(176, 116, 30);
            p.OnAccent = C(28, 24, 16);
            p.Good = C(76, 195, 138);
            p.Warn = C(229, 179, 90);
            p.Bad = C(224, 91, 91);
            p.GoodBg = C(28, 48, 40);
            p.WarnBg = C(50, 43, 26);
            p.BadBg = C(52, 30, 30);
            p.CardBg = Tok.Card;
            p.CardSel = C(34, 41, 50);
            p.BtnFillHover = C(34, 41, 50);
            p.BtnDisabledFill = C(23, 27, 33);
            p.BtnDisabledText = C(93, 102, 114);
            p.DangerFill = C(50, 33, 33);
            p.DangerFillHover = C(70, 38, 38);
            p.DangerBorder = C(120, 62, 62);
            p.DangerText = C(240, 160, 160);
            p.DropHotFill = C(38, 34, 24);
            p.DropIconBg = C(46, 52, 62);
            p.DropIconFg = C(35, 28, 16);
            p.BadgeGoodBg = C(38, 56, 47);
            p.BadgeWarnBg = C(50, 43, 26);
            p.BadgeBadBg = C(52, 30, 30);
            p.CloseHoverBg = C(70, 40, 40);
            p.CloseHoverFg = C(240, 150, 150);
            p.CheckMark = C(30, 26, 18);
            return p;
        }

        private static Palette Military()
        {
            Palette p = new Palette();
            p.Name = "军绿战术";
            p.Dark = true;
            p.Bg = C(16, 18, 14);
            p.BgSoft = C(21, 24, 18);
            p.Panel = C(26, 30, 23);
            p.PanelHover = C(33, 38, 29);
            p.PanelActive = C(39, 45, 34);
            p.Line = C(53, 60, 46);
            p.LineSoft = C(39, 45, 36);
            p.Text = C(235, 238, 226);
            p.TextDim = C(160, 167, 148);
            p.TextFaint = C(112, 120, 102);
            p.Accent = C(176, 196, 92);
            p.AccentSoft = C(206, 222, 132);
            p.AccentDeep = C(120, 140, 52);
            p.OnAccent = C(22, 26, 14);
            p.Good = C(108, 196, 120);
            p.Warn = C(214, 178, 86);
            p.Bad = C(214, 96, 84);
            p.GoodBg = C(30, 46, 31);
            p.WarnBg = C(48, 43, 25);
            p.BadBg = C(54, 30, 28);
            p.CardBg = C(23, 27, 20);
            p.CardSel = C(34, 40, 30);
            p.BtnFillHover = C(39, 45, 34);
            p.BtnDisabledFill = C(31, 35, 27);
            p.BtnDisabledText = C(107, 115, 98);
            p.DangerFill = C(52, 33, 30);
            p.DangerFillHover = C(72, 38, 34);
            p.DangerBorder = C(122, 64, 58);
            p.DangerText = C(240, 160, 148);
            p.DropHotFill = C(38, 40, 24);
            p.DropIconBg = C(48, 54, 42);
            p.DropIconFg = C(24, 28, 14);
            p.BadgeGoodBg = C(38, 56, 38);
            p.BadgeWarnBg = C(48, 43, 25);
            p.BadgeBadBg = C(54, 30, 28);
            p.CloseHoverBg = C(72, 40, 36);
            p.CloseHoverFg = C(240, 150, 140);
            p.CheckMark = C(24, 28, 14);
            return p;
        }

        private static Palette Ocean()
        {
            Palette p = new Palette();
            p.Name = "冷蓝夜航";
            p.Dark = true;
            p.Bg = C(11, 15, 21);
            p.BgSoft = C(16, 21, 29);
            p.Panel = C(21, 27, 36);
            p.PanelHover = C(27, 35, 46);
            p.PanelActive = C(33, 42, 55);
            p.Line = C(44, 55, 70);
            p.LineSoft = C(32, 41, 53);
            p.Text = C(226, 235, 245);
            p.TextDim = C(146, 160, 178);
            p.TextFaint = C(100, 114, 131);
            p.Accent = C(94, 168, 232);
            p.AccentSoft = C(136, 199, 255);
            p.AccentDeep = C(48, 110, 168);
            p.OnAccent = C(10, 22, 34);
            p.Good = C(80, 196, 168);
            p.Warn = C(226, 178, 96);
            p.Bad = C(224, 100, 110);
            p.GoodBg = C(26, 46, 44);
            p.WarnBg = C(48, 42, 26);
            p.BadBg = C(52, 30, 36);
            p.CardBg = C(18, 24, 32);
            p.CardSel = C(28, 37, 49);
            p.BtnFillHover = C(33, 42, 55);
            p.BtnDisabledFill = C(26, 31, 40);
            p.BtnDisabledText = C(100, 112, 128);
            p.DangerFill = C(46, 31, 38);
            p.DangerFillHover = C(66, 36, 46);
            p.DangerBorder = C(116, 62, 78);
            p.DangerText = C(238, 158, 170);
            p.DropHotFill = C(26, 36, 48);
            p.DropIconBg = C(40, 50, 64);
            p.DropIconFg = C(10, 22, 34);
            p.BadgeGoodBg = C(32, 54, 50);
            p.BadgeWarnBg = C(48, 42, 26);
            p.BadgeBadBg = C(52, 32, 38);
            p.CloseHoverBg = C(64, 36, 46);
            p.CloseHoverFg = C(236, 150, 166);
            p.CheckMark = C(10, 22, 34);
            return p;
        }

        private static Palette Paper()
        {
            Palette p = new Palette();
            p.Name = "纸白";
            p.Dark = false;
            // 亮色主题同样保持 页面底 / 面板 / 卡片 三层可区分（配合 1px 描边）。
            p.Bg = C(238, 241, 246);
            p.BgSoft = C(255, 255, 255);
            p.Panel = C(255, 255, 255);
            p.PanelHover = C(243, 246, 250);
            p.PanelActive = C(234, 239, 245);
            p.Line = C(211, 218, 227);
            p.LineSoft = C(228, 233, 240);
            p.Text = C(27, 33, 41);
            p.TextDim = C(86, 97, 111);
            p.TextFaint = C(124, 136, 150);
            p.Accent = C(214, 130, 36);
            p.AccentSoft = C(236, 160, 72);
            p.AccentDeep = C(160, 92, 18);
            p.OnAccent = C(255, 255, 255);
            p.Good = C(34, 150, 96);
            p.Warn = C(178, 132, 30);
            p.Bad = C(198, 60, 60);
            p.GoodBg = C(226, 244, 235);
            p.WarnBg = C(250, 241, 220);
            p.BadBg = C(252, 230, 230);
            p.CardBg = C(248, 250, 252);
            p.CardSel = C(238, 243, 249);
            p.BtnFillHover = C(243, 246, 250);
            p.BtnDisabledFill = C(240, 242, 245);
            p.BtnDisabledText = C(160, 168, 178);
            p.DangerFill = C(253, 235, 235);
            p.DangerFillHover = C(250, 220, 220);
            p.DangerBorder = C(226, 166, 166);
            p.DangerText = C(180, 50, 50);
            p.DropHotFill = C(253, 246, 232);
            p.DropIconBg = C(236, 238, 242);
            p.DropIconFg = C(255, 255, 255);
            p.BadgeGoodBg = C(226, 244, 235);
            p.BadgeWarnBg = C(250, 241, 220);
            p.BadgeBadBg = C(252, 230, 230);
            p.CloseHoverBg = C(250, 220, 220);
            p.CloseHoverFg = C(180, 50, 50);
            p.CheckMark = C(255, 255, 255);
            return p;
        }

        private static Color C(int r, int g, int b)
        {
            return Color.FromArgb(r, g, b);
        }
    }

    /// <summary>
    /// 主界面设计令牌：颜色、圆角、间距与可点击高度。
    /// 所有自绘界面统一从这里取值，不再各处硬编码 0.5px/2px/随意圆角。
    /// 颜色以默认暗色主题为基准，其它主题通过 Palette 保持可用。
    /// </summary>
    internal static class Tok
    {
        // 页面底 / 面板 / 卡片：三层表面
        public static readonly Color Page = Color.FromArgb(0x0D, 0x0F, 0x12);
        public static readonly Color Panel = Color.FromArgb(0x14, 0x17, 0x1C);
        public static readonly Color Card = Color.FromArgb(0x1A, 0x1E, 0x24);
        // 1px 描边
        public static readonly Color Stroke = Color.FromArgb(0x2A, 0x31, 0x3A);
        public static readonly Color StrokeSoft = Color.FromArgb(0x20, 0x26, 0x2E);
        // 文字三级：主 / 次 / 弱
        public static readonly Color TextPrimary = Color.FromArgb(0xE8, 0xED, 0xF3);
        public static readonly Color TextSecondary = Color.FromArgb(0x98, 0xA2, 0xAF);
        public static readonly Color TextWeak = Color.FromArgb(0x6B, 0x75, 0x83);

        // 圆角（设计像素）
        public const int CardRadius = 12;
        public const int ButtonRadius = 8;

        // 可点击高度（设计像素）
        public const int PrimaryHeight = 36;
        public const int SecondaryHeight = 32;
        public const int IconButton = 32;

        // 间距基准：4 / 8 / 12 / 16 / 24 / 32
        public const int Space4 = 4;
        public const int Space8 = 8;
        public const int Space12 = 12;
        public const int Space16 = 16;
        public const int Space24 = 24;
        public const int Space32 = 32;

        public const int PagePadding = 24;
        public const int CardGap = 16;
        public const int RightColumn = 400;

        // 描边与动效
        public const float Border = 1f;
        public const float FocusRing = 2f;
        public const int MotionMs = 140;
        public const int PanelMotionMs = 170;
    }

    /// <summary>主题、缩放、字体和绘图小工具。</summary>
    internal static class Ui
    {
        public static Palette T = Palettes.All[0];
        public static bool Animations = true;
        /// <summary>
        /// 颜色/描边微过渡（hover/focus/面板切换）。只改变颜色与透明度，
        /// 不移动控件，也不改动窗口整体透明度，因此不受“关闭界面动画”开关影响。
        /// </summary>
        public static bool Motion = true;

        private static float _scale = 1f;
        private static Font _title;
        private static Font _h1;
        private static Font _body;
        private static Font _bodyBold;
        private static Font _small;
        private static Font _smallBold;
        private static Font _mono;
        private static Font _cardTitle;
        private static Font _cardSub;
        private static Font _cardLabel;
        private static Font _cardValue;

        public static Color Bg { get { return T.Bg; } }
        public static Color BgSoft { get { return T.BgSoft; } }
        public static Color Panel { get { return T.Panel; } }
        public static Color PanelHover { get { return T.PanelHover; } }
        public static Color PanelActive { get { return T.PanelActive; } }
        public static Color Line { get { return T.Line; } }
        public static Color LineSoft { get { return T.LineSoft; } }
        public static Color Text { get { return T.Text; } }
        public static Color TextDim { get { return T.TextDim; } }
        public static Color TextFaint { get { return T.TextFaint; } }
        public static Color Accent { get { return T.Accent; } }
        public static Color AccentSoft { get { return T.AccentSoft; } }
        public static Color AccentDeep { get { return T.AccentDeep; } }
        public static Color OnAccent { get { return T.OnAccent; } }
        public static Color Good { get { return T.Good; } }
        public static Color Warn { get { return T.Warn; } }
        public static Color Bad { get { return T.Bad; } }
        public static Color GoodBg { get { return T.GoodBg; } }
        public static Color WarnBg { get { return T.WarnBg; } }
        public static Color BadBg { get { return T.BadBg; } }
        public static Color CardBg { get { return T.CardBg; } }
        public static Color CardSel { get { return T.CardSel; } }

        // ---- 设计令牌访问器：主界面一律用这些，不再直接写颜色/圆角数值 ----
        public static Color Page { get { return T.Bg; } }
        public static Color Surface { get { return T.Panel; } }
        public static Color Card { get { return T.CardBg; } }
        public static Color Stroke { get { return T.Line; } }
        public static Color StrokeSoft { get { return T.LineSoft; } }
        public static Color TextPrimary { get { return T.Text; } }
        public static Color TextSecondary { get { return T.TextDim; } }
        public static Color TextWeak { get { return T.TextFaint; } }
        public static int RCard { get { return S(Tok.CardRadius); } }
        public static int RButton { get { return S(Tok.ButtonRadius); } }
        public static int HPrimary { get { return S(Tok.PrimaryHeight); } }
        public static int HSecondary { get { return S(Tok.SecondaryHeight); } }
        public static int HIcon { get { return S(Tok.IconButton); } }
        public static int PadPage { get { return S(Tok.PagePadding); } }
        public static int GapCard { get { return S(Tok.CardGap); } }
        public static int Sp4 { get { return S(Tok.Space4); } }
        public static int Sp8 { get { return S(Tok.Space8); } }
        public static int Sp12 { get { return S(Tok.Space12); } }
        public static int Sp16 { get { return S(Tok.Space16); } }
        public static int Sp24 { get { return S(Tok.Space24); } }
        public static int Sp32 { get { return S(Tok.Space32); } }
        /// <summary>1px 设计描边；保持 1 设备像素，避免半像素抗锯齿把描边冲淡。</summary>
        public static float BorderWidth { get { return 1f; } }
        /// <summary>大面积虚线拖放框在高 DPI 下取整到 1~2 设备像素，保证虚线可见。</summary>
        public static float Hairline { get { return Math.Max(1f, (float)Math.Round(_scale)); } }

        public static float Scale
        {
            get { return _scale; }
        }

        public static int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        public static float SF(float value)
        {
            return value * _scale;
        }

        public static void Init(float scale, float fontZoom)
        {
            if (scale > 0.5f && scale < 4f)
            {
                _scale = scale;
            }
            // 字号也要跟着界面缩放走：点值只随系统 DPI 放大，程序内 Zoom 必须单独乘进去，
            // 否则会出现“卡片/按钮变大、文字不变”的错位。
            if (fontZoom <= 0.1f || fontZoom > 3f)
            {
                fontZoom = 1f;
            }
            DisposeFonts();
            string family = PickFamily("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
            _title = new Font(family, 15.5f * fontZoom, FontStyle.Bold, GraphicsUnit.Point);
            _h1 = new Font(family, 11.5f * fontZoom, FontStyle.Bold, GraphicsUnit.Point);
            _body = new Font(family, 9.5f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
            _bodyBold = new Font(family, 9.5f * fontZoom, FontStyle.Bold, GraphicsUnit.Point);
            _small = new Font(family, 8.5f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
            _smallBold = new Font(family, 8.5f * fontZoom, FontStyle.Bold, GraphicsUnit.Point);
            _mono = new Font("Consolas", 8.5f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
            // 管理卡片/浮层专用：约 14px / 12px / 11px / 12px，全列表统一。
            _cardTitle = new Font(family, 10.5f * fontZoom, FontStyle.Bold, GraphicsUnit.Point);
            _cardSub = new Font(family, 9f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
            _cardLabel = new Font(family, 8.25f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
            _cardValue = new Font(family, 9f * fontZoom, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static void SetTheme(int index)
        {
            if (index < 0 || index >= Palettes.All.Length)
            {
                index = 0;
            }
            T = Palettes.All[index];
        }

        public static void ApplyThemeToOpenForms()
        {
            FormCollection forms = Application.OpenForms;
            for (int i = 0; i < forms.Count; i++)
            {
                forms[i].BackColor = T.Bg;
                RepaintTree(forms[i]);
            }
            Native.DarkTitleBar(forms);
        }

        private static void RepaintTree(Control control)
        {
            control.Invalidate(true);
            for (int i = 0; i < control.Controls.Count; i++)
            {
                RepaintTree(control.Controls[i]);
            }
        }

        private static void DisposeFonts()
        {
            Font[] all = new Font[] { _title, _h1, _body, _bodyBold, _small, _smallBold, _mono,
                                      _cardTitle, _cardSub, _cardLabel, _cardValue };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    all[i].Dispose();
                }
            }
        }

        private static string PickFamily(params string[] names)
        {
            using (InstalledFontCollection fonts = new InstalledFontCollection())
            {
                for (int n = 0; n < names.Length; n++)
                {
                    for (int i = 0; i < fonts.Families.Length; i++)
                    {
                        if (string.Equals(fonts.Families[i].Name, names[n], StringComparison.OrdinalIgnoreCase))
                        {
                            return names[n];
                        }
                    }
                }
            }
            return FontFamily.GenericSansSerif.Name;
        }

        public static Font Title { get { return _title; } }
        public static Font H1 { get { return _h1; } }
        public static Font Body { get { return _body; } }
        public static Font BodyBold { get { return _bodyBold; } }
        public static Font Small { get { return _small; } }
        public static Font SmallBold { get { return _smallBold; } }
        public static Font Mono { get { return _mono; } }
        public static Font CardTitle { get { return _cardTitle; } }
        public static Font CardSub { get { return _cardSub; } }
        public static Font CardLabel { get { return _cardLabel; } }
        public static Font CardValue { get { return _cardValue; } }

        public static GraphicsPath Round(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
            if (d <= 2)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRound(Graphics g, Rectangle rect, int radius, Color color, float width)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (Pen pen = new Pen(color, width))
            {
                pen.Alignment = PenAlignment.Inset;
                g.DrawPath(pen, path);
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, rect, color, flags);
        }

        /// <summary>焦点描边：2px 可见轮廓，向内收 3px，不依赖颜色变化。</summary>
        public static void DrawFocus(Graphics g, Rectangle rect, int radius, Color color)
        {
            int inset = S(3);
            Rectangle r = new Rectangle(rect.X + inset, rect.Y + inset,
                                        Math.Max(1, rect.Width - inset * 2),
                                        Math.Max(1, rect.Height - inset * 2));
            DrawRound(g, r, Math.Max(S(2), radius - inset), color, Tok.FocusRing);
        }

        /// <summary>单色着色绘制图标（彩色站点图标统一压成灰度/弱化色，避免抢焦点）。</summary>
        public static void DrawIconTinted(Graphics g, Image image, Rectangle rect, Color color)
        {
            if (image == null || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }
            // 先转灰度再染成目标色：否则红色/彩色 favicon 会保留原色，破坏单色按钮体系。
            float rf = color.R / 255f;
            float gf = color.G / 255f;
            float bf = color.B / 255f;
            ColorMatrix matrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.299f * rf, 0.587f * rf, 0.114f * rf, 0, 0 },
                new float[] { 0.299f * gf, 0.587f * gf, 0.114f * gf, 0, 0 },
                new float[] { 0.299f * bf, 0.587f * bf, 0.114f * bf, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });
            using (ImageAttributes attrs = new ImageAttributes())
            {
                attrs.SetColorMatrix(matrix);
                InterpolationMode old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, rect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attrs);
                g.InterpolationMode = old;
            }
        }

        /// <summary>按字符宽度硬换行（中文优先）。用于键值行，保证长值换行而不是省略号截断。</summary>
        public static List<string> WrapLines(string text, Font font, int width)
        {
            List<string> lines = new List<string>();
            if (text == null)
            {
                text = "";
            }
            if (width < 1)
            {
                width = 1;
            }
            if (text.Length == 0)
            {
                lines.Add("");
                return lines;
            }
            string current = "";
            for (int i = 0; i < text.Length; i++)
            {
                string trial = current + text[i];
                if (current.Length > 0 && TextRenderer.MeasureText(trial, font).Width > width)
                {
                    lines.Add(current);
                    current = text[i].ToString();
                }
                else
                {
                    current = trial;
                }
            }
            if (current.Length > 0)
            {
                lines.Add(current);
            }
            return lines;
        }

        public static int LineHeight(Font font)
        {
            return TextRenderer.MeasureText("国Ag", font).Height;
        }

        public static TextFormatFlags LeftMiddle = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public static TextFormatFlags RightMiddle = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public static TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        public static TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t <= 0)
            {
                return a;
            }
            if (t >= 1)
            {
                return b;
            }
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>窗口淡入，关闭动画时直接显示。</summary>
        public static void FadeIn(Form form)
        {
            if (!Animations)
            {
                form.Opacity = 1;
                Native.RemoveLayered(form);
                return;
            }
            form.Opacity = 0.3;
            Timer timer = new Timer();
            timer.Interval = 16;
            DateTime start = DateTime.UtcNow;
            timer.Tick += delegate
            {
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 190.0;
                if (t >= 1 || form.IsDisposed)
                {
                    if (!form.IsDisposed)
                    {
                        form.Opacity = 1;
                        Native.RemoveLayered(form);
                    }
                    timer.Stop();
                    timer.Dispose();
                    return;
                }
                form.Opacity = 0.3 + 0.7 * t;
            };
            timer.Start();
        }

        /// <summary>对话框用滑入动画（不改变窗口透明度，避免残影）。</summary>
        public static void SlideIn(Form form)
        {
            if (!Animations)
            {
                return;
            }
            int targetTop = form.Top;
            int startTop = targetTop + S(10);
            form.Top = startTop;
            Timer timer = new Timer();
            timer.Interval = 16;
            DateTime start = DateTime.UtcNow;
            timer.Tick += delegate
            {
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 160.0;
                if (t >= 1 || form.IsDisposed)
                {
                    if (!form.IsDisposed)
                    {
                        form.Top = targetTop;
                    }
                    timer.Stop();
                    timer.Dispose();
                    return;
                }
                form.Top = startTop - (int)((startTop - targetTop) * t);
            };
            timer.Start();
        }
    }

    /// <summary>少量 Win32 调用：暗色标题栏。</summary>
    internal static class Native
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string appName, string idList);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>取主显示器的有效 DPI（Per-Monitor 感知下才是真实值）。</summary>
        public static float GetDpiScale()
        {
            try
            {
                POINT point = new POINT();
                point.X = 0;
                point.Y = 0;
                IntPtr monitor = MonitorFromPoint(point, 2);
                uint x;
                uint y;
                if (GetDpiForMonitor(monitor, 0, out x, out y) == 0 && x >= 72 && x <= 480)
                {
                    return x / 96f;
                }
            }
            catch
            {
            }
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 72 && dpi <= 480)
                {
                    return dpi / 96f;
                }
            }
            catch
            {
            }
            return 1f;
        }

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();
        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, uint flags);

        /// <summary>等待 DWM 完成一次合成，避免缩放时窗口尺寸已变、画面还没跟上。</summary>
        public static void FlushComposition()
        {
            try
            {
                DwmFlush();
            }
            catch
            {
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        /// <summary>取窗口所在显示器的物理工作区（绕开 WinForms 在混合 DPI 下的虚拟化）。</summary>
        public static Rectangle GetMonitorWorkArea(IntPtr hwnd)
        {
            try
            {
                IntPtr monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
                MONITORINFO info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    return Rectangle.FromLTRB(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
                }
            }
            catch
            {
            }
            return Screen.FromHandle(hwnd).WorkingArea;
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        /// <summary>
        /// 去掉 WS_EX_LAYERED。窗口用 Opacity 做过淡入后会被打上分层窗口样式，
        /// 无边框窗口在这种状态下拖动会出现残影/拖影，淡入结束后必须移除。
        /// </summary>
        /// <summary>拖动时移动窗口：SWP_NOCOPYBITS 不复制旧像素，强制重绘，避免拖影。</summary>
        /// <summary>拖动边角缩放：同样禁止复制旧像素，避免缩放时出现拖影。</summary>
        public static void ResizeWindowNoCopy(Form form, Rectangle bounds)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }
            try
            {
                // SWP_NOREDRAW：先把尺寸改完，再一次性擦除并重绘整窗（含子控件），
                // 避免系统在尺寸未定稿时逐块复制旧像素，留下条状拖影。
                SetWindowPos(form.Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                             0x0004 | 0x0010);
                RedrawWindow(form.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 | 0x0100 | 0x0200);
            }
            catch
            {
            }
        }

        public static void MoveWindowNoCopy(Form form, int x, int y)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }
            try
            {
                SetWindowPos(form.Handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0100);
            }
            catch
            {
            }
        }

        public static void RemoveLayered(Form form)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }
            try
            {
                int ex = GetWindowLong(form.Handle, -20);
                if ((ex & 0x00080000) != 0)
                {
                    SetWindowLong(form.Handle, -20, ex & ~0x00080000);
                    form.Refresh();
                }
            }
            catch
            {
            }
        }
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);

        /// <summary>把窗口放到前台一次，随后恢复普通层级，不长期置顶。</summary>
        public static void BringToFrontOnce(Form form)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }
            try
            {
                ShowWindow(form.Handle, 5); // SW_SHOW
                SetWindowPos(form.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                SetWindowPos(form.Handle, new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                SetForegroundWindow(form.Handle);
                form.Activate();
                form.Focus();
            }
            catch
            {
            }
        }

        public static void DarkTitleBar(Form form)
        {
            try
            {
                int dark = Ui.T.Dark ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
            }
            catch
            {
            }
        }

        public static void DarkTitleBar(FormCollection forms)
        {
            for (int i = 0; i < forms.Count; i++)
            {
                DarkTitleBar(forms[i]);
            }
        }

        /// <summary>把滚动条也换成暗色（系统原生滚动条默认是浅色）。</summary>
        public static void ThemeScrollbars(Control root)
        {
            if (root == null)
            {
                return;
            }
            try
            {
                if (root is ListBox || root is ScrollableControl)
                {
                    if (root.IsHandleCreated)
                    {
                        SetWindowTheme(root.Handle, Ui.T.Dark ? "DarkMode_Explorer" : "Explorer", null);
                    }
                }
            }
            catch
            {
            }
            for (int i = 0; i < root.Controls.Count; i++)
            {
                ThemeScrollbars(root.Controls[i]);
            }
        }

        public static void SetCue(TextBox box, string cue)
        {
            box.HandleCreated += delegate
            {
                SendMessage(box.Handle, 0x1501, (IntPtr)1, cue);
            };
        }
    }

    /// <summary>盖住系统浅色滚动条，画一条与主题一致的细滚动条。</summary>
    internal class ScrollCover : Control
    {
        private readonly ScrollableControl _target;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartValue;
        private bool _hover;

        public ScrollCover(ScrollableControl target)
        {
            _target = target;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.CardBg;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private int ThumbHeight()
        {
            int maximum = _target.VerticalScroll.Maximum;
            int large = _target.VerticalScroll.LargeChange;
            if (maximum <= 0)
            {
                return 0;
            }
            int height = (int)((double)Height * large / Math.Max(1, maximum));
            return Math.Max(Ui.S(28), Math.Min(Height, height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            if (!_target.VerticalScroll.Visible)
            {
                return;
            }
            int thumb = ThumbHeight();
            if (thumb <= 0 || Height <= thumb)
            {
                return;
            }
            int maximum = _target.VerticalScroll.Maximum - _target.VerticalScroll.LargeChange + 1;
            double ratio = maximum <= 0 ? 0 : (double)_target.VerticalScroll.Value / maximum;
            if (ratio < 0)
            {
                ratio = 0;
            }
            if (ratio > 1)
            {
                ratio = 1;
            }
            int y = (int)((Height - thumb) * ratio);
            Rectangle bar = new Rectangle(Ui.S(3), y, Math.Max(2, Width - Ui.S(6)), thumb);
            Ui.FillRound(g, bar, bar.Width / 2, _hover || _dragging ? Ui.TextDim : Ui.Line);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartValue = _target.VerticalScroll.Value;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                int thumb = ThumbHeight();
                int span = Math.Max(1, Height - thumb);
                int maximum = _target.VerticalScroll.Maximum - _target.VerticalScroll.LargeChange;
                if (maximum > 0 && span > 0)
                {
                    int value = _dragStartValue + (int)((double)(e.Y - _dragStartY) * maximum / span);
                    if (value < 0)
                    {
                        value = 0;
                    }
                    if (value > maximum)
                    {
                        value = maximum;
                    }
                    _target.VerticalScroll.Value = value;
                    _target.PerformLayout();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    /// <summary>双缓冲面板，拖动窗口大小时减少闪烁和残影。</summary>
    internal class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    /// <summary>
    /// 控件内的颜色过渡（120~160ms）。只插值颜色，不移动控件，不改变窗口透明度。
    /// </summary>
    internal sealed class Tween
    {
        private readonly Control _owner;
        private readonly Timer _timer;
        private DateTime _start;
        private float _from;
        private float _to;
        private int _duration;
        public float Value;

        public Tween(Control owner, float value)
        {
            _owner = owner;
            Value = value;
            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate { Step(); };
        }

        public bool Running
        {
            get { return _timer.Enabled; }
        }

        public void To(float target, int durationMs)
        {
            if (Math.Abs(target - Value) < 0.001f)
            {
                return;
            }
            if (!Ui.Motion || _owner == null || !_owner.IsHandleCreated)
            {
                Value = target;
                if (_owner != null)
                {
                    _owner.Invalidate();
                }
                return;
            }
            _from = Value;
            _to = target;
            _duration = Math.Max(1, durationMs);
            _start = DateTime.UtcNow;
            _timer.Stop();
            _timer.Start();
        }

        public void Snap(float value)
        {
            _timer.Stop();
            Value = value;
            if (_owner != null)
            {
                _owner.Invalidate();
            }
        }

        private void Step()
        {
            double t = (DateTime.UtcNow - _start).TotalMilliseconds / _duration;
            if (t >= 1)
            {
                Value = _to;
                _timer.Stop();
            }
            else
            {
                double eased = 1 - Math.Pow(1 - t, 3);
                Value = _from + (_to - _from) * (float)eased;
            }
            if (_owner != null && !_owner.IsDisposed)
            {
                _owner.Invalidate();
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }

    /// <summary>顶部站点入口：第二行的次级/幽灵按钮，图标统一弱化色。</summary>
    internal class SiteButton : Control
    {
        public Bitmap Icon;
        private readonly Tween _hoverT;
        private bool _down;

        public SiteButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Ui.Page;
            Cursor = Cursors.Hand;
            Font = Ui.CardSub;
            Height = Ui.HSecondary;
            TabStop = true;
            _hoverT = new Tween(this, 0f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hoverT.To(1f, Tok.MotionMs);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _down = false;
            _hoverT.To(0f, Tok.MotionMs);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            float t = _hoverT.Value;
            Color fill = Ui.Mix(Ui.Page, Ui.PanelHover, _down ? Math.Max(t, 0.7f) : t);
            Color border = Ui.Mix(Ui.Stroke, Ui.TextWeak, Math.Max(t * 0.45f, _down ? 0.5f : 0f));
            Color text = Ui.Mix(Ui.TextSecondary, Ui.TextPrimary, t);
            Ui.FillRound(g, r, Ui.RButton, fill);
            Ui.DrawRound(g, r, Ui.RButton, border, Ui.BorderWidth);
            int pad = Ui.Sp12;
            int icon = Ui.S(16);
            int tx = pad;
            if (Icon != null)
            {
                // 站点图标保留原始配色：这是品牌标识，不参与强调色/状态色体系。
                Rectangle ir = new Rectangle(pad, (Height - icon) / 2, icon, icon);
                System.Drawing.Drawing2D.InterpolationMode oldMode = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(Icon, ir);
                g.InterpolationMode = oldMode;
                tx += icon + Ui.Sp8;
            }
            Ui.DrawText(g, Text, Font, text,
                    new Rectangle(tx, 0, Math.Max(Ui.S(10), Width - tx - pad), Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.RButton, Ui.TextSecondary);
            }
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

    /// <summary>顶部路径选择：状态点 + 路径 + “更改”，与设置按钮同高（38）。</summary>
    internal class PathChip : Control
    {
        public bool Valid = true;
        public string PathText = "";
        private readonly Tween _hoverT;
        private bool _down;

        public PathChip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Ui.Page;
            Cursor = Cursors.Hand;
            Font = Ui.CardSub;
            Height = Ui.S(38);
            TabStop = true;
            _hoverT = new Tween(this, 0f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hoverT.To(1f, Tok.MotionMs);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _down = false;
            _hoverT.To(0f, Tok.MotionMs);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            float t = _hoverT.Value;
            Ui.FillRound(g, r, Ui.RButton, Ui.Mix(_down ? Ui.PanelActive : Ui.Surface, Ui.PanelHover, t));
            Ui.DrawRound(g, r, Ui.RButton, Ui.Mix(Ui.Stroke, Ui.TextWeak, t * 0.4f), Ui.BorderWidth);
            using (SolidBrush dot = new SolidBrush(Valid ? Ui.Good : Ui.Bad))
            {
                g.FillEllipse(dot, Ui.Sp12, (Height - Ui.S(8)) / 2, Ui.S(8), Ui.S(8));
            }
            string action = "更改";
            Size actionSize = TextRenderer.MeasureText(action, Font);
            int actionX = Width - Ui.Sp12 - actionSize.Width;
            Ui.DrawText(g, PathText, Font, Ui.Mix(Ui.TextSecondary, Ui.TextPrimary, t),
                    new Rectangle(Ui.Sp12 + Ui.S(16), 0, Math.Max(Ui.S(20), actionX - Ui.Sp8 - (Ui.Sp12 + Ui.S(16))), Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Ui.DrawText(g, action, Font, Ui.Mix(Ui.TextWeak, Ui.TextSecondary, t),
                    new Rectangle(actionX, 0, actionSize.Width + Ui.S(2), Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.RButton, Ui.TextSecondary);
            }
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

    /// <summary>扁平按钮：主按钮 / 次级 / 危险三种外观，36 高，8 圆角，四态 + 焦点描边。</summary>
    internal class FlatButton : Control
    {
        private readonly Tween _hoverT;
        private readonly Tween _pressT;
        private bool _primary;
        private bool _danger;
        private int _radiusPx = Tok.ButtonRadius;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Ui.Panel;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Ui.HPrimary;
            Font = Ui.BodyBold;
            _hoverT = new Tween(this, 0f);
            _pressT = new Tween(this, 0f);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && Parent.BackColor != Color.Transparent)
            {
                BackColor = Parent.BackColor;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        public bool Primary
        {
            get { return _primary; }
            set { _primary = value; Invalidate(); }
        }

        public bool Danger
        {
            get { return _danger; }
            set { _danger = value; Invalidate(); }
        }

        /// <summary>圆角（设计像素），默认 8。</summary>
        public int Radius
        {
            get { return _radiusPx; }
            set { _radiusPx = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Ui.S(_radiusPx);
            float h = _hoverT.Value;
            float p = _pressT.Value;
            Color fill;
            Color border;
            Color text;
            if (!Enabled)
            {
                // 禁用态保留描边与底纹，只降低文字对比度。
                fill = Ui.T.BtnDisabledFill;
                border = Ui.Stroke;
                text = Ui.T.BtnDisabledText;
            }
            else if (_primary)
            {
                fill = Ui.Mix(Ui.Accent, Ui.AccentSoft, h);
                if (p > 0)
                {
                    fill = Ui.Mix(fill, Ui.AccentDeep, p * 0.85f);
                }
                border = fill;
                text = Ui.OnAccent;
            }
            else if (_danger)
            {
                fill = Ui.Mix(Ui.T.DangerFill, Ui.T.DangerFillHover, h);
                border = Ui.T.DangerBorder;
                text = Ui.T.DangerText;
            }
            else
            {
                fill = Ui.Mix(Ui.Surface, Ui.PanelHover, h);
                if (p > 0)
                {
                    fill = Ui.Mix(fill, Ui.PanelActive, p);
                }
                border = Ui.Mix(Ui.Stroke, Ui.TextWeak, Math.Max(h * 0.4f, p * 0.5f));
                text = Ui.TextPrimary;
            }

            Ui.FillRound(g, r, radius, fill);
            Ui.DrawRound(g, r, radius, border, Ui.BorderWidth);
            Ui.DrawText(g, Text, Font, text, r, Ui.Center);
            if (Focused && Enabled)
            {
                Ui.DrawFocus(g, r, radius, _primary ? Ui.OnAccent : Ui.TextSecondary);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hoverT.To(1f, Tok.MotionMs);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverT.To(0f, Tok.MotionMs);
            _pressT.Snap(0f);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressT.To(1f, 90);
                Focus();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressT.To(0f, Tok.MotionMs);
            base.OnMouseUp(e);
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
                if (Enabled)
                {
                    OnClick(EventArgs.Empty);
                }
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled)
            {
                _hoverT.Snap(0f);
                _pressT.Snap(0f);
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hoverT.Dispose();
                _pressT.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>自绘的复选行：一排显示 勾选 / 国旗 / 机名 / 国家 / 状态标签。</summary>
    internal class CheckRow : Control
    {
        private bool _checked = true;
        private bool _hover;
        private bool _down;
        private string _title = "";
        private string _country = "";
        private string _model = "";
        private string _folder = "";
        private string _levelWeight = "";
        private string _subtitle = "";
        private string _badge = "";
        private int _badgeKind = 1;
        private bool _highlight;
        private string _nationCode = "";
        private int _titleWidth = 220;

        public CheckRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.Panel;
            Height = Ui.S(46);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; Invalidate(); }
        }

        public string Country
        {
            get { return _country; }
            set { _country = value; Invalidate(); }
        }

        public string Model
        {
            get { return _model; }
            set { _model = value; Invalidate(); }
        }

        public string Folder
        {
            get { return _folder; }
            set { _folder = value; Invalidate(); }
        }

        /// <summary>内联在「国家」之后的等级/权重，例如「VII · 11.7」；空值显示「—」。</summary>
        public string LevelWeight
        {
            get { return _levelWeight; }
            set { _levelWeight = value; Invalidate(); }
        }

        /// <summary>设置页用的第二行说明；设置后按两排绘制。</summary>
        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value; Invalidate(); }
        }

        public string NationCode
        {
            get { return _nationCode; }
            set { _nationCode = value; Invalidate(); }
        }

        /// <summary>1 = 绿色（上次导入），2 = 红色（未识别），3 = 中性（准备就绪）。</summary>
        public int BadgeKind
        {
            get { return _badgeKind; }
            set { _badgeKind = value; Invalidate(); }
        }

        public string Badge
        {
            get { return _badge; }
            set { _badge = value; Invalidate(); }
        }

        public bool Highlight
        {
            get { return _highlight; }
            set { _highlight = value; Invalidate(); }
        }

        /// <summary>机名列的固定宽度，用来让每行的国家从同一 x 开始。</summary>
        public int TitleWidth
        {
            get { return _titleWidth; }
            set { _titleWidth = Math.Max(Ui.S(80), value); Invalidate(); }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && Parent.BackColor != Color.Transparent)
            {
                BackColor = Parent.BackColor;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _highlight ? Ui.Mix(Ui.CardBg, Ui.Accent, 0.16f) : Ui.CardBg;
            if (_hover)
            {
                fill = Ui.Mix(fill, Ui.PanelHover, 0.55f);
            }
            if (_down)
            {
                fill = Ui.Mix(fill, Ui.Page, 0.2f);
            }
            Ui.FillRound(g, r, Ui.RButton, fill);
            if (_highlight)
            {
                Ui.FillRound(g, new Rectangle(r.X, r.Y + Ui.S(7), Ui.S(3), r.Height - Ui.S(14)), Ui.S(2), Ui.Accent);
            }

            int box = Ui.S(17);
            int by = (Height - box) / 2;
            // 与左侧高亮条留出间距，避免勾选框压住黄条。
            Rectangle boxRect = new Rectangle(Ui.S(10), by, box, box);
            if (_checked)
            {
                Ui.FillRound(g, boxRect, Ui.S(4), Ui.Accent);
                using (Pen pen = new Pen(Ui.T.CheckMark, Math.Max(1.6f, Ui.Scale * 1.8f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new Point[] {
                        new Point(boxRect.X + box / 4, boxRect.Y + box / 2),
                        new Point(boxRect.X + box * 2 / 5, boxRect.Y + box * 3 / 4),
                        new Point(boxRect.X + box * 4 / 5, boxRect.Y + box / 4)
                    });
                }
            }
            else
            {
                Ui.DrawRound(g, boxRect, Ui.S(4), Ui.Stroke, Ui.BorderWidth);
            }

            int titleX = boxRect.Right + Ui.S(11);
            if (_nationCode.Length > 0)
            {
                int flagW = Ui.S(24);
                int flagH = Ui.S(16);
                Rectangle flagRect = new Rectangle(titleX, (Height - flagH) / 2, flagW, flagH);
                NationInfo.Draw(g, flagRect, _nationCode);
                titleX = flagRect.Right + Ui.S(9);
            }

            int badgeWidth = 0;
            if (_badge.Length > 0)
            {
                Size size = TextRenderer.MeasureText(_badge, Ui.SmallBold);
                badgeWidth = size.Width + Ui.S(14);
                int pillH = Ui.S(19);
                Rectangle pill = new Rectangle(Width - badgeWidth - Ui.S(8), (Height - pillH) / 2, badgeWidth, pillH);
                Color badgeColor = _badgeKind == 2 ? Ui.Bad : (_badgeKind == 1 ? Ui.Good : Ui.TextDim);
                Color badgeFill = _badgeKind == 2 ? Ui.BadBg : Ui.T.CardBg;
                Ui.FillRound(g, pill, pill.Height / 2, badgeFill);
                Ui.DrawRound(g, pill, pill.Height / 2, badgeColor, Ui.BorderWidth);
                Ui.DrawText(g, _badge, Ui.SmallBold, badgeColor, pill, Ui.Center);
            }

            int rightLimit = Width - badgeWidth - (_badge.Length > 0 ? Ui.S(16) : Ui.S(10));

            if (_subtitle.Length > 0)
            {
                Rectangle titleRect = new Rectangle(titleX, 0, Math.Max(Ui.S(20), rightLimit - titleX), Height / 2);
                Ui.DrawText(g, _title, Ui.BodyBold, Ui.Text, titleRect,
                        TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                Rectangle subRect = new Rectangle(titleX, Height / 2 + Ui.S(1),
                                                  Math.Max(Ui.S(20), Width - titleX - Ui.S(10)), Height / 2 - Ui.S(2));
                Ui.DrawText(g, _subtitle, Ui.Small, _highlight ? Ui.Text : Ui.TextDim, subRect,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                return;
            }

            // 一排：机名（固定栏宽） + 国家 + 等级/权重（内联，不新增行）
            Rectangle nameRect = new Rectangle(titleX, 0, Math.Min(_titleWidth, Math.Max(Ui.S(20), rightLimit - titleX)), Height);
            Ui.DrawText(g, _title, Ui.BodyBold, Ui.Text, nameRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            int cursorX = titleX + Math.Min(_titleWidth, Math.Max(Ui.S(20), rightLimit - titleX)) + Ui.S(6);
            if (_country.Length > 0 && cursorX < rightLimit)
            {
                int maxCountry = Math.Max(Ui.S(10), rightLimit - cursorX);
                int countryW = TextRenderer.MeasureText(_country, Ui.Small).Width + Ui.S(2);
                if (countryW > maxCountry)
                {
                    countryW = maxCountry;
                }
                Rectangle countryRect = new Rectangle(cursorX, 0, countryW, Height);
                Ui.DrawText(g, _country, Ui.Small, _highlight ? Ui.Text : Ui.TextDim, countryRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                cursorX += countryW + Ui.S(10);
            }

            if (cursorX < rightLimit)
            {
                string meta = string.IsNullOrEmpty(_levelWeight) ? "—" : _levelWeight;
                Rectangle metaRect = new Rectangle(cursorX, 0, Math.Max(Ui.S(8), rightLimit - cursorX), Height);
                Ui.DrawText(g, meta, Ui.Small, Ui.TextFaint, metaRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.RButton, Ui.TextSecondary);
            }
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

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnClick(EventArgs e)
        {
            _checked = !_checked;
            Invalidate();
            base.OnClick(e);
        }
    }

    /// <summary>浮层数据：简称/全名两行标题 + 键值 + 状态标签。</summary>
    internal class DetailInfo
    {
        public string ShortName = "";
        public string FullName = "";
        public string Country = "";
        public string NationCode = "";
        public string Model = "";
        public string Folder = "";
        public bool FolderExists;
        public string Status = "";
        public int StatusKind = 3; // 1 绿色，2 红色，3 中性
        /// <summary>等级 / 权重（只显示 RB），例如「VIII · 12.7」；缺数据为「—」。</summary>
        public string LevelWeight = "";
        /// <summary>三档不一致时的补充行，例如「AB 12.7 / RB 12.7 / SB 12.7」；相同或缺失为空。</summary>
        public string LevelWeightModes = "";
    }

    /// <summary>悬停浮层：左图右文，标题两行（简称在上、全名在下），三行键值 + 状态标签。</summary>
    internal class DetailCard : Form
    {
        private DetailInfo _info = new DetailInfo();
        private Bitmap _icon;
        public string UnitId;

        private Timer _timer;
        private int _targetTop;
        private int _hideToken;

        // 设计尺寸（96DPI 逻辑像素），绘制时统一 Ui.S
        private const int Pad = 14;
        private const int IconBox = 96;
        private const int Gap = 12;
        private const int TitleGap = 4;
        private const int DividerGap = 8;
        private const int RowH = 22;
        private const int LabelW = 78;
        private const int FolderLineH = 18;

        public DetailCard()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Ui.Panel;
            DoubleBuffered = true;
            Font = Ui.Body;
            AutoScaleMode = AutoScaleMode.None;
            // 悬停浮层必须盖在主窗口上方；无激活显示，不会抢走鼠标所在行的焦点。
            TopMost = true;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
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

        public void SetIcon(Bitmap icon)
        {
            // 图标缓存会按 LRU 释放位图：存一份自己的副本，避免浮层持有已释放对象。
            Bitmap previous = _icon;
            _icon = icon == null ? null : new Bitmap(icon);
            if (previous != null)
            {
                try { previous.Dispose(); } catch { }
            }
            Invalidate();
        }

        public void SetData(DetailInfo info)
        {
            _info = info == null ? new DetailInfo() : info;
            Invalidate();
        }

        private int CardWidth()
        {
            return Ui.S(500);
        }

        private int TextLeft()
        {
            return Ui.S(Pad + IconBox + Gap);
        }

        private int TextWidth()
        {
            return CardWidth() - TextLeft() - Ui.S(Pad);
        }

        private int LabelWidth()
        {
            return Ui.S(LabelW);
        }

        private int ValueLeft()
        {
            return TextLeft() + LabelWidth() + Ui.S(6);
        }

        private int TitleLineH()
        {
            return Math.Max(Ui.S(19), Ui.LineHeight(Ui.CardTitle));
        }

        private int FullLineH()
        {
            return Math.Max(Ui.S(17), Ui.LineHeight(Ui.CardSub));
        }

        private int StatusWidth()
        {
            if (string.IsNullOrEmpty(_info.Status))
            {
                return 0;
            }
            return TextRenderer.MeasureText(_info.Status, Ui.SmallBold).Width + Ui.S(16);
        }

        private int FullLines()
        {
            if (string.IsNullOrEmpty(_info.FullName) ||
                string.Equals(_info.FullName, _info.ShortName, StringComparison.Ordinal))
            {
                return 0;
            }
            int count = Ui.WrapLines(_info.FullName, Ui.CardSub, TextWidth()).Count;
            return Math.Min(2, Math.Max(1, count));
        }

        private int TitleBlockHeight()
        {
            int h = TitleLineH();
            int full = FullLines();
            if (full > 0)
            {
                h += Ui.S(TitleGap) + full * FullLineH();
            }
            return h;
        }

        private int FolderLines()
        {
            if (string.IsNullOrEmpty(_info.Folder))
            {
                return 1;
            }
            int width = Math.Max(Ui.S(60), TextWidth() - LabelWidth() - Ui.S(6));
            return Math.Max(1, Ui.WrapLines(_info.Folder, Ui.CardValue, width).Count);
        }

        private bool HasModesLine()
        {
            return !string.IsNullOrEmpty(_info.LevelWeightModes);
        }

        private int TextBlockHeight()
        {
            int rows = Ui.S(RowH) * 3 + (HasModesLine() ? Ui.S(RowH) : 0);
            return TitleBlockHeight() + Ui.S(DividerGap) + rows +
                   Math.Max(Ui.S(RowH), FolderLines() * Ui.S(FolderLineH));
        }

        /// <summary>裁掉透明边距后的图标显示尺寸（保持等比，限制在 96×96 内，显示高度 72~96）。</summary>
        private Size IconSize()
        {
            if (_icon == null)
            {
                return new Size(Ui.S(IconBox), Ui.S(IconBox));
            }
            int box = Ui.S(IconBox);
            float scale = Math.Min((float)box / _icon.Width, (float)box / _icon.Height);
            return new Size(Math.Max(1, (int)(_icon.Width * scale)), Math.Max(1, (int)(_icon.Height * scale)));
        }

        public Size MeasureCard()
        {
            int content = Math.Max(TextBlockHeight(), IconSize().Height);
            return new Size(CardWidth(), content + Ui.S(Pad) * 2);
        }

        public void ShowAt(Point anchor, Rectangle bounds)
        {
            _hideToken++;
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
            Size size = MeasureCard();
            int x = anchor.X;
            int y = anchor.Y;
            if (x + size.Width > bounds.Right - Ui.S(8))
            {
                x = bounds.Right - size.Width - Ui.S(8);
            }
            if (x < bounds.Left + Ui.S(8))
            {
                x = bounds.Left + Ui.S(8);
            }
            if (y + size.Height > bounds.Bottom - Ui.S(8))
            {
                y = anchor.Y - size.Height - Ui.S(40);
            }
            if (y < bounds.Top + Ui.S(8))
            {
                y = bounds.Top + Ui.S(8);
            }
            ClientSize = size;
            using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, size.Width, size.Height), Ui.RCard))
            {
                Region = new Region(path);
            }
            Location = new Point(x, y);
            _targetTop = y;

            if (!Visible)
            {
                Opacity = 0;
                Top = y + Ui.S(6);
                Show();
            }
            else
            {
                Opacity = 1;
                Top = y;
            }
            BringToFront();
            StartAnimation();
        }

        private void StartAnimation()
        {
            if (!Ui.Animations)
            {
                Opacity = 1;
                Top = _targetTop;
                Invalidate();
                return;
            }
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            _timer = new Timer();
            _timer.Interval = 16;
            DateTime start = DateTime.UtcNow;
            int startTop = Top;
            _timer.Tick += delegate
            {
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 170.0;
                if (t >= 1 || IsDisposed)
                {
                    if (!IsDisposed)
                    {
                        Opacity = 1;
                        Top = _targetTop;
                        Invalidate();
                    }
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                    return;
                }
                Opacity = Math.Min(1.0, t * 1.6);
                Top = startTop + (int)((_targetTop - startTop) * t);
            };
            _timer.Start();
        }

        public void HideCard()
        {
            if (!Visible)
            {
                return;
            }
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
            if (!Ui.Animations)
            {
                Hide();
                return;
            }
            Timer fade = new Timer();
            fade.Interval = 16;
            DateTime start = DateTime.UtcNow;
            double startOpacity = Opacity;
            int startTop = Top;
            int token = ++_hideToken;
            fade.Tick += delegate
            {
                if (token != _hideToken)
                {
                    fade.Stop();
                    fade.Dispose();
                    return;
                }
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 130.0;
                if (t >= 1 || IsDisposed)
                {
                    fade.Stop();
                    fade.Dispose();
                    if (!IsDisposed)
                    {
                        Hide();
                        Opacity = 1;
                    }
                    return;
                }
                Opacity = Math.Max(0, startOpacity * (1 - t));
                Top = startTop - (int)(Ui.S(4) * t);
            };
            fade.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            Ui.FillRound(g, r, Ui.RCard, Ui.Surface);
            Ui.DrawRound(g, r, Ui.RCard, Ui.Stroke, Ui.BorderWidth);

            int textH = TextBlockHeight();
            int iconH = IconSize().Height;
            int contentH = Math.Max(textH, iconH);
            int textTop = Ui.S(Pad) + (contentH - textH) / 2;
            int iconTop = Ui.S(Pad) + (contentH - iconH) / 2;

            DrawIcon(g, iconTop);

            int y = textTop;
            string shortName = string.IsNullOrEmpty(_info.ShortName) ? _info.FullName : _info.ShortName;
            int statusW = StatusWidth();
            int titleW = TextWidth();
            if (statusW > 0)
            {
                titleW -= statusW + Ui.S(8);
            }
            Ui.DrawText(g, shortName, Ui.CardTitle, Ui.Text,
                    new Rectangle(TextLeft(), y, Math.Max(Ui.S(40), titleW), TitleLineH()),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            y += TitleLineH();
            int fullLines = FullLines();
            if (fullLines > 0)
            {
                y += Ui.S(TitleGap);
                List<string> lines = Ui.WrapLines(_info.FullName, Ui.CardSub, TextWidth());
                for (int i = 0; i < lines.Count && i < fullLines; i++)
                {
                    Ui.DrawText(g, lines[i], Ui.CardSub, Ui.TextDim,
                            new Rectangle(TextLeft(), y + i * FullLineH(), TextWidth(), FullLineH()),
                            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                }
                y += fullLines * FullLineH();
            }
            if (statusW > 0)
            {
                DrawStatus(g, statusW, textTop);
            }
            y += Ui.S(DividerGap);
            using (Pen pen = new Pen(Ui.StrokeSoft, Ui.BorderWidth))
            {
                g.DrawLine(pen, TextLeft(), y - Ui.S(4), ClientSize.Width - Ui.S(Pad), y - Ui.S(4));
            }

            DrawField(g, "国家", _info.Country, string.IsNullOrEmpty(_info.Country), ref y);
            DrawLevelWeight(g, ref y);
            DrawField(g, "机型代号", _info.Model, string.IsNullOrEmpty(_info.Model), ref y);
            DrawFolderField(g, ref y);
        }

        /// <summary>等级/权重：只显示 RB；三档不一致时下一行补 AB/RB/SB。</summary>
        private void DrawLevelWeight(Graphics g, ref int y)
        {
            bool missing = string.IsNullOrEmpty(_info.LevelWeight);
            string shown = missing ? "—" : _info.LevelWeight;
            Ui.DrawText(g, "等级 / 权重", Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(TextLeft(), y, LabelWidth(), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            Ui.DrawText(g, shown, Ui.CardValue, missing ? Ui.TextFaint : Ui.Text,
                    new Rectangle(ValueLeft(), y, TextLeft() + TextWidth() - ValueLeft(), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
            y += Ui.S(RowH);
            if (HasModesLine())
            {
                Ui.DrawText(g, _info.LevelWeightModes, Ui.CardValue, Ui.TextDim,
                        new Rectangle(ValueLeft(), y, TextLeft() + TextWidth() - ValueLeft(), Ui.S(RowH)),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                        TextFormatFlags.EndEllipsis);
                y += Ui.S(RowH);
            }
        }

        private void DrawStatus(Graphics g, int width, int top)
        {
            int pillH = Ui.S(20);
            Rectangle pill = new Rectangle(ClientSize.Width - Ui.S(Pad) - width, top, width, pillH);
            Color color = _info.StatusKind == 2 ? Ui.Bad : (_info.StatusKind == 1 ? Ui.Good : Ui.TextDim);
            Color fill = _info.StatusKind == 2 ? Ui.BadBg : (_info.StatusKind == 1 ? Ui.GoodBg : Ui.T.CardBg);
            Ui.FillRound(g, pill, pill.Height / 2, fill);
            Ui.DrawRound(g, pill, pill.Height / 2, color, Ui.BorderWidth);
            Ui.DrawText(g, _info.Status, Ui.SmallBold, color, pill, Ui.Center);
        }

        private void DrawIcon(Graphics g, int top)
        {
            Size size = IconSize();
            Rectangle area = new Rectangle(Ui.S(Pad), top, size.Width, size.Height);
            if (_icon == null)
            {
                Rectangle box = new Rectangle(Ui.S(Pad), top, Ui.S(IconBox), Ui.S(IconBox));
                Ui.FillRound(g, box, Ui.RButton, Ui.Mix(Ui.CardBg, Ui.PanelHover, 0.6f));
                return;
            }
            System.Drawing.Drawing2D.InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(_icon, area);
            g.InterpolationMode = old;
        }

        private void DrawField(Graphics g, string label, string value, bool fallback, ref int y)
        {
            Ui.DrawText(g, label, Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(TextLeft(), y, LabelWidth(), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            int valueX = ValueLeft();
            string shown = string.IsNullOrEmpty(value) ? "未知" : value;
            if (string.Equals(label, "国家", StringComparison.Ordinal) && !string.IsNullOrEmpty(_info.NationCode))
            {
                int flagW = Ui.S(19);
                int flagH = Ui.S(13);
                Rectangle flagRect = new Rectangle(valueX, y + (Ui.S(RowH) - flagH) / 2, flagW, flagH);
                NationInfo.Draw(g, flagRect, _info.NationCode);
                valueX = flagRect.Right + Ui.S(6);
            }
            Ui.DrawText(g, shown, Ui.CardValue, fallback ? Ui.TextFaint : Ui.Text,
                    new Rectangle(valueX, y, TextLeft() + TextWidth() - valueX, Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            y += Ui.S(RowH);
        }

        private void DrawFolderField(Graphics g, ref int y)
        {
            bool fallback = string.IsNullOrEmpty(_info.Folder);
            string value = fallback ? "未知" : _info.Folder;

            Ui.DrawText(g, "文件夹", Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(TextLeft(), y, LabelWidth(), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);

            int valueWidth = Math.Max(Ui.S(60), TextWidth() - LabelWidth() - Ui.S(6));
            List<string> lines = Ui.WrapLines(value, Ui.CardValue, valueWidth);
            for (int i = 0; i < lines.Count; i++)
            {
                Rectangle lineRect = new Rectangle(ValueLeft(), y + i * Ui.S(FolderLineH), valueWidth, Ui.S(FolderLineH));
                Ui.DrawText(g, lines[i], Ui.CardValue, fallback ? Ui.TextFaint : Ui.Text, lineRect,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }

            y += Math.Max(Ui.S(RowH), lines.Count * Ui.S(FolderLineH));
        }
    }

    /// <summary>
    /// 拖放区：有内容时是顶部紧凑条，空列表时是整栏空态（图标 + 主文案 + 说明 + 选择文件）。
    /// 默认虚线描边，悬停实线提亮，拖拽经过变 Accent 并微亮背景。
    /// </summary>
    internal class DropZone : Control
    {
        private bool _hot;
        private bool _hover;
        private string _main = "";
        private string _sub = "";
        private bool _empty;
        private readonly Tween _hoverT;
        private readonly FlatButton _choose;

        public DropZone()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.Page;
            AllowDrop = true;
            TabStop = true;
            _hoverT = new Tween(this, 0f);

            _choose = new FlatButton();
            _choose.Text = "选择文件";
            _choose.Primary = true;
            _choose.Font = Ui.BodyBold;
            _choose.Visible = false;
            _choose.TabIndex = 0;
            Controls.Add(_choose);
            // 放在卡片底上，而不是页面底上，避免圆角处透出深色底。
            _choose.BackColor = Ui.Surface;
        }

        public FlatButton ChooseButton
        {
            get { return _choose; }
        }

        public bool Hot
        {
            get { return _hot; }
            set
            {
                if (_hot != value)
                {
                    _hot = value;
                    Invalidate();
                }
            }
        }

        /// <summary>空列表：整栏空态。</summary>
        public bool Empty
        {
            get { return _empty; }
            set
            {
                if (_empty == value)
                {
                    return;
                }
                _empty = value;
                _choose.Visible = value;
                if (value)
                {
                    _hover = false;
                    _hoverT.Snap(0f);
                }
                LayoutChoose();
                Invalidate();
            }
        }

        public string Main
        {
            get { return _main; }
            set { _main = value; Invalidate(); }
        }

        public string Sub
        {
            get { return _sub; }
            set { _sub = value; Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChoose();
            Invalidate();
        }

        private int EmptyBlockHeight()
        {
            return Ui.S(56) + Ui.Sp16 + Ui.LineHeight(Ui.CardTitle) + Ui.Sp8 +
                   Ui.LineHeight(Ui.Body) + Ui.Sp24 + Ui.HPrimary;
        }

        private void LayoutChoose()
        {
            if (!_empty || _choose == null)
            {
                return;
            }
            int width = Math.Max(Ui.S(112), TextRenderer.MeasureText(_choose.Text, _choose.Font).Width + Ui.S(40));
            int blockTop = Math.Max(0, (Height - EmptyBlockHeight()) / 2);
            int y = blockTop + Ui.S(56) + Ui.Sp16 + Ui.LineHeight(Ui.CardTitle) + Ui.Sp8 +
                    Ui.LineHeight(Ui.Body) + Ui.Sp24;
            _choose.SetBounds((Width - width) / 2, y, width, Ui.HPrimary);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            _hoverT.To(1f, Tok.MotionMs);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _hoverT.To(0f, Tok.MotionMs);
            base.OnMouseLeave(e);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            float t = _hoverT.Value;
            Color fill = _hot
                ? Ui.Mix(Ui.Surface, Ui.Accent, 0.08f)
                : Ui.Mix(Ui.Surface, Ui.PanelHover, t * 0.65f);
            Color border = _hot ? Ui.Accent : Ui.Mix(Ui.Stroke, Ui.TextWeak, t * 0.35f);
            Ui.FillRound(g, r, Ui.RCard, fill);

            bool dashed = !_hover && !_hot;
            using (GraphicsPath path = Ui.Round(r, Ui.RCard))
            using (Pen pen = new Pen(border, Ui.Hairline))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
                if (dashed)
                {
                    pen.DashStyle = DashStyle.Custom;
                    pen.DashPattern = new float[] { 3f, 2.5f };
                }
                g.DrawPath(pen, path);
            }

            if (_empty)
            {
                PaintEmpty(g, t);
            }
            else
            {
                PaintCompact(g, t);
            }

            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.RCard, Ui.TextSecondary);
            }
        }

        private void PaintEmpty(Graphics g, float t)
        {
            int blockTop = Math.Max(0, (Height - EmptyBlockHeight()) / 2);
            int icon = Ui.S(56);
            int iconX = (Width - icon) / 2;
            Rectangle iconRect = new Rectangle(iconX, blockTop, icon, icon);
            Ui.FillRound(g, iconRect, Ui.RCard, Ui.Mix(Ui.PanelHover, Ui.Card, 0.45f));
            Ui.DrawRound(g, iconRect, Ui.RCard, Ui.Mix(Ui.StrokeSoft, Ui.Stroke, t), Ui.BorderWidth);
            DrawDropGlyph(g, iconRect, _hot ? Ui.Accent : Ui.Mix(Ui.TextWeak, Ui.TextSecondary, t));

            Font mainFont = Ui.CardTitle;
            Font subFont = Ui.Body;
            int mainH = Ui.LineHeight(mainFont);
            int subH = Ui.LineHeight(subFont);
            int mainTop = iconRect.Bottom + Ui.Sp16;
            Ui.DrawText(g, _main, mainFont, Ui.TextPrimary,
                    new Rectangle(Ui.Sp24, mainTop, Width - Ui.Sp24 * 2, mainH),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            Ui.DrawText(g, _sub, subFont, Ui.Mix(Ui.TextSecondary, Ui.TextPrimary, t * 0.35f),
                    new Rectangle(Ui.Sp24, mainTop + mainH + Ui.Sp8, Width - Ui.Sp24 * 2, subH),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private void PaintCompact(Graphics g, float t)
        {
            int icon = Ui.S(38);
            int ix = Ui.Sp24;
            int iy = (Height - icon) / 2;
            Rectangle iconRect = new Rectangle(ix, iy, icon, icon);
            Ui.FillRound(g, iconRect, Ui.RButton, Ui.Mix(Ui.PanelHover, Ui.Card, 0.35f));
            DrawDropGlyph(g, iconRect, _hot ? Ui.Accent : Ui.Mix(Ui.TextWeak, Ui.TextSecondary, t));

            int textX = iconRect.Right + Ui.Sp16;
            Rectangle mainRect = new Rectangle(textX, 0, Math.Max(Ui.S(20), Width - textX - Ui.Sp16), Height);
            if (_sub.Length > 0)
            {
                mainRect = new Rectangle(textX, Ui.Sp8, Width - textX - Ui.Sp16, Height / 2 - Ui.Sp8);
                Ui.DrawText(g, _main, Ui.BodyBold, Ui.TextPrimary, mainRect,
                        TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                Rectangle subRect = new Rectangle(textX, Height / 2 + Ui.Sp4, Width - textX - Ui.Sp16, Height / 2 - Ui.Sp8);
                Ui.DrawText(g, _sub, Ui.Small, Ui.TextSecondary, subRect,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                Ui.DrawText(g, _main, Ui.BodyBold, Ui.TextPrimary, mainRect, Ui.LeftMiddle);
            }
        }

        /// <summary>向下箭头落入托盘，避免再用一个强调色图标。</summary>
        private void DrawDropGlyph(Graphics g, Rectangle box, Color color)
        {
            int arm = Math.Max(Ui.S(7), box.Width / 5);
            int cx = box.X + box.Width / 2;
            int cy = box.Y + box.Height / 2 - Ui.S(1);
            using (Pen pen = new Pen(color, Math.Max(1.4f, Ui.Scale * 1.6f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
                g.DrawLine(pen, cx - arm + Ui.S(1), cy + Ui.S(2), cx, cy + arm + Ui.S(1));
                g.DrawLine(pen, cx + arm - Ui.S(1), cy + Ui.S(2), cx, cy + arm + Ui.S(1));
                int tray = Math.Max(Ui.S(10), box.Width / 3);
                int trayY = cy + arm + Ui.S(7);
                g.DrawLine(pen, cx - tray, trayY, cx - tray, trayY + Ui.S(5));
                g.DrawLine(pen, cx + tray, trayY, cx + tray, trayY + Ui.S(5));
                g.DrawLine(pen, cx - tray, trayY + Ui.S(5), cx + tray, trayY + Ui.S(5));
            }
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
}
