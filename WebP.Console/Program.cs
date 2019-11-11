using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ASI.Core.Imaging;
using CommandLine;
//using ImageProcessor;
//using ImageProcessor.Formats;

namespace WebP.ConsoleApp
{
    class Program
    {
        static IImageConverter Converter = new ImageConverter();
        static void Main(string[] args)
        {
            args = new List<string>
            {
                "-iC:\\temp\\tiny", "-f91325746.png" , "-oC:\\temp\\diff", "-s99", "-t70", "-afalse"
            }.ToArray();
            //if (args.Length == 0)
            //{
            //    Console.WriteLine("Please execute the run-me.bat file instead this executable directly.");
            //    Console.Read();
            //    return;
            //}

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>((errs) => HandleParseError(errs));

            Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            errs.ToList().ForEach(a=> Console.WriteLine(a.Tag.ToString()));
        }

        private static void RunOptionsAndReturnExitCode(Options o)
        {
            if (!Directory.Exists(o.InputFolder))
            {
                Console.WriteLine($"Input directory {o.InputFolder} does not exits.");
                return;
            }

            var files = GetFiles(o.InputFolder, o.FileSearchPatterns.Split('|'), o.SearchOption);

            if (!files.Any())
            {
                Console.WriteLine($"No files found in the input folder {o.InputFolder}.");
                return;
            }

            var po = new ParallelOptions();
            if (o.MaxParrallelism.HasValue)
                po.MaxDegreeOfParallelism = o.MaxParrallelism.Value;

            var startTime = DateTime.Now;
            var newTotalSize = 0;
            var badFiles = new Dictionary<FileInfo, Tuple<int, int, byte[]>>();

            Parallel.ForEach(files, po, (a) =>
            //files.ForEach(a=>
            {
                var outputPath = Path.GetDirectoryName(a.FullName.Replace(o.InputFolder, o.OutputFolder, true, CultureInfo.InvariantCulture));

                var newFilePath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(a.Name) + ".webp");
                Directory.CreateDirectory(outputPath);

                //CreateWebPImage(Image.FromFile(a), quality, newFilePath);
                var result = CreateWebPImage(a.FullName, o.StartingQuality, o.Tolerance, o.Lossless || o.StartingQuality == 100 ? 100 : 99, o.AllowAlphaChannel);
                newTotalSize += result.Item3.Length;
                var reduction = (a.Length - result.Item3.Length) * 100 / a.Length;
                if (reduction < 0)
                    badFiles.Add(a, result);
                Console.WriteLine($"file:{a.FullName}, diff:{result.Item1}, quality:{result.Item2}, size: {a.Length}, new size: {result.Item3.Length}, reduction: {reduction}%");
                File.WriteAllBytes(newFilePath, result.Item3);
                //CreateWebPImage(Image.FromFile(a));
            });
            var totalSize = files.Sum(s => s.Length);
            var totalReduction = (totalSize - newTotalSize) * 100 / totalSize;
            Console.WriteLine();
            Console.WriteLine($"Total of {files.Count()} files completed in {(int)DateTime.Now.Subtract(startTime).TotalSeconds} seconds.");
            Console.WriteLine($"Total Size: {files.Sum(s => s.Length)}, new total size: {newTotalSize}, reduction: {totalReduction}%");
            Console.WriteLine($"input: {o.InputFolder}, files: {o.FileSearchPatterns}, output: {o.OutputFolder}, conversion quality tolerance: {o.Tolerance}.");
            Console.WriteLine();
            Console.WriteLine("negative reduction files:");
            badFiles.Keys.ToList().ForEach(a =>
            {
                Console.WriteLine($"file:{a.FullName}, diff:{badFiles[a].Item1}, quality:{badFiles[a].Item2}, size: {a.Length}, new size: {badFiles[a].Item3.Length}.");
            });
        }

