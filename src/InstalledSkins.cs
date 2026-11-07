using System;
using System.Collections.Generic;
using System.IO;

namespace WtSkin
{
    internal class InstalledSkin
    {
        public string Folder = "";
        public string Path = "";
        public string UnitId = "";
        public bool Disabled;
        public Vehicle Vehicle;
        public bool Selected;
        /// <summary>列表级去重后的简称；由 NameMap.ApplyUniqueTitles 写入。</summary>
        public string ShortNameOverride = "";

        /// <summary>游戏内全名，完整保留。</summary>
        public string FullName
        {
            get
            {
                if (Vehicle != null && !string.IsNullOrEmpty(Vehicle.Zh))
                {
                    return Vehicle.Zh;
                }
                if (!string.IsNullOrEmpty(UnitId))
                {
                    return UnitId;
                }
                return Folder;
            }
        }

        /// <summary>界面显示用简称：配置表 → 中文短名 → 官方名 → 型号代号 → 全名。</summary>
        public string ShortName
        {
            get
            {
                if (!string.IsNullOrEmpty(ShortNameOverride))
                {
                    return ShortNameOverride;
                }
                return BaseShortName;
            }
        }

        /// <summary>未做列表去重的简称，供 NameMap 计算唯一标题。</summary>
        public string BaseShortName
        {
            get
            {
                // 以涂装文件夹里的 slug 为主键（Vehicle 可能按名称匹配到别的 id，导致配置表命中不到）
                string byUnit = NameMap.Short(UnitId, Vehicle == null ? FullName : Vehicle.Zh, FullName);
                if (!string.IsNullOrEmpty(byUnit))
                {
                    return byUnit;
                }
                return NameMap.Short(UnitId, FullName, Folder);
            }
        }

        /// <summary>兼容旧代码：标题即全名。</summary>
        public string Title
        {
            get { return FullName; }
        }

        public string Nation
        {
            get { return Vehicle != null ? Vehicle.Nation : ""; }
        }

        public string NationName
        {
            get { return NameMap.NationName(Nation); }
        }

        public string StatusText
        {
            get { return Disabled ? "已禁用" : "已启用"; }
        }
    }

    /// <summary>
    /// 只枚举 UserSkins 顶层的目录名来判断涂装，不读取目录里的任何文件。
    /// 禁用状态只看目录名是否以 .BAN 结尾。
    /// </summary>
    internal static class InstalledSkins
    {
        public const string BanSuffix = ".BAN";

        public static List<InstalledSkin> Scan(string userSkins)
        {
            List<InstalledSkin> list = new List<InstalledSkin>();
            if (string.IsNullOrEmpty(userSkins) || !Directory.Exists(userSkins))
            {
                return list;
            }
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(userSkins);
            }
            catch
            {
                return list;
            }
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.IsNullOrEmpty(name) || name.StartsWith("."))
                {
                    continue;
                }
                bool disabled = name.EndsWith(BanSuffix, StringComparison.OrdinalIgnoreCase);
                string baseName = disabled ? name.Substring(0, name.Length - BanSuffix.Length) : name;
                InstalledSkin skin = new InstalledSkin();
                skin.Folder = name;
                skin.Path = dirs[i];
                skin.Disabled = disabled;
                skin.UnitId = ParseUnit(baseName);
                if (skin.UnitId.Length > 0)
                {
                    skin.Vehicle = VehicleDb.Find(skin.UnitId);
                }
                list.Add(skin);
            }
            list.Sort(delegate(InstalledSkin a, InstalledSkin b)
            {
                int r = string.Compare(NationInfo.Rank(a.Nation).ToString(), NationInfo.Rank(b.Nation).ToString(), StringComparison.Ordinal);
                if (r != 0)
                {
                    return r;
                }
                r = string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
                if (r != 0)
                {
                    return r;
                }
                return string.Compare(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static string ParseUnit(string name)
        {
            if (name.StartsWith("template_", StringComparison.OrdinalIgnoreCase))
            {
                string id = name.Substring("template_".Length);
                int dot = id.IndexOf('.');
                if (dot > 0)
                {
                    id = id.Substring(0, dot);
                }
                if (VehicleDb.Find(id) != null)
                {
                    return id;
                }
            }
            if (VehicleDb.Find(name) != null)
            {
                return name;
            }
            return "";
        }

        public static bool Disable(InstalledSkin skin, out string error)
        {
            error = null;
            if (skin.Disabled)
            {
                return true;
            }
            string target = skin.Path + BanSuffix;
            try
            {
                if (!Directory.Exists(skin.Path) && Directory.Exists(target))
                {
                    skin.Path = target;
                    if (!skin.Folder.EndsWith(BanSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        skin.Folder = skin.Folder + BanSuffix;
                    }
                    skin.Disabled = true;
                    return true;
                }
                if (Directory.Exists(target))
                {
                    error = "目标目录已存在：" + target;
                    return false;
                }
                Directory.Move(skin.Path, target);
                skin.Path = target;
                if (!skin.Folder.EndsWith(BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    skin.Folder = skin.Folder + BanSuffix;
                }
                skin.Disabled = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool Enable(InstalledSkin skin, out string error)
        {
            error = null;
            if (!skin.Disabled)
            {
                return true;
            }
            string target = skin.Path.Substring(0, skin.Path.Length - BanSuffix.Length);
            try
            {
                if (!Directory.Exists(skin.Path) && Directory.Exists(target))
                {
                    skin.Path = target;
                    if (skin.Folder.EndsWith(BanSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        skin.Folder = skin.Folder.Substring(0, skin.Folder.Length - BanSuffix.Length);
                    }
                    skin.Disabled = false;
                    return true;
                }
                if (Directory.Exists(target))
                {
                    error = "目标目录已存在：" + target;
                    return false;
                }
                Directory.Move(skin.Path, target);
                skin.Path = target;
                if (skin.Folder.EndsWith(BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    skin.Folder = skin.Folder.Substring(0, skin.Folder.Length - BanSuffix.Length);
                }
                skin.Disabled = false;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string BackupRootDir
        {
            get
            {
                string configured = Config.BackupRoot;
                if (!string.IsNullOrEmpty(configured))
                {
                    return configured;
                }
                return Path.Combine(Config.InstallDir, "backups", "skins");
            }
        }
    }
}
