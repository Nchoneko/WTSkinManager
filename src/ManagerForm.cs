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
    /// 涂装管理器：左侧国家筛选与涂装库统计 + 顶部工具条 + 载具卡片网格 + 右栏涂装列表。
    /// 只枚举 UserSkins 下的载具目录与涂装子目录；本页不提供目录改名，也不读写 .BAN，
    /// 已有的 .BAN 目录按普通涂装原样显示。
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
        private BufferedPanel _searchPanel;
        private FlatButton _sortButton;
        private FlatButton _selectAll;
        private FlatButton _clearButton;
        private FlatButton _openButton;
        private FlatButton _backupButton;
        private FlatButton _restoreButton;
        private FlatButton _refreshButton;
        private FlatButton _updateButton;
        private FlatButton _updateAllButton;
        private Label _stats;
        private Label _metaLabel;
        private Label _libStats;
        private string _nationFilter = "";
        private int _sortMode;
        private bool _rebuilding;
        private bool _updating;
        private int _lastGridWidth;

        // ---- 右栏：当前载具的已安装涂装列表 ----
        private BufferedPanel _detail;
        private BufferedPanel _detailList;
        private FlatButton _detailSelectAll;
        private FlatButton _detailBackup;
        private FlatButton _detailDelete;
        private FlatButton _detailRename;
        private FlatButton _detailClose;
        private FlatButton _detailSort;
        private FlatButton _detailRecalc;
        private Label _detailCount;
        private Label _detailEmpty;
        private bool _detailOpen;
        private string _detailFolder = "";
        private ManagerVehicleEntry _detailVehicle;
        private readonly List<ManagerSkinRow> _detailRows = new List<ManagerSkinRow>();
        private ManagerLibrary _library;
        private int _detailSortMode;
        private int _measureGeneration = -1;
        private System.Windows.Forms.Timer _visibilityTimer;
        private bool _wasHidden;

        public ManagerForm()
            : base("涂装管理", 1320, 860)
        {
            Content.BackColor = Ui.Bg;
            BuildSidebar();
            BuildToolbar();
            BuildGrid();
            BuildDetailPanel();
            Shown += delegate { Reload(); };
            Resize += delegate { LayoutManager(); };
            _visibilityTimer = new System.Windows.Forms.Timer();
            _visibilityTimer.Interval = 1500;
            _visibilityTimer.Tick += delegate { CheckVisibilityRefresh(); };
            _visibilityTimer.Start();
            LayoutManager();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _visibilityTimer != null)
            {
                _visibilityTimer.Stop();
                _visibilityTimer.Dispose();
                _visibilityTimer = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 嵌入主窗口时，切回管理页（父容器重新可见）就重扫一次：
        /// 导入完成后回到管理页会立即看到新涂装，并重新统计占用。
        /// 管理页可见期间不自动刷新，避免打断用户的勾选。
        /// </summary>
        private void CheckVisibilityRefresh()
        {
            if (_detail == null || IsDisposed)
            {
                return;
            }
            bool visible;
            try
            {
                visible = Visible;
            }
            catch
            {
                return;
            }
            if (!visible)
            {
                _wasHidden = true;
                return;
            }
            if (_wasHidden)
            {
                _wasHidden = false;
                Reload();
            }
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

            _libStats = new Label();
            _libStats.Text = "已安装 0 个涂装 · 占用 计算中…";
            _libStats.Font = Ui.Small;
            _libStats.ForeColor = Ui.TextFaint;
            _libStats.BackColor = Ui.BgSoft;
            _libStats.AutoSize = false;
            _libStats.AutoEllipsis = true;
            _libStats.TextAlign = ContentAlignment.MiddleLeft;
            _libStats.SetBounds(Ui.S(20), Ui.S(44), Ui.S(160), Ui.S(18));
            _side.Controls.Add(_libStats);

            Label tip = new Label();
            tip.Text = "按国家筛选";
            tip.Font = Ui.Small;
            tip.ForeColor = Ui.TextFaint;
            tip.BackColor = Ui.BgSoft;
            tip.AutoSize = false;
            tip.SetBounds(Ui.S(20), Ui.S(72), Ui.S(150), Ui.S(18));
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
            _searchPanel = new BufferedPanel();
            _searchPanel.BackColor = Ui.Bg;
            _searchPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Ui.Smooth(e.Graphics);
                Rectangle r = new Rectangle(0, 0, _searchPanel.Width - 1, _searchPanel.Height - 1);
                Ui.FillRound(e.Graphics, r, Ui.S(9), Ui.Panel);
                bool focus = _search != null && _search.Focused;
                Ui.DrawRound(e.Graphics, r, Ui.S(9), focus ? Ui.Accent : Ui.Stroke,
                             focus ? Math.Max(Ui.BorderWidth, (int)Math.Round(Ui.SF(1.4f))) : Ui.BorderWidth);
            };
            _search = new TextBox();
            _search.BorderStyle = BorderStyle.None;
            _search.BackColor = Ui.Panel;
            _search.ForeColor = Ui.Text;
            _search.Font = Ui.Body;
            Native.SetCue(_search, "搜索涂装 / 载具 / 文件夹");
            _search.TextChanged += delegate { RebuildGrid(); UpdateButtons(); };
            _search.GotFocus += delegate { _searchPanel.Invalidate(); };
            _search.LostFocus += delegate { _searchPanel.Invalidate(); };
            _searchPanel.Controls.Add(_search);
            _searchPanel.Resize += delegate
            {
                _search.SetBounds(Ui.S(12), Ui.S(7),
                                  Math.Max(Ui.S(40), _searchPanel.Width - Ui.S(24)), Ui.S(20));
            };
            Content.Controls.Add(_searchPanel);

            _sortButton = ToolButton("排序：文件夹", false);
            _sortButton.Click += delegate
            {
                _sortMode = (_sortMode + 1) % 2;
                _sortButton.Text = _sortMode == 0 ? "排序：文件夹" : "排序：载具";
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

        /// <summary>右栏详情：当前载具的已安装涂装列表（与主窗口「涂装包详情」同一张卡片样式）。</summary>
        private void BuildDetailPanel()
        {
            _detail = new BufferedPanel();
            // 圆角外的四角要透出页面底色；若用 CardBg 会填满矩形，圆角看起来是断的/发黑。
            _detail.BackColor = Ui.Bg;
            _detail.Visible = false;
            _detail.Paint += delegate(object sender, PaintEventArgs e) { PaintDetail(e.Graphics); };
            Content.Controls.Add(_detail);

            _detailList = new BufferedPanel();
            _detailList.BackColor = Ui.CardBg;
            _detailList.AutoScroll = true;
            _detailList.Visible = false;
            _detailList.Resize += delegate { LayoutDetailRows(); };
            _detail.Controls.Add(_detailList);

            _detailSelectAll = new FlatButton();
            _detailSelectAll.Text = "全选";
            _detailSelectAll.Font = Ui.SmallBold;
            _detailSelectAll.Click += delegate { DetailSelectAll(); };
            _detail.Controls.Add(_detailSelectAll);

            _detailBackup = new FlatButton();
            _detailBackup.Text = "备份所选";
            _detailBackup.Font = Ui.SmallBold;
            _detailBackup.Click += delegate { DetailBackupClicked(); };
            _detail.Controls.Add(_detailBackup);

            _detailDelete = new FlatButton();
            _detailDelete.Text = "删除所选";
            _detailDelete.Font = Ui.SmallBold;
            _detailDelete.Danger = true;
            _detailDelete.Click += delegate { DetailDeleteClicked(); };
            _detail.Controls.Add(_detailDelete);

            _detailRename = new FlatButton();
            _detailRename.Text = "重命名";
            _detailRename.Font = Ui.SmallBold;
            _detailRename.Click += delegate { DetailRenameClicked(); };
            _detail.Controls.Add(_detailRename);

            _detailClose = new FlatButton();
            _detailClose.Text = "×";
            _detailClose.Font = Ui.H1;
            _detailClose.Click += delegate { CloseDetailPanel(); };
            _detail.Controls.Add(_detailClose);

            _detailSort = new FlatButton();
            _detailSort.Text = "排序：名称";
            _detailSort.Font = Ui.SmallBold;
            _detailSort.Click += delegate
            {
                _detailSortMode = (_detailSortMode + 1) % 2;
                RebuildDetailList();
            };
            _detail.Controls.Add(_detailSort);

            _detailRecalc = new FlatButton();
            _detailRecalc.Text = "重新计算占用";
            _detailRecalc.Font = Ui.SmallBold;
            _detailRecalc.Click += delegate { RecalculateOccupancy(); };
            _detail.Controls.Add(_detailRecalc);

            _detailCount = new Label();
            _detailCount.Text = "已选 0 个";
            _detailCount.Font = Ui.Small;
            _detailCount.ForeColor = Ui.TextDim;
            _detailCount.BackColor = Ui.CardBg;
            _detailCount.AutoSize = false;
            _detailCount.TextAlign = ContentAlignment.MiddleRight;
            _detailCount.AutoEllipsis = true;
            _detail.Controls.Add(_detailCount);

            _detailEmpty = new Label();
            _detailEmpty.Text = "";
            _detailEmpty.Font = Ui.Body;
            _detailEmpty.ForeColor = Ui.TextFaint;
            _detailEmpty.BackColor = Ui.CardBg;
            _detailEmpty.AutoSize = false;
            _detailEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _detailEmpty.Visible = false;
            _detail.Controls.Add(_detailEmpty);

            // FlatButton 会继承父容器背景（详情卡现在是页面底色），
            // 圆角外的四角必须显式用卡片色，否则每个按钮四角都会发黑。
            FlatButton[] detailButtons = new FlatButton[]
            {
                _detailSelectAll, _detailBackup, _detailDelete, _detailRename,
                _detailSort, _detailRecalc, _detailClose
            };
            for (int i = 0; i < detailButtons.Length; i++)
            {
                if (detailButtons[i] != null)
                {
                    detailButtons[i].BackColor = Ui.CardBg;
                }
            }
            UpdateDetailButtons();
        }

        private void LayoutManager()
        {
            if (Content == null || _side == null || _grid == null)
            {
                return;
            }
            int width = Content.Width;
            int height = Content.Height;
            // 侧栏要放得下「已安装 N 个涂装 · 占用 X」这一整行（约 207px）。
            int sideW = Ui.S(242);
            _side.SetBounds(0, 0, sideW, height);
            _nationPanel.SetBounds(0, Ui.S(94), sideW, Math.Max(Ui.S(100), height - Ui.S(94)));
            if (_libStats != null)
            {
                _libStats.SetBounds(Ui.S(20), Ui.S(44), Math.Max(Ui.S(110), sideW - Ui.S(30)), Ui.S(18));
            }

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
            _searchPanel.SetBounds(x, Ui.S(12), searchW, Ui.S(34));
            _search.SetBounds(Ui.S(12), Ui.S(7), Math.Max(Ui.S(40), searchW - Ui.S(24)), Ui.S(20));
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
            _restoreButton.SetBounds(bx, y2, bwRestore, Ui.S(33));
            bx += bwRestore + bgap;
            _refreshButton.SetBounds(bx, y2, bwRefresh, Ui.S(33));
            bx += bwRefresh + bgap;
            _updateButton.SetBounds(bx, y2, bwData, Ui.S(33));
            bx += bwData + bgap;
            _updateAllButton.SetBounds(bx, y2, bwDataAll, Ui.S(33));
            bx += bwDataAll + bgap;
            _metaLabel.SetBounds(bx, y2, Math.Max(Ui.S(80), mainLeft + mainW - pad - bx), Ui.S(33));

            int contentTop = Ui.S(96);
            int contentHeight = Math.Max(Ui.S(120), height - contentTop - Ui.S(10));
            int detailW = 0;
            if (_detailOpen && _detail != null)
            {
                // 右栏详情：约占主区 36%，保证列表行放得下勾选框/名称/大小/按钮。
                detailW = (int)Math.Round(mainW * 0.36);
                if (detailW < Ui.S(360))
                {
                    detailW = Ui.S(360);
                }
                if (detailW > Ui.S(430))
                {
                    detailW = Ui.S(430);
                }
                // 小窗口下先保证卡片网格至少 240，避免右栏压到卡片。
                int maxDetail = mainW - Ui.S(24) - Ui.S(240) - Ui.S(12);
                if (maxDetail < Ui.S(260))
                {
                    maxDetail = Ui.S(260);
                }
                if (detailW > maxDetail)
                {
                    detailW = maxDetail;
                }
            }
            int gridW = mainW - Ui.S(24) - (detailW > 0 ? detailW + Ui.S(12) : 0);
            _grid.SetBounds(mainLeft + Ui.S(12), contentTop,
                            Math.Max(Ui.S(240), gridW), contentHeight);
            if (_detail != null)
            {
                _detail.Visible = detailW > 0;
                if (detailW > 0)
                {
                    _detail.SetBounds(mainLeft + mainW - pad - detailW, contentTop, detailW, contentHeight);
                }
            }
            LayoutDetail();
            LayoutNationItems();
            _lastGridWidth = _grid.ClientSize.Width;
        }

        private void LayoutDetail()
        {
            if (_detail == null)
            {
                return;
            }
            bool show = _detailOpen;
            _detailList.Visible = show;
            _detailSelectAll.Visible = show;
            _detailBackup.Visible = show;
            _detailDelete.Visible = show;
            _detailRename.Visible = show;
            _detailClose.Visible = show;
            _detailSort.Visible = show;
            _detailRecalc.Visible = show;
            _detailCount.Visible = show;
            _detailEmpty.Visible = show && _detailVehicle != null && _detailVehicle.Items.Count == 0;
            if (!show)
            {
                return;
            }
            int w = _detail.ClientSize.Width;
            int h = _detail.ClientSize.Height;
            if (w <= 0 || h <= 0)
            {
                return;
            }
            int pad = Ui.S(14);
            int btnH = Ui.S(28);
            int bx = pad;
            int top = Ui.S(52);
            int wAll = Math.Max(Ui.S(58), TextRenderer.MeasureText("取消勾选", Ui.SmallBold).Width + Ui.S(16));
            _detailSelectAll.SetBounds(bx, top, wAll, btnH);
            bx += wAll + Ui.S(6);
            int wBackup = Math.Max(Ui.S(76), TextRenderer.MeasureText("备份所选", Ui.SmallBold).Width + Ui.S(16));
            _detailBackup.SetBounds(bx, top, wBackup, btnH);
            bx += wBackup + Ui.S(6);
            int wDelete = Math.Max(Ui.S(76), TextRenderer.MeasureText("删除所选", Ui.SmallBold).Width + Ui.S(16));
            _detailDelete.SetBounds(bx, top, wDelete, btnH);
            bx += wDelete + Ui.S(6);
            int wRename = Math.Max(Ui.S(64), TextRenderer.MeasureText("重命名", Ui.SmallBold).Width + Ui.S(16));
            _detailRename.SetBounds(bx, top, wRename, btnH);
            _detailClose.SetBounds(w - pad - Ui.S(26), Ui.S(12), Ui.S(26), Ui.S(26));
            int countW = Math.Max(Ui.S(64), TextRenderer.MeasureText("已选 99 个", Ui.SmallBold).Width + Ui.S(10));

            int rowTop = top + btnH + Ui.S(8);
            int wSort = Math.Max(Ui.S(84), TextRenderer.MeasureText("排序：名称", Ui.SmallBold).Width + Ui.S(16));
            int wRecalc = Math.Max(Ui.S(96), TextRenderer.MeasureText("重新计算占用", Ui.SmallBold).Width + Ui.S(16));
            bool narrow = w < Ui.S(348);
            if (narrow)
            {
                // 窄右栏：勾选/备份/删除只占第一行，「已选 N 个」挪到第二行右侧，避免压住按钮。
                int recalcX = Math.Max(pad + wSort + Ui.S(6), w - pad - wRecalc);
                _detailRecalc.SetBounds(recalcX, rowTop, wRecalc, Ui.S(26));
                int countX = Math.Max(pad + wSort + Ui.S(6), recalcX - Ui.S(6) - countW);
                _detailCount.SetBounds(countX, rowTop, countW, Ui.S(26));
            }
            else
            {
                _detailCount.SetBounds(w - pad - countW, top, countW, btnH);
                _detailRecalc.SetBounds(w - pad - wRecalc, rowTop, wRecalc, Ui.S(26));
            }
            _detailSort.SetBounds(pad, rowTop, wSort, Ui.S(26));

            int listTop = rowTop + Ui.S(26) + Ui.S(8);
            int listH = Math.Max(Ui.S(60), h - listTop - Ui.S(10));
            _detailList.SetBounds(pad, listTop, Math.Max(Ui.S(80), w - pad * 2), listH);
            int emptyH = Ui.S(56);
            _detailEmpty.SetBounds(pad, listTop + Math.Max(0, (listH - emptyH) / 2),
                                   Math.Max(Ui.S(80), w - pad * 2), emptyH);
            LayoutDetailRows();
        }

        /// <summary>重新按列表宽度摆放每一行（列表宽度变化时调用）。</summary>
        private void LayoutDetailRows()
        {
            if (_detailList == null)
            {
                return;
            }
            int reserve = SystemInformation.VerticalScrollBarWidth + Ui.S(4);
            int rowW = Math.Max(Ui.S(160), _detailList.ClientSize.Width - reserve);
            int top = 0;
            int gap = Ui.S(2);
            for (int i = 0; i < _detailRows.Count; i++)
            {
                ManagerSkinRow row = _detailRows[i];
                row.SetBounds(0, top, rowW, RowHeight(row));
                top += row.Height + gap;
            }
            try
            {
                _detailList.AutoScrollMinSize = new Size(0, top + Ui.S(4));
            }
            catch
            {
            }
        }

        private static int RowHeight(ManagerSkinRow row)
        {
            return row != null && row.Item != null && row.Item.Legacy ? Ui.S(50) : Ui.S(42);
        }

        private void PaintDetail(Graphics g)
        {
            if (_detail == null)
            {
                return;
            }
            Ui.Smooth(g);
            Rectangle r = new Rectangle(0, 0, _detail.Width - 1, _detail.Height - 1);
            Ui.FillRound(g, r, Ui.RCard, Ui.CardBg);
            Ui.DrawRound(g, r, Ui.RCard, Ui.Stroke, Ui.BorderWidth);
            string title = "涂装列表";
            if (_detailVehicle != null && !string.IsNullOrEmpty(_detailVehicle.Title))
            {
                title = "涂装列表 · " + _detailVehicle.Title;
            }
            Ui.DrawText(g, title, Ui.SmallBold, Ui.TextSecondary,
                    new Rectangle(Ui.S(14), Ui.S(14), Math.Max(Ui.S(60), _detail.Width - Ui.S(28)), Ui.S(20)),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
            using (Pen pen = new Pen(Ui.LineSoft, Ui.BorderWidth))
            {
                g.DrawLine(pen, Ui.S(14), Ui.S(44), _detail.Width - Ui.S(14), Ui.S(44));
            }
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
            string keepDetail = _detailOpen ? _detailFolder : "";
            _all.Clear();
            _all.AddRange(InstalledSkins.Scan(Config.UserSkinsDir));
            NameMap.ApplyUniqueTitles(_all);

            HashSet<string> stillSelected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _all.Count; i++)
            {
                string key = _all[i].Folder;
                if (_selectedBases.Contains(key))
                {
                    _all[i].Selected = true;
                    stillSelected.Add(key);
                }
            }
            _selectedBases = stillSelected;

            // 刷新/导入/删除后都重算占用：清空缓存，重新在后台统计。
            ManagerSkins.ClearSizeCache();
            _library = ManagerSkins.BuildLibrary(_all);

            _detailVehicle = null;
            _detailFolder = "";
            _detailOpen = false;
            if (keepDetail.Length > 0)
            {
                ManagerVehicleEntry entry = _library.Find(keepDetail);
                if (entry != null)
                {
                    _detailVehicle = entry;
                    _detailFolder = entry.Folder;
                    _detailOpen = true;
                }
            }

            BuildNationItems();
            RebuildGrid();
            RebuildDetailList();
            UpdateButtons();
            UpdateLibraryStats();
            StartMeasure();
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
                    card.DetailOpen = _detailOpen &&
                        string.Equals(card.Skin.Folder, _detailFolder, StringComparison.OrdinalIgnoreCase);
                    InstalledSkin capturedSkin = card.Skin;
                    SkinCard capturedCard = card;
                    card.SelectionChanged += delegate
                    {
                        capturedSkin.Selected = capturedCard.Selected;
                        if (capturedCard.Selected)
                        {
                            _selectedBases.Add(capturedSkin.Folder);
                        }
                        else
                        {
                            _selectedBases.Remove(capturedSkin.Folder);
                        }
                        UpdateButtons();
                    };
                    card.DetailsRequested += delegate { ToggleVehicleDetail(capturedSkin); };
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

            // 以实际子控件底边为准，防止逐行累加误差把滚动范围越撑越大（无限下滑）。
            int maxBottom = 0;
            for (int i = 0; i < _grid.Controls.Count; i++)
            {
                Control child = _grid.Controls[i];
                if (child != null && child.Visible)
                {
                    maxBottom = Math.Max(maxBottom, child.Bottom);
                }
            }
            int contentHeight = Math.Max(0, Math.Max(top, maxBottom) + Ui.S(4));
            // 再按行数夹一次上限，任何异常行高都不会产生超大空白滚动区。
            int rows = list.Count == 0 ? 1 : (list.Count + Math.Max(1, columns) - 1) / Math.Max(1, columns);
            int cap = rows * Ui.S(260) + Ui.S(400);
            if (contentHeight > cap)
            {
                contentHeight = cap;
            }
            _grid.AutoScrollMinSize = new Size(0, contentHeight);
            _grid.ResumeLayout();
            _rebuilding = false;
            try
            {
                int maxScroll = Math.Max(0, contentHeight - _grid.ClientSize.Height);
                int target = Math.Max(0, Math.Min(keepScroll, maxScroll));
                _grid.AutoScrollPosition = new Point(0, target);
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
                    _selectedBases.Add(visible[i].Folder);
                }
                else
                {
                    _selectedBases.Remove(visible[i].Folder);
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
            _restoreButton.Enabled = true;
            _backupButton.Enabled = _all.Count > 0;
            _updateButton.Enabled = !_updating;
            _updateAllButton.Enabled = !_updating;
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
                _selectedBases.Remove(selected[i].Folder);
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

        // ---------------- 右栏涂装列表：加载、选择与排序 ----------------

        /// <summary>点载具卡片（勾选框以外区域）：显示该载具的涂装列表；再点一次收起。</summary>
        private void ToggleVehicleDetail(InstalledSkin skin)
        {
            if (skin == null)
            {
                return;
            }
            if (_detailOpen && string.Equals(skin.Folder, _detailFolder, StringComparison.OrdinalIgnoreCase))
            {
                _detailOpen = false;
                _detailFolder = "";
                _detailVehicle = null;
                RebuildDetailList();
                UpdateCardDetailFlags();
                ScheduleLayout();
                return;
            }
            ManagerVehicleEntry entry = _library == null ? null : _library.Find(skin.Folder);
            _detailOpen = true;
            _detailFolder = skin.Folder;
            _detailVehicle = entry;
            RebuildDetailList();
            UpdateCardDetailFlags();
            ScheduleLayout();
        }

        /// <summary>
        /// 展开/收起右栏会改变卡片网格宽度，需要重排并重建卡片。
        /// 这个动作由卡片点击触发，直接重排会在卡片自己的事件里把它 Dispose 掉，所以推迟到消息循环下一拍。
        /// </summary>
        private void ScheduleLayout()
        {
            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!IsDisposed)
                        {
                            LayoutManager();
                        }
                    });
                    return;
                }
            }
            catch
            {
            }
            LayoutManager();
        }

        private void UpdateCardDetailFlags()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].DetailOpen = _detailOpen &&
                    string.Equals(_cards[i].Skin.Folder, _detailFolder, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void RebuildDetailList()
        {
            if (_detail == null || _detailList == null)
            {
                return;
            }
            _detailList.SuspendLayout();
            for (int i = _detailList.Controls.Count - 1; i >= 0; i--)
            {
                _detailList.Controls[i].Dispose();
            }
            _detailList.Controls.Clear();
            _detailRows.Clear();
            try
            {
                _detailList.AutoScrollPosition = new Point(0, 0);
            }
            catch
            {
            }

            if (!_detailOpen || _detailVehicle == null)
            {
                _detailEmpty.Text = "";
                _detailList.ResumeLayout();
                LayoutDetail();
                _detail.Invalidate();
                UpdateDetailButtons();
                return;
            }

            List<ManagerSkinItem> items = new List<ManagerSkinItem>(_detailVehicle.Items);
            items.Sort(CompareDetailItems);
            for (int i = 0; i < items.Count; i++)
            {
                ManagerSkinRow row = new ManagerSkinRow();
                row.Item = items[i];
                row.Height = RowHeight(row);
                row.SelectionChanged += delegate { UpdateDetailButtons(); };
                _detailList.Controls.Add(row);
                _detailRows.Add(row);
            }
            if (_detailVehicle.Unreadable)
            {
                _detailEmpty.Text = "无法读取这个载具目录（可能没有访问权限）。";
            }
            else if (items.Count == 0)
            {
                _detailEmpty.Text = "这个载具还没有涂装。\r\n在游戏里为该载具创建涂装示例后，涂装会显示在这里。";
            }
            else
            {
                _detailEmpty.Text = "";
            }
            _detailList.ResumeLayout();
            LayoutDetailRows();
            LayoutDetail();
            _detail.Invalidate();
            UpdateDetailButtons();
        }

        /// <summary>排序：名称升序（默认）；大小降序，计算中的条目排在末尾。</summary>
        private int CompareDetailItems(ManagerSkinItem a, ManagerSkinItem b)
        {
            if (_detailSortMode == 1)
            {
                bool ka = a.Size >= 0;
                bool kb = b.Size >= 0;
                if (ka != kb)
                {
                    return ka ? -1 : 1;
                }
                if (ka && a.Size != b.Size)
                {
                    return b.Size.CompareTo(a.Size);
                }
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private List<ManagerSkinItem> SelectedDetailItems()
        {
            List<ManagerSkinItem> list = new List<ManagerSkinItem>();
            if (_detailVehicle == null)
            {
                return list;
            }
            for (int i = 0; i < _detailVehicle.Items.Count; i++)
            {
                ManagerSkinItem item = _detailVehicle.Items[i];
                if (item.CanSelect && item.Selected)
                {
                    list.Add(item);
                }
            }
            return list;
        }

        private void UpdateDetailButtons()
        {
            if (_detailSelectAll == null)
            {
                return;
            }
            int selectable = 0;
            int selected = 0;
            if (_detailVehicle != null)
            {
                for (int i = 0; i < _detailVehicle.Items.Count; i++)
                {
                    ManagerSkinItem item = _detailVehicle.Items[i];
                    if (!item.CanSelect)
                    {
                        continue;
                    }
                    selectable++;
                    if (item.Selected)
                    {
                        selected++;
                    }
                }
            }
            bool all = selectable > 0 && selected == selectable;
            bool open = _detailOpen && _detailVehicle != null;
            _detailSelectAll.Text = all ? "取消勾选" : "全选";
            _detailSelectAll.Primary = all;
            _detailSelectAll.Enabled = open && selectable > 0;
            _detailBackup.Enabled = open && selected > 0;
            _detailDelete.Enabled = open && selected > 0;
            _detailRename.Enabled = open && selected == 1;
            _detailSort.Enabled = open;
            _detailRecalc.Enabled = open;
            _detailCount.Text = "已选 " + selected + " 个";
            _detailSort.Text = _detailSortMode == 0 ? "排序：名称" : "排序：大小";
        }

        private void DetailSelectAll()
        {
            if (_detailVehicle == null)
            {
                return;
            }
            int selectable = 0;
            int selected = 0;
            for (int i = 0; i < _detailVehicle.Items.Count; i++)
            {
                ManagerSkinItem item = _detailVehicle.Items[i];
                if (!item.CanSelect)
                {
                    continue;
                }
                selectable++;
                if (item.Selected)
                {
                    selected++;
                }
            }
            bool all = selectable > 0 && selected == selectable;
            for (int i = 0; i < _detailVehicle.Items.Count; i++)
            {
                ManagerSkinItem item = _detailVehicle.Items[i];
                if (item.CanSelect)
                {
                    item.Selected = !all;
                }
            }
            for (int i = 0; i < _detailRows.Count; i++)
            {
                _detailRows[i].Invalidate();
            }
            UpdateDetailButtons();
        }

        /// <summary>关闭右栏展开列表（不影响载具勾选状态）。</summary>
        private void CloseDetailPanel()
        {
            _detailOpen = false;
            _detailFolder = "";
            _detailVehicle = null;
            RebuildDetailList();
            UpdateCardDetailFlags();
            ScheduleLayout();
        }

        private void DetailRenameClicked()
        {
            List<ManagerSkinItem> selected = SelectedDetailItems();
            if (selected.Count != 1 || _detailVehicle == null)
            {
                return;
            }
            ManagerSkinItem item = selected[0];
            if (item.Legacy)
            {
                MessageBox.Show(this, "这是载具目录本身，不能重命名。", "重命名",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (RenameSkinForm form = new RenameSkinForm(item.FolderName))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string name = form.Value;
                string error = SkinNameRules.Validate(name);
                if (error.Length > 0)
                {
                    MessageBox.Show(this, error, "重命名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.Equals(name, item.FolderName, StringComparison.Ordinal))
                {
                    return;
                }
                string parent = Path.GetDirectoryName(item.Path);
                string target = Path.Combine(parent, name);
                if (Directory.Exists(target))
                {
                    MessageBox.Show(this, "目标文件夹已存在：\r\n\r\n" + target, "重命名",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (File.Exists(target))
                {
                    MessageBox.Show(this, "目标同名文件已存在，不能重命名：\r\n\r\n" + target, "重命名",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    Directory.Move(item.Path, target);
                    Config.Log("重命名涂装：" + item.Path + " → " + target);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "重命名失败：\r\n\r\n" + ex.Message, "重命名",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            string keepFolder = _detailFolder;
            bool keepOpen = _detailOpen;
            Reload();
            if (keepOpen && _library != null)
            {
                ManagerVehicleEntry entry = _library.Find(keepFolder);
                _detailOpen = entry != null;
                _detailFolder = entry != null ? keepFolder : "";
                _detailVehicle = entry;
                RebuildDetailList();
                UpdateCardDetailFlags();
                ScheduleLayout();
            }
        }

        // ---------------- 右栏涂装列表：备份与删除 ----------------

        private void DetailBackupClicked()
        {
            List<ManagerSkinItem> selected = SelectedDetailItems();
            if (selected.Count == 0 || _detailVehicle == null)
            {
                return;
            }
            string root;
            int copied;
            string error;
            if (!ManagerSkins.BackupItems(selected, _detailVehicle.Folder, out root, out copied, out error))
            {
                MessageBox.Show(this, "备份失败：\r\n\r\n" + error, "备份所选",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Config.Log("备份涂装 " + copied + " 个 -> " + root + "（载具 " + _detailVehicle.Folder + "）");
            MessageBox.Show(this, "已备份 " + copied + " 个涂装到：\r\n\r\n" + root +
                            "\r\n\r\n原目录未做改动。", "备份完成",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DetailDeleteClicked()
        {
            List<ManagerSkinItem> selected = SelectedDetailItems();
            if (selected.Count == 0 || _detailVehicle == null)
            {
                return;
            }
            using (DeleteSkinsConfirmForm form = new DeleteSkinsConfirmForm(_detailVehicle, selected))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (form.BackupFirst)
                {
                    string root;
                    int copied;
                    string error;
                    if (!ManagerSkins.BackupItems(selected, _detailVehicle.Folder, out root, out copied, out error))
                    {
                        MessageBox.Show(this, "备份失败，未执行删除：\r\n\r\n" + error, "删除所选",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            List<string> deleted = new List<string>();
            List<string> skipped = new List<string>();
            string vehicleFolder = _detailVehicle.Folder;
            string vehiclePath = _detailVehicle.Path;
            ManagerSkins.DeleteItems(selected, vehiclePath, deleted, skipped);
            Config.Log("删除涂装 " + deleted.Count + " 个，跳过 " + skipped.Count + " 个（载具 " + vehicleFolder + "）");
            // 刷新列表与左栏总占用；载具目录本身保留，全部删完时右栏显示空态。
            Reload();
            string text = "已删除 " + deleted.Count + " 个涂装。";
            if (skipped.Count > 0)
            {
                text += "\r\n跳过 " + skipped.Count + " 个（可能被占用或无权限）：\r\n" +
                        string.Join("\r\n", skipped.ToArray());
            }
            text += "\r\n\r\n载具目录本身已保留。";
            MessageBox.Show(this, text, "删除所选", MessageBoxButtons.OK,
                            skipped.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // ---------------- 空间统计（后台线程 + 内存缓存） ----------------

        private void StartMeasure()
        {
            if (_library == null)
            {
                return;
            }
            int generation = ManagerSkins.CacheGeneration;
            _measureGeneration = generation;
            if (!_library.HasPendingSizes())
            {
                UpdateLibraryStats();
                return;
            }
            ManagerLibrary lib = _library;
            ManagerSkins.MeasureAsync(lib, generation,
                delegate(ManagerVehicleEntry entry) { PostMeasureVehicle(generation, entry); },
                delegate { PostMeasureDone(generation); });
        }

        private void PostMeasureVehicle(int generation, ManagerVehicleEntry entry)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed || generation != _measureGeneration)
                    {
                        return;
                    }
                    if (_detailVehicle != null && entry != null &&
                        string.Equals(_detailVehicle.Folder, entry.Folder, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int i = 0; i < _detailRows.Count; i++)
                        {
                            _detailRows[i].Invalidate();
                        }
                    }
                });
            }
            catch
            {
            }
        }

        private void PostMeasureDone(int generation)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed || generation != _measureGeneration)
                    {
                        return;
                    }
                    UpdateLibraryStats();
                    if (_detailOpen && _detailSortMode == 1)
                    {
                        // 按大小排序时，尺寸陆续到位后顺序会变，统计完成后重排一次。
                        RebuildDetailList();
                    }
                    else
                    {
                        for (int i = 0; i < _detailRows.Count; i++)
                        {
                            _detailRows[i].Invalidate();
                        }
                    }
                    UpdateDetailButtons();
                });
            }
            catch
            {
            }
        }

        private void RecalculateOccupancy()
        {
            if (_library == null)
            {
                return;
            }
            ManagerSkins.ClearSizeCache();
            _library.ResetSizes();
            UpdateLibraryStats();
            for (int i = 0; i < _detailRows.Count; i++)
            {
                _detailRows[i].Invalidate();
            }
            UpdateDetailButtons();
            StartMeasure();
        }

        private void UpdateLibraryStats()
        {
            if (_libStats == null)
            {
                return;
            }
            if (_library == null)
            {
                _libStats.Text = "已安装 0 个涂装 · 占用 计算中…";
                return;
            }
            _libStats.Text = "已安装 " + _library.SkinCount + " 个涂装 · 占用 " +
                             ManagerSkins.FormatSize(_library.TotalBytes);
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

    /// <summary>
    /// 管理卡片：左图右文，标题两行，四行键值（国家/等级·权重/机型代号/文件夹），右上角圆形勾选框。
    /// 勾选框只切换载具选中；卡片其余区域负责打开右栏涂装列表（再点收起）。
    /// </summary>
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
        private bool _detailOpen;
        public InstalledSkin Skin;

        public event EventHandler SelectionChanged;
        /// <summary>点了勾选框以外的区域：请求在右栏展开该载具的涂装列表。</summary>
        public event EventHandler DetailsRequested;

        public SkinCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Ui.Bg;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        /// <summary>右栏当前是否正在显示这个载具的涂装列表（只影响描边，与勾选无关）。</summary>
        public bool DetailOpen
        {
            get { return _detailOpen; }
            set
            {
                if (_detailOpen == value)
                {
                    return;
                }
                _detailOpen = value;
                Invalidate();
            }
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
            int total = Ui.S(Pad) * 2 + Ui.S(BandH) + Ui.S(10) + titleH + Ui.S(8) + rows;
            if (total < Ui.S(230))
            {
                total = Ui.S(230);
            }
            if (total > Ui.S(330))
            {
                total = Ui.S(330);
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
                Focus();
                // 只有右上角勾选框切换载具选中；其余区域打开/收起右栏涂装列表。
                if (CheckHitRect().Contains(e.Location))
                {
                    Selected = !Selected;
                    return;
                }
                if (DetailsRequested != null)
                {
                    DetailsRequested(this, EventArgs.Empty);
                }
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

            Ui.DrawRound(g, r, Ui.S(10), _selected ? Ui.Accent : Ui.LineSoft, _selected ? 1.6f : 1f);
            if (_detailOpen)
            {
                // 右栏正在显示这个载具：左侧加一道强调色，便于和勾选框区分开。
                Ui.FillRound(g, new Rectangle(0, Ui.S(14), Math.Max(2, Ui.S(3)), Height - Ui.S(28)),
                             Ui.S(2), Ui.Accent);
            }
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
            MakeLabel("作用范围仅限当前勾选的载具目录；载具目录里的涂装子目录会一并删除。", 24, 40, 610, 20, Ui.Small, Ui.TextDim);

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

    /// <summary>
    /// 删除勾选涂装的二次确认：列出名称、大小、数量；提示载具目录本身会保留；
    /// 「先备份再删除」默认勾选；默认按钮为取消（Enter / Esc 都执行取消）。
    /// </summary>
    /// <summary>重命名涂装子目录：实时校验名称，默认焦点在取消上。</summary>
    internal class RenameSkinForm : ModalForm
    {
        public string Value = "";
        private readonly TextBox _box;
        private readonly Label _error;
        private FlatButton _ok;

        public RenameSkinForm(string current)
            : base("重命名涂装", 540, 300)
        {
            MakeLabel("涂装文件夹名", 24, 18, 200, 22, Ui.SmallBold, Ui.TextDim);
            _box = new TextBox();
            _box.Left = Ui.S(24);
            _box.Top = Ui.S(44);
            _box.Width = Ui.S(492);
            _box.Font = Ui.Body;
            _box.BackColor = Ui.BgSoft;
            _box.ForeColor = Ui.Text;
            _box.BorderStyle = BorderStyle.FixedSingle;
            _box.Text = current;
            Content.Controls.Add(_box);
            _error = MakeLabel("", 24, 84, 492, 36, Ui.Small, Ui.Bad);
            MakeLabel("只改第二级涂装文件夹名，载具 template_ 目录本身不变。", 24, 124, 492, 22, Ui.Small, Ui.TextDim);

            FlatButton cancel = MakeButton("取消", 340, 210, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            _ok = MakeButton("确定", 448, 210, 96, true);
            _ok.Click += delegate
            {
                Value = _box.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };
            _box.TextChanged += delegate { ValidateName(); };
            ValidateName();
            Shown += delegate
            {
                Native.ThemeScrollbars(this);
                int y = Content.Height - Ui.S(18) - Ui.S(38);
                _ok.SetBounds(Content.Width - Ui.S(24) - Ui.S(96), y, Ui.S(96), Ui.S(38));
                cancel.SetBounds(_ok.Left - Ui.S(12) - Ui.S(96), y, Ui.S(96), Ui.S(38));
                cancel.Focus();
                _box.SelectAll();
            };
        }

        private void ValidateName()
        {
            string error = SkinNameRules.Validate(_box.Text);
            _error.Text = error;
            _ok.Enabled = error.Length == 0;
        }
    }

    internal class DeleteSkinsConfirmForm : ModalForm
    {
        public bool BackupFirst;

        public DeleteSkinsConfirmForm(ManagerVehicleEntry vehicle, List<ManagerSkinItem> items)
            : base("删除涂装", 660, 540)
        {
            long total = 0;
            int unknown = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Size >= 0)
                {
                    total += items[i].Size;
                }
                else if (items[i].Size == -1)
                {
                    unknown++;
                }
            }
            string sizeText = ManagerSkins.FormatSize(total);
            if (unknown > 0)
            {
                sizeText += "（另有 " + unknown + " 个计算中）";
            }
            MakeLabel("将删除 " + items.Count + " 个涂装（共 " + sizeText + "）", 24, 12, 600, 26, Ui.H1, Ui.Bad);
            string vehicleName = "";
            if (vehicle != null)
            {
                vehicleName = string.IsNullOrEmpty(vehicle.Title)
                    ? vehicle.Folder
                    : vehicle.Title + " · " + vehicle.Folder;
            }
            MakeLabel("所属载具：" + vehicleName, 24, 40, 610, 20, Ui.Small, Ui.TextDim);

            ListBox list = new ListBox();
            list.Left = Ui.S(24);
            list.Top = Ui.S(66);
            list.Width = Ui.S(612);
            list.Height = Ui.S(250);
            list.BackColor = Ui.BgSoft;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.None;
            list.Font = Ui.Mono;
            for (int i = 0; i < items.Count; i++)
            {
                list.Items.Add(items[i].Name + "    " + ManagerSkins.FormatSize(items[i].Size));
            }
            Content.Controls.Add(list);

            MakeLabel("只删除勾选的涂装子目录；载具目录本身会保留。", 24, 326, 600, 20, Ui.Small, Ui.TextDim);

            CheckBox backup = new CheckBox();
            backup.Text = "先备份再删除";
            backup.Left = Ui.S(24);
            backup.Top = Ui.S(354);
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
                      24, 386, 600, 40, Ui.Small, Ui.TextFaint);

            FlatButton cancel = MakeButton("取消", 426, 438, 96, false);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            FlatButton ok = MakeButton("删除", 530, 438, 96, false);
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
