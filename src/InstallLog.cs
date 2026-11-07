using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WtSkin
{
    /// <summary>
    /// 安装器日志。全程写到「数据目录」下的 logs\installer.log，
    /// 从不写进安装包目录；数据目录不可写时退回临时目录，并在返回内容里注明。
    /// </summary>
    internal static class InstallLog
    {
        private static readonly object Sync = new object();
        private static string _path = "";
        private static bool _fallback;

        public static string Path
        {
            get { lock (Sync) { return _path; } }
        }

        public static bool UsingFallback
        {
            get { lock (Sync) { return _fallback; } }
        }

        /// <summary>开始一次安装/更新会话。dataDir 为空时用 Config.InstallDir。</summary>
        public static void Begin(string dataDir)
        {
            Redirect(dataDir);
            Write("========== 安装器会话开始 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==========");
            Write("日志位置：" + (Path.Length == 0 ? "无法写入（内存记录）" : Path) +
                  (UsingFallback ? "（数据目录不可写，已退回临时目录）" : ""));
        }

        /// <summary>
        /// 只把日志切到另一个数据目录，不写会话头、不清空已有记录。
        /// 安装向导在提示发布者签名之前调用，让签名相关记录直接写进真正的安装目录。
        /// </summary>
        public static void Redirect(string dataDir)
        {
            string dir = string.IsNullOrEmpty(dataDir) ? Config.InstallDir : dataDir;
            string path = "";
            if (!string.IsNullOrEmpty(dir))
            {
                try
                {
                    string logs = System.IO.Path.Combine(dir, "logs");
                    Directory.CreateDirectory(logs);
                    path = System.IO.Path.Combine(logs, "installer.log");
                }
                catch
                {
                    path = "";
                }
            }
            bool fallback = false;
            if (path.Length == 0)
            {
                fallback = true;
                try
                {
                    string logs = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WTSkinManager");
                    Directory.CreateDirectory(logs);
                    path = System.IO.Path.Combine(logs, "installer.log");
                }
                catch
                {
                    path = "";
                }
            }
            lock (Sync)
            {
                _path = path;
                _fallback = fallback;
            }
        }

        /// <summary>日志还没开始时按需开始（设置页在程序运行中补装 / 移除签名时用）。</summary>
        public static void Ensure(string dataDir)
        {
            bool needed;
            lock (Sync)
            {
                needed = _path.Length == 0;
            }
            if (needed)
            {
                Begin(dataDir);
            }
        }

        public static void Write(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (text == null ? "" : text);
            lock (Sync)
            {
                if (_path.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine(line);
                    return;
                }
                try
                {
                    File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
                }
                catch
                {
                }
            }
            System.Diagnostics.Debug.WriteLine(line);
        }

        public static void WriteAll(IEnumerable<string> lines)
        {
            if (lines == null)
            {
                return;
            }
            foreach (string line in lines)
            {
                Write(line);
            }
        }
    }
}
