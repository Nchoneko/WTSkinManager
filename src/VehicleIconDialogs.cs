using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 图标覆盖率窗口：成功 / 重试中 / 永久失败三类数字、缺失清单导出、
    /// 「重试失败项」入口（重置计数并立即请求一次）。
    /// </summary>
    internal class IconStatusForm : ModalForm
    {
        public bool RetryRequested;
        private readonly List<VehicleIconEntry> _failed = new List<VehicleIconEntry>();

        public IconStatusForm()
            : base(Lang.T("载具图标", "Vehicle icons"), 640, 520)
        {
            VehicleCatalog.Reload();
            List<VehicleIconEntry> all = VehicleCatalog.Snapshot();
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsOk)
                {
                    _failed.Add(all[i]);
                }
            }

            int ok = VehicleCatalog.OkCount;
            int retrying = VehicleCatalog.RetryingCount;
            int permanent = VehicleCatalog.PermanentCount;
            long bytes = VehicleCatalog.UniqueBytes();

            MakeLabel(Lang.T("清单共 ", "Entries: ") + VehicleCatalog.Count + Lang.T(" 条 · 成功 ", " · ok ") + ok + Lang.T(" · 重试中 ", " · retrying ") + retrying +
                      Lang.T(" · 永久失败 ", " · permanently failed ") + permanent, 24, 8, 580, 26, Ui.H1,
                      permanent > 0 ? Ui.Warn : Ui.Good);
            MakeLabel(Lang.T("唯一文件 ", "Unique files: ") + (ok - VehicleCatalog.DuplicateCount()) + Lang.T(" 张（sha256 去重 ", " (sha256 dedup ") +
                      VehicleCatalog.DuplicateCount() + Lang.T(" 条）· 体积 ", " · size ") +
                      string.Format(CultureInfo.InvariantCulture, "{0:N1} MB", bytes / 1048576.0) +
                      Lang.T(" · 清单版本 ", " · manifest ") + VehicleCatalog.Version, 24, 38, 580, 22, Ui.Small, Ui.TextDim);

            ListBox list = new ListBox();
            list.Left = Ui.S(24);
            list.Top = Ui.S(70);
            list.Width = Ui.S(590);
            list.Height = Ui.S(340);
            list.BackColor = Ui.BgSoft;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Ui.S(30);
            list.Font = Ui.Small;
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
                              text.StartsWith("…", StringComparison.Ordinal) ? Ui.Warn : Ui.TextDim;
                Ui.DrawText(e.Graphics, text, Ui.Small, color,
                        new Rectangle(e.Bounds.X + Ui.S(6), e.Bounds.Y, e.Bounds.Width - Ui.S(6), e.Bounds.Height),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                        TextFormatFlags.EndEllipsis);
            };
            for (int i = 0; i < _failed.Count && i < 200; i++)
            {
                VehicleIconEntry e = _failed[i];
                list.Items.Add(string.Format(CultureInfo.InvariantCulture, Lang.T("✕ {0} · {1}/{2} · 第 {3}/{4} 次 · {5}", "✕ {0} · {1}/{2} · try {3}/{4} · {5}"),
                    e.Slug, e.Nation, e.Branch, e.AttemptCount, VehicleCatalog.MaxAttempts,
                    e.LastError.Length == 0 ? Lang.T("尚未请求", "Not requested yet") : e.LastError));
            }
            if (_failed.Count > 200)
            {
                list.Items.Add(Lang.T("… 其余 ", "… and ") + (_failed.Count - 200) + Lang.T(" 条请导出缺失清单查看", " more; export the missing list"));
            }
            if (_failed.Count == 0)
            {
                list.Items.Add(Lang.T("全部载具图标都已就位，没有待重试或永久失败的条目。", "All vehicle icons are present; nothing to retry."));
            }
            Content.Controls.Add(list);

            FlatButton export = MakeButton(Lang.T("导出缺失清单", "Export missing list"), 24, 430, 156, false);
            export.Click += delegate
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = Lang.T("文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*", "Text files (*.txt)|*.txt|All files (*.*)|*.*");
                    dialog.FileName = Lang.T("缺失图标清单_", "missing-icons_") + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".txt";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    try
                    {
                        int n = VehicleCatalog.ExportMissing(dialog.FileName);
                        MessageBox.Show(this, Lang.T("已导出 ", "Exported ") + n + Lang.T(" 条缺失记录：\r\n", " missing entries:\r\n") + dialog.FileName,
                                        Lang.T("导出缺失清单", "Export missing list"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, Lang.T("导出失败：", "Export failed: ") + ex.Message, Lang.T("导出缺失清单", "Export missing list"),
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            FlatButton retry = MakeButton(Lang.T("重试失败项", "Retry failed items"), 330, 430, 150, false);
            retry.Enabled = _failed.Count > 0;
            retry.Click += delegate
            {
                RetryRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            FlatButton close = MakeButton(Lang.T("关闭", "Close"), 500, 430, 116, true);
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        }
    }
}
