using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 一个已安装涂装（管理页右栏列表里的一个条目）。
    /// 两级结构下就是 UserSkins\template_&lt;代号&gt;\&lt;涂装名&gt; 里的子目录；
    /// 旧结构下就是载具目录本身（Legacy=true，不可删除、无勾选框）。
    /// </summary>
    internal sealed class ManagerSkinItem
    {
        /// <summary>列表显示名称。</summary>
        public string Name = "";
        /// <summary>真实文件夹名（备份路径、打开文件夹都用它）。</summary>
        public string FolderName = "";
        /// <summary>涂装目录的完整路径。</summary>
        public string Path = "";
        /// <summary>所属载具顶层文件夹名，例如 template_a6m5c。</summary>
        public string VehicleFolder = "";
        /// <summary>旧结构：载具目录本身即涂装，不可删除。</summary>
        public bool Legacy;
        /// <summary>占用字节数：&gt;=0 为实测值；-1 计算中；-2 无权限/读取失败。</summary>
        public long Size = -1;
        public bool Selected;
        /// <summary>旧结构条目的提示文案。</summary>
        public string Note = "";

        public bool CanSelect
        {
            get { return !Legacy; }
        }
    }

    /// <summary>一个载具顶层目录（UserSkins\template_&lt;代号&gt;）及其涂装条目。</summary>
    internal sealed class ManagerVehicleEntry
    {
        public InstalledSkin Skin;
        public string Folder = "";
        public string Path = "";
        public string Title = "";
        /// <summary>目录枚举失败（无权限等）时为 true，涂装数量按 0 计。</summary>
        public bool Unreadable;
        public readonly List<ManagerSkinItem> Items = new List<ManagerSkinItem>();

        public int SelectedCount()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].CanSelect && Items[i].Selected)
                {
                    n++;
                }
            }
            return n;
        }
    }

    /// <summary>整个涂装库的扫描结果（名称同步枚举，尺寸后台统计）。</summary>
    internal sealed class ManagerLibrary
    {
        public readonly List<ManagerVehicleEntry> Vehicles = new List<ManagerVehicleEntry>();
        /// <summary>已安装涂装数量：两级结构按子目录计，旧结构计 1。</summary>
        public int SkinCount;
        /// <summary>总占用：-1 计算中；-2 统计失败；&gt;=0 字节数。</summary>
        public long TotalBytes = -1;
        public bool SizesComplete;

        public ManagerVehicleEntry Find(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }
            for (int i = 0; i < Vehicles.Count; i++)
            {
                if (string.Equals(Vehicles[i].Folder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    return Vehicles[i];
                }
            }
            return null;
        }

        public bool HasPendingSizes()
        {
            for (int i = 0; i < Vehicles.Count; i++)
            {
                List<ManagerSkinItem> items = Vehicles[i].Items;
                for (int s = 0; s < items.Count; s++)
                {
                    if (items[s].Size == -1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>把所有条目重置为「计算中」，用于手动重新计算占用。</summary>
        public void ResetSizes()
        {
            TotalBytes = -1;
            SizesComplete = false;
            for (int i = 0; i < Vehicles.Count; i++)
            {
                List<ManagerSkinItem> items = Vehicles[i].Items;
                for (int s = 0; s < items.Count; s++)
                {
                    items[s].Size = -1;
                }
            }
        }
    }

    /// <summary>
    /// 管理页涂装列表的数据层：两级目录枚举、后台占用统计与缓存、备份/删除的基本操作。
    /// 只读 UserSkins 的目录名和文件大小；删除只针对被勾选的涂装子目录。
    /// </summary>
    internal static class ManagerSkins
    {
        public const string TemplatePrefix = "template_";
        /// <summary>旧结构条目的提示。</summary>
        public static readonly string LegacyHint = Lang.T("这是载具目录本身，请在外部处理", "This is the vehicle folder itself; handle it externally");

        private static readonly object CacheLock = new object();
        private static Dictionary<string, long> _sizeCache =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static int _cacheGeneration;

        // ---------------- 尺寸缓存 ----------------

        /// <summary>缓存代次：刷新/删除/手动重算时递增，旧的后台统计线程会自动退出。</summary>
        public static int CacheGeneration
        {
            get
            {
                lock (CacheLock)
                {
                    return _cacheGeneration;
                }
            }
        }

        public static void ClearSizeCache()
        {
            lock (CacheLock)
            {
                _sizeCache.Clear();
                _cacheGeneration++;
            }
        }

        private static bool TryGetCached(string path, out long size)
        {
            lock (CacheLock)
            {
                return _sizeCache.TryGetValue(path, out size);
            }
        }

        private static bool StoreCached(string path, long size, int generation)
        {
            lock (CacheLock)
            {
                if (generation != _cacheGeneration)
                {
                    return false;
                }
                _sizeCache[path] = size;
                return true;
            }
        }

        // ---------------- 名称展示 ----------------

        /// <summary>旧结构条目名称：目录名去掉 template_ 前缀（其余原样保留，包括 .BAN）。</summary>
        public static string DisplayName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return "";
            }
            if (folderName.StartsWith(TemplatePrefix, StringComparison.OrdinalIgnoreCase) &&
                folderName.Length > TemplatePrefix.Length)
            {
                return folderName.Substring(TemplatePrefix.Length);
            }
            return folderName;
        }

        // ---------------- 扫描（只枚举目录名，尺寸交给后台线程） ----------------

        /// <summary>
        /// 同步枚举所有载具目录与涂装子目录（不做文件 IO 遍历，几百个目录也很快），
        /// 已缓存的尺寸直接填入，未缓存的保持 -1（界面显示「计算中…」）。
        /// </summary>
        public static ManagerLibrary BuildLibrary(IList<InstalledSkin> skins)
        {
            ManagerLibrary lib = new ManagerLibrary();
            if (skins == null)
            {
                return lib;
            }
            long total = 0;
            bool pending = false;
            for (int i = 0; i < skins.Count; i++)
            {
                InstalledSkin skin = skins[i];
                if (skin == null || string.IsNullOrEmpty(skin.Path))
                {
                    continue;
                }
                ManagerVehicleEntry entry = new ManagerVehicleEntry();
                entry.Skin = skin;
                entry.Folder = skin.Folder;
                entry.Path = skin.Path;
                entry.Title = skin.ShortName;
                string[] dirs = null;
                try
                {
                    dirs = Directory.GetDirectories(Util.LongPath(skin.Path));
                }
                catch (Exception ex)
                {
                    entry.Unreadable = true;
                    Config.Log("枚举涂装目录失败：" + skin.Path + "（" + ex.Message + "）");
                }
                if (dirs != null)
                {
                    if (dirs.Length == 0)
                    {
                        // 旧结构：载具目录本身就是一个涂装（目录里真的有文件才算）。
                        if (HasDirectFile(skin.Path))
                        {
                            ManagerSkinItem legacy = new ManagerSkinItem();
                            legacy.Name = DisplayName(skin.Folder);
                            legacy.FolderName = skin.Folder;
                            legacy.Path = skin.Path;
                            legacy.VehicleFolder = skin.Folder;
                            legacy.Legacy = true;
                            legacy.Note = LegacyHint;
                            entry.Items.Add(legacy);
                        }
                    }
                    else
                    {
                        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                        for (int d = 0; d < dirs.Length; d++)
                        {
                            string name = "";
                            try
                            {
                                name = Path.GetFileName(dirs[d]);
                            }
                            catch
                            {
                            }
                            if (string.IsNullOrEmpty(name))
                            {
                                continue;
                            }
                            ManagerSkinItem item = new ManagerSkinItem();
                            item.Name = name;
                            item.FolderName = name;
                            item.Path = dirs[d];
                            item.VehicleFolder = skin.Folder;
                            entry.Items.Add(item);
                        }
                    }
                }
                for (int s = 0; s < entry.Items.Count; s++)
                {
                    long cached;
                    if (TryGetCached(entry.Items[s].Path, out cached))
                    {
                        entry.Items[s].Size = cached;
                    }
                }
                for (int s = 0; s < entry.Items.Count; s++)
                {
                    if (entry.Items[s].Size >= 0)
                    {
                        total += entry.Items[s].Size;
                    }
                    else if (entry.Items[s].Size == -1)
                    {
                        pending = true;
                    }
                }
                lib.Vehicles.Add(entry);
                lib.SkinCount += entry.Items.Count;
            }
            if (!pending)
            {
                lib.TotalBytes = total;
                lib.SizesComplete = true;
            }
            return lib;
        }

        private static bool HasDirectFile(string dir)
        {
            try
            {
                string[] files = Directory.GetFiles(Util.LongPath(dir), "*", SearchOption.TopDirectoryOnly);
                return files.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // ---------------- 后台占用统计 ----------------

        /// <summary>
        /// 后台统计所有未缓存条目的占用。每个载具统计完回调一次（便于就地把「计算中…」改成数字），
        /// 全部结束后回调 finished。generation 与缓存代次不一致时线程自行退出。
        /// </summary>
        public static void MeasureAsync(ManagerLibrary lib, int generation,
                                        Action<ManagerVehicleEntry> vehicleDone, Action finished)
        {
            if (lib == null)
            {
                return;
            }
            Thread thread = new Thread(delegate()
            {
                bool aborted = false;
                try
                {
                    for (int v = 0; v < lib.Vehicles.Count; v++)
                    {
                        ManagerVehicleEntry entry = lib.Vehicles[v];
                        bool changed = false;
                        for (int s = 0; s < entry.Items.Count; s++)
                        {
                            ManagerSkinItem item = entry.Items[s];
                            if (item.Size != -1)
                            {
                                continue;
                            }
                            long bytes;
                            bool ok = TryMeasure(item.Path, out bytes);
                            item.Size = ok ? bytes : -2;
                            changed = true;
                            if (!StoreCached(item.Path, item.Size, generation))
                            {
                                aborted = true;
                                break;
                            }
                        }
                        if (aborted)
                        {
                            break;
                        }
                        if (changed && vehicleDone != null)
                        {
                            vehicleDone(entry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Config.Log("统计涂装占用异常：" + ex.Message);
                }
                if (aborted)
                {
                    return;
                }
                long total = 0;
                bool pending = false;
                for (int v = 0; v < lib.Vehicles.Count; v++)
                {
                    List<ManagerSkinItem> items = lib.Vehicles[v].Items;
                    for (int s = 0; s < items.Count; s++)
                    {
                        if (items[s].Size >= 0)
                        {
                            total += items[s].Size;
                        }
                        else if (items[s].Size == -1)
                        {
                            pending = true;
                        }
                    }
                }
                lib.TotalBytes = pending ? -1 : total;
                lib.SizesComplete = !pending;
                if (finished != null)
                {
                    finished();
                }
            });
            thread.IsBackground = true;
            try
            {
                thread.Start();
            }
            catch (Exception ex)
            {
                Config.Log("启动占用统计线程失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 只统计「涂装文件」的字节数：目录条目本身不计，隐藏文件不计，无权限子目录跳过，
        /// 重新解析点（junction/symlink）不跟随。返回 false 表示根目录读不了。
        /// </summary>
        private static bool TryMeasure(string root, out long bytes)
        {
            bytes = 0;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(Util.LongPath(root)))
            {
                return false;
            }
            Stack<string> stack = new Stack<string>();
            stack.Push(root);
            bool readable = false;
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files;
                string[] subs;
                try
                {
                    files = Directory.GetFiles(Util.LongPath(dir));
                    subs = Directory.GetDirectories(Util.LongPath(dir));
                }
                catch
                {
                    // 无权限子目录：跳过它，继续统计其余。
                    continue;
                }
                readable = true;
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileAttributes attrs = File.GetAttributes(files[i]);
                        if ((attrs & FileAttributes.Hidden) != 0)
                        {
                            continue;
                        }
                        bytes += new FileInfo(files[i]).Length;
                    }
                    catch
                    {
                    }
                }
                for (int i = 0; i < subs.Length; i++)
                {
                    try
                    {
                        FileAttributes attrs = File.GetAttributes(subs[i]);
                        if ((attrs & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    stack.Push(subs[i]);
                }
            }
            return readable;
        }

        // ---------------- 体积格式：<1024MB 一位 MB，>=1024MB 两位 GB，1024 进制 ----------------

        public static string FormatSize(long bytes)
        {
            if (bytes == -1)
            {
                return Lang.T("计算中…", "calculating…");
            }
            if (bytes < 0)
            {
                return "—";
            }
            double mb = bytes / 1048576.0;
            if (mb < 1024.0)
            {
                return mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            }
            return (mb / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        // ---------------- 备份 ----------------

        /// <summary>备份根：Config.BackupDir（其本身即安装目录下的 backups）。</summary>
        public static string BackupRoot
        {
            get { return Config.BackupDir; }
        }

        /// <summary>
        /// 把勾选的涂装复制到 BackupRoot\&lt;载具文件夹&gt;\&lt;涂装名&gt;\，保留目录结构，不改动原目录。
        /// </summary>
        public static bool BackupItems(IList<ManagerSkinItem> items, string vehicleFolder,
                                       out string root, out int copied, out string error)
        {
            copied = 0;
            error = null;
            root = Path.Combine(BackupRoot, vehicleFolder ?? "");
            try
            {
                Directory.CreateDirectory(Util.LongPath(root));
            }
            catch (Exception ex)
            {
                error = Lang.T("无法创建备份目录：", "Cannot create backup folder: ") + root + "（" + ex.Message + "）";
                return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                ManagerSkinItem item = items[i];
                if (item == null || item.Legacy)
                {
                    continue;
                }
                string target = Path.Combine(root, item.FolderName);
                string copyError;
                if (!CopyDirectory(item.Path, target, out copyError))
                {
                    error = item.Name + "：" + copyError;
                    return false;
                }
                copied++;
            }
            return true;
        }

        /// <summary>递归复制目录（含隐藏文件），目标同名文件覆盖，长路径安全。</summary>
        public static bool CopyDirectory(string source, string target, out string error)
        {
            error = null;
            string src = Util.LongPath(source);
            try
            {
                if (!Directory.Exists(src))
                {
                    error = Lang.T("目录不存在：", "Folder not found: ") + source;
                    return false;
                }
                Directory.CreateDirectory(Util.LongPath(target));
                string[] files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
                string root = src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (int i = 0; i < files.Length; i++)
                {
                    string rel = files[i].Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string dest = Util.LongPath(Path.Combine(target, rel));
                    string dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(files[i], dest, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ---------------- 删除 ----------------

        /// <summary>
        /// 批量删除勾选的涂装子目录：被占用/无权限的条目跳过并记录，其余继续删。
        /// 永远不接受旧结构条目或载具目录本身。deleted / skipped 为结果清单（skipped 里带原因）。
        /// </summary>
        public static void DeleteItems(IList<ManagerSkinItem> items, string vehiclePath,
                                       List<string> deleted, List<string> skipped)
        {
            if (items == null)
            {
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                ManagerSkinItem item = items[i];
                if (item == null)
                {
                    continue;
                }
                if (item.Legacy)
                {
                    skipped.Add("· " + item.Name + Lang.T("：旧结构项不可删除", ": legacy item cannot be deleted"));
                    continue;
                }
                if (!IsDirectChild(item.Path, vehiclePath))
                {
                    skipped.Add("· " + item.Name + Lang.T("：不是载具目录内的涂装子目录，已跳过", ": not a skin subfolder of the vehicle; skipped"));
                    Config.Log("删除涂装跳过（越界）" + item.Path);
                    continue;
                }
                string error;
                if (DeleteDirectoryStrict(item.Path, out error))
                {
                    deleted.Add(item.Name);
                }
                else
                {
                    skipped.Add("· " + item.Name + "：" + error);
                    Config.Log("删除涂装跳过 " + item.Path + "：" + error);
                }
            }
        }

        /// <summary>子目录是否为 parent 的直接子目录（约束：只允许删除载具目录的直接子目录）。</summary>
        public static bool IsDirectChild(string child, string parent)
        {
            try
            {
                if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent))
                {
                    return false;
                }
                string childFull = Path.GetFullPath(child)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string parentFull = Path.GetFullPath(parent)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dir = Path.GetDirectoryName(childFull);
                if (string.IsNullOrEmpty(dir))
                {
                    return false;
                }
                return string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                     parentFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 只删一个涂装子目录（绝不删载具模板目录，调用方保证传入的是子目录）。
        /// 返回 false 表示被占用或无权限，调用方应跳过并继续其余条目。
        /// </summary>
        public static bool DeleteDirectoryStrict(string dir, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(dir))
            {
                error = Lang.T("目录为空", "Folder is empty");
                return false;
            }
            string full = Util.LongPath(dir);
            try
            {
                if (!Directory.Exists(full))
                {
                    error = Lang.T("目录不存在", "Folder not found");
                    return false;
                }
                Directory.Delete(full, true);
                return true;
            }
            catch (Exception first)
            {
                // 只读文件：去掉只读属性后再试一次；仍失败就把错误交给调用方跳过。
                try
                {
                    string[] files = Directory.GetFiles(full, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            File.SetAttributes(files[i], FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }
                    Directory.Delete(full, true);
                    return true;
                }
                catch (Exception ex)
                {
                    error = DescribeDeleteError(ex, first);
                    return false;
                }
            }
        }

        private static string DescribeDeleteError(Exception ex, Exception first)
        {
            if (ex is UnauthorizedAccessException)
            {
                return Lang.T("没有删除权限：", "No permission to delete: ") + ex.Message;
            }
            if (ex is IOException)
            {
                return Lang.T("目录被占用或正被其他程序使用：", "Folder is in use by another program: ") + ex.Message;
            }
            return ex.Message;
        }
    }

    /// <summary>
    /// 右栏涂装列表的一行：勾选框 + 涂装文件夹名 + 占用大小 + 打开文件夹按钮。
    /// 旧结构行没有勾选框，显示「这是载具目录本身，请在外部处理」。
    /// </summary>
    internal sealed class ManagerSkinRow : Control
    {
        private const int Pad = 8;
        private const int CheckSize = 18;
        private const int CheckHit = 28;

        private bool _hover;
        private bool _checkHot;
        private readonly FlatButton _open;

        public ManagerSkinItem Item;
        public event EventHandler SelectionChanged;

        public ManagerSkinRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.CardBg;
            Cursor = Cursors.Hand;
            TabStop = true;
            _open = new FlatButton();
            _open.Text = Lang.T("打开文件夹", "Open folder");
            _open.Font = Ui.SmallBold;
            _open.Height = Ui.S(26);
            _open.Click += delegate { OpenFolder(); };
            Controls.Add(_open);
            _open.BackColor = Ui.CardBg;
        }

        /// <summary>按钮圆角外的四角要跟行当前底色一致，悬停/聚焦时同步换色。</summary>
        private void SyncOpenBackColor()
        {
            if (_open == null)
            {
                return;
            }
            Color color = _hover ? Ui.PanelHover : (Focused ? Ui.PanelActive : Ui.CardBg);
            if (_open.BackColor != color)
            {
                _open.BackColor = color;
            }
        }

        public bool Selected
        {
            get { return Item != null && Item.Selected; }
            set
            {
                if (Item == null || !Item.CanSelect)
                {
                    return;
                }
                if (Item.Selected == value)
                {
                    return;
                }
                Item.Selected = value;
                Invalidate();
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_open != null)
            {
                int openH = Ui.S(26);
                int openW = Math.Max(Ui.S(78),
                    TextRenderer.MeasureText(Lang.T("打开文件夹", "Open folder"), Ui.SmallBold).Width + Ui.S(18));
                _open.SetBounds(Width - Ui.S(Pad) - openW, (Height - openH) / 2, openW, openH);
            }
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            SyncOpenBackColor();
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            SyncOpenBackColor();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            SyncOpenBackColor();
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _checkHot = false;
            SyncOpenBackColor();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = !(Item != null && Item.Legacy) && CheckRect().Contains(e.Location);
            if (hot != _checkHot)
            {
                _checkHot = hot;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                if (Item != null && Item.CanSelect)
                {
                    Selected = !Selected;
                }
                return;
            }
            base.OnMouseDown(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused && Item != null && Item.CanSelect &&
                (keyData == Keys.Space || keyData == Keys.Enter))
            {
                Selected = !Selected;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Invalidate();
        }

        private Rectangle CheckRect()
        {
            return new Rectangle(Ui.S(Pad), (Height - Ui.S(CheckHit)) / 2, Ui.S(CheckHit), Ui.S(CheckHit));
        }

        private void OpenFolder()
        {
            if (Item == null || string.IsNullOrEmpty(Item.Path))
            {
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + Item.Path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), Lang.T("无法打开资源管理器：", "Cannot open File Explorer: ") + ex.Message, Lang.T("打开文件夹", "Open folder"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            SyncOpenBackColor();
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover)
            {
                Ui.FillRound(g, r, Ui.S(6), Ui.PanelHover);
            }
            else if (Focused)
            {
                Ui.FillRound(g, r, Ui.S(6), Ui.PanelActive);
            }

            bool legacy = Item != null && Item.Legacy;
            int textX = Ui.S(Pad);
            if (!legacy)
            {
                Rectangle hit = CheckRect();
                Rectangle circle = new Rectangle(hit.X + (hit.Width - Ui.S(CheckSize)) / 2,
                    hit.Y + (hit.Height - Ui.S(CheckSize)) / 2, Ui.S(CheckSize), Ui.S(CheckSize));
                bool selected = Item != null && Item.Selected;
                if (selected)
                {
                    Ui.FillRound(g, circle, circle.Width / 2, Ui.Accent);
                    using (Pen pen = new Pen(Ui.T.CheckMark, Math.Max(1.4f, Ui.Scale * 1.7f)))
                    {
                        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        g.DrawLines(pen, new Point[]
                        {
                            new Point(circle.X + Ui.S(4), circle.Y + Ui.S(9)),
                            new Point(circle.X + Ui.S(7), circle.Y + Ui.S(12)),
                            new Point(circle.X + Ui.S(14), circle.Y + Ui.S(5))
                        });
                    }
                }
                else
                {
                    using (Pen pen = new Pen(_checkHot ? Ui.TextDim : Ui.Line, 1f))
                    {
                        g.DrawEllipse(pen, circle);
                    }
                }
                textX = hit.Right + Ui.S(2);
            }

            int openLeft = _open == null ? Width : _open.Left;
            int sizeW = Ui.S(70);
            int sizeX = Math.Max(textX + Ui.S(40), openLeft - Ui.S(6) - sizeW);
            int nameW = Math.Max(Ui.S(30), sizeX - Ui.S(6) - textX);
            if (legacy)
            {
                Ui.DrawText(g, Item == null ? "" : Item.Name, Ui.Body, Ui.Text,
                    new Rectangle(textX, Ui.S(5), Math.Max(Ui.S(30), openLeft - Ui.S(8) - textX), Ui.S(20)),
                    Ui.LeftMiddle);
                Ui.DrawText(g, ManagerSkins.LegacyHint, Ui.Small, Ui.TextFaint,
                    new Rectangle(textX, Ui.S(25), Math.Max(Ui.S(30), openLeft - Ui.S(8) - textX), Ui.S(18)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
            }
            else
            {
                Ui.DrawText(g, Item == null ? "" : Item.Name, Ui.Body, Ui.Text,
                    new Rectangle(textX, 0, nameW, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
                string sizeText = Item == null ? "" : ManagerSkins.FormatSize(Item.Size);
                Color sizeColor = Item != null && Item.Size >= 0 ? Ui.TextDim : Ui.TextFaint;
                Ui.DrawText(g, sizeText, Ui.Small, sizeColor,
                    new Rectangle(sizeX, 0, sizeW, Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (Focused)
            {
                Ui.DrawFocus(g, r, Ui.S(6), Ui.Accent);
            }
        }
    }
}
