using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace WtSkin
{
    /// <summary>同名涂装文件夹已存在时的处理方式。</summary>
    internal enum SkinConflictPolicy
    {
        /// <summary>自动改名，不覆盖旧涂装（默认）。</summary>
        Rename = 0,
        /// <summary>先把旧涂装备份到 backups，再覆盖写入。</summary>
        Overwrite = 1,
        /// <summary>跳过，不改动旧涂装。</summary>
        Skip = 2
    }

    internal class ImportTarget
    {
        public SkinPackage Package;
        public DetectedUnit Unit;
        /// <summary>第一级目录：UserSkins\ 下的载具模板文件夹（通常为 template_&lt;载具ID&gt;）。</summary>
        public string Folder;
        /// <summary>第二级目录名：涂装文件夹名，由导入确认框填写/校验。</summary>
        public string SkinName = "";

        /// <summary>实际写入的涂装子目录（未做安全校验，仅用于显示与已知合法值拼接）。</summary>
        public string TargetDir
        {
            get
            {
                if (string.IsNullOrEmpty(Folder) || string.IsNullOrEmpty(SkinName))
                {
                    return Folder;
                }
                return Path.Combine(Folder, SkinName);
            }
        }
    }

    internal class ImportPlan
    {
        public List<ImportTarget> Targets = new List<ImportTarget>();
        public List<DetectedUnit> Missing = new List<DetectedUnit>();
        public List<string> Warnings = new List<string>();
        public long TotalBytes;
        /// <summary>同名涂装文件夹冲突处理方式，由导入确认框选择。</summary>
        public SkinConflictPolicy Conflict = SkinConflictPolicy.Rename;
    }

    internal class ImportResult
    {
        public List<string> Done = new List<string>();
        public List<string> Failed = new List<string>();
        /// <summary>真正导入成功的目标（用于写导入记录）。</summary>
        public List<ImportTarget> DoneTargets = new List<ImportTarget>();
        public int Folders;
        public long Bytes;
    }

    /// <summary>
    /// 只往游戏已经生成好的载具文件夹里导入：先备份旧文件夹到安装目录，再清空、复制。
    /// 不会新建文件夹，也不会改文件夹名——游戏只认它自己生成的那个。
    /// </summary>
    internal static class Importer
    {
        /// <summary>
        /// 只认“专属”文件夹：template_&lt;载具ID&gt;，或只包含这一个载具 .blk 的文件夹。
        /// 一个文件夹里有多个型号的 .blk 时，不会把它算到每个型号头上（否则所有行都串成同一个名字）。
        /// </summary>
        public static string FindExistingFolder(string userSkins, string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || !Directory.Exists(userSkins))
            {
                return null;
            }
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(userSkins);
            }
            catch
            {
                return null;
            }
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.Equals(name, "template_" + unitId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    return dirs[i];
                }
            }
            return null;
        }

        private static int CountBlks(string dir)
        {
            try
            {
                return Directory.GetFiles(dir, "*.blk", SearchOption.TopDirectoryOnly).Length;
            }
            catch
            {
                return 99;
            }
        }

        public static bool ContainsUnitBlk(string dir, string unitId)
        {
            try
            {
                string[] files = Directory.GetFiles(dir, "*.blk", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    if (string.Equals(Path.GetFileNameWithoutExtension(files[i]), unitId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static ImportPlan Build(List<SkinPackage> packages)
        {
            ImportPlan plan = new ImportPlan();
            string skins = Config.UserSkinsDir;
            HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> takenUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> missingSeen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int templateCount = 0;
            int legacyCount = 0;
            int legacyFileCount = 0;

            Action<string> warn = delegate(string text)
            {
                if (warnings.Add(text))
                {
                    plan.Warnings.Add(text);
                }
            };

            for (int p = 0; p < packages.Count; p++)
            {
                SkinPackage package = packages[p];
                List<DetectedUnit> units = package.SelectedUnits();
                units.Sort(NationInfo.CompareUnits);
                for (int u = 0; u < units.Count; u++)
                {
                    DetectedUnit unit = units[u];
                    string owner;
                    if (takenUnits.TryGetValue(unit.UnitId, out owner))
                    {
                        warn("「" + unit.Title + "」同时出现在 " + owner + " 和 " + package.Name + "，只导入前者。");
                        continue;
                    }
                    takenUnits[unit.UnitId] = package.Name;

                    string folder = FindExistingFolder(skins, unit.UnitId);
                    if (folder == null)
                    {
                        if (!missingSeen.ContainsKey(unit.UnitId))
                        {
                            missingSeen[unit.UnitId] = unit.Title;
                            plan.Missing.Add(unit);
                        }
                        continue;
                    }

                    ImportTarget target = new ImportTarget();
                    target.Package = package;
                    target.Unit = unit;
                    // 第一级仍是游戏生成/已有的载具模板文件夹，实际涂装写到它下面的子文件夹。
                    target.Folder = folder;
                    target.SkinName = package.TopFolderName != null ? package.TopFolderName : "";
                    plan.Targets.Add(target);

                    if (Path.GetFileName(folder).StartsWith(TemplateFolders.Prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        templateCount++;
                    }
                    else
                    {
                        legacyCount++;
                    }
                    if (HasTopLevelFiles(folder))
                    {
                        legacyFileCount++;
                    }
                    if (unit.Confidence < 100)
                    {
                        warn("「" + unit.Title + "」是按文件名推测的（" + unit.UnitId + "），请确认。");
                    }
                }
            }

            if (templateCount > 0)
            {
                warn("新涂装会写进 template_<载具ID>\\<涂装文件夹名> 子文件夹；模板文件夹里已有的旧涂装不会被清理或覆盖。");
            }
            if (legacyCount > 0)
            {
                warn("有 " + legacyCount.ToString(CultureInfo.InvariantCulture) +
                     " 个载具用的不是 template_ 文件夹，导入会写进它的子文件夹，旧内容同样保留。");
            }
            if (legacyFileCount > 0)
            {
                warn("有 " + legacyFileCount.ToString(CultureInfo.InvariantCulture) +
                     " 个载具文件夹里直接放着旧结构涂装文件（旧结构：文件夹本身就是涂装），本次不会清理它们，新涂装会另外写进子文件夹。");
            }

            plan.TotalBytes = EstimateBytes(plan);
            return plan;
        }

        /// <summary>导入时可能写入/备份的字节数，用于进度与磁盘空间检查。</summary>
        public static long EstimateBytes(ImportPlan plan)
        {
            long total = 0;
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                ImportTarget target = plan.Targets[i];
                if (target.Package != null)
                {
                    total += target.Package.TotalBytes;
                }
                string dir;
                if (Config.Backup && plan.Conflict == SkinConflictPolicy.Overwrite &&
                    TryTargetDir(target, out dir) && Directory.Exists(dir))
                {
                    total += Util.DirectorySize(dir);
                }
            }
            return total;
        }

        /// <summary>载具文件夹下是否直接放着文件（旧结构：文件夹本身就是涂装）。</summary>
        private static bool HasTopLevelFiles(string folder)
        {
            try
            {
                return Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static ImportResult Execute(ImportPlan plan, Action<long, long, string> progress, Func<bool> cancelled)
        {
            ImportResult result = new ImportResult();
            string skins = Config.UserSkinsDir;
            if (!Directory.Exists(skins))
            {
                Directory.CreateDirectory(skins);
            }
            Config.EnsureDirs();

            long total = plan.TotalBytes;
            try
            {
                string rootPath = Path.GetPathRoot(Path.GetFullPath(skins));
                if (!string.IsNullOrEmpty(rootPath))
                {
                    DriveInfo drive = new DriveInfo(rootPath);
                    if (drive.AvailableFreeSpace < total + total / 5)
                    {
                        result.Failed.Add("磁盘剩余空间不足，未开始写入（需要约 " + Util.HumanSize(total + total / 5) + "）");
                        return result;
                    }
                }
            }
            catch
            {
            }
            long done = 0;
            result.Folders = plan.Targets.Count;

            HashSet<string> processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                if (cancelled != null && cancelled())
                {
                    break;
                }
                ImportTarget target = plan.Targets[i];
                string title = target.Unit != null ? target.Unit.Title : "";
                string skinDir = null;
                string backupPath = null;
                bool existedBefore = false;
                bool modified = false;
                try
                {
                    // 1. 校验涂装名并算第二级目录；同名冲突按确认框里选择的方式处理。
                    string resolveError;
                    skinDir = ResolveTargetDir(target, plan.Conflict, out resolveError);
                    if (skinDir == null)
                    {
                        result.Failed.Add(title + "：" + resolveError);
                        continue;
                    }
                    if (!processed.Add(skinDir))
                    {
                        continue;
                    }

                    // 2. 只动这个涂装子目录：template 文件夹本身和同级其他涂装一律不碰。
                    if (!Directory.Exists(target.Folder))
                    {
                        Directory.CreateDirectory(Util.LongPath(target.Folder));
                    }
                    existedBefore = Directory.Exists(skinDir) || File.Exists(skinDir);

                    // 3. 覆盖前先把旧涂装子目录整份复制到 backups（保留 template\涂装 两级结构）。
                    if (existedBefore)
                    {
                        if (File.Exists(skinDir))
                        {
                            backupPath = BackupSkinFile(skinDir, target.Folder, target.SkinName);
                            if (backupPath == null)
                            {
                                result.Failed.Add(title + "：同名位置是个文件，备份失败，未做任何改动");
                                continue;
                            }
                            modified = true;
                            File.SetAttributes(skinDir, FileAttributes.Normal);
                            File.Delete(Util.LongPath(skinDir));
                        }
                        else
                        {
                            backupPath = BackupSkinCopy(skinDir, target.Folder, target.SkinName, ref done, total, progress);
                            if (backupPath == null)
                            {
                                result.Failed.Add(title + "：备份旧涂装失败，未做任何改动");
                                continue;
                            }
                        }
                    }

                    // 4. 清空/写入只作用于第二级涂装目录；template 目录里的旧结构文件保持不动。
                    Directory.CreateDirectory(Util.LongPath(skinDir));
                    modified = true;
                    ClearFolder(skinDir);
                    HashSet<string> expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool completed = CopyFiles(target, expected, ref done, total, progress, cancelled);
                    if (!completed)
                    {
                        string cancelNote = RollbackSkin(skinDir, backupPath, existedBefore, modified, ref done, total, progress);
                        result.Failed.Add(title + "：已取消（" + cancelNote + "）");
                        continue;
                    }

                    // 5. 清掉这个涂装子目录里不属于本套涂装的残留（.blk 保留）。
                    RemoveExtras(skinDir, expected);

                    result.Done.Add(title + " → " + Path.GetFileName(target.Folder) + "\\" + target.SkinName);
                    result.DoneTargets.Add(target);
                }
                catch (Exception ex)
                {
                    string note = RollbackSkin(skinDir, backupPath, existedBefore, modified, ref done, total, progress);
                    result.Failed.Add(title + "：" + ex.Message + "（" + note + "）");
                }
            }

            TrimBackups();
            result.Bytes = done;
            return result;
        }

        /// <summary>
        /// 把目标解析成实际写入的第二级涂装目录；遇到同名时按 policy 处理：
        /// 改名 → 换成不冲突的名字（会更新 target.SkinName），覆盖 → 原样返回，跳过 → 返回 null 并给出原因。
        /// </summary>
        private static string ResolveTargetDir(ImportTarget target, SkinConflictPolicy policy, out string error)
        {
            error = "";
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string nameError = SkinNameRules.Validate(target.SkinName);
                if (nameError.Length > 0)
                {
                    error = "涂装文件夹名无效：" + nameError;
                    return null;
                }
                string dir;
                if (!TryTargetDir(target, out dir))
                {
                    error = "涂装文件夹名不安全（不允许 ..、绝对路径或跳出模板文件夹）";
                    return null;
                }
                bool exists = Directory.Exists(dir) || File.Exists(dir);
                if (!exists)
                {
                    return dir;
                }
                if (policy == SkinConflictPolicy.Skip)
                {
                    error = "已跳过（同名涂装文件夹已存在，未做任何改动）";
                    return null;
                }
                if (policy == SkinConflictPolicy.Overwrite)
                {
                    return dir;
                }
                string unique = UniqueSkinName(target.Folder, target.SkinName, null);
                if (string.Equals(unique, target.SkinName, StringComparison.Ordinal))
                {
                    error = "无法生成不冲突的涂装文件夹名";
                    return null;
                }
                target.SkinName = unique;
            }
            error = "无法生成不冲突的涂装文件夹名";
            return null;
        }

        /// <summary>
        /// 安全地把「模板文件夹 + 涂装文件夹名」拼成写入目录：
        /// 名称必须通过 SkinNameRules，结果必须在 template 目录内且只有一层。
        /// </summary>
        public static bool TryTargetDir(ImportTarget target, out string dir)
        {
            dir = null;
            if (target == null || string.IsNullOrEmpty(target.Folder))
            {
                return false;
            }
            string name = target.SkinName;
            if (SkinNameRules.Validate(name).Length > 0)
            {
                return false;
            }
            try
            {
                char[] trim = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                string templateFull = Path.GetFullPath(target.Folder).TrimEnd(trim);
                string skins = Config.UserSkinsDir;
                if (!string.IsNullOrEmpty(skins))
                {
                    string root = Path.GetFullPath(skins).TrimEnd(trim) + Path.DirectorySeparatorChar;
                    if (!(templateFull + Path.DirectorySeparatorChar).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                string combined = Path.GetFullPath(Path.Combine(templateFull, name));
                string prefix = templateFull + Path.DirectorySeparatorChar;
                if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                string parent = Path.GetDirectoryName(combined);
                if (parent == null || !string.Equals(parent.TrimEnd(trim), templateFull, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (string.Equals(combined.TrimEnd(trim), templateFull, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                dir = combined;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>在 template 目录里取一个不冲突的涂装文件夹名（如「涂装 (2)」）。</summary>
        public static string UniqueSkinName(string templateFolder, string desired, ICollection<string> reservedNames)
        {
            string baseName = desired != null ? desired.Trim() : "";
            if (baseName.Length == 0)
            {
                baseName = "涂装";
            }
            baseName = TruncateSkinName(baseName, SkinNameRules.MaxLength);
            if (!NameTaken(templateFolder, baseName, reservedNames))
            {
                return baseName;
            }
            for (int i = 2; i < 1000; i++)
            {
                string suffix = " (" + i.ToString(CultureInfo.InvariantCulture) + ")";
                string candidate = TruncateSkinName(baseName, SkinNameRules.MaxLength - suffix.Length) + suffix;
                if (!NameTaken(templateFolder, candidate, reservedNames))
                {
                    return candidate;
                }
            }
            return "涂装_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        private static string TruncateSkinName(string name, int max)
        {
            if (max < 1)
            {
                max = 1;
            }
            if (name.Length <= max)
            {
                return name;
            }
            string cut = name.Substring(0, max).TrimEnd(' ', '.');
            return cut.Length == 0 ? name.Substring(0, 1) : cut;
        }

        private static bool NameTaken(string templateFolder, string name, ICollection<string> reservedNames)
        {
            if (reservedNames != null)
            {
                foreach (string reserved in reservedNames)
                {
                    if (string.Equals(reserved, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            try
            {
                string path = Path.Combine(templateFolder, name);
                return Directory.Exists(path) || File.Exists(path);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>覆盖前备份旧涂装目录：backups\template_&lt;载具ID&gt;\&lt;涂装名&gt;_yyyyMMdd_HHmmss。</summary>
        private static string BackupSkinCopy(string skinDir, string templateFolder, string skinName,
                                             ref long done, long total, Action<long, long, string> progress)
        {
            if (progress != null)
            {
                progress(done, total, "备份 " + skinName);
            }
            string dest = null;
            try
            {
                dest = MakeBackupDir(templateFolder, skinName);
                if (dest == null)
                {
                    return null;
                }
                CopyDirectory(skinDir, dest, ref done, total, progress);
                return dest;
            }
            catch
            {
                if (dest != null)
                {
                    Util.TryDeleteDirectory(dest);
                }
                return null;
            }
        }

        /// <summary>同名位置是个文件时的备份：把文件放进备份目录。</summary>
        private static string BackupSkinFile(string filePath, string templateFolder, string skinName)
        {
            string dest = null;
            try
            {
                dest = MakeBackupDir(templateFolder, skinName);
                if (dest == null)
                {
                    return null;
                }
                Directory.CreateDirectory(Util.LongPath(dest));
                File.Copy(Util.LongPath(filePath), Util.LongPath(Path.Combine(dest, Path.GetFileName(filePath))), true);
                return dest;
            }
            catch
            {
                if (dest != null)
                {
                    Util.TryDeleteDirectory(dest);
                }
                return null;
            }
        }

        private static string MakeBackupDir(string templateFolder, string skinName)
        {
            string root = Config.BackupDir;
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(Util.LongPath(root));
            }
            string templateName = Path.GetFileName(templateFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string group = Path.Combine(root, Util.SafeFileName(templateName));
            if (!Directory.Exists(group))
            {
                Directory.CreateDirectory(Util.LongPath(group));
            }
            string cleanName = Util.SafeFileName((skinName != null ? skinName : "").TrimEnd(' ', '.'));
            if (cleanName.Length == 0)
            {
                cleanName = "涂装";
            }
            string name = cleanName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string dest = Path.Combine(group, name);
            int n = 2;
            while (Directory.Exists(dest) || File.Exists(dest))
            {
                dest = Path.Combine(group, name + "_" + n.ToString(CultureInfo.InvariantCulture));
                n++;
                if (n > 1000)
                {
                    return null;
                }
            }
            return dest;
        }

        /// <summary>取消/失败时只回滚当前涂装子目录，不碰 template 目录和同级其他涂装。</summary>
        private static string RollbackSkin(string skinDir, string backupPath, bool existedBefore, bool modified,
                                           ref long done, long total, Action<long, long, string> progress)
        {
            if (!modified || string.IsNullOrEmpty(skinDir))
            {
                return "未改动任何文件";
            }
            if (!string.IsNullOrEmpty(backupPath))
            {
                if (RestoreBackup(backupPath, skinDir, ref done, total, progress))
                {
                    return "已从备份恢复原涂装";
                }
                return "恢复失败，请到 backups 里手动找回旧涂装";
            }
            if (!existedBefore)
            {
                Util.TryDeleteDirectory(skinDir);
                return "已删除本次新建的涂装子文件夹";
            }
            return "原有涂装已被清空且没有备份，请手动恢复";
        }

        private static void CopyDirectory(string from, string to, ref long done, long total,
                                          Action<long, long, string> progress)
        {
            Directory.CreateDirectory(Util.LongPath(to));
            string[] files = Directory.GetFiles(from, "*", SearchOption.AllDirectories);
            string basePath = from.TrimEnd('\\');
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i].Substring(basePath.Length).TrimStart('\\');
                string dest = Path.Combine(to, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(Util.LongPath(destDir));
                }
                File.Copy(Util.LongPath(files[i]), Util.LongPath(dest), true);
                try
                {
                    done += new FileInfo(files[i]).Length;
                }
                catch
                {
                }
                if (progress != null)
                {
                    progress(done, total, "备份 " + Path.GetFileName(files[i]));
                }
            }
        }

        /// <summary>删除不属于本次涂装的文件（官方模板残留、旧贴图）。</summary>
        private static void RemoveExtras(string folder, HashSet<string> expected)
        {
            try
            {
                string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    if (!expected.Contains(files[i]) &&
                        !files[i].EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.SetAttributes(files[i], FileAttributes.Normal);
                            File.Delete(files[i]);
                        }
                        catch
                        {
                        }
                    }
                }
                string[] dirs = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories);
                Array.Sort(dirs);
                for (int i = dirs.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(dirs[i]).Length == 0)
                        {
                            Directory.Delete(dirs[i]);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static bool CopyFiles(ImportTarget target, HashSet<string> expected, ref long done, long total,
                                      Action<long, long, string> progress, Func<bool> cancelled)
        {
            SkinPackage package = target.Package;
            if (package.IsZip)
            {
                using (ZipArchive zip = ZipFile.OpenRead(package.SourcePath))
                {
                    for (int i = 0; i < package.Files.Count; i++)
                    {
                        if (cancelled != null && cancelled())
                        {
                            return false;
                        }
                        PackageEntry pe = package.Files[i];
                        if (ImportValidation.IsWarn(pe.Rel) || IsForeignBlk(pe.Rel, target))
                        {
                            continue;
                        }
                        string rel = DestinationRel(pe.Rel, target);
                        string dest = Util.SafeCombine(target.TargetDir, rel);
                        string destDir = Path.GetDirectoryName(dest);
                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(Util.LongPath(destDir));
                        }
                        expected.Add(Path.GetFullPath(dest));
                        ZipArchiveEntry entry = zip.Entries[pe.ZipIndex];
                        using (Stream input = entry.Open())
                        using (FileStream output = OpenWriteWithRetry(dest))
                        {
                            CopyStream(input, output, pe.Length, target, ref done, total, progress);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < package.Files.Count; i++)
                {
                    if (cancelled != null && cancelled())
                    {
                        return false;
                    }
                    PackageEntry pe = package.Files[i];
                    if (ImportValidation.IsWarn(pe.Rel) || IsForeignBlk(pe.Rel, target))
                    {
                        continue;
                    }
                    string rel = DestinationRel(pe.Rel, target);
                    string dest = Util.SafeCombine(target.TargetDir, rel);
                    string destDir = Path.GetDirectoryName(dest);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(Util.LongPath(destDir));
                    }
                    expected.Add(Path.GetFullPath(dest));
                    CopyFileWithRetry(pe.FullPath, dest);
                    done += pe.Length;
                    if (progress != null)
                    {
                        progress(done, total, rel);
                    }
                }
            }
            return true;
        }

        private static FileStream OpenWriteWithRetry(string path)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return new FileStream(Util.LongPath(path), FileMode.Create, FileAccess.Write, FileShare.None);
                }
                catch (UnauthorizedAccessException)
                {
                    attempt++;
                    if (attempt >= 4)
                    {
                        throw;
                    }
                    System.Threading.Thread.Sleep(180 * attempt);
                }
            }
        }

        private static void CopyFileWithRetry(string from, string to)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    File.Copy(Util.LongPath(from), Util.LongPath(to), true);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    attempt++;
                    if (attempt >= 4)
                    {
                        throw;
                    }
                    System.Threading.Thread.Sleep(180 * attempt);
                }
            }
        }

        /// <summary>多型号合集包里，别的型号的 .blk 不往这个文件夹里放。</summary>
        private static bool IsForeignBlk(string rel, ImportTarget target)
        {
            if (!rel.EndsWith(".blk", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (string.Equals(rel, target.Unit.BlkRel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string name = Path.GetFileNameWithoutExtension(rel);
            return !string.Equals(name, target.Unit.UnitId, StringComparison.OrdinalIgnoreCase);
        }

        private static string DestinationRel(string rel, ImportTarget target)
        {
            // .blk 必须和载具 ID 同名，否则游戏不会读取。
            if (string.Equals(rel, target.Unit.BlkRel, StringComparison.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(rel);
                string desired = target.Unit.UnitId + ".blk";
                if (!string.Equals(fileName, desired, StringComparison.OrdinalIgnoreCase))
                {
                    string dir = rel.Substring(0, rel.Length - fileName.Length);
                    return dir + desired;
                }
            }
            return rel;
        }

        private static void CopyStream(Stream input, FileStream output, long expected,
                                       ImportTarget target, ref long done, long total,
                                       Action<long, long, string> progress)
        {
            byte[] buffer = new byte[1024 * 256];
            long fileDone = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                fileDone += read;
                done += read;
                if (fileDone > SkinPackage.MaxSingleBytes || done > SkinPackage.MaxTotalBytes)
                {
                    throw new InvalidDataException("解压内容超过安全上限");
                }
                if (progress != null)
                {
                    progress(done, total, target.Unit.Title);
                }
            }
            if (fileDone < expected)
            {
                done += expected - fileDone;
            }
        }

        /// <summary>清空载具文件夹内的全部文件与子目录（保留文件夹本身）。</summary>
        private static void ClearFolder(string folder)
        {
            string root = Path.GetFullPath(Config.UserSkinsDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(folder);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("拒绝清空 UserSkins 之外的目录：" + full);
            }
            foreach (string entry in Directory.GetFileSystemEntries(full))
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(Util.LongPath(entry), true);
                    }
                    else
                    {
                        File.SetAttributes(entry, FileAttributes.Normal);
                        File.Delete(Util.LongPath(entry));
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException("无法清空目标文件夹：" + Path.GetFileName(entry) + "（" + ex.Message + "）", ex);
                }
            }
        }

        private static bool RestoreBackup(string backupPath, string folder, ref long done, long total,
                                          Action<long, long, string> progress)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
            {
                return false;
            }
            string temp = folder.TrimEnd('\\') + ".restore_" + Guid.NewGuid().ToString("N");
            try
            {
                // 先复制到同级的临时目录，成功后再替换，避免恢复途中失败把原目录清空。
                CopyDirectory(backupPath, temp, ref done, total, progress);
                Util.TryDeleteDirectory(folder);
                Directory.Move(temp, folder);
                return true;
            }
            catch
            {
                Util.TryDeleteDirectory(temp);
                return false;
            }
        }

        private static void TrimBackups()
        {
            try
            {
                string root = Config.BackupDir;
                if (!Directory.Exists(root))
                {
                    return;
                }
                List<string> dirs = new List<string>();
                List<string> groups = new List<string>();
                foreach (string dir in Directory.GetDirectories(root))
                {
                    // 手动备份在 backups\skins，必须与自动备份隔离，绝不参与自动清理。
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, "skins", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    // 旧布局：backups\<folder>_yyyyMMdd_HHmmss[_n]。
                    if (Regex.IsMatch(name, @"_\d{8}_\d{6}(_\d+)?$"))
                    {
                        dirs.Add(dir);
                        continue;
                    }
                    // 新布局：backups\template_<载具ID>\<涂装名>_yyyyMMdd_HHmmss[_n]。
                    if (!name.StartsWith(TemplateFolders.Prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    groups.Add(dir);
                    try
                    {
                        foreach (string sub in Directory.GetDirectories(dir))
                        {
                            if (Regex.IsMatch(Path.GetFileName(sub), @"_\d{8}_\d{6}(_\d+)?$"))
                            {
                                dirs.Add(sub);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                if (dirs.Count <= Config.KeepBackups)
                {
                    return;
                }
                dirs.Sort(delegate(string a, string b)
                {
                    DateTime ta = Directory.GetLastWriteTimeUtc(a);
                    DateTime tb = Directory.GetLastWriteTimeUtc(b);
                    return tb.CompareTo(ta);
                });
                for (int i = Config.KeepBackups; i < dirs.Count; i++)
                {
                    Util.TryDeleteDirectory(dirs[i]);
                }
                // 只清理我们新建的空分组目录，避免留下空壳。
                for (int i = 0; i < groups.Count; i++)
                {
                    try
                    {
                        if (Directory.Exists(groups[i]) && Directory.GetFileSystemEntries(groups[i]).Length == 0)
                        {
                            Directory.Delete(groups[i]);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