        public static Tuple<int, int, byte[]> CreateWebPImage(string inPath, int quality, int tolerance, int maxQuality, bool allowAlphaChannel)
        {
            var bytes = File.ReadAllBytes(inPath);
            using(var ms = new MemoryStream(bytes))
            using (var oriImage = Image.FromStream(ms))
            {
                if (!allowAlphaChannel && HasTranparency(oriImage))
                {
                    using (var referenceImage = RemoveTransparency(oriImage))
                    {

                        return CreateWebPImage(bytes, referenceImage, quality, tolerance, maxQuality, allowAlphaChannel);
                    }
                }
                else
                {
                    return CreateWebPImage(bytes, oriImage, quality, tolerance, maxQuality, allowAlphaChannel);
                }
            }


            // var bitmap = new WebPFormat().Load(File.OpenRead(filePath)) as Bitmap;
            // var resultPath = $"C:\\temp\\result{Path.GetExtension(inPath)}";
            // //ImageUtility.ToGrayScale(bitmap).Save(resultPath);

            // bitmap.Save(resultPath);
            // image.Save($"C:\\temp\\original{Path.GetExtension(inPath)}");
            // //resultPath = $"C:\\temp\\xx.jpg";
            //// var diff = ImageTool.GetPercentageDifference(inPath, resultPath, 0);
            // var diff = GetDiff(image, bitmap);
            // Console.WriteLine($"{Path.GetFileName(inPath)} {diff}");
        }

        public static bool HasTranparency(Image image)
        {
            if (image.RawFormat.Guid == ImageFormat.Jpeg.Guid) return false;

            var bmp = (Bitmap)image;
            for (var x = 0; x < bmp.Width; x++)
            {
                for (var y = 0; y < bmp.Height; y++)
                {
                    if (bmp.GetPixel(x, y).A != 255) return true;
                }
            }
            return false;
        }

        public static Tuple<int, int, byte[]> CreateWebPImage(byte[] bytes, Image referenceImage, int quality, int tolerance, int maxQuality, bool allowAlphaChannel)
        {
            //var bitDepth = FormatUtilities.GetSupportedBitDepth(referenceImage.PixelFormat);
            byte[] webp = null;
            byte[] previousWebp = null;
            int previousQuality = 0;
            var diff = 1000;
            //if (quality == 100)
            //{
            //    webp = GetWebP(bytes, quality);
            //    using (var ms = new MemoryStream(webp))
            //    {
            //        using (var temp = new WebPFormat().Load(ms))
            //            diff = GetDiff(referenceImage, temp, tolerance);
            //    }
            //    return new Tuple<int, int, byte[]>(diff, quality, webp);
            //}


            while (diff > tolerance && quality <= maxQuality)
            {
                webp = GetWebP(bytes, quality, allowAlphaChannel);
                if(webp.Length >= bytes.Length && previousWebp != null && webp.Length > previousWebp.Length)
                {

                    using (var temp = Converter.GetImage(Converter.ConvertToBitmap(previousWebp, Converter.GetImageFormat(referenceImage))))
                        diff = GetDiff(referenceImage, temp, tolerance, 99);

                    webp = previousWebp;
                    quality = previousQuality;
                    break;
                }

                previousWebp = webp;
                previousQuality = quality;

                using (var temp = Converter.GetImage(Converter.ConvertToBitmap(webp, Converter.GetImageFormat(referenceImage))))
                    diff = quality == 100 ? 0 : GetDiff(referenceImage, temp, tolerance, quality);

                if (quality == maxQuality) break;

                if (quality < 70) quality += 10;
                else if (quality >= 70 && quality < 90) quality += 5;
                else if (quality >= 90 && quality < 96) quality += 2;
                else quality++;
            }
            return new Tuple<int, int, byte[]>(diff, quality, webp);
        }

        //public static byte[] GetWebP(Image image, int quality)
        //{
        //    using (var webPFileStream = new MemoryStream())
        //    {
        //        using (var imageFactory = new ImageFactory())
        //        {
        //            imageFactory.Load(image);
        //            imageFactory.Quality = quality;
        //            imageFactory.BackgroundColor(Color.White);
        //            imageFactory.Save(webPFileStream);
        //        }

