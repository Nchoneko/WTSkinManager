using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WtSkin
{
    internal static class Util
    {
        /// <summary>ZIP/文件夹条目是否是可安全拼接的相对路径（拒绝绝对路径、盘符、.. 与空段）。</summary>
        public static bool IsSafeRel(string rel)
        {
            if (string.IsNullOrEmpty(rel))
            {
                return false;
            }
            string r = rel.Replace('\\', '/');
            if (r.StartsWith("/", StringComparison.Ordinal) || r.Contains(":"))
            {
                return false;
            }
            string[] parts = r.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || part == "." || part == "..")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>在 root 内拼接相对路径；越界或非法时抛 InvalidDataException。</summary>
        public static string SafeCombine(string root, string rel)
        {
            if (!IsSafeRel(rel))
            {
                throw new InvalidDataException("包含不安全的路径：" + rel);
            }
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            string dest = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("路径越界：" + rel);
            }
            return dest;
        }

        public static string HumanSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            }
            double kb = bytes / 1024.0;
            if (kb < 1024)
            {
                return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            }
            double mb = kb / 1024.0;
            if (mb < 1024)
            {
                return mb.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            }
            return (mb / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }

        public static string Count(long n)
        {
            return n.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>去掉 Windows 不允许的文件夹字符，并限制长度。</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "未命名涂装";
            }
            StringBuilder sb = new StringBuilder(name.Length);
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = true;
                for (int b = 0; b < bad.Length; b++)
                {
                    if (bad[b] == c)
                    {
                        ok = false;
                        break;
                    }
                }
                if (c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|')
                {
                    ok = false;
                }
                if (ok)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }
            string cleaned = sb.ToString();
            while (cleaned.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                cleaned = cleaned.Replace("  ", " ");
            }
            cleaned = cleaned.Trim().TrimEnd('.');
            if (cleaned.Length > 70)
            {
                cleaned = cleaned.Substring(0, 70).TrimEnd();
            }
            if (cleaned.Length == 0)
            {
                cleaned = "未命名涂装";
            }
            return cleaned;
        }

        public static string LongPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return path;
            }
            if (path.Length >= 240)
            {
                if (path.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    return @"\\?\UNC\" + path.Substring(2);
                }
                return @"\\?\" + path;
            }
            return path;
        }

        /// <summary>在 parent 下取一个不冲突的文件夹名。</summary>
        public static string UniqueDir(string parent, string desired)
        {
            string candidate = Path.Combine(parent, desired);
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
            for (int i = 2; i < 1000; i++)
            {
                candidate = Path.Combine(parent, desired + " (" + i.ToString(CultureInfo.InvariantCulture) + ")");
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return Path.Combine(parent, desired + " (" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture) + ")");
        }

        public static string SafeFileName(string name)
        {
            StringBuilder sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = true;
                for (int b = 0; b < bad.Length; b++)
                {
                    if (bad[b] == c)
                    {
                        ok = false;
                        break;
                    }
                }
                sb.Append(ok ? c : '_');
            }
            return sb.ToString();
        }

        public static long DirectorySize(string dir)
        {
            long total = 0;
            try
            {
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        total += new FileInfo(files[i]).Length;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return total;
        }

        public static void TryDeleteDirectory(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // 只读文件会导致删除失败，逐个去掉只读再试一次。
                try
                {
                    string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        File.SetAttributes(files[i], FileAttributes.Normal);
                    }
                    Directory.Delete(dir, true);
                }
                catch
                {
                }
            }
        }

        public static void MoveDirectory(string from, string to)
        {
            string parent = Path.GetDirectoryName(to);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
            Directory.Move(LongPath(from), LongPath(to));
        }
    }
}
