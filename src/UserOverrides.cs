using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WtSkin
{
    /// <summary>
    /// 用户覆盖表：数据目录下的 user-overrides.json。
    /// 运行时先读内置清单（含外部可编辑配置），再用这份文件覆盖同名条目。
    /// 文件缺失按空处理：不创建、不删除、不报错；格式错误只记录提示，绝不打断启动。
    /// </summary>
    internal static class UserOverrides
    {
        public const int SupportedSchemaVersion = 1;

        private static bool _loaded;
        private static bool _found;
        private static string _error = "";
        private static string _dataVersion = "";
        private static readonly Dictionary<string, string> _nations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _short =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, object>> _vehicles =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, object>> _meta =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

        public static string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(Config.InstallDir))
                {
                    return "";
                }
                return Path.Combine(Config.InstallDir, "user-overrides.json");
            }
        }

        public static bool Found
        {
            get { EnsureLoaded(); return _found; }
        }

        public static string DataVersion
        {
            get { EnsureLoaded(); return _dataVersion; }
        }

        public static string Warning
        {
            get { EnsureLoaded(); return _error; }
        }

        public static void Reload()
        {
            _loaded = false;
            _found = false;
            _error = "";
            _dataVersion = "";
            _nations.Clear();
            _short.Clear();
            _vehicles.Clear();
            _meta.Clear();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            string path = FilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            _found = true;
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                object root = Json.Parse(text);
                Dictionary<string, object> obj = root as Dictionary<string, object>;
                if (obj == null)
                {
                    _error = "user-overrides.json 格式不正确，已按空处理（未修改文件）";
                    return;
                }
                int schema = (int)Json.Num(obj, "schemaVersion");
                if (schema > SupportedSchemaVersion)
                {
                    _error = "user-overrides.json 的 schemaVersion=" + schema + " 高于程序支持的 " +
                             SupportedSchemaVersion + "，已忽略该文件（未修改文件）";
                    _nations.Clear();
                    _short.Clear();
                    _vehicles.Clear();
                    _meta.Clear();
                    _found = false;
                    return;
                }
                if (schema > 0 && schema != SupportedSchemaVersion)
                {
                    _error = "user-overrides.json 的 schemaVersion=" + schema + " 与程序支持的 " +
                             SupportedSchemaVersion + " 不一致，已忽略该文件（未修改文件）";
                    _nations.Clear();
                    _short.Clear();
                    _vehicles.Clear();
                    _meta.Clear();
                    _found = false;
                    return;
                }
                _dataVersion = Json.Str(obj, "dataVersion");

                Dictionary<string, object> nations = Json.Get(obj, "nations") as Dictionary<string, object>;
                if (nations != null)
                {
                    foreach (KeyValuePair<string, object> pair in nations)
                    {
                        string value = pair.Value as string;
                        if (pair.Key.Length > 0 && !string.IsNullOrEmpty(value))
                        {
                            _nations[pair.Key] = value;
                        }
                    }
                }

                Dictionary<string, object> shorts = Json.Get(obj, "shortnames") as Dictionary<string, object>;
                if (shorts == null)
                {
                    shorts = Json.Get(obj, "shortNames") as Dictionary<string, object>;
                }
                if (shorts != null)
                {
                    foreach (KeyValuePair<string, object> pair in shorts)
                    {
                        string value = pair.Value as string;
                        if (pair.Key.Length > 0 && !string.IsNullOrEmpty(value))
                        {
                            _short[pair.Key] = value;
                        }
                    }
                }

                Dictionary<string, object> vehicles = Json.Get(obj, "vehicles") as Dictionary<string, object>;
                if (vehicles != null)
                {
                    foreach (KeyValuePair<string, object> pair in vehicles)
                    {
                        Dictionary<string, object> row = pair.Value as Dictionary<string, object>;
                        if (pair.Key.Length > 0 && row != null)
                        {
                            _vehicles[pair.Key] = row;
                        }
                    }
                }

                Dictionary<string, object> meta = Json.Get(obj, "meta") as Dictionary<string, object>;
                if (meta == null)
                {
                    meta = Json.Get(obj, "vehicleMeta") as Dictionary<string, object>;
                }
                if (meta != null)
                {
                    foreach (KeyValuePair<string, object> pair in meta)
                    {
                        Dictionary<string, object> row = pair.Value as Dictionary<string, object>;
                        if (pair.Key.Length > 0 && row != null)
                        {
                            _meta[pair.Key] = row;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _error = "读取 user-overrides.json 失败，已按空处理（未修改文件）：" + ex.Message;
                _nations.Clear();
                _short.Clear();
                _vehicles.Clear();
                _meta.Clear();
            }
        }

        private static string Value(Dictionary<string, object> obj, string key)
        {
            return Json.Str(obj, key);
        }

        private static bool Number(Dictionary<string, object> obj, string key, out double value)
        {
            value = 0;
            object raw;
            if (obj == null || !obj.TryGetValue(key, out raw) || raw == null)
            {
                return false;
            }
            if (raw is double)
            {
                value = (double)raw;
                return true;
            }
            string text = raw as string;
            if (text != null && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
            return false;
        }

        private static int CountOf()
        {
            EnsureLoaded();
            return _nations.Count + _short.Count + _vehicles.Count + _meta.Count;
        }

        public static string Summary()
        {
            EnsureLoaded();
            if (!_found)
            {
                return "user-overrides.json：未找到（按空处理）";
            }
            string text = "user-overrides.json：覆盖 " + CountOf() + " 条（简称 " + _short.Count +
                          "、国家 " + _nations.Count + "、载具 " + _vehicles.Count + "、等级权重 " + _meta.Count + "）";
            if (_dataVersion.Length > 0)
            {
                text += "，声明数据版本 v" + _dataVersion;
            }
            if (_error.Length > 0)
            {
                text += "；" + _error;
            }
            return text;
        }

        /// <summary>把覆盖条目按运行时真实解析结果输出，供诊断与验收确认「覆盖生效」。</summary>
        public static List<string> EffectiveLines()
        {
            EnsureLoaded();
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> pair in _short)
            {
                lines.Add("简称覆盖 " + pair.Key + " → " + NameMap.Short(pair.Key, "", ""));
            }
            foreach (KeyValuePair<string, string> pair in _nations)
            {
                lines.Add("国家覆盖 " + pair.Key + " → " + NameMap.NationName(pair.Key));
            }
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in _vehicles)
            {
                Vehicle vehicle = VehicleDb.Find(pair.Key);
                if (vehicle == null)
                {
                    lines.Add("载具覆盖 " + pair.Key + " → 内置清单中不存在，未应用");
                }
                else
                {
                    lines.Add("载具覆盖 " + pair.Key + " → " + vehicle.Zh + " / " + vehicle.En + " / " + vehicle.Nation);
                }
            }
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in _meta)
            {
                VehicleMetaEntry entry = VehicleMeta.FindBySlug(pair.Key);
                if (entry == null)
                {
                    lines.Add("等级权重覆盖 " + pair.Key + " → 内置清单中不存在，未应用");
                }
                else
                {
                    lines.Add("等级权重覆盖 " + pair.Key + " → " + VehicleMeta.Display(pair.Key) +
                              "（来源 " + entry.Source + "）");
                }
            }
            return lines;
        }

        /// <summary>国家显示名覆盖。</summary>
        public static void ApplyNations(Dictionary<string, string> nations)
        {
            EnsureLoaded();
            if (nations == null)
            {
                return;
            }
            foreach (KeyValuePair<string, string> pair in _nations)
            {
                nations[pair.Key] = pair.Value;
            }
        }

        /// <summary>载具简称覆盖：同时按 ID 与全名生效，和 shortnames.ini 的 [short] 行为一致。</summary>
        public static void ApplyShortNames(Dictionary<string, string> byId, Dictionary<string, string> byName)
        {
            EnsureLoaded();
            foreach (KeyValuePair<string, string> pair in _short)
            {
                if (byId != null)
                {
                    byId[pair.Key] = pair.Value;
                }
                if (byName != null)
                {
                    byName[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>等级/权重覆盖：只覆盖已存在的条目，不新增载具。</summary>
        public static void ApplyMeta(Dictionary<string, VehicleMetaEntry> bySlug,
                                     Dictionary<string, VehicleMetaEntry> byLocal)
        {
            EnsureLoaded();
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in _meta)
            {
                VehicleMetaEntry entry = null;
                if (bySlug != null)
                {
                    bySlug.TryGetValue(pair.Key, out entry);
                }
                if (entry == null && byLocal != null)
                {
                    byLocal.TryGetValue(pair.Key, out entry);
                }
                if (entry == null)
                {
                    continue;
                }
                Dictionary<string, object> row = pair.Value;
                double number;
                int rank;
                if (Number(row, "rank", out number) && number >= 1 && number <= 9)
                {
                    entry.Rank = (int)Math.Round(number);
                }
                else
                {
                    string rankText = Value(row, "rank");
                    rank = VehicleMeta.ParseRankNumber(rankText);
                    if (rank > 0)
                    {
                        entry.Rank = rank;
                    }
                }
                bool sameModes = false;
                object sameRaw;
                if (row.TryGetValue("sameModes", out sameRaw) && sameRaw is bool)
                {
                    sameModes = (bool)sameRaw;
                }
                if (sameModes)
                {
                    double rb;
                    if (Number(row, "rb", out rb))
                    {
                        entry.Rb = rb;
                        entry.Ab = rb;
                        entry.Sb = rb;
                        entry.SameModes = true;
                    }
                }
                else
                {
                    double value;
                    bool any = false;
                    if (Number(row, "rb", out value))
                    {
                        entry.Rb = value;
                        any = true;
                    }
                    if (Number(row, "ab", out value))
                    {
                        entry.Ab = value;
                        any = true;
                    }
                    if (Number(row, "sb", out value))
                    {
                        entry.Sb = value;
                        any = true;
                    }
                    if (any)
                    {
                        entry.SameModes = false;
                    }
                }
                string display = Value(row, "displayName");
                if (display.Length > 0)
                {
                    entry.DisplayName = display;
                }
                entry.Source = "覆盖";
            }
        }

        /// <summary>载具中文名/英文名/国家覆盖：只覆盖已存在的条目。</summary>
        public static void ApplyVehicles(Dictionary<string, Vehicle> byId, List<Vehicle> all)
        {
            EnsureLoaded();
            foreach (KeyValuePair<string, Dictionary<string, object>> pair in _vehicles)
            {
                Vehicle vehicle = null;
                if (byId != null)
                {
                    byId.TryGetValue(pair.Key, out vehicle);
                }
                if (vehicle == null)
                {
                    continue;
                }
                string zh = Value(pair.Value, "zh");
                string en = Value(pair.Value, "en");
                string nation = Value(pair.Value, "nation");
                if (zh.Length > 0)
                {
                    vehicle.Zh = zh;
                }
                if (en.Length > 0)
                {
                    vehicle.En = en;
                }
                if (nation.Length > 0)
                {
                    vehicle.Nation = nation;
                }
            }
            if (all != null)
            {
                all.Sort(delegate(Vehicle a, Vehicle b)
                {
                    return string.Compare(a.Zh, b.Zh, StringComparison.CurrentCulture);
                });
            }
        }
    }
}