        //        return webPFileStream.ToArray();
        //    }
        //}

        public static byte[] GetWebP(byte[] bytes, int quality, bool allowAlphaChannel)
        {
            //using (var webp = new WebPWrapper.WebP())
            //{
            //    return webp.EncodeLossless((Bitmap)Image.FromStream(new MemoryStream(bytes)), 9);
            //}
            return Converter.ConvertToWebP(bytes, quality, !allowAlphaChannel);
            //using (var memoryStream = new MemoryStream())
            //{
            //    using (var imageFactory = new ImageFactory())
            //    {
            //        imageFactory.Load(bytes);

            //        imageFactory.Quality = quality;
            //        //imageFactory.Alpha(0);
            //        if(!allowAlphaChannel)
            //            imageFactory.BackgroundColor(Color.White);
            //        imageFactory.Save(memoryStream, new WebPFormat());
            //    }
            //    // new WebPFormat().Save(memoryStream, Image.FromFile("C:\\temp\\tiny\\91325746.png"), bitDepth, quality);
            //    return memoryStream.ToArray();
            //}
        }

        public static int GetDiff(Image img1, Image img2, int tolerance, int quality)
        {
            var bm1 = (Bitmap)img1;
            var bm2 = (Bitmap)img2;
            var max_diff = 0;

            for (var x = 0; x < img1.Width; x++)
            {
                for (var y = 0; y < img1.Height; y++)
                {
                    // Calculate the pixels' difference.
                    var color1 = bm1.GetPixel(x, y);
                    var color2 = bm2.GetPixel(x, y);
                    var diff =
                        Math.Abs(color1.R - color2.R) +
                        Math.Abs(color1.G - color2.G) +
                        Math.Abs(color1.B - color2.B);

                    if (diff > tolerance && quality != 99) return diff;
                    if (diff > max_diff) max_diff = diff;
                }
            }

            return max_diff;
        }

        public static Bitmap MakeGrayscale3(Bitmap original)
        {
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);

