using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 统一的重启入口：延迟 2 秒再启动新进程，让旧进程先退出，
    /// 避免单实例检查把新进程挡掉（表现为「已经在运行了」且新界面不出现）。
    /// </summary>
    internal static class AppRestart
    {
        public const int DelaySeconds = 2;

        public static void Restart()
        {
            string exe;
            try
            {
                exe = Application.ExecutablePath;
            }
            catch
            {
                exe = Config.CurrentExe;
            }
            string args = "";
            try
            {
                string[] argv = Environment.GetCommandLineArgs();
                for (int i = 1; i < argv.Length; i++)
                {
                    if (args.Length > 0)
                    {
                        args += " ";
                    }
                    args += "\"" + argv[i].Replace("\"", "\\\"") + "\"";
                }
            }
            catch
            {
            }
            try
            {
                // ping 作为延时器：-n 3 约 2 秒，不需要用户输入，也不依赖 timeout.exe 的交互。
                string command = "/c ping 127.0.0.1 -n " + (DelaySeconds + 1).ToString() +
                                 " >nul & start \"\" \"" + exe + "\"" +
                                 (args.Length > 0 ? " " + args : "");
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "cmd.exe";
                info.Arguments = command;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
                Process.Start(info);
            }
            catch
            {
            }
            try
            {
                Config.Log("重启程序：延迟 " + DelaySeconds + " 秒启动新进程 → " + exe);
            }
            catch
            {
            }
            Application.Exit();
        }
    }
}
