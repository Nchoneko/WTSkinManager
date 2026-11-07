using System;
using System.Threading;

namespace WtSkin
{
    /// <summary>
    /// 单实例锁。更新完成后需要在启动新进程前主动释放，否则新进程会被误判为重复运行。
    /// 单独成类，便于安装向导与自测程序复用。
    /// </summary>
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
