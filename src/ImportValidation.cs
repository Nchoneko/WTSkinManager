using System;
using System.Collections.Generic;
using System.IO;

namespace WtSkin
{
    internal class ValidationResult
    {
        public List<string> Allowed = new List<string>();
        public List<string> Warn = new List<string>();
        public List<string> Illegal = new List<string>();

        public bool HasIllegal
        {
            get { return Illegal.Count > 0; }
        }
    }

    /// <summary>按扩展名把压缩包条目分成 合法 / 警告 / 非法 三档（名单来自设置）。</summary>
    internal static class ImportValidation
    {
        public static ValidationResult Classify(SkinPackage package)
        {
            ValidationResult result = new ValidationResult();
            if (package == null)
            {
                return result;
            }
            HashSet<string> allowed = Parse(Config.AllowedExt);
            HashSet<string> warn = Parse(Config.WarnExt);
            for (int i = 0; i < package.Files.Count; i++)
            {
                string rel = package.Files[i].Rel;
                if (string.IsNullOrEmpty(rel) || rel.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }
                string ext = Path.GetExtension(rel).ToLowerInvariant();
                if (allowed.Contains(ext))
                {
                    result.Allowed.Add(rel);
                }
                else if (warn.Contains(ext))
                {
                    result.Warn.Add(rel);
                }
                else
                {
                    result.Illegal.Add(rel);
                }
            }
            return result;
        }

        public static ValidationResult ClassifyAll(IEnumerable<SkinPackage> packages)
        {
            ValidationResult all = new ValidationResult();
            foreach (SkinPackage package in packages)
            {
                ValidationResult one = Classify(package);
                all.Allowed.AddRange(one.Allowed);
                all.Warn.AddRange(one.Warn);
                all.Illegal.AddRange(one.Illegal);
            }
            return all;
        }

        public static bool IsWarn(string rel)
        {
            string ext = Path.GetExtension(rel).ToLowerInvariant();
            return Parse(Config.WarnExt).Contains(ext);
        }

        private static HashSet<string> Parse(string csv)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(csv))
            {
                return set;
            }
            string[] parts = csv.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string ext = parts[i].Trim().ToLowerInvariant();
                if (ext.Length == 0)
                {
                    continue;
                }
                if (!ext.StartsWith("."))
                {
                    ext = "." + ext;
                }
                set.Add(ext);
            }
            return set;
        }
    }
}
