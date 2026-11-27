using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PdfWordTool
{
    enum FileState
    {
        Waiting,
        Converting,
        Done,
        Failed
    }

    sealed class FileItem
    {
        public string Path;
        public string Output;
        public FileState State = FileState.Waiting;
        public string Error;

        // 仅用于状态 pill 的颜色过渡，不参与业务判断。
        public FileState PrevState = FileState.Waiting;
        public float StateT = 1f;
    }

    // 所有工具页共享的交互框架：单卡片工作区、文件列表、进度、动画、右键菜单。
    abstract class ToolPage : UserControl
    {
        protected readonly CardPanel Card;
        protected readonly DataGridView Grid;
        protected readonly RoundedProgressBar Progress;
        protected readonly Label StatusLabel;
        protected readonly Label CountLabel;
        protected readonly EmptyStateView EmptyView;
        protected readonly PillButton BtnAdd;
        protected readonly PillButton BtnClear;
        protected readonly PillButton BtnStart;

        private readonly Panel _toolbar;
        private readonly FlowLayoutPanel _options;
        private readonly Panel _outputBar;
        private readonly OutputPathPicker _outputPicker;
        private readonly Panel _contentHost;
        private readonly VScrollBarEx _scrollBar;
        private readonly Font _headerFont;
        private readonly HashSet<string> _paths;
        private readonly string[] _extensions;
        private readonly string _startText;
        private readonly string _verb;
        private readonly Timer _spinTimer;
        private readonly Timer _fadeTimer;
        private readonly Timer _hoverAnimTimer;
        private readonly Timer _pillTimer;
        private readonly Dictionary<FileItem, float> _rowHoverT = new Dictionary<FileItem, float>();

        private Color _fadeFrom;
        private float _fadeT = 1f;
        private int _spinAngle;
        private int _hoverRow = -1;
        private bool _running;
        private volatile bool _cancelRequested;
        private TaskbarProgress _taskbar;
        private RoundedPopup _menuPopup;

        protected ToolPage(string dropTitle, string dropSubtitle,
                           string startText, string verb, string[] extensions)
        {
            BackColor = Theme.FormBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            DoubleBuffered = true;

            _startText = startText;
            _verb = verb;
            _extensions = extensions;
            _paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _headerFont = new Font(Font.FontFamily, 9F, FontStyle.Bold);

            _spinTimer = new Timer();
            _spinTimer.Interval = 40;
            _spinTimer.Tick += OnSpinTick;

            _fadeTimer = new Timer();
            _fadeTimer.Interval = 16;
            _fadeTimer.Tick += OnFadeTick;

            _hoverAnimTimer = new Timer();
            _hoverAnimTimer.Interval = 16;
            _hoverAnimTimer.Tick += OnRowHoverTick;

            _pillTimer = new Timer();
            _pillTimer.Interval = 16;
            _pillTimer.Tick += OnPillTick;

            // ---------- 单卡片工作区 ----------
            Card = new CardPanel();
            Card.Dock = DockStyle.Fill;

            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 52;
            _toolbar.BackColor = Theme.CardBack;
            _toolbar.Visible = false;

            TableLayoutPanel toolbarLayout = new TableLayoutPanel();
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.ColumnCount = 1;
            toolbarLayout.RowCount = 1;
            toolbarLayout.BackColor = Theme.CardBack;
            toolbarLayout.Padding = Padding.Empty;
            toolbarLayout.Margin = Padding.Empty;
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _options = new FlowLayoutPanel();
            _options.Dock = DockStyle.Fill;
            _options.FlowDirection = FlowDirection.LeftToRight;
            _options.WrapContents = false;
            _options.AutoSize = false;
            _options.BackColor = Theme.CardBack;
            _options.Padding = new Padding(0, 10, 0, 10);
            _options.Margin = Padding.Empty;

            BtnAdd = new PillButton();
            BtnAdd.Text = "+ 添加文件";
            BtnAdd.Size = new Size(104, 34);
            BtnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BtnAdd.Margin = Padding.Empty;
            BtnAdd.Click += OnAddClick;

            toolbarLayout.Controls.Add(_options, 0, 0);
            _toolbar.Controls.Add(toolbarLayout);

            // ---------- 输出路径 ----------
            _outputBar = new Panel();
            _outputBar.Dock = DockStyle.Top;
            _outputBar.Height = 44;
            _outputBar.BackColor = Theme.CardBack;
            _outputBar.Visible = false;

            Label outputLabel = new Label();
            outputLabel.Text = "输出路径";
            outputLabel.AutoSize = true;
            outputLabel.ForeColor = Theme.Muted;
            outputLabel.Font = Font;
            outputLabel.Location = new Point(0, 13);

            _outputPicker = new OutputPathPicker();
            _outputPicker.Size = new Size(320, 30);
            _outputPicker.Location = new Point(outputLabel.PreferredWidth + 8, 7);
            _outputPicker.OutputDirChanged += OnOutputDirChanged;

            _outputBar.Controls.Add(outputLabel);
            _outputBar.Controls.Add(_outputPicker);

            // ---------- 文件列表 ----------
            Grid = new ThemedGrid();
            Grid.Dock = DockStyle.Fill;
            Grid.Font = Font;
            Grid.Margin = Padding.Empty;
            Grid.BackgroundColor = Theme.CardBack;
            Grid.GridColor = Theme.CardBack;
            Grid.BorderStyle = BorderStyle.None;
            Grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
            Grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            Grid.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            Grid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            Grid.EnableHeadersVisualStyles = false;
            Grid.ColumnHeadersHeight = 36;
            Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Grid.ColumnHeadersVisible = false;
            Grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle();
            Grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.CardBack;
            Grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            Grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.CardBack;
            Grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;

            Grid.RowHeadersVisible = false;
            Grid.AllowUserToAddRows = false;
            Grid.AllowUserToDeleteRows = false;
            Grid.AllowUserToResizeRows = false;
            Grid.AllowUserToOrderColumns = false;
            Grid.AllowUserToResizeColumns = false;
            Grid.ReadOnly = true;
            Grid.MultiSelect = true;
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.ScrollBars = ScrollBars.None;

            DataGridViewTextBoxColumn colFile = new DataGridViewTextBoxColumn();
            colFile.HeaderText = "文件";
            colFile.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colFile.FillWeight = 100;
            colFile.MinimumWidth = Theme.FileColumnMinWidth;
            colFile.SortMode = DataGridViewColumnSortMode.NotSortable;
            colFile.ReadOnly = true;
            colFile.DividerWidth = 0;

            DataGridViewTextBoxColumn colState = new DataGridViewTextBoxColumn();
            colState.HeaderText = "状态";
            colState.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colState.Width = Theme.StateColumnWidth;
            colState.MinimumWidth = Theme.StateColumnWidth;
            colState.SortMode = DataGridViewColumnSortMode.NotSortable;
            colState.ReadOnly = true;
            colState.DividerWidth = 0;

            Grid.Columns.Add(colFile);
            Grid.Columns.Add(colState);

            Grid.RowTemplate.Height = 36;
            Grid.RowTemplate.DividerHeight = 0;
            Grid.ShowCellToolTips = true;

            DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();
            cellStyle.BackColor = Theme.CardBack;
            cellStyle.ForeColor = Theme.Text;
            cellStyle.SelectionBackColor = Theme.SelectedBack;
            cellStyle.SelectionForeColor = Theme.Text;
            cellStyle.Padding = new Padding(10, 0, 8, 0);
            Grid.DefaultCellStyle = cellStyle;

            Grid.RowPrePaint += OnRowPrePaint;
            Grid.RowPostPaint += OnRowPostPaint;
            Grid.CellDoubleClick += OnGridDoubleClick;
            Grid.MouseDown += OnGridMouseDown;
            Grid.MouseMove += OnGridMouseMove;
            Grid.MouseLeave += OnGridMouseLeave;
            Grid.ContextMenuStrip = BuildContextMenu();
            Grid.Scroll += delegate { SyncScrollBar(); };
            Grid.RowsAdded += delegate { SyncScrollBar(); };
            Grid.RowsRemoved += delegate { SyncScrollBar(); };
            Grid.Resize += delegate { SyncScrollBar(); };

            _scrollBar = new VScrollBarEx();
            _scrollBar.Dock = DockStyle.Fill;
            _scrollBar.Margin = Padding.Empty;
            _scrollBar.ValueChanged += OnScrollValueChanged;
            ((ThemedGrid)Grid).ScrollBar = _scrollBar;

            TableLayoutPanel contentLayout = new TableLayoutPanel();
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.ColumnCount = 2;
            contentLayout.RowCount = 2;
            contentLayout.BackColor = Theme.CardBack;
            contentLayout.Padding = Padding.Empty;
            contentLayout.Margin = Padding.Empty;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12f));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FileListHeader listHeader = new FileListHeader();
            listHeader.Dock = DockStyle.Fill;
            listHeader.Margin = Padding.Empty;
            contentLayout.Controls.Add(listHeader, 0, 0);
            contentLayout.SetColumnSpan(listHeader, 2);
            contentLayout.Controls.Add(Grid, 0, 1);
            contentLayout.Controls.Add(_scrollBar, 1, 1);

            EmptyView = new EmptyStateView();
            EmptyView.Dock = DockStyle.Fill;
            EmptyView.Title = dropTitle;
            EmptyView.Subtitle = dropSubtitle;
            EmptyView.Click += OnAddClick;

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = Theme.CardBack;
            _contentHost.Controls.Add(contentLayout);
            _contentHost.Controls.Add(EmptyView);

            Card.Controls.Add(_contentHost);
            Card.Controls.Add(_outputBar);
            Card.Controls.Add(_toolbar);

            Panel cardHost = new Panel();
            cardHost.Dock = DockStyle.Fill;
            cardHost.Padding = new Padding(16, 12, 16, 8);
            cardHost.BackColor = Theme.FormBack;
            cardHost.Controls.Add(Card);

            // ---------- 底部操作区 ----------
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            bottom.BackColor = Theme.FormBack;

            Progress = new RoundedProgressBar();
            Progress.Location = new Point(16, 0);
            Progress.Size = new Size(100, 3);
            Progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Progress.Visible = false;

            // 左下角留给外壳的设置按钮，状态文案从其后开始。
            StatusLabel = new Label();
            StatusLabel.Location = new Point(52, 18);
            StatusLabel.Size = new Size(100, 20);
            StatusLabel.AutoEllipsis = true;
            StatusLabel.ForeColor = Theme.Muted;
            StatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            CountLabel = new Label();
            CountLabel.Size = new Size(160, 20);
            CountLabel.TextAlign = ContentAlignment.MiddleRight;
            CountLabel.ForeColor = Theme.Muted;
            CountLabel.Text = "共 0 个文件";
            CountLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            BtnClear = new PillButton();
            BtnClear.Text = "清空列表";
            BtnClear.Size = new Size(96, 34);
            BtnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BtnClear.Click += OnClearClick;

            BtnStart = new PillButton();
            BtnStart.Text = startText;
            BtnStart.Size = new Size(116, 34);
            BtnStart.Accent = true;
            BtnStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BtnStart.Click += OnStartClick;

            bottom.Controls.Add(Progress);
            bottom.Controls.Add(StatusLabel);
            bottom.Controls.Add(CountLabel);
            bottom.Controls.Add(BtnAdd);
            bottom.Controls.Add(BtnClear);
            bottom.Controls.Add(BtnStart);

            bottom.Resize += delegate { LayoutBottom(bottom); };

            Controls.Add(cardHost);
            Controls.Add(bottom);

            WireDrop(this);
            WireDrop(Card);
            WireDrop(_contentHost);
            WireDrop(_toolbar);
            WireDrop(_outputBar);
            WireDrop(_options);
            WireDrop(listHeader);
            WireDrop(Grid);
            WireDrop(EmptyView);
            WireDrop(BtnAdd);

            LayoutBottom(bottom);
            UpdateEmptyState();
        }

        // ---------- 页面配置 ----------

        protected abstract void Execute(FileItem item, object options);

        protected virtual object CaptureOptions()
        {
            return null;
        }

        // 空字符串表示输出到源文件所在目录。
        protected string OutputDir
        {
            get { return _outputPicker.OutputDir; }
        }

        private void OnOutputDirChanged(object sender, EventArgs e)
        {
            ResetCompletedRows();
        }

        // 向卡片顶部工具行添加一个“标签 + 控件”分组，返回分组容器便于按需显隐。
        protected Control AddOption(string labelText, Control control)
        {
            TableLayoutPanel group = new TableLayoutPanel();
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.ColumnCount = 2;
            group.RowCount = 1;
            group.BackColor = Theme.CardBack;
            group.Margin = new Padding(0, 0, 24, 0);
            group.Padding = Padding.Empty;
            group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.ForeColor = Theme.Muted;
            label.Font = Font;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 0, 8, 0);

            control.Anchor = AnchorStyles.Left;
            control.Margin = Padding.Empty;

            group.Controls.Add(label, 0, 0);
            group.Controls.Add(control, 1, 0);
            _options.Controls.Add(group);
            return group;
        }

        protected void AddOptionHint(string text)
        {
            Label hint = new Label();
            hint.Text = text;
            hint.AutoSize = true;
            hint.ForeColor = Theme.Muted;
            hint.Font = Font;
            hint.Margin = new Padding(0, 17, 0, 0);
            _options.Controls.Add(hint);
        }

        // 直接添加一个自定义选项控件（用于需要自定义纵向布局的页面）。
        protected void AddOptionControl(Control control)
        {
            control.Margin = Padding.Empty;
            _options.Controls.Add(control);
        }

        // 调整选项区高度；自定义选项控件需要更多纵向空间时调用。
        protected void SetOptionsHeight(int height)
        {
            _toolbar.Height = height;
        }

        public void FocusGrid()
        {
            if (Grid.Rows.Count > 0) Grid.Focus();
            else EmptyView.Focus();
        }

        // ---------- 布局 ----------

        private void LayoutBottom(Panel bottom)
        {
            int w = bottom.ClientSize.Width;
            Progress.Width = Math.Max(60, w - 32);
            BtnStart.Location = new Point(w - 16 - BtnStart.Width, 11);
            BtnClear.Location = new Point(BtnStart.Left - 8 - BtnClear.Width, 11);
            int countRight = BtnClear.Left;
            // 用“是否有文件”而非 BtnAdd.Visible 判断：后者是有效可见性，页面处于
            // 隐藏标签页时读到 false，会把计数标签摆到添加按钮的位置下，恢复显示后两者重叠。
            if (Grid.Rows.Count > 0)
            {
                BtnAdd.Location = new Point(BtnClear.Left - 8 - BtnAdd.Width, 11);
                countRight = BtnAdd.Left;
            }
            CountLabel.Location = new Point(Math.Max(8, countRight - 12 - CountLabel.Width), 18);
            StatusLabel.Width = Math.Max(80, CountLabel.Left - 52 - 12);
        }

        // ---------- 主题化菜单 ----------

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new ThemedMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Theme.CardBack;
            menu.ForeColor = Theme.Text;
            menu.Font = Font;
            menu.Padding = new Padding(
                Theme.PopupBorderInset, Theme.MenuPaddingY,
                Theme.PopupBorderInset, Theme.MenuPaddingY);
            menu.DropShadowEnabled = false;

            ToolStripMenuItem openOutput = new ToolStripMenuItem("打开输出文件");
            openOutput.Click += delegate { OpenSelectedOutput(); };

            ToolStripMenuItem openFolder = new ToolStripMenuItem("打开所在文件夹");
            openFolder.Click += delegate { OpenSelectedFolder(); };

            ToolStripMenuItem remove = new ToolStripMenuItem("从列表移除");
            remove.Click += delegate { RemoveSelected(); };

            menu.Items.Add(openOutput);
            menu.Items.Add(openFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(remove);

            foreach (ToolStripItem item in menu.Items)
                item.Padding = new Padding(
                    Theme.MenuItemPaddingX, Theme.MenuItemPaddingY,
                    Theme.MenuItemPaddingX, Theme.MenuItemPaddingY);

            menu.Opening += delegate
            {
                bool hasSelection = Grid.SelectedRows.Count > 0;
                openOutput.Enabled = SelectedDoneOutput() != null;
                openFolder.Enabled = hasSelection;
                remove.Enabled = hasSelection && !_running;
            };

            _menuPopup = new RoundedPopup(menu, Theme.RadiusControl);

            return menu;
        }

        // ---------- 添加文件 ----------

        private void OnAddClick(object sender, EventArgs e)
        {
            if (_running) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = BuildFilter();
                dlg.Title = "选择要处理的文件";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddFiles(dlg.FileNames);
            }
        }

        private string BuildFilter()
        {
            List<string> patterns = new List<string>();
            foreach (string ext in _extensions) patterns.Add("*" + ext);

            string joined = string.Join(";", patterns.ToArray());
            string label;
            if (_extensions.Length == 1 && _extensions[0] == ".pdf") label = "PDF 文件";
            else label = "支持的图片";
            return label + " (" + joined + ")|" + joined + "|所有文件 (*.*)|*.*";
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            int before = Grid.Rows.Count;
            foreach (string p in paths)
            {
                if (Directory.Exists(p))
                {
                    string[] files;
                    try { files = Directory.GetFiles(p); }
                    catch { continue; }
                    foreach (string f in files)
                        if (IsSupported(f)) AddOne(f);
                }
                else if (File.Exists(p))
                {
                    AddOne(p);
                }
            }

            int added = Grid.Rows.Count - before;
            if (added > 0)
            {
                Grid.ClearSelection();
                Grid.CurrentCell = null;
            }
            UpdateEmptyState();
            UpdateCountLabel();

            if (added > 0)
            {
                SetStatusText("");
            }
            else if (before == 0)
            {
                SetStatusText("没有找到可处理的文件。");
            }
            else
            {
                SetStatusText("所选文件已在列表中。");
            }
        }

        private bool IsSupported(string path)
        {
            string ext = Path.GetExtension(path);
            foreach (string item in _extensions)
                if (string.Equals(ext, item, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool AddOne(string path)
        {
            if (!IsSupported(path)) return false;

            string full = Path.GetFullPath(path);
            if (_paths.Contains(full)) return false;
            _paths.Add(full);

            FileItem item = new FileItem();
            item.Path = full;

            int index = Grid.Rows.Add(Path.GetFileName(full), StatusText(FileState.Waiting));
            Grid.Rows[index].Tag = item;
            Grid.Rows[index].Cells[0].ToolTipText = full;
            return true;
        }

        private void UpdateEmptyState()
        {
            bool empty = Grid.Rows.Count == 0;
            _toolbar.Visible = !empty && _options.Controls.Count > 0;
            _outputBar.Visible = !empty;
            BtnAdd.Visible = !empty;
            EmptyView.Visible = empty;
            if (empty) EmptyView.BringToFront();
            else Grid.BringToFront();
            Panel bottom = BtnAdd.Parent as Panel;
            if (bottom != null) LayoutBottom(bottom);
            SyncScrollBar();
        }

        private void UpdateCountLabel()
        {
            if (!_running)
                CountLabel.Text = string.Format("共 {0} 个文件", Grid.Rows.Count);
        }

        // ---------- 移除 / 清空 ----------

        private void RemoveSelected()
        {
            if (_running) return;
            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in Grid.SelectedRows)
                if (!row.IsNewRow) rows.Add(row);

            foreach (DataGridViewRow row in rows)
            {
                FileItem item = row.Tag as FileItem;
                if (item != null)
                {
                    _paths.Remove(item.Path);
                    _rowHoverT.Remove(item);
                }
                Grid.Rows.Remove(row);
            }
            _hoverRow = -1;

            UpdateEmptyState();
            UpdateCountLabel();
            if (Grid.Rows.Count == 0) SetStatusText("");
        }

        private void OnClearClick(object sender, EventArgs e)
        {
            if (_running) return;
            Grid.Rows.Clear();
            _paths.Clear();
            _rowHoverT.Clear();
            _hoverRow = -1;
            _hoverAnimTimer.Stop();
            Progress.Value = 0;
            Progress.Snap();
            SetStatusText("");
            UpdateEmptyState();
            UpdateCountLabel();
        }

        // ---------- 列表绘制 ----------

        private void OnRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            FileItem item = Grid.Rows[e.RowIndex].Tag as FileItem;
            if (item == null) return;

            // Suppress every native row/cell paint layer.  Mixing DataGridView's
            // partial hover invalidation with owner painting can intermittently
            // reintroduce its grey focus/grid outline between adjacent rows.
            e.PaintParts = DataGridViewPaintParts.None;

            bool selected = Grid.Rows[e.RowIndex].Selected;
            float hoverT = RowHoverAmount(item);
            Rectangle rowRect = RowHighlightBounds(e.RowBounds.Top, e.RowBounds.Height);

            // SmoothingMode lives on the grid's shared Graphics and carries over
            // into the next row.  An antialiased FillRectangle leaves its top and
            // left edge pixel half covered, so the native grid line beneath shows
            // through as a 1px outline on every row after the first.
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using (SolidBrush clear = new SolidBrush(Theme.CardBack))
                e.Graphics.FillRectangle(clear,
                    new Rectangle(0, e.RowBounds.Top, Grid.ClientSize.Width, e.RowBounds.Height));

            if (selected || hoverT > 0.001f)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Theme.RoundedRect(rowRect, Theme.RadiusControl))
                using (SolidBrush b = new SolidBrush(selected
                    ? Theme.SelectedBack
                    : Theme.Lerp(Theme.CardBack, Theme.HoverBack, hoverT)))
                    e.Graphics.FillPath(b, path);
            }
        }

        private void OnRowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= Grid.Rows.Count) return;
            DataGridViewRow row = Grid.Rows[e.RowIndex];
            FileItem item = row.Tag as FileItem;
            if (item == null) return;

            Graphics g = e.Graphics;

            // Repaint one extra boundary pixel so separately invalidated adjacent
            // hover rows cannot leave a stale native divider between them.  Keep
            // the fill non-antialiased for the same reason as in OnRowPrePaint.
            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush clear = new SolidBrush(Theme.CardBack))
                g.FillRectangle(clear, new Rectangle(
                    0, e.RowBounds.Top, Grid.ClientSize.Width, e.RowBounds.Height + 1));

            bool selected = row.Selected;
            float hoverT = RowHoverAmount(item);
            if (selected || hoverT > 0.001f)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle highlight = RowHighlightBounds(e.RowBounds.Top, e.RowBounds.Height);
                using (GraphicsPath path = Theme.RoundedRect(highlight, Theme.RadiusControl))
                using (SolidBrush b = new SolidBrush(selected
                    ? Theme.SelectedBack
                    : Theme.Lerp(Theme.CardBack, Theme.HoverBack, hoverT)))
                    g.FillPath(b, path);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle fileCell = Grid.GetCellDisplayRectangle(0, e.RowIndex, false);
            Rectangle fileText = fileCell;
            fileText.X += 10;
            fileText.Width -= 16;
            TextRenderer.DrawText(g, Path.GetFileName(item.Path), Grid.Font, fileText, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            Rectangle stateCell = Grid.GetCellDisplayRectangle(1, e.RowIndex, false);
            DrawStatusPill(g, stateCell, item);
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (e.RowIndex < 0)
            {
                if (e.ColumnIndex < 0) return;
                using (SolidBrush headerBack = new SolidBrush(Theme.CardBack))
                    g.FillRectangle(headerBack, e.CellBounds);

                string text = Grid.Columns[e.ColumnIndex].HeaderText;
                Rectangle textRect = e.CellBounds;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
                if (e.ColumnIndex == 1)
                {
                    textRect.Inflate(-10, 0);
                    flags |= TextFormatFlags.HorizontalCenter;
                }
                else
                {
                    textRect.X += 10;
                    textRect.Width -= 12;
                    flags |= TextFormatFlags.Left;
                }
                TextRenderer.DrawText(g, text, _headerFont, textRect, Theme.Muted, flags);

                if (e.ColumnIndex == 0)
                {
                    using (Pen p = new Pen(Theme.Border))
                    {
                        int y = e.CellBounds.Bottom - 1;
                        g.DrawLine(p, 12, y, Grid.ClientSize.Width - 12, y);
                    }
                }
                e.Handled = true;
                return;
            }

            if (e.ColumnIndex < 0) return;

            DataGridViewRow row = Grid.Rows[e.RowIndex];
            FileItem item = row.Tag as FileItem;
            if (item == null) return;

            // CellPainting is sometimes called for only a narrow invalid strip
            // after a page switch.  Always clear that strip before drawing text;
            // otherwise ClearType pixels from the previous frame can remain.
            bool cellSelected = row.Selected;
            float cellHoverT = RowHoverAmount(item);
            GraphicsState backgroundState = g.Save();
            try
            {
                // Each cell event shares the grid Graphics.  Clip row-background
                // repainting to this cell so a later status-cell paint cannot cover
                // the filename text drawn by the file cell.
                g.SetClip(e.CellBounds, CombineMode.Intersect);
                using (SolidBrush clear = new SolidBrush(Theme.CardBack))
                    g.FillRectangle(clear, e.CellBounds);
                if (cellSelected || cellHoverT > 0.001f)
                {
                    Rectangle rowRect = RowHighlightBounds(e.CellBounds.Top, e.CellBounds.Height);
                    using (GraphicsPath path = Theme.RoundedRect(rowRect, Theme.RadiusControl))
                    using (SolidBrush b = new SolidBrush(cellSelected
                        ? Theme.SelectedBack
                        : Theme.Lerp(Theme.CardBack, Theme.HoverBack, cellHoverT)))
                        g.FillPath(b, path);
                }
            }
            finally
            {
                g.Restore(backgroundState);
            }

            if (e.ColumnIndex == 0)
            {
                Rectangle textRect = e.CellBounds;
                textRect.X += 10;
                textRect.Width -= 16;
                TextRenderer.DrawText(g, Path.GetFileName(item.Path), Grid.Font, textRect, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            else if (e.ColumnIndex == 1)
            {
                DrawStatusPill(g, e.CellBounds, item);
            }

            e.Handled = true;
        }

        private Rectangle RowHighlightBounds(int top, int height)
        {
            return new Rectangle(
                Theme.RowHighlightInsetX,
                top + Theme.RowHighlightInsetY,
                Math.Max(0, Grid.ClientSize.Width - Theme.RowHighlightInsetX * 2),
                Math.Max(0, height - Theme.RowHighlightInsetY * 2));
        }

        private static void StatusColors(FileState state,
            out string text, out Color back, out Color fore)
        {
            switch (state)
            {
                case FileState.Converting:
                    text = "处理中";
                    back = Theme.AccentSoft;
                    fore = Theme.Accent;
                    break;
                case FileState.Done:
                    text = "完成";
                    back = Theme.SuccessSoft;
                    fore = Theme.Success;
                    break;
                case FileState.Failed:
                    text = "失败";
                    back = Theme.DangerSoft;
                    fore = Theme.Danger;
                    break;
                default:
                    text = "等待";
                    back = Theme.PendingBack;
                    fore = Theme.PendingFore;
                    break;
            }
        }

        private void DrawStatusPill(Graphics g, Rectangle cell, FileItem item)
        {
            string text;
            Color back, fore;
            StatusColors(item.State, out text, out back, out fore);

            if (item.StateT < 1f)
            {
                string ignored;
                Color prevBack, prevFore;
                StatusColors(item.PrevState, out ignored, out prevBack, out prevFore);
                back = Theme.Lerp(prevBack, back, item.StateT);
                fore = Theme.Lerp(prevFore, fore, item.StateT);
            }

            Size textSize = TextRenderer.MeasureText(text, Grid.Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding);
            bool spinning = item.State == FileState.Converting;
            int h = 22;
            int icon = spinning ? 15 : 0;
            int w = textSize.Width + 22 + icon;
            Rectangle pill = new Rectangle(
                cell.Left + (cell.Width - w) / 2,
                cell.Top + (cell.Height - h) / 2,
                w, h);

            using (GraphicsPath path = Theme.CircularRoundedRect(pill, h / 2))
            using (SolidBrush b = new SolidBrush(back))
                g.FillPath(b, path);

            int textLeft = pill.Left + 11;
            if (spinning)
            {
                float d = 11f;
                float x = pill.Left + 9;
                float y = pill.Top + (h - d) / 2f;
                using (Pen p = new Pen(fore, 2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, x, y, d, d, _spinAngle, 250);
                }
                textLeft = (int)(x + d + 4);
            }

            Rectangle textRect = new Rectangle(textLeft, pill.Top, pill.Right - textLeft - 8, pill.Height);
            TextRenderer.DrawText(g, text, Grid.Font, textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void OnSpinTick(object sender, EventArgs e)
        {
            _spinAngle = (_spinAngle + 30) % 360;
            for (int i = 0; i < Grid.Rows.Count; i++)
            {
                FileItem item = Grid.Rows[i].Tag as FileItem;
                if (item != null && item.State == FileState.Converting)
                    Grid.InvalidateRow(i);
            }
        }

        private void OnPillTick(object sender, EventArgs e)
        {
            bool busy = false;
            for (int i = 0; i < Grid.Rows.Count; i++)
            {
                FileItem item = Grid.Rows[i].Tag as FileItem;
                if (item == null || item.StateT >= 1f) continue;

                item.StateT += (1f - item.StateT) * 0.25f;
                if (item.StateT > 0.99f) item.StateT = 1f;
                else busy = true;
                Grid.InvalidateRow(i);
            }
            if (!busy) _pillTimer.Stop();
        }

        private void BeginStateTransition(FileItem item, FileState state)
        {
            if (item.State == state) return;
            item.PrevState = item.State;
            item.State = state;
            item.StateT = 0f;
            if (!_pillTimer.Enabled) _pillTimer.Start();
        }

        private void OnGridMouseMove(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = Grid.HitTest(e.X, e.Y);
            SetHoverRow(hit.RowIndex);
        }

        private void OnGridMouseLeave(object sender, EventArgs e)
        {
            SetHoverRow(-1);
        }

        private void SetHoverRow(int rowIndex)
        {
            if (rowIndex == _hoverRow) return;
            int old = _hoverRow;
            _hoverRow = rowIndex;
            EnsureRowHoverEntry(old);
            EnsureRowHoverEntry(_hoverRow);
            if (!_hoverAnimTimer.Enabled) _hoverAnimTimer.Start();
            if (old >= 0 && old < Grid.Rows.Count) Grid.InvalidateRow(old);
            if (_hoverRow >= 0 && _hoverRow < Grid.Rows.Count) Grid.InvalidateRow(_hoverRow);
        }

        private void EnsureRowHoverEntry(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Grid.Rows.Count) return;
            FileItem item = Grid.Rows[rowIndex].Tag as FileItem;
            if (item != null && !_rowHoverT.ContainsKey(item)) _rowHoverT[item] = 0f;
        }

        private float RowHoverAmount(FileItem item)
        {
            float t;
            return item != null && _rowHoverT.TryGetValue(item, out t) ? t : 0f;
        }

        private void OnRowHoverTick(object sender, EventArgs e)
        {
            FileItem hovered = null;
            if (_hoverRow >= 0 && _hoverRow < Grid.Rows.Count)
                hovered = Grid.Rows[_hoverRow].Tag as FileItem;

            bool busy = false;
            List<FileItem> remove = null;
            foreach (FileItem item in new List<FileItem>(_rowHoverT.Keys))
            {
                float target = item == hovered ? 1f : 0f;
                float t = _rowHoverT[item];
                t += (target - t) * 0.35f;
                if (Math.Abs(target - t) < 0.01f) t = target;
                else busy = true;

                int index = RowIndexOf(item);
                if (t <= 0f || index < 0)
                {
                    if (remove == null) remove = new List<FileItem>();
                    remove.Add(item);
                }
                else
                {
                    _rowHoverT[item] = t;
                }

                if (index >= 0) Grid.InvalidateRow(index);
            }

            if (remove != null)
                foreach (FileItem item in remove) _rowHoverT.Remove(item);

            if (!busy) _hoverAnimTimer.Stop();
        }

        private int RowIndexOf(FileItem item)
        {
            for (int i = 0; i < Grid.Rows.Count; i++)
                if (Grid.Rows[i].Tag == item) return i;
            return -1;
        }

        private void OnGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            DataGridView.HitTestInfo hit = Grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0 && !Grid.Rows[hit.RowIndex].Selected)
            {
                Grid.ClearSelection();
                Grid.Rows[hit.RowIndex].Selected = true;
            }
        }

        private void OnGridDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            OpenSelectedOutput();
        }

        // ---------- 滚动条 ----------

        private void SyncScrollBar()
        {
            if (EmptyView == null || _scrollBar == null) return;

            if (EmptyView.Visible)
            {
                _scrollBar.Visible = false;
                return;
            }

            int total = Grid.Rows.Count;
            int visible = 0;
            try { visible = Grid.DisplayedRowCount(false); }
            catch { }
            if (visible <= 0) visible = 1;

            int first = 0;
            try { first = Grid.FirstDisplayedScrollingRowIndex; }
            catch { }
            if (first < 0) first = 0;

            _scrollBar.UpdateState(total, visible, first);
            _scrollBar.Visible = total > visible;
        }

        private void OnScrollValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (Grid.FirstDisplayedScrollingRowIndex != _scrollBar.Value)
                    Grid.FirstDisplayedScrollingRowIndex = _scrollBar.Value;
            }
            catch { }
        }

        // ---------- 打开结果 ----------

        private FileItem SelectedDoneOutput()
        {
            foreach (DataGridViewRow row in Grid.SelectedRows)
            {
                FileItem item = row.Tag as FileItem;
                if (item != null && item.State == FileState.Done &&
                    !string.IsNullOrEmpty(item.Output) && File.Exists(item.Output))
                    return item;
            }
            return null;
        }

        private FileItem FirstSelectedItem()
        {
            foreach (DataGridViewRow row in Grid.SelectedRows)
            {
                FileItem item = row.Tag as FileItem;
                if (item != null) return item;
            }
            return null;
        }

        private void OpenSelectedOutput()
        {
            FileItem item = SelectedDoneOutput();
            if (item == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(item.Output) { UseShellExecute = true });
            }
            catch { }
        }

        private void OpenSelectedFolder()
        {
            FileItem item = FirstSelectedItem();
            if (item == null) return;
            try
            {
                Process.Start("explorer.exe", "/select,\"" + item.Path + "\"");
            }
            catch { }
        }

        // ---------- 拖放 ----------

        private void WireDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += OnDragEnter;
            control.DragLeave += OnDragLeave;
            control.DragDrop += OnDragDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!_running && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                Card.Highlight = true;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragLeave(object sender, EventArgs e)
        {
            Card.Highlight = false;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            Card.Highlight = false;
            if (_running) return;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length > 0) AddFiles(paths);
        }

        // ---------- 快捷键 ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.O))
            {
                OnAddClick(null, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.Delete && Grid.ContainsFocus)
            {
                RemoveSelected();
                return true;
            }

            if (keyData == Keys.Enter)
            {
                Control focus = FindFocused(this);
                if (focus == BtnStart || focus == BtnClear || focus == BtnAdd || focus == EmptyView)
                    return base.ProcessCmdKey(ref msg, keyData);
                StartOrCancel();
                return true;
            }

            if (keyData == Keys.Escape && _running)
            {
                RequestCancel();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static Control FindFocused(Control container)
        {
            Control current = container;
            while (current is ContainerControl)
            {
                Control active = ((ContainerControl)current).ActiveControl;
                if (active == null) break;
                current = active;
            }
            return current;
        }

        // ---------- 状态文字 ----------

        private void SetStatusText(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatusText), text);
                return;
            }
            _fadeTimer.Stop();
            StatusLabel.ForeColor = Theme.Muted;
            StatusLabel.Text = text;
        }

        private void FlashStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, Color>(FlashStatus), text, color);
                return;
            }
            _fadeTimer.Stop();
            _fadeFrom = color;
            _fadeT = 0f;
            StatusLabel.Text = text;
            StatusLabel.ForeColor = color;
            _fadeTimer.Start();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            _fadeT += 0.035f;
            if (_fadeT >= 1f)
            {
                _fadeT = 1f;
                _fadeTimer.Stop();
            }
            StatusLabel.ForeColor = Theme.Lerp(_fadeFrom, Theme.Muted, _fadeT);
        }

        // ---------- 处理流程 ----------

        private void OnStartClick(object sender, EventArgs e)
        {
            StartOrCancel();
        }

        private void StartOrCancel()
        {
            if (_running)
            {
                RequestCancel();
                return;
            }

            if (Grid.Rows.Count == 0)
            {
                AppDialog.Show(FindForm(), "请先添加要处理的文件。", "提示");
                return;
            }

            List<int> indices = new List<int>();
            for (int i = 0; i < Grid.Rows.Count; i++)
            {
                FileItem item = Grid.Rows[i].Tag as FileItem;
                if (item != null && item.State != FileState.Done) indices.Add(i);
            }

            if (indices.Count == 0)
            {
                AppDialog.Show(FindForm(), "列表中的文件都已处理完成。", "提示");
                return;
            }

            List<string> paths = new List<string>();
            foreach (int i in indices)
                paths.Add(((FileItem)Grid.Rows[i].Tag).Path);

            object options = CaptureOptions();

            _running = true;
            _cancelRequested = false;
            BtnClear.Enabled = false;
            BtnAdd.Enabled = false;
            _outputPicker.Enabled = false;
            BtnStart.Accent = false;
            BtnStart.Text = "取消";
            SetStatusText("开始" + _verb + "…");
            Progress.Visible = true;
            Progress.Maximum = indices.Count;
            Progress.Value = 0;
            Progress.Snap();
            CountLabel.Text = string.Format("0 / {0}", indices.Count);
            UpdateTaskbar(0, indices.Count);
            Grid.ClearSelection();
            _spinTimer.Start();

            Task.Run(new Action(delegate
            {
                int ok = 0;
                int fail = 0;
                int done = 0;

                for (int n = 0; n < indices.Count; n++)
                {
                    if (_cancelRequested) break;

                    int index = indices[n];
                    string path = paths[n];
                    string name = Path.GetFileName(path);

                    SetRowState(index, FileState.Converting, null, null);
                    SetStatusText("正在" + _verb + "：" + name + "（" + (n + 1) + "/" + indices.Count + "）");

                    string output = null;
                    string error = null;
                    FileState state;

                    try
                    {
                        FileItem working = new FileItem();
                        working.Path = path;
                        Execute(working, options);
                        output = working.Output;
                        if (string.IsNullOrEmpty(output))
                            throw new IOException("未生成输出文件。");
                        ok++;
                        state = FileState.Done;
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        error = ex.Message;
                        state = FileState.Failed;
                    }

                    done++;
                    SetRowState(index, state, output, error);
                    SetProgressDone(done, indices.Count);
                }

                bool cancelled = _cancelRequested;
                if (IsDisposed) return;
                try { BeginInvoke(new Action<int, int, bool>(OnProcessingDone), ok, fail, cancelled); }
                catch (InvalidOperationException) { }
            }));
        }

        private void RequestCancel()
        {
            if (!_running || _cancelRequested) return;
            _cancelRequested = true;
            BtnStart.Enabled = false;
            BtnStart.Text = "取消中…";
            SetStatusText("将在当前文件处理完成后停止…");
        }

        private void SetRowState(int index, FileState state, string output, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, FileState, string, string>(SetRowState), index, state, output, error);
                return;
            }
            if (index < 0 || index >= Grid.Rows.Count) return;

            FileItem item = Grid.Rows[index].Tag as FileItem;
            if (item == null) return;

            BeginStateTransition(item, state);
            if (output != null) item.Output = output;
            item.Error = error;

            DataGridViewRow row = Grid.Rows[index];
            row.Cells[1].Value = StatusText(state);
            row.Cells[1].ToolTipText = state == FileState.Failed ? error
                : state == FileState.Done ? "输出：" + Path.GetFileName(output)
                : null;
            Grid.InvalidateRow(index);
        }

        // 选项变化后，已完成的行需要按新选项重新处理，重置为等待。
        protected void ResetCompletedRows()
        {
            if (_running) return;

            int count = 0;
            for (int i = 0; i < Grid.Rows.Count; i++)
            {
                FileItem item = Grid.Rows[i].Tag as FileItem;
                if (item == null || item.State != FileState.Done) continue;

                BeginStateTransition(item, FileState.Waiting);
                item.Output = null;
                item.Error = null;

                DataGridViewRow row = Grid.Rows[i];
                row.Cells[1].Value = StatusText(FileState.Waiting);
                row.Cells[1].ToolTipText = null;
                Grid.InvalidateRow(i);
                count++;
            }

            if (count > 0)
                SetStatusText(string.Format("选项已更改，{0} 个已完成文件已重置为等待。", count));
        }

        private void SetProgressDone(int done, int total)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int>(SetProgressDone), done, total);
                return;
            }
            Progress.Maximum = Math.Max(1, total);
            Progress.Value = done;
            CountLabel.Text = string.Format("{0} / {1}", done, total);
            UpdateTaskbar(done, total);
        }

        private void UpdateTaskbar(int done, int total)
        {
            if (_taskbar == null)
            {
                Form form = FindForm();
                if (form != null && form.IsHandleCreated)
                    _taskbar = new TaskbarProgress(form.Handle);
            }
            if (_taskbar != null) _taskbar.SetValue(done, total);
        }

        private void OnProcessingDone(int ok, int fail, bool cancelled)
        {
            _running = false;
            _cancelRequested = false;
            BtnStart.Enabled = true;
            BtnStart.Accent = true;
            BtnStart.Text = _startText;
            BtnClear.Enabled = true;
            BtnAdd.Enabled = true;
            _outputPicker.Enabled = true;
            Progress.Value = Progress.Maximum;
            Progress.Snap();
            Progress.Visible = false;
            _spinTimer.Stop();
            if (_taskbar != null) _taskbar.Clear();

            UpdateCountLabel();

            if (cancelled)
            {
                FlashStatus(string.Format("已取消：成功 {0} 个，失败 {1} 个，其余保持等待。", ok, fail),
                    Theme.Text);
            }
            else if (fail == 0)
            {
                FlashStatus(string.Format("完成：全部 {0} 个文件{1}成功。", ok, _verb), Theme.Success);
            }
            else
            {
                FlashStatus(string.Format("完成：成功 {0} 个，失败 {1} 个。失败项可再次点击“{2}”重试。",
                    ok, fail, _startText), Theme.Danger);
            }
        }

        private static string StatusText(FileState state)
        {
            switch (state)
            {
                case FileState.Converting: return "处理中";
                case FileState.Done: return "完成";
                case FileState.Failed: return "失败";
                default: return "等待";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinTimer.Stop();
                _spinTimer.Dispose();
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _hoverAnimTimer.Stop();
                _hoverAnimTimer.Dispose();
                _pillTimer.Stop();
                _pillTimer.Dispose();
                _headerFont.Dispose();
                if (_menuPopup != null)
                {
                    _menuPopup.Dispose();
                    _menuPopup = null;
                }
                if (_taskbar != null)
                {
                    _taskbar.Dispose();
                    _taskbar = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
