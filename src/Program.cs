using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WtSkin
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 自更新替换助手：最先处理，不加载任何界面（除失败提示框外）。
            //   WTSkinManager.exe --update-helper <pid> <新exe> <安装目录> <旧版本>
            if (args != null && args.Length >= 5 && args[0] == UpdateInstaller.HelperFlag)
            {
                UpdateInstaller.RunHelper(args[1], args[2], args[3], args[4]);
                return;
            }
            if (args != null && args.Length >= 1 && args[0] == "--diagnose")
            {
                RunDiagnose(args.Length >= 2 ? args[1] : "");
                return;
            }
            if (args != null && args.Length >= 3 && args[0] == "--install")
            {
                RunElevatedInstall(args[1], args[2]);
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "WTSkinManager.SingleInstance", out created))
            {
                InstanceGuard.Attach(mutex);
                if (!created)
                {
                    MessageBox.Show("战雷涂装管理器已经在运行了。", "战雷涂装管理器",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    ShowCrash(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    ShowCrash(e.ExceptionObject as Exception);
                };

                Config.InstallDir = Config.DetectInstallDir();
                Config.Load();
                Ui.SetTheme(Config.ThemeIndex);
                // 全部界面动画关闭：淡入/滑入/分页过渡/浮层渐变都不再执行
                Ui.Animations = false;
                Ui.Init(Native.GetDpiScale() * (Config.ZoomPercent / 100f), Config.ZoomPercent / 100f);

                // 上次自更新失败回滚过：启动时提示一次（读完即删）。
                UpdateInstaller.ShowRollbackNotice();

                bool installed = File.Exists(Config.InstalledFlag);
                if (!installed)
                {
                    // 安装向导：按顺序检测已安装实例（注册表 → 默认目录 → settings.ini → 标记文件），
                    // 只做常数级路径检查，不扫描磁盘。
                    Stopwatch detectWatch = Stopwatch.StartNew();
                    InstallScan scan = InstallProbe.Scan();
                    detectWatch.Stop();
                    InstallLog.Begin(scan.Best != null ? scan.Best.Dir : Config.InstallDir);
                    InstallLog.Write("启动安装向导：程序 v" + AppInfo.VersionText + "；数据 " + AppInfo.DataVersionText);
                    for (int i = 0; i < scan.Steps.Count; i++)
                    {
                        InstallLog.Write("检测：" + scan.Steps[i]);
                    }
                    InstallLog.Write("检测耗时：" + detectWatch.ElapsedMilliseconds + " ms");
                    using (SetupForm setup = new SetupForm(GameLocator.FindBest(), scan))
                    {
                        if (setup.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                    }
                    Config.Load();
                    Ui.SetTheme(Config.ThemeIndex);
                }

                bool nowInstalled = File.Exists(Config.InstalledFlag);
                if (nowInstalled)
                {
                    if (!Config.GameDirLooksValid(Config.GameDir))
                    {
                        using (SettingsForm settings = new SettingsForm())
                        {
                            if (settings.ShowDialog() != DialogResult.OK)
                            {
                                return;
                            }
                        }
                        if (!Config.GameDirLooksValid(Config.GameDir))
                        {
                            return;
                        }
                    }
                    // 首次安装 / 覆盖更新完成后弹一次；普通启动不弹（待展示文件不存在）。
                    UpdateNotesForm.ShowPendingIfAny();
                    DataFiles.CheckAndReport();
                }

                if (UpdateInstaller.ExitRequested)
                {
                    // 设置页点了「立即更新」：替换助手已经接手，这里直接退出进程。
                    return;
                }

                NameMap.EnsureFile();
                if (args != null && Array.IndexOf(args, "--manager") >= 0)
                {
                    using (ManagerForm manager = new ManagerForm())
                    {
                        manager.ShowDialog();
                    }
                    return;
                }

                Application.Run(new MainForm(args));
            }
        }

        private static void RunElevatedInstall(string installDir, string gameDir)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string exe = Path.Combine(Path.GetFullPath(installDir).TrimEnd('\\'), "WTSkinManager.exe");
            try
            {
                InstallLog.Begin(installDir);
                InstallLog.Write("管理员安装：目标 " + installDir + "，游戏目录 " + gameDir);
                InstallScan probe = new InstallScan();
                InstallCandidate existing = InstallProbe.ProbeDir(installDir, "管理员安装目标", null, probe);
                InstallResult result = Installer.Install(installDir, gameDir, existing);
                InstallLog.Write("管理员安装完成：v" + result.NewVersion.ToDisplay() +
                                 "（数据 " + result.DataVersion + "）");
            }
            catch (Exception ex)
            {
                InstallLog.Write("管理员安装失败：" + ex.Message);
                MessageBox.Show("用管理员身份也没能写入：\n\n" + ex.Message + "\n\n" + Installer.EnvironmentHint(),
                                "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装完成，但没能自动启动：\n\n" + ex.Message + "\n\n请手动打开 " + exe,
                                "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 诊断模式：只读检查安装检测、版本来源、数据版本、更新日志与覆盖表，写报告后退出。
        /// 不安装、不修改任何文件（日志除外），用于验收与排查。
        /// </summary>
        private static void RunDiagnose(string outPath)
        {
            Config.InstallDir = Config.DetectInstallDir();
            Config.Load();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("战雷涂装管理器 诊断报告");
            sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("程序版本：v" + AppInfo.VersionText);
            sb.AppendLine("数据版本：" + AppInfo.DataVersionText + "（schema " + DataFiles.SchemaVersion + "）");
            sb.AppendLine("程序文件：" + Config.CurrentExe);
            sb.AppendLine("配置目录：" + Config.InstallDir);
            sb.AppendLine("settings.ini：" + Config.SettingsPath + "（" + (File.Exists(Config.SettingsPath) ? "存在" : "不存在") + "）");
            sb.AppendLine("已安装标记：" + (File.Exists(Config.InstalledFlag) ? "存在" : "不存在"));
            sb.AppendLine("游戏目录：" + Config.GameDir);
            sb.AppendLine();

            sb.AppendLine("== 安装检测 ==");
            Stopwatch watch = Stopwatch.StartNew();
            InstallScan scan = InstallProbe.Scan();
            watch.Stop();
            sb.AppendLine("检测耗时：" + watch.ElapsedMilliseconds + " ms（要求 ≤1000 ms）");
            for (int i = 0; i < scan.Steps.Count; i++)
            {
                sb.AppendLine("  " + scan.Steps[i]);
            }
            for (int i = 0; i < scan.Candidates.Count; i++)
            {
                InstallCandidate candidate = scan.Candidates[i];
                sb.AppendLine("  候选 " + (i + 1) + "：" + candidate.Dir);
                sb.AppendLine("    版本：" + candidate.VersionLabel + "；来源：" + candidate.Source +
                              "；版本来源：" + candidate.VersionSource);
                for (int a = 0; a < candidate.VersionAttempts.Count; a++)
                {
                    sb.AppendLine("    兜底：" + candidate.VersionAttempts[a]);
                }
            }
            for (int i = 0; i < scan.Residues.Count; i++)
            {
                sb.AppendLine("  残留：" + scan.Residues[i].Dir + "（" + scan.Residues[i].Reason +
                              "，来源 " + scan.Residues[i].Source + "）");
            }
            sb.AppendLine("  最佳候选：" + (scan.Best == null ? "无" : scan.Best.Dir + "  " + scan.Best.VersionLabel));
            sb.AppendLine();

            sb.AppendLine("== 快速检测（找到即停）==");
            Stopwatch fast = Stopwatch.StartNew();
            InstallScan first = InstallProbe.ScanFirst();
            fast.Stop();
            sb.AppendLine("耗时：" + fast.ElapsedMilliseconds + " ms；结果：" +
                          (first.Best == null ? "未发现已安装" : first.Best.Dir + "  " + first.Best.VersionLabel));
            sb.AppendLine();

            sb.AppendLine("== 数据版本 ==");
            DataCheck check = DataFiles.Check();
            for (int i = 0; i < check.Lines.Count; i++)
            {
                sb.AppendLine("  " + check.Lines[i]);
            }
            List<DataFileEntry> files = DataFiles.Files;
            for (int i = 0; i < files.Count; i++)
            {
                sb.AppendLine("  " + files[i].File + "：schema " + files[i].SchemaVersion +
                              "，data " + files[i].DataVersion + "，条目 " + files[i].Entries);
            }
            sb.AppendLine("  schema 一致：" + (!check.SchemaMismatch));
            sb.AppendLine("  数据落后：" + check.DataOlder);
            sb.AppendLine();

            sb.AppendLine("== 更新日志 ==");
            List<ReleaseNote> notes = ReleaseLog.All;
            sb.AppendLine("版本段数：" + notes.Count + (ReleaseLog.Error.Length > 0 ? "；错误：" + ReleaseLog.Error : ""));
            for (int i = 0; i < notes.Count && i < 5; i++)
            {
                sb.AppendLine("  v" + notes[i].VersionText + "  " + notes[i].Date +
                              "  条目 " + notes[i].Count + "（数据 " + notes[i].DataVersion + "）");
            }
            bool truncated;
            SemVer from = notes.Count > 1 ? notes[1].Version : null;
            SemVer to = notes.Count > 0 ? notes[0].Version : AppInfo.AppVersion;
            List<ReleaseNote> range = ReleaseLog.Range(from, to, out truncated);
            sb.AppendLine("区间 " + (from == null ? "(无)" : from.Display) + " → " + to.Display +
                          "：段数 " + range.Count + "，更早未收录=" + truncated);
            sb.AppendLine();

            sb.AppendLine("== 用户覆盖表 ==");
            sb.AppendLine("  路径：" + UserOverrides.FilePath);
            sb.AppendLine("  " + UserOverrides.Summary());
            List<string> effective = UserOverrides.EffectiveLines();
            for (int i = 0; i < effective.Count; i++)
            {
                sb.AppendLine("  " + effective[i]);
            }
            sb.AppendLine();

            sb.AppendLine("== 安装日志 ==");
            sb.AppendLine("  日志路径：" + InstallLog.Path);
            sb.AppendLine("  最近失败/回滚：");
            sb.AppendLine("  （见安装日志文件）");

            string path = outPath;
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Path.GetTempPath(), "WTSkinManager-diagnose-" +
                                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            }
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
            try
            {
                Console.WriteLine(sb.ToString());
            }
            catch
            {
            }
        }

        private static void ShowCrash(Exception ex)
        {
            try
            {
                Config.Log("崩溃: " + (ex == null ? "未知错误" : ex.ToString()));
            }
            catch
            {
            }
            string text = ex == null ? "发生未知错误。" : ex.Message;
            MessageBox.Show("程序遇到一个问题：\n\n" + text + "\n\n详细信息已写入日志。",
                            "战雷涂装管理器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
