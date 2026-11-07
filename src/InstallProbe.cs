using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WtSkin
{
    /// <summary>一个已安装实例（目录 + 版本 + 来源）。版本可能未知，但仍算已安装。</summary>
    internal class InstallCandidate
    {
        public string Dir = "";
        public SemVer Version;
        public string VersionSource = "";
        public string Source = "";
        public readonly List<string> VersionAttempts = new List<string>();

        public bool VersionUnknown
        {
            get { return Version == null; }
        }

        public string VersionLabel
        {
            get { return Version == null ? "未知版本" : Version.Display; }
        }

        public string DisplayText
        {
            get { return VersionLabel + "  ·  " + Dir; }
        }
    }

    /// <summary>目录在、主程序缺失的残留，不算已安装。</summary>
    internal class InstallResidue
    {
        public string Dir = "";
        public string Source = "";
        public string Reason = "";
    }

    internal class InstallScan
    {
        public readonly List<InstallCandidate> Candidates = new List<InstallCandidate>();
        public readonly List<InstallResidue> Residues = new List<InstallResidue>();
        public readonly List<string> Steps = new List<string>();
        public long ElapsedMs;
        public InstallCandidate Best;

        public bool HasInstalled
        {
            get { return Candidates.Count > 0; }
        }

        public InstallCandidate Match(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }
            string full = Normalize(dir);
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (string.Equals(Normalize(Candidates[i].Dir), full, StringComparison.OrdinalIgnoreCase))
                {
                    return Candidates[i];
                }
            }
            return null;
        }

        public InstallResidue ResidueFor(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }
            string full = Normalize(dir);
            for (int i = 0; i < Residues.Count; i++)
            {
                if (string.Equals(Normalize(Residues[i].Dir), full, StringComparison.OrdinalIgnoreCase))
                {
                    return Residues[i];
                }
            }
            return null;
        }

        internal static string Normalize(string dir)
        {
            try
            {
                return Path.GetFullPath(dir).TrimEnd('\\');
            }
            catch
            {
                return (dir ?? "").TrimEnd('\\');
            }
        }

        public string Summary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("检测到 ").Append(Candidates.Count).Append(" 个已安装实例");
            for (int i = 0; i < Candidates.Count; i++)
            {
                sb.Append(i == 0 ? "：" : "；");
                sb.Append(Candidates[i].Dir).Append("（").Append(Candidates[i].VersionLabel)
                  .Append("，来源 ").Append(Candidates[i].Source).Append("）");
            }
            if (Residues.Count > 0)
            {
                sb.Append("；残留 ").Append(Residues.Count).Append(" 个");
                for (int i = 0; i < Residues.Count; i++)
                {
                    sb.Append(i == 0 ? "：" : "、").Append(Residues[i].Dir);
                }
            }
            sb.Append("；耗时 ").Append(ElapsedMs).Append(" ms");
            return sb.ToString();
        }
    }

    internal class UninstallEntry
    {
        public string Hive = "";
        public string KeyName = "";
        public string DisplayName = "";
        public string DisplayVersion = "";
        public string InstallLocation = "";
        public string DisplayIcon = "";
        public string UninstallString = "";

        public string ResolveDir()
        {
            if (!string.IsNullOrEmpty(InstallLocation))
            {
                return InstallLocation.Trim().Trim('"');
            }
            string fromIcon = PathFromCommand(DisplayIcon);
            if (fromIcon.Length > 0)
            {
                return fromIcon;
            }
            return PathFromCommand(UninstallString);
        }

        private static string PathFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return "";
            }
            string text = command.Trim();
            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = text.IndexOf('"', 1);
                if (end > 1)
                {
                    text = text.Substring(1, end - 1);
                }
            }
            else
            {
                int comma = text.IndexOf(',');
                if (comma > 0)
                {
                    text = text.Substring(0, comma);
                }
                int space = text.IndexOf(' ');
                if (space > 0)
                {
                    text = text.Substring(0, space);
                }
            }
            try
            {
                return Path.GetDirectoryName(text.Trim());
            }
            catch
            {
                return "";
            }
        }
    }

    /// <summary>
    /// 安装检测：注册表卸载项 → 默认目录 → settings.ini 记录的目录 → 上次安装标记。
    /// 每个来源命中即停；只做常数级路径检查，不扫描磁盘，整体 ≤1 秒。
    /// 目录存在但主程序缺失记为「残留」，不算已安装。
    /// </summary>
    internal static class InstallProbe
    {
        public static InstallScan Scan()
        {
            InstallScan scan = new InstallScan();
            Stopwatch watch = Stopwatch.StartNew();
            scan.Steps.Add("检测开始：按 注册表 → 默认目录 → settings.ini → 标记文件 的顺序；不扫描磁盘；多安装时列出全部候选");

            // ---- a. 注册表卸载项（HKCU 优先，再 HKLM 的 64/32 位视图）----
            // 注册表只枚举卸载项子键（常数级），不扫描磁盘；多安装时全部列出，供用户选最高版本。
            List<UninstallEntry> entries = InstallRegistry.ReadUninstall();
            scan.Steps.Add("注册表卸载项：共读取 " + entries.Count + " 条");
            int registryHits = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                UninstallEntry entry = entries[i];
                if (!InstallRegistry.Matches(entry))
                {
                    continue;
                }
                string dir = entry.ResolveDir();
                if (string.IsNullOrEmpty(dir))
                {
                    scan.Steps.Add("注册表：" + entry.DisplayName + " 没有可用安装路径，跳过");
                    continue;
                }
                InstallCandidate candidate = ProbeDir(dir, "注册表(" + entry.Hive + ")", entry.DisplayVersion, scan);
                if (candidate != null && AddCandidate(scan, candidate) != null)
                {
                    registryHits++;
                }
            }
            if (registryHits == 0)
            {
                scan.Steps.Add("注册表：未发现已安装条目");
            }

            // ---- b. 默认目录 ----
            ProbeAndAdd(scan, Config.DefaultInstallDir, "默认目录", null);

            // ---- c. settings.ini 里记录的安装目录 ----
            List<string> settingsFiles = new List<string>();
            AddUnique(settingsFiles, Path.Combine(Config.DefaultInstallDir, "settings.ini"));
            AddUnique(settingsFiles, Config.LegacySettingsPath);
            if (!string.IsNullOrEmpty(Config.InstallDir))
            {
                AddUnique(settingsFiles, Path.Combine(Config.InstallDir, "settings.ini"));
            }
            for (int i = 0; i < settingsFiles.Count; i++)
            {
                string file = settingsFiles[i];
                if (!File.Exists(file))
                {
                    continue;
                }
                string recorded = Ini.Read(file, "InstallDir");
                string dir = recorded.Length > 0 ? recorded : Path.GetDirectoryName(file);
                ProbeAndAdd(scan, dir, "settings.ini", null);
            }

            // ---- d. 上次安装的标记文件（记录路径与版本）----
            string marker = InstallMarker.GlobalPath;
            string markerDir = InstallMarker.Read(marker, "InstallDir");
            string markerVersion = InstallMarker.Read(marker, "Version");
            if (markerDir.Length > 0)
            {
                InstallCandidate candidate = ProbeDir(markerDir, "标记文件", null, scan, markerVersion);
                if (candidate != null)
                {
                    AddCandidate(scan, candidate);
                }
            }
            else
            {
                scan.Steps.Add("标记文件：" + marker + " 不存在或没有记录安装路径");
            }

            scan.Best = PickBest(scan);
            scan.ElapsedMs = watch.ElapsedMilliseconds;
            scan.Steps.Add("检测结束：" + scan.Summary());
            if (scan.ElapsedMs > 1000)
            {
                // 超过 1 秒只记录；正常路径不会发生（没有磁盘扫描）。
                scan.Steps.Add("警告：检测耗时超过 1 秒（" + scan.ElapsedMs + " ms）");
            }
            return scan;
        }

        /// <summary>只要第一个命中结果时使用：注册表命中就直接返回，不再检查其余来源。</summary>
        public static InstallScan ScanFirst()
        {
            InstallScan scan = new InstallScan();
            Stopwatch watch = Stopwatch.StartNew();
            List<UninstallEntry> entries = InstallRegistry.ReadUninstall();
            for (int i = 0; i < entries.Count; i++)
            {
                UninstallEntry entry = entries[i];
                if (!InstallRegistry.Matches(entry))
                {
                    continue;
                }
                string dir = entry.ResolveDir();
                if (dir.Length == 0)
                {
                    continue;
                }
                InstallCandidate candidate = ProbeDir(dir, "注册表(" + entry.Hive + ")", entry.DisplayVersion, scan);
                if (candidate != null)
                {
                    AddCandidate(scan, candidate);
                    scan.ElapsedMs = watch.ElapsedMilliseconds;
                    scan.Best = PickBest(scan);
                    return scan;
                }
            }
            // 其余来源走同一套顺序，但找到第一个就停。
            string[] dirs = new string[] { Config.DefaultInstallDir };
            for (int i = 0; i < dirs.Length; i++)
            {
                InstallCandidate candidate = ProbeDir(dirs[i], "默认目录", null, scan);
                if (candidate != null)
                {
                    AddCandidate(scan, candidate);
                    scan.ElapsedMs = watch.ElapsedMilliseconds;
                    scan.Best = PickBest(scan);
                    return scan;
                }
            }
            string markerDir = InstallMarker.Read(InstallMarker.GlobalPath, "InstallDir");
            if (markerDir.Length > 0)
            {
                InstallCandidate candidate = ProbeDir(markerDir, "标记文件", null, scan,
                    InstallMarker.Read(InstallMarker.GlobalPath, "Version"));
                if (candidate != null)
                {
                    AddCandidate(scan, candidate);
                }
            }
            scan.ElapsedMs = watch.ElapsedMilliseconds;
            scan.Best = PickBest(scan);
            return scan;
        }

        public static InstallCandidate ProbeDir(string dir, string source, string registryVersion, InstallScan scan)
        {
            return ProbeDir(dir, source, registryVersion, scan, null);
        }

        public static InstallCandidate ProbeDir(string dir, string source, string registryVersion, InstallScan scan,
                                                string markerVersionFallback)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }
            string full = InstallScan.Normalize(dir);
            if (scan != null && AlreadyKnown(scan, full))
            {
                return null;
            }
            if (!Directory.Exists(full))
            {
                if (scan != null)
                {
                    scan.Steps.Add(source + " → " + full + "：目录不存在，跳过");
                }
                return null;
            }
            string exe = Path.Combine(full, "WTSkinManager.exe");
            if (!File.Exists(exe))
            {
                if (scan != null)
                {
                    InstallResidue residue = new InstallResidue();
                    residue.Dir = full;
                    residue.Source = source;
                    residue.Reason = "目录存在，但缺少 WTSkinManager.exe";
                    scan.Residues.Add(residue);
                    scan.Steps.Add(source + " → " + full + "：判定为残留（缺少主程序），不算已安装");
                }
                return null;
            }

            InstallCandidate candidate = new InstallCandidate();
            candidate.Dir = full;
            candidate.Source = source;

            // 旧版本读取兜底：exe 版本资源 → 安装目录版本标记 → 注册表 DisplayVersion → settings.ini。
            SemVer version = ReadExeVersion(exe, candidate.VersionAttempts);
            if (version != null)
            {
                candidate.Version = version;
                candidate.VersionSource = "exe 版本资源";
            }
            if (candidate.Version == null)
            {
                string marker = Path.Combine(full, "install-info.ini");
                version = ParseVersion(InstallMarker.Read(marker, "Version"));
                candidate.VersionAttempts.Add("安装目录版本标记：" + (version == null ? "读取失败" : version.Display));
                if (version != null)
                {
                    candidate.Version = version;
                    candidate.VersionSource = "安装目录版本标记";
                }
            }
            if (candidate.Version == null && !string.IsNullOrEmpty(markerVersionFallback))
            {
                version = ParseVersion(markerVersionFallback);
                candidate.VersionAttempts.Add("上次安装标记记录版本：" + (version == null ? "读取失败" : version.Display));
                if (version != null)
                {
                    candidate.Version = version;
                    candidate.VersionSource = "上次安装标记";
                }
            }
            if (candidate.Version == null)
            {
                version = ParseVersion(registryVersion);
                candidate.VersionAttempts.Add("注册表 DisplayVersion：" + (version == null ? "读取失败" : version.Display));
                if (version != null)
                {
                    candidate.Version = version;
                    candidate.VersionSource = "注册表 DisplayVersion";
                }
            }
            if (candidate.Version == null)
            {
                version = ParseVersion(Ini.Read(Path.Combine(full, "settings.ini"), "AppVersion"));
                candidate.VersionAttempts.Add("settings.ini 版本字段：" + (version == null ? "读取失败" : version.Display));
                if (version != null)
                {
                    candidate.Version = version;
                    candidate.VersionSource = "settings.ini 版本字段";
                }
            }
            if (candidate.Version == null)
            {
                candidate.VersionSource = "未知（全部来源读取失败）";
            }

            if (scan != null)
            {
                scan.Steps.Add(source + " → " + full + "：已安装，版本 " +
                              (candidate.Version == null ? "读取失败" : candidate.Version.Display) +
                              "（版本来源：" + candidate.VersionSource + "）");
            }
            return candidate;
        }

        private static InstallCandidate AddCandidate(InstallScan scan, InstallCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }
            for (int i = 0; i < scan.Candidates.Count; i++)
            {
                if (string.Equals(InstallScan.Normalize(scan.Candidates[i].Dir),
                                  InstallScan.Normalize(candidate.Dir), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            scan.Candidates.Add(candidate);
            return candidate;
        }

        private static void ProbeAndAdd(InstallScan scan, string dir, string source, string registryVersion)
        {
            InstallCandidate candidate = ProbeDir(dir, source, registryVersion, scan);
            if (candidate != null)
            {
                AddCandidate(scan, candidate);
            }
        }

        private static bool AlreadyKnown(InstallScan scan, string dir)
        {
            for (int i = 0; i < scan.Candidates.Count; i++)
            {
                if (string.Equals(InstallScan.Normalize(scan.Candidates[i].Dir), dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            for (int i = 0; i < scan.Residues.Count; i++)
            {
                if (string.Equals(InstallScan.Normalize(scan.Residues[i].Dir), dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static InstallCandidate PickBest(InstallScan scan)
        {
            InstallCandidate best = null;
            for (int i = 0; i < scan.Candidates.Count; i++)
            {
                InstallCandidate current = scan.Candidates[i];
                if (best == null)
                {
                    best = current;
                    continue;
                }
                if (current.Version == null && best.Version != null)
                {
                    continue;
                }
                if (current.Version != null && best.Version == null)
                {
                    best = current;
                    continue;
                }
                if (current.Version != null && best.Version != null &&
                    current.Version.CompareTo(best.Version) > 0)
                {
                    best = current;
                }
            }
            return best;
        }

        private static SemVer ReadExeVersion(string exe, List<string> attempts)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(exe);
                SemVer parsed = ParseVersion(info.FileVersion);
                if (parsed == null)
                {
                    parsed = ParseVersion(info.ProductVersion);
                }
                attempts.Add("exe 版本资源：" + (parsed == null ? "读取失败" : parsed.Display));
                return parsed;
            }
            catch (Exception ex)
            {
                attempts.Add("exe 版本资源：读取失败（" + ex.Message + "）");
                return null;
            }
        }

        public static SemVer ParseVersion(string text)
        {
            SemVer version;
            return SemVer.TryParse(text, out version) ? version : null;
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            list.Add(value);
        }
    }

    /// <summary>install-info.ini：上次安装的标记文件，记录安装路径、程序版本、数据版本。</summary>
    internal static class InstallMarker
    {
        public const string FileName = "install-info.ini";

        public static string GlobalDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WTSkinManager");
            }
        }

        public static string GlobalPath
        {
            get { return Path.Combine(GlobalDir, FileName); }
        }

        public static string LocalPath(string dir)
        {
            return string.IsNullOrEmpty(dir) ? "" : Path.Combine(dir, FileName);
        }

        public static string Read(string path, string key)
        {
            return Ini.Read(path, key);
        }

        /// <summary>安装/更新成功时写入：安装目录一份，%LOCALAPPDATA% 再存一份指针。</summary>
        public static bool Write(string dir, string version, string dataVersion, string previousVersion)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 战雷涂装管理器安装标记（记录路径与版本，供安装器检测）");
                sb.AppendLine("InstallDir=" + dir);
                sb.AppendLine("Version=" + version);
                sb.AppendLine("DataVersion=" + dataVersion);
                sb.AppendLine("PreviousVersion=" + (previousVersion ?? ""));
                sb.AppendLine("InstalledAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                string text = sb.ToString();
                string local = LocalPath(dir);
                if (local.Length > 0)
                {
                    File.WriteAllText(local, text, new UTF8Encoding(false));
                }
                Directory.CreateDirectory(GlobalDir);
                File.WriteAllText(GlobalPath, text, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                InstallLog.Write("写入安装标记失败：" + ex.Message);
                return false;
            }
        }
    }

    /// <summary>注册表卸载项读写。HKCU 优先，再 HKLM；检测与写入都不需要管理员权限。</summary>
    internal static class InstallRegistry
    {
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        public const string AppKeyName = "WTSkinManager";
        public const string DisplayName = "战雷涂装管理器";

        public static List<UninstallEntry> ReadUninstall()
        {
            List<UninstallEntry> result = new List<UninstallEntry>();
            ReadHive(result, RegistryHive.CurrentUser, RegistryView.Default, "HKCU");
            ReadHive(result, RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64");
            ReadHive(result, RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM32");
            return result;
        }

        private static void ReadHive(List<UninstallEntry> result, RegistryHive hive, RegistryView view, string label)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(UninstallKey))
                {
                    if (key == null)
                    {
                        return;
                    }
                    string[] names;
                    try
                    {
                        names = key.GetSubKeyNames();
                    }
                    catch
                    {
                        return;
                    }
                    for (int i = 0; i < names.Length; i++)
                    {
                        using (RegistryKey sub = key.OpenSubKey(names[i]))
                        {
                            if (sub == null)
                            {
                                continue;
                            }
                            UninstallEntry entry = new UninstallEntry();
                            entry.Hive = label;
                            entry.KeyName = names[i];
                            entry.DisplayName = Str(sub, "DisplayName");
                            if (!LooksRelevant(names[i]) &&
                                entry.DisplayName.IndexOf("WTSkinManager", StringComparison.OrdinalIgnoreCase) < 0 &&
                                entry.DisplayName.IndexOf("战雷涂装管理器", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }
                            entry.DisplayVersion = Str(sub, "DisplayVersion");
                            entry.InstallLocation = Str(sub, "InstallLocation");
                            entry.DisplayIcon = Str(sub, "DisplayIcon");
                            entry.UninstallString = Str(sub, "UninstallString");
                            result.Add(entry);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static bool LooksRelevant(string keyName)
        {
            return keyName.IndexOf("WTSkinManager", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool Matches(UninstallEntry entry)
        {
            if (entry == null)
            {
                return false;
            }
            if (entry.DisplayName.IndexOf("WTSkinManager", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            if (entry.DisplayName.IndexOf("战雷涂装管理器", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return LooksRelevant(entry.KeyName);
        }

        /// <summary>写入/更新 HKCU 卸载项，让安装检测在下次能读到路径与版本。</summary>
        public static bool Write(string dir, string version)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey + "\\" + AppKeyName))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    key.SetValue("DisplayName", DisplayName, RegistryValueKind.String);
                    key.SetValue("DisplayVersion", version ?? "", RegistryValueKind.String);
                    key.SetValue("InstallLocation", dir ?? "", RegistryValueKind.String);
                    key.SetValue("DisplayIcon", Path.Combine(dir ?? "", "WTSkinManager.exe") + ",0", RegistryValueKind.String);
                    key.SetValue("Publisher", "WTSkinManager", RegistryValueKind.String);
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                return true;
            }
            catch (Exception ex)
            {
                InstallLog.Write("写入注册表卸载项失败：" + ex.Message);
                return false;
            }
        }

        private static string Str(RegistryKey key, string name)
        {
            try
            {
                object value = key.GetValue(name);
                return value == null ? "" : value.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }
    }

    /// <summary>够用的 INI 读取（只读，键不区分大小写）。</summary>
    internal static class Ini
    {
        public static string Read(string path, string key)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(key) || !File.Exists(path))
            {
                return "";
            }
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(eq + 1).Trim();
                    }
                }
            }
            catch
            {
            }
            return "";
        }
    }
}
