using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PdfWordTool
{
    sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }
    }

    static class Theme
    {
        public static readonly Color FormBack = Color.FromArgb(246, 248, 251);
        public static readonly Color CardBack = Color.White;
        public static readonly Color Border = Color.FromArgb(223, 228, 235);
        public static readonly Color BorderHover = Color.FromArgb(193, 202, 214);
        public static readonly Color Text = Color.FromArgb(31, 36, 44);
        public static readonly Color Muted = Color.FromArgb(110, 119, 132);
        public static readonly Color Accent = Color.FromArgb(47, 107, 255);
        public static readonly Color AccentHover = Color.FromArgb(31, 91, 235);
        public static readonly Color AccentPressed = Color.FromArgb(23, 76, 205);
        public static readonly Color AccentSoft = Color.FromArgb(234, 241, 255);
        public static readonly Color SelectedBack = Color.FromArgb(234, 241, 255);
        public static readonly Color HoverBack = Color.FromArgb(244, 247, 251);
        public static readonly Color DisabledBack = Color.FromArgb(241, 243, 246);
        public static readonly Color DisabledText = Color.FromArgb(166, 173, 184);
        public static readonly Color Success = Color.FromArgb(31, 157, 85);
        public static readonly Color SuccessSoft = Color.FromArgb(232, 247, 238);
        public static readonly Color Danger = Color.FromArgb(214, 59, 59);
        public static readonly Color DangerSoft = Color.FromArgb(253, 236, 236);
        public static readonly Color PendingBack = Color.FromArgb(238, 241, 245);
        public static readonly Color PendingFore = Color.FromArgb(91, 100, 114);
        public static readonly Color ScrollTrack = Color.FromArgb(238, 241, 245);
        public static readonly Color ScrollThumb = Color.FromArgb(203, 211, 222);
        public static readonly Color ScrollThumbHover = Color.FromArgb(166, 177, 192);

        public const int RadiusControl = 8;
        public const int RadiusCard = 12;
        public const int RadiusPill = 16;

        public const int FileColumnMinWidth = 140;
        public const int StateColumnWidth = 140;
        public const int RowHighlightInsetX = 4;
        public const int RowHighlightInsetY = 2;

        public const int PopupBorderInset = 1;
        public const int PopupPadding = 2;
        public const int MenuHoverInsetX = 4;
        public const int MenuHoverInsetY = 1;
        public const int MenuPaddingY = 4;
        public const int MenuItemPaddingX = 12;
        public const int MenuItemPaddingY = 6;

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // 连续曲率圆角（superellipse）。1/4 圆弧在直线与弧的交点处曲率从 0 突然
        // 跳到 1/r，视觉上会“割裂”；superellipse（指数 4）在切点处曲率同样为 0，
        // 从直边平滑过渡到圆角，接近 Apple 的连续曲率观感。
        private const float CornerExponent = 4f;
        private const int CornerSegments = 12;

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            return RoundedRect(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
        }

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0f || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }
            float r = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
            float left = bounds.Left, top = bounds.Top;
            float right = bounds.Right, bottom = bounds.Bottom;

            List<PointF> points = new List<PointF>(CornerSegments * 4 + 4);
            points.Add(new PointF(left + r, top));
            points.Add(new PointF(right - r, top));
            AppendSmoothCorner(points,
                new PointF(right - r, top), new PointF(right, top + r), new PointF(right, top));
            points.Add(new PointF(right, bottom - r));
            AppendSmoothCorner(points,
                new PointF(right, bottom - r), new PointF(right - r, bottom), new PointF(right, bottom));
            points.Add(new PointF(left + r, bottom));
            AppendSmoothCorner(points,
                new PointF(left + r, bottom), new PointF(left, bottom - r), new PointF(left, bottom));
            points.Add(new PointF(left, top + r));
            AppendSmoothCorner(points,
                new PointF(left, top + r), new PointF(left + r, top), new PointF(left, top));

            path.AddPolygon(points.ToArray());
            return path;
        }

        // 胶囊类形状（圆角 = 高度/2）必须保持标准半圆，不能用 superellipse，
        // 否则端部会被压扁。
        public static GraphicsPath CircularRoundedRect(Rectangle bounds, int radius)
        {
            return CircularRoundedRect(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
        }

        public static GraphicsPath CircularRoundedRect(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0f || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }
            radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
            float d = radius * 2f;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // from/to 是该圆角的两条切点，sharp 是包围盒的直角点。曲线以 superellipse
        // 的 (1 - cos^p, 1 - sin^p) 权重在两条切线方向间插值：t=0 落在 from，
        // t=π/2 落在 to，且中部比圆弧更贴近直角点（squircle 的“饱满”观感）。
        private static void AppendSmoothCorner(List<PointF> points,
            PointF from, PointF to, PointF sharp)
        {
            float ax = from.X - sharp.X, ay = from.Y - sharp.Y;
            float bx = to.X - sharp.X, by = to.Y - sharp.Y;
            double power = 2.0 / CornerExponent;
            for (int i = 1; i < CornerSegments; i++)
            {
                double t = (Math.PI / 2.0) * i / CornerSegments;
                float wa = 1f - (float)Math.Pow(Math.Sin(t), power);
                float wb = 1f - (float)Math.Pow(Math.Cos(t), power);
                points.Add(new PointF(
                    sharp.X + ax * wa + bx * wb,
                    sharp.Y + ay * wa + by * wb));
            }
        }
    }

    // 圆角弹层：关闭系统阴影，把弹层 HWND 裁成圆角 Region。
    // 边框由 RoundedPopupRenderer 内缩绘制，避免被 Region 裁掉四角/右侧/底部。
    sealed class RoundedPopup : IDisposable
    {
        private readonly ToolStripDropDown _popup;
        private readonly int _radius;
        private Region _region;
        private bool _disposed;

        public RoundedPopup(ToolStripDropDown popup, int radius)
        {
            _popup = popup;
            _radius = Math.Max(1, radius);
            _popup.DropShadowEnabled = false;
            _popup.Opened += OnPopupBoundsChanged;
            _popup.SizeChanged += OnPopupBoundsChanged;
            _popup.Disposed += OnPopupDisposed;
        }

        private void OnPopupBoundsChanged(object sender, EventArgs e)
        {
            ApplyRegion();
        }

        private void ApplyRegion()
        {
            if (_disposed || _popup == null || _popup.IsDisposed) return;
            if (_popup.Width <= _radius * 2 || _popup.Height <= _radius * 2) return;

            Region next = null;
            try
            {
                // Region geometry uses continuous client coordinates [0, Width] and
                // [0, Height].  Keep the complete client area here; the visible 1px
                // border is drawn separately on a 1px-inset path so the hard region
                // clip never cuts through the anti-aliased stroke.
                int radius = Math.Max(1,
                    (int)Math.Round(_radius * (_popup.DeviceDpi / 96f)));
                using (GraphicsPath path = Theme.RoundedRect(
                    new RectangleF(0f, 0f, _popup.Width, _popup.Height), radius))
                {
                    next = new Region(path);
                }

                Region previous = _region;
                _region = next;
                next = null;
                _popup.Region = _region;
                if (previous != null) previous.Dispose();
            }
            catch
            {
                if (next != null) next.Dispose();
            }
        }

        private void OnPopupDisposed(object sender, EventArgs e)
        {
            Release();
        }

        public void Dispose()
        {
            Release();
        }

        private void Release()
        {
            if (_disposed) return;
            _disposed = true;

            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Opened -= OnPopupBoundsChanged;
                _popup.SizeChanged -= OnPopupBoundsChanged;
                _popup.Disposed -= OnPopupDisposed;
                try { _popup.Region = null; } catch { }
            }

            if (_region != null)
            {
                _region.Dispose();
                _region = null;
            }
        }
    }

    class RoundedPopupRenderer : ToolStripRenderer
    {
        private readonly int _radius;

        public RoundedPopupRenderer(int radius)
        {
            _radius = Math.Max(1, radius);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            RectangleF r = new RectangleF(0f, 0f,
                Math.Max(0, e.ToolStrip.Width),
                Math.Max(0, e.ToolStrip.Height));
            if (r.Width <= 0 || r.Height <= 0) return;

            int radius = Math.Max(1,
                (int)Math.Round(_radius * (e.ToolStrip.DeviceDpi / 96f)));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundedRect(r, radius))
            using (SolidBrush b = new SolidBrush(Theme.CardBack))
                e.Graphics.FillPath(b, path);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // 1px 描边在浅色圆角面上很容易被抗锯齿切成断线：
            // 画笔中心落在整数坐标时会跨两个像素、各覆盖 50%，弧段覆盖量
            // 忽高忽低，看起来就是锯齿/断裂。
            // 改用“外圈填充 + 内圈填充”做差：两条实心圆角路径都按整像素内缩，
            // 直线段得到干净的 1px 实色，弧段交给填充抗锯齿平滑过渡。
            float radius = _radius * (e.ToolStrip.DeviceDpi / 96f);
            DrawRing(e.Graphics,
                new RectangleF(1f, 1f, e.ToolStrip.Width - 2f, e.ToolStrip.Height - 2f),
                new RectangleF(2f, 2f, e.ToolStrip.Width - 4f, e.ToolStrip.Height - 4f),
                radius);
        }

        internal static void DrawRing(Graphics g, RectangleF outer, RectangleF inner, float radius)
        {
            if (outer.Width <= 0 || outer.Height <= 0) return;
            if (inner.Width <= 0 || inner.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 只填充外圈与内圈之间的环带，绝不能重绘内部：
            // OnRenderToolStripBorder 在菜单项之后才绘制，若这里铺底色
            // 会把刚画好的下拉项整片盖掉（表现为空白菜单）。
            using (GraphicsPath ring = new GraphicsPath(FillMode.Alternate))
            {
                using (GraphicsPath edge = Theme.RoundedRect(outer, Math.Max(1f, radius - 1f)))
                    ring.AddPath(edge, false);
                using (GraphicsPath hole = Theme.RoundedRect(inner, Math.Max(1f, radius - 2f)))
                    ring.AddPath(hole, false);
                using (SolidBrush brush = new SolidBrush(Theme.Border))
                    g.FillPath(brush, ring);
            }
        }
    }

    sealed class PillButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private float _hoverT;
        private float _pressT;
        private readonly Timer _anim;

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.FormBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        public bool Accent { get; set; }

        private void OnAnimTick(object sender, EventArgs e)
        {
            float hoverTarget = _hover && Enabled ? 1f : 0f;
            float pressTarget = _pressed && Enabled ? 1f : 0f;
            _hoverT += (hoverTarget - _hoverT) * 0.35f;
            _pressT += (pressTarget - _pressT) * 0.5f;

            bool hoverDone = Math.Abs(hoverTarget - _hoverT) < 0.01f;
            bool pressDone = Math.Abs(pressTarget - _pressT) < 0.01f;
            if (hoverDone) _hoverT = hoverTarget;
            if (pressDone) _pressT = pressTarget;
            if (hoverDone && pressDone) _anim.Stop();

            Invalidate();
        }

        private void Animate()
        {
            if (!_anim.Enabled) _anim.Start();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Animate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Animate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Animate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed) { _pressed = false; Animate(); }
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled) { _hover = false; _pressed = false; }
            Animate();
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int inset = (int)Math.Round(_pressT);
            Rectangle r = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
            Color fill, border, text;

            if (!Enabled)
            {
                fill = Theme.DisabledBack;
                border = Theme.Border;
                text = Theme.DisabledText;
            }
            else if (Accent)
            {
                fill = Theme.Lerp(Theme.Lerp(Theme.Accent, Theme.AccentHover, _hoverT),
                    Theme.AccentPressed, _pressT);
                border = fill;
                text = Color.White;
            }
            else
            {
                fill = Theme.Lerp(Theme.Lerp(Color.White, Theme.HoverBack, _hoverT),
                    Color.FromArgb(232, 237, 245), _pressT);
                border = Theme.Lerp(Theme.Lerp(Theme.Border, Theme.BorderHover, _hoverT),
                    Theme.BorderHover, _pressT);
                text = Theme.Text;
            }

            using (GraphicsPath path = Theme.RoundedRect(r, Theme.RadiusControl))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }

            Rectangle textRect = ClientRectangle;
            textRect.Offset(0, inset);
            TextRenderer.DrawText(g, Text, Font, textRect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Color ring = Accent && Enabled
                    ? Color.FromArgb(150, 255, 255, 255)
                    : Theme.Accent;
                Rectangle inner = Rectangle.Inflate(r, -3, -3);
                using (GraphicsPath path = Theme.RoundedRect(inner, Math.Max(4, Theme.RadiusControl - 3)))
                using (Pen p = new Pen(ring))
                    g.DrawPath(p, path);
            }
        }
    }

    enum ThemedDialogKind
    {
        Info,
        Error
    }

    // 主题化模态对话框：无系统边框，圆角 Region 裁切，CardBack 背景 + 1px 内缩边框。
    sealed class ThemedDialog : Form
    {
        private const int DialogWidth = 400;
        private const int Pad = 24;
        private const int TitleHeight = 26;
        private const int TitleGap = 8;
        private const int MinMessageHeight = 20;
        private const int ButtonGap = 24;
        private const int ButtonWidth = 96;
        private const int ButtonHeight = 34;
        private const int CheckboxHeight = 24;
        private const int CheckboxGap = 14;

        private readonly string _title;
        private readonly string _message;
        private readonly ThemedDialogKind _kind;
        private readonly Font _titleFont;
        private readonly Font _messageFont;
        private readonly PillButton _ok;
        private readonly PillButton _exit;
        private readonly ThemedCheckBox _remember;
        private readonly DialogCloseButton _close;
        private readonly int _messageHeight;

        public ThemedDialog(string title, string message, ThemedDialogKind kind,
                            bool confirm = false, bool remember = false)
        {
            _title = string.IsNullOrEmpty(title) ? "提示" : title;
            _message = message ?? string.Empty;
            _kind = kind;

            Text = _title;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.CardBack;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _titleFont = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _messageFont = new Font("Microsoft YaHei UI", 9.5F);

            _messageHeight = MeasureMessageHeight();
            int height = Pad + TitleHeight + TitleGap + _messageHeight +
                         (remember ? CheckboxGap + CheckboxHeight : 0) +
                         ButtonGap + ButtonHeight + Pad;
            ClientSize = new Size(DialogWidth, height);

            _close = new DialogCloseButton();
            _close.Location = new Point(DialogWidth - Pad - _close.Width, Pad - 1);
            Controls.Add(_close);

            if (remember)
            {
                _remember = new ThemedCheckBox();
                _remember.Text = "记住我的选择";
                _remember.Location = new Point(Pad,
                    Pad + TitleHeight + TitleGap + _messageHeight + CheckboxGap);
                _remember.Size = new Size(DialogWidth - Pad * 2, CheckboxHeight);
                Controls.Add(_remember);
            }

            _ok = new PillButton();
            _ok.Text = confirm ? "最小化到托盘" : "确定";
            _ok.Accent = true;
            _ok.BackColor = Theme.CardBack;
            _ok.DialogResult = confirm ? DialogResult.Yes : DialogResult.OK;
            int okWidth = confirm ? 128 : ButtonWidth;
            _ok.Size = new Size(okWidth, ButtonHeight);
            _ok.Location = new Point(DialogWidth - Pad - okWidth, height - Pad - ButtonHeight);
            Controls.Add(_ok);

            if (confirm)
            {
                _exit = new PillButton();
                _exit.Text = "退出";
                _exit.BackColor = Theme.CardBack;
                _exit.DialogResult = DialogResult.No;
                _exit.Size = new Size(80, ButtonHeight);
                _exit.Location = new Point(_ok.Left - 8 - _exit.Width, _ok.Top);
                Controls.Add(_exit);
            }

            AcceptButton = _ok;
            CancelButton = _ok;
        }

        public bool RememberChoice
        {
            get { return _remember != null && _remember.Checked; }
        }

        private int MeasureMessageHeight()
        {
            int width = DialogWidth - Pad * 2;
            Size measured = TextRenderer.MeasureText(_message, _messageFont,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            int height = Math.Max(MinMessageHeight, measured.Height + 2);

            // 极端长文本（如未处理异常堆栈）不让对话框超出屏幕；
            // 超出部分在绘制区裁切，保持窗口可用。
            int maxHeight = Screen.PrimaryScreen.WorkingArea.Height - 240;
            return Math.Min(height, Math.Max(MinMessageHeight, maxHeight));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyRegion();
            _ok.Focus();
        }

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int radius = (int)Math.Round(Theme.RadiusCard * (DeviceDpi / 96f));
            using (GraphicsPath path = Theme.RoundedRect(
                new RectangleF(0f, 0f, Width, Height), radius))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CardBack);

            // 与弹层一致：用内外两条实心圆角路径做差绘制 1px 边框，
            // 避免 1px 描边在浅色圆角面上被抗锯齿切成断线。
            float radius = Theme.RadiusCard * (DeviceDpi / 96f);
            RoundedPopupRenderer.DrawRing(g,
                new RectangleF(1f, 1f, Width - 2f, Height - 2f),
                new RectangleF(2f, 2f, Width - 4f, Height - 4f),
                radius);

            Rectangle titleRect = new Rectangle(Pad, Pad, Width - Pad * 2 - 36, TitleHeight);
            TextRenderer.DrawText(g, _title, _titleFont, titleRect,
                _kind == ThemedDialogKind.Error ? Theme.Danger : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            Rectangle messageRect = new Rectangle(Pad, Pad + TitleHeight + TitleGap,
                Width - Pad * 2, _messageHeight);
            TextRenderer.DrawText(g, _message, _messageFont, messageRect, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _messageFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // 弹框右上角的关闭按钮：悬停时显示浅灰圆角底色，点击以 Cancel 关闭弹框。
    sealed class DialogCloseButton : Control
    {
        private readonly Font _glyphFont;
        private bool _hover;

        public DialogCloseButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            _glyphFont = new Font("Segoe MDL2 Assets", 9F);
            Cursor = Cursors.Hand;
            TabStop = false;
            Size = new Size(28, 28);
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
            base.OnClick(e);
            Form form = FindForm();
            if (form == null) return;
            form.DialogResult = DialogResult.Cancel;
            form.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_hover)
            {
                using (GraphicsPath path = Theme.CircularRoundedRect(
                    new Rectangle(1, 1, Width - 2, Height - 2), 6))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(228, 232, 239)))
                    g.FillPath(b, path);
            }

            TextRenderer.DrawText(g, "\uE8BB", _glyphFont, ClientRectangle,
                _hover ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _glyphFont.Dispose();
            base.Dispose(disposing);
        }
    }

    static class AppDialog
    {
        public static void Show(IWin32Window owner, string message, string title)
        {
            Show(owner, message, title, ThemedDialogKind.Info);
        }

        public static void ShowError(IWin32Window owner, string message, string title)
        {
            Show(owner, message, title, ThemedDialogKind.Error);
        }

        // 返回 true 表示最小化到托盘，false 表示退出，null 表示关闭了对话框取消操作；
        // remember 表示勾选了「记住我的选择」。
        public static bool? ConfirmMinimize(IWin32Window owner, out bool remember)
        {
            using (ThemedDialog dlg = new ThemedDialog(
                "关闭 Capy",
                "最小化到托盘可让程序继续在后台运行；选择「退出」将直接关闭程序。\r\n" +
                "可在「设置」中修改此行为。",
                ThemedDialogKind.Info, true, true))
            {
                DialogResult result = dlg.ShowDialog(owner);
                remember = dlg.RememberChoice;
                if (result == DialogResult.Cancel) return null;
                return result == DialogResult.Yes;
            }
        }

        private static void Show(IWin32Window owner, string message, string title,
                                 ThemedDialogKind kind)
        {
            using (ThemedDialog dlg = new ThemedDialog(title, message, kind))
                dlg.ShowDialog(owner);
        }
    }

    sealed class ThemedCheckBox : Control
    {
        private bool _checked;
        private bool _hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(200, 24);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle box = new Rectangle(1, (Height - 16) / 2, 16, 16);
            using (GraphicsPath path = Theme.RoundedRect(box, 4))
            {
                if (_checked)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.White, 1.8f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawLines(p, new PointF[]
                        {
                            new PointF(box.Left + 4f, box.Top + 8.5f),
                            new PointF(box.Left + 7f, box.Top + 11.5f),
                            new PointF(box.Left + 12f, box.Top + 5f)
                        });
                    }
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, path);
                    using (Pen p = new Pen(_hover ? Theme.Accent : Theme.Border))
                        g.DrawPath(p, path);
                }
            }

            TextRenderer.DrawText(g, Text, Font, new Rectangle(24, 0, Width - 24, Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
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
            base.OnClick(e);
            Focus();
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }

    // 托盘菜单「设置」打开的偏好窗口：选择关闭主窗口时的行为。
    sealed class SettingsForm : Form
    {
        private const int DialogWidth = 660;
        private const int DialogHeight = 420;
        private const int SidebarWidth = 176;
        private const int Pad = 28;
        private const int SectionHeight = 20;
        private const int RadioHeight = 40;
        private const int RadioGap = 2;
        private const int ButtonHeight = 34;
        private const int ButtonGap = 8;
        private const int ContentTop = 100;
        private const int NavTop = 64;
        private const int NavItemHeight = 36;
        private const int NavItemGap = 4;
        // 关于页各行统一行距，内容总高度（图标 + 各行 + 间距）用于垂直居中。
        private const int AboutLineGap = 14;
        private const int AboutContentHeight = 80 + 24 + 20 * 4 + AboutLineGap * 5;

        private static readonly string[] NavItems = { "通用", "关于" };

        private readonly Font _titleFont;
        private readonly Font _navFont;
        private readonly Font _sectionFont;
        private readonly Bitmap _logo;
        private readonly ThemedRadioButton _ask;
        private readonly ThemedRadioButton _minimize;
        private readonly ThemedRadioButton _exit;
        private readonly PillButton _cancel;
        private readonly PillButton _save;
        private readonly Dictionary<string, int> _aboutInkOffsets = new Dictionary<string, int>();
        private int _navIndex;

        public SettingsForm()
        {
            Text = "设置";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.CardBack;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _titleFont = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _navFont = new Font("Microsoft YaHei UI", 9.5F);
            _sectionFont = new Font("Microsoft YaHei UI", 9F);

            // 关于页的应用图标：从高清 logo 缩放到 80px。
            using (Bitmap large = AppIcon.CreateLogo())
            {
                _logo = new Bitmap(80, 80);
                using (Graphics g = Graphics.FromImage(_logo))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(large, new Rectangle(0, 0, 80, 80));
                }
            }

            ClientSize = new Size(DialogWidth, DialogHeight);

            // 与通用弹框一致：右上角关闭按钮，点击等同取消（不保存设置）。
            DialogCloseButton close = new DialogCloseButton();
            close.Location = new Point(DialogWidth - Pad - close.Width, 17);
            Controls.Add(close);

            int contentLeft = SidebarWidth + Pad;
            int contentWidth = DialogWidth - contentLeft - Pad;

            _ask = CreateRadio("每次询问", "关闭窗口时先询问，可选择最小化到托盘或退出",
                contentLeft, contentWidth, ContentTop);
            _minimize = CreateRadio("最小化到托盘", "关闭后隐藏窗口并驻留托盘，双击托盘图标恢复",
                contentLeft, contentWidth, ContentTop + RadioHeight + RadioGap);
            _exit = CreateRadio("直接退出程序", "关闭窗口即结束程序",
                contentLeft, contentWidth, ContentTop + (RadioHeight + RadioGap) * 2);

            switch (AppSettings.CloseBehavior)
            {
                case AppSettings.CloseAction.Minimize: _minimize.Checked = true; break;
                case AppSettings.CloseAction.Exit: _exit.Checked = true; break;
                default: _ask.Checked = true; break;
            }

            int buttonsTop = DialogHeight - Pad - ButtonHeight;

            _cancel = new PillButton();
            _cancel.Text = "取消";
            _cancel.BackColor = Theme.CardBack;
            _cancel.DialogResult = DialogResult.Cancel;
            _cancel.Size = new Size(80, ButtonHeight);
            _cancel.Location = new Point(DialogWidth - Pad - 96 - ButtonGap - 80, buttonsTop);
            Controls.Add(_cancel);

            _save = new PillButton();
            _save.Text = "保存";
            _save.Accent = true;
            _save.BackColor = Theme.CardBack;
            _save.DialogResult = DialogResult.OK;
            _save.Size = new Size(96, ButtonHeight);
            _save.Location = new Point(DialogWidth - Pad - 96, buttonsTop);
            _save.Click += delegate { AppSettings.CloseBehavior = SelectedAction(); };
            Controls.Add(_save);

            AcceptButton = _save;
            CancelButton = _cancel;

            ApplyPage();
        }

        private static Rectangle NavRect(int index)
        {
            return new Rectangle(12, NavTop + index * (NavItemHeight + NavItemGap),
                SidebarWidth - 24, NavItemHeight);
        }

        private static int NavIndexAt(Point p)
        {
            for (int i = 0; i < NavItems.Length; i++)
                if (NavRect(i).Contains(p)) return i;
            return -1;
        }

        // 通用页显示关闭选项与保存/取消；关于页仅作展示，不显示这些控件。
        private void ApplyPage()
        {
            bool general = _navIndex == 0;
            _ask.Visible = general;
            _minimize.Visible = general;
            _exit.Visible = general;
            _cancel.Visible = general;
            _save.Visible = general;
            Invalidate();
        }

        // 关于页文本以「可见墨迹」的水平中点对齐到同一条中轴线，而不是按字宽居中。
        // 全角标点（：。）行首 © 等带单侧留白，按字宽居中的话视觉上会左右错位。
        private void DrawAboutLine(Graphics g, string text, Font font, Color color,
            int left, int width, int top, int height)
        {
            int axis = left + width / 2;
            int offset = AboutInkOffset(text, font);
            Rectangle rect = new Rectangle(axis - offset - width / 2, top, width, height);
            TextRenderer.DrawText(g, text, font, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        // 用「与实际绘制完全相同的居中方式」把文字画到离屏位图上，扫描墨迹范围，
        // 得到墨迹中点相对矩形中点的偏移。结果按文字+字号缓存，只测量一次。
        private int AboutInkOffset(string text, Font font)
        {
            string key = text + "\u0001" + font.SizeInPoints.ToString("0.###") + "|" + (int)font.Style;
            int offset;
            if (_aboutInkOffsets.TryGetValue(key, out offset)) return offset;

            Size measured = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
            int w = Math.Max(measured.Width + 16, 32);
            int h = Math.Max(measured.Height + 8, 20);

            offset = 0;
            using (Bitmap buffer = new Bitmap(w, h))
            {
                // 离屏位图默认 96 DPI，需与屏幕一致，否则字形宽度与实绘不符。
                using (Graphics screen = Graphics.FromHwnd(Handle))
                    buffer.SetResolution(screen.DpiX, screen.DpiY);

                using (Graphics g = Graphics.FromImage(buffer))
                {
                    g.Clear(Color.Black);
                    TextRenderer.DrawText(g, text, font, new Rectangle(0, 0, w, h), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);

                    int minX = buffer.Width, maxX = -1;
                    for (int y = 0; y < buffer.Height; y++)
                    {
                        for (int x = 0; x < buffer.Width; x++)
                        {
                            if (buffer.GetPixel(x, y).R <= 40) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                        }
                    }
                    if (maxX >= minX)
                        offset = (minX + maxX) / 2 - w / 2;
                }
            }

            _aboutInkOffsets[key] = offset;
            return offset;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = NavIndexAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int index = NavIndexAt(e.Location);
            if (index >= 0 && index != _navIndex)
            {
                _navIndex = index;
                ApplyPage();
            }
            base.OnMouseClick(e);
        }

        private ThemedRadioButton CreateRadio(string text, string description,
                                              int left, int width, int top)
        {
            ThemedRadioButton radio = new ThemedRadioButton();
            radio.Text = text;
            radio.Description = description;
            radio.Size = new Size(width, RadioHeight);
            radio.Location = new Point(left, top);
            radio.CheckedChanged += OnRadioChecked;
            Controls.Add(radio);
            return radio;
        }

        private void OnRadioChecked(object sender, EventArgs e)
        {
            ThemedRadioButton source = (ThemedRadioButton)sender;
            if (!source.Checked) return;
            if (_ask != source) _ask.Checked = false;
            if (_minimize != source) _minimize.Checked = false;
            if (_exit != source) _exit.Checked = false;
        }

        private AppSettings.CloseAction SelectedAction()
        {
            if (_minimize.Checked) return AppSettings.CloseAction.Minimize;
            if (_exit.Checked) return AppSettings.CloseAction.Exit;
            return AppSettings.CloseAction.Ask;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyRegion();
        }

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int radius = (int)Math.Round(Theme.RadiusCard * (DeviceDpi / 96f));
            using (GraphicsPath path = Theme.RoundedRect(
                new RectangleF(0f, 0f, Width, Height), radius))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CardBack);

            // 左侧菜单栏
            using (SolidBrush b = new SolidBrush(Theme.FormBack))
                g.FillRectangle(b, 0, 0, SidebarWidth, Height);
            using (Pen p = new Pen(Theme.Border))
                g.DrawLine(p, SidebarWidth - 1, 0, SidebarWidth - 1, Height);

            float radius = Theme.RadiusCard * (DeviceDpi / 96f);
            RoundedPopupRenderer.DrawRing(g,
                new RectangleF(1f, 1f, Width - 2f, Height - 2f),
                new RectangleF(2f, 2f, Width - 4f, Height - 4f),
                radius);

            TextRenderer.DrawText(g, "设置", _titleFont,
                new Rectangle(20, 18, SidebarWidth - 40, 26), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            for (int i = 0; i < NavItems.Length; i++)
            {
                Rectangle nav = NavRect(i);
                if (i == _navIndex)
                {
                    using (GraphicsPath path = Theme.CircularRoundedRect(nav, Theme.RadiusControl))
                    using (SolidBrush b = new SolidBrush(Theme.AccentSoft))
                        g.FillPath(b, path);
                }
                TextRenderer.DrawText(g, NavItems[i], _navFont,
                    new Rectangle(nav.Left + 12, nav.Top, nav.Width - 24, nav.Height),
                    i == _navIndex ? Theme.Accent : Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            int contentLeft = SidebarWidth + Pad;
            int contentWidth = Width - contentLeft - Pad;
            if (_navIndex == 0)
            {
                TextRenderer.DrawText(g, "关闭主窗口时", _sectionFont,
                    new Rectangle(contentLeft, 70, contentWidth, SectionHeight), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                // 自上而下每一行间距一致（统一 AboutLineGap），整块垂直居中。
                int y = (Height - AboutContentHeight) / 2;
                int centerX = contentLeft + contentWidth / 2;

                g.DrawImage(_logo, new Rectangle(centerX - 40, y, 80, 80));
                y += 80 + AboutLineGap;
                DrawAboutLine(g, "Capy", _titleFont, Theme.Text,
                    contentLeft, contentWidth, y, 24);
                y += 24 + AboutLineGap;
                DrawAboutLine(g, BuildInfo.About, _navFont, Theme.Muted,
                    contentLeft, contentWidth, y, 20);
                y += 20 + AboutLineGap;
                DrawAboutLine(g, "简单，刚刚好。", _navFont, Theme.Muted,
                    contentLeft, contentWidth, y, 20);
                y += 20 + AboutLineGap;
                DrawAboutLine(g, "开发者：xianghui", _navFont, Theme.Muted,
                    contentLeft, contentWidth, y, 20);
                y += 20 + AboutLineGap;
                DrawAboutLine(g, "© 2026 xianghui 保留所有权利", _navFont, Theme.Muted,
                    contentLeft, contentWidth, y, 20);
            }

            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _navFont.Dispose();
                _sectionFont.Dispose();
                _logo.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    sealed class EmptyStateView : Control
    {
        private bool _hover;
        private float _hoverT;
        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Timer _anim;

        public EmptyStateView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            _titleFont = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _subFont = new Font("Microsoft YaHei UI", 9.5F);
            Cursor = Cursors.Hand;
            TabStop = true;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        public string Title { get; set; }
        public string Subtitle { get; set; }

        private void Animate()
        {
            if (!_anim.Enabled) _anim.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            float target = _hover ? 1f : 0f;
            _hoverT += (target - _hoverT) * 0.35f;
            if (Math.Abs(target - _hoverT) < 0.01f)
            {
                _hoverT = target;
                _anim.Stop();
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Animate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Animate();
            base.OnMouseLeave(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
                _titleFont.Dispose();
                _subFont.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            float dpi = DeviceDpi / 96f;
            float iconD = 44 * dpi;
            float gap = 14 * dpi;
            float titleH = 26 * dpi;
            float subH = 20 * dpi;
            bool hasSub = !string.IsNullOrEmpty(Subtitle);
            float blockH = iconD + gap + titleH + (hasSub ? subH : 0);
            float top = (Height - blockH) / 2f;
            float cx = Width / 2f;

            Color iconColor = Theme.Lerp(Color.FromArgb(150, 160, 175), Theme.Accent, _hoverT);
            Color iconBack = Theme.Lerp(Color.FromArgb(241, 245, 250), Theme.AccentSoft, _hoverT);
            RectangleF iconRect = new RectangleF(cx - iconD / 2f, top, iconD, iconD);
            using (SolidBrush b = new SolidBrush(iconBack))
                g.FillEllipse(b, iconRect);

            using (Pen pen = new Pen(iconColor, Math.Max(1.6f, 2f * dpi)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float pad = 12 * dpi;
                float topY = top + pad;
                float bottomY = top + iconD - pad;
                g.DrawLine(pen, cx, bottomY, cx, topY + 1f);
                g.DrawLine(pen, cx - 6f * dpi, topY + 7f * dpi, cx, topY);
                g.DrawLine(pen, cx + 6f * dpi, topY + 7f * dpi, cx, topY);
            }

            string title = string.IsNullOrEmpty(Title) ? "拖入文件" : Title;
            Rectangle titleRect = new Rectangle(0, (int)(top + iconD + gap), Width, (int)titleH);
            TextRenderer.DrawText(g, title, _titleFont, titleRect, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (hasSub)
            {
                Rectangle subRect = new Rectangle(0, (int)(top + iconD + gap + titleH), Width, (int)subH);
                TextRenderer.DrawText(g, Subtitle, _subFont, subRect, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    sealed class RoundedProgressBar : Control
    {
        private int _value;
        private int _maximum = 100;
        private float _display;
        private readonly Timer _anim;

        public RoundedProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FormBack;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(0, Math.Min(_maximum, value));
                if (v == _value) return;
                _value = v;
                if (_value == 0)
                {
                    _display = 0f;
                    _anim.Stop();
                    Invalidate();
                }
                else if (!_anim.Enabled)
                {
                    _anim.Start();
                }
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(1, value);
                if (_value > _maximum) _value = _maximum;
                if (_display > _maximum) _display = _maximum;
                Invalidate();
            }
        }

        public void Snap()
        {
            _display = _value;
            _anim.Stop();
            Invalidate();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            float diff = _value - _display;
            if (Math.Abs(diff) < 0.2f)
            {
                _display = _value;
                _anim.Stop();
            }
            else
            {
                _display += diff * 0.22f;
                if (Math.Abs(diff) < 0.6f) _display = _value;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int h = Math.Min(Height, Math.Max(4, Height));
            Rectangle track = new Rectangle(0, 0, Width, h);
            using (GraphicsPath p = Theme.CircularRoundedRect(track, h / 2))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 231, 239)))
                g.FillPath(b, p);

            if (_display <= 0.01f) return;

            float pct = _display / _maximum;
            int w = (int)Math.Round(Width * pct);
            if (w < h) w = h;
            if (w > Width) w = Width;

            Rectangle fill = new Rectangle(0, 0, w, h);
            using (GraphicsPath p = Theme.CircularRoundedRect(fill, h / 2))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillPath(b, p);
        }
    }

    sealed class CardPanel : Panel
    {
        private bool _highlight;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FormBack;
            Padding = new Padding(16);
        }

        public bool Highlight
        {
            get { return _highlight; }
            set
            {
                if (_highlight == value) return;
                _highlight = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.FormBack);

            Rectangle card = new Rectangle(1, 1,
                Math.Max(0, ClientSize.Width - 3), Math.Max(0, ClientSize.Height - 3));
            if (card.Width < 16 || card.Height < 16) return;

            using (GraphicsPath path = Theme.RoundedRect(card, Theme.RadiusCard))
            {
                using (SolidBrush b = new SolidBrush(Theme.CardBack)) g.FillPath(b, path);
                using (Pen p = new Pen(_highlight ? Theme.Accent : Theme.Border,
                                       _highlight ? 1.6f : 1f))
                    g.DrawPath(p, path);
            }
        }
    }

    sealed class TabStrip : Control
    {
        private string[] _items = new string[0];
        private readonly List<Rectangle> _rects = new List<Rectangle>();
        private readonly Dictionary<int, float> _hoverT = new Dictionary<int, float>();
        private readonly Timer _anim;
        private int _selected;
        private int _hover = -1;
        private float _selX;
        private float _selW;
        private bool _selInit;
        private bool _selAnimating;

        public TabStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FormBack;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            TabStop = false;
            Height = 52;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        public event EventHandler SelectedIndexChanged;

        public void SetItems(string[] items)
        {
            _items = items ?? new string[0];
            Invalidate();
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = Math.Max(0, Math.Min(_items.Length - 1, value));
                if (v == _selected) return;
                _selected = v;
                _selAnimating = _selInit && v >= 0 && v < _rects.Count;
                Animate();
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private void Animate()
        {
            if (!_anim.Enabled) _anim.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            bool busy = false;

            List<int> keys = new List<int>(_hoverT.Keys);
            foreach (int index in keys)
            {
                float target = index == _hover ? 1f : 0f;
                float t = _hoverT[index];
                t += (target - t) * 0.35f;
                if (Math.Abs(target - t) < 0.01f) t = target;
                else busy = true;

                if (t <= 0f) _hoverT.Remove(index);
                else _hoverT[index] = t;
            }

            if (_selAnimating && _selected >= 0 && _selected < _rects.Count)
            {
                Rectangle target = _rects[_selected];
                _selX += (target.Left - _selX) * 0.25f;
                _selW += (target.Width - _selW) * 0.25f;
                if (Math.Abs(target.Left - _selX) < 0.5f && Math.Abs(target.Width - _selW) < 0.5f)
                {
                    _selX = target.Left;
                    _selW = target.Width;
                    _selAnimating = false;
                }
                else
                {
                    busy = true;
                }
            }

            if (!busy) _anim.Stop();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hover = HitTest(e.Location);
            if (hover != _hover)
            {
                int old = _hover;
                _hover = hover;
                if (hover != -1 && !_hoverT.ContainsKey(hover)) _hoverT[hover] = 0f;
                if (old != -1 && !_hoverT.ContainsKey(old)) _hoverT[old] = 0f;
                Cursor = hover != -1 ? Cursors.Hand : Cursors.Default;
                Animate();
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1 && !_hoverT.ContainsKey(_hover)) _hoverT[_hover] = 0f;
            _hover = -1;
            Animate();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int hit = HitTest(e.Location);
            if (hit >= 0) SelectedIndex = hit;
            base.OnMouseDown(e);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _rects.Count; i++)
                if (_rects[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            _rects.Clear();
            int x = 16;
            int y = (Height - 32) / 2;

            for (int i = 0; i < _items.Length; i++)
            {
                Size size = TextRenderer.MeasureText(_items[i], Font);
                int w = size.Width + 32;
                _rects.Add(new Rectangle(x, y, w, 32));
                x += w + 4;
            }

            // While idle, keep the indicator glued to the selected item so
            // resizes and first paint do not animate from a stale position.
            if (!_selInit || !_selAnimating)
            {
                if (_selected >= 0 && _selected < _rects.Count)
                {
                    _selX = _rects[_selected].Left;
                    _selW = _rects[_selected].Width;
                    _selInit = true;
                }
            }

            for (int i = 0; i < _rects.Count; i++)
            {
                float t;
                if (!_hoverT.TryGetValue(i, out t) || t <= 0.001f) continue;
                using (GraphicsPath path = Theme.CircularRoundedRect(_rects[i], Theme.RadiusPill))
                using (SolidBrush b = new SolidBrush(Theme.Lerp(BackColor, Theme.HoverBack, t)))
                    g.FillPath(b, path);
            }

            if (_selInit && _selW > 0f)
            {
                RectangleF pill = new RectangleF(_selX, y, _selW, 32);
                using (GraphicsPath path = Theme.CircularRoundedRect(pill, Theme.RadiusPill))
                using (SolidBrush b = new SolidBrush(Theme.AccentSoft))
                    g.FillPath(b, path);
            }

            for (int i = 0; i < _rects.Count; i++)
            {
                float hoverT;
                _hoverT.TryGetValue(i, out hoverT);
                Color baseColor = Theme.Lerp(Theme.Muted, Theme.Text, hoverT);
                Color color = Theme.Lerp(baseColor, Theme.Accent, SelectionAmount(_rects[i]));
                TextRenderer.DrawText(g, _items[i], Font, _rects[i], color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private float SelectionAmount(Rectangle rect)
        {
            if (!_selInit || _selW <= 0f) return 0f;
            float left = Math.Max(rect.Left, _selX);
            float right = Math.Min(rect.Right, _selX + _selW);
            float overlap = right - left;
            if (overlap <= 0f) return 0f;
            return Math.Min(1f, overlap / rect.Width);
        }
    }

    // 自绘标题栏：应用图标、标题与最小化/最大化/关闭按钮。
    // 系统标题栏由 MainForm 的 WM_NCCALCSIZE 去掉，原生边框保留以支持缩放。
    sealed class TitleBar : Control
    {
        private const int MinKey = 0;
        private const int MaxKey = 1;
        private const int CloseKey = 2;
        private const int ButtonWidth = 46;
        private const int HoverWidth = 32;
        private const int HoverHeight = 28;
        private const int HoverRadius = 6;
        private const int IconSize = 20;
        private const int CaptionHeight = 40;
        private const int WmNclButtonDown = 0x00A1;
        private const int HtCaption = 2;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly Font _titleFont;
        private readonly Font _glyphFont;
        private readonly Bitmap _icon;
        private readonly Dictionary<int, float> _hoverT = new Dictionary<int, float>();
        private readonly Timer _anim;
        private int _hover = -1;
        private bool _maximized;
        private Rectangle _titleRect = Rectangle.Empty;
        private Rectangle _minRect = Rectangle.Empty;
        private Rectangle _maxRect = Rectangle.Empty;
        private Rectangle _closeRect = Rectangle.Empty;
        private readonly Dictionary<string, Rectangle> _glyphInk = new Dictionary<string, Rectangle>();

        public event EventHandler MinimizeRequested;
        public event EventHandler MaximizeRequested;
        public event EventHandler CloseRequested;

        public TitleBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardDoubleClick, true);
            BackColor = Theme.FormBack;
            Font = new Font("Microsoft YaHei UI", 9.5F);
            _titleFont = new Font("Microsoft YaHei UI", 9.5F);
            _glyphFont = new Font("Segoe MDL2 Assets", 9.5F);
            _icon = AppIcon.CreateSmall();
            TabStop = false;
            Height = CaptionHeight;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        public bool Maximized
        {
            get { return _maximized; }
            set
            {
                if (_maximized == value) return;
                _maximized = value;
                Invalidate();
            }
        }

        // 标题栏中的按钮区域必须保持可点击，其余区域用于拖动窗口。
        public bool IsInteractiveAt(Point p)
        {
            return _minRect.Contains(p) || _maxRect.Contains(p) || _closeRect.Contains(p);
        }

        private void UpdateRects()
        {
            int titleX = 12 + IconSize + 8;
            Size titleSize = TextRenderer.MeasureText(BuildInfo.Title, _titleFont, Size.Empty,
                TextFormatFlags.NoPadding);
            _titleRect = new Rectangle(titleX, 0, titleSize.Width, Height);
            _minRect = new Rectangle(Width - ButtonWidth * 3, 0, ButtonWidth, Height);
            _maxRect = new Rectangle(Width - ButtonWidth * 2, 0, ButtonWidth, Height);
            _closeRect = new Rectangle(Width - ButtonWidth, 0, ButtonWidth, Height);
        }

        private int HitTest(Point p)
        {
            if (_minRect.Contains(p)) return MinKey;
            if (_maxRect.Contains(p)) return MaxKey;
            if (_closeRect.Contains(p)) return CloseKey;
            return -1;
        }

        private void Animate()
        {
            if (!_anim.Enabled) _anim.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            bool busy = false;
            List<int> keys = new List<int>(_hoverT.Keys);
            foreach (int key in keys)
            {
                float target = key == _hover ? 1f : 0f;
                float t = _hoverT[key];
                t += (target - t) * 0.35f;
                if (Math.Abs(target - t) < 0.01f) t = target;
                else busy = true;

                if (t <= 0f) _hoverT.Remove(key);
                else _hoverT[key] = t;
            }

            if (!busy) _anim.Stop();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRects();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hover = HitTest(e.Location);
            if (hover != _hover)
            {
                int old = _hover;
                _hover = hover;
                if (hover != -1 && !_hoverT.ContainsKey(hover)) _hoverT[hover] = 0f;
                if (old != -1 && !_hoverT.ContainsKey(old)) _hoverT[old] = 0f;
                Cursor = hover != -1 ? Cursors.Hand : Cursors.Default;
                Animate();
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1 && !_hoverT.ContainsKey(_hover)) _hoverT[_hover] = 0f;
            _hover = -1;
            Cursor = Cursors.Default;
            Animate();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || IsInteractiveAt(e.Location)) return;
            if (e.Clicks == 2)
            {
                if (MaximizeRequested != null) MaximizeRequested(this, EventArgs.Empty);
                return;
            }
            // 空白区域作为窗口标题拖动。
            Form form = FindForm();
            if (form == null) return;
            ReleaseCapture();
            SendMessage(form.Handle, WmNclButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                switch (HitTest(e.Location))
                {
                    case MinKey:
                        if (MinimizeRequested != null) MinimizeRequested(this, EventArgs.Empty);
                        break;
                    case MaxKey:
                        if (MaximizeRequested != null) MaximizeRequested(this, EventArgs.Empty);
                        break;
                    case CloseKey:
                        if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
                        break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UpdateRects();
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            DrawWindowButton(g, _minRect, MinKey, "\uE921", false);
            DrawWindowButton(g, _maxRect, MaxKey, _maximized ? "\uE923" : "\uE922", false);
            DrawWindowButton(g, _closeRect, CloseKey, "\uE8BB", true);

            Rectangle iconRect = new Rectangle(12, (Height - IconSize) / 2, IconSize, IconSize);
            g.DrawImage(_icon, iconRect);

            TextRenderer.DrawText(g, BuildInfo.Title, _titleFont, _titleRect, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void DrawWindowButton(Graphics g, Rectangle rect, int key, string glyph, bool close)
        {
            float t;
            _hoverT.TryGetValue(key, out t);
            if (t > 0.001f)
            {
                // 悬停底色只覆盖按钮中心的小圆角块，而不是整个单元格，
                // 避免按钮区域贴满标题栏高度显得笨重。
                Rectangle pill = new Rectangle(
                    rect.X + (rect.Width - HoverWidth) / 2,
                    rect.Y + (rect.Height - HoverHeight) / 2,
                    HoverWidth, HoverHeight);
                // GDI 文本按行高垂直居中，字形墨迹并不落在单元格正中，
                // 悬停底色改为对齐字形墨迹的实际中心，图标本身位置保持不变。
                Rectangle ink = GlyphInk(glyph, rect.Size);
                if (ink.Width > 0)
                    pill = new Rectangle(
                        rect.X + ink.Left + ink.Width / 2 - HoverWidth / 2,
                        rect.Y + ink.Top + ink.Height / 2 - HoverHeight / 2,
                        HoverWidth, HoverHeight);
                Color hoverColor = close ? Theme.Danger : Color.FromArgb(228, 232, 239);
                using (GraphicsPath path = Theme.CircularRoundedRect(pill, HoverRadius))
                using (SolidBrush b = new SolidBrush(Theme.Lerp(BackColor, hoverColor, t)))
                    g.FillPath(b, path);
            }

            Color color = close ? Theme.Lerp(Theme.Text, Color.White, t) : Theme.Text;
            DrawGlyph(g, glyph, rect, color);
        }

        // 把图标画到同尺寸离屏位图上，扫描出墨迹像素范围（相对单元格左上角）。
        // 结果用于让悬停底色对齐图标的视觉中心，按字形缓存。
        private Rectangle GlyphInk(string glyph, Size cellSize)
        {
            Rectangle ink;
            if (_glyphInk.TryGetValue(glyph, out ink)) return ink;

            int left = cellSize.Width, top = cellSize.Height, right = -1, bottom = -1;
            using (Bitmap buffer = new Bitmap(cellSize.Width, cellSize.Height))
            {
                // 离屏位图默认 96 DPI，与屏幕 DPI 不一致时字形尺寸会错位。
                using (Graphics screen = Graphics.FromHwnd(Handle))
                    buffer.SetResolution(screen.DpiX, screen.DpiY);

                using (Graphics g = Graphics.FromImage(buffer))
                {
                    g.Clear(Color.Black);
                    TextRenderer.DrawText(g, glyph, _glyphFont,
                        new Rectangle(Point.Empty, cellSize), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);

                    for (int y = 0; y < cellSize.Height; y++)
                    {
                        for (int x = 0; x < cellSize.Width; x++)
                        {
                            Color c = buffer.GetPixel(x, y);
                            if (c.R < 32 && c.G < 32 && c.B < 32) continue;
                            if (x < left) left = x;
                            if (x > right) right = x;
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                        }
                    }
                }
            }

            ink = right < left
                ? Rectangle.Empty
                : new Rectangle(left, top, right - left + 1, bottom - top + 1);
            _glyphInk[glyph] = ink;
            return ink;
        }

        private void DrawGlyph(Graphics g, string glyph, Rectangle rect, Color color)
        {
            // Segoe MDL2 Assets 走 GDI（TextRenderer）比 GDI+ DrawString 更清晰。
            TextRenderer.DrawText(g, glyph, _glyphFont, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
                _titleFont.Dispose();
                _glyphFont.Dispose();
                _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // 左下角设置按钮：齿轮图标，悬停时显示圆角底色。
    sealed class GearButton : Control
    {
        private readonly Font _glyphFont;
        private bool _hover;

        public GearButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FormBack;
            _glyphFont = new Font("Segoe MDL2 Assets", 11F);
            Cursor = Cursors.Hand;
            TabStop = false;
            Size = new Size(30, 30);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover)
            {
                using (GraphicsPath path = Theme.CircularRoundedRect(rect, Theme.RadiusControl))
                using (SolidBrush b = new SolidBrush(Theme.HoverBack))
                    g.FillPath(b, path);
            }

            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(_hover ? Theme.Text : Theme.Muted))
                    g.DrawString("\uE713", _glyphFont, b, ClientRectangle, format);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _glyphFont.Dispose();
            base.Dispose(disposing);
        }
    }

    // 主题化下拉框：自绘外观，弹出层使用无边框列表。
    sealed class ThemedComboBox : Control
    {
        private readonly List<string> _items = new List<string>();
        private readonly Font _popupFont;
        private int _selectedIndex = -1;
        private int _hoverItem = -1;
        private bool _hover;
        private float _hoverT;
        private readonly Timer _anim;
        private ToolStripDropDown _dropDown;
        private ListBox _list;
        private ToolStripControlHost _host;
        private RoundedPopup _popup;

        public ThemedComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            _popupFont = new Font("Microsoft YaHei UI", 9F);
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(132, 32);

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;
        }

        private void Animate()
        {
            if (!_anim.Enabled) _anim.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            float target = _hover ? 1f : 0f;
            _hoverT += (target - _hoverT) * 0.35f;
            if (Math.Abs(target - _hoverT) < 0.01f)
            {
                _hoverT = target;
                _anim.Stop();
            }
            Invalidate();
        }

        public event EventHandler SelectedIndexChanged;

        public List<string> Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int v = _items.Count == 0 ? -1 : Math.Max(0, Math.Min(_items.Count - 1, value));
                if (v == _selectedIndex) return;
                _selectedIndex = v;
                if (_list != null) _list.SelectedIndex = v;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private string SelectedText
        {
            get
            {
                return _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : "";
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Animate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Animate();
            base.OnMouseLeave(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (_dropDown != null && _dropDown.Visible) _dropDown.Close();
            else ShowDropDown();
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_items.Count > 0)
                SelectedIndex = _selectedIndex + (e.Delta > 0 ? -1 : 1);
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.Down)
            {
                ShowDropDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && _dropDown != null && _dropDown.Visible)
            {
                _dropDown.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void ShowDropDown()
        {
            if (_items.Count == 0) return;

            if (_dropDown == null) CreateDropDown();

            int rowH = 30;
            int rows = Math.Min(_items.Count, 8);
            int innerWidth = Math.Max(1, Width - Theme.PopupPadding * 2);
            _list.ItemHeight = rowH;
            _list.Items.Clear();
            foreach (string item in _items) _list.Items.Add(item);
            _list.SelectedIndex = _selectedIndex;
            _list.Size = new Size(innerWidth, rows * rowH);
            ApplyListRegion();
            _host.Size = _list.Size;
            _dropDown.Size = new Size(
                Width,
                _list.Height + Theme.PopupPadding * 2);
            // Place the popup immediately after the control.  The previous +1
            // offset exposed the white card background as a hairline seam.
            _dropDown.Show(this, new Point(0, Height));
            _list.Focus();
        }

        private void CreateDropDown()
        {
            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 30;
            _list.Font = _popupFont;
            _list.BackColor = Theme.CardBack;
            _list.ForeColor = Theme.Text;
            _list.IntegralHeight = false;
            _list.DrawItem += OnListDrawItem;
            _list.MouseMove += OnListMouseMove;
            _list.MouseLeave += OnListMouseLeave;
            _list.Click += OnListClick;
            _list.KeyDown += OnListKeyDown;

            _host = new ToolStripControlHost(_list);
            _host.Margin = Padding.Empty;
            _host.Padding = Padding.Empty;
            _host.AutoSize = false;

            _dropDown = new ToolStripDropDown();
            _dropDown.Padding = new Padding(Theme.PopupPadding);
            _dropDown.Margin = Padding.Empty;
            _dropDown.AutoSize = false;
            _dropDown.BackColor = Theme.CardBack;
            _dropDown.DropShadowEnabled = false;
            _dropDown.Renderer = new RoundedPopupRenderer(Theme.RadiusControl);
            _dropDown.Items.Add(_host);
            _popup = new RoundedPopup(_dropDown, Theme.RadiusControl);
            _dropDown.Closed += delegate
            {
                _hoverItem = -1;
                Invalidate();
                Focus();
            };
        }

        // 列表是方角控件，放在圆角弹层里会盖住四角的边框弧段。
        // 给列表本身套一个内圆角 Region，把角让给父级绘制边框。
        private void ApplyListRegion()
        {
            if (_list == null || _list.Width <= 0 || _list.Height <= 0) return;

            float dpi = _list.DeviceDpi / 96f;
            float radius = Math.Max(0f,
                (Theme.RadiusControl - Theme.PopupPadding) * dpi);
            using (GraphicsPath path = Theme.RoundedRect(
                new RectangleF(0f, 0f, _list.Width, _list.Height), radius))
            {
                Region previous = _list.Region;
                _list.Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        private void OnListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = e.Index == _selectedIndex;
            bool hovered = e.Index == _hoverItem;
            Color back = selected ? Theme.SelectedBack : hovered ? Theme.HoverBack : Theme.CardBack;
            using (SolidBrush b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            Rectangle textRect = Rectangle.Inflate(e.Bounds, -12, 0);
            TextRenderer.DrawText(e.Graphics, _list.Items[e.Index].ToString(), _list.Font,
                textRect, selected ? Theme.Accent : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void OnListMouseMove(object sender, MouseEventArgs e)
        {
            int index = e.Y / Math.Max(1, _list.ItemHeight);
            if (index < 0 || index >= _list.Items.Count) index = -1;
            if (index != _hoverItem)
            {
                _hoverItem = index;
                _list.Invalidate();
            }
        }

        private void OnListMouseLeave(object sender, EventArgs e)
        {
            if (_hoverItem != -1)
            {
                _hoverItem = -1;
                _list.Invalidate();
            }
        }

        private void OnListClick(object sender, EventArgs e)
        {
            if (_list.SelectedIndex >= 0) SelectedIndex = _list.SelectedIndex;
            _dropDown.Close();
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (_list.SelectedIndex >= 0) SelectedIndex = _list.SelectedIndex;
                _dropDown.Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _dropDown.Close();
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
                if (_popup != null)
                {
                    _popup.Dispose();
                    _popup = null;
                }
                if (_dropDown != null) _dropDown.Dispose();
                _popupFont.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            float dpi = DeviceDpi / 96f;
            // 整数坐标：GDI+ 默认像素偏移下坐标落在像素中心，1px 描边才是纯色实线；
            // 加 0.5 会让描边跨两个像素各覆盖一半，颜色被冲淡成两条糊边。
            RectangleF r = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
            Color border = !Enabled
                ? Theme.Border
                : (Focused || (_dropDown != null && _dropDown.Visible))
                    ? Theme.Accent
                    : Theme.Lerp(Theme.Border, Theme.BorderHover, _hoverT);
            Color fill = Enabled ? Theme.CardBack : Theme.DisabledBack;
            Color textColor = Enabled ? Theme.Text : Theme.DisabledText;
            Color arrowColor = Enabled ? Theme.Muted : Theme.DisabledText;

            using (GraphicsPath path = Theme.RoundedRect(r, Theme.RadiusControl * dpi))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }

            Rectangle textRect = new Rectangle(10, 0, Width - 36, Height);
            TextRenderer.DrawText(g, SelectedText, Font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            float cx = Width - 17;
            float cy = Height / 2f - 1;
            using (Pen p = new Pen(arrowColor, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(p, cx, cy + 2, cx + 4, cy - 2);
            }
        }
    }

    // 主题化数字输入：0 表示不限制，Suffix 显示在输入框右侧。
    sealed class ThemedNumberBox : Control
    {
        private readonly TextBox _edit;
        private bool _hover;

        public ThemedNumberBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(132, 32);
            Maximum = 20000;

            _edit = new TextBox();
            _edit.BorderStyle = BorderStyle.None;
            _edit.Font = Font;
            _edit.BackColor = Theme.CardBack;
            _edit.ForeColor = Theme.Text;
            _edit.MaxLength = 5;
            _edit.Text = "0";
            _edit.TabStop = true;
            _edit.KeyPress += OnEditKeyPress;
            _edit.TextChanged += OnEditTextChanged;
            _edit.GotFocus += delegate { Invalidate(); };
            _edit.LostFocus += OnEditLostFocus;
            Controls.Add(_edit);
        }

        public int Maximum { get; set; }

        private string _suffix;

        public string Suffix
        {
            get { return _suffix; }
            set
            {
                _suffix = value;
                Invalidate();
                LayoutEdit();
            }
        }

        private int _decimals;

        // 小数位数：0 为整数输入（默认）；目标体积等场景可设为 1-2 位。
        public int Decimals
        {
            get { return _decimals; }
            set
            {
                _decimals = Math.Max(0, Math.Min(4, value));
                _edit.MaxLength = Math.Max(1, Maximum.ToString(CultureInfo.InvariantCulture).Length +
                                              (_decimals > 0 ? _decimals + 1 : 0) + 2);
                DecimalValue = DecimalValue;
            }
        }

        public decimal DecimalValue
        {
            get
            {
                decimal v;
                if (!decimal.TryParse(_edit.Text, NumberStyles.AllowDecimalPoint,
                                      CultureInfo.InvariantCulture, out v)) v = 0m;
                if (v < 0m) v = 0m;
                if (v > Maximum) v = Maximum;
                return v;
            }
            set
            {
                decimal v = value;
                if (v < 0m) v = 0m;
                if (v > Maximum) v = Maximum;
                string text = FormatValue(v);
                if (_edit.Text != text) _edit.Text = text;
            }
        }

        public int Value
        {
            get { return (int)DecimalValue; }
            set { DecimalValue = value; }
        }

        private string FormatValue(decimal v)
        {
            if (_decimals <= 0) return ((int)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0." + new string('#', _decimals), CultureInfo.InvariantCulture);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutEdit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutEdit();
        }

        private int SuffixWidth
        {
            get
            {
                if (string.IsNullOrEmpty(Suffix)) return 0;
                return TextRenderer.MeasureText(Suffix, Font).Width + 6;
            }
        }

        private void LayoutEdit()
        {
            if (_edit == null) return;
            int pad = 10;
            int editW = Math.Max(20, Width - pad * 2 - SuffixWidth);
            int editH = _edit.Height;
            _edit.SetBounds(pad, (Height - editH) / 2, editW, editH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _edit.Focus();
            base.OnMouseDown(e);
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

        private void OnEditKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (char.IsDigit(e.KeyChar)) return;
            if (e.KeyChar == '.' && _decimals > 0 && _edit.Text.IndexOf('.') < 0) return;
            e.Handled = true;
        }

        private void OnEditTextChanged(object sender, EventArgs e)
        {
            string text = _edit.Text;
            bool clean = true;
            int dots = 0;
            foreach (char c in text)
            {
                if (char.IsDigit(c)) continue;
                if (c == '.' && _decimals > 0 && dots == 0) { dots++; continue; }
                clean = false;
                break;
            }
            if (!clean)
            {
                int pos = _edit.SelectionStart;
                string digits = "";
                bool dot = false;
                foreach (char c in text)
                {
                    if (char.IsDigit(c)) digits += c;
                    else if (c == '.' && _decimals > 0 && !dot) { digits += '.'; dot = true; }
                }
                _edit.Text = digits;
                _edit.SelectionStart = Math.Min(pos, _edit.Text.Length);
            }
        }

        private void OnEditLostFocus(object sender, EventArgs e)
        {
            DecimalValue = DecimalValue;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border = !Enabled
                ? Theme.Border
                : _edit.Focused ? Theme.Accent : _hover ? Theme.BorderHover : Theme.Border;
            Color fill = Enabled ? Theme.CardBack : Theme.DisabledBack;
            Color suffixColor = Enabled ? Theme.Muted : Theme.DisabledText;

            using (GraphicsPath path = Theme.RoundedRect(r, Theme.RadiusControl * (DeviceDpi / 96f)))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }

            if (!string.IsNullOrEmpty(Suffix))
            {
                int w = TextRenderer.MeasureText(Suffix, Font).Width;
                Rectangle suffixRect = new Rectangle(Width - 10 - w, 0, w, Height);
                TextRenderer.DrawText(g, Suffix, Font, suffixRect, suffixColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    // 主题化单选：左侧圆点 + 加粗标题 + 可选说明行。
    sealed class ThemedRadioButton : Control
    {
        private bool _checked;
        private string _description = "";
        private readonly Font _titleFont;

        public ThemedRadioButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            _titleFont = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            Size = new Size(560, 38);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (_checked && CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public string Description
        {
            get { return _description; }
            set
            {
                _description = value != null ? value : "";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int cy = 9;
            Rectangle outer = new Rectangle(2, cy - 7, 14, 14);
            if (_checked)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, outer);
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.FillEllipse(b, new Rectangle(6, cy - 3, 6, 6));
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, outer);
                using (Pen p = new Pen(Theme.Border)) g.DrawEllipse(p, outer);
            }

            TextRenderer.DrawText(g, Text, _titleFont, new Rectangle(24, 0, Width - 24, 18), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrEmpty(_description))
            {
                TextRenderer.DrawText(g, _description, Font, new Rectangle(24, 17, Width - 24, 18), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            Checked = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = true;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _titleFont.Dispose();
            base.Dispose(disposing);
        }
    }

    // 输出目录选择器：默认为源文件所在目录，可切换到其他文件夹。
    sealed class OutputPathPicker : Control
    {
        private readonly Timer _anim;
        private readonly ToolTip _tip;
        private readonly ContextMenuStrip _menu;
        private readonly RoundedPopup _popup;
        private string _dir = "";
        private bool _hover;
        private float _hoverT;

        public OutputPathPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(320, 30);

            _tip = new ToolTip();
            _tip.InitialDelay = 700;
            _tip.SetToolTip(this, "输出到源文件所在目录");

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += OnAnimTick;

            _menu = new ContextMenuStrip();
            _menu.Renderer = new ThemedMenuRenderer(true);
            _menu.ShowImageMargin = false;
            _menu.ShowCheckMargin = false;
            _menu.BackColor = Theme.CardBack;
            _menu.ForeColor = Theme.Text;
            _menu.Font = Font;
            _menu.Padding = new Padding(
                Theme.PopupBorderInset, Theme.MenuPaddingY,
                Theme.PopupBorderInset, Theme.MenuPaddingY);
            _menu.DropShadowEnabled = false;

            ToolStripMenuItem defaultDir = new ToolStripMenuItem("源文件所在目录（默认）");
            defaultDir.Click += delegate { OutputDir = ""; };
            ToolStripMenuItem browse = new ToolStripMenuItem("选择其他目录…");
            browse.Click += delegate { BrowseFolder(); };

            _menu.Items.Add(defaultDir);
            _menu.Items.Add(browse);
            foreach (ToolStripItem item in _menu.Items)
                item.Padding = new Padding(
                    Theme.MenuItemPaddingX, Theme.MenuItemPaddingY,
                    Theme.MenuItemPaddingX, Theme.MenuItemPaddingY);

            _popup = new RoundedPopup(_menu, Theme.RadiusControl);
        }

        public event EventHandler OutputDirChanged;

        public string OutputDir
        {
            get { return _dir; }
            set
            {
                string v = value != null ? value : "";
                if (v == _dir) return;
                _dir = v;
                _tip.SetToolTip(this, _dir.Length > 0 ? _dir : "输出到源文件所在目录");
                Invalidate();
                if (OutputDirChanged != null) OutputDirChanged(this, EventArgs.Empty);
            }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            float target = _hover ? 1f : 0f;
            _hoverT += (target - _hoverT) * 0.35f;
            if (Math.Abs(target - _hoverT) < 0.01f)
            {
                _hoverT = target;
                _anim.Stop();
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            if (!_anim.Enabled) _anim.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            if (!_anim.Enabled) _anim.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left && Enabled)
                _menu.Show(this, new Point(0, Height));
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down))
            {
                _menu.Show(this, new Point(0, Height));
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择输出目录";
                dlg.ShowNewFolderButton = true;
                if (_dir.Length > 0 && Directory.Exists(_dir)) dlg.SelectedPath = _dir;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    OutputDir = dlg.SelectedPath;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            float dpi = DeviceDpi / 96f;
            RectangleF r = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
            Color border = !Enabled
                ? Theme.Border
                : (Focused || _menu.Visible)
                    ? Theme.Accent
                    : Theme.Lerp(Theme.Border, Theme.BorderHover, _hoverT);
            Color fill = Enabled ? Theme.CardBack : Theme.DisabledBack;
            Color textColor = Enabled ? Theme.Text : Theme.DisabledText;
            Color iconColor = Enabled ? Theme.Muted : Theme.DisabledText;

            using (GraphicsPath path = Theme.RoundedRect(r, Theme.RadiusControl * dpi))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }

            int cy = Height / 2;
            int fx = 12;
            int fy = cy - 5;
            using (GraphicsPath folder = new GraphicsPath())
            {
                folder.AddLines(new PointF[]
                {
                    new PointF(fx, fy + 10), new PointF(fx, fy),
                    new PointF(fx + 5, fy), new PointF(fx + 6.5f, fy + 2.5f),
                    new PointF(fx + 13, fy + 2.5f), new PointF(fx + 13, fy + 10)
                });
                folder.CloseFigure();
                using (SolidBrush b = new SolidBrush(Enabled ? Theme.HoverBack : Theme.DisabledBack))
                    g.FillPath(b, folder);
                using (Pen p = new Pen(iconColor, 1.2f)) g.DrawPath(p, folder);
            }

            string text = _dir.Length > 0 ? _dir : "源文件所在目录";
            TextRenderer.DrawText(g, text, Font,
                new Rectangle(34, 0, Math.Max(0, Width - 64), Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.PathEllipsis | TextFormatFlags.NoPadding);

            float cx = Width - 17;
            float ay = cy - 1;
            using (Pen p = new Pen(iconColor, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4, ay - 2, cx, ay + 2);
                g.DrawLine(p, cx, ay + 2, cx + 4, ay - 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _anim.Stop();
                _anim.Dispose();
                _tip.Dispose();
                if (_popup != null) _popup.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // 宽高比锁定开关：45° 斜向双链环（Feather link 造型），锁定时两半互扣，解锁时拉开。
    sealed class ChainToggle : Control
    {
        private bool _locked = true;
        private bool _hover;

        public ChainToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Size = new Size(28, 32);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public event EventHandler CheckedChanged;

        public bool Locked
        {
            get { return _locked; }
            set
            {
                if (_locked == value) return;
                _locked = value;
                Invalidate();
            }
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
            base.OnClick(e);
            Focus();
            Toggle();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Toggle();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void Toggle()
        {
            _locked = !_locked;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Color color = !Enabled ? Theme.Border : (_locked ? Theme.Accent : (_hover ? Theme.BorderHover : Theme.Muted));

            // 两段 C 形半环互为 180° 旋转；锁定时在中心互扣，解锁时沿对角线拉开。
            float scale = 1.1f;
            float sep = _locked ? 0f : 1.5f;
            float cx = Width / 2f;
            float cy = Height / 2f;

            using (GraphicsPath half = LinkHalf())
            using (Pen p = new Pen(color, 1.4f / scale))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                DrawLinkHalf(g, p, half, cx + sep, cy - sep, scale, false);
                DrawLinkHalf(g, p, half, cx - sep, cy + sep, scale, true);
            }
        }

        private static RectangleF ArcRect(float cx, float cy, float r)
        {
            return new RectangleF(cx - r, cy - r, r * 2, r * 2);
        }

        // Feather "link" 图标的单侧半环，坐标基于 24×24 画布。
        private static GraphicsPath LinkHalf()
        {
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddArc(ArcRect(14.0038f, 10.0051f, 5f), 143.203f, -98.214f);
            p.AddLine(17.54f, 13.54f, 20.54f, 10.54f);
            p.AddArc(ArcRect(16.9436f, 7.0664f, 5f), 44.004f, -178.009f);
            p.AddLine(13.47f, 3.47f, 11.75f, 5.18f);
            p.CloseFigure();
            return p;
        }

        private static void DrawLinkHalf(Graphics g, Pen pen, GraphicsPath path,
                                         float cx, float cy, float scale, bool flip)
        {
            g.ResetTransform();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);
            if (flip) g.RotateTransform(180f);
            g.TranslateTransform(-12f, -12f);
            g.DrawPath(pen, path);
            g.ResetTransform();
        }
    }

    sealed class FileListHeader : Control
    {
        private readonly Font _headerFont;

        public FileListHeader()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            _headerFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            Height = 36;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _headerFont.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.CardBack);

            int scrollWidth = 12;
            int gridRight = Math.Max(0, Width - scrollWidth);
            int stateLeft = Math.Max(0, gridRight - Theme.StateColumnWidth);

            Rectangle fileRect = new Rectangle(10, 0,
                Math.Max(0, stateLeft - 22), Height);
            Rectangle stateRect = new Rectangle(stateLeft, 0,
                Math.Max(0, gridRight - stateLeft), Height);

            TextRenderer.DrawText(g, "文件", _headerFont, fileRect, Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "状态", _headerFont, stateRect, Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int lineLeft = Theme.RowHighlightInsetX;
            int lineRight = Math.Max(lineLeft, gridRight - Theme.RowHighlightInsetX);
            using (Pen p = new Pen(Theme.Border))
                g.DrawLine(p, lineLeft, Height - 1, lineRight, Height - 1);
        }
    }

    sealed class ThemedGrid : DataGridView
    {
        public ThemedGrid()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        public VScrollBarEx ScrollBar { get; set; }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ScrollBar != null && ScrollBar.Visible)
            {
                ScrollBar.ScrollRows(e.Delta > 0 ? -3 : 3);
                return;
            }
            base.OnMouseWheel(e);
        }
    }

    sealed class VScrollBarEx : Control
    {
        private int _total;
        private int _visible = 1;
        private int _value;
        private bool _hover;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartValue;

        public VScrollBarEx()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Width = 12;
            Visible = false;
        }

        public event EventHandler ValueChanged;

        public int Value
        {
            get { return _value; }
            set
            {
                int max = MaxValue;
                int v = Math.Max(0, Math.Min(max, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        private int MaxValue
        {
            get { return Math.Max(0, _total - _visible); }
        }

        public void UpdateState(int total, int visible, int value)
        {
            _total = Math.Max(0, total);
            _visible = Math.Max(1, visible);
            int v = Math.Max(0, Math.Min(MaxValue, value));
            if (v != _value)
            {
                _value = v;
                Invalidate();
            }
            else
            {
                Invalidate();
            }
        }

        public void ScrollRows(int delta)
        {
            Value = _value + delta;
        }

        private Rectangle TrackRect
        {
            get { return new Rectangle(Width - 8, 2, 4, Math.Max(1, Height - 4)); }
        }

        private Rectangle ThumbRect()
        {
            Rectangle track = TrackRect;
            if (_total <= _visible)
                return Rectangle.Empty;
            float ratio = (float)_visible / _total;
            int h = Math.Max(24, (int)(track.Height * ratio));
            if (h > track.Height) h = track.Height;
            int travel = track.Height - h;
            int top = MaxValue == 0 ? 0 : (int)Math.Round((float)travel * _value / MaxValue);
            return new Rectangle(track.Left - 1, track.Top + top, track.Width + 2, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            Rectangle track = TrackRect;
            using (GraphicsPath p = Theme.CircularRoundedRect(track, track.Width / 2))
            using (SolidBrush b = new SolidBrush(Theme.ScrollTrack))
                g.FillPath(b, p);

            Color color = (_hover || _dragging) ? Theme.ScrollThumbHover : Theme.ScrollThumb;
            using (GraphicsPath p = Theme.CircularRoundedRect(thumb, thumb.Width / 2))
            using (SolidBrush b = new SolidBrush(color))
                g.FillPath(b, p);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Rectangle thumb = ThumbRect();
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartValue = _value;
                Capture = true;
            }
            else
            {
                Value = e.Y < thumb.Top ? _value - _visible : _value + _visible;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Rectangle track = TrackRect;
                Rectangle thumb = ThumbRect();
                int travel = track.Height - thumb.Height;
                if (travel > 0)
                {
                    int dy = e.Y - _dragStartY;
                    Value = _dragStartValue + (int)Math.Round((float)dy * MaxValue / travel);
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    sealed class ThemedMenuRenderer : RoundedPopupRenderer
    {
        private readonly bool _centerVertically;

        public ThemedMenuRenderer() : this(false) { }

        public ThemedMenuRenderer(bool centerVertically) : base(Theme.RadiusControl)
        {
            _centerVertically = centerVertically;
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.CardBack))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle r = new Rectangle(
                Theme.MenuHoverInsetX,
                Theme.MenuHoverInsetY,
                e.Item.Width - Theme.MenuHoverInsetX * 2,
                e.Item.Height - Theme.MenuHoverInsetY * 2);
            using (GraphicsPath path = Theme.RoundedRect(r, Theme.RadiusControl))
            using (SolidBrush b = new SolidBrush(Theme.AccentSoft))
                e.Graphics.FillPath(b, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.DisabledText;
            if (_centerVertically)
            {
                // 左对齐、垂直居中：左内边距对齐原样式，竖直方向按整行高度居中。
                int padL = e.Item.Padding.Left;
                int padR = e.Item.Padding.Right;
                Rectangle r = new Rectangle(padL, 0,
                    Math.Max(0, e.Item.Width - padL - padR), e.Item.Height);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, r, e.TextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                return;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            if (r.Width <= 0 || r.Height <= 0)
            {
                Rectangle bounds = e.Item.Bounds;
                r = new Rectangle(8, bounds.Top, 16, bounds.Height);
            }

            float x = r.Left + (r.Width - 12f) / 2f;
            float y = r.Top + (r.Height - 12f) / 2f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Accent, 2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                e.Graphics.DrawLines(p, new PointF[]
                {
                    new PointF(x + 1f, y + 6f),
                    new PointF(x + 4.5f, y + 9.5f),
                    new PointF(x + 10.5f, y + 2.5f)
                });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            Rectangle r = e.ArrowRectangle;
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(e.Item.Selected ? Theme.Accent : Theme.Muted, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                e.Graphics.DrawLines(p, new PointF[]
                {
                    new PointF(cx - 2f, cy - 4f),
                    new PointF(cx + 2f, cy),
                    new PointF(cx - 2f, cy + 4f)
                });
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            // 分隔线横贯整个菜单，只避开 1px 描边；短菜单下两端不再出现明显缺口。
            int y = e.Item.Height / 2;
            int originX = e.Item.Bounds.Left;
            using (Pen p = new Pen(Theme.Border))
                e.Graphics.DrawLine(p,
                    1 - originX, y,
                    e.ToolStrip.Width - 1 - originX, y);
        }
    }

    // 任务栏进度：ITaskbarList3（Win7+），调用失败时静默降级。
    sealed class TaskbarProgress : IDisposable
    {
        [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, int state);
        }

        private readonly IntPtr _hwnd;
        private ITaskbarList3 _list;
        private bool _available;

        public TaskbarProgress(IntPtr hwnd)
        {
            _hwnd = hwnd;
            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"));
                if (type == null) return;
                _list = (ITaskbarList3)Activator.CreateInstance(type);
                _list.HrInit();
                _available = true;
            }
            catch
            {
                _available = false;
            }
        }

        public void SetValue(int done, int total)
        {
            if (!_available) return;
            try
            {
                _list.SetProgressState(_hwnd, 2); // TBPF_NORMAL
                _list.SetProgressValue(_hwnd, (ulong)Math.Max(0, done), (ulong)Math.Max(1, total));
            }
            catch { }
        }

        public void Clear()
        {
            if (!_available) return;
            try { _list.SetProgressState(_hwnd, 0); } catch { } // TBPF_NOPROGRESS
        }

        public void Dispose()
        {
            Clear();
            if (_list != null)
            {
                try { Marshal.ReleaseComObject(_list); } catch { }
                _list = null;
            }
            _available = false;
        }
    }

    static class AppIcon
    {
        public static Icon Create()
        {
            using (Stream stream = OpenResource("PdfWordTool.Capy.ico"))
            using (Icon icon = new Icon(stream, 64, 64))
                return (Icon)icon.Clone();
        }

        public static Bitmap CreateLogo()
        {
            using (Stream stream = OpenResource("PdfWordTool.CapyLogo.ico"))
            using (Icon icon = new Icon(stream, 128, 128))
                return icon.ToBitmap();
        }

        public static Icon CreateTray()
        {
            using (Stream stream = OpenResource("PdfWordTool.CapyTray.ico"))
            using (Icon icon = new Icon(stream, 32, 32))
                return (Icon)icon.Clone();
        }

        // 标题栏小图标：用高清 logo 缩放，比 ico 的 16px 帧更清晰。
        public static Bitmap CreateSmall()
        {
            using (Bitmap large = CreateLogo())
            {
                Bitmap small = new Bitmap(20, 20);
                using (Graphics g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(large, new Rectangle(0, 0, 20, 20));
                }
                return small;
            }
        }

        private static Stream OpenResource(string name)
        {
            Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("缺少图标资源：" + name);
            return stream;
        }
    }
}
