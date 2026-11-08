using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WtSkin
{
    internal class ImportTarget
    {
        public SkinPackage Package;
        public DetectedUnit Unit;
        public string Folder;
    }

    internal class ImportPlan
    {
        public List<ImportTarget> Targets = new List<ImportTarget>();
        public List<DetectedUnit> Missing = new List<DetectedUnit>();
        public List<string> Warnings = new List<string>();
        public long TotalBytes;
    }

    internal class ImportResult
    {
        public List<string> Done = new List<string>();
        public List<string> Failed = new List<string>();
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
                    target.Folder = folder;
                    plan.Targets.Add(target);

                    if (Path.GetFileName(folder).StartsWith("template_", StringComparison.OrdinalIgnoreCase))
                    {
                        warn("「" + Path.GetFileName(folder) + "」是游戏生成的官方模板文件夹，内容会被清空。");
                    }
                    if (unit.Confidence < 100)
                    {
                        warn("「" + unit.Title + "」是按文件名推测的（" + unit.UnitId + "），请确认。");
                    }
                }
            }

            HashSet<string> counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plan.Targets.Count; i++)
            {
                ImportTarget t = plan.Targets[i];
                if (counted.Add(t.Folder))
                {
                    plan.TotalBytes += t.Package.TotalBytes;
                    if (Config.Backup)
                    {
                        plan.TotalBytes += Util.DirectorySize(t.Folder);
                    }
                }
            }
            return plan;
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
                if (!processed.Add(target.Folder))
                {
                    continue;
                }
                string backupPath = null;
                try
                {
                    // 1. 备份：复制旧内容到安装目录（不移动、不删除目标文件夹本身，
                    //    避免 Windows 的“删除挂起”状态让后续写入报拒绝访问）。
                    if (Config.Backup && Directory.Exists(target.Folder))
                    {
                        backupPath = BackupCopy(target.Folder, ref done, total, progress);
                    }

                    // 2. 原地覆盖写入新涂装。
                    Directory.CreateDirectory(Util.LongPath(target.Folder));
                    HashSet<string> expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool completed = CopyFiles(target, expected, ref done, total, progress, cancelled);
                    if (!completed)
                    {
                        // 取消：不清理旧文件、不计入成功；尽量用本次备份恢复原状。
                        RestoreBackup(backupPath, target.Folder, ref done, total, progress);
                        result.Failed.Add(target.Unit.Title + "：已取消");
                        continue;
                    }

                    // 3. 清掉不属于这套涂装的官方残留文件。
                    RemoveExtras(target.Folder, expected);

                    result.Done.Add(target.Unit.Title + " → " + Path.GetFileName(target.Folder));
                }
                catch (Exception ex)
                {
                    RestoreBackup(backupPath, target.Folder, ref done, total, progress);
                    result.Failed.Add(target.Unit.Title + "：" + ex.Message);
                }
            }

            TrimBackups();
            result.Bytes = done;
            return result;
        }

        private static string BackupCopy(string folder, ref long done, long total, Action<long, long, string> progress)
        {
            if (progress != null)
            {
                progress(done, total, "备份 " + Path.GetFileName(folder));
            }
            string backupRoot = Config.BackupDir;
            if (!Directory.Exists(backupRoot))
            {
                Directory.CreateDirectory(backupRoot);
            }
            string name = Path.GetFileName(folder) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string dest = Path.Combine(backupRoot, Util.SafeFileName(name));
            int n = 2;
            while (Directory.Exists(dest))
            {
                dest = Path.Combine(backupRoot, Util.SafeFileName(name + "_" + n.ToString(CultureInfo.InvariantCulture)));
                n++;
            }
            CopyDirectory(folder, dest, ref done, total, progress);
            return dest;
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
                        string dest = Util.SafeCombine(target.Folder, rel);
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
                    string dest = Util.SafeCombine(target.Folder, rel);
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

        private static void RestoreBackup(string backupPath, string folder, ref long done, long total,
                                          Action<long, long, string> progress)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
            {
                return;
            }
            try
            {
                Util.TryDeleteDirectory(folder);
                CopyDirectory(backupPath, folder, ref done, total, progress);
            }
            catch
            {
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
                foreach (string dir in Directory.GetDirectories(root))
                {
                    // 手动备份在 backups\skins，必须与自动备份隔离，绝不参与自动清理。
                    if (string.Equals(Path.GetFileName(dir), "skins", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    dirs.Add(dir);
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
            }
            catch
            {
            }
        }
    }
}
