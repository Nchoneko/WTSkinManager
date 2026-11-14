using System;
using System.Collections.Generic;
using System.IO;

namespace WtSkin
{
    internal enum FolderCreateResult
    {
        Created,
        Existed,
        Cancelled,
        Failed
    }

    internal class FolderCreateOutcome
    {
        public DetectedUnit Unit;
        public string Dir;
        public FolderCreateResult Result;
        public string Error = "";
    }

    /// <summary>
    /// 在 UserSkins 下创建 template_&lt;机型代号&gt; 空目录。
    /// 只做严格校验后的创建：不覆盖、不删除、不写占位文件。
    /// </summary>
    internal static class TemplateFolders
    {
        public const string Prefix = "template_";

        public static string TargetName(string unitId)
        {
            return Prefix + (unitId ?? "").Trim();
        }

        public static bool IsValidUnitId(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return false;
            }
            string id = unitId.Trim();
            if (id.Length == 0 || id.Length > 120 || id == "." || id == "..")
            {
                return false;
            }
            if (id.IndexOf('\\') >= 0 || id.IndexOf('/') >= 0 || id.IndexOf(':') >= 0)
            {
                return false;
            }
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++)
            {
                if (id.IndexOf(bad[i]) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        public static List<FolderCreateOutcome> CreateMany(IList<DetectedUnit> units)
        {
            List<FolderCreateOutcome> list = new List<FolderCreateOutcome>();
            if (units == null)
            {
                return list;
            }
            for (int i = 0; i < units.Count; i++)
            {
                list.Add(CreateOne(units[i]));
            }
            return list;
        }

        public static FolderCreateOutcome CreateOne(DetectedUnit unit)
        {
            FolderCreateOutcome outcome = new FolderCreateOutcome();
            outcome.Unit = unit;
            string unitId = unit != null ? (unit.UnitId ?? "").Trim() : "";
            string root = Config.UserSkinsDir;
            outcome.Dir = "";

            if (unit == null || !IsValidUnitId(unitId))
            {
                outcome.Result = FolderCreateResult.Failed;
                outcome.Error = "机型代号为空或包含非法字符：" + unitId;
                Config.Log("创建涂装目录 失败：" + outcome.Error);
                return outcome;
            }
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                outcome.Result = FolderCreateResult.Failed;
                outcome.Error = "UserSkins 目录不存在：" + root;
                Config.Log("创建涂装目录 失败：" + outcome.Error);
                return outcome;
            }

            string name = TargetName(unitId);
            string full;
            try
            {
                string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                full = Path.GetFullPath(Path.Combine(rootFull, name));
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    outcome.Result = FolderCreateResult.Failed;
                    outcome.Error = "目标路径不在 UserSkins 内：" + full;
                    Config.Log("创建涂装目录 失败：" + outcome.Error);
                    return outcome;
                }
            }
            catch (Exception ex)
            {
                outcome.Result = FolderCreateResult.Failed;
                outcome.Error = "路径无效：" + ex.Message;
                Config.Log("创建涂装目录 失败：" + outcome.Error);
                return outcome;
            }
            outcome.Dir = full;

            if (Directory.Exists(full))
            {
                outcome.Result = FolderCreateResult.Existed;
                Config.Log("创建涂装目录 已存在：" + unitId + " → " + full);
                return outcome;
            }
            if (File.Exists(full))
            {
                outcome.Result = FolderCreateResult.Failed;
                outcome.Error = "同名文件已存在，不能创建目录：" + full;
                Config.Log("创建涂装目录 失败：" + outcome.Error);
                return outcome;
            }
            if (Importer.FindExistingFolder(root, unitId) != null)
            {
                // 已通过其他文件夹形式（如 <代号>）识别，不重复创建。
                outcome.Result = FolderCreateResult.Existed;
                Config.Log("创建涂装目录 已可识别，跳过：" + unitId);
                return outcome;
            }

            try
            {
                Directory.CreateDirectory(full);
                if (Importer.FindExistingFolder(root, unitId) == null)
                {
                    outcome.Result = FolderCreateResult.Failed;
                    outcome.Error = "目录已创建但仍无法识别，请检查游戏目录与代号。";
                    Config.Log("创建涂装目录 失败：" + unitId + " → " + full + "：" + outcome.Error);
                    return outcome;
                }
                outcome.Result = FolderCreateResult.Created;
                Config.Log("创建涂装目录 成功：" + unitId + " → " + full);
            }
            catch (Exception ex)
            {
                outcome.Result = FolderCreateResult.Failed;
                outcome.Error = "创建失败：" + ex.Message;
                Config.Log("创建涂装目录 失败：" + unitId + " → " + full + "：" + ex.Message);
            }
            return outcome;
        }
    }
}
