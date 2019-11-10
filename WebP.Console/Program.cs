using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using ImageProcessor;
using ImageProcessor.Formats;

namespace WebP.ConsoleApp
{
    class Program
    {

        static void Main(string[] args)
        {
            //args = new List<string>
            //{
            //    "-iC:\\temp\\tiny", "-f91325746.png", "-oC:\\temp\\out-image-tiny-110m42", "-s95", "-t100"
            //}.ToArray();
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
                var result = CreateWebPImage(a.FullName, o.StartingQuality, o.Tolerance);
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

        public static Tuple<int, int, byte[]> CreateWebPImage(string inPath, int quality, int tolerance)
        {
            var bytes = File.ReadAllBytes(inPath);
            using(var ms = new MemoryStream(bytes))
            using (var oriImage = Image.FromStream(ms))
            {
                if (HasTranparency(oriImage))
                {
                    using (var referenceImage = RemoveTransparency(oriImage))
                    {

                        return CreateWebPImage(bytes, referenceImage, quality, tolerance);
                    }
                }
                else
                {
                    return CreateWebPImage(bytes, oriImage, quality, tolerance);
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

        public static Tuple<int, int, byte[]> CreateWebPImage(byte[] bytes, Image referenceImage, int quality, int tolerance)
        {
            //var bitDepth = FormatUtilities.GetSupportedBitDepth(referenceImage.PixelFormat);
            var webp = GetWebP(bytes, quality);
            var diff = 1000;

            while (diff > tolerance && quality < 100)
            {
                if (quality < 70) quality += 10;
                else if (quality >= 70 && quality < 90) quality += 5;
                else if (quality >= 90 && quality < 96) quality += 2;
                else quality++;
                webp = GetWebP(bytes, quality);
                using(var ms = new MemoryStream(webp))
                {
                    using (var temp = new WebPFormat().Load(ms))
                        diff = GetDiff(referenceImage, temp, tolerance);
                }
            }
            return new Tuple<int, int, byte[]>(diff == 1000? 0: diff, quality, webp);
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

        public static byte[] GetWebP(byte[] bytes, int quality)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(bytes);

                    imageFactory.Quality = quality;
                    imageFactory.BackgroundColor(Color.White);
                    imageFactory.Save(memoryStream, new WebPFormat());
                }
                // new WebPFormat().Save(memoryStream, Image.FromFile("C:\\temp\\tiny\\91325746.png"), bitDepth, quality);
                return memoryStream.ToArray();
            }
        }

        public static int GetDiff(Image img1, Image img2, int tolerance)
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

                    if (diff > tolerance) return diff;
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
            return result;
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


        [Option('s', "starting-quality", Required = false, HelpText = "starting conversion quality, if not specified, then 40 percent quality is used. ex: -q 40")]
        public int _startingQuality { get; set; }
        public int StartingQuality => _startingQuality == 0 ? 40 : _startingQuality;

        [Option('m', "max-parrallelism", Required = false, HelpText = "maximium parralellism, if not specified, then max value is used, if set to 0, then processor count x 10 is used. ex: -q 100")]
        public int? _maxParrallelism { get; set; }
        public int? MaxParrallelism => _maxParrallelism.HasValue && _maxParrallelism.Value == 0 ? Environment.ProcessorCount * 10 : _maxParrallelism;

        [Option('t', "tolerant", Required = false, HelpText = "image difference tolerance, valid values are 0 - 765, default 100 . ex: -q 100")]
        public int? _tolerance { get; set; }
        public int Tolerance => _tolerance.HasValue ? _tolerance.Value : 100;

    }

}
