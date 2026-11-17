using System;

namespace WtSkin
{
    /// <summary>极简双语支持：Config.Language=zh/en，界面启动时按当前语言取文案。</summary>
    internal static class Lang
    {
        public static bool English
        {
            get { return string.Equals(Config.Language, "en", StringComparison.OrdinalIgnoreCase); }
        }

        public static string T(string zh, string en)
        {
            return English ? en : zh;
        }

        public static void Toggle()
        {
            Config.Language = English ? "zh" : "en";
            Config.Save();
        }
    }
}
