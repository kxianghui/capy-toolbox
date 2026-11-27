using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PdfWordTool
{
    public class MainForm : Form
    {
        private const int WmSetRedraw = 0x000B;
        private const int WmNcCalcSize = 0x0083;
        private const int WmWindowPosChanged = 0x0047;
        private const int WsBorder = 0x00800000;
        private const int WsCaption = 0x00C00000;
        private const uint RdwInvalidate = 0x0001;
        private const uint RdwErase = 0x0004;
        private const uint RdwAllChildren = 0x0080;
        private const uint RdwUpdateNow = 0x0100;

        private struct NcRect
        {
            public int Left, Top, Right, Bottom;
        }

        private struct NcCalcSizeParams
        {
            public NcRect Rgrc0;
            public NcRect Rgrc1;
            public NcRect Rgrc2;
            public IntPtr WindowPos;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(
            IntPtr hWnd, IntPtr updateRect, IntPtr updateRegion, uint flags);

        private readonly TitleBar _titleBar;
        private readonly TabStrip _tabs;
        private readonly ToolPage[] _pages;
        private readonly Panel _host;
        private readonly GearButton _settingsButton;
        private bool _pagesWarmed;
        private bool _sizeCalcHack;

        private NotifyIcon _tray;
        private Icon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private RoundedPopup _trayPopup;
        private bool _trayTipShown;
        private bool _minimizeToTray;
        private bool _exitRequested;

        public MainForm()
        {
            Text = BuildInfo.Title;
            Icon = AppIcon.Create();
            ClientSize = new Size(880, 640);
            MinimumSize = new Size(720, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.FormBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            DoubleBuffered = true;

            _host = new BufferedPanel();
            _host.Dock = DockStyle.Fill;
            _host.BackColor = Theme.FormBack;

            _titleBar = new TitleBar();
            _titleBar.Dock = DockStyle.Top;
            _titleBar.MinimizeRequested += delegate { WindowState = FormWindowState.Minimized; };
            _titleBar.MaximizeRequested += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            _titleBar.CloseRequested += delegate { Close(); };

            _tabs = new TabStrip();
            _tabs.Dock = DockStyle.Top;
            _tabs.SetItems(new string[] { "PDF转Word", "图片压缩", "图片格式转换" });
            _tabs.SelectedIndexChanged += delegate { ShowPage(_tabs.SelectedIndex); };

            _pages = new ToolPage[]
            {
                new PdfToWordPage(),
                new ImageCompressPage(),
                new ImageConvertPage()
            };

            foreach (ToolPage page in _pages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                _host.Controls.Add(page);
            }

            // 左下角设置入口：覆盖在页面底部状态行左侧，位置随窗口尺寸调整。
            _settingsButton = new GearButton();
            _settingsButton.Click += delegate { OpenSettings(); };

            Controls.Add(_host);
            Controls.Add(_tabs);
            Controls.Add(_titleBar);
            Controls.Add(_settingsButton);
            _settingsButton.BringToFront();

            InitializeTray();
            ShowPage(0);
        }

        private void InitializeTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Renderer = new ThemedMenuRenderer();
            _trayMenu.ShowImageMargin = false;
            _trayMenu.ShowCheckMargin = false;
            _trayMenu.BackColor = Theme.CardBack;
            _trayMenu.ForeColor = Theme.Text;
            _trayMenu.Font = Font;
            _trayMenu.Padding = new Padding(
                Theme.PopupBorderInset, Theme.MenuPaddingY,
                Theme.PopupBorderInset, Theme.MenuPaddingY);

            ToolStripMenuItem restore = new ToolStripMenuItem("显示主界面");
            restore.Click += delegate { RestoreFromTray(); };
            ToolStripMenuItem settings = new ToolStripMenuItem("打开设置");
            settings.Click += delegate { OpenSettings(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { ExitFromTray(); };

            _trayMenu.Items.Add(restore);
            _trayMenu.Items.Add(settings);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exit);
            int textWidth = 0;
            using (Graphics measure = Graphics.FromHwnd(IntPtr.Zero))
            {
                foreach (ToolStripItem item in _trayMenu.Items)
                {
                    ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                    if (menuItem == null) continue;
                    // 下拉菜单会把菜单项的垂直 Padding 全部堆在文字下方，导致首项贴顶、
                    // 末项下方留白过大；改用垂直 Margin 分配间距，上下留白才对称。
                    menuItem.Padding = new Padding(
                        Theme.MenuItemPaddingX, 0, Theme.MenuItemPaddingX, 0);
                    menuItem.Margin = new Padding(0, 3, 0, 3);
                    // 菜单项没有快捷键，关闭预留列可去掉右侧一块无用空白。
                    menuItem.ShowShortcutKeys = false;
                    textWidth = Math.Max(textWidth, TextRenderer.MeasureText(
                        measure, menuItem.Text, _trayMenu.Font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
                }
            }
            // 下拉菜单会为每项预留右侧箭头列，短文案下右侧留白明显；按最宽文案限宽使左右对称。
            _trayMenu.MaximumSize = new Size(textWidth + Theme.MenuItemPaddingX * 2, 0);

            // 与页面内右键菜单保持一致的圆角外观（RoundedPopup 同时关闭系统阴影）。
            _trayPopup = new RoundedPopup(_trayMenu, Theme.RadiusControl);

            _trayIcon = AppIcon.CreateTray();
            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "Capy";
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { RestoreFromTray(); };
            _tray.Visible = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WinForms 换算窗口/客户区尺寸时会用 AdjustWindowRectEx 按样式加回非客户区。
                // 本窗口客户区盖住了系统标题栏，实际非客户区只剩缩放宽边框；
                // 计算期间去掉标题栏样式、补上 WS_BORDER，使其恰好换算成 8px 边框。
                if (_sizeCalcHack)
                {
                    cp.Style &= ~WsCaption;
                    cp.Style |= WsBorder;
                }
                return cp;
            }
        }

        protected override void SetClientSizeCore(int x, int y)
        {
            _sizeCalcHack = true;
            try
            {
                base.SetClientSizeCore(x, y);
            }
            finally
            {
                _sizeCalcHack = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmWindowPosChanged)
            {
                _sizeCalcHack = true;
                try
                {
                    base.WndProc(ref m);
                }
                finally
                {
                    _sizeCalcHack = false;
                }
                return;
            }
            if (m.Msg == WmNcCalcSize && m.WParam != IntPtr.Zero)
            {
                base.WndProc(ref m);
                // 客户区上沿扩展一个标题栏高度，盖掉系统标题栏（由 TitleBar 自绘）。
                // 普通与最大化状态都成立：最大化时客户区恰好对齐工作区顶部。
                // 左右/下边框保持原生行为，继续支持缩放。
                NcCalcSizeParams p = (NcCalcSizeParams)Marshal.PtrToStructure(
                    m.LParam, typeof(NcCalcSizeParams));
                p.Rgrc0.Top -= SystemInformation.CaptionHeight;
                Marshal.StructureToPtr(p, m.LParam, false);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);
            if (_titleBar != null)
                _titleBar.Maximized = WindowState == FormWindowState.Maximized;
            if (_settingsButton != null)
                _settingsButton.Location = new Point(16, ClientSize.Height - 56 + 13);
            if (WindowState == FormWindowState.Minimized && _minimizeToTray)
            {
                _minimizeToTray = false;
                HideToTray();
            }
        }

        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
            _tray.Visible = true;

            if (_trayTipShown) return;
            _trayTipShown = true;
            _tray.ShowBalloonTip(2500, "Capy", "已最小化到托盘，双击图标可恢复。", ToolTipIcon.Info);
        }

        // 由后续启动的实例唤起：无论主窗口是隐藏到托盘还是最小化，都恢复并置于前台。
        public void ActivateFromSecondInstance()
        {
            if (IsDisposed) return;
            RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            // 必须在 Show 前恢复为普通状态，否则窗口会以最小化状态重新显示。
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Show();
            Activate();
        }

        private void ExitFromTray()
        {
            _exitRequested = true;
            _tray.Visible = false;
            Close();
        }

        private void OpenSettings()
        {
            // 最小化的窗口仍处于 Visible 状态，其坐标为 -32000；
            // 不先恢复的话，以它为父窗口居中的对话框会被钳制到屏幕左上角。
            if (!Visible || WindowState == FormWindowState.Minimized)
                RestoreFromTray();
            using (SettingsForm dlg = new SettingsForm())
                dlg.ShowDialog(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || _exitRequested) return;
            if (e.CloseReason != CloseReason.UserClosing) return;

            // 点右上角关闭：按记住的行为直接处理，未记住时询问是驻留托盘还是退出。
            AppSettings.CloseAction action = AppSettings.CloseBehavior;
            if (action == AppSettings.CloseAction.Ask)
            {
                bool remember;
                bool? minimize = AppDialog.ConfirmMinimize(this, out remember);
                if (minimize == null)
                {
                    // 通过关闭按钮或 Alt+F4 取消弹框：放弃本次关闭，程序继续运行。
                    e.Cancel = true;
                    return;
                }
                action = minimize.Value ? AppSettings.CloseAction.Minimize : AppSettings.CloseAction.Exit;
                if (remember) AppSettings.CloseBehavior = action;
            }

            if (action == AppSettings.CloseAction.Exit) return;

            e.Cancel = true;
            // 只有关闭按钮触发的最小化才隐藏到托盘，标题栏最小化仍保留任务栏图标。
            _minimizeToTray = true;
            WindowState = FormWindowState.Minimized;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_trayPopup != null) _trayPopup.Dispose();
                if (_trayMenu != null) _trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnLoad(System.EventArgs e)
        {
            base.OnLoad(e);
            WarmPageHandles();
        }

        private void WarmPageHandles()
        {
            if (_pagesWarmed) return;
            _pagesWarmed = true;

            // Hidden WinForms pages lazily create child HWNDs on first display.
            // Force handle creation and layout before the form becomes visible so
            // the user's first tab switch has the same cost as later switches.
            _host.PerformLayout();
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].PerformLayout();
                WarmControlTree(_pages[i]);
            }
        }

        private static void WarmControlTree(Control control)
        {
            if (control == null || control.IsDisposed) return;
            System.IntPtr unused = control.Handle;
            foreach (Control child in control.Controls)
                WarmControlTree(child);
        }

        private void ShowPage(int index)
        {
            ToolPage target = _pages[index];
            bool freezePaint = IsHandleCreated && Visible && _host.IsHandleCreated;
            if (freezePaint)
                SendMessage(_host.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);

            _host.SuspendLayout();
            try
            {
                // Put the target above the current page before hiding anything.
                // This keeps a fully painted page covering the host at every point
                // in the switch and avoids the one-frame FormBack flash.
                target.Visible = true;
                target.BringToFront();
                target.PerformLayout();
                target.Invalidate(true);

                for (int i = 0; i < _pages.Length; i++)
                    if (_pages[i] != target) _pages[i].Visible = false;

                if (IsHandleCreated)
                    target.FocusGrid();
            }
            finally
            {
                _host.ResumeLayout(true);
                if (freezePaint)
                {
                    SendMessage(_host.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                    RedrawWindow(_host.Handle, IntPtr.Zero, IntPtr.Zero,
                        RdwInvalidate | RdwErase | RdwAllChildren | RdwUpdateNow);
                }
            }
        }
    }

}
