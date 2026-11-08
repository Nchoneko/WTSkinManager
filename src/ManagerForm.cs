using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WtSkin
{
    /// <summary>
    /// 涂装管理器：左侧国家筛选 + 顶部工具条 + 三列卡片网格 + 底部操作。
    /// 只枚举 UserSkins 顶层目录名；除禁用/启用重命名外不碰目录内容。
    /// </summary>
    internal class ManagerForm : ModalForm
    {
        private readonly List<InstalledSkin> _all = new List<InstalledSkin>();
        private readonly List<SkinCard> _cards = new List<SkinCard>();
        private readonly List<NationItem> _nationItems = new List<NationItem>();
        private HashSet<string> _selectedBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private BufferedPanel _side;
        private BufferedPanel _nationPanel;
        private BufferedPanel _grid;
        private TextBox _search;
        private FlatButton _sortButton;
        private FlatButton _selectAll;
        private FlatButton _clearButton;
        private FlatButton _openButton;
        private FlatButton _backupButton;
        private FlatButton _toggleButton;
        private FlatButton _restoreButton;
        private FlatButton _refreshButton;
        private FlatButton _updateButton;
        private FlatButton _updateAllButton;
        private Label _stats;
        private Label _metaLabel;
        private string _nationFilter = "";
        private int _sortMode;
        private bool _rebuilding;
        private bool _updating;
        private int _lastGridWidth;

        public ManagerForm()
            : base("涂装管理", 1320, 860)
        {
            Content.BackColor = Ui.Bg;
            BuildSidebar();
            BuildToolbar();
            BuildGrid();
            Shown += delegate { Reload(); };
            Resize += delegate { LayoutManager(); };
            LayoutManager();
        }

        /// <summary>嵌入主窗口作为分页时调用：去掉自己的标题栏。</summary>
        public void Embed()
        {
            SetEmbedded();
            LayoutManager();
        }

        /// <summary>嵌入后第一次显示时立即加载，避免切换动画捕捉到空列表。</summary>
        public void ReloadNow()
        {
            Reload();
        }

        // ---------------- 布局 ----------------

        private void BuildSidebar()
        {
            _side = new BufferedPanel();
            _side.BackColor = Ui.BgSoft;
            _side.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Ui.LineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, _side.Width - 1, 0, _side.Width - 1, _side.Height);
                }
            };
            Content.Controls.Add(_side);

            Label brand = new Label();
            brand.Text = "涂装库";
            brand.Font = Ui.H1;
            brand.ForeColor = Ui.Text;
            brand.BackColor = Ui.BgSoft;
            brand.AutoSize = false;
            brand.SetBounds(Ui.S(20), Ui.S(16), Ui.S(150), Ui.S(26));
            _side.Controls.Add(brand);

            Label tip = new Label();
            tip.Text = "按国家筛选";
            tip.Font = Ui.Small;
            tip.ForeColor = Ui.TextFaint;
            tip.BackColor = Ui.BgSoft;
            tip.AutoSize = false;
            tip.SetBounds(Ui.S(20), Ui.S(44), Ui.S(150), Ui.S(18));
            _side.Controls.Add(tip);

            _nationPanel = new BufferedPanel();
            _nationPanel.BackColor = Ui.BgSoft;
            _nationPanel.AutoScroll = true;
            _side.Controls.Add(_nationPanel);
        }

        private FlatButton ToolButton(string text, bool danger)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.BackColor = Ui.Bg;
            button.Danger = danger;
            Content.Controls.Add(button);
            return button;
        }

        private void BuildToolbar()
        {
            _search = new TextBox();
            _search.BorderStyle = BorderStyle.FixedSingle;
            _search.BackColor = Ui.Panel;
            _search.ForeColor = Ui.Text;
            _search.Font = Ui.Body;
            Native.SetCue(_search, "搜索涂装 / 载具 / 文件夹");
            _search.TextChanged += delegate { RebuildGrid(); UpdateButtons(); };
            Content.Controls.Add(_search);

            _sortButton = ToolButton("排序：文件夹", false);
            _sortButton.Click += delegate
            {
                _sortMode = (_sortMode + 1) % 3;
                _sortButton.Text = _sortMode == 0 ? "排序：文件夹" : (_sortMode == 1 ? "排序：载具" : "排序：状态");
                RebuildGrid();
                UpdateButtons();
            };

            _selectAll = ToolButton("全选", false);
            _selectAll.Click += delegate { ToggleSelectAll(); };

            _clearButton = ToolButton("清空文件夹", true);
            _clearButton.Click += delegate { ClearSelected(); };
            _openButton = ToolButton("打开文件夹", false);
            _openButton.Click += delegate { OpenSelected(); };
            _backupButton = ToolButton("备份", false);
            _backupButton.Click += delegate { BackupClicked(); };
            _toggleButton = ToolButton("禁用 / 启用", false);
            _toggleButton.Click += delegate { ToggleSelected(); };
            _restoreButton = ToolButton("还原备份", false);
            _restoreButton.Click += delegate { RestoreBackup(); };
            _refreshButton = ToolButton("刷新", false);
            _refreshButton.Click += delegate { Reload(); };

            _updateButton = ToolButton("更新数据", false);
            _updateButton.Click += delegate { UpdateVehicleMeta(false); };

            _updateAllButton = ToolButton("更新全部数据", false);
            _updateAllButton.Click += delegate { UpdateVehicleMeta(true); };

            _metaLabel = new Label();
            _metaLabel.Font = Ui.Small;
            _metaLabel.ForeColor = Ui.TextFaint;
            _metaLabel.BackColor = Ui.Bg;
            _metaLabel.AutoSize = false;
            _metaLabel.AutoEllipsis = true;
            _metaLabel.TextAlign = ContentAlignment.MiddleLeft;
            _metaLabel.Cursor = Cursors.Hand;
            _metaLabel.Text = VehicleMeta.StatusText() + " · " + VehicleCatalog.CoverageShort();
            _metaLabel.Click += delegate { ShowMetaInfo(); };
            Content.Controls.Add(_metaLabel);

            _stats = new Label();
            _stats.Font = Ui.Small;
            _stats.ForeColor = Ui.TextDim;
            _stats.BackColor = Ui.Bg;
            _stats.AutoSize = false;
            _stats.TextAlign = ContentAlignment.MiddleRight;
            _stats.AutoEllipsis = true;
            Content.Controls.Add(_stats);
        }

        private void BuildGrid()
        {
            _grid = new BufferedPanel();
            _grid.BackColor = Ui.Bg;
            _grid.AutoScroll = true;
            _grid.Resize += delegate
            {
                if (!_rebuilding && _grid.ClientSize.Width != _lastGridWidth)
                {
                    RebuildGrid();
                }
            };
            Content.Controls.Add(_grid);
        }

        private void LayoutManager()
        {
            if (Content == null || _side == null || _grid == null)
            {
                return;
            }
            int width = Content.Width;
            int height = Content.Height;
            int sideW = Ui.S(196);
            _side.SetBounds(0, 0, sideW, height);
            _nationPanel.SetBounds(0, Ui.S(72), sideW, Math.Max(Ui.S(100), height - Ui.S(72)));

            int mainLeft = sideW;
            int mainW = Math.Max(Ui.S(360), width - sideW);
            int pad = Ui.S(16);
            int gap = Ui.S(8);
            int sortW = Ui.S(116);
            int selW = Ui.S(96);
            int statsMin = Ui.S(170);
            int searchW = mainW - pad * 2 - sortW - selW - statsMin - gap * 2;
            if (searchW < Ui.S(150))
            {
                searchW = Ui.S(150);
            }
            if (searchW > Ui.S(340))
            {
                searchW = Ui.S(340);
            }
            int x = mainLeft + pad;
            _search.SetBounds(x, Ui.S(13), searchW, Ui.S(30));
            x += searchW + gap;
            _sortButton.SetBounds(x, Ui.S(12), sortW, Ui.S(34));
            x += sortW + gap;
            _selectAll.SetBounds(x, Ui.S(12), selW, Ui.S(34));
            x += selW + gap;
            _stats.SetBounds(x, Ui.S(12), Math.Max(Ui.S(80), mainLeft + mainW - pad - x), Ui.S(34));

            int y2 = Ui.S(54);
            int bx = mainLeft + pad;
            int bwClear = Ui.S(104);
            int bwOpen = Ui.S(104);
            int bwBackup = Ui.S(84);
            int bwToggle = Ui.S(92);
            int bwRestore = Ui.S(104);
            int bwRefresh = Ui.S(72);
            int bwData = Ui.S(92);
            int bwDataAll = Ui.S(112);
            int bgap = Ui.S(7);
            _clearButton.SetBounds(bx, y2, bwClear, Ui.S(33));
            bx += bwClear + bgap;
            _openButton.SetBounds(bx, y2, bwOpen, Ui.S(33));
            bx += bwOpen + bgap;
            _backupButton.SetBounds(bx, y2, bwBackup, Ui.S(33));
            bx += bwBackup + bgap;
            _toggleButton.SetBounds(bx, y2, bwToggle, Ui.S(33));
            bx += bwToggle + bgap;
            _restoreButton.SetBounds(bx, y2, bwRestore, Ui.S(33));
            bx += bwRestore + bgap;
            _refreshButton.SetBounds(bx, y2, bwRefresh, Ui.S(33));
            bx += bwRefresh + bgap;
            _updateButton.SetBounds(bx, y2, bwData, Ui.S(33));
            bx += bwData + bgap;
            _updateAllButton.SetBounds(bx, y2, bwDataAll, Ui.S(33));
            bx += bwDataAll + bgap;
            _metaLabel.SetBounds(bx, y2, Math.Max(Ui.S(80), mainLeft + mainW - pad - bx), Ui.S(33));

            _grid.SetBounds(mainLeft + Ui.S(12), Ui.S(96),
                            Math.Max(Ui.S(240), mainW - Ui.S(24)),
                            Math.Max(Ui.S(120), height - Ui.S(96) - Ui.S(10)));
            LayoutNationItems();
            _lastGridWidth = _grid.ClientSize.Width;
        }

        private void LayoutNationItems()
        {
            if (_nationPanel == null)
            {
                return;
            }
            int width = Math.Max(Ui.S(100), _nationPanel.ClientSize.Width - Ui.S(20));
            for (int i = 0; i < _nationItems.Count; i++)
            {
                _nationItems[i].Width = width;
            }
        }

        // ---------------- 数据 ----------------

        private void Reload()
        {
            _all.Clear();
            _all.AddRange(InstalledSkins.Scan(Config.UserSkinsDir));
            NameMap.ApplyUniqueTitles(_all);

            HashSet<string> stillSelected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _all.Count; i++)
            {
                string key = BaseFolder(_all[i].Folder);
                if (_selectedBases.Contains(key))
                {
                    _all[i].Selected = true;
                    stillSelected.Add(key);
                }
            }
            _selectedBases = stillSelected;

            BuildNationItems();
            RebuildGrid();
            UpdateButtons();
        }

        private static string BaseFolder(string folder)
        {
            if (folder == null)
            {
                return "";
            }
            if (folder.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return folder.Substring(0, folder.Length - InstalledSkins.BanSuffix.Length);
            }
            return folder;
        }

        private void BuildNationItems()
        {
            if (_nationPanel == null)
            {
                return;
            }
            _nationPanel.SuspendLayout();
            for (int i = _nationPanel.Controls.Count - 1; i >= 0; i--)
            {
                _nationPanel.Controls[i].Dispose();
            }
            _nationPanel.Controls.Clear();
            _nationItems.Clear();

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _all.Count; i++)
            {
                string code = string.IsNullOrEmpty(_all[i].Nation) ? "__unknown" : _all[i].Nation;
                int n;
                counts.TryGetValue(code, out n);
                counts[code] = n + 1;
            }

            List<string> codes = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                codes.Add(pair.Key);
            }
            codes.Sort(delegate(string a, string b)
            {
                int na;
                int nb;
                counts.TryGetValue(a, out na);
                counts.TryGetValue(b, out nb);
                if (na != nb)
                {
                    return nb.CompareTo(na);
                }
                int ra = a == "__unknown" ? 10 : NationInfo.Rank(a);
                int rb = b == "__unknown" ? 10 : NationInfo.Rank(b);
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            bool filterStillExists = _nationFilter.Length == 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (string.Equals(codes[i], _nationFilter, StringComparison.OrdinalIgnoreCase))
                {
                    filterStillExists = true;
                }
            }
            if (!filterStillExists)
            {
                _nationFilter = "";
            }

            int top = Ui.S(2);
            AddNationItem("", "全部", _all.Count, top);
            top += Ui.S(36);
            for (int i = 0; i < codes.Count; i++)
            {
                string code = codes[i];
                int n;
                counts.TryGetValue(code, out n);
                string label = code == "__unknown" ? "未知" : NameMap.NationName(code);
                AddNationItem(code, label, n, top);
                top += Ui.S(36);
            }
            _nationPanel.ResumeLayout();
            LayoutNationItems();
            _nationPanel.Invalidate();
        }

        private void AddNationItem(string code, string label, int count, int top)
        {
            NationItem item = new NationItem();
            item.Code = code;
            item.Label = label;
            item.Count = count;
            item.Selected = string.Equals(code, _nationFilter, StringComparison.OrdinalIgnoreCase);
            item.SetBounds(Ui.S(10), top, Math.Max(Ui.S(100), _nationPanel.ClientSize.Width - Ui.S(20)), Ui.S(32));
            item.Picked += delegate
            {
                _nationFilter = item.Code;
                for (int i = 0; i < _nationItems.Count; i++)
                {
                    _nationItems[i].Selected = string.Equals(_nationItems[i].Code, _nationFilter, StringComparison.OrdinalIgnoreCase);
                    _nationItems[i].Invalidate();
                }
                RebuildGrid();
                UpdateButtons();
            };
            _nationPanel.Controls.Add(item);
            _nationItems.Add(item);
        }

        private List<InstalledSkin> Filtered()
        {
            string query = _search == null ? "" : _search.Text.Trim();
            List<InstalledSkin> list = new List<InstalledSkin>();
            for (int i = 0; i < _all.Count; i++)
            {
                InstalledSkin skin = _all[i];
                if (_nationFilter == "__unknown")
                {
                    if (!string.IsNullOrEmpty(skin.Nation))
                    {
                        continue;
                    }
                }
                else if (_nationFilter.Length > 0 &&
                         !string.Equals(skin.Nation, _nationFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (query.Length > 0)
                {
                    string hay = skin.Folder + " " + skin.FullName + " " + skin.ShortName + " " +
                                 skin.UnitId + " " + skin.NationName;
                    if (hay.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0)
                    {
                        continue;
                    }
                }
                list.Add(skin);
            }
            list.Sort(delegate(InstalledSkin a, InstalledSkin b)
            {
                if (_sortMode == 1)
                {
                    int r = string.Compare(a.ShortName, b.ShortName, StringComparison.CurrentCulture);
                    if (r != 0)
                    {
                        return r;
                    }
                }
                else if (_sortMode == 2)
                {
                    int r = a.Disabled.CompareTo(b.Disabled);
                    if (r != 0)
                    {
                        return r;
                    }
                }
                return string.Compare(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private void RebuildGrid()
        {
            if (_grid == null || _rebuilding)
            {
                return;
            }
            _rebuilding = true;
            int keepScroll = 0;
            try
            {
                keepScroll = _grid.VerticalScroll.Value;
                _grid.AutoScrollPosition = new Point(0, 0);
            }
            catch
            {
            }
            _grid.SuspendLayout();
            for (int i = _grid.Controls.Count - 1; i >= 0; i--)
            {
                _grid.Controls[i].Dispose();
            }
            _grid.Controls.Clear();
            _cards.Clear();

            List<InstalledSkin> list = Filtered();
            int pad = Ui.S(10);
            int gap = Ui.S(12);
            int reserve = SystemInformation.VerticalScrollBarWidth + Ui.S(6);
            int available = _grid.ClientSize.Width - pad * 2 - reserve;
            if (available < Ui.S(300))
            {
                available = Math.Max(Ui.S(280), _grid.ClientSize.Width - pad * 2);
            }
            int minW = Ui.S(320);
            int columns = (available + gap) / (minW + gap);
            if (columns < 1)
            {
                columns = 1;
            }
            if (columns > 3)
            {
                columns = 3;
            }
            int cardW = (available - (columns - 1) * gap) / columns;
            if (cardW < minW && columns > 1)
            {
                columns--;
                cardW = (available - (columns - 1) * gap) / columns;
            }
            if (cardW < Ui.S(240))
            {
                cardW = Math.Max(Ui.S(240), available);
            }

            int top = 0;
            int index = 0;
            while (index < list.Count)
            {
                List<SkinCard> row = new List<SkinCard>();
                int rowH = Ui.S(168);
                int count = Math.Min(columns, list.Count - index);
                for (int c = 0; c < count; c++)
                {
                    InstalledSkin skin = list[index + c];
                    SkinCard card = new SkinCard();
                    card.Skin = skin;
                    card.Selected = skin.Selected;
                    card.Width = cardW;
                    int need = card.PreferredHeight();
                    if (need > rowH)
                    {
                        rowH = need;
                    }
                    row.Add(card);
                }
                for (int c = 0; c < row.Count; c++)
                {
                    SkinCard card = row[c];
                    card.SetBounds(pad + c * (cardW + gap), top, cardW, rowH);
                    InstalledSkin capturedSkin = card.Skin;
                    SkinCard capturedCard = card;
                    card.SelectionChanged += delegate
                    {
                        capturedSkin.Selected = capturedCard.Selected;
                        if (capturedCard.Selected)
                        {
                            _selectedBases.Add(BaseFolder(capturedSkin.Folder));
                        }
                        else
                        {
                            _selectedBases.Remove(BaseFolder(capturedSkin.Folder));
                        }
                        UpdateButtons();
                    };
                    _grid.Controls.Add(card);
                    _cards.Add(card);
                }
                index += count;
                top += rowH + gap;
            }

            if (list.Count == 0)
            {
                Label empty = new Label();
                empty.Text = _all.Count == 0
                    ? "UserSkins 里还没有涂装。\r\n先在游戏里创建涂装示例并导入一个包，这里就会出现。"
                    : "没有匹配的涂装。\r\n试试切换国家筛选或清空搜索关键词。";
                empty.ForeColor = Ui.TextFaint;
                empty.BackColor = Ui.Bg;
                empty.Font = Ui.Body;
                empty.AutoSize = false;
                empty.SetBounds(Ui.S(10), Ui.S(20), Math.Max(Ui.S(200), _grid.ClientSize.Width - Ui.S(40)), Ui.S(80));
                _grid.Controls.Add(empty);
            }

            int contentHeight = Math.Max(0, top + Ui.S(4));
            _grid.AutoScrollMinSize = new Size(0, contentHeight);
            _grid.ResumeLayout();
            _rebuilding = false;
            try
            {
                if (keepScroll > 0)
                {
                    int maxScroll = Math.Max(0, contentHeight - _grid.ClientSize.Height);
                    _grid.AutoScrollPosition = new Point(0, Math.Min(keepScroll, maxScroll));
                }
            }
            catch
            {
            }
            _lastGridWidth = _grid.ClientSize.Width;
            _grid.Invalidate();
            UpdateButtons();
        }

        // ---------------- 选中与全选 ----------------

        private int CountSelected()
        {
            int n = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Selected)
                {
                    n++;
                }
            }
            return n;
        }

        private List<InstalledSkin> SelectedSkins()
        {
            List<InstalledSkin> list = new List<InstalledSkin>();
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Selected)
                {
                    list.Add(_all[i]);
                }
            }
            return list;
        }

        private void ToggleSelectAll()
        {
            List<InstalledSkin> visible = Filtered();
            if (visible.Count == 0)
            {
                return;
            }
            bool all = true;
            for (int i = 0; i < visible.Count; i++)
            {
                if (!visible[i].Selected)
                {
                    all = false;
                    break;
                }
            }
            for (int i = 0; i < visible.Count; i++)
            {
                visible[i].Selected = !all;
                if (!all)
                {
                    _selectedBases.Add(BaseFolder(visible[i].Folder));
                }
                else
                {
                    _selectedBases.Remove(BaseFolder(visible[i].Folder));
                }
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Selected = _cards[i].Skin.Selected;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_selectAll == null)
            {
                return;
            }
            List<InstalledSkin> visible = Filtered();
            int visibleSelected = 0;
            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i].Selected)
                {
                    visibleSelected++;
                }
            }
            bool allVisible = visible.Count > 0 && visibleSelected == visible.Count;
            _selectAll.Text = allVisible ? "取消勾选" : "全选";
            _selectAll.Primary = allVisible;
            _selectAll.Enabled = visible.Count > 0;

            int selected = CountSelected();
            bool any = selected > 0;
            _clearButton.Enabled = any;
            _openButton.Enabled = any;
            _toggleButton.Enabled = any;
            _restoreButton.Enabled = true;
            _backupButton.Enabled = _all.Count > 0;
            _updateButton.Enabled = !_updating;
            _updateAllButton.Enabled = !_updating;

            if (any)
            {
                bool allDisabled = true;
                bool allEnabled = true;
                for (int i = 0; i < _all.Count; i++)
                {
                    if (!_all[i].Selected)
                    {
                        continue;
                    }
                    if (_all[i].Disabled)
                    {
                        allEnabled = false;
                    }
                    else
                    {
                        allDisabled = false;
                    }
                }
                _toggleButton.Text = allDisabled ? "启用" : (allEnabled ? "禁用" : "禁用 / 启用");
            }
            else
            {
                _toggleButton.Text = "禁用 / 启用";
            }
            if (Content != null && Content.Width < Ui.S(1000))
            {
                _stats.Text = "共" + _all.Count + " · 筛" + visible.Count + " · 选" + selected;
            }
            else
            {
                _stats.Text = "共 " + _all.Count + " 个 · 当前 " + visible.Count + " 个 · 已选 " + selected + " 个";
            }
        }

        // ---------------- 数据更新 ----------------

        private void UpdateVehicleMeta(bool full)
        {
            if (_updating)
            {
                return;
            }
            _updating = true;
            _updateButton.Enabled = false;
            _updateAllButton.Enabled = false;
            _updateButton.Text = "更新中…";
            _updateButton.Invalidate();
            _metaLabel.Text = full ? "正在采集全部载具数据…" : "正在更新本机涂装载具数据…";

            // 本机涂装模式只给这些载具补图；全部模式按清单补齐。
            List<string> scope = new List<string>();
            if (!full)
            {
                for (int i = 0; i < _all.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_all[i].UnitId))
                    {
                        scope.Add(_all[i].UnitId);
                    }
                }
            }

            Thread thread = new Thread(delegate()
            {
                VehicleMetaUpdateResult result = null;
                IconUpdateResult icons = null;
                try
                {
                    result = VehicleMetaUpdater.Update(full, delegate(string text)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (_updating && _metaLabel != null)
                                {
                                    _metaLabel.Text = text;
                                }
                            });
                        }
                        catch
                        {
                        }
                    });
                }
                catch (Exception ex)
                {
                    result = new VehicleMetaUpdateResult();
                    result.Message = "更新失败：" + ex.Message;
                    Config.Log("载具数据更新异常：" + ex.Message);
                }
                // 等级权重采集结束后，同一任务继续补图标；两者共用同一份清单状态。
                try
                {
                    icons = VehicleIconUpdater.Update(full, full ? null : scope, delegate(string text)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (_updating && _metaLabel != null)
                                {
                                    _metaLabel.Text = text;
                                }
                            });
                        }
                        catch
                        {
                        }
                    });
                }
                catch (Exception ex)
                {
                    icons = new IconUpdateResult();
                    icons.Message = "图标更新失败：" + ex.Message;
                    Config.Log("载具图标更新异常：" + ex.Message);
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate { FinishVehicleMetaUpdate(result, icons); });
                }
                catch
                {
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void FinishVehicleMetaUpdate(VehicleMetaUpdateResult result, IconUpdateResult icons)
        {
            _updating = false;
            _updateButton.Enabled = true;
            _updateAllButton.Enabled = true;
            _updateButton.Text = "更新数据";
            _updateButton.Invalidate();
            _updateAllButton.Invalidate();
            VehicleMeta.Reload();
            VehicleCatalog.Reload();
            VehicleIcons.ClearCache();
            _metaLabel.Text = VehicleMeta.StatusText() + " · " + VehicleCatalog.CoverageShort();
            if (result != null && result.UnmatchedCount > 0)
            {
                _metaLabel.Text = _metaLabel.Text + " · 未匹配 " + result.UnmatchedCount;
            }
            if (_grid != null)
            {
                _grid.Invalidate(true);
            }
            UpdateButtons();
            bool paused = (result != null && result.Paused) || (icons != null && icons.Paused);
            if (paused)
            {
                string why = result != null && result.Paused ? result.Message : icons.Message;
                MessageBox.Show(this,
                    why + "\r\n\r\n请检查网络后再次点击「更新数据」，会从断点继续，已完成的条目不会重复请求。",
                    "更新已暂停", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                if (result != null && !string.IsNullOrEmpty(result.Message))
                {
                    Config.Log("界面载具数据更新结果：" + result.Message);
                }
                if (icons != null && !string.IsNullOrEmpty(icons.Message))
                {
                    Config.Log("界面载具图标更新结果：" + icons.Message);
                }
            }
        }

        /// <summary>「重试失败项」：清零计数后立即各请求一次。</summary>
        private void RetryFailedIcons()
        {
            if (_updating)
            {
                return;
            }
            _updating = true;
            _updateButton.Enabled = false;
            _updateAllButton.Enabled = false;
            _updateButton.Text = "重试中…";
            _updateButton.Invalidate();
            _metaLabel.Text = "正在重试失败的图标…";
            Thread thread = new Thread(delegate()
            {
                IconUpdateResult icons = null;
                try
                {
                    icons = VehicleIconUpdater.RetryFailed(delegate(string text)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (_updating && _metaLabel != null)
                                {
                                    _metaLabel.Text = text;
                                }
                            });
                        }
                        catch
                        {
                        }
                    });
                }
                catch (Exception ex)
                {
                    icons = new IconUpdateResult();
                    icons.Message = "重试失败：" + ex.Message;
                    Config.Log("重试图标异常：" + ex.Message);
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate { FinishVehicleMetaUpdate(null, icons); });
                }
                catch
                {
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void ShowMetaInfo()
        {
            try
            {
                using (IconStatusForm form = new IconStatusForm())
                {
                    form.ShowDialog(this);
                    if (form.RetryRequested)
                    {
                        RetryFailedIcons();
                    }
                }
            }
            catch (Exception ex)
            {
                Config.Log("显示载具数据详情失败：" + ex.Message);
            }
        }

        // ---------------- 管理操作 ----------------

        private void ClearSelected()
        {
            List<InstalledSkin> selected = SelectedSkins();
            if (selected.Count == 0)
            {
                return;
            }
            using (ClearSkinsForm confirm = new ClearSkinsForm(selected))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (confirm.BackupFirst)
                {
                    string root;
                    string error;
                    if (!BackupTo(selected, out root, out error))
                    {
                        MessageBox.Show(this, "备份失败，未执行清空：\r\n\r\n" + error, "清空文件夹",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            string deleteError;
            if (!DeleteSkins(selected, out deleteError))
            {
                MessageBox.Show(this, "清空失败，已回滚：\r\n\r\n" + deleteError, "清空文件夹",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                Reload();
                return;
            }
            for (int i = 0; i < selected.Count; i++)
            {
                _selectedBases.Remove(BaseFolder(selected[i].Folder));
            }
            Config.Log("清空涂装 " + selected.Count + " 个");
            Reload();
        }

        /// <summary>删除前先复制到程序目录的撤销副本；任何一步失败都把已删的恢复回来。</summary>
        private bool DeleteSkins(List<InstalledSkin> skins, out string error)
        {
            error = null;
            string root;
            try
            {
                root = Path.GetFullPath(Config.UserSkinsDir).TrimEnd('\\');
            }
            catch (Exception ex)
            {
                error = "无法解析 UserSkins 路径：" + ex.Message;
                return false;
            }
            string prefix = root + "\\";
            for (int i = 0; i < skins.Count; i++)
            {
                string full;
                try
                {
                    full = Path.GetFullPath(skins[i].Path);
                }
                catch (Exception ex)
                {
                    error = "路径无效：" + skins[i].Path + "（" + ex.Message + "）";
                    return false;
                }
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(skins[i].Folder) ||
                    skins[i].Folder.IndexOf('\\') >= 0 || skins[i].Folder.IndexOf('/') >= 0)
                {
                    error = "拒绝删除 UserSkins 之外的路径：" + full;
                    return false;
                }
                if (!Directory.Exists(full))
                {
                    error = "目录不存在：" + full;
                    return false;
                }
            }

            string undo = Path.Combine(Config.BackupDir, "clear_undo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            try
            {
                Directory.CreateDirectory(undo);
                for (int i = 0; i < skins.Count; i++)
                {
                    CopyDirectory(skins[i].Path, Path.Combine(undo, skins[i].Folder));
                }
            }
            catch (Exception ex)
            {
                Util.TryDeleteDirectory(undo);
                error = "无法创建撤销副本，未删除任何文件：" + ex.Message;
                return false;
            }

            List<InstalledSkin> deleted = new List<InstalledSkin>();
            for (int i = 0; i < skins.Count; i++)
            {
                // 先把当前目录也放进回滚列表：删除到一半失败时，它同样需要从撤销副本恢复。
                deleted.Add(skins[i]);
                try
                {
                    DeleteTree(skins[i].Path);
                }
                catch (Exception ex)
                {
                    string rollback;
                    bool restored = RestoreFromUndo(deleted, undo, out rollback);
                    if (restored)
                    {
                        Util.TryDeleteDirectory(undo);
                        error = "删除「" + skins[i].Folder + "」失败，已恢复先前删掉的涂装：" + ex.Message;
                    }
                    else
                    {
                        error = "删除「" + skins[i].Folder + "」失败，恢复也失败：" + ex.Message +
                                "\r\n撤销副本保留在：" + undo + "\r\n" + rollback;
                    }
                    return false;
                }
            }
            Util.TryDeleteDirectory(undo);
            return true;
        }

        private static bool RestoreFromUndo(List<InstalledSkin> deleted, string undo, out string error)
        {
            error = null;
            for (int i = deleted.Count - 1; i >= 0; i--)
            {
                try
                {
                    string source = Path.Combine(undo, deleted[i].Folder);
                    if (Directory.Exists(deleted[i].Path))
                    {
                        Util.TryDeleteDirectory(deleted[i].Path);
                    }
                    CopyDirectory(source, deleted[i].Path);
                }
                catch (Exception ex)
                {
                    error = deleted[i].Folder + "：" + ex.Message;
                    return false;
                }
            }
            return true;
        }

        private static void DeleteTree(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            try
            {
                Directory.Delete(dir, true);
                return;
            }
            catch
            {
                // 只读文件：去掉只读属性后再试一次。
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
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
                Directory.Delete(dir, true);
            }
        }

        private void OpenSelected()
        {
            List<InstalledSkin> selected = SelectedSkins();
            if (selected.Count == 0)
            {
                return;
            }
            string path = selected[0].Path;
            if (!Directory.Exists(path))
            {
                MessageBox.Show(this, "目录不存在：\r\n\r\n" + path, "打开文件夹",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Reload();
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开资源管理器：" + ex.Message, "打开文件夹",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BackupClicked()
        {
            List<InstalledSkin> selected = SelectedSkins();
            bool all = selected.Count == 0;
            List<InstalledSkin> targets = all ? new List<InstalledSkin>(_all) : selected;
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "UserSkins 里还没有可备份的涂装。", "备份",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string preview = Path.Combine(InstalledSkins.BackupRootDir,
                                          DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string text = "将备份 " + targets.Count + " 个涂装（" + (all ? "全部" : "选中的") + "）到：\r\n\r\n" +
                          preview + "\r\n\r\n备份保留原目录结构，不写入战雷目录。";
            if (MessageBox.Show(this, text, "备份", MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                return;
            }
            string root;
            string error;
            if (!BackupTo(targets, out root, out error))
            {
                MessageBox.Show(this, "备份失败，已清理未完成的副本：\r\n\r\n" + error, "备份",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Config.Log("备份涂装 " + targets.Count + " 个 -> " + root);
            MessageBox.Show(this, "已备份 " + targets.Count + " 个涂装到：\r\n\r\n" + root +
                            "\r\n\r\n可以从「还原备份」把文件复制回 UserSkins。",
                            "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool BackupTo(List<InstalledSkin> skins, out string root, out string error)
        {
            root = Path.Combine(InstalledSkins.BackupRootDir, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            error = null;
            try
            {
                if (Directory.Exists(root))
                {
                    root = Path.Combine(InstalledSkins.BackupRootDir,
                                        DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                }
                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                error = "无法创建备份目录：" + root + "（" + ex.Message + "）";
                return false;
            }
            for (int i = 0; i < skins.Count; i++)
            {
                try
                {
                    CopyDirectory(skins[i].Path, Path.Combine(root, skins[i].Folder));
                }
                catch (Exception ex)
                {
                    Util.TryDeleteDirectory(root);
                    error = skins[i].Folder + "：" + ex.Message;
                    return false;
                }
            }
            return true;
        }

        private void RestoreBackup()
        {
            string root = InstalledSkins.BackupRootDir;
            if (!Directory.Exists(root))
            {
                MessageBox.Show(this, "还没有备份。\r\n\r\n默认备份目录：" + root, "还原备份",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (RestoreBackupsForm form = new RestoreBackupsForm(root))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string run = form.SelectedRun;
                if (string.IsNullOrEmpty(run) || !Directory.Exists(run))
                {
                    return;
                }
                string userSkins = Config.UserSkinsDir;
                if (string.IsNullOrEmpty(userSkins))
                {
                    MessageBox.Show(this, "还没有设置正确的游戏目录。", "还原备份",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string[] dirs = Directory.GetDirectories(run);
                if (dirs.Length == 0)
                {
                    return;
                }
                string list = "";
                for (int i = 0; i < dirs.Length && i < 12; i++)
                {
                    list += "· " + Path.GetFileName(dirs[i]) + "\r\n";
                }
                if (dirs.Length > 12)
                {
                    list += "… 等 " + dirs.Length + " 个\r\n";
                }
                string text = "将把 " + dirs.Length + " 个涂装还原到：\r\n\r\n" + userSkins +
                              "\r\n\r\n" + list + "\r\n同名文件夹会跳过，不会覆盖。";
                if (MessageBox.Show(this, text, "还原备份", MessageBoxButtons.OKCancel,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
                {
                    return;
                }
                List<string> created = new List<string>();
                List<string> skipped = new List<string>();
                string error = null;
                for (int i = 0; i < dirs.Length; i++)
                {
                    string name = Path.GetFileName(dirs[i]);
                    if (string.IsNullOrEmpty(name) || name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
                    {
                        continue;
                    }
                    string target = Path.Combine(userSkins, name);
                    if (Directory.Exists(target))
                    {
                        skipped.Add(name);
                        continue;
                    }
                    try
                    {
                        // 先登记目标再复制：复制到一半失败时，回滚才能把它清掉。
                        created.Add(target);
                        CopyDirectory(dirs[i], target);
                    }
                    catch (Exception ex)
                    {
                        error = name + "：" + ex.Message;
                        break;
                    }
                }
                if (error != null)
                {
                    for (int i = 0; i < created.Count; i++)
                    {
                        Util.TryDeleteDirectory(created[i]);
                    }
                    MessageBox.Show(this, "还原失败，已移除本次新建的文件夹：\r\n\r\n" + error, "还原备份",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    string note = "已还原 " + created.Count + " 个涂装。";
                    if (skipped.Count > 0)
                    {
                        note += "\r\n跳过 " + skipped.Count + " 个同名文件夹：" + string.Join("、", skipped.ToArray());
                    }
                    MessageBox.Show(this, note, "还原备份", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                Reload();
            }
        }

        private void ToggleSelected()
        {
            List<InstalledSkin> selected = SelectedSkins();
            if (selected.Count == 0)
            {
                return;
            }
            string list = "";
            for (int i = 0; i < selected.Count; i++)
            {
                string target = selected[i].Disabled
                    ? selected[i].Path.Substring(0, selected[i].Path.Length - InstalledSkins.BanSuffix.Length)
                    : selected[i].Path + InstalledSkins.BanSuffix;
                list += "· " + selected[i].Path + "\r\n  → " + target + "\r\n";
            }
            string text = "将对 " + selected.Count + " 个涂装切换禁用/启用：" +
                          "\r\n\r\n" + list +
                          "\r\n只重命名文件夹（追加/去掉 " + InstalledSkins.BanSuffix + "），不改目录内容。";
            if (MessageBox.Show(this, text, "禁用 / 启用", MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                return;
            }

            List<InstalledSkin> done = new List<InstalledSkin>();
            List<bool> doneWasDisabled = new List<bool>();
            string error = null;
            for (int i = 0; i < selected.Count; i++)
            {
                bool wasDisabled = selected[i].Disabled;
                string message;
                bool ok = wasDisabled
                    ? InstalledSkins.Enable(selected[i], out message)
                    : InstalledSkins.Disable(selected[i], out message);
                if (!ok)
                {
                    error = selected[i].Folder + "：" + message;
                    break;
                }
                ApplyToggleState(selected[i], !wasDisabled);
                done.Add(selected[i]);
                doneWasDisabled.Add(wasDisabled);
            }
            if (error != null)
            {
                string rollbackError = null;
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    string message;
                    bool ok = doneWasDisabled[i]
                        ? InstalledSkins.Disable(done[i], out message)
                        : InstalledSkins.Enable(done[i], out message);
                    if (ok)
                    {
                        ApplyToggleState(done[i], doneWasDisabled[i]);
                    }
                    else if (rollbackError == null)
                    {
                        rollbackError = done[i].Folder + "：" + message;
                    }
                }
                string note = "操作失败，已回滚：" + error;
                if (rollbackError != null)
                {
                    note += "\r\n回滚时又遇到问题：" + rollbackError;
                }
                MessageBox.Show(this, note, "涂装状态", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                Config.Log("切换禁用/启用 " + done.Count + " 个");
            }
            Reload();
        }

        /// <summary>重命名成功后同步对象状态，回滚时按真实状态反向操作。</summary>
        private static void ApplyToggleState(InstalledSkin skin, bool disabled)
        {
            string path = skin.Path;
            string folder = skin.Folder;
            if (disabled)
            {
                if (!path.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    path += InstalledSkins.BanSuffix;
                }
                if (!folder.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    folder += InstalledSkins.BanSuffix;
                }
            }
            else
            {
                if (path.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - InstalledSkins.BanSuffix.Length);
                }
                if (folder.EndsWith(InstalledSkins.BanSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    folder = folder.Substring(0, folder.Length - InstalledSkins.BanSuffix.Length);
                }
            }
            skin.Path = path;
            skin.Folder = folder;
            skin.Disabled = disabled;
        }

        private static void CopyDirectory(string source, string target)
        {
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException("目录不存在：" + source);
            }
            Directory.CreateDirectory(target);
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            string root = source.TrimEnd('\\');
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i].Substring(root.Length).TrimStart('\\');
                string dest = Path.Combine(target, rel);
                string dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(Util.LongPath(files[i]), Util.LongPath(dest), true);
            }
        }
    }

    /// <summary>左侧国家筛选项：选中时主题黄高亮，右侧显示数量。</summary>
    internal class NationItem : Control
    {
        public string Code = "";
        public string Label = "";
        public int Count;
        private bool _selected;
        private bool _hover;

        public event EventHandler Picked;

        public NationItem()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.BgSoft;
            Font = Ui.Body;
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = Ui.S(32);
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_selected)
            {
                Ui.FillRound(g, r, Ui.S(7), Ui.Mix(Ui.BgSoft, Ui.Accent, 0.18f));
                Ui.FillRound(g, new Rectangle(r.X, r.Y + Ui.S(6), Ui.S(3), r.Height - Ui.S(12)), Ui.S(2), Ui.Accent);
            }
            else if (_hover)
            {
                Ui.FillRound(g, r, Ui.S(7), Ui.Panel);
            }
            Color text = _selected ? Ui.Accent : Ui.TextDim;
            Ui.DrawText(g, Label, _selected ? Ui.BodyBold : Ui.Body, text,
                    new Rectangle(Ui.S(12), 0, Width - Ui.S(56), Height), Ui.LeftMiddle);
            Ui.DrawText(g, Count.ToString(CultureInfo.InvariantCulture), Ui.SmallBold, text,
                    new Rectangle(Width - Ui.S(46), 0, Ui.S(34), Height), Ui.RightMiddle);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (Picked != null)
            {
                Picked(this, EventArgs.Empty);
            }
            base.OnClick(e);
        }
    }

    /// <summary>管理卡片：左图右文，标题两行，四行键值（国家/等级·权重/机型代号/文件夹），状态标签，右上角圆形勾选框。</summary>
    internal class SkinCard : Control
    {
        private const int Pad = 11;
        // 图标条：铺满卡片宽度、固定高度 88（设计像素）——维基渲染图多为 2:1~3:1 宽幅，
        // 只有整条铺开才能在以“contain”显示时保持 72~96px 的显示高度，不裁切也不拉伸。
        private const int BandH = 88;
        private const int Gap = 12;
        private const int LabelW = 64;
        private const int RowH = 22;
        private const int TitleLineH = 21;
        private const int FullLineH = 18;
        private const int CheckSize = 22;
        private const int CheckHit = 32;

        private bool _selected;
        private bool _hover;
        private bool _checkHot;
        public InstalledSkin Skin;

        public event EventHandler SelectionChanged;

        public SkinCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.Bg;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value)
                {
                    return;
                }
                _selected = value;
                Invalidate();
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        private static int TextWidth(int cardWidth)
        {
            return Math.Max(Ui.S(80), cardWidth - Ui.S(Pad) * 2);
        }

        private static int TitleWidth(int cardWidth)
        {
            return Math.Max(Ui.S(60), TextWidth(cardWidth) - Ui.S(38));
        }

        private static int FolderValueWidth(int cardWidth)
        {
            return Math.Max(Ui.S(40), TextWidth(cardWidth) - Ui.S(LabelW) - Ui.S(6));
        }

        private int FullLines()
        {
            string full = Skin == null ? "" : Skin.FullName;
            string shortName = Skin == null ? "" : Skin.ShortName;
            if (string.IsNullOrEmpty(full) ||
                string.Equals(full.Trim(), shortName.Trim(), StringComparison.Ordinal))
            {
                // 简称与全名完全相同时不显示第二行；只差类别前缀时仍显示。
                return 0;
            }
            int n = Ui.WrapLines(full, Ui.CardSub, TitleWidth(Width)).Count;
            return Math.Min(2, Math.Max(1, n));
        }

        private int FolderLines()
        {
            string folder = Skin == null ? "" : Skin.Folder;
            if (string.IsNullOrEmpty(folder))
            {
                return 1;
            }
            int n = Ui.WrapLines(folder, Ui.CardValue, FolderValueWidth(Width)).Count;
            return Math.Max(1, n);
        }

        private int ModelLines()
        {
            string model = Skin == null ? "" : Skin.UnitId;
            if (string.IsNullOrEmpty(model))
            {
                return 1;
            }
            int n = Ui.WrapLines(model, Ui.CardValue, FolderValueWidth(Width)).Count;
            return Math.Max(1, n);
        }

        /// <summary>测量卡片高度：图标条 + 四行键值完整显示；设计高度限制在 250~340px（再乘 Ui.S）。</summary>
        private int MeasureHeightExact(int cardWidth)
        {
            int titleH = Ui.S(TitleLineH) + FullLines() * Ui.S(FullLineH);
            int rows = Ui.S(RowH) * 2 + Math.Max(Ui.S(RowH), ModelLines() * Ui.S(FullLineH)) +
                       Math.Max(Ui.S(RowH), FolderLines() * Ui.S(FullLineH));
            int statusH = Ui.S(20) + Ui.S(4);
            int total = Ui.S(Pad) * 2 + Ui.S(BandH) + Ui.S(10) + titleH + Ui.S(8) + rows + statusH;
            if (total < Ui.S(250))
            {
                total = Ui.S(250);
            }
            if (total > Ui.S(340))
            {
                total = Ui.S(340);
            }
            return total;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _checkHot = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = CheckHitRect().Contains(e.Location);
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
                // 卡片任意位置（含右上角勾选框）只在这一次点击里切换一次。
                Focus();
                Selected = !Selected;
                return;
            }
            base.OnMouseDown(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused && (keyData == Keys.Space || keyData == Keys.Enter))
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

        private Rectangle CheckHitRect()
        {
            return new Rectangle(Width - Ui.S(Pad) - Ui.S(CheckHit), Ui.S(Pad), Ui.S(CheckHit), Ui.S(CheckHit));
        }

        /// <summary>
        /// 每次绘制都问一次 VehicleIcons：缓存按 LRU 淘汰并释放旧位图，
        /// 卡片自己长期持有 Bitmap 会拿到已释放对象，所以这里不缓存。
        /// </summary>
        private Bitmap Icon()
        {
            return Skin == null ? VehicleIcons.Placeholder : VehicleIcons.Get(Skin.UnitId);
        }

        private static Rectangle Fit(Size image, Rectangle box)
        {
            if (image.Width <= 0 || image.Height <= 0)
            {
                return new Rectangle(box.X, box.Y, 1, 1);
            }
            float scale = Math.Min((float)box.Width / image.Width, (float)box.Height / image.Height);
            int w = Math.Max(1, (int)(image.Width * scale));
            int h = Math.Max(1, (int)(image.Height * scale));
            return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Smooth(g);
            bool disabled = Skin != null && Skin.Disabled;

            Rectangle shadow = new Rectangle(Ui.S(2), Ui.S(3), Width - Ui.S(5), Height - Ui.S(5));
            Ui.FillRound(g, shadow, Ui.S(10), Color.FromArgb(18, 0, 0, 0));
            Rectangle r = new Rectangle(0, 0, Width - Ui.S(3), Height - Ui.S(3));
            Color card = _hover ? Ui.PanelHover : Ui.CardBg;
            Ui.FillRound(g, r, Ui.S(10), card);

            int pad = Ui.S(Pad);
            Rectangle imageBox = new Rectangle(pad, pad, Width - pad * 2, Ui.S(BandH));
            Ui.FillRound(g, imageBox, Ui.S(7), Ui.BgSoft);
            Bitmap shownIcon = Icon();
            if (shownIcon != null)
            {
                Rectangle target = Fit(shownIcon.Size, new Rectangle(imageBox.X + Ui.S(4), imageBox.Y + Ui.S(5),
                                                                     imageBox.Width - Ui.S(8), imageBox.Height - Ui.S(10)));
                InterpolationMode old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(shownIcon, target);
                g.InterpolationMode = old;
            }
            else
            {
                Ui.DrawText(g, "暂无图标", Ui.Small, Ui.TextFaint, imageBox, Ui.Center);
            }

            int textX = pad;
            int textW = TextWidth(Width);
            int y = pad + Ui.S(BandH) + Ui.S(10);
            string shortName = Skin == null ? "" : Skin.ShortName;
            string fullName = Skin == null ? "" : Skin.FullName;
            if (string.IsNullOrEmpty(fullName))
            {
                fullName = Skin == null ? "" : Skin.Folder;
            }
            Ui.DrawText(g, shortName, Ui.CardTitle, Ui.Text,
                    new Rectangle(textX, y, TitleWidth(Width), Ui.S(TitleLineH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            y += Ui.S(TitleLineH);
            int fullLines = FullLines();
            List<string> fullWrap = Ui.WrapLines(fullName, Ui.CardSub, TitleWidth(Width));
            for (int i = 0; i < fullWrap.Count && i < fullLines; i++)
            {
                Ui.DrawText(g, fullWrap[i], Ui.CardSub, Ui.TextDim,
                        new Rectangle(textX, y + i * Ui.S(FullLineH), TitleWidth(Width), Ui.S(FullLineH)),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }
            y += fullLines * Ui.S(FullLineH) + Ui.S(8);

            DrawKeyValue(g, "国家", Skin == null ? "" : Skin.NationName, Skin == null ? "" : Skin.Nation, textX, textW, ref y, false);
            DrawLevelWeight(g, textX, textW, ref y);
            DrawKeyValue(g, "机型代号", Skin == null ? "" : Skin.UnitId, "", textX, textW, ref y, false);
            DrawFolder(g, textX, textW, ref y);

            if (disabled)
            {
                Ui.FillRound(g, r, Ui.S(10), Color.FromArgb(72, Ui.Bg));
            }

            Color statusColor = disabled ? Ui.Bad : Ui.Good;
            Color statusFill = disabled ? Ui.BadBg : Ui.GoodBg;
            string statusText = disabled ? "已禁用" : "已启用";
            int statusW = TextRenderer.MeasureText(statusText, Ui.SmallBold).Width + Ui.S(14);
            Rectangle pill = new Rectangle(textX, Math.Min(y + Ui.S(3), Height - Ui.S(26)), statusW, Ui.S(20));
            Ui.FillRound(g, pill, Ui.S(10), statusFill);
            Ui.DrawRound(g, pill, Ui.S(10), statusColor, 1f);
            Ui.DrawText(g, statusText, Ui.SmallBold, statusColor, pill, Ui.Center);

            Ui.DrawRound(g, r, Ui.S(10), _selected ? Ui.Accent : Ui.LineSoft, _selected ? 1.6f : 1f);
            if (Focused && !_selected)
            {
                Ui.DrawRound(g, new Rectangle(Ui.S(2), Ui.S(2), Width - Ui.S(7), Height - Ui.S(7)),
                             Ui.S(9), Ui.Accent, 1f);
            }

            Rectangle hit = CheckHitRect();
            Rectangle circle = new Rectangle(hit.X + (hit.Width - Ui.S(CheckSize)) / 2, hit.Y + (hit.Height - Ui.S(CheckSize)) / 2,
                                             Ui.S(CheckSize), Ui.S(CheckSize));
            if (_selected)
            {
                Ui.FillRound(g, circle, circle.Width / 2, Ui.Accent);
                using (Pen pen = new Pen(Ui.T.CheckMark, Math.Max(1.6f, Ui.Scale * 1.9f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new Point[]
                    {
                        new Point(circle.X + Ui.S(5), circle.Y + Ui.S(10)),
                        new Point(circle.X + Ui.S(8), circle.Y + Ui.S(14)),
                        new Point(circle.X + Ui.S(16), circle.Y + Ui.S(6))
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
        }

        private void DrawKeyValue(Graphics g, string label, string value, string nationCode,
                                  int textX, int textW, ref int y, bool wrap)
        {
            Ui.DrawText(g, label, Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(textX, y, Ui.S(LabelW), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            int valueX = textX + Ui.S(LabelW) + Ui.S(6);
            int available = textX + textW - valueX;
            if (!string.IsNullOrEmpty(nationCode))
            {
                int flagW = Ui.S(19);
                int flagH = Ui.S(13);
                Rectangle flagRect = new Rectangle(valueX, y + (Ui.S(RowH) - flagH) / 2, flagW, flagH);
                NationInfo.Draw(g, flagRect, nationCode);
                valueX = flagRect.Right + Ui.S(6);
                available = textX + textW - valueX;
            }
            string shown = string.IsNullOrEmpty(value) ? "未知" : value;
            List<string> lines = Ui.WrapLines(shown, Ui.CardValue, available);
            int lineH = Ui.S(FullLineH);
            int contentH = lines.Count * lineH;
            int rowH = Math.Max(Ui.S(RowH), contentH);
            int textTop = y + (rowH - contentH) / 2;
            for (int i = 0; i < lines.Count; i++)
            {
                Ui.DrawText(g, lines[i], Ui.CardValue, string.IsNullOrEmpty(value) ? Ui.TextFaint : Ui.Text,
                        new Rectangle(valueX, textTop + i * lineH, available, lineH),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }
            y += rowH;
        }

        /// <summary>等级 / 权重固定一行：例如「VII · 11.7」；缺数据显示「—」。</summary>
        private void DrawLevelWeight(Graphics g, int textX, int textW, ref int y)
        {
            Ui.DrawText(g, "等级 / 权重", Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(textX, y, Ui.S(LabelW), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            int valueX = textX + Ui.S(LabelW) + Ui.S(6);
            int available = Math.Max(Ui.S(10), textX + textW - valueX);
            string value = VehicleMeta.Display(Skin == null ? "" : Skin.UnitId);
            Color color = value == "—" ? Ui.TextFaint : Ui.TextDim;
            Ui.DrawText(g, value, Ui.Small, color,
                    new Rectangle(valueX, y, available, Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            y += Ui.S(RowH);
        }

        private void DrawFolder(Graphics g, int textX, int textW, ref int y)
        {
            string folder = Skin == null ? "" : Skin.Folder;
            bool fallback = string.IsNullOrEmpty(folder);
            string shown = fallback ? "未知" : folder;
            Ui.DrawText(g, "文件夹", Ui.CardLabel, Ui.TextFaint,
                    new Rectangle(textX, y, Ui.S(LabelW), Ui.S(RowH)),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            int valueX = textX + Ui.S(LabelW) + Ui.S(6);
            int available = textX + textW - valueX;
            List<string> lines = Ui.WrapLines(shown, Ui.CardValue, available);
            int lineH = Ui.S(FullLineH);
            int contentH = lines.Count * lineH;
            int rowH = Math.Max(Ui.S(RowH), contentH);
            int textTop = y + (rowH - contentH) / 2;
            for (int i = 0; i < lines.Count; i++)
            {
                Ui.DrawText(g, lines[i], Ui.CardValue, fallback ? Ui.TextFaint : Ui.Text,
                        new Rectangle(valueX, textTop + i * lineH, available, lineH),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }
            y += rowH;
        }

        /// <summary>实际高度按当前宽度再测一次，供网格在布局后微调。</summary>
        public int PreferredHeight()
        {
            return MeasureHeightExact(Width);
        }
    }

    /// <summary>清空确认：列出名称与完整路径，可先备份，默认按钮是取消。</summary>
    internal class ClearSkinsForm : ModalForm
    {
        public bool BackupFirst;

        public ClearSkinsForm(List<InstalledSkin> skins)
            : base("清空文件夹", 660, 520)
        {
            MakeLabel("将删除 " + skins.Count + " 个涂装", 24, 12, 600, 26, Ui.H1, Ui.Bad);
            MakeLabel("作用范围仅限当前勾选的涂装；已禁用的涂装也会一并删除。", 24, 40, 610, 20, Ui.Small, Ui.TextDim);

            ListBox list = new ListBox();
            list.Left = Ui.S(24);
            list.Top = Ui.S(66);
            list.Width = Ui.S(612);
            list.Height = Ui.S(250);
            list.BackColor = Ui.BgSoft;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.None;
            list.Font = Ui.Mono;
            for (int i = 0; i < skins.Count; i++)
            {
                list.Items.Add(skins[i].Folder + "    " + skins[i].Path);
            }
            Content.Controls.Add(list);

            CheckBox backup = new CheckBox();
            backup.Text = "先备份再清空";
            backup.Left = Ui.S(24);
            backup.Top = Ui.S(326);
            backup.Width = Ui.S(240);
            backup.Height = Ui.S(26);
            backup.ForeColor = Ui.Text;
            backup.BackColor = Ui.Panel;
            backup.FlatStyle = FlatStyle.Flat;
            backup.Checked = true;
            Content.Controls.Add(backup);
            BackupFirst = true;
            backup.CheckedChanged += delegate { BackupFirst = backup.Checked; };

            MakeLabel("删除后无法撤销（除非勾选了备份）。按 Enter 或 Esc 都执行「取消」。",
                      24, 356, 600, 40, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton("取消", 426, 418, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("删除", 530, 418, 96, false);
            ok.Danger = true;
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            AcceptButton = null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    /// <summary>选择要还原的备份批次。</summary>
    internal class RestoreBackupsForm : ModalForm
    {
        public string SelectedRun = "";
        private readonly ListBox _list = new ListBox();
        private readonly List<string> _runs = new List<string>();

        public RestoreBackupsForm(string root)
            : base("还原备份", 680, 560)
        {
            MakeLabel("备份目录：" + root, 24, 12, 620, 22, Ui.Small, Ui.TextDim);
            MakeLabel("选择要还原的批次；同名文件夹会跳过，不会覆盖。", 24, 38, 620, 22, Ui.Small, Ui.TextFaint);

            _list.Left = Ui.S(24);
            _list.Top = Ui.S(68);
            _list.Width = Ui.S(632);
            _list.Height = Ui.S(350);
            _list.BackColor = Ui.BgSoft;
            _list.ForeColor = Ui.Text;
            _list.BorderStyle = BorderStyle.None;
            _list.Font = Ui.Body;
            _list.DoubleClick += delegate
            {
                if (_list.SelectedIndex >= 0)
                {
                    SelectedRun = _runs[_list.SelectedIndex];
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            Content.Controls.Add(_list);

            string[] dirs = Directory.Exists(root) ? Directory.GetDirectories(root) : new string[0];
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = dirs.Length - 1; i >= 0; i--)
            {
                string name = Path.GetFileName(dirs[i]);
                string[] skins = Directory.GetDirectories(dirs[i]);
                long size = 0;
                for (int s = 0; s < skins.Length; s++)
                {
                    size += Util.DirectorySize(skins[s]);
                }
                _list.Items.Add(name + "    (" + skins.Length + " 个涂装, " + Util.HumanSize(size) + ")");
                _runs.Add(dirs[i]);
            }
            if (_runs.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
            else
            {
                _list.Items.Add("（这个目录里还没有备份）");
            }

            FlatButton cancel = MakeButton("取消", 444, 438, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("还原", 550, 438, 104, true);
            ok.Enabled = _runs.Count > 0;
            ok.Click += delegate
            {
                if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _runs.Count)
                {
                    return;
                }
                SelectedRun = _runs[_list.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };
            AcceptButton = null;
        }
    }

    /// <summary>发现异常文件：列出非法与警告文件，默认取消。</summary>
    internal class AbnormalFilesForm : ModalForm
    {
        public AbnormalFilesForm(ValidationResult result)
            : base("发现异常文件", 700, 600)
        {
            MakeLabel("这个包里有 " + result.Illegal.Count + " 个未通过校验的文件", 24, 12, 620, 26, Ui.H1, Ui.Warn);
            MakeLabel("允许的类型：" + AllowedText() + "。下面这些文件可能有风险，是否仍然安装？",
                      24, 42, 640, 22, Ui.Small, Ui.TextDim);

            ListBox list = new ListBox();
            list.Left = Ui.S(24);
            list.Top = Ui.S(70);
            list.Width = Ui.S(640);
            list.Height = Ui.S(190);
            list.BackColor = Ui.BgSoft;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.None;
            list.Font = Ui.Mono;
            for (int i = 0; i < result.Illegal.Count; i++)
            {
                list.Items.Add("! " + result.Illegal[i]);
            }
            Content.Controls.Add(list);

            int top = 270;
            if (result.Warn.Count > 0)
            {
                MakeLabel("已忽略的说明文件（不会安装）：", 24, top, 400, 20, Ui.SmallBold, Ui.TextDim);
                top += 24;
                ListBox warns = new ListBox();
                warns.Left = Ui.S(24);
                warns.Top = Ui.S(top);
                warns.Width = Ui.S(640);
                warns.Height = Ui.S(Math.Min(90, 22 + result.Warn.Count * 18));
                warns.BackColor = Ui.BgSoft;
                warns.ForeColor = Ui.TextFaint;
                warns.BorderStyle = BorderStyle.None;
                warns.Font = Ui.Small;
                for (int i = 0; i < result.Warn.Count; i++)
                {
                    warns.Items.Add("· " + result.Warn[i]);
                }
                Content.Controls.Add(warns);
                top += warns.Height + 10;
            }

            MakeLabel("选「仍然安装」会按原包内容继续，完成后会标注未通过校验的文件数量。",
                      24, top, 640, 40, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton("取消", 460, 470, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("仍然安装", 566, 470, 110, true);
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            AcceptButton = null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static string AllowedText()
        {
            string[] parts = Config.AllowedExt.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string text = "";
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    text += " / ";
                }
                text += parts[i].Trim();
            }
            return text.Length == 0 ? ".dds / .blk / .tga" : text;
        }
    }
}
