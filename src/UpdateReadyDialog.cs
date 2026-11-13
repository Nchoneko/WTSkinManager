using System;
using System.Drawing;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 6px 圆角下载进度条：轨道用 PanelActive，填充 Accent，完成后变 Good。
    /// 只用现有的 Ui 颜色/字体与 Ui.S() 做 DPI 缩放。
    /// </summary>
    internal class UpdateProgressBar : Control
    {
        private double _value;
        private bool _complete;

        public UpdateProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Panel;
            TabStop = false;
            Height = Ui.S(6);
        }

        public void SetValue(double value, bool complete)
        {
            double clamped = value;
            if (clamped < 0)
            {
                clamped = 0;
            }
            if (clamped > 1)
            {
                clamped = 1;
            }
            if (Math.Abs(clamped - _value) < 0.0005 && complete == _complete)
            {
                return;
            }
            _value = clamped;
            _complete = complete;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            int height = Math.Max(4, Ui.S(6));
            int radius = Math.Max(2, height / 2);
            Rectangle track = new Rectangle(0, 0, Width, height);
            Ui.FillRound(g, track, radius, Ui.PanelActive);

            int filled = (int)Math.Round(Width * _value);
            if (filled > 0)
            {
                Rectangle fill = new Rectangle(0, 0, Math.Max(filled, radius * 2), height);
                Ui.FillRound(g, fill, radius, _complete ? Ui.Good : Ui.Accent);
            }
        }
    }

    /// <summary>
    /// 「更新已就绪」模态弹窗：深色现有样式，显示新旧版本、包大小与 SHA-256 校验结果；
    /// 「立即更新」为主按钮，「稍后」关闭；Esc = 稍后（由 ModalForm 统一处理）。
    /// </summary>
    internal class UpdateReadyDialog : ModalForm
    {
        public UpdateReadyDialog(string currentVersion, string newVersion, long packageSize)
            : base("更新已就绪", 520, 300)
        {
            string current = string.IsNullOrEmpty(currentVersion) ? "未知" : "v" + TrimV(currentVersion);
            string latest = string.IsNullOrEmpty(newVersion) ? "未知" : "v" + TrimV(newVersion);

            Row(24, 12, "新版本", latest, Ui.BodyBold, Ui.Accent);
            Row(24, 40, "当前版本", current, Ui.Body, Ui.TextDim);
            Row(24, 68, "安装包大小", DownloadFormat.Size(packageSize), Ui.Body, Ui.TextDim);
            Row(24, 96, "文件校验", "SHA-256 校验通过", Ui.Body, Ui.Good);

            Panel divider = new Panel();
            divider.BackColor = Ui.LineSoft;
            divider.Left = Ui.S(24);
            divider.Top = Ui.S(126);
            divider.Width = Ui.S(472);
            divider.Height = 1;
            Content.Controls.Add(divider);

            Label hint = MakeLabel("更新会关闭当前程序，替换完成后自动重新启动。\n" +
                                   "旧版本会备份到安装目录外的 backups 文件夹；替换失败会自动回滚并继续用旧版本。",
                                   24, 138, 472, 46, Ui.Small, Ui.TextFaint);
            hint.Height = Ui.S(46);

            FlatButton later = MakeButton("稍后", 296, 190, 96, false);
            later.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            FlatButton install = MakeButton("立即更新", 400, 190, 96, true);
            install.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private void Row(int x, int y, string title, string value, Font valueFont, Color valueColor)
        {
            MakeLabel(title, x, y + 3, 96, 22, Ui.Small, Ui.TextFaint);
            MakeLabel(value, x + 102, y, 370, 24, valueFont, valueColor);
        }

        private static string TrimV(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length > 0 && (value[0] == 'v' || value[0] == 'V'))
            {
                return value.Substring(1);
            }
            return value;
        }
    }
}
