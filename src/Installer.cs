using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace WtSkin
{
    /// <summary>把程序安装/更新到目标目录、写设置与标记、建快捷方式。</summary>
    internal static class Installer
    {
        /// <summary>测试/工具场景下不创建桌面与开始菜单快捷方式；正式安装保持 false。</summary>
        public static bool SuppressShortcuts = false;

        /// <summary>
        /// 安装或覆盖更新。existing 为检测到的已安装实例（可为 null，此时按目标目录现场探测）。
        /// 成功返回安装结果；失败抛 InstallException / InstallBusyException / UnauthorizedAccessException。
        /// </summary>
        public static InstallResult Install(string installDir, string gameDir, InstallCandidate existing)
        {
            return UpdateEngine.Execute(installDir, gameDir, existing);
        }

        public static InstallResult Install(string installDir, string gameDir, InstallCandidate existing, string sourceExe)
        {
            return UpdateEngine.Execute(installDir, gameDir, existing, sourceExe);
        }

        public static InstallResult Install(string installDir, string gameDir)
        {
            return Install(installDir, gameDir, null);
        }

        public static void CreateShortcuts(string exe, string workDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return;
                }
                object shell = Activator.CreateInstance(shellType);
                CreateShortcut(shell, shellType,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "战雷涂装管理器.lnk"),
                    exe, workDir);
                CreateShortcut(shell, shellType,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "战雷涂装管理器.lnk"),
                    exe, workDir);
            }
            catch
            {
            }
        }

        private static void CreateShortcut(object shell, Type shellType, string linkPath, string exe, string workDir)
        {
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            Type type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
            type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exe + ",0" });
            type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "战雷涂装管理器" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        public static string EnvironmentHint()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("可能的原因：");
            sb.AppendLine("· 桌面 / 文档等文件夹被安全软件（火绒、360 等）保护，未知程序不能写入");
            sb.AppendLine("· 该文件夹的 Windows 权限不允许当前用户修改");
            sb.AppendLine();
            sb.Append("建议装到不受保护的普通目录（例如 D 盘上新建一个文件夹），或者直接用默认目录。");
            return sb.ToString();
        }
    }
}
