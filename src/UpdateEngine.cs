using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WtSkin
{
    /// <summary>目标程序正在运行，无法覆盖；界面据此提供「重试」。</summary>
    internal class InstallBusyException : Exception
    {
        public readonly string TargetExe;

        public InstallBusyException(string targetExe, string message)
            : base(message)
        {
            TargetExe = targetExe;
        }
    }

    /// <summary>覆盖失败后的异常，带上回滚结果，界面必须如实显示。</summary>
    internal class InstallException : Exception
    {
        public readonly bool RolledBack;
        public readonly string RollbackDetail;

        public InstallException(string message, bool rolledBack, string rollbackDetail, Exception inner)
            : base(message, inner)
        {
            RolledBack = rolledBack;
            RollbackDetail = rollbackDetail ?? "";
        }
    }

    internal class InstallResult
    {
        public bool Fresh;
        public string InstallDir = "";
        public string TargetExe = "";
        public SemVer OldVersion;
        public string OldVersionSource = "";
        public SemVer NewVersion;
        public string DataVersion = "";
        public string BackupDir = "";
        public string LogPath = "";
        public bool SettingsPreserved;
        public long ExeBytes;
        public string ExeSha256 = "";
        public long ElapsedMs;
        public bool AssetsReplaced;

        public string OldVersionText
        {
            get { return OldVersion == null ? "读取失败" : OldVersion.Display; }
        }
    }

    /// <summary>
    /// 覆盖更新：原地替换主程序与内置数据；更新前把 settings.ini 与用户数据复制到临时目录；
    /// 失败整体回滚并还原设置。检测、旧版本来源、判定、前后版本、数据版本、失败与回滚结果全部写日志。
    /// </summary>
    internal static class UpdateEngine
    {
        /// <summary>临时备份根目录覆盖（仅自测/便携场景使用；正式安装为 %TEMP%\WTSkinManager）。</summary>
        internal static string BackupRootOverride = "";

        private static readonly string[] PreservedFiles = new string[]
        {
            "settings.ini",
            "shortnames.ini",
            "user-overrides.json",
            "history.ini",
            "notes-seen.ini",
            "shortnames_unmatched.txt",
            "unmatched.txt"
        };

        public static InstallResult Execute(string installDir, string gameDir, InstallCandidate existing)
        {
            return Execute(installDir, gameDir, existing, Config.CurrentExe);
        }

        public static InstallResult Execute(string installDir, string gameDir, InstallCandidate existing, string sourceExe)
        {
            Stopwatch watch = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(installDir))
            {
                throw new ArgumentException("安装目录为空");
            }
            string full = Path.GetFullPath(installDir).TrimEnd('\\');
            bool dirExisted = Directory.Exists(full);
            Config.InstallDir = full;
            InstallLog.Begin(full);
            InstallResult result = new InstallResult();
            result.InstallDir = full;
            result.NewVersion = AppInfo.AppVersion;
            result.DataVersion = AppInfo.DataVersion;
            result.LogPath = InstallLog.Path;

            InstallLog.Write("目标目录：" + full);
            InstallLog.Write("程序版本：v" + AppInfo.VersionText + "；数据版本：" + AppInfo.DataVersionText +
                             "（schema " + DataFiles.SchemaVersion + "）");

            string targetExe = Path.Combine(full, "WTSkinManager.exe");
            result.TargetExe = targetExe;
            bool exeExisted = File.Exists(targetExe);
            result.Fresh = !exeExisted;

            if (existing == null && exeExisted)
            {
                InstallScan probe = new InstallScan();
                existing = InstallProbe.ProbeDir(full, "目标目录", null, probe);
            }
            if (existing != null)
            {
                result.OldVersion = existing.Version;
                result.OldVersionSource = existing.VersionSource;
                InstallLog.Write("检测路径与来源：" + existing.Source + " → " + existing.Dir);
                InstallLog.Write("旧版本：" + (existing.Version == null ? "读取失败" : existing.Version.Display) +
                                 "（来源：" + existing.VersionSource + "）");
                for (int i = 0; i < existing.VersionAttempts.Count; i++)
                {
                    InstallLog.Write("  版本兜底尝试：" + existing.VersionAttempts[i]);
                }
            }
            if (result.OldVersion == null && exeExisted)
            {
                InstallLog.Write("旧版本：读取失败（exe 版本资源 / 安装目录版本标记 / 注册表 DisplayVersion / settings.ini 版本字段 均未读到）");
                InstallLog.Write("旧版本来源：读取失败");
            }

            if (!exeExisted)
            {
                InstallLog.Write("判定：" + (dirExisted ? "全新安装（目录存在但不是已安装实例）" : "全新安装（目录不存在）"));
            }
            else
            {
                InstallLog.Write("判定：覆盖更新（原地替换，不新建、不改名旧目录）");
            }
            InstallLog.Write("更新前版本：" + (result.OldVersion == null ? "读取失败" : result.OldVersion.Display) +
                             " → 更新后版本：v" + result.NewVersion.ToDisplay());
            InstallLog.Write("数据版本：" + AppInfo.DataVersionText + "（schema " + DataFiles.SchemaVersion + "）");

            // ---- 预检：目录可写、目标未被占用、不是自己 ----
            try
            {
                if (!Directory.Exists(full))
                {
                    Directory.CreateDirectory(full);
                }
                string probeFile = Path.Combine(full, ".write_test");
                File.WriteAllText(probeFile, "ok");
                File.Delete(probeFile);
                InstallLog.Write("写入检查：目标目录可写");
            }
            catch (Exception ex)
            {
                InstallLog.Write("失败：目标目录不可写：" + ex.Message);
                throw new UnauthorizedAccessException("目标目录不可写：" + ex.Message, ex);
            }

            if (exeExisted)
            {
                InstallLog.Write("占用检查：检查目标程序是否正在运行");
                EnsureNotBusy(targetExe);
                InstallLog.Write("占用检查：目标程序未被占用");
            }

            string backupBase = "";
            string backupDir = "";
            bool replacing = false;
            result.BackupDir = backupDir;
            Dictionary<string, string> preservedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string oldExeBackup = "";
            string oldAssetsDir = "";
            string incomingAssetsDir = "";
            string rollbackDetail = "";
            bool rolledBack = false;

            try
            {
                backupBase = ResolveBackupBase(full);
                backupDir = Path.Combine(backupBase,
                    "update-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 6));
                result.BackupDir = backupDir;
                Directory.CreateDirectory(backupDir);
                InstallLog.Write("临时备份目录：" + backupDir);

                // ---- 备份 settings.ini 与用户数据 ----
                for (int i = 0; i < PreservedFiles.Length; i++)
                {
                    string name = PreservedFiles[i];
                    string source = Path.Combine(full, name);
                    if (!File.Exists(source))
                    {
                        continue;
                    }
                    string copy = Path.Combine(backupDir, name);
                    File.Copy(source, copy, true);
                    preservedHashes[name] = Hash(source);
                    InstallLog.Write("备份：" + name + "（" + new FileInfo(source).Length + " 字节）");
                }
                result.SettingsPreserved = preservedHashes.ContainsKey("settings.ini");
                WriteUserDataManifest(full, backupDir);
                CopySmallUserDir(full, backupDir, "cache");

                // ---- 备份旧 exe ----
                if (exeExisted)
                {
                    oldExeBackup = Path.Combine(backupDir, "WTSkinManager.exe.old");
                    File.Copy(targetExe, oldExeBackup, true);
                    InstallLog.Write("备份：WTSkinManager.exe（旧版本 " +
                                     (result.OldVersion == null ? "读取失败" : result.OldVersion.Display) + "）");
                }

                // ---- 替换 exe：先写临时文件，再原子替换 ----
                string stage = Path.Combine(full, "WTSkinManager.exe.new");
                if (File.Exists(stage))
                {
                    File.Delete(stage);
                }
                replacing = true;
                File.Copy(sourceExe, stage, true);
                if (exeExisted)
                {
                    try
                    {
                        File.Replace(stage, targetExe, null);
                    }
                    catch (Exception replaceError)
                    {
                        InstallLog.Write("原子替换不可用，改用复制覆盖：" + replaceError.Message);
                        File.Copy(stage, targetExe, true);
                        File.Delete(stage);
                    }
                }
                else
                {
                    File.Move(stage, targetExe);
                }
                result.ExeBytes = new FileInfo(targetExe).Length;
                result.ExeSha256 = Hash(targetExe);
                InstallLog.Write("替换：WTSkinManager.exe → v" + result.NewVersion.ToDisplay() +
                                 "（" + result.ExeBytes + " 字节，SHA256 " + result.ExeSha256 + "）");

                // ---- 替换随包外置数据（单文件分发时不存在，仍按同一流程处理）----
                string sourceAssets = Path.Combine(Config.ExeDir, "assets", "vehicles");
                string targetAssets = Path.Combine(full, "assets", "vehicles");
                if (Directory.Exists(sourceAssets) &&
                    !string.Equals(Path.GetFullPath(sourceAssets), Path.GetFullPath(targetAssets),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    string assetsRoot = Path.Combine(full, "assets");
                    Directory.CreateDirectory(assetsRoot);
                    incomingAssetsDir = Path.Combine(assetsRoot, "vehicles.new");
                    if (Directory.Exists(incomingAssetsDir))
                    {
                        Directory.Delete(incomingAssetsDir, true);
                    }
                    Directory.CreateDirectory(incomingAssetsDir);
                    string[] files = Directory.GetFiles(sourceAssets);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                        if (ext != ".png" && ext != ".json")
                        {
                            continue;
                        }
                        File.Copy(files[i], Path.Combine(incomingAssetsDir, Path.GetFileName(files[i])), true);
                    }
                    if (Directory.Exists(targetAssets))
                    {
                        oldAssetsDir = Path.Combine(assetsRoot, "vehicles.old");
                        if (Directory.Exists(oldAssetsDir))
                        {
                            Directory.Delete(oldAssetsDir, true);
                        }
                        Directory.Move(targetAssets, oldAssetsDir);
                    }
                    Directory.Move(incomingAssetsDir, targetAssets);
                    incomingAssetsDir = "";
                    result.AssetsReplaced = true;
                    InstallLog.Write("替换：外置图标与清单 assets\\vehicles");
                }
                else
                {
                    InstallLog.Write("替换：图标、清单、简称、国家、等级权重全部内嵌在 exe 中，随主程序一并替换");
                }

                // ---- 保留配置：更新时不改 settings.ini ----
                bool settingsExisted = preservedHashes.ContainsKey("settings.ini");
                if (!exeExisted || !settingsExisted)
                {
                    Config.InstallDir = full;
                    Config.GameDir = string.IsNullOrEmpty(gameDir) ? Config.GameDir : Path.GetFullPath(gameDir).TrimEnd('\\');
                    Config.FirstRunDone = true;
                    Config.EnsureDirs();
                    Config.Save();
                    NameMap.EnsureFile();
                    InstallLog.Write("写入：settings.ini（全新安装）");
                }
                else
                {
                    InstallLog.Write("保留：settings.ini 原样保留（更新不写配置）");
                }

                // ---- 标记、待展示更新信息、注册表、快捷方式 ----
                string oldText = result.OldVersion == null ? "" : result.OldVersion.ToDisplay();
                InstallMarker.Write(full, result.NewVersion.ToDisplay(), AppInfo.DataVersion, oldText);
                InstallLog.Write("写入：安装标记 " + InstallMarker.LocalPath(full));
                InstallRegistry.Write(full, result.NewVersion.ToDisplay());
                InstallLog.Write("写入：注册表卸载项 HKCU\\" + InstallRegistry.UninstallKey + "\\" + InstallRegistry.AppKeyName);
                if (!File.Exists(Config.InstalledFlag))
                {
                    File.WriteAllText(Config.InstalledFlag, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), new UTF8Encoding(false));
                }
                PendingNotes.Write(oldText, result.NewVersion.ToDisplay(), !exeExisted);
                InstallLog.Write("写入：待展示更新信息（首次安装=" + (!exeExisted) + "）");
                if (!exeExisted && !Installer.SuppressShortcuts)
                {
                    Installer.CreateShortcuts(targetExe, full);
                    InstallLog.Write("写入：桌面与开始菜单快捷方式");
                }

                // ---- 校验：用户配置与用户数据逐字节不变 ----
                bool settingsOk = true;
                foreach (KeyValuePair<string, string> pair in preservedHashes)
                {
                    string path = Path.Combine(full, pair.Key);
                    if (!File.Exists(path) || !string.Equals(Hash(path), pair.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        settingsOk = false;
                        InstallLog.Write("校验失败：" + pair.Key + " 在更新过程中发生了变化");
                        string backup = Path.Combine(backupDir, pair.Key);
                        if (File.Exists(backup))
                        {
                            File.Copy(backup, path, true);
                            InstallLog.Write("已从临时备份还原：" + pair.Key);
                        }
                    }
                }
                result.SettingsPreserved = settingsOk;
                InstallLog.Write(settingsOk
                    ? "校验：settings.ini 与用户配置文件逐字节未变"
                    : "校验：用户配置文件发生变化，已从备份还原");
                InstallLog.Write("校验：backups、logs、cache 未在本流程中写入（见 " + Path.Combine(backupDir, "user-data.txt") + "）");

                // ---- 清理旧数据（成功后才删）----
                if (oldAssetsDir.Length > 0 && Directory.Exists(oldAssetsDir))
                {
                    Directory.Delete(oldAssetsDir, true);
                }
                InstallLog.Write("清理：临时替换文件");
                try
                {
                    Directory.Delete(backupDir, true);
                    InstallLog.Write("清理：临时备份目录（更新成功后删除）");
                }
                catch (Exception cleanError)
                {
                    InstallLog.Write("清理临时备份目录失败（可手动删除）：" + cleanError.Message);
                }

                watch.Stop();
                result.ElapsedMs = watch.ElapsedMilliseconds;
                InstallLog.Write("完成：v" + (result.OldVersion == null ? "未知" : result.OldVersion.ToDisplay()) +
                                 " → v" + result.NewVersion.ToDisplay() +
                                 "；数据版本 " + AppInfo.DataVersionText + "；耗时 " + result.ElapsedMs + " ms");
                return result;
            }
            catch (Exception ex)
            {
                InstallLog.Write("失败：" + ex.GetType().Name + "：" + ex.Message);
                int rollback = Rollback(full, targetExe, exeExisted, oldExeBackup, backupDir,
                                        preservedHashes, oldAssetsDir, incomingAssetsDir, replacing,
                                        ref rollbackDetail);
                rolledBack = rollback > 0;
                InstallLog.Write("回滚结果：" + rollbackDetail);
                InstallBusyException busy = ex as InstallBusyException;
                if (busy != null)
                {
                    throw;
                }
                throw new InstallException("更新失败：" + ex.Message + "\n回滚：" + rollbackDetail,
                                           rolledBack, rollbackDetail, ex);
            }
        }

        private static void EnsureNotBusy(string targetExe)
        {
            if (string.Equals(Path.GetFullPath(targetExe), Path.GetFullPath(Config.CurrentExe),
                              StringComparison.OrdinalIgnoreCase))
            {
                InstallLog.Write("占用检查：目标程序正是当前运行的程序，无法覆盖自身");
                throw new InstallBusyException(targetExe,
                    "当前正在从这个目录运行程序，无法覆盖自己。\n请关闭程序后，从新的安装包重新运行。");
            }
            try
            {
                using (new FileStream(targetExe, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                InstallLog.Write("占用检查：没有写入权限：" + ex.Message);
                throw new UnauthorizedAccessException(
                    "没有写入权限，无法替换 " + targetExe + "。\n请用管理员身份运行，或改用其他目录。", ex);
            }
            catch (IOException ex)
            {
                if (ProcessRunning(targetExe))
                {
                    InstallLog.Write("失败：程序正在运行，尚未开始替换，未产生半覆盖");
                    throw new InstallBusyException(targetExe,
                        "程序正在运行，文件被占用。\n请先关闭战雷涂装管理器，然后点「重试」。");
                }
                throw new IOException("无法写入 " + targetExe + "：" + ex.Message, ex);
            }
        }

        private static bool ProcessRunning(string exe)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("WTSkinManager");
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        string file = processes[i].MainModule.FileName;
                        if (!string.IsNullOrEmpty(file) &&
                            string.Equals(Path.GetFullPath(file), Path.GetFullPath(exe),
                                          StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        processes[i].Dispose();
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static string ResolveBackupBase(string installDir)
        {
            if (!string.IsNullOrEmpty(BackupRootOverride))
            {
                Directory.CreateDirectory(BackupRootOverride);
                return BackupRootOverride;
            }
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(Path.GetTempPath(), "WTSkinManager"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "WTSkinManager", "temp"));
            candidates.Add(Path.Combine(installDir, ".update-backup"));
            for (int i = 0; i < candidates.Count; i++)
            {
                try
                {
                    Directory.CreateDirectory(candidates[i]);
                    string probe = Path.Combine(candidates[i], ".write_test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    InstallLog.Write("备份根目录：" + candidates[i]);
                    return candidates[i];
                }
                catch (Exception ex)
                {
                    InstallLog.Write("备份根目录不可用：" + candidates[i] + "（" + ex.Message + "）");
                }
            }
            throw new IOException("找不到可写的临时备份目录（已尝试 TEMP、LOCALAPPDATA 与安装目录）");
        }

        private static int Rollback(string full, string targetExe, bool exeExisted, string oldExeBackup,
                                    string backupDir, Dictionary<string, string> preservedHashes,
                                    string oldAssetsDir, string incomingAssetsDir, bool replacing,
                                    ref string detail)
        {
            int restored = 0;
            StringBuilder sb = new StringBuilder();
            if (!replacing)
            {
                try
                {
                    string pending0 = PendingNotes.Path;
                    if (pending0.Length > 0 && File.Exists(pending0))
                    {
                        File.Delete(pending0);
                    }
                }
                catch
                {
                }
                detail = "无需回滚：失败发生在写入前，未产生任何修改";
                return 0;
            }
            try
            {
                string stage = Path.Combine(full, "WTSkinManager.exe.new");
                if (File.Exists(stage))
                {
                    File.Delete(stage);
                }
                if (exeExisted)
                {
                    if (File.Exists(oldExeBackup))
                    {
                        File.Copy(oldExeBackup, targetExe, true);
                        restored++;
                        sb.Append("已还原旧版主程序；");
                    }
                    else
                    {
                        sb.Append("旧版主程序备份缺失；");
                    }
                }
                else if (File.Exists(targetExe))
                {
                    File.Delete(targetExe);
                    restored++;
                    sb.Append("已移除未完成安装的主程序；");
                }
                foreach (string name in PreservedFiles)
                {
                    string backup = Path.Combine(backupDir, name);
                    if (!File.Exists(backup))
                    {
                        continue;
                    }
                    string path = Path.Combine(full, name);
                    string currentHash = File.Exists(path) ? Hash(path) : "";
                    string backupHash;
                    if (preservedHashes.TryGetValue(name, out backupHash) &&
                        !string.Equals(currentHash, backupHash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(backup, path, true);
                        restored++;
                        sb.Append("已还原 ").Append(name).Append("；");
                    }
                }
                if (oldAssetsDir.Length > 0 && Directory.Exists(oldAssetsDir))
                {
                    string targetAssets = Path.Combine(full, "assets", "vehicles");
                    if (Directory.Exists(targetAssets))
                    {
                        Directory.Delete(targetAssets, true);
                    }
                    Directory.Move(oldAssetsDir, targetAssets);
                    restored++;
                    sb.Append("已还原外置图标目录；");
                }
                if (incomingAssetsDir.Length > 0 && Directory.Exists(incomingAssetsDir))
                {
                    Directory.Delete(incomingAssetsDir, true);
                }
                string pending = PendingNotes.Path;
                if (pending.Length > 0 && File.Exists(pending))
                {
                    File.Delete(pending);
                }
                if (sb.Length == 0)
                {
                    sb.Append("没有需要还原的内容（失败发生在写入前）");
                }
            }
            catch (Exception ex)
            {
                sb.Append("回滚失败：").Append(ex.Message);
                detail = sb.ToString();
                return restored;
            }
            detail = (restored > 0 ? "成功：" : "无需回滚：") + sb.ToString();
            return restored;
        }

        private static void CopySmallUserDir(string full, string backupDir, string name)
        {
            try
            {
                string source = Path.Combine(full, name);
                if (!Directory.Exists(source))
                {
                    return;
                }
                long bytes = DirBytes(source);
                if (bytes > 64L * 1024 * 1024)
                {
                    InstallLog.Write("备份：" + name + "\\ 共 " + bytes + " 字节，超过 64MB，未复制（更新不写入该目录）");
                    return;
                }
                string target = Path.Combine(backupDir, name);
                Directory.CreateDirectory(target);
                string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string relative = files[i].Substring(source.Length).TrimStart('\\');
                    string dest = Path.Combine(target, relative);
                    string dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(files[i], dest, true);
                }
                InstallLog.Write("备份：" + name + "\\（" + files.Length + " 个文件，" + bytes + " 字节）");
            }
            catch (Exception ex)
            {
                InstallLog.Write("备份 " + name + "\\ 失败：" + ex.Message);
            }
        }

        private static void WriteUserDataManifest(string full, string backupDir)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 用户数据目录清单（更新前）：目录|文件数|字节数。更新不写入这些目录。");
                string[] names = new string[] { "backups", "logs", "cache" };
                for (int i = 0; i < names.Length; i++)
                {
                    string dir = Path.Combine(full, names[i]);
                    if (!Directory.Exists(dir))
                    {
                        sb.AppendLine(names[i] + "|0|0|不存在");
                        continue;
                    }
                    string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    long bytes = 0;
                    for (int f = 0; f < files.Length; f++)
                    {
                        try
                        {
                            bytes += new FileInfo(files[f]).Length;
                        }
                        catch
                        {
                        }
                    }
                    sb.AppendLine(names[i] + "|" + files.Length + "|" + bytes + "|存在");
                }
                File.WriteAllText(Path.Combine(backupDir, "user-data.txt"), sb.ToString(), new UTF8Encoding(false));
                InstallLog.Write("备份：用户数据清单 user-data.txt（backups / logs / cache）");
            }
            catch (Exception ex)
            {
                InstallLog.Write("写入用户数据清单失败：" + ex.Message);
            }
        }

        private static long DirBytes(string dir)
        {
            long total = 0;
            try
            {
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        total += new FileInfo(files[i]).Length;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return total;
        }

        public static string Hash(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
