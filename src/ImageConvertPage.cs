using System.Drawing;
using System.Windows.Forms;

namespace PdfWordTool
{
    sealed class FormatOptions
    {
        public string TargetExt;
        public int QualityLevel;
        public string OutputDir;
    }

    sealed class ImageConvertPage : ToolPage
    {
        private static readonly string[] Extensions =
            new string[] { ".jpg", ".png", ".bmp", ".ico", ".xbm", ".xpm", ".pdf" };

        private static readonly string[] FormatLabels = new string[]
        {
            "JPG（*.jpg）", "PNG（*.png）", "BMP（*.bmp）", "ICO（*.ico）",
            "XBM（*.xbm）", "XPM（*.xpm）", "PDF（*.pdf）"
        };

        // 每个格式下 普通/高清/超清 的实际含义各不相同。
        private static readonly string[][] QualityLabels = new string[][]
        {
            new string[] { "普通（质量85）", "高清（质量92）", "超清（质量97）" },
            new string[] { "普通（8位256色）", "高清（24位真彩）", "超清（32位透明）" },
            new string[] { "普通（8位索引）", "高清（24位）", "超清（32位）" },
            new string[] { "普通（32×32）", "高清（64×64）", "超清（256×256）" },
            new string[] { "普通（阈值）", "高清（抖动）", "超清（2倍抖动）" },
            new string[] { "普通（64色）", "高清（128色）", "超清（256色）" },
            new string[] { "普通（质量85）", "高清（质量92）", "超清（质量97）" }
        };

        private readonly ThemedComboBox _format;
        private readonly ThemedComboBox _quality;

        public ImageConvertPage()
            : base("批量拖入图片",
                   "7 种格式 · 点击选择",
                   "开始转换", "转换",
                   new string[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            _format = new ThemedComboBox();
            _format.Size = new Size(140, 32);
            foreach (string label in FormatLabels) _format.Items.Add(label);
            AddOption("目标格式", _format);

            _quality = new ThemedComboBox();
            _quality.Size = new Size(184, 32);
            AddOption("画质", _quality);

            _format.SelectedIndexChanged += delegate
            {
                RebuildQualityItems(_format.SelectedIndex);
                ResetCompletedRows();
            };
            _format.SelectedIndex = 0;
            RebuildQualityItems(0);
        }

        private void RebuildQualityItems(int formatIndex)
        {
            if (formatIndex < 0 || formatIndex >= QualityLabels.Length) return;

            string[] labels = QualityLabels[formatIndex];
            _quality.Items.Clear();
            foreach (string label in labels) _quality.Items.Add(label);
            _quality.SelectedIndex = 1;
            _quality.Invalidate();
        }

        protected override object CaptureOptions()
        {
            FormatOptions options = new FormatOptions();
            options.TargetExt = Extensions[_format.SelectedIndex];
            options.QualityLevel = _quality.SelectedIndex;
            options.OutputDir = OutputDir;
            return options;
        }

        protected override void Execute(FileItem item, object options)
        {
            FormatOptions format = (FormatOptions)options;
            item.Output = ImageOps.Convert(item.Path, format.TargetExt, format.QualityLevel, format.OutputDir);
        }
    }
}
