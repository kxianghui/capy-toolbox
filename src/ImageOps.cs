using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfWordTool
{
    enum CompressMode
    {
        Normal,
        HighQuality,
        TargetSize
    }

    sealed class CompressOptions
    {
        public CompressMode Mode = CompressMode.Normal;
        public long TargetBytes;
        public int MaxWidth;
        public int MaxHeight;
        public string OutputDir;
    }

    static class ImageOps
    {
        public static readonly string[] SupportedExtensions =
            new string[] { ".jpg", ".jpeg", ".png", ".bmp" };

        private const int NormalJpegQuality = 85;
        private const int HighJpegMinQuality = 60;
        private const int HighJpegMaxQuality = 85;
        private const double JpegMinPsnr = 33.0;
        private const double IndexedMinPsnr = 28.0;

        public static bool IsSupported(string path)
        {
            string ext = Path.GetExtension(path);
            foreach (string item in SupportedExtensions)
                if (string.Equals(ext, item, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // 压缩入口：普通 / 高品质 / 目标体积三种模式。
        // 输出到指定目录（未设置时为源文件同目录），文件名追加 _压缩，冲突自动编号；永不覆盖原文件。
        public static string Compress(string input, CompressOptions options)
        {
            string full = Path.GetFullPath(input);
            string dir = string.IsNullOrEmpty(options.OutputDir)
                ? Path.GetDirectoryName(full)
                : options.OutputDir;
            if (!Directory.Exists(dir)) throw new IOException("输出目录不存在：" + dir);
            string name = Path.GetFileNameWithoutExtension(full);
            string ext = Path.GetExtension(full).ToLowerInvariant();
            string outExt = (ext == ".jpg" || ext == ".jpeg") ? ".jpg" : ext;

            if (outExt != ".jpg" && outExt != ".png" && outExt != ".bmp")
                throw new NotSupportedException("不支持的图片格式：" + ext);

            long originalLength = new FileInfo(full).Length;
            string output = UniquePath(dir, name + "_压缩", outExt);

            using (Bitmap source = LoadOriented(full))
            {
                if (options.Mode == CompressMode.TargetSize)
                {
                    CompressToTarget(source, full, output, outExt, options.TargetBytes,
                                     options.MaxWidth, options.MaxHeight, originalLength);
                    Verify(output);
                    return output;
                }

                using (Bitmap resized = ResizeWithin(source, options.MaxWidth, options.MaxHeight))
                {
                    Bitmap work = resized != null ? resized : source;
                    if (options.Mode == CompressMode.HighQuality)
                        SaveHighQuality(work, output, outExt);
                    else
                        SaveNormal(work, output, outExt);

                    // 未缩放且结果比原文件更大：保留原文件内容，保证"压缩"不变大。
                    if (resized == null && new FileInfo(output).Length >= originalLength)
                        ReplaceWithOriginal(full, output);

                    Verify(output);
                    return output;
                }
            }
        }

        // 格式转换：输出到指定目录（未设置时为源文件同目录），不覆盖原文件。
        public static string Convert(string input, string targetExt, int level, string outputDir)
        {
            string full = Path.GetFullPath(input);
            string dir = string.IsNullOrEmpty(outputDir)
                ? Path.GetDirectoryName(full)
                : outputDir;
            if (!Directory.Exists(dir)) throw new IOException("输出目录不存在：" + dir);
            string name = Path.GetFileNameWithoutExtension(full);
            string inputExt = Path.GetExtension(full).ToLowerInvariant();
            string ext = targetExt.ToLowerInvariant();

            using (Bitmap source = LoadOriented(full))
            {
                bool sameFormat = inputExt == ext || (inputExt == ".jpeg" && ext == ".jpg");
                string baseName = sameFormat ? name + "_转换" : name;
                string output = UniquePath(dir, baseName, ext);
                ImageWriters.Save(source, output, ext, level);
                Verify(output);
                return output;
            }
        }

        // ---------- 三种压缩模式 ----------

        private static void SaveNormal(Bitmap work, string path, string ext)
        {
            if (ext == ".jpg")
            {
                File.WriteAllBytes(path, ImageWriters.EncodeJpegBytes(work, NormalJpegQuality));
                return;
            }

            bool alpha = HasTransparency(work);
            byte[] bytes = ext == ".png" ? EncodePng(work, alpha) : EncodeBmp(work, alpha);
            File.WriteAllBytes(path, bytes);
        }

        private static void SaveHighQuality(Bitmap work, string path, string ext)
        {
            if (ext == ".jpg")
            {
                File.WriteAllBytes(path, EncodeBestJpeg(work, JpegMinPsnr));
                return;
            }

            if (HasTransparency(work))
            {
                byte[] alphaBytes = ext == ".png" ? EncodePng(work, true) : EncodeBmp(work, true);
                File.WriteAllBytes(path, alphaBytes);
                return;
            }

            byte[] lossless = ext == ".png" ? EncodePng(work, false) : EncodeBmp(work, false);
            byte[] indexed = null;
            double psnr;
            using (Bitmap indexedBmp = QuantizeToIndexed(work, 256, true, out psnr))
            {
                if (psnr >= IndexedMinPsnr)
                    indexed = SaveToBytes(indexedBmp, ext == ".png" ? ImageFormat.Png : ImageFormat.Bmp);
            }

            File.WriteAllBytes(path, indexed != null && indexed.Length < lossless.Length ? indexed : lossless);
        }

        // 目标体积：先按边界框缩放，再搜索能落进目标的画质/调色板，必要时逐步降分辨率。
        // 保持原格式；无法命中时抛异常，不产出超标文件。
        private static void CompressToTarget(Bitmap source, string inputFull, string output,
                                             string ext, long targetBytes, int maxWidth, int maxHeight,
                                             long originalLength)
        {
            if (targetBytes <= 0)
                throw new IOException("目标体积必须大于 0");

            double fit = 1.0;
            if (maxWidth > 0) fit = Math.Min(fit, (double)maxWidth / source.Width);
            if (maxHeight > 0) fit = Math.Min(fit, (double)maxHeight / source.Height);
            if (fit > 1.0) fit = 1.0;

            // 原文件已达标且无需缩放：直接复制，避免无意义的质量损失。
            if (fit >= 1.0 && originalLength <= targetBytes)
            {
                File.Copy(inputFull, output, true);
                return;
            }

            double scale = fit;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int w = Math.Max(1, (int)Math.Round(source.Width * scale));
                int h = Math.Max(1, (int)Math.Round(source.Height * scale));

                Bitmap resized = null;
                try
                {
                    Bitmap work = source;
                    if (w != source.Width || h != source.Height)
                    {
                        resized = ResizeTo(source, w, h);
                        work = resized;
                    }

                    byte[] bytes;
                    if (TryEncodeUnderTarget(work, ext, targetBytes, out bytes))
                    {
                        File.WriteAllBytes(output, bytes);
                        return;
                    }
                }
                finally
                {
                    if (resized != null) resized.Dispose();
                }

                if (w <= 24 || h <= 24) break;
                scale *= 0.85;
            }

            throw new IOException("无法压缩到 " + FormatSize(targetBytes) + " 以内");
        }

        private static bool TryEncodeUnderTarget(Bitmap work, string ext, long target, out byte[] result)
        {
            result = null;

            if (ext == ".jpg")
            {
                // 找到仍能落进目标的最高画质（画质越高体积越大）。
                int lo = 5;
                int hi = 95;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    byte[] bytes = ImageWriters.EncodeJpegBytes(work, mid);
                    if (bytes.Length <= target)
                    {
                        result = bytes;
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
                return result != null;
            }

            bool alpha = HasTransparency(work);

            // 无损 24 位（或透明 32 位）已经达标时优先保留最高质量。
            byte[] lossless = ext == ".png" ? EncodePng(work, alpha) : EncodeBmp(work, alpha);
            if (lossless.Length <= target)
            {
                result = lossless;
                return true;
            }

            // PNG/BMP 含透明通道时无法索引量化，只能靠后续继续降分辨率。
            if (alpha) return false;

            int[] counts = new int[] { 256, 64, 16, 4, 2 };
            foreach (int count in counts)
            {
                double psnr;
                using (Bitmap indexed = QuantizeToIndexed(work, count, false, out psnr))
                {
                    byte[] bytes = SaveToBytes(indexed, ext == ".png" ? ImageFormat.Png : ImageFormat.Bmp);
                    if (bytes.Length <= target)
                    {
                        result = bytes;
                        return true;
                    }
                }
            }
            return false;
        }

        // 从高画质向低画质二分，取仍满足清晰度阈值的最低画质（体积最小）。
        private static byte[] EncodeBestJpeg(Bitmap work, double minPsnr)
        {
            using (Bitmap reference = ToRgb24(work))
            {
                byte[] best = null;
                int lo = HighJpegMinQuality;
                int hi = HighJpegMaxQuality;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    byte[] bytes = ImageWriters.EncodeJpegBytes(work, mid);
                    if (JpegPsnr(reference, bytes) >= minPsnr)
                    {
                        best = bytes;
                        hi = mid - 1;
                    }
                    else
                    {
                        lo = mid + 1;
                    }
                }
                if (best != null) return best;
            }
            return ImageWriters.EncodeJpegBytes(work, HighJpegMaxQuality);
        }

        // ---------- 缩放 ----------

        private static Bitmap ResizeWithin(Bitmap source, int maxWidth, int maxHeight)
        {
            double scale = 1.0;
            if (maxWidth > 0 && source.Width > maxWidth)
                scale = Math.Min(scale, (double)maxWidth / source.Width);
            if (maxHeight > 0 && source.Height > maxHeight)
                scale = Math.Min(scale, (double)maxHeight / source.Height);
            if (scale >= 1.0) return null;

            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));
            return ResizeTo(source, w, h);
        }

        private static Bitmap ResizeTo(Bitmap source, int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, width, height));
            }
            return result;
        }

        // ---------- 编码辅助 ----------

        private static byte[] EncodePng(Bitmap work, bool keepAlpha)
        {
            if (keepAlpha) return SaveToBytes(work, ImageFormat.Png);
            using (Bitmap rgb = ToRgb24(work))
                return SaveToBytes(rgb, ImageFormat.Png);
        }

        private static byte[] EncodeBmp(Bitmap work, bool keepAlpha)
        {
            if (keepAlpha) return SaveToBytes(work, ImageFormat.Bmp);
            using (Bitmap rgb = ToRgb24(work))
                return SaveToBytes(rgb, ImageFormat.Bmp);
        }

        private static byte[] SaveToBytes(Image image, ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, format);
                return ms.ToArray();
            }
        }

        private static Bitmap ToRgb24(Image image)
        {
            Bitmap canvas = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
            }
            return canvas;
        }

        private static bool HasTransparency(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] row = new byte[stride];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy((IntPtr)((long)data.Scan0 + (long)y * data.Stride), row, 0, stride);
                    for (int x = 0; x < bitmap.Width; x++)
                        if (row[x * 4 + 3] < 255) return true;
                }
                return false;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        // ---------- PSNR ----------

        private static double JpegPsnr(Bitmap reference24, byte[] jpegBytes)
        {
            using (MemoryStream ms = new MemoryStream(jpegBytes))
            using (Image decoded = Image.FromStream(ms))
            using (Bitmap rgb = ToRgb24(decoded))
            {
                return Psnr(reference24, rgb);
            }
        }

        private static double Psnr(Bitmap a, Bitmap b)
        {
            int w = Math.Min(a.Width, b.Width);
            int h = Math.Min(a.Height, b.Height);
            int step = (long)w * h > 1500000L ? 2 : 1;

            BitmapData da = a.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData db = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Min(Math.Abs(da.Stride), Math.Abs(db.Stride));
                byte[] ra = new byte[Math.Abs(da.Stride)];
                byte[] rb = new byte[Math.Abs(db.Stride)];
                long se = 0;
                long count = 0;

                for (int y = 0; y < h; y += step)
                {
                    Marshal.Copy((IntPtr)((long)da.Scan0 + (long)y * da.Stride), ra, 0, stride);
                    Marshal.Copy((IntPtr)((long)db.Scan0 + (long)y * db.Stride), rb, 0, stride);
                    for (int x = 0; x < w; x += step)
                    {
                        int o = x * 3;
                        int dr = ra[o] - rb[o];
                        int dg = ra[o + 1] - rb[o + 1];
                        int dbv = ra[o + 2] - rb[o + 2];
                        se += (long)dr * dr + (long)dg * dg + (long)dbv * dbv;
                        count += 3;
                    }
                }

                if (count == 0 || se == 0) return 99.0;
                double mse = (double)se / count;
                return 10.0 * Math.Log10(255.0 * 255.0 / mse);
            }
            finally
            {
                a.UnlockBits(da);
                b.UnlockBits(db);
            }
        }

        // ---------- 调色板量化（PNG/BMP 8bpp） ----------

        private static Bitmap QuantizeToIndexed(Bitmap source, int colorCount, bool dither, out double psnr)
        {
            using (Bitmap rgb = ToRgb24(source))
            {
                int w = rgb.Width;
                int h = rgb.Height;
                int[] palette = BuildPalette(rgb, colorCount);
                byte[] lut = BuildPaletteLut(palette);

                Bitmap result = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
                ColorPalette cp = result.Palette;
                for (int i = 0; i < 256; i++)
                {
                    int c = i < palette.Length ? palette[i] : 0;
                    cp.Entries[i] = Color.FromArgb(255, (c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
                }
                result.Palette = cp;

                Rectangle rect = new Rectangle(0, 0, w, h);
                BitmapData src = rgb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                BitmapData dst = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    byte[] row = new byte[Math.Abs(src.Stride)];
                    byte[] outRow = new byte[w];
                    int[] errCur = dither ? new int[(w + 2) * 3] : null;
                    int[] errNext = dither ? new int[(w + 2) * 3] : null;
                    long se = 0;

                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy((IntPtr)((long)src.Scan0 + (long)y * src.Stride), row, 0, row.Length);

                        for (int x = 0; x < w; x++)
                        {
                            int o = x * 3;
                            int b0 = row[o];
                            int g0 = row[o + 1];
                            int r0 = row[o + 2];

                            int r = r0;
                            int g = g0;
                            int b = b0;
                            if (dither)
                            {
                                r = ClampByte(r0 + errCur[(x + 1) * 3] / 16);
                                g = ClampByte(g0 + errCur[(x + 1) * 3 + 1] / 16);
                                b = ClampByte(b0 + errCur[(x + 1) * 3 + 2] / 16);
                            }

                            int idx = lut[((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)];
                            int pc = palette[idx];
                            int pr = (pc >> 16) & 0xFF;
                            int pg = (pc >> 8) & 0xFF;
                            int pb = pc & 0xFF;
                            outRow[x] = (byte)idx;

                            int dr = r0 - pr;
                            int dg = g0 - pg;
                            int db = b0 - pb;
                            se += (long)dr * dr + (long)dg * dg + (long)db * db;

                            if (dither)
                            {
                                int er = r - pr;
                                int eg = g - pg;
                                int eb = b - pb;

                                errCur[(x + 2) * 3] += er * 7;
                                errCur[(x + 2) * 3 + 1] += eg * 7;
                                errCur[(x + 2) * 3 + 2] += eb * 7;

                                errNext[x * 3] += er * 3;
                                errNext[x * 3 + 1] += eg * 3;
                                errNext[x * 3 + 2] += eb * 3;

                                errNext[(x + 1) * 3] += er * 5;
                                errNext[(x + 1) * 3 + 1] += eg * 5;
                                errNext[(x + 1) * 3 + 2] += eb * 5;

                                errNext[(x + 2) * 3] += er;
                                errNext[(x + 2) * 3 + 1] += eg;
                                errNext[(x + 2) * 3 + 2] += eb;
                            }
                        }

                        Marshal.Copy(outRow, 0, (IntPtr)((long)dst.Scan0 + (long)y * dst.Stride), w);

                        if (dither)
                        {
                            int[] swap = errCur;
                            errCur = errNext;
                            errNext = swap;
                            Array.Clear(errNext, 0, errNext.Length);
                        }
                    }

                    long samples = (long)w * h * 3;
                    if (se == 0) psnr = 99.0;
                    else
                    {
                        double mse = (double)se / samples;
                        psnr = 10.0 * Math.Log10(255.0 * 255.0 / mse);
                    }
                }
                finally
                {
                    rgb.UnlockBits(src);
                    result.UnlockBits(dst);
                }

                return result;
            }
        }

        private static int ClampByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }

        // 中位切分量化：采样 → 按最大跨度通道切分 → 每盒取均值。
        private sealed class PaletteBox
        {
            public readonly int Start;
            public readonly int End;

            public PaletteBox(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Count
            {
                get { return End - Start + 1; }
            }
        }

        private sealed class ChannelComparer : IComparer<int>
        {
            private readonly int _shift;

            public ChannelComparer(int shift)
            {
                _shift = shift;
            }

            public int Compare(int a, int b)
            {
                return ((a >> _shift) & 0xFF).CompareTo((b >> _shift) & 0xFF);
            }
        }

        private static int[] BuildPalette(Bitmap rgb, int colorCount)
        {
            int w = rgb.Width;
            int h = rgb.Height;
            int step = Math.Max(1, (int)Math.Sqrt((double)w * h / 20000.0));

            List<int> samples = new List<int>();
            BitmapData data = rgb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < h; y += step)
                {
                    Marshal.Copy((IntPtr)((long)data.Scan0 + (long)y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < w; x += step)
                    {
                        int o = x * 3;
                        samples.Add((row[o + 2] << 16) | (row[o + 1] << 8) | row[o]);
                    }
                }
            }
            finally
            {
                rgb.UnlockBits(data);
            }

            if (samples.Count == 0) return new int[] { 0 };

            List<PaletteBox> boxes = new List<PaletteBox>();
            boxes.Add(new PaletteBox(0, samples.Count - 1));

            while (boxes.Count < colorCount)
            {
                int bestIndex = -1;
                int bestRange = 0;
                int bestChannel = 0;

                for (int i = 0; i < boxes.Count; i++)
                {
                    PaletteBox box = boxes[i];
                    if (box.Count <= 1) continue;

                    int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
                    for (int j = box.Start; j <= box.End; j++)
                    {
                        int c = samples[j];
                        int r = (c >> 16) & 0xFF;
                        int g = (c >> 8) & 0xFF;
                        int b = c & 0xFF;
                        if (r < minR) minR = r;
                        if (r > maxR) maxR = r;
                        if (g < minG) minG = g;
                        if (g > maxG) maxG = g;
                        if (b < minB) minB = b;
                        if (b > maxB) maxB = b;
                    }

                    int rangeR = maxR - minR;
                    int rangeG = maxG - minG;
                    int rangeB = maxB - minB;
                    int range = Math.Max(rangeR, Math.Max(rangeG, rangeB));
                    int channel = range == rangeR ? 16 : range == rangeG ? 8 : 0;

                    if (range > bestRange)
                    {
                        bestRange = range;
                        bestIndex = i;
                        bestChannel = channel;
                    }
                }

                if (bestIndex < 0 || bestRange <= 0) break;

                PaletteBox split = boxes[bestIndex];
                samples.Sort(split.Start, split.Count, new ChannelComparer(bestChannel));
                int mid = split.Start + split.Count / 2;

                boxes.RemoveAt(bestIndex);
                boxes.Add(new PaletteBox(split.Start, mid - 1));
                boxes.Add(new PaletteBox(mid, split.End));
            }

            int[] palette = new int[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                PaletteBox box = boxes[i];
                long sr = 0, sg = 0, sb = 0;
                for (int j = box.Start; j <= box.End; j++)
                {
                    int c = samples[j];
                    sr += (c >> 16) & 0xFF;
                    sg += (c >> 8) & 0xFF;
                    sb += c & 0xFF;
                }
                int n = box.Count;
                palette[i] = (((int)((sr + n / 2) / n) & 0xFF) << 16) |
                             (((int)((sg + n / 2) / n) & 0xFF) << 8) |
                             ((int)((sb + n / 2) / n) & 0xFF);
            }
            return palette;
        }

        // 32×32×32 最近色查找表，避免逐像素遍历调色板。
        private static byte[] BuildPaletteLut(int[] palette)
        {
            byte[] lut = new byte[32 * 32 * 32];
            for (int r = 0; r < 32; r++)
            {
                int rr = r * 255 / 31;
                for (int g = 0; g < 32; g++)
                {
                    int gg = g * 255 / 31;
                    for (int b = 0; b < 32; b++)
                    {
                        int bb = b * 255 / 31;
                        int best = 0;
                        long bestD = long.MaxValue;
                        for (int i = 0; i < palette.Length; i++)
                        {
                            int c = palette[i];
                            int dr = rr - ((c >> 16) & 0xFF);
                            int dg = gg - ((c >> 8) & 0xFF);
                            int db = bb - (c & 0xFF);
                            long d = (long)dr * dr + (long)dg * dg + (long)db * db;
                            if (d < bestD)
                            {
                                bestD = d;
                                best = i;
                                if (d == 0) break;
                            }
                        }
                        lut[(r << 10) | (g << 5) | b] = (byte)best;
                    }
                }
            }
            return lut;
        }

        // ---------- 通用辅助 ----------

        private static void ReplaceWithOriginal(string source, string output)
        {
            try { File.Copy(source, output, true); } catch { }
        }

        private static string FormatSize(long bytes)
        {
            return (bytes / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " KB";
        }

        private static void Verify(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new IOException("输出文件写入失败：" + Path.GetFileName(path));
        }

        private static string UniquePath(string dir, string baseName, string ext)
        {
            string candidate = Path.Combine(dir, baseName + ext);
            if (!File.Exists(candidate)) return candidate;
            for (int i = 1; ; i++)
            {
                candidate = Path.Combine(dir, string.Format("{0} ({1}){2}", baseName, i, ext));
                if (!File.Exists(candidate)) return candidate;
            }
        }

        // 读取图片并按 EXIF 方向校正，同时解除文件占用。
        private static Bitmap LoadOriented(string path)
        {
            using (Image raw = Image.FromFile(path))
            {
                int orientation = 1;
                try
                {
                    foreach (PropertyItem prop in raw.PropertyItems)
                    {
                        if (prop.Id == 0x0112 && prop.Value != null && prop.Value.Length >= 2)
                        {
                            orientation = BitConverter.ToUInt16(prop.Value, 0);
                            break;
                        }
                    }
                }
                catch { }

                Bitmap bmp = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(raw, new Rectangle(0, 0, raw.Width, raw.Height));
                }

                switch (orientation)
                {
                    case 2: bmp.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                    case 3: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                    case 4: bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); break;
                    case 5: bmp.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                    case 6: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                    case 7: bmp.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                    case 8: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                }

                return bmp;
            }
        }
    }
}
