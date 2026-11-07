using System;
using System.Drawing;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>标题栏下方的两个分页：导入 / 涂装管理。</summary>
    internal class PageTabs : Control
    {
        public int Active;
        public event EventHandler PageChanged;
        private int _hover = -1;
        private readonly string[] _labels = new string[] { "导入", "涂装管理" };

        public PageTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Bg;
            Height = Ui.S(44);
            Cursor = Cursors.Hand;
        }

        private Rectangle TabRect(int index)
        {
            int width = Ui.S(96);
            return new Rectangle(Ui.S(24) + index * (width + Ui.S(6)), Ui.S(2), width, Height - Ui.S(4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Bg))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (Pen pen = new Pen(Ui.LineSoft, 1f))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                Rectangle r = TabRect(i);
                bool active = i == Active;
                if (active)
                {
                    Ui.FillRound(g, r, Ui.S(8), Ui.PanelHover);
                    Ui.DrawRound(g, r, Ui.S(8), Ui.LineSoft, 1f);
                    Ui.FillRound(g, new Rectangle(r.X + Ui.S(12), r.Bottom - Ui.S(4), r.Width - Ui.S(24), Ui.S(2)), Ui.S(1), Ui.Accent);
                }
                else if (_hover == i)
                {
                    Ui.FillRound(g, r, Ui.S(8), Ui.Panel);
                }
                Ui.DrawText(g, _labels[i], active ? Ui.BodyBold : Ui.Body,
                        active ? Ui.Text : Ui.TextDim, r, Ui.Center);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hover = -1;
            for (int i = 0; i < _labels.Length; i++)
            {
                if (TabRect(i).Contains(e.Location))
                {
                    hover = i;
                }
            }
            if (hover != _hover)
            {
                _hover = hover;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                if (TabRect(i).Contains(e.Location) && i != Active)
                {
                    Active = i;
                    Invalidate();
                    if (PageChanged != null)
                    {
                        PageChanged(this, EventArgs.Empty);
                    }
                    return;
                }
            }
            base.OnMouseDown(e);
        }
    }
}
