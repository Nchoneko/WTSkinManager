using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace WtSkin
{
    /// <summary>
    /// 语义化版本：1~4 段数字 + 可选预发布标记。逐段按数值比较，禁止字符串比较；
    /// 预发布版本低于同号正式版（3.7.0-beta &lt; 3.7.0）。
    /// </summary>
    internal sealed class SemVer : IComparable<SemVer>, IEquatable<SemVer>
    {
        public int Major;
        public int Minor;
        public int Patch;
        public int Revision;
        public string Pre = "";
        public string Raw = "";

        public bool IsUnknown
        {
            get { return Raw.Length == 0; }
        }

        public static SemVer Unknown()
        {
            return new SemVer();
        }

        public static bool TryParse(string text, out SemVer version)
        {
            version = null;
            if (text == null)
            {
                return false;
            }
            string s = text.Trim();
            if (s.Length == 0)
            {
                return false;
            }
            if (s[0] == 'v' || s[0] == 'V')
            {
                s = s.Substring(1).Trim();
            }
            if (s.Length == 0)
            {
                return false;
            }

            string pre = "";
            int dash = s.IndexOf('-');
            if (dash > 0)
            {
                pre = s.Substring(dash + 1).Trim();
                s = s.Substring(0, dash).Trim();
            }
            else
            {
                int plus = s.IndexOf('+');
                if (plus > 0)
                {
                    s = s.Substring(0, plus).Trim();
                }
            }
            // 4 段之后的附加段（例如 3.7.0.0.1）忽略，只取前四段。
            string[] parts = s.Split('.');
            if (parts.Length == 0)
            {
                return false;
            }
            int[] numbers = new int[4];
            int used = Math.Min(parts.Length, 4);
            for (int i = 0; i < used; i++)
            {
                string part = parts[i].Trim();
                int value;
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
                {
                    return false;
                }
                numbers[i] = value;
            }
            SemVer result = new SemVer();
            result.Major = numbers[0];
            result.Minor = numbers[1];
            result.Patch = numbers[2];
            result.Revision = numbers[3];
            result.Pre = pre;
            result.Raw = text.Trim();
            version = result;
            return true;
        }

        public static SemVer ParseOrUnknown(string text)
        {
            SemVer value;
            return TryParse(text, out value) ? value : Unknown();
        }

        public int CompareTo(SemVer other)
        {
            if (other == null)
            {
                return 1;
            }
            // 未知版本不参与大小判断，比任何已知版本都小。
            if (IsUnknown && other.IsUnknown)
            {
                return 0;
            }
            if (IsUnknown)
            {
                return -1;
            }
            if (other.IsUnknown)
            {
                return 1;
            }
            if (Major != other.Major)
            {
                return Major < other.Major ? -1 : 1;
            }
            if (Minor != other.Minor)
            {
                return Minor < other.Minor ? -1 : 1;
            }
            if (Patch != other.Patch)
            {
                return Patch < other.Patch ? -1 : 1;
            }
            if (Revision != other.Revision)
            {
                return Revision < other.Revision ? -1 : 1;
            }
            bool leftPre = Pre.Length > 0;
            bool rightPre = other.Pre.Length > 0;
            if (!leftPre && !rightPre)
            {
                return 0;
            }
            if (!leftPre)
            {
                return 1;
            }
            if (!rightPre)
            {
                return -1;
            }
            return ComparePre(Pre, other.Pre);
        }

        /// <summary>预发布标记逐段比较：数值段按数值，字母段按序号，数字段低于字母段。</summary>
        private static int ComparePre(string a, string b)
        {
            string[] left = a.Split('.');
            string[] right = b.Split('.');
            int count = Math.Max(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                if (i >= left.Length)
                {
                    return -1;
                }
                if (i >= right.Length)
                {
                    return 1;
                }
                string x = left[i];
                string y = right[i];
                int xn;
                int yn;
                bool xNum = int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out xn);
                bool yNum = int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out yn);
                if (xNum && yNum)
                {
                    if (xn != yn)
                    {
                        return xn < yn ? -1 : 1;
                    }
                    continue;
                }
                if (xNum != yNum)
                {
                    return xNum ? -1 : 1;
                }
                int cmp = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp < 0 ? -1 : 1;
                }
            }
            return 0;
        }

        public bool IsSame(SemVer other)
        {
            return other != null && CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return IsSame(obj as SemVer);
        }

        public override int GetHashCode()
        {
            return (Major * 1000003 + Minor * 10007 + Patch * 101 + Revision) ^ Pre.ToLowerInvariant().GetHashCode();
        }

        public bool Equals(SemVer other)
        {
            return IsSame(other);
        }

        /// <summary>界面展示用：3.7.0；预发布标出 3.7.0-beta，第四段非零时补上。</summary>
        public string ToDisplay()
        {
            if (IsUnknown)
            {
                return "未知版本";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
            if (Revision > 0)
            {
                sb.Append('.').Append(Revision);
            }
            if (Pre.Length > 0)
            {
                sb.Append('-').Append(Pre);
            }
            return sb.ToString();
        }

        public string Display
        {
            get { return "v" + ToDisplay(); }
        }

        public override string ToString()
        {
            return ToDisplay();
        }
    }

    /// <summary>程序自身版本与随包数据版本（两者分开记录，互不替代）。</summary>
    internal static class AppInfo
    {
        private static SemVer _appVersion;
        private static bool _appLoaded;

        public static SemVer AppVersion
        {
            get
            {
                if (!_appLoaded)
                {
                    _appLoaded = true;
                    _appVersion = SemVer.Unknown();
                    try
                    {
                        Version v = Assembly.GetExecutingAssembly().GetName().Version;
                        SemVer parsed;
                        if (v != null && SemVer.TryParse(v.ToString(), out parsed))
                        {
                            _appVersion = parsed;
                        }
                    }
                    catch
                    {
                    }
                }
                return _appVersion;
            }
        }

        public static string VersionText
        {
            get { return AppVersion.ToDisplay(); }
        }

        public static string DisplayVersion
        {
            get { return "v" + VersionText; }
        }

        /// <summary>随包数据版本，来自嵌入的 data-manifest.json。</summary>
        public static string DataVersion
        {
            get { return DataFiles.DataVersion; }
        }

        public static string DataVersionText
        {
            get
            {
                string v = DataVersion;
                return string.IsNullOrEmpty(v) ? "未知" : "v" + v;
            }
        }
    }
}
