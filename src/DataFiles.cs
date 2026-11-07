using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>版本清单里的一份随包数据文件。</summary>
    internal class DataFileEntry
    {
        public string File = "";
        public int SchemaVersion;
        public string DataVersion = "";
        public int Entries;
    }

    internal class DataCheck
    {
        public readonly List<string> Lines = new List<string>();
        public bool SchemaMismatch;
        public string SchemaMessage = "";
        public bool DataOlder;
        public string DataOlderMessage = "";
        public bool ExternalIgnored;
        public string Warning = "";
        public bool HasProblem
        {
            get { return SchemaMismatch || DataOlder; }
        }
    }

    /// <summary>
    /// 随包数据的版本清单与启动校验。清单由 build.ps1 生成并内嵌进 exe，
    /// 记录文件名 / schemaVersion / dataVersion / 条目数 / 生成时间。
    /// 程序版本与数据版本分开记录，互不替代。
    /// </summary>
    internal static class DataFiles
    {
        public const int SupportedSchemaVersion = 1;

        private static bool _loaded;
        private static readonly List<DataFileEntry> _files = new List<DataFileEntry>();
        private static int _schemaVersion;
        private static string _dataVersion = "";
        private static string _generated = "";
        private static string _loadError = "";
        private static bool _reported;

        public static int SchemaVersion
        {
            get { EnsureLoaded(); return _schemaVersion; }
        }

        public static string DataVersion
        {
            get { EnsureLoaded(); return _dataVersion; }
        }

        public static string Generated
        {
            get { EnsureLoaded(); return _generated; }
        }

        public static string LoadError
        {
            get { EnsureLoaded(); return _loadError; }
        }

        public static List<DataFileEntry> Files
        {
            get { EnsureLoaded(); return new List<DataFileEntry>(_files); }
        }

        /// <summary>启动时是否已确认外部数据不可用（由 Check 写入，供清单加载参考）。</summary>
        public static bool ExternalManifestIgnored;

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                using (Stream stream = typeof(DataFiles).Assembly.GetManifestResourceStream("data-manifest.json"))
                {
                    if (stream == null)
                    {
                        _loadError = "缺少内嵌 data-manifest.json";
                        return;
                    }
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        object root = Json.Parse(reader.ReadToEnd());
                        Dictionary<string, object> obj = root as Dictionary<string, object>;
                        if (obj == null)
                        {
                            _loadError = "data-manifest.json 格式不正确";
                            return;
                        }
                        _schemaVersion = (int)Json.Num(obj, "schemaVersion");
                        _dataVersion = Json.Str(obj, "dataVersion");
                        _generated = Json.Str(obj, "generated");
                        List<object> rows = Json.Arr(obj, "files");
                        for (int i = 0; i < rows.Count; i++)
                        {
                            Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
                            if (row == null)
                            {
                                continue;
                            }
                            DataFileEntry entry = new DataFileEntry();
                            entry.File = Json.Str(row, "file");
                            entry.SchemaVersion = (int)Json.Num(row, "schemaVersion");
                            entry.DataVersion = Json.Str(row, "dataVersion");
                            entry.Entries = (int)Json.Num(row, "entries");
                            if (entry.File.Length > 0)
                            {
                                _files.Add(entry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
            }
        }

        public static string Summary()
        {
            EnsureLoaded();
            StringBuilder sb = new StringBuilder();
            sb.Append("数据版本 ").Append(string.IsNullOrEmpty(_dataVersion) ? "未知" : "v" + _dataVersion);
            sb.Append("（schema ").Append(_schemaVersion).Append("，").Append(_files.Count).Append(" 份文件");
            if (_generated.Length > 0)
            {
                sb.Append("，生成于 ").Append(_generated);
            }
            sb.Append("）");
            return sb.ToString();
        }

        /// <summary>校验内置清单的 schemaVersion，并检查安装目录里遗留的外部数据。</summary>
        public static DataCheck Check()
        {
            DataCheck check = new DataCheck();
            EnsureLoaded();
            check.Lines.Add("数据校验：" + Summary());
            if (_loadError.Length > 0)
            {
                check.SchemaMismatch = true;
                check.SchemaMessage = "读取内置数据版本清单失败：" + _loadError + "。程序会继续启动，但不会自动覆盖任何数据。";
            }
            else if (_schemaVersion != SupportedSchemaVersion)
            {
                check.SchemaMismatch = true;
                check.SchemaMessage = "内置数据 schemaVersion=" + _schemaVersion +
                                      "，程序支持 " + SupportedSchemaVersion +
                                      "。程序不会静默按旧格式读取，请改用与此版本配套的数据。";
            }
            else if (_dataVersion.Length == 0)
            {
                check.SchemaMismatch = true;
                check.SchemaMessage = "内置数据缺少 dataVersion，无法确认数据版本。程序会继续启动，但不会自动覆盖任何数据。";
            }
            else
            {
                check.Lines.Add("数据校验通过：schemaVersion=" + _schemaVersion + " 与程序一致");
            }

            // 安装目录里遗留的外部图标清单（旧版非单文件安装会留下 assets\\vehicles）。
            try
            {
                string ext = Path.Combine(Config.InstallDir, "assets", "vehicles", "manifest.json");
                if (!string.IsNullOrEmpty(Config.InstallDir) && File.Exists(ext))
                {
                    string text = File.ReadAllText(ext, Encoding.UTF8);
                    Dictionary<string, object> obj = Json.Parse(text) as Dictionary<string, object>;
                    if (obj == null)
                    {
                        check.ExternalIgnored = true;
                        check.Lines.Add("外部数据：manifest.json 格式不正确，已改用内置数据");
                    }
                    else
                    {
                        int extSchema = (int)Json.Num(obj, "schemaVersion");
                        string extData = Json.Str(obj, "dataVersion");
                        string label = extSchema <= 0 ? "缺少 schemaVersion" : "schemaVersion=" + extSchema;
                        if (extSchema != SupportedSchemaVersion)
                        {
                            ExternalManifestIgnored = true;
                            check.ExternalIgnored = true;
                            check.Lines.Add("外部数据：manifest.json " + label + " 与程序不一致，已忽略并改用内置数据");
                            if (check.Warning.Length == 0)
                            {
                                check.Warning = "安装目录里的图标清单版本与程序不一致（" + label +
                                                "），本次已忽略该文件并改用内置数据。文件不会被自动覆盖，可手动删除 " + ext + "。";
                            }
                        }
                        else if (extData.Length > 0 && _dataVersion.Length > 0)
                        {
                            SemVer extVer;
                            SemVer builtinVer;
                            if (SemVer.TryParse(extData, out extVer) && SemVer.TryParse(_dataVersion, out builtinVer) &&
                                extVer.CompareTo(builtinVer) < 0)
                            {
                                check.DataOlder = true;
                                check.Lines.Add("外部数据：v" + extData + " 落后于随包 v" + _dataVersion + "，提示可更新，不自动覆盖");
                                check.DataOlderMessage = "安装目录里的图标数据是 v" + extData +
                                                         "，比随包数据 v" + _dataVersion + " 旧。可以在「设置」里查看程序版本与数据版本；程序不会自动覆盖这份数据。";
                            }
                            else
                            {
                                check.Lines.Add("外部数据：manifest.json schemaVersion=" + extSchema + "，dataVersion=" + extData);
                            }
                        }
                        else
                        {
                            check.Lines.Add("外部数据：manifest.json schemaVersion=" + extSchema + "，未记录 dataVersion");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                check.Lines.Add("外部数据检查失败：" + ex.Message);
            }

            string overrideWarning = UserOverrides.Warning;
            if (overrideWarning.Length > 0)
            {
                check.Lines.Add("user-overrides.json：" + overrideWarning);
                if (check.Warning.Length == 0)
                {
                    check.Warning = overrideWarning;
                }
            }
            return check;
        }

        /// <summary>启动时执行一次：写日志；schemaVersion 不匹配或数据落后时弹窗提示，绝不静默。</summary>
        public static void CheckAndReport()
        {
            if (_reported)
            {
                return;
            }
            _reported = true;
            DataCheck check = Check();
            for (int i = 0; i < check.Lines.Count; i++)
            {
                Config.Log(check.Lines[i]);
            }
            string message = check.SchemaMessage;
            MessageBoxIcon icon = MessageBoxIcon.Warning;
            if (message.Length == 0 && check.DataOlder)
            {
                message = check.DataOlderMessage;
                icon = MessageBoxIcon.Information;
            }
            if (message.Length == 0 && check.Warning.Length > 0)
            {
                message = check.Warning;
            }
            if (message.Length > 0)
            {
                try
                {
                    MessageBox.Show(message, "数据版本提示", MessageBoxButtons.OK, icon);
                }
                catch
                {
                }
            }
        }
    }
}
