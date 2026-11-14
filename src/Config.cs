using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WtSkin
{
    /// <summary>安装位置与用户设置。所有数据都放在安装目录里，不写进游戏目录。</summary>
    internal static class Config
    {
        public static string InstallDir = "";
        public static string GameDir = "";
        public static bool Backup = true;
        public static int KeepBackups = 8;
        /// <summary>缺少 template_ 目录时：0=每次询问，1=自动创建，2=关闭。</summary>
        public static int MissingFolderPolicy = 0;
        public static int ThemeIndex = 0;
        public static int ZoomPercent = 100;
        public static bool Animations = false;
        /// <summary>开发模式：把窗口尺寸/状态变化写入日志，用于排查缩放与恢复问题。</summary>
        public static bool DebugLayout = false;
        public static string AllowedExt = ".dds,.blk,.tga";
        public static string WarnExt = ".txt,.md,.pdf,.jpg,.ini,.url,.rtf";
        public static string IllegalExt = ".png,.exe,.bat,.cmd,.dll,.js,.ps1,.vbs,.msi,.scr,.com";
        public static string BackupRoot = "";
        public static bool FirstRunDone = false;
        public static bool Installed = false;

        // ---- 更新检查（设置页「更新与日志」+ 启动时静默检查） ----
        /// <summary>启动时自动检查更新，默认开启；关闭后仅在设置页手动检查。</summary>
        public static bool AutoCheckUpdate = true;
        public static string UpdateApiLatest = "https://api.github.com/repos/Nchoneko/WTSkinManager/releases/latest";
        public static string UpdateApiList = "https://api.github.com/repos/Nchoneko/WTSkinManager/releases";
        public static string ReleasesPage = "https://github.com/Nchoneko/WTSkinManager/releases";
        public static string RepoUrl = "https://github.com/Nchoneko/WTSkinManager";

        /// <summary>
        /// 应用内更新的镜像前缀模板（逗号分隔，按顺序探测；{version} 替换成 3.9.13 这样的三段版本）。
        /// 直连 GitHub 失败时才会用到；改成空字符串即完全关闭镜像。
        /// </summary>
        public static string DefaultDownloadMirrors =
            "https://ghfast.top/https://github.com/Nchoneko/WTSkinManager/releases/download/v{version}/WTSkinManager.exe," +
            "https://gh-proxy.com/https://github.com/Nchoneko/WTSkinManager/releases/download/v{version}/WTSkinManager.exe," +
            "https://ghproxy.net/https://github.com/Nchoneko/WTSkinManager/releases/download/v{version}/WTSkinManager.exe";

        public static string DownloadMirrors = DefaultDownloadMirrors;

        public static string DefaultInstallDir
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "Programs", "WTSkinManager");
            }
        }

        public static string ExeDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string ExePath
        {
            get { return Path.Combine(ExeDir, "WTSkinManager.exe"); }
        }

        /// <summary>当前真实运行的 exe（用户可能改过文件名）。</summary>
        public static string CurrentExe
        {
            get { return System.Windows.Forms.Application.ExecutablePath; }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(InstallDir, "settings.ini"); }
        }

        public static string BackupDir
        {
            get { return Path.Combine(InstallDir, "backups"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(InstallDir, "logs"); }
        }

        public static string LogPath
        {
            get { return Path.Combine(LogDir, "import.log"); }
        }

        public static string CacheDir
        {
            get { return Path.Combine(InstallDir, "cache"); }
        }

        public static string InstalledFlag
        {
            get { return Path.Combine(InstallDir, "installed.flag"); }
        }

        public static string UserSkinsDir
        {
            get
            {
                if (string.IsNullOrEmpty(GameDir))
                {
                    return "";
                }
                return Path.Combine(GameDir, "UserSkins");
            }
        }

        public static string LegacySettingsPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WTSkinManager");
                return Path.Combine(dir, "settings.ini");
            }
        }

        /// <summary>已安装时用 exe 所在目录；否则用默认安装目录（等待安装向导确认）。</summary>
        public static string DetectInstallDir()
        {
            string exeDir = ExeDir;
            if (File.Exists(Path.Combine(exeDir, "installed.flag")) || File.Exists(Path.Combine(exeDir, "settings.ini")))
            {
                return exeDir;
            }
            return DefaultInstallDir;
        }

        public static void Load()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path) && File.Exists(LegacySettingsPath))
                {
                    path = LegacySettingsPath;
                }
                if (!File.Exists(path))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string value = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "gamedir":
                            GameDir = value;
                            break;
                        case "backup":
                            Backup = value != "0";
                            break;
                        case "missingfolderpolicy":
                            int missingPolicy;
                            if (int.TryParse(value, out missingPolicy) && missingPolicy >= 0 && missingPolicy <= 2)
                            {
                                MissingFolderPolicy = missingPolicy;
                            }
                            break;
                        case "keepbackups":
                            int keep;
                            if (int.TryParse(value, out keep) && keep >= 1 && keep <= 50)
                            {
                                KeepBackups = keep;
                            }
                            break;
                        case "theme":
                            int theme;
                            if (int.TryParse(value, out theme) && theme >= 0 && theme < Palettes.All.Length)
                            {
                                ThemeIndex = theme;
                            }
                            break;
                        case "zoom":
                            int zoom;
                            if (int.TryParse(value, out zoom) && zoom >= 75 && zoom <= 150)
                            {
                                ZoomPercent = zoom;
                            }
                            break;
                        case "animations":
                            Animations = value != "0";
                            break;
                        case "debuglayout":
                            DebugLayout = value == "1";
                            break;
                        case "allowedext":
                            AllowedExt = value;
                            break;
                        case "warnext":
                            WarnExt = value;
                            break;
                        case "illegalext":
                            IllegalExt = value;
                            break;
                        case "backuproot":
                            BackupRoot = value;
                            break;
                        case "firstrun":
                            FirstRunDone = value == "1";
                            break;
                        case "autocheckupdate":
                            AutoCheckUpdate = value != "0";
                            break;
                        case "updateapilatest":
                            if (value.Length > 0)
                            {
                                UpdateApiLatest = value;
                            }
                            break;
                        case "updateapilist":
                            if (value.Length > 0)
                            {
                                UpdateApiList = value;
                            }
                            break;
                        case "releasespage":
                            if (value.Length > 0)
                            {
                                ReleasesPage = value;
                            }
                            break;
                        case "downloadmirrors":
                            // 允许显式留空来关闭镜像；未配置时保持默认三条模板。
                            DownloadMirrors = value;
                            break;
                    }
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(InstallDir))
                {
                    return;
                }
                if (!Directory.Exists(InstallDir))
                {
                    Directory.CreateDirectory(InstallDir);
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 战雷涂装管理器设置");
                sb.AppendLine("GameDir=" + GameDir);
                sb.AppendLine("InstallDir=" + InstallDir);
                sb.AppendLine("AppVersion=" + AppInfo.VersionText);
                sb.AppendLine("DataVersion=" + AppInfo.DataVersion);
                sb.AppendLine("Backup=" + (Backup ? "1" : "0"));
                sb.AppendLine("KeepBackups=" + KeepBackups);
                sb.AppendLine("MissingFolderPolicy=" + MissingFolderPolicy);
                sb.AppendLine("Theme=" + ThemeIndex);
                sb.AppendLine("Zoom=" + ZoomPercent);
                sb.AppendLine("Animations=" + (Animations ? "1" : "0"));
            sb.AppendLine("DebugLayout=" + (DebugLayout ? "1" : "0"));
                sb.AppendLine("AllowedExt=" + AllowedExt);
                sb.AppendLine("WarnExt=" + WarnExt);
                sb.AppendLine("IllegalExt=" + IllegalExt);
                sb.AppendLine("BackupRoot=" + BackupRoot);
                sb.AppendLine("FirstRun=" + (FirstRunDone ? "1" : "0"));
                sb.AppendLine("AutoCheckUpdate=" + (AutoCheckUpdate ? "1" : "0"));
                sb.AppendLine("UpdateApiLatest=" + UpdateApiLatest);
                sb.AppendLine("UpdateApiList=" + UpdateApiList);
                sb.AppendLine("ReleasesPage=" + ReleasesPage);
                sb.AppendLine("DownloadMirrors=" + DownloadMirrors);
                File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        public static void EnsureDirs()
        {
            try
            {
                if (!Directory.Exists(InstallDir))
                {
                    Directory.CreateDirectory(InstallDir);
                }
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }
                if (!Directory.Exists(BackupDir))
                {
                    Directory.CreateDirectory(BackupDir);
                }
            }
            catch
            {
            }
        }

        public static void Log(string text)
        {
            try
            {
                EnsureDirs();
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine;
                File.AppendAllText(LogPath, line, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        public static bool GameDirLooksValid(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return false;
            }
            bool hasSkins = Directory.Exists(Path.Combine(dir, "UserSkins"));
            bool hasGame = File.Exists(Path.Combine(dir, "aces.exe")) ||
                           File.Exists(Path.Combine(dir, "config.blk")) ||
                           Directory.Exists(Path.Combine(dir, "win64"));
            return hasSkins && hasGame;
        }
    }

    /// <summary>自动寻找 War Thunder 安装目录。</summary>
    internal static class GameLocator
    {
        public static List<string> FindAll()
        {
            List<string> found = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddSteamLibraries(found, seen);
            AddLauncherRegistry(found, seen);
            AddKnownPaths(found, seen);
            AddShortcuts(found, seen);
            AddRunningProcess(found, seen);

            List<string> valid = new List<string>();
            for (int i = 0; i < found.Count; i++)
            {
                if (Config.GameDirLooksValid(found[i]) && !valid.Contains(found[i]))
                {
                    valid.Add(found[i]);
                }
            }
            return valid;
        }

        public static string FindBest()
        {
            List<string> all = FindAll();
            if (all.Count == 0)
            {
                return "";
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return all[i];
                }
            }
            return all[0];
        }

        private static void Add(string path, List<string> found, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            string full;
            try
            {
                full = Path.GetFullPath(path).TrimEnd('\\');
            }
            catch
            {
                return;
            }
            if (seen.Add(full))
            {
                found.Add(full);
            }
        }

        private static void AddSteamLibraries(List<string> found, HashSet<string> seen)
        {
            List<string> roots = new List<string>();
            string steam = ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
            if (!string.IsNullOrEmpty(steam))
            {
                roots.Add(steam.Replace('/', '\\'));
            }
            string install = ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(install))
            {
                roots.Add(install.Replace('/', '\\'));
            }
            string install32 = ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(install32))
            {
                roots.Add(install32.Replace('/', '\\'));
            }

            HashSet<string> libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < roots.Count; i++)
            {
                string root = roots[i];
                if (libraries.Add(root))
                {
                    Add(Path.Combine(root, "steamapps", "common", "War Thunder"), found, seen);
                }
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                foreach (string lib in ParseLibraryFolders(vdf))
                {
                    if (libraries.Add(lib))
                    {
                        Add(Path.Combine(lib, "steamapps", "common", "War Thunder"), found, seen);
                    }
                }
            }
        }

        private static IEnumerable<string> ParseLibraryFolders(string vdf)
        {
            List<string> result = new List<string>();
            if (!File.Exists(vdf))
            {
                return result;
            }
            try
            {
                string[] lines = File.ReadAllLines(vdf);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int idx = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0)
                    {
                        continue;
                    }
                    string rest = line.Substring(idx + 6).Trim();
                    int first = rest.IndexOf('"');
                    int last = rest.LastIndexOf('"');
                    if (first < 0 || last <= first)
                    {
                        continue;
                    }
                    string path = rest.Substring(first + 1, last - first - 1).Replace("\\\\", "\\");
                    if (path.Length > 1)
                    {
                        result.Add(path.TrimEnd('\\'));
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private static void AddKnownPaths(List<string> found, HashSet<string> seen)
        {
            List<string> suffixes = new List<string>();
            suffixes.Add(Path.Combine("steamapps", "common", "War Thunder"));
            suffixes.Add(Path.Combine("SteamLibrary", "steamapps", "common", "War Thunder"));
            suffixes.Add(Path.Combine("Steam", "steamapps", "common", "War Thunder"));
            suffixes.Add(Path.Combine("Games", "SteamLibrary", "steamapps", "common", "War Thunder"));
            suffixes.Add("War Thunder");
            suffixes.Add("WarThunder");
            suffixes.Add(Path.Combine("Games", "War Thunder"));
            suffixes.Add(Path.Combine("Games", "WarThunder"));
            suffixes.Add(Path.Combine("Program Files", "WarThunder"));
            suffixes.Add(Path.Combine("Program Files (x86)", "WarThunder"));
            suffixes.Add(Path.Combine("WarThunder", "War Thunder"));
            suffixes.Add(Path.Combine("Program Files (x86)", "Steam", "steamapps", "common", "War Thunder"));
            suffixes.Add(Path.Combine("Program Files", "Steam", "steamapps", "common", "War Thunder"));

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return;
            }
            for (int d = 0; d < drives.Length; d++)
            {
                DriveInfo drive = drives[d];
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }
                string root = drive.RootDirectory.FullName;
                for (int s = 0; s < suffixes.Count; s++)
                {
                    string candidate = Path.Combine(root, suffixes[s]);
                    if (Directory.Exists(candidate))
                    {
                        Add(candidate, found, seen);
                    }
                }
            }
        }

        /// <summary>官方启动器的安装路径通常写在 Gaijin 注册表键里。</summary>
        private static void AddLauncherRegistry(List<string> found, HashSet<string> seen)
        {
            string[] keys = new string[]
            {
                @"HKEY_CURRENT_USER\Software\Gaijin Entertainment\War Thunder",
                @"HKEY_CURRENT_USER\Software\Gaijin\War Thunder",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Gaijin Entertainment\War Thunder",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Gaijin Entertainment\War Thunder"
            };
            string[] names = new string[] { "InstallPath", "InstallDir", "Path", "GamePath", "Location", "" };
            for (int k = 0; k < keys.Length; k++)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    string value = ReadRegistry(keys[k], names[n]);
                    AddInstallPath(value, found, seen);
                }
            }
        }

        /// <summary>桌面/开始菜单里的官方启动器快捷方式也指向游戏目录（例如 E:\WarThunder）。</summary>
        private static void AddShortcuts(List<string> found, HashSet<string> seen)
        {
            string[] roots = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };
            object shell = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                if (type != null)
                {
                    shell = Activator.CreateInstance(type);
                }
            }
            catch
            {
                shell = null;
            }
            for (int i = 0; i < roots.Length; i++)
            {
                ScanShortcuts(roots[i], shell, 0, found, seen);
            }
        }

        private static void ScanShortcuts(string dir, object shell, int depth, List<string> found, HashSet<string> seen)
        {
            if (shell == null || depth > 2 || string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }
            try
            {
                string[] files = Directory.GetFiles(dir, "*.lnk");
                for (int i = 0; i < files.Length; i++)
                {
                    string target = ResolveShortcut(shell, files[i]);
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }
                    string name = Path.GetFileNameWithoutExtension(files[i]);
                    string exe = Path.GetFileName(target);
                    if (name.IndexOf("War Thunder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("WarThunder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        exe.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        exe.IndexOf("aces", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        exe.IndexOf("warthunder", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AddInstallPath(target, found, seen);
                    }
                }
                string[] subdirs = Directory.GetDirectories(dir);
                for (int i = 0; i < subdirs.Length; i++)
                {
                    ScanShortcuts(subdirs[i], shell, depth + 1, found, seen);
                }
            }
            catch
            {
            }
        }

        private static string ResolveShortcut(object shell, string path)
        {
            try
            {
                object shortcut = shell.GetType().InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
                object target = shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
                return target as string;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>注册表/快捷方式给的是 exe 或安装目录，统一换算成目录再交给 FindAll 校验。</summary>
        private static void AddInstallPath(string value, List<string> found, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            string path = value.Trim().Trim('"');
            try
            {
                if (File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return;
                }
                Add(path, found, seen);
                string parent = Path.GetDirectoryName(path.TrimEnd('\\'));
                if (!string.IsNullOrEmpty(parent))
                {
                    Add(parent, found, seen);
                }
            }
            catch
            {
            }
        }

        private static void AddRunningProcess(List<string> found, HashSet<string> seen)
        {
            try
            {
                System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("aces");
                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        string file = processes[i].MainModule.FileName;
                        if (!string.IsNullOrEmpty(file))
                        {
                            Add(Path.GetDirectoryName(file), found, seen);
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
        }

        private static string ReadRegistry(string key, string name)
        {
            try
            {
                object value = Microsoft.Win32.Registry.GetValue(key, name, null);
                return value as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
