using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfWordTool
{
    // 图片格式写出：7 种输出格式，level 0/1/2 对应 普通/高清/超清。
    static class ImageWriters
    {
        public static void Save(Image image, string path, string ext, int level)
        {
            if (level < 0) level = 0;
            else if (level > 2) level = 2;

            ext = ext.ToLowerInvariant();
            switch (ext)
            {
                case ".jpg": SaveJpeg(image, path, JpegQuality(level)); return;
                case ".png": SavePng(image, path, level); return;
                case ".bmp": SaveBmp(image, path, level); return;
                case ".ico": SaveIco(image, path, level); return;
                case ".xbm": SaveXbm(image, path, level); return;
                case ".xpm": SaveXpm(image, path, level); return;
                case ".pdf": SavePdf(image, path, level); return;
                default: throw new NotSupportedException("不支持的输出格式：" + ext);
            }
        }

        private static int JpegQuality(int level)
        {
            switch (level)
            {
                case 0: return 85;
                case 1: return 92;
                default: return 97;
            }
        }

        // ---------- 通用位图辅助 ----------

        // 24bpp 白底合成：用于 JPG / BMP24 / XPM / PDF / XBM。
        private static Bitmap ToRgb24OnWhite(Image image)
        {
            Bitmap canvas = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
            }
            return canvas;
        }

        // 32bpp 保留透明通道。
        private static Bitmap ToArgb32(Image image)
        {
            Bitmap canvas = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
            }
            return canvas;
        }

        // 32bpp：保留 alpha，同时把 RGB 合成到白底。
        // 忽略 alpha 的老查看器（以及 GDI+ 读取 BMP 时）不会把透明区域显示成黑色。
        private static Bitmap ToArgb32OnWhite(Image image)
        {
            using (Bitmap argb = ToArgb32(image))
            {
                int w = argb.Width;
                int h = argb.Height;
                Rectangle rect = new Rectangle(0, 0, w, h);
                Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                BitmapData src = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData dst = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[Math.Abs(src.Stride)];
                    byte[] outRow = new byte[Math.Abs(dst.Stride)];
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy((IntPtr)((long)src.Scan0 + (long)y * src.Stride), row, 0, row.Length);
                        for (int x = 0; x < w; x++)
                        {
                            int o = x * 4;
                            int b = row[o];
                            int g = row[o + 1];
                            int r = row[o + 2];
                            int a = row[o + 3];
                            int inv = 255 - a;
                            outRow[o] = (byte)((b * a + 255 * inv) / 255);
                            outRow[o + 1] = (byte)((g * a + 255 * inv) / 255);
                            outRow[o + 2] = (byte)((r * a + 255 * inv) / 255);
                            outRow[o + 3] = (byte)a;
                        }
                        Marshal.Copy(outRow, 0, (IntPtr)((long)dst.Scan0 + (long)y * dst.Stride), outRow.Length);
                    }
                }
                finally
                {
                    argb.UnlockBits(src);
                    result.UnlockBits(dst);
                }
                return result;
            }
        }

        // 8bpp 索引色：手写 3-3-2 调色板 + 逐行量化。
        // Graphics 无法可靠地直接绘制到 8bpp 位图，因此手动锁定像素。
        private static Bitmap ToIndexed8(Image image)
        {
            using (Bitmap rgb = ToRgb24OnWhite(image))
            {
                Bitmap result = new Bitmap(rgb.Width, rgb.Height, PixelFormat.Format8bppIndexed);
                ColorPalette palette = result.Palette;
                for (int i = 0; i < 256; i++)
                {
                    int r = (i >> 5) & 7;
                    int g = (i >> 2) & 7;
                    int b = i & 3;
                    palette.Entries[i] = Color.FromArgb(255, r * 255 / 7, g * 255 / 7, b * 255 / 3);
                }
                result.Palette = palette;

                int w = rgb.Width;
                int h = rgb.Height;
                Rectangle rect = new Rectangle(0, 0, w, h);
                BitmapData src = rgb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                BitmapData dst = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    int srcStride = src.Stride;
                    int dstStride = dst.Stride;
                    byte[] srcRow = new byte[Math.Abs(srcStride)];
                    byte[] dstRow = new byte[Math.Abs(dstStride)];
                    for (int y = 0; y < h; y++)
                    {
                        IntPtr sp = (IntPtr)((long)src.Scan0 + (long)y * srcStride);
                        Marshal.Copy(sp, srcRow, 0, srcRow.Length);
                        for (int x = 0; x < w; x++)
                        {
                            int o = x * 3;
                            int b = srcRow[o];
                            int g = srcRow[o + 1];
                            int r = srcRow[o + 2];
                            dstRow[x] = (byte)(((r >> 5) << 5) | ((g >> 5) << 2) | (b >> 6));
                        }
                        IntPtr dp = (IntPtr)((long)dst.Scan0 + (long)y * dstStride);
                        Marshal.Copy(dstRow, 0, dp, dstRow.Length);
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

        // ---------- JPEG ----------

        private static void SaveJpeg(Image image, string path, int quality)
        {
            File.WriteAllBytes(path, EncodeJpegBytes(image, quality));
        }

        internal static byte[] EncodeJpegBytes(Image image, int quality)
        {
            using (Bitmap canvas = ToRgb24OnWhite(image))
            using (MemoryStream ms = new MemoryStream())
            {
                ImageCodecInfo codec = GetEncoder(ImageFormat.Jpeg);
                if (codec == null)
                {
                    canvas.Save(ms, ImageFormat.Jpeg);
                }
                else
                {
                    using (EncoderParameters parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                        canvas.Save(ms, codec, parameters);
                    }
                }
                return ms.ToArray();
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                if (codec.FormatID == format.Guid) return codec;
            return null;
        }

        // ---------- PNG / BMP ----------

        private static void SavePng(Image image, string path, int level)
        {
            if (level <= 0)
            {
                using (Bitmap indexed = ToIndexed8(image)) indexed.Save(path, ImageFormat.Png);
            }
            else if (level == 1)
            {
                using (Bitmap rgb = ToRgb24OnWhite(image)) rgb.Save(path, ImageFormat.Png);
            }
            else
            {
                using (Bitmap argb = ToArgb32(image)) argb.Save(path, ImageFormat.Png);
            }
        }

        private static void SaveBmp(Image image, string path, int level)
        {
            if (level <= 0)
            {
                using (Bitmap indexed = ToIndexed8(image)) indexed.Save(path, ImageFormat.Bmp);
            }
            else if (level == 1)
            {
                using (Bitmap rgb = ToRgb24OnWhite(image)) rgb.Save(path, ImageFormat.Bmp);
            }
            else
            {
                using (Bitmap argb = ToArgb32OnWhite(image)) argb.Save(path, ImageFormat.Bmp);
            }
        }

        // ---------- ICO ----------

        private static void SaveIco(Image image, string path, int level)
        {
            int size = level <= 0 ? 32 : (level == 1 ? 64 : 256);
            byte[] frame = EncodeIcoDib(image, size);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((ushort)0);   // reserved
                w.Write((ushort)1);   // type: icon
                w.Write((ushort)1);   // image count

                w.Write((byte)(size >= 256 ? 0 : size)); // width
                w.Write((byte)(size >= 256 ? 0 : size)); // height
                w.Write((byte)0);     // color count
                w.Write((byte)0);     // reserved
                w.Write((ushort)1);   // planes
                w.Write((ushort)32);  // bit count
                w.Write((uint)frame.Length);
                w.Write((uint)22);    // data offset: ICONDIR(6) + ICONDIRENTRY(16)
                w.Write(frame);
            }
        }

        // 正方形透明画布，等比居中，输出标准 BMP 位图帧（DIB）。
        // 不能写 PNG 压缩帧：PNG 帧只有部分读取器支持，GDI+（System.Drawing.Icon.ToBitmap
        // 及依赖它的查看器/预览）会解析失败并退回白底；DIB + 32bpp alpha 才能在各环境下
        // 正确保留透明背景。
        private static byte[] EncodeIcoDib(Image image, int size)
        {
            using (Bitmap canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    double scale = Math.Min((double)size / image.Width, (double)size / image.Height);
                    int w = Math.Max(1, (int)Math.Round(image.Width * scale));
                    int h = Math.Max(1, (int)Math.Round(image.Height * scale));
                    g.DrawImage(image, new Rectangle((size - w) / 2, (size - h) / 2, w, h));
                }
                return BuildIcoDib(canvas);
            }
        }

        // BITMAPINFOHEADER + 自下而上的 BGRA 像素 + AND 掩码。
        // 掩码把完全透明的像素置位，不支持 alpha 的旧式查看器也不会把透明区域画成色块。
        private static byte[] BuildIcoDib(Bitmap bmp)
        {
            int size = bmp.Width;
            int maskRowBytes = ((size + 31) / 32) * 4;
            Rectangle rect = new Rectangle(0, 0, size, size);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] row = new byte[Math.Abs(stride)];
                byte[][] maskRows = new byte[size][];

                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter w = new BinaryWriter(ms))
                {
                    w.Write((uint)40);      // biSize
                    w.Write(size);          // biWidth
                    w.Write(size * 2);      // biHeight: XOR + AND
                    w.Write((ushort)1);     // biPlanes
                    w.Write((ushort)32);    // biBitCount
                    w.Write((uint)0);       // biCompression
                    w.Write((uint)(size * size * 4));
                    w.Write(0);             // biXPelsPerMeter
                    w.Write(0);             // biYPelsPerMeter
                    w.Write((uint)0);       // biClrUsed
                    w.Write((uint)0);       // biClrImportant

                    for (int y = size - 1; y >= 0; y--)
                    {
                        Marshal.Copy((IntPtr)((long)data.Scan0 + (long)y * stride), row, 0, row.Length);
                        byte[] maskRow = new byte[maskRowBytes];
                        for (int x = 0; x < size; x++)
                        {
                            if (row[x * 4 + 3] == 0)
                                maskRow[x >> 3] |= (byte)(0x80 >> (x & 7));
                        }
                        maskRows[y] = maskRow;
                        w.Write(row, 0, size * 4);
                    }
                    for (int y = size - 1; y >= 0; y--)
                        w.Write(maskRows[y], 0, maskRowBytes);

                    w.Flush();
                    return ms.ToArray();
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        // ---------- XBM ----------

        private static void SaveXbm(Image image, string path, int level)
        {
            int scale = level >= 2 ? 2 : 1;
            string id = SanitizeIdentifier(Path.GetFileNameWithoutExtension(path));

            using (Bitmap gray = GrayscaleCanvas(image, scale))
            {
                int w = gray.Width;
                int h = gray.Height;
                byte[] lum = ReadLuminance(gray);
                bool[] on = level == 0 ? Threshold(lum) : Dither(lum, w, h);

                int rowBytes = (w + 7) / 8;
                byte[] packed = new byte[rowBytes * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (on[y * w + x])
                            packed[y * rowBytes + (x >> 3)] |= (byte)(1 << (x & 7));

                StringBuilder sb = new StringBuilder();
                sb.Append("#define ").Append(id).Append("_width ").Append(w).Append('\n');
                sb.Append("#define ").Append(id).Append("_height ").Append(h).Append('\n');
                sb.Append("static unsigned char ").Append(id).Append("_bits[] = {\n");
                for (int i = 0; i < packed.Length; i++)
                {
                    if (i % 12 == 0) sb.Append("  ");
                    sb.Append("0x").Append(packed[i].ToString("x2", CultureInfo.InvariantCulture));
                    if (i != packed.Length - 1) sb.Append(',');
                    if (i % 12 == 11 || i == packed.Length - 1) sb.Append('\n');
                    else sb.Append(' ');
                }
                sb.Append("};\n");
                File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
            }
        }

        private static Bitmap GrayscaleCanvas(Image image, int scale)
        {
            int w = image.Width * scale;
            int h = image.Height * scale;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(image, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }

        private static byte[] ReadLuminance(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            byte[] lum = new byte[w * h];
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy((IntPtr)((long)data.Scan0 + (long)y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < w; x++)
                    {
                        int o = x * 3;
                        lum[y * w + x] = (byte)((row[o + 2] * 299 + row[o + 1] * 587 + row[o] * 114) / 1000);
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return lum;
        }

        private static bool[] Threshold(byte[] lum)
        {
            bool[] on = new bool[lum.Length];
            for (int i = 0; i < lum.Length; i++) on[i] = lum[i] < 128;
            return on;
        }

        // Floyd–Steinberg 误差扩散抖动。
        private static bool[] Dither(byte[] lum, int w, int h)
        {
            int[] work = new int[lum.Length];
            for (int i = 0; i < lum.Length; i++) work[i] = lum[i];
            bool[] on = new bool[lum.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    int v = work[i];
                    bool black = v < 128;
                    on[i] = black;
                    int err = v - (black ? 0 : 255);
                    if (x + 1 < w) work[i + 1] += err * 7 / 16;
                    if (y + 1 < h)
                    {
                        int down = i + w;
                        if (x > 0) work[down - 1] += err * 3 / 16;
                        work[down] += err * 5 / 16;
                        if (x + 1 < w) work[down + 1] += err / 16;
                    }
                }
            }
            return on;
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                          (c >= '0' && c <= '9') || c == '_';
                sb.Append(ok ? c : '_');
            }
            if (sb.Length == 0) sb.Append('_');
            if (sb[0] >= '0' && sb[0] <= '9') sb.Insert(0, '_');
            return sb.ToString();
        }

        // ---------- XPM ----------

        private static void SaveXpm(Image image, string path, int level)
        {
            int colorCount;
            int[] colors = BuildXpmPalette(level, out colorCount);
            int cpp = colorCount > 92 ? 2 : 1;
            char[] alphabet = XpmAlphabet();
            byte[] lut = BuildXpmLut(colors);
            string id = SanitizeIdentifier(Path.GetFileNameWithoutExtension(path));

            StringBuilder sb = new StringBuilder();
            sb.Append("/* XPM */\n");
            sb.Append("static char *").Append(id).Append("[] = {\n");

            using (Bitmap rgb = ToRgb24OnWhite(image))
            {
                int w = rgb.Width;
                int h = rgb.Height;

                sb.Append('"').Append(w).Append(' ').Append(h).Append(' ')
                  .Append(colorCount).Append(' ').Append(cpp).Append('"').Append(",\n");

                for (int i = 0; i < colorCount; i++)
                {
                    int c = colors[i];
                    sb.Append('"').Append(Code(alphabet, i, cpp)).Append(" c #")
                      .Append(((c >> 16) & 0xFF).ToString("X2", CultureInfo.InvariantCulture))
                      .Append(((c >> 8) & 0xFF).ToString("X2", CultureInfo.InvariantCulture))
                      .Append((c & 0xFF).ToString("X2", CultureInfo.InvariantCulture))
                      .Append('"');
                    if (i != colorCount - 1) sb.Append(',');
                    sb.Append('\n');
                }

                BitmapData data = rgb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    byte[] row = new byte[Math.Abs(data.Stride)];
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy((IntPtr)((long)data.Scan0 + (long)y * data.Stride), row, 0, row.Length);
                        sb.Append('"');
                        for (int x = 0; x < w; x++)
                        {
                            int o = x * 3;
                            int key = ((row[o + 2] >> 3) << 10) | ((row[o + 1] >> 3) << 5) | (row[o] >> 3);
                            sb.Append(Code(alphabet, lut[key], cpp));
                        }
                        sb.Append('"');
                        if (y != h - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                }
                finally
                {
                    rgb.UnlockBits(data);
                }
            }

            sb.Append("};\n");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        private static char[] XpmAlphabet()
        {
            List<char> list = new List<char>();
            for (int c = 33; c <= 126; c++)
                if (c != '"' && c != '\\') list.Add((char)c);
            return list.ToArray();
        }

        private static string Code(char[] alphabet, int index, int cpp)
        {
            if (cpp == 1) return alphabet[index % alphabet.Length].ToString();
            return alphabet[index / alphabet.Length].ToString() + alphabet[index % alphabet.Length].ToString();
        }

        private static int Rgb(int r, int g, int b)
        {
            return (r << 16) | (g << 8) | b;
        }

        // 普通 64 色(4×4×4) / 高清 128 色(4×8×4) / 超清 216 web-safe + 40 灰。
        private static int[] BuildXpmPalette(int level, out int count)
        {
            List<int> list = new List<int>();
            if (level <= 0)
            {
                for (int r = 0; r < 4; r++)
                    for (int g = 0; g < 4; g++)
                        for (int b = 0; b < 4; b++)
                            list.Add(Rgb(r * 85, g * 85, b * 85));
            }
            else if (level == 1)
            {
                for (int r = 0; r < 4; r++)
                    for (int g = 0; g < 8; g++)
                        for (int b = 0; b < 4; b++)
                            list.Add(Rgb(r * 85, g * 255 / 7, b * 85));
            }
            else
            {
                for (int r = 0; r < 6; r++)
                    for (int g = 0; g < 6; g++)
                        for (int b = 0; b < 6; b++)
                            list.Add(Rgb(r * 51, g * 51, b * 51));
                for (int i = 0; i < 40; i++)
                {
                    int v = (int)Math.Round(i * 255.0 / 39.0);
                    list.Add(Rgb(v, v, v));
                }
            }
            count = list.Count;
            return list.ToArray();
        }

        // 32×32×32 最近色查找表，避免逐像素遍历调色板。
        private static byte[] BuildXpmLut(int[] colors)
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
                        for (int i = 0; i < colors.Length; i++)
                        {
                            int c = colors[i];
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

        // ---------- PDF ----------

        private static void SavePdf(Image image, string path, int level)
        {
            byte[] jpeg = EncodeJpegBytes(image, JpegQuality(level));
            int iw = image.Width;
            int ih = image.Height;

            double pageW = 595.28;
            double pageH = 841.89;
            if (iw > ih)
            {
                double t = pageW;
                pageW = pageH;
                pageH = t;
            }

            double scale = Math.Min(pageW / iw, pageH / ih);
            double dw = iw * scale;
            double dh = ih * scale;
            double x = (pageW - dw) / 2.0;
            double y = (pageH - dh) / 2.0;

            string content = string.Format(CultureInfo.InvariantCulture,
                "q\n{0:0.####} 0 0 {1:0.####} {2:0.####} {3:0.####} cm\n/Im0 Do\nQ\n",
                dw, dh, x, y);
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);

            using (MemoryStream ms = new MemoryStream())
            {
                long[] offsets = new long[6];
                WriteAscii(ms, "%PDF-1.4\n");

                offsets[1] = ms.Position;
                WriteAscii(ms, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                offsets[2] = ms.Position;
                WriteAscii(ms, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

                offsets[3] = ms.Position;
                WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                    "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0:0.####} {1:0.####}] " +
                    "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
                    pageW, pageH));

                offsets[4] = ms.Position;
                WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                    "4 0 obj\n<< /Type /XObject /Subtype /Image /Width {0} /Height {1} " +
                    "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {2} >>\nstream\n",
                    iw, ih, jpeg.Length));
                ms.Write(jpeg, 0, jpeg.Length);
                WriteAscii(ms, "\nendstream\nendobj\n");

                offsets[5] = ms.Position;
                WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                    "5 0 obj\n<< /Length {0} >>\nstream\n", contentBytes.Length));
                ms.Write(contentBytes, 0, contentBytes.Length);
                WriteAscii(ms, "endstream\nendobj\n");

                long xrefPos = ms.Position;
                WriteAscii(ms, "xref\n0 6\n");
                WriteAscii(ms, "0000000000 65535 f \n");
                for (int i = 1; i <= 5; i++)
                    WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                        "{0:0000000000} 00000 n \n", offsets[i]));
                WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                    "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{0}\n%%EOF\n", xrefPos));

                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        private static void WriteAscii(Stream stream, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
