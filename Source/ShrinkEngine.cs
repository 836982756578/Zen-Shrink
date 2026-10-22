// Zen Shrink - Görsel Sıkıştırıcı ve Format Dönüştürücü
// Geliştirici: Serkan ŞENTÜRK
// Telif Hakkı © 2026 Serkan ŞENTÜRK. Tüm Hakları Saklıdır.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ZenShrink
{
    public enum TargetFormat
    {
        KeepOriginal,
        JPEG,
        PNG,
        BMP
    }

    public class ShrinkOptions
    {
        public long Quality { get; set; } // 10 - 100
        public int MaxDimension { get; set; } // 0 = original, or 1920, 1280, 1024, 800
        public TargetFormat Format { get; set; }
        public string OutputDirectory { get; set; } // null or empty means same directory
        public string Suffix { get; set; }

        public ShrinkOptions()
        {
            Quality = 80;
            MaxDimension = 0;
            Format = TargetFormat.KeepOriginal;
            Suffix = "_zen";
        }
    }

    public class ShrinkResult
    {
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public long OriginalBytes { get; set; }
        public long CompressedBytes { get; set; }
        public double SavedRatio { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public string GetOriginalFormatted()
        {
            return FormatBytes(OriginalBytes);
        }

        public string GetCompressedFormatted()
        {
            return FormatBytes(CompressedBytes);
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
    }

    public static class ShrinkEngine
    {
        public static readonly string[] SupportedExtensions = new string[]
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif"
        };

        public static bool IsSupported(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            foreach (string s in SupportedExtensions)
            {
                if (ext == s) return true;
            }
            return false;
        }

        public static ShrinkResult ProcessFile(string sourcePath, ShrinkOptions options)
        {
            var res = new ShrinkResult
            {
                SourcePath = sourcePath,
                Success = false
            };

            try
            {
                FileInfo fi = new FileInfo(sourcePath);
                if (!fi.Exists)
                {
                    res.ErrorMessage = "Dosya bulunamadı.";
                    return res;
                }
                res.OriginalBytes = fi.Length;

                // Load image into memory without locking original file
                byte[] raw = File.ReadAllBytes(sourcePath);
                using (MemoryStream ms = new MemoryStream(raw))
                using (Image srcImg = Image.FromStream(ms))
                {
                    int origW = srcImg.Width;
                    int origH = srcImg.Height;

                    int targetW = origW;
                    int targetH = origH;

                    if (options.MaxDimension > 0)
                    {
                        if (origW > options.MaxDimension || origH > options.MaxDimension)
                        {
                            if (origW >= origH)
                            {
                                targetW = options.MaxDimension;
                                targetH = (int)((double)origH * options.MaxDimension / origW);
                            }
                            else
                            {
                                targetH = options.MaxDimension;
                                targetW = (int)((double)origW * options.MaxDimension / origH);
                            }
                        }
                    }

                    if (targetW < 1) targetW = 1;
                    if (targetH < 1) targetH = 1;

                    // Output path determination
                    string outDir = string.IsNullOrEmpty(options.OutputDirectory)
                        ? Path.GetDirectoryName(sourcePath)
                        : options.OutputDirectory;

                    if (!Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }

                    string baseName = Path.GetFileNameWithoutExtension(sourcePath);
                    string targetExt = Path.GetExtension(sourcePath).ToLowerInvariant();

                    ImageFormat saveFormat = srcImg.RawFormat;
                    ImageCodecInfo codec = null;

                    switch (options.Format)
                    {
                        case TargetFormat.JPEG:
                            targetExt = ".jpg";
                            codec = GetEncoder(ImageFormat.Jpeg);
                            break;
                        case TargetFormat.PNG:
                            targetExt = ".png";
                            codec = GetEncoder(ImageFormat.Png);
                            break;
                        case TargetFormat.BMP:
                            targetExt = ".bmp";
                            codec = GetEncoder(ImageFormat.Bmp);
                            break;
                        default:
                            if (targetExt == ".png") codec = GetEncoder(ImageFormat.Png);
                            else if (targetExt == ".bmp") codec = GetEncoder(ImageFormat.Bmp);
                            else { codec = GetEncoder(ImageFormat.Jpeg); targetExt = ".jpg"; }
                            break;
                    }

                    string outFileName = baseName + options.Suffix + targetExt;
                    string outPath = Path.Combine(outDir, outFileName);

                    // Avoid overwriting original if same name
                    if (string.Equals(Path.GetFullPath(outPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                    {
                        outPath = Path.Combine(outDir, baseName + "_zen_compressed" + targetExt);
                    }

                    // Render resampled image
                    using (Bitmap canvas = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(canvas))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;

                            // Fill white background for JPEGs if source has transparency
                            if (codec != null && codec.FormatID == ImageFormat.Jpeg.Guid)
                            {
                                g.Clear(Color.White);
                            }

                            g.DrawImage(srcImg, new Rectangle(0, 0, targetW, targetH), 0, 0, origW, origH, GraphicsUnit.Pixel);
                        }

                        // Save with quality encoder if JPEG
                        if (codec != null && codec.FormatID == ImageFormat.Jpeg.Guid)
                        {
                            EncoderParameters ep = new EncoderParameters(1);
                            ep.Param[0] = new EncoderParameter(Encoder.Quality, options.Quality);
                            canvas.Save(outPath, codec, ep);
                        }
                        else if (codec != null)
                        {
                            canvas.Save(outPath, codec, null);
                        }
                        else
                        {
                            canvas.Save(outPath, ImageFormat.Png);
                        }
                    }

                    FileInfo outFi = new FileInfo(outPath);
                    res.OutputPath = outPath;
                    res.CompressedBytes = outFi.Length;
                    res.Success = true;

                    if (res.OriginalBytes > 0)
                    {
                        double ratio = (1.0 - ((double)res.CompressedBytes / res.OriginalBytes)) * 100.0;
                        res.SavedRatio = ratio;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorMessage = ex.Message;
            }

            return res;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
