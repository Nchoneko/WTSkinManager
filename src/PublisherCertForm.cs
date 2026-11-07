using System;
using System.Drawing;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 首次安装 / 覆盖更新时的「安装发布者签名」提示。自绘，与安装向导同一深色风格，
    /// 不使用 Windows 的证书安装弹窗。用户点「暂不安装」后本次安装不再重复询问。
    /// </summary>
    internal class PublisherCertForm : ModalForm
    {
        public bool Installed;
        public bool Skipped;
        public CertResult Result;

        private readonly Label _status;
        private readonly FlatButton _install;
        private readonly FlatButton _skip;

        public PublisherCertForm()
            : base("安装发布者签名", 640, 400)
        {
            MakeLabel("这个签名用来告诉 Windows 这个软件是谁发布的，减少「未知发布者」的安全提示。",
                      24, 18, 592, 40, Ui.Body, Ui.Text);
            MakeLabel("签名会安装到当前用户的受信任发布者列表，只影响当前用户，不需要管理员权限。",
                      24, 62, 592, 40, Ui.Body, Ui.Text);
            MakeLabel("安装后可以随时移除，不影响软件的其他功能。",
                      24, 106, 592, 40, Ui.Body, Ui.Text);

            MakeLabel("签名：CN=Nchoneko, E=nchoneko, C=CN, S=Guangdong", 24, 158, 592, 18, Ui.Small, Ui.TextFaint);
            MakeLabel("指纹：9F5C 5501 0135 AC41 4C2B 9CA6 7755 0AB9 68D7 1D76", 24, 178, 592, 18, Ui.Small, Ui.TextFaint);
            MakeLabel("只导入证书（.cer，仅含公钥），不会安装任何私钥。", 24, 198, 592, 18, Ui.Small, Ui.TextFaint);

            _status = MakeLabel("", 24, 228, 592, 50, Ui.Small, Ui.TextDim);

            _skip = MakeButton("暂不安装", 386, 300, 110, false);
            _skip.Click += delegate { Skip(); };
            _install = MakeButton("安装签名", 506, 300, 110, true);
            _install.Click += delegate { DoInstall(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                _install.Focus();
            }
            catch
            {
            }
        }

        private void DoInstall()
        {
            _install.Enabled = false;
            try
            {
                Result = PublisherCert.InstallPackage();
            }
            finally
            {
                _install.Enabled = true;
            }
            if (Result != null && Result.Ok)
            {
                Installed = true;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            _status.ForeColor = Ui.Warn;
            _status.Text = (Result == null ? "导入失败。" : Result.Message) + Environment.NewLine +
                           "可以点「暂不安装」继续安装，不影响软件功能。";
            _install.Text = "重试";
        }

        private void Skip()
        {
            Skipped = true;
            PublisherCert.SkippedThisRun = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            // 直接关窗口 / 按 Esc 等同「暂不安装」，本次安装不再追问。
            if (!Installed && !Skipped)
            {
                Skipped = true;
                PublisherCert.SkippedThisRun = true;
            }
        }
    }
}
