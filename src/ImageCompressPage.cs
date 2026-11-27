using System;
using System.Drawing;
using System.Windows.Forms;

namespace PdfWordTool
{
    // 压缩页的纵向选项区：三种压缩方式 + 目标体积 + 分辨率上限。
    sealed class CompressOptionsPanel : Panel
    {
        private readonly ThemedRadioButton _normal;
        private readonly ThemedRadioButton _high;
        private readonly ThemedRadioButton _target;
        private readonly ThemedNumberBox _targetValue;
        private readonly ThemedComboBox _unit;
        private readonly ThemedNumberBox _width;
        private readonly ThemedNumberBox _height;
        private readonly ChainToggle _link;
        private readonly Label _resolutionLabel;
        private readonly Label _hint;

        private bool _syncingLink;
        private int _lastWidth;
        private int _lastHeight;

        public CompressOptionsPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.CardBack;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(620, 266);

            Label title = new Label();
            title.Text = "压缩方式";
            title.AutoSize = true;
            title.ForeColor = Theme.Text;
            title.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            title.Location = new Point(0, 0);
            Controls.Add(title);

            int titleWidth = TextRenderer.MeasureText(title.Text, title.Font).Width;
            Label note = new Label();
            note.Text = "不支持 gif、svg 格式";
            note.AutoSize = true;
            note.ForeColor = Theme.Muted;
            note.Location = new Point(titleWidth + 10, 1);
            Controls.Add(note);

            _normal = NewRadio("普通压缩", "压缩比例小，体积大", 24);
            _high = NewRadio("高品质压缩", "在保证清晰度的前提下，压缩到最小", 64);
            _target = NewRadio("每张图片体积小于", "", 104);

            _targetValue = new ThemedNumberBox();
            _targetValue.Size = new Size(150, 32);
            _targetValue.Maximum = 1048576;
            _targetValue.Decimals = 2;
            _targetValue.DecimalValue = 200m;
            _targetValue.Location = new Point(24, 146);
            Controls.Add(_targetValue);

            _unit = new ThemedComboBox();
            _unit.Size = new Size(76, 32);
            _unit.Items.Add("KB");
            _unit.Items.Add("MB");
            _unit.SelectedIndex = 0;
            _unit.Location = new Point(182, 146);
            Controls.Add(_unit);

            _resolutionLabel = new Label();
            _resolutionLabel.Text = "压缩后分辨率上限（单位：像素）";
            _resolutionLabel.AutoSize = true;
            _resolutionLabel.ForeColor = Theme.Muted;
            _resolutionLabel.Location = new Point(24, 186);
            Controls.Add(_resolutionLabel);

            _width = new ThemedNumberBox();
            _width.Size = new Size(150, 32);
            _width.Suffix = "宽";
            _width.Maximum = 20000;
            _width.Location = new Point(24, 206);
            Controls.Add(_width);

            _link = new ChainToggle();
            _link.Size = new Size(28, 32);
            _link.Location = new Point(180, 206);
            Controls.Add(_link);

            _height = new ThemedNumberBox();
            _height.Size = new Size(150, 32);
            _height.Suffix = "高";
            _height.Maximum = 20000;
            _height.Location = new Point(214, 206);
            Controls.Add(_height);

            _hint = new Label();
            _hint.Text = "0 = 不限制，超出上限时按比例缩小";
            _hint.AutoSize = true;
            _hint.ForeColor = Theme.Muted;
            _hint.Location = new Point(24, 246);
            Controls.Add(_hint);

            _normal.CheckedChanged += delegate { SelectMode(_normal); };
            _high.CheckedChanged += delegate { SelectMode(_high); };
            _target.CheckedChanged += delegate { SelectMode(_target); };
            _width.Leave += OnWidthLeave;
            _height.Leave += OnHeightLeave;

            SelectMode(_normal);
        }

        public event EventHandler SettingsVisibilityChanged;

