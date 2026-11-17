using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 安装向导：选安装目录 + 确认游戏目录。
    /// 检测到已安装时在顶部提示版本与路径，自动填入目录，主按钮变成「更新到 vX」；
    /// 用户改到其他目录则按全新安装处理。未安装时布局与旧版完全一致。
    /// </summary>
    internal class SetupForm : ModalForm
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00040000; // WS_EX_APPWINDOW：无边框窗口也要有任务栏项
                cp.ExStyle &= ~0x00000080; // 去掉 WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private readonly TextBox _installBox;
        private readonly TextBox _gameBox;
        private readonly Label _resolvedLabel;
        private readonly Label _banner;
        private readonly ComboBox _candidates;
        private readonly FlatButton _primary;
        private readonly FlatButton _quit;
        private readonly Label _certStatus;
        private readonly InstallScan _scan;
        private readonly List<InstallCandidate> _candidateList = new List<InstallCandidate>();
        private readonly int _offset;
        private InstallCandidate _selected;
        public string InstallDir = "";

        private static int Offset(InstallScan scan)
        {
            // 未安装（包括只剩残留目录）时布局与旧版完全一致；残留只写日志，不占界面。
            if (scan == null || !scan.HasInstalled)
            {
                return 0;
            }
            return scan.Candidates.Count > 1 ? 88 : 52;
        }

        public SetupForm(string detectedGame)
            : this(detectedGame, InstallProbe.Scan())
        {
        }

        public SetupForm(string detectedGame, InstallScan scan)
            : base(scan != null && scan.HasInstalled ? Lang.T("更新 · 已安装", "Update · installed") : Lang.T("安装 · 首次使用", "Install · first run"),
                   720, 540 + Offset(scan) + 30)
        {
            _scan = scan ?? new InstallScan();
            _offset = Offset(scan);
            ShowInTaskbar = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            int o = _offset;

            // ---- 顶部：已安装提示 ----
            _banner = MakeLabel("", 24, 10, 672, 40, Ui.Small, Ui.Accent);
            _banner.Visible = _scan.HasInstalled;

            if (_scan.Candidates.Count > 1)
            {
                _candidates = new ComboBox();
                _candidates.Left = Ui.S(24);
                _candidates.Top = Ui.S(54);
                _candidates.Width = Ui.S(672);
                _candidates.Height = Ui.S(26);
                _candidates.DropDownStyle = ComboBoxStyle.DropDownList;
                _candidates.FlatStyle = FlatStyle.Flat;
                _candidates.BackColor = Ui.PanelActive;
                _candidates.ForeColor = Ui.Text;
                _candidates.Font = Ui.Body;
                _candidates.DrawMode = DrawMode.OwnerDrawFixed;
                _candidates.ItemHeight = Ui.S(21);
                _candidates.DrawItem += delegate(object sender, DrawItemEventArgs e)
                {
                    if (e.Index < 0 || e.Index >= _candidates.Items.Count)
                    {
                        return;
                    }
                    Ui.Smooth(e.Graphics);
                    bool hot = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                    using (SolidBrush fill = new SolidBrush(hot ? Ui.PanelHover : Ui.PanelActive))
                    {
                        e.Graphics.FillRectangle(fill, e.Bounds);
                    }
                    Ui.DrawText(e.Graphics, (string)_candidates.Items[e.Index], Ui.Body, Ui.Text,
                            new Rectangle(e.Bounds.X + Ui.S(6), e.Bounds.Y, e.Bounds.Width - Ui.S(10), e.Bounds.Height),
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                };
                for (int i = 0; i < _scan.Candidates.Count; i++)
                {
                    _candidateList.Add(_scan.Candidates[i]);
                    _candidates.Items.Add(_scan.Candidates[i].VersionLabel + "  ·  " + _scan.Candidates[i].Dir);
                }
                int bestIndex = 0;
                for (int i = 0; i < _candidateList.Count; i++)
                {
                    if (_candidateList[i] == _scan.Best)
                    {
                        bestIndex = i;
                        break;
                    }
                }
                _candidates.SelectedIndex = bestIndex;
                _candidates.SelectedIndexChanged += delegate
                {
                    int index = _candidates.SelectedIndex;
                    if (index >= 0 && index < _candidateList.Count)
                    {
                        _installBox.Text = _candidateList[index].Dir;
                    }
                };
                Content.Controls.Add(_candidates);
            }

            MakeLabel(Lang.T("安装目录", "Install folder"), 24, 16 + o, 200, 22, Ui.SmallBold, Ui.TextDim);
            TextBox installField;
            MakeField(24, 40 + o, 672, 40, out installField);
            _installBox = installField;
            _installBox.Text = _scan.Best != null ? _scan.Best.Dir : Config.DefaultInstallDir;
            _installBox.TextChanged += delegate { UpdateMode(); };

            MakeLabel(Lang.T("备份、日志、缓存都会放在这里，不会写进战雷目录。", "Backups, logs and cache are kept here, never in the game folder."), 24, 86 + o, 660, 20, Ui.Small, Ui.TextFaint);
            _resolvedLabel = MakeLabel("", 24, 108 + o, 660, 20, Ui.Small, Ui.Accent);

            FlatButton pickInstall = MakeButton(Lang.T("浏览…", "Browse…"), 538, 132 + o, 74, false);
            pickInstall.Click += delegate
            {
                string picked = PickFolder(Lang.T("选择安装目录", "Select install folder"), _installBox.Text, false);
                if (!string.IsNullOrEmpty(picked))
                {
                    _installBox.Text = picked;
                }
            };
            FlatButton useDefault = MakeButton(Lang.T("用默认目录", "Use default"), 620, 132 + o, 90, false);
            useDefault.Click += delegate { _installBox.Text = Config.DefaultInstallDir; };

            MakeLabel(Lang.T("建议选一个空文件夹；如果选的是盘符根目录或已有内容的文件夹，程序会自动装到其中的 WTSkinManager 子文件夹。", "Prefer an empty folder. If you choose a drive root or a non-empty folder, the app installs into a WTSkinManager subfolder."),
                     24, 184 + o, 672, 20, Ui.Small, Ui.TextFaint);

            MakeLabel(Lang.T("游戏目录", "Game folder"), 24, 214 + o, 200, 22, Ui.SmallBold, Ui.TextDim);
            TextBox gameField;
            MakeField(24, 238 + o, 672, 40, out gameField);
            _gameBox = gameField;
            _gameBox.Text = string.IsNullOrEmpty(detectedGame) ? Lang.T("尚未找到，请手动选择", "Not found; select manually") : detectedGame;
            _gameBox.ReadOnly = true;
            _gameBox.TabStop = false;

            MakeLabel(Lang.T("目录里应当能看到 UserSkins 文件夹（Steam 版一般在 steamapps\\common\\War Thunder）。", "The folder should contain UserSkins (Steam: steamapps\\common\\War Thunder)."),
                    24, 284 + o, 660, 20, Ui.Small, Ui.TextFaint);

            FlatButton detect = MakeButton(Lang.T("自动检测", "Auto-detect"), 476, 310 + o, 96, false);
            detect.Click += delegate
            {
                string best = GameLocator.FindBest();
                if (string.IsNullOrEmpty(best))
                {
                    MessageBox.Show(this, Lang.T("没有找到战雷安装目录，请手动选择。", "War Thunder installation not found; select it manually."), Lang.T("自动检测", "Auto-detect"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _gameBox.Text = best;
            };
            FlatButton pickGame = MakeButton(Lang.T("选择游戏目录…", "Select game folder…"), 582, 310 + o, 128, false);
            pickGame.Click += delegate
            {
                string picked = PickFolder(Lang.T("选择 War Thunder 安装目录（包含 UserSkins）", "Select War Thunder install folder (containing UserSkins)"), _gameBox.Text, true);
                if (!string.IsNullOrEmpty(picked))
                {
                    _gameBox.Text = picked;
                }
            };

            _quit = MakeButton(Lang.T("退出", "Exit"), 462, 424 + o, 96, false);
            _quit.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _primary = MakeButton(Lang.T("安装并启动", "Install and launch"), 592, 424 + o, 118, true);
            _primary.Click += delegate { DoInstall(); };

            // 发布者签名的安装结果就显示在这一行（成功 / 跳过 / 没有可用证书）。
            _certStatus = MakeLabel("", 24, 468 + o, 440, 20, Ui.Small, Ui.TextFaint);

            UpdateMode();
        }

        /// <summary>把用户选的目录规范化：根目录/有内容的文件夹 → 自动加 WTSkinManager 子文件夹。</summary>
        public static string ResolveInstallDir(string input)
        {
            string raw = (input == null ? "" : input.Trim());
            if (raw.Length == 0)
            {
                raw = Config.DefaultInstallDir;
            }
            string full;
            try
            {
                full = Path.GetFullPath(raw);
            }
            catch
            {
                return raw;
            }
            full = full.TrimEnd('\\');
            string root = Path.GetPathRoot(full);
            if (root != null && string.Equals(full, root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(full, "WTSkinManager");
            }
            if (Directory.Exists(full))
            {
                bool ours = File.Exists(Path.Combine(full, "WTSkinManager.exe")) ||
                            File.Exists(Path.Combine(full, "settings.ini"));
                if (!ours)
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(full).Length > 0)
                        {
                            return Path.Combine(full, "WTSkinManager");
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return full;
        }

        private void UpdateMode()
        {
            string resolved = ResolveInstallDir(_installBox.Text);
            _selected = _scan.Match(resolved);
            string text = _selected != null ? (Lang.T("更新到 v", "Update to v") + AppInfo.VersionText) : Lang.T("安装并启动", "Install and launch");
            _primary.Text = text;
            // 按文字实际宽度算按钮宽度，右对齐并留 24px 边距；左侧「退出」跟着让位，避免重叠/贴边。
            int width = Math.Max(Ui.S(118), TextRenderer.MeasureText(text, Ui.BodyBold).Width + Ui.S(44));
            _primary.Width = width;
            _primary.Left = Ui.S(720) - Ui.S(24) - width;
            if (_quit != null)
            {
                _quit.Left = _primary.Left - Ui.S(10) - _quit.Width;
            }
            UpdateBanner();
            UpdateResolved();
        }

        private void UpdateBanner()
        {
            if (_banner == null)
            {
                return;
            }
            if (_scan.Candidates.Count == 0)
            {
                _banner.Visible = false;
                return;
            }
            _banner.Visible = true;
            string firstLine;
            if (_selected != null)
            {
                _banner.ForeColor = Ui.Accent;
                if (_scan.Candidates.Count > 1)
                {
                    firstLine = Lang.T("检测到 ", "Detected ") + _scan.Candidates.Count + Lang.T(" 个已安装版本，默认选择最高版本 ", " installed versions; highest selected by default: ") +
                                _selected.VersionLabel + Lang.T("，位于 ", " at ") + _selected.Dir + Lang.T("。可在上方切换。", ". You can switch above.");
                }
                else if (_selected.VersionUnknown)
                {
                    firstLine = Lang.T("检测到已安装的程序，位于 ", "Installed program found at ") + _selected.Dir + Lang.T("（版本读取失败）。", " (version could not be read).");
                }
                else
                {
                    firstLine = Lang.T("检测到已安装版本 ", "Installed version ") + _selected.VersionLabel + Lang.T("，位于 ", " at ") + _selected.Dir + "。";
                }
            }
            else
            {
                _banner.ForeColor = Ui.Warn;
                InstallCandidate best = _scan.Best;
                firstLine = Lang.T("检测到已安装版本 ", "Installed version ") + (best == null ? "" : best.VersionLabel) +
                            Lang.T("，位于 ", " at ") + (best == null ? "" : best.Dir) +
                            Lang.T("；当前选择的是其他目录，将按全新安装处理，不覆盖已安装版本。", "; the selected folder is different, so this will be a fresh install and will not overwrite the installed version.");
            }
            _banner.Text = firstLine;
        }

        private void UpdateResolved()
        {
            string resolved = ResolveInstallDir(_installBox.Text);
            string typed = _installBox.Text.Trim();
            string suffix = typed.Length == 0 ||
                            string.Equals(resolved.TrimEnd('\\'), typed.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                ? ""
                : Lang.T("（自动加了子文件夹）", " (subfolder added automatically)");
            bool risky = IsUnderFolder(resolved, Environment.GetFolderPath(Environment.SpecialFolder.Desktop)) ||
                         IsUnderFolder(resolved, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            string mode = _selected != null
                ? Lang.T("（覆盖更新）", " (update in place)")
                : (_scan.HasInstalled ? Lang.T("（全新安装）", " (fresh install)") : "");
            _resolvedLabel.Text = Lang.T("将安装到：", "Install to: ") + resolved + suffix + mode +
                (risky ? Lang.T("   ⚠ 桌面/文档目录可能被安全软件拦截", "   ⚠ Desktop/Documents may be blocked by security software") : "");
            _resolvedLabel.ForeColor = risky ? Ui.Warn : Ui.Accent;
        }

        private static bool IsUnderFolder(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder))
            {
                return false;
            }
            try
            {
                string full = Path.GetFullPath(path).TrimEnd('\\');
                string root = Path.GetFullPath(folder).TrimEnd('\\');
                return full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _installBox.Select(0, 0);
            _installBox.Focus();
        }

        private void DoInstall()
        {
            string installDir = ResolveInstallDir(_installBox.Text);
            string gameDir = _gameBox.Text.Trim();
            if (installDir.Length == 0)
            {
                MessageBox.Show(this, Lang.T("请选择一个安装目录。", "Select an install folder."), Lang.T("安装", "Install"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Config.GameDirLooksValid(gameDir))
            {
                MessageBox.Show(this, Lang.T("这个游戏目录看起来不对：\n\n", "This game folder looks wrong:\n\n") + gameDir +
                                Lang.T("\n\n请选择同时包含 UserSkins 和 aces.exe（或 config.blk）的文件夹。", "\n\nChoose a folder containing UserSkins and aces.exe (or config.blk)."),
                                Lang.T("目录不正确", "Invalid folder"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string fullInstall;
            try
            {
                fullInstall = Path.GetFullPath(installDir).TrimEnd('\\');
            }
            catch
            {
                fullInstall = installDir;
            }
            string fullGame;
            try
            {
                fullGame = Path.GetFullPath(gameDir).TrimEnd('\\');
            }
            catch
            {
                fullGame = gameDir;
            }
            if (fullInstall.StartsWith(fullGame + "\\", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullInstall, fullGame, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, Lang.T("安装目录不能放在战雷的游戏目录里，请换一个位置。", "The install folder cannot be inside the game folder."), Lang.T("目录不合适", "Unsuitable folder"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 安装/更新前先处理仍在运行的主程序，否则覆盖文件会失败或需要重启。
            List<System.Diagnostics.Process> running = RunningInstance.FindOthers();
            if (running.Count > 0)
            {
                using (CloseRunningAppForm form = new CloseRunningAppForm(running.Count))
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                }
                string closeError;
                if (!RunningInstance.CloseAll(running, out closeError))
                {
                    MessageBox.Show(this, Lang.T("无法关闭正在运行的程序：\n\n", "Cannot close the running program:\n\n") + closeError +
                                    Lang.T("\n\n请手动退出后重试。", "\n\nExit it manually and try again."), Lang.T("关闭失败", "Close failed"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                InstallLog.Write("安装前已关闭正在运行的实例 " + running.Count + " 个");
            }

            InstallCandidate target = _scan.Match(fullInstall);
            if (target != null && target.Version != null)
            {
                int compare = AppInfo.AppVersion.CompareTo(target.Version);
                if (compare == 0)
                {
                    using (SameVersionForm form = new SameVersionForm(target.Version))
                    {
                        form.ShowDialog(this);
                        if (form.Choice != 1)
                        {
                            return;
                        }
                    }
                }
                else if (compare < 0)
                {
                    using (DowngradeForm form = new DowngradeForm(target.Version, AppInfo.AppVersion))
                    {
                        form.ShowDialog(this);
                        if (form.Choice != 1)
                        {
                            return;
                        }
                    }
                }
            }

            // 发布者签名：全新安装 / 覆盖更新时提示导入；已装过、或本次已选「暂不安装」都不再问。
            // 提示发生在写入程序文件之前，签名结果随后显示在安装界面底部。
            OfferPublisherCert(target == null ? Lang.T("全新安装", "fresh install") : Lang.T("覆盖更新", "update in place"), fullInstall);

            InstallResult result = null;
            while (true)
            {
                try
                {
                    result = Installer.Install(fullInstall, fullGame, target);
                    break;
                }
                catch (InstallBusyException busy)
                {
                    using (BusyRetryForm form = new BusyRetryForm(busy.Message))
                    {
                        form.ShowDialog(this);
                        if (!form.Retry)
                        {
                            return;
                        }
                    }
                }
                catch (InstallException failed)
                {
                    MessageBox.Show(this, failed.Message,
                                    failed.RolledBack ? Lang.T("更新失败（已回滚）", "Update failed (rolled back)") : Lang.T("更新失败（回滚未完成）", "Update failed (rollback incomplete)"),
                                    MessageBoxButtons.OK,
                                    failed.RolledBack ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
                    return;
                }
                catch (UnauthorizedAccessException ex)
                {
                    using (InstallFailForm fail = new InstallFailForm(fullInstall, ex.Message))
                    {
                        fail.ShowDialog(this);
                        if (fail.Choice == 2)
                        {
                            RetryElevated(fullInstall, fullGame);
                        }
                        else if (fail.Choice == 1)
                        {
                            _installBox.Text = Config.DefaultInstallDir;
                            UpdateMode();
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Lang.T("安装失败：\n\n", "Installation failed:\n\n") + ex.Message +
                                    "\n\n" + Installer.EnvironmentHint(),
                                    Lang.T("安装失败", "Installation failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (result == null)
            {
                return;
            }
            InstallLog.Write("界面：完成 " + (result.Fresh ? "全新安装" : "覆盖更新") +
                             "，v" + result.NewVersion.ToDisplay() + Lang.T("（数据 ", " (data ") + result.DataVersion + "）");
            InstallDir = fullInstall;
            string targetExe = Path.Combine(fullInstall, "WTSkinManager.exe");
            if (string.Equals(Path.GetFullPath(targetExe), Path.GetFullPath(Config.ExePath),
                              StringComparison.OrdinalIgnoreCase))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            try
            {
                InstallLog.Write("准备启动更新后的程序：" + targetExe);
                // 先释放单实例锁，再启动更新后的程序，避免新进程被当成重复启动。
                InstanceGuard.Release();
                InstallLog.Write("单实例锁已释放");
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = targetExe;
                info.WorkingDirectory = fullInstall;
                info.UseShellExecute = false;
                System.Diagnostics.Process started = System.Diagnostics.Process.Start(info);
                InstallLog.Write("已启动更新后的程序：" + (started == null ? "无进程返回" : "PID " + started.Id));
            }
            catch (Exception startError)
            {
                InstallLog.Write("启动更新后的程序失败：" + startError.Message);
                MessageBox.Show(this, Lang.T("安装/更新已完成，但没能自动启动新版程序：\n\n", "Installation finished but the new version could not be launched:\n\n") + startError.Message +
                                Lang.T("\n\n请手动打开 ", "\n\nPlease open manually: ") + targetExe,
                                Lang.T("需要手动启动", "Manual launch required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>提示并导入发布者签名。签名缺失 / 失败都不阻断安装，结果写安装日志。</summary>
        private void OfferPublisherCert(string mode, string installDir)
        {
            // 日志指向真正的安装目录（UpdateEngine 稍后会在同一文件里继续写安装过程）。
            InstallLog.Redirect(installDir);
            InstallLog.Write("发布者签名：检查 CurrentUser\\TrustedPublisher（指纹 " + PublisherCert.ExpectedThumbprint + "）");

            X509Certificate2 existing;
            if (PublisherCert.TryFindInstalled(out existing))
            {
                InstallLog.Write("发布者签名：已存在，不弹窗（主体 " +
                                 (existing == null ? PublisherCert.ExpectedSubject : existing.Subject) +
                                 Lang.T("，指纹 ", ", thumbprint ") + PublisherCert.ExpectedThumbprint + "）");
                SetCertStatus(Lang.T("发布者签名已存在", "Publisher signature already installed"), Ui.TextFaint);
                return;
            }
            if (PublisherCert.SkippedThisRun)
            {
                InstallLog.Write("发布者签名：本次安装已选择「暂不安装」，不重复询问；证书未导入");
                SetCertStatus(Lang.T("本次已跳过发布者签名安装，可在设置里补装", "Publisher signature skipped; you can install it later in Settings"), Ui.TextFaint);
                return;
            }

            byte[] bytes;
            string source;
            CertResult load = PublisherCert.LoadPackage(out bytes, out source);
            if (!load.Ok)
            {
                InstallLog.Write("发布者签名：不弹窗，" + load.Detail);
                SetCertStatus(Lang.T("没有可用的发布者签名证书，已跳过", "No usable publisher certificate; skipped"), Ui.TextFaint);
                return;
            }
            InstallLog.Write("发布者签名：弹出安装提示（" + mode + "）；" + load.Detail);

            bool installedNow;
            bool skipped;
            CertResult attempt;
            using (PublisherCertForm form = new PublisherCertForm())
            {
                form.ShowDialog(this);
                installedNow = form.Installed;
                skipped = form.Skipped;
                attempt = form.Result;
            }
            if (installedNow && attempt != null)
            {
                InstallLog.Write("发布者签名：用户选择「安装签名」→ " + attempt.Detail);
                SetCertStatus(Lang.T("发布者签名已安装（", "Publisher signature installed (") + PublisherCert.PublisherName + "）", Ui.Good);
            }
            else if (attempt != null && !attempt.Ok)
            {
                InstallLog.Write("发布者签名：用户尝试「安装签名」但导入失败：" + attempt.Detail);
                InstallLog.Write("发布者签名：用户随后" + (skipped ? "选择「暂不安装」" : "关闭了提示") +
                                 Lang.T("，安装继续；证书未导入，指纹 ", ", installation continues; certificate not imported, thumbprint ") + PublisherCert.ExpectedThumbprint);
                SetCertStatus(Lang.T("发布者签名导入失败，已跳过（可在设置里重试）", "Publisher signature import failed; skipped (retry in Settings)"), Ui.Warn);
            }
            else
            {
                InstallLog.Write("发布者签名：用户选择「暂不安装」，本次安装不再询问；证书未导入，指纹 " +
                                 PublisherCert.ExpectedThumbprint);
                SetCertStatus(Lang.T("已跳过发布者签名安装，可在设置里补装", "Publisher signature skipped; install it later in Settings"), Ui.TextFaint);
            }
        }

        private void SetCertStatus(string text, Color color)
        {
            if (_certStatus == null)
            {
                return;
            }
            _certStatus.Text = text;
            _certStatus.ForeColor = color;
            try
            {
                // 紧接着就是同步的安装流程（窗口会冻结），先把这一行画出来，
                // 保证「安装界面」上确实能看到签名结果。
                _certStatus.Refresh();
            }
            catch
            {
            }
        }

        /// <summary>用 UAC 重新启动自己，在管理员权限下只做「复制 + 写设置」，然后再拉起程序。</summary>
        private void RetryElevated(string installDir, string gameDir)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = Config.CurrentExe;
                info.Arguments = "--install \"" + installDir + "\" \"" + gameDir + "\"";
                info.Verb = "runas";
                info.UseShellExecute = true;
                System.Diagnostics.Process.Start(info);
                DialogResult = DialogResult.Cancel;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("没有拿到管理员权限，安装已取消。\n\n", "Administrator permission was not granted; installation canceled.\n\n") + ex.Message +
                                Lang.T("\n\n也可以直接改用默认目录，或用 D 盘里一个普通文件夹。", "\n\nYou can also use the default folder or a normal folder on another drive."),
                                Lang.T("安装", "Install"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string PickFolder(string title, string initial, bool requireGame)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = title;
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                {
                    dialog.SelectedPath = initial;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return "";
                }
                if (requireGame && !Config.GameDirLooksValid(dialog.SelectedPath))
                {
                    MessageBox.Show(this, Lang.T("这个目录里没有 UserSkins，确认是战雷安装目录吗？", "This folder has no UserSkins. Is it really the War Thunder install folder?"), Lang.T("提示", "Notice"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return dialog.SelectedPath;
            }
        }
    }

    /// <summary>同版本：提示可以重装或取消，默认不改动任何文件。</summary>
    internal class SameVersionForm : ModalForm
    {
        public int Choice; // 1=重新安装 0=取消

        public SameVersionForm(SemVer version)
            : base(Lang.T("已安装相同版本", "Same version installed"), 580, 292)
        {
            MakeLabel("已安装相同版本 " + (version == null ? "" : version.Display),
                      24, 14, 520, 26, Ui.H1, Ui.Accent);
            MakeLabel(Lang.T("安装目录里已经是这个版本。可以重新安装并覆盖当前程序文件；", "This version is already installed. Reinstalling overwrites the current program files;") +
                      Lang.T("settings.ini、shortnames.ini、备份、日志与缓存都会原样保留。", "settings.ini, shortnames.ini, backups, logs and cache are kept."),
                      24, 48, 520, 44, Ui.Small, Ui.TextDim);
            MakeLabel(Lang.T("如果只是想继续使用，直接取消即可。", "If you just want to keep using it, click Cancel."), 24, 100, 520, 20, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 338, 160, 96, false);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton reinstall = MakeButton(Lang.T("重新安装", "Reinstall"), 444, 160, 110, true);
            reinstall.Click += delegate { Choice = 1; DialogResult = DialogResult.OK; Close(); };
        }
    }

    /// <summary>降级：默认取消，只有明确点「继续降级」才执行。</summary>
    internal class DowngradeForm : ModalForm
    {
        public int Choice; // 1=继续降级 0=取消

        public DowngradeForm(SemVer oldVersion, SemVer newVersion)
            : base(Lang.T("这是降级安装", "Downgrade install"), 600, 330)
        {
            string oldText = oldVersion == null ? Lang.T("未知版本", "unknown version") : oldVersion.Display;
            string newText = newVersion == null ? Lang.T("当前安装包", "This package") : newVersion.Display;
            MakeLabel(Lang.T("已安装更高版本 ", "A newer version is installed ") + oldText, 24, 14, 540, 26, Ui.H1, Ui.Warn);
            MakeLabel(Lang.T("当前安装包是 ", "This package is ") + newText + Lang.T("，继续会把程序降级到旧版本。", ", continuing downgrades the app.") +
                      Lang.T("新版本里的修复与功能在这之后不会生效。", "Fixes and features from the newer version will not be available."),
                      24, 48, 540, 44, Ui.Small, Ui.TextDim);
            MakeLabel(Lang.T("推荐点「取消」，保留现在安装的版本。", "Recommended: click Cancel and keep the installed version."), 24, 96, 540, 20, Ui.Small, Ui.Warn);

            FlatButton proceed = MakeButton(Lang.T("继续降级", "Downgrade"), 404, 190, 110, false);
            proceed.Danger = true;
            proceed.Click += delegate { Choice = 1; DialogResult = DialogResult.OK; Close(); };
            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 522, 190, 96, true);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
        }
    }

    /// <summary>目标程序被占用：提示先关闭，提供「重试」。</summary>
    internal class BusyRetryForm : ModalForm
    {
        public bool Retry;

        public BusyRetryForm(string message)
            : base(Lang.T("程序正在运行", "Program is running"), 620, 360)
        {
            MakeLabel(Lang.T("无法覆盖正在运行的程序", "Cannot overwrite a running program"), 24, 14, 540, 26, Ui.H1, Ui.Warn);
            Label detail = MakeLabel(string.IsNullOrEmpty(message) ? Lang.T("程序正在运行，文件被占用。", "The program is running and its files are in use.") : message,
                                     24, 48, 560, 70, Ui.Small, Ui.TextDim);
            detail.AutoEllipsis = true;
            MakeLabel(Lang.T("请先关闭战雷涂装管理器（包括托盘里的窗口），然后点「重试」。", "Close WTSkinManager (including tray windows), then click Retry."),
                      24, 124, 560, 40, Ui.Small, Ui.TextDim);
            MakeLabel(Lang.T("本次更新尚未写入任何文件，不会留下半覆盖。", "No files have been written yet; the update is not partially applied."), 24, 168, 560, 20, Ui.Small, Ui.Good);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 404, 220, 96, false);
            cancel.Click += delegate { Retry = false; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton retry = MakeButton(Lang.T("重试", "Retry"), 510, 220, 96, true);
            retry.Click += delegate { Retry = true; DialogResult = DialogResult.OK; Close(); };
        }
    }

    /// <summary>安装/更新前发现主程序仍在运行：确认是否关闭并继续。</summary>
    internal class CloseRunningAppForm : ModalForm
    {
        public CloseRunningAppForm(int count)
            : base(Lang.T("程序正在运行", "Program is running"), 540, 260)
        {
            MakeLabel(Lang.T("检测到战雷涂装管理器正在运行", "WTSkinManager is running") + (count > 1 ? "（" + count + " 个实例）" : "") + "。",
                      24, 16, 492, 24, Ui.BodyBold, Ui.Text);
            MakeLabel(Lang.T("安装 / 更新需要先关闭它，未保存的操作会丢失。关闭后安装向导会继续。", "Install/update requires closing it; unsaved work will be lost. The wizard continues after closing."),
                      24, 48, 492, 40, Ui.Small, Ui.TextDim);
            MakeLabel(Lang.T("也可以选择「取消」，手动退出程序后重新打开安装包。", "You can also click Cancel, exit the app manually and reopen the installer."),
                      24, 92, 492, 20, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton(Lang.T("取消", "Cancel"), 300, 180, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton close = MakeButton(Lang.T("关闭并继续", "Close and continue"), 408, 180, 120, true);
            close.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Shown += delegate
            {
                int y = Content.Height - Ui.S(18) - Ui.S(38);
                close.SetBounds(Content.Width - Ui.S(24) - Ui.S(120), y, Ui.S(120), Ui.S(38));
                cancel.SetBounds(close.Left - Ui.S(10) - Ui.S(96), y, Ui.S(96), Ui.S(38));
                cancel.Focus();
            };
        }
    }
}
