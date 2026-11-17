using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WtSkin
{
    /// <summary>
    /// 单实例锁。更新完成后需要在启动新进程前主动释放，否则新进程会被误判为重复运行。
    /// 单独成类，便于安装向导与自测程序复用。
    /// </summary>
    /// <summary>安装/更新前查找并关闭正在运行的主程序。</summary>
    internal static class RunningInstance
    {
        public const string ProcessName = "WTSkinManager";

        public static List<Process> FindOthers()
        {
            List<Process> list = new List<Process>();
            try
            {
                Process[] all = Process.GetProcessesByName(ProcessName);
                int self = Process.GetCurrentProcess().Id;
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        if (all[i].Id != self && !all[i].HasExited)
                        {
                            list.Add(all[i]);
                        }
                    }
                    catch
                    {
                        try { all[i].Dispose(); } catch { }
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        /// <summary>先请求正常关闭，超时后强制结束；返回是否全部退出。</summary>
        public static bool CloseAll(List<Process> processes, out string error)
        {
            error = "";
            if (processes == null || processes.Count == 0)
            {
                return true;
            }
            for (int i = 0; i < processes.Count; i++)
            {
                try
                {
                    if (!processes[i].HasExited)
                    {
                        processes[i].CloseMainWindow();
                    }
                }
                catch
                {
                }
            }
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                bool alive = false;
                for (int i = 0; i < processes.Count; i++)
                {
                    try
                    {
                        if (!processes[i].HasExited)
                        {
                            alive = true;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
                if (!alive)
                {
                    return true;
                }
                System.Threading.Thread.Sleep(200);
            }
            for (int i = 0; i < processes.Count; i++)
            {
                try
                {
                    if (!processes[i].HasExited)
                    {
                        processes[i].Kill();
                        processes[i].WaitForExit(3000);
                    }
                }
                catch
                {
                }
            }
            Thread.Sleep(300);
            for (int i = 0; i < processes.Count; i++)
            {
                try
                {
                    if (!processes[i].HasExited)
                    {
                        error = "无法关闭正在运行的程序（PID " + processes[i].Id + "）";
                        return false;
                    }
                }
                catch
                {
                }
            }
            return true;
        }
    }

    internal static class InstanceGuard
    {
        private static Mutex _mutex;

        public static void Attach(Mutex mutex)
        {
            _mutex = mutex;
        }

        public static void Release()
        {
            try
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex = null;
                }
            }
            catch
            {
            }
        }
    }
}
