using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 应用内自更新的替换阶段：主程序退出后由 %TEMP%\wtsm-updater.exe --update-helper 执行。
    /// 等待旧进程退出 → 复核新 exe → 备份旧 exe（保留 3 份）→ 原子替换（占用重试 10×200ms）
    /// → 校验新 exe → 启动新程序 → 写 update.log；失败回滚旧 exe 并留下「下次启动提示」。
    /// 助手不加载任何界面，只可能弹失败提示框。
    /// </summary>
    internal static class UpdateInstaller
    {
        public const string HelperFlag = "--update-helper";
        private const int WaitTimeoutMs = 60000;
        private const int ReplaceRetries = 10;
        private const int ReplaceRetryDelayMs = 200;
        private const int KeepBackups = 3;

        /// <summary>「立即更新」已经交给助手后置位，Program 据此直接退出消息循环。</summary>
        public static bool ExitRequested;

        public static string AppDataRoot
        {
            get { return UpdatePaths.Root; }
        }

        public static string UpdatesRoot
        {
            get { return UpdatePaths.Updates; }
        }

        public static string LogPath
        {
            get { return UpdatePaths.UpdateLog; }
        }

        public static string BackupRoot
        {
            get { return UpdatePaths.Backups; }
        }

        /// <summary>回滚提示文件：助手写，主程序下次启动读一次并删除。</summary>
        public static string RollbackPath
        {
            get { return UpdatePaths.RollbackNotice; }
        }

        // ---------------- 主程序侧 ----------------

        /// <summary>
        /// 把自身复制成 %TEMP%\wtsm-updater.exe 并带参数启动；成功后调用方应关闭整个程序。
        /// 失败时返回 false（界面提示，程序继续运行）。
        /// </summary>
        public static bool Begin(string newExe, string installDir, string oldVersion, out string error)
        {
            error = "";
            try
            {
                if (string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
                {
                    error = "更新包不存在或已被删除";
                    return false;
                }
                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                {
                    error = "安装目录不存在";
                    return false;
                }
                newExe = Path.GetFullPath(newExe);
                installDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar,
                                                                   Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(UpdatesRoot);
                string helper = StageHelper();
                int pid;
                using (Process current = Process.GetCurrentProcess())
                {
                    pid = current.Id;
                }
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = helper;
                info.Arguments = HelperFlag + " " +
                                 pid.ToString(CultureInfo.InvariantCulture) + " " +
                                 Quote(newExe) + " " + Quote(installDir) + " " + Quote(oldVersion);
                info.UseShellExecute = false;
                info.WorkingDirectory = Path.GetTempPath();
                Config.Log("自更新：启动替换助手 " + helper + " " + info.Arguments);
                Process.Start(info);
                ExitRequested = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Config.Log("自更新：启动替换助手失败 → " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>下次启动时提示「更新失败，已回滚到 vX」；读完即删，只提示一次。</summary>
        public static void ShowRollbackNotice()
        {
            try
            {
                if (!File.Exists(RollbackPath))
                {
                    return;
                }
                string version = Ini.Read(RollbackPath, "version");
                try
                {
                    File.Delete(RollbackPath);
                }
                catch
                {
                }
                if (string.IsNullOrEmpty(version))
                {
                    return;
                }
                MessageBox.Show("更新失败，已回滚到 v" + version + "。\n\n" +
                                "旧版本已恢复并继续运行，设置与涂装数据没有丢失。\n" +
                                "可以稍后再试，或到 GitHub Releases 手动下载最新版。",
                                "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
            }
        }

        private static string StageHelper()
        {
            string source = Application.ExecutablePath;
            string preferred = Path.Combine(Path.GetTempPath(), "wtsm-updater.exe");
            try
            {
                CopySelf(source, preferred);
                return preferred;
            }
            catch (Exception ex)
            {
                Log("固定助手名不可用（" + ex.Message + "），改用唯一文件名");
            }
            string unique = Path.Combine(Path.GetTempPath(),
                "wtsm-updater-" + DateTime.Now.ToString("HHmmssfff", CultureInfo.InvariantCulture) + ".exe");
            CopySelf(source, unique);
            return unique;
        }

        private static void CopySelf(string source, string target)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }
            catch
            {
            }
            File.Copy(source, target, true);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").TrimEnd('"') + "\"";
        }

        // ---------------- 助手侧 ----------------

        /// <summary>只由 Program.Main 在 --update-helper 模式下调用，不初始化任何界面。</summary>
        public static void RunHelper(string pidText, string newExe, string installDir, string oldVersion)
        {
            // 命令行可能带多余空白/引号（手动调用或被别的启动器拼过），这里统一规范化。
            newExe = (newExe ?? "").Trim().Trim('"');
            installDir = (installDir ?? "").Trim().Trim('"');
            oldVersion = (oldVersion ?? "").Trim().Trim('"');
            try
            {
                if (newExe.Length > 0)
                {
                    newExe = Path.GetFullPath(newExe);
                }
                if (installDir.Length > 0)
                {
                    installDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar,
                                                                       Path.AltDirectorySeparatorChar);
                }
            }
            catch
            {
            }

            int pid = 0;
            int.TryParse((pidText ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }
            catch
            {
            }

            Log("助手启动：pid=" + pid + " 旧版本=v" + oldVersion + " 新包=" + newExe + " 安装目录=" + installDir);

            string newVersion = Ini.Read(Path.Combine(Path.GetDirectoryName(newExe) ?? "", "ready.ini"), "version");
            string restore = "";
            try
            {
                if (!Directory.Exists(installDir))
                {
                    Fail("安装目录不存在：" + installDir, installDir, oldVersion, false);
                    return;
                }
                if (pid > 0 && !WaitForExit(pid, WaitTimeoutMs))
                {
                    Log("失败：等待旧进程退出超时（" + (WaitTimeoutMs / 1000) + " 秒）");
                    Log("结果：失败（旧程序仍在运行，未替换任何文件）");
                    MessageBox.Show("更新失败：旧程序还在运行，本次没有替换任何文件。\n\n" +
                                    "请关闭程序后重新点「立即更新」，或到 GitHub Releases 手动下载。",
                                    "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!File.Exists(newExe))
                {
                    Log("失败：新安装包不存在 " + newExe);
                    Fail("更新包不存在或已被删除。", installDir, oldVersion, false);
                    return;
                }
                if (!IsWritable(installDir))
                {
                    Log("失败：安装目录不可写 " + installDir);
                    MessageBox.Show("更新失败：安装目录不可写。\n\n" +
                                    "请用管理员身份运行程序，或把程序移动到可写目录（例如 D:\\Tools\\WTSkinManager）后手动替换 exe。\n\n" +
                                    "安装目录：" + installDir,
                                    "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Launch(ResolveInstallExe(installDir), installDir);
                    return;
                }

                string installExe = ResolveInstallExe(installDir);
                Log("目标程序： " + installExe);

                // 复核新 exe 的 SHA-256 与大小（下载阶段已校验，这里防中间被改动/复制不完整）。
                string expectedSha = Ini.Read(Path.Combine(Path.GetDirectoryName(newExe) ?? "", "ready.ini"), "sha256");
                long expectedSize = 0;
                string sizeText = Ini.Read(Path.Combine(Path.GetDirectoryName(newExe) ?? "", "ready.ini"), "size");
                long.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedSize);
                long actualSize = new FileInfo(newExe).Length;
                if (expectedSize > 0 && actualSize != expectedSize)
                {
                    Log("失败：新 exe 大小不符，期望 " + expectedSize + "，实际 " + actualSize);
                    Fail("更新包大小与记录不一致，已取消更新。", installDir, oldVersion, false);
                    return;
                }
                string actualSha = UpdateDownloader.HashFile(newExe);
                if (expectedSha.Length > 0 && !string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    Log("失败：新 exe 哈希不符，期望 " + expectedSha + "，实际 " + actualSha);
                    Fail("更新包校验失败，已取消更新。", installDir, oldVersion, false);
                    return;
                }
                if (expectedSha.Length == 0)
                {
                    Log("警告：缺少 ready.ini 里的 SHA-256，跳过哈希复核（大小 " + actualSize + "）");
                }
                else
                {
                    Log("复核通过：sha256=" + actualSha + "，大小=" + actualSize);
                }
                CheckSignature(newExe);

                // 旧 exe 备份到 %LOCALAPPDATA%\WTSkinManager\backups，保留最近 3 份。
                Directory.CreateDirectory(BackupRoot);
                string backup = Path.Combine(BackupRoot,
                    "WTSkinManager_" + SafeVersion(oldVersion) + "_" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".exe");
                if (File.Exists(installExe))
                {
                    File.Copy(installExe, backup, true);
                    Log("备份旧 exe：" + backup);
                    RotateBackups(KeepBackups);
                    restore = installExe + ".rollback";
                    File.Copy(installExe, restore, true);
                }

                string staged = installExe + ".new";
                File.Copy(newExe, staged, true);
                string stagedSha = UpdateDownloader.HashFile(staged);
                if (expectedSha.Length > 0 && !string.Equals(stagedSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(staged);
                    Log("失败：复制到安装目录后哈希不符");
                    Fail("复制到安装目录后校验失败，已取消更新。", installDir, oldVersion, restore.Length > 0);
                    return;
                }

                Exception last = null;
                bool replaced = false;
                for (int i = 0; i < ReplaceRetries; i++)
                {
                    try
                    {
                        if (File.Exists(installExe))
                        {
                            File.Replace(staged, installExe, null, true);
                        }
                        else
                        {
                            File.Move(staged, installExe);
                        }
                        replaced = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (i < ReplaceRetries - 1)
                        {
                            Thread.Sleep(ReplaceRetryDelayMs);
                        }
                    }
                }
                if (!replaced)
                {
                    TryDelete(staged);
                    Log("失败：替换 exe 失败（已重试 " + ReplaceRetries + " 次）→ " +
                        (last == null ? "未知原因" : last.Message));
                    Fail("替换程序文件失败（文件被占用）。", installDir, oldVersion, restore.Length > 0);
                    return;
                }

                string installedSha = UpdateDownloader.HashFile(installExe);
                if (expectedSha.Length > 0 && !string.Equals(installedSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    Log("失败：替换后哈希不符，期望 " + expectedSha + "，实际 " + installedSha);
                    Fail("替换后的程序校验失败。", installDir, oldVersion, restore.Length > 0);
                    return;
                }

                TryDelete(restore);
                TryDelete(newExe);

                Log("替换成功：新 exe sha256=" + installedSha);
                if (!Launch(installExe, installDir))
                {
                    Log("警告：新程序启动失败，请手动运行 " + installExe);
                    MessageBox.Show("更新完成，但没能自动启动新版本。\n\n请手动运行：" + installExe,
                                    "更新完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                Log("结果：成功（v" + oldVersion + " → v" + (newVersion.Length > 0 ? newVersion : "?") + "）");
            }
            catch (Exception ex)
            {
                Log("失败：未预料的异常 → " + ex.GetType().Name + ": " + ex.Message);
                Fail("更新过程中出现异常。", installDir, oldVersion, restore.Length > 0);
            }
        }

        /// <summary>
        /// 可选的 Authenticode 校验：只用 PublisherCert 的期望指纹做对比，失败只写日志、绝不阻断更新
        /// （未签名或证书链不受信任都属于正常情况）。
        /// </summary>
        private static void CheckSignature(string exePath)
        {
            try
            {
                X509Certificate cert = X509Certificate.CreateFromSignedFile(exePath);
                if (cert == null)
                {
                    Log("签名检查：未签名（不影响更新）");
                    return;
                }
                string thumb = (cert.GetCertHashString() ?? "").Replace(" ", "").ToUpperInvariant();
                if (string.Equals(thumb, PublisherCert.ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    Log("签名检查：通过（" + cert.Subject + "）");
                }
                else
                {
                    Log("签名检查：证书指纹不匹配，已忽略（" + thumb + "）");
                }
            }
            catch (Exception ex)
            {
                Log("签名检查：读取签名失败，已忽略 → " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>失败收尾：能从 .rollback 恢复就恢复，恢复成功才写「下次启动提示」。</summary>
        private static void Fail(string message, string installDir, string oldVersion, bool hasRestore)
        {
            string installExe = ResolveInstallExe(installDir);
            string restore = installExe + ".rollback";
            bool rolledBack = false;
            if (hasRestore && File.Exists(restore))
            {
                try
                {
                    if (File.Exists(installExe))
                    {
                        TryDelete(installExe);
                    }
                    File.Move(restore, installExe);
                    rolledBack = true;
                    Log("回滚：已用 .rollback 恢复旧 exe " + installExe);
                }
                catch (Exception ex)
                {
                    Log("回滚失败：恢复旧 exe 出错 → " + ex.Message);
                }
            }
            if (rolledBack)
            {
                WriteRollbackNotice(oldVersion);
            }
            else
            {
                Log("回滚：没有替换动作或恢复失败，旧 exe 保持不变");
            }
            TryDelete(installExe + ".new");
            Log("结果：失败（" + message + "）");
            if (!Launch(installExe, installDir))
            {
                MessageBox.Show(message + "\n\n自动启动旧版本失败，请手动运行：" + installExe,
                                "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void WriteRollbackNotice(string oldVersion)
        {
            try
            {
                Directory.CreateDirectory(UpdatesRoot);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 自更新失败回滚记录；程序下次启动提示一次后删除");
                sb.AppendLine("version=" + SafeVersion(oldVersion));
                sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                File.WriteAllText(RollbackPath, sb.ToString(), new UTF8Encoding(false));
                Log("已写入回滚提示：" + RollbackPath);
            }
            catch (Exception ex)
            {
                Log("写回滚提示失败 → " + ex.Message);
            }
        }

        // ---------------- 工具 ----------------

        private static bool WaitForExit(int pid, int timeoutMs)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    return process.WaitForExit(timeoutMs);
                }
            }
            catch (ArgumentException)
            {
                // 进程已经不在了。
                return true;
            }
            catch (Exception ex)
            {
                Log("等待旧进程退出时出错 → " + ex.Message);
                return true;
            }
        }

        private static string ResolveInstallExe(string installDir)
        {
            string preferred = Path.Combine(installDir, "WTSkinManager.exe");
            if (File.Exists(preferred))
            {
                return preferred;
            }
            try
            {
                string[] files = Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly);
                if (files.Length == 1)
                {
                    return files[0];
                }
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(files[i]);
                    if (name.StartsWith("WTSkinManager", StringComparison.OrdinalIgnoreCase))
                    {
                        return files[i];
                    }
                }
            }
            catch
            {
            }
            return preferred;
        }

        private static bool IsWritable(string dir)
        {
            string probe = Path.Combine(dir, ".wtsm-write-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1))
                {
                    stream.WriteByte(0);
                }
            }
            catch
            {
                return false;
            }
            TryDelete(probe);
            return true;
        }

        private static bool Launch(string exe, string workDir)
        {
            try
            {
                if (!File.Exists(exe))
                {
                    Log("启动失败：文件不存在 " + exe);
                    return false;
                }
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.UseShellExecute = true;
                info.WorkingDirectory = workDir;
                Process.Start(info);
                Log("已启动：" + exe);
                return true;
            }
            catch (Exception ex)
            {
                Log("启动失败：" + exe + " → " + ex.Message);
                return false;
            }
        }

        private static void RotateBackups(int keep)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(BackupRoot);
                if (!dir.Exists)
                {
                    return;
                }
                FileInfo[] files = dir.GetFiles("WTSkinManager_*.exe");
                Array.Sort(files, delegate(FileInfo a, FileInfo b)
                {
                    return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc);
                });
                for (int i = keep; i < files.Length; i++)
                {
                    try
                    {
                        files[i].Delete();
                        Log("清理旧备份：" + files[i].Name);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Log("清理旧备份失败 → " + ex.Message);
            }
        }

        private static string SafeVersion(string version)
        {
            string value = (version ?? "").Trim().TrimStart('v', 'V');
            if (value.Length == 0)
            {
                return "unknown";
            }
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                          c == '.' || c == '-' || c == '_';
                sb.Append(ok ? c : '_');
            }
            return sb.ToString();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(UpdatesRoot);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                              "  " + text + Environment.NewLine;
                File.AppendAllText(LogPath, line, new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
