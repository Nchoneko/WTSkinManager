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
            : base(scan != null && scan.HasInstalled ? "更新 · 已安装" : "安装 · 首次使用",
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

            MakeLabel("安装目录", 24, 16 + o, 200, 22, Ui.SmallBold, Ui.TextDim);
            TextBox installField;
            MakeField(24, 40 + o, 672, 40, out installField);
            _installBox = installField;
            _installBox.Text = _scan.Best != null ? _scan.Best.Dir : Config.DefaultInstallDir;
            _installBox.TextChanged += delegate { UpdateMode(); };

            MakeLabel("备份、日志、缓存都会放在这里，不会写进战雷目录。", 24, 86 + o, 660, 20, Ui.Small, Ui.TextFaint);
            _resolvedLabel = MakeLabel("", 24, 108 + o, 660, 20, Ui.Small, Ui.Accent);

            FlatButton pickInstall = MakeButton("浏览…", 538, 132 + o, 74, false);
            pickInstall.Click += delegate
            {
                string picked = PickFolder("选择安装目录", _installBox.Text, false);
                if (!string.IsNullOrEmpty(picked))
                {
                    _installBox.Text = picked;
                }
            };
            FlatButton useDefault = MakeButton("用默认目录", 620, 132 + o, 90, false);
            useDefault.Click += delegate { _installBox.Text = Config.DefaultInstallDir; };

            MakeLabel("建议选一个空文件夹；如果选的是盘符根目录或已有内容的文件夹，程序会自动装到其中的 WTSkinManager 子文件夹。",
                     24, 184 + o, 672, 20, Ui.Small, Ui.TextFaint);

            MakeLabel("游戏目录", 24, 214 + o, 200, 22, Ui.SmallBold, Ui.TextDim);
            TextBox gameField;
            MakeField(24, 238 + o, 672, 40, out gameField);
            _gameBox = gameField;
            _gameBox.Text = string.IsNullOrEmpty(detectedGame) ? "尚未找到，请手动选择" : detectedGame;
            _gameBox.ReadOnly = true;
            _gameBox.TabStop = false;

            MakeLabel("目录里应当能看到 UserSkins 文件夹（Steam 版一般在 steamapps\\common\\War Thunder）。",
                    24, 284 + o, 660, 20, Ui.Small, Ui.TextFaint);

            FlatButton detect = MakeButton("自动检测", 476, 310 + o, 96, false);
            detect.Click += delegate
            {
                string best = GameLocator.FindBest();
                if (string.IsNullOrEmpty(best))
                {
                    MessageBox.Show(this, "没有找到战雷安装目录，请手动选择。", "自动检测",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _gameBox.Text = best;
            };
            FlatButton pickGame = MakeButton("选择游戏目录…", 582, 310 + o, 128, false);
            pickGame.Click += delegate
            {
                string picked = PickFolder("选择 War Thunder 安装目录（包含 UserSkins）", _gameBox.Text, true);
                if (!string.IsNullOrEmpty(picked))
                {
                    _gameBox.Text = picked;
                }
            };

            _quit = MakeButton("退出", 462, 424 + o, 96, false);
            _quit.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _primary = MakeButton("安装并启动", 592, 424 + o, 118, true);
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
            string text = _selected != null ? ("更新到 v" + AppInfo.VersionText) : "安装并启动";
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
                    firstLine = "检测到 " + _scan.Candidates.Count + " 个已安装版本，默认选择最高版本 " +
                                _selected.VersionLabel + "，位于 " + _selected.Dir + "。可在上方切换。";
                }
                else if (_selected.VersionUnknown)
                {
                    firstLine = "检测到已安装的程序，位于 " + _selected.Dir + "（版本读取失败）。";
                }
                else
                {
                    firstLine = "检测到已安装版本 " + _selected.VersionLabel + "，位于 " + _selected.Dir + "。";
                }
            }
            else
            {
                _banner.ForeColor = Ui.Warn;
                InstallCandidate best = _scan.Best;
                firstLine = "检测到已安装版本 " + (best == null ? "" : best.VersionLabel) +
                            "，位于 " + (best == null ? "" : best.Dir) +
                            "；当前选择的是其他目录，将按全新安装处理，不覆盖已安装版本。";
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
                : "（自动加了子文件夹）";
            bool risky = IsUnderFolder(resolved, Environment.GetFolderPath(Environment.SpecialFolder.Desktop)) ||
                         IsUnderFolder(resolved, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            string mode = _selected != null
                ? "（覆盖更新）"
                : (_scan.HasInstalled ? "（全新安装）" : "");
            _resolvedLabel.Text = "将安装到：" + resolved + suffix + mode +
                (risky ? "   ⚠ 桌面/文档目录可能被安全软件拦截" : "");
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
                MessageBox.Show(this, "请选择一个安装目录。", "安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Config.GameDirLooksValid(gameDir))
            {
                MessageBox.Show(this, "这个游戏目录看起来不对：\n\n" + gameDir +
                                "\n\n请选择同时包含 UserSkins 和 aces.exe（或 config.blk）的文件夹。",
                                "目录不正确", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "安装目录不能放在战雷的游戏目录里，请换一个位置。", "目录不合适",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
            OfferPublisherCert(target == null ? "全新安装" : "覆盖更新", fullInstall);

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
                                    failed.RolledBack ? "更新失败（已回滚）" : "更新失败（回滚未完成）",
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
                    MessageBox.Show(this, "安装失败：\n\n" + ex.Message +
                                    "\n\n" + Installer.EnvironmentHint(),
                                    "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (result == null)
            {
                return;
            }
            InstallLog.Write("界面：完成 " + (result.Fresh ? "全新安装" : "覆盖更新") +
                             "，v" + result.NewVersion.ToDisplay() + "（数据 " + result.DataVersion + "）");
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
                MessageBox.Show(this, "安装/更新已完成，但没能自动启动新版程序：\n\n" + startError.Message +
                                "\n\n请手动打开 " + targetExe,
                                "需要手动启动", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                                 "，指纹 " + PublisherCert.ExpectedThumbprint + "）");
                SetCertStatus("发布者签名已存在", Ui.TextFaint);
                return;
            }
            if (PublisherCert.SkippedThisRun)
            {
                InstallLog.Write("发布者签名：本次安装已选择「暂不安装」，不重复询问；证书未导入");
                SetCertStatus("本次已跳过发布者签名安装，可在设置里补装", Ui.TextFaint);
                return;
            }

            byte[] bytes;
            string source;
            CertResult load = PublisherCert.LoadPackage(out bytes, out source);
            if (!load.Ok)
            {
                InstallLog.Write("发布者签名：不弹窗，" + load.Detail);
                SetCertStatus("没有可用的发布者签名证书，已跳过", Ui.TextFaint);
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
                SetCertStatus("发布者签名已安装（" + PublisherCert.PublisherName + "）", Ui.Good);
            }
            else if (attempt != null && !attempt.Ok)
            {
                InstallLog.Write("发布者签名：用户尝试「安装签名」但导入失败：" + attempt.Detail);
                InstallLog.Write("发布者签名：用户随后" + (skipped ? "选择「暂不安装」" : "关闭了提示") +
                                 "，安装继续；证书未导入，指纹 " + PublisherCert.ExpectedThumbprint);
                SetCertStatus("发布者签名导入失败，已跳过（可在设置里重试）", Ui.Warn);
            }
            else
            {
                InstallLog.Write("发布者签名：用户选择「暂不安装」，本次安装不再询问；证书未导入，指纹 " +
                                 PublisherCert.ExpectedThumbprint);
                SetCertStatus("已跳过发布者签名安装，可在设置里补装", Ui.TextFaint);
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
                MessageBox.Show(this, "没有拿到管理员权限，安装已取消。\n\n" + ex.Message +
                                "\n\n也可以直接改用默认目录，或用 D 盘里一个普通文件夹。",
                                "安装", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, "这个目录里没有 UserSkins，确认是战雷安装目录吗？", "提示",
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
            : base("已安装相同版本", 580, 292)
        {
            MakeLabel("已安装相同版本 " + (version == null ? "" : version.Display),
                      24, 14, 520, 26, Ui.H1, Ui.Accent);
            MakeLabel("安装目录里已经是这个版本。可以重新安装并覆盖当前程序文件；" +
                      "settings.ini、shortnames.ini、备份、日志与缓存都会原样保留。",
                      24, 48, 520, 44, Ui.Small, Ui.TextDim);
            MakeLabel("如果只是想继续使用，直接取消即可。", 24, 100, 520, 20, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton("取消", 338, 160, 96, false);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton reinstall = MakeButton("重新安装", 444, 160, 110, true);
            reinstall.Click += delegate { Choice = 1; DialogResult = DialogResult.OK; Close(); };
        }
    }

    /// <summary>降级：默认取消，只有明确点「继续降级」才执行。</summary>
    internal class DowngradeForm : ModalForm
    {
        public int Choice; // 1=继续降级 0=取消

        public DowngradeForm(SemVer oldVersion, SemVer newVersion)
            : base("这是降级安装", 600, 330)
        {
            string oldText = oldVersion == null ? "未知版本" : oldVersion.Display;
            string newText = newVersion == null ? "当前安装包" : newVersion.Display;
            MakeLabel("已安装更高版本 " + oldText, 24, 14, 540, 26, Ui.H1, Ui.Warn);
            MakeLabel("当前安装包是 " + newText + "，继续会把程序降级到旧版本。" +
                      "新版本里的修复与功能在这之后不会生效。",
                      24, 48, 540, 44, Ui.Small, Ui.TextDim);
            MakeLabel("推荐点「取消」，保留现在安装的版本。", 24, 96, 540, 20, Ui.Small, Ui.Warn);

            FlatButton proceed = MakeButton("继续降级", 404, 190, 110, false);
            proceed.Danger = true;
            proceed.Click += delegate { Choice = 1; DialogResult = DialogResult.OK; Close(); };
            FlatButton cancel = MakeButton("取消", 522, 190, 96, true);
            cancel.Click += delegate { Choice = 0; DialogResult = DialogResult.Cancel; Close(); };
        }
    }

    /// <summary>目标程序被占用：提示先关闭，提供「重试」。</summary>
    internal class BusyRetryForm : ModalForm
    {
        public bool Retry;

        public BusyRetryForm(string message)
            : base("程序正在运行", 620, 360)
        {
            MakeLabel("无法覆盖正在运行的程序", 24, 14, 540, 26, Ui.H1, Ui.Warn);
            Label detail = MakeLabel(string.IsNullOrEmpty(message) ? "程序正在运行，文件被占用。" : message,
                                     24, 48, 560, 70, Ui.Small, Ui.TextDim);
            detail.AutoEllipsis = true;
            MakeLabel("请先关闭战雷涂装管理器（包括托盘里的窗口），然后点「重试」。",
                      24, 124, 560, 40, Ui.Small, Ui.TextDim);
            MakeLabel("本次更新尚未写入任何文件，不会留下半覆盖。", 24, 168, 560, 20, Ui.Small, Ui.Good);

            FlatButton cancel = MakeButton("取消", 404, 220, 96, false);
            cancel.Click += delegate { Retry = false; DialogResult = DialogResult.Cancel; Close(); };
            FlatButton retry = MakeButton("重试", 510, 220, 96, true);
            retry.Click += delegate { Retry = true; DialogResult = DialogResult.OK; Close(); };
        }
    }
}
