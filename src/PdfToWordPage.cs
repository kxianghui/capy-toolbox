using System;

namespace PdfWordTool
{
    sealed class PdfToWordPage : ToolPage
    {
        public PdfToWordPage()
            : base("批量拖入 PDF 文件",
                   "PDF · 点击选择",
                   "开始转换", "转换", new string[] { ".pdf" })
        {
        }

        protected override object CaptureOptions()
        {
            return OutputDir;
        }

        protected override void Execute(FileItem item, object options)
        {
            string output;
            Engine.Convert(item.Path, (string)options, out output);
            item.Output = output;
        }
    }
}