            //create the grayscale ColorMatrix
            ColorMatrix colorMatrix = new ColorMatrix(
               new float[][]
               {
         new float[] {.3f, .3f, .3f, 0, 0},
         new float[] {.59f, .59f, .59f, 0, 0},
         new float[] {.11f, .11f, .11f, 0, 0},
         new float[] {0, 0, 0, 1, 0},
         new float[] {0, 0, 0, 0, 1}
               });

            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();

            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);

            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);

            //dispose the Graphics object
            g.Dispose();
            return newBitmap;
        }

        public static int GetDiff2(Image img1, Image img2, int tolerance, int quality)
        {
            var bm1 = (Bitmap)img1;
            var bm2 = (Bitmap)img2;

            var max_diff = 0;
            for (var x = 0; x < img1.Width; x++)
            {
                for (var y = 0; y < img1.Height; y++)
                {
                    // Calculate the pixels' difference.
                    var color1 = bm1.GetPixel(x, y);
                    var color2 = bm2.GetPixel(x, y);
                    var diff =
                        Math.Abs(color1.R - color2.R) +
                        Math.Abs(color1.G - color2.G) +
                        Math.Abs(color1.B - color2.B);

                    if (diff > tolerance && quality != 99) return diff;
                    if (diff > max_diff) max_diff = diff;
                }
            }

            return max_diff;
        }
        //public static void CreateWebPImage(Image image, int quality, string filePath)
        //{
        //    using (var webPFileStream = new FileStream(filePath, FileMode.Create))
        //    {
        //        using (var imageFactory = new ImageFactory())
        //        {
        //            imageFactory.Load(image)
        //                .Format(new WebPFormat())
        //                .Quality(quality)
        //                .Save(webPFileStream);
        //        }
        //    }
        //}

        //public static void CreateWebPImage(Image image)
        //{
        //    //var originalGrayScaleBitmap = ImageUtility.ToGrayScale((Bitmap)image);
        //    using (var webPFileStream = new MemoryStream())
        //    {
        //        using (var imageFactory = new ImageFactory())
        //        {
        //            imageFactory.Load(image)
        //                .Format(new WebPFormat())
        //                .Quality(1)
        //                .Save(webPFileStream);
        //        }

        //        var bitmap = new WebPFormat().Load(webPFileStream) as Bitmap;
        //        //var grayScale = ImageUtility.ToGrayScale(bitmap);
        //        var diff = image.PercentageDifference(bitmap, 3);

        //    }
        //}


        public static IEnumerable<FileInfo> GetFiles(string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var directionInfo = new DirectoryInfo(path);
            return searchPatterns.AsParallel()
                .SelectMany(searchPattern =>
                    directionInfo.EnumerateFiles(searchPattern, searchOption));
        }

        public static Bitmap RemoveTransparency(Image bitmap)
        {
            var result = new Bitmap(bitmap.Size.Width, bitmap.Size.Height, PixelFormat.Format24bppRgb);

            result.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.White);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.DrawImage(bitmap, 0, 0);
            }

            using (var ms = new MemoryStream())
            {
                result.Save(ms, bitmap.RawFormat);
                return Image.FromStream(ms) as Bitmap;
            }
        }

        public static byte[] GetBytes(Image image)
        {
            using(var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Bmp);
                return ms.ToArray();
            }

        }
    }

    class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input folder. ex: -i \"c:\\temp\\images\"")]
        public string _inputFolder { get; set; }

        public string InputFolder => _inputFolder;

        [Option('f', "file", Required = false, HelpText = "file search patterns. ex: -f \"*.jpg|*.png\"")]
        public string _fileSearchPattern { get; set; }
        public string FileSearchPatterns => string.IsNullOrEmpty(_fileSearchPattern) ? "*.jpg|*.jpeg|*.png" : _fileSearchPattern;

        [Option('r', "recurrsive", Required = false, HelpText = "include all child directories or just input directory. ex: -r true")]
        public bool _recurrsive { get; set; }
        public SearchOption SearchOption => _recurrsive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        [Option('o', "output", Required = false, HelpText = "Output folder, if not specified, then it the same as the input folder. ex: -o \"c:\\temp\\out\"")]
        public string _outputFolder { get; set; }
        public string OutputFolder => string.IsNullOrEmpty(_outputFolder) ? InputFolder : _outputFolder;


        [Option('s', "starting-quality", Required = false, HelpText = "starting conversion quality, if not specified, then 40 percent quality is used. ex: -s 40")]
        public int _startingQuality { get; set; }
        public int StartingQuality => _startingQuality == 0 ? 40 : _startingQuality;

        [Option('m', "max-parrallelism", Required = false, HelpText = "maximium parralellism, if not specified, then max value is used, if set to 0, then processor count x 10 is used. ex: -m 10")]
        public int? _maxParrallelism { get; set; }
        public int? MaxParrallelism => _maxParrallelism.HasValue && _maxParrallelism.Value == 0 ? Environment.ProcessorCount * 10 : _maxParrallelism;

        [Option('t', "tolerant", Required = false, HelpText = "image difference tolerance, valid values are 0 - 765, default 100 . ex: -t 100")]
        public int? _tolerance { get; set; }
        public int Tolerance => _tolerance.HasValue ? _tolerance.Value : 100;

        [Option('l', "lossless", Required = false, HelpText = "when tolerance level could not be achieved, a false value would stop at 99% quality, a true value would stop at 100%, default is true. ex: -l false")]
        public bool? _lossless { get; set; }
        public bool Lossless => _lossless.HasValue ? _lossless.Value : true;

        [Option('a', "alpha", Required = false, HelpText = "true to allow alpha channel, false to remove alpha and add white background. ex: -a false")]
        public bool? _allowAlphaChannel { get; set; }
        public bool AllowAlphaChannel => _allowAlphaChannel.HasValue ? _allowAlphaChannel.Value : false;

    }

}
