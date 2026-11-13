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
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.Page;
            Height = Ui.S(44);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        private Rectangle TabRect(int index)
        {
            int width = Ui.S(96);
            return new Rectangle(Ui.Sp24 + index * (width + Ui.Sp8), Ui.S(2), width, Height - Ui.S(4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            using (SolidBrush brush = new SolidBrush(Ui.Page))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (Pen pen = new Pen(Ui.StrokeSoft, Ui.BorderWidth))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                Rectangle r = TabRect(i);
                bool active = i == Active;
                if (active)
                {
                    Ui.FillRound(g, r, Ui.RButton, Ui.Surface);
                    Ui.DrawRound(g, r, Ui.RButton, Ui.Stroke, Ui.BorderWidth);
                    Ui.FillRound(g, new Rectangle(r.X + Ui.Sp12, r.Bottom - Ui.S(3), r.Width - Ui.Sp24, Ui.S(2)), Ui.S(1), Ui.Accent);
                }
                else if (_hover == i)
                {
                    Ui.FillRound(g, r, Ui.RButton, Ui.PanelHover);
                }
                Ui.DrawText(g, _labels[i], active ? Ui.BodyBold : Ui.Body,
                        active ? Ui.TextPrimary : Ui.TextSecondary, r, Ui.Center);
                if (active && Focused)
                {
                    Ui.DrawFocus(g, r, Ui.RButton, Ui.TextSecondary);
                }
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
            if (key == Keys.Left || key == Keys.Right || key == Keys.Space || key == Keys.Enter)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                int next = e.KeyCode == Keys.Left ? Math.Max(0, Active - 1) : Math.Min(_labels.Length - 1, Active + 1);
                if (next != Active)
                {
                    Active = next;
                    Invalidate();
                    if (PageChanged != null)
                    {
                        PageChanged(this, EventArgs.Empty);
                    }
                }
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                if (PageChanged != null)
                {
                    PageChanged(this, EventArgs.Empty);
                }
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
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
