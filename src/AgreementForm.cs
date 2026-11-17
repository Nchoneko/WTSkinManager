using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>首次启动时的用户协议：显示完整协议，同意才进入程序，拒绝直接退出。</summary>
    internal class AgreementForm : ModalForm
    {
        public const string Version = "1.1";

        public AgreementForm()
            : base("用户协议 / User Agreement", 820, 640)
        {
            MakeLabel("战雷涂装管理器 用户协议 / User Agreement", 24, 12, 760, 26,
                      Ui.H1, Ui.Text);

            TextBox text = new TextBox();
            text.Multiline = true;
            text.ReadOnly = true;
            text.ScrollBars = ScrollBars.Vertical;
            text.WordWrap = true;
            text.BackColor = Ui.BgSoft;
            text.ForeColor = Ui.Text;
            text.Font = Ui.Small;
            text.BorderStyle = BorderStyle.FixedSingle;
            text.Left = Ui.S(24);
            text.Top = Ui.S(48);
            text.Width = Ui.S(760);
            text.Height = Ui.S(400);
            text.Text = LoadAgreement();
            text.SelectionStart = 0;
            text.SelectionLength = 0;
            Content.Controls.Add(text);

            Label hint = MakeLabel(
                "同意后写入本地配置；拒绝将立即退出，不写入任何游戏或配置内容。",
                24, 440, 520, 22, Ui.Small, Ui.TextFaint);

            FlatButton reject = MakeButton("拒绝并退出", 440, 560, 150, false);
            reject.Danger = true;
            reject.Click += delegate { DialogResult = DialogResult.Abort; Close(); };
            FlatButton accept = MakeButton("我同意用户协议，开始使用", 600, 560, 190, true);
            accept.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Shown += delegate
            {
                Native.ThemeScrollbars(this);
                // 底部按钮按内容区高度倒排，避免 DPI 缩放下被裁切。
                int buttonY = Content.Height - Ui.S(18) - Ui.S(38);
                accept.SetBounds(Content.Width - Ui.S(24) - Ui.S(190), buttonY, Ui.S(190), Ui.S(38));
                reject.SetBounds(accept.Left - Ui.S(12) - Ui.S(150), buttonY, Ui.S(150), Ui.S(38));
                hint.Top = buttonY - Ui.S(28);
                text.Height = Math.Max(Ui.S(120), hint.Top - Ui.S(10) - text.Top);
                accept.Focus();
            };
        }

        /// <summary>把 Markdown 简化成可读纯文本（保留段落、去 #/**/表格分隔线）。</summary>
        private static string ToPlainText(string markdown)
        {
            string[] lines = (markdown ?? "").Replace("\r\n", "\n").Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                string trim = line.Trim();
                if (trim.IndexOf("本文件为本地草稿", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                if (trim == "---" || trim == "***")
                {
                    continue;
                }
                if (trim.StartsWith("|") && trim.EndsWith("|"))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(trim, @"^\|[\s:\-\|]+\|$"))
                    {
                        continue;
                    }
                    string row = trim.Trim('|').Replace("|", "  |  ").Trim();
                    sb.AppendLine(row);
                    continue;
                }
                if (trim.StartsWith("#"))
                {
                    line = trim.TrimStart('#').Trim();
                }
                else if (trim.StartsWith(">"))
                {
                    line = trim.TrimStart('>').Trim();
                }
                line = line.Replace("**", "").Replace("`", "");
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private static string LoadAgreement()
        {
            try
            {
                using (Stream stream = typeof(AgreementForm).Assembly.GetManifestResourceStream("user-agreement.md"))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            return ToPlainText(reader.ReadToEnd());
                        }
                    }
                }
            }
            catch
            {
            }
            return "未能加载用户协议文本。\r\n\r\n继续使用即表示你同意项目仓库中的 USER_AGREEMENT.md。";
        }
    }
}