        public CompressMode Mode
        {
            get
            {
                if (_high.Checked) return CompressMode.HighQuality;
                if (_target.Checked) return CompressMode.TargetSize;
                return CompressMode.Normal;
            }
        }

        public long TargetBytes
        {
            get
            {
                decimal multiplier = _unit.SelectedIndex == 1 ? 1024m * 1024m : 1024m;
                return (long)Math.Round(_targetValue.DecimalValue * multiplier, MidpointRounding.AwayFromZero);
            }
        }

        public int MaxWidth
        {
            get { return _width.Value; }
        }

        public int MaxHeight
        {
            get { return _height.Value; }
        }

        private ThemedRadioButton NewRadio(string text, string description, int top)
        {
            ThemedRadioButton radio = new ThemedRadioButton();
            radio.Text = text;
            radio.Description = description;
            radio.Size = new Size(600, 38);
            radio.Location = new Point(0, top);
            Controls.Add(radio);
            return radio;
        }

        private void SelectMode(ThemedRadioButton selected)
        {
            bool target = selected == _target;
            _normal.Checked = selected == _normal;
            _high.Checked = selected == _high;
            _target.Checked = selected == _target;

            // 只有「每张图片体积小于」需要下方面板；其他模式隐藏以节约空间。
            _targetValue.Visible = target;
            _unit.Visible = target;
            _resolutionLabel.Visible = target;
            _width.Visible = target;
            _link.Visible = target;
            _height.Visible = target;
            _hint.Visible = target;

            int height = target ? 266 : 150;
            if (Height != height) Height = height;
            if (SettingsVisibilityChanged != null) SettingsVisibilityChanged(this, EventArgs.Empty);
        }

        private void OnWidthLeave(object sender, EventArgs e)
        {
            if (_syncingLink) return;

            int w = _width.Value;
            int h = _height.Value;
            if (_link.Locked && _lastWidth > 0 && _lastHeight > 0 && w > 0)
            {
                int adjusted = (int)Math.Round((double)w * _lastHeight / _lastWidth);
                if (adjusted < 1) adjusted = 1;
                if (adjusted > _height.Maximum) adjusted = _height.Maximum;
                _syncingLink = true;
                _height.Value = adjusted;
                _syncingLink = false;
                h = adjusted;
            }

            _lastWidth = w;
            _lastHeight = h;
        }

        private void OnHeightLeave(object sender, EventArgs e)
        {
            if (_syncingLink) return;

            int w = _width.Value;
            int h = _height.Value;
            if (_link.Locked && _lastWidth > 0 && _lastHeight > 0 && h > 0)
            {
                int adjusted = (int)Math.Round((double)h * _lastWidth / _lastHeight);
                if (adjusted < 1) adjusted = 1;
                if (adjusted > _width.Maximum) adjusted = _width.Maximum;
                _syncingLink = true;
                _width.Value = adjusted;
                _syncingLink = false;
                w = adjusted;
            }

            _lastWidth = w;
            _lastHeight = h;
        }
    }

    sealed class ImageCompressPage : ToolPage
    {
        private readonly CompressOptionsPanel _options;

        public ImageCompressPage()
            : base("批量拖入图片",
                   "JPG / PNG / BMP · 点击选择",
                   "开始压缩", "压缩",
                   new string[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            _options = new CompressOptionsPanel();
            _options.SettingsVisibilityChanged += delegate { SetOptionsHeight(_options.Height + 22); };
            AddOptionControl(_options);
            SetOptionsHeight(_options.Height + 22);
        }

        protected override object CaptureOptions()
        {
            CompressOptions options = new CompressOptions();
            options.Mode = _options.Mode;
            options.OutputDir = OutputDir;
            if (options.Mode == CompressMode.TargetSize)
            {
                options.TargetBytes = _options.TargetBytes;
                options.MaxWidth = _options.MaxWidth;
                options.MaxHeight = _options.MaxHeight;
            }
            return options;
        }

        protected override void Execute(FileItem item, object options)
        {
            item.Output = ImageOps.Compress(item.Path, (CompressOptions)options);
        }
    }
}
