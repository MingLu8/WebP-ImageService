using System;
using System.Collections.Generic;
using System.Drawing;
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
            args = new List<string>
            {
                "-iC:\\temp\\tiny", "-f*.jpg|*.png", "-oC:\\temp\\out-tiny3", "-m8", "-s30", "-t100"
            }.ToArray();
            if (args.Length == 0)
            {
                Console.WriteLine("Please execute the run-me.bat file instead this executable directly.");
                Console.Read();
                return;
            }

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

            var files = GetFiles(o.InputFolder, o.FileSearchPatterns.Split('|'), o.SearchOption).ToList();

            if (!files.Any())
            {
                Console.WriteLine($"No files found in the input folder {o.InputFolder}.");
                return;
            }

            var po = new ParallelOptions();
            if (o.MaxParrallelism.HasValue)
                po.MaxDegreeOfParallelism = o.MaxParrallelism.Value;

            Parallel.ForEach(files, po ,(a) =>
            //files.ForEach(a=>
            {
                Console.WriteLine(a);
                var outputPath = Path.GetDirectoryName(a.Replace(o.InputFolder, o.OutputFolder, true, CultureInfo.InvariantCulture));

                var newFilePath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(a) + ".webp");
                Directory.CreateDirectory(outputPath);

                //CreateWebPImage(Image.FromFile(a), quality, newFilePath);
                CreateWebPImage(a, o.StartingQuality, newFilePath, o.Tolerance);
                //CreateWebPImage(Image.FromFile(a));
            });

            Console.WriteLine($"input: {o.InputFolder}, output: {o.OutputFolder}, conversion qulity tolerance: {o.Tolerance}");
        }

        public static void CreateWebPImage(string inPath, int quality, string filePath, int tolerance)
        {
            using (var oriImage = Image.FromFile(inPath))
            {
                using (var image = RemoveTransparency(oriImage))
                {
                    Console.WriteLine("================================================");
                    Console.WriteLine($"Converting {Path.GetFileName(inPath)}.............");

                    CreateWebPImage(image, quality, filePath, tolerance);
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

        public static void CreateWebPImage(Image image, int quality, string file, int tolerance)
        {
            var webp = GetWebP(image, quality);
            var diff = 765;

            while (diff > tolerance && quality < 100)
            {
                if (quality < 70) quality += 10;
                else if (quality >= 70 && quality < 90) quality += 5;
                else if (quality >= 90 && quality < 96) quality += 2;
                else quality++;
                webp = GetWebP(image, quality);
                using(var ms = new MemoryStream(webp))
                {
                    using (var temp = new WebPFormat().Load(ms))
                        diff = GetDiff(image, temp);
                }

                Console.WriteLine($"file:{file}, diff:{diff}, quality:{quality}");
            }

            Console.WriteLine($"file:{file}, diff:{diff}, quality:{quality}");
            File.WriteAllBytes(file, webp);
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

        public static byte[] GetWebP(Image bitmapImage, int quality)
        {
            using (var memoryStream = new MemoryStream())
            {
                new WebPFormat().Save(memoryStream, bitmapImage, FormatUtilities.GetSupportedBitDepth(bitmapImage.PixelFormat), quality);
                return memoryStream.ToArray();
            }
        }

        public static int GetDiff(Image img1, Image img2)
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
                    if (diff > max_diff)
                        max_diff = diff;
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


        public static IEnumerable<string> GetFiles(string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return searchPatterns.AsParallel()
                .SelectMany(searchPattern =>
                    Directory.EnumerateFiles(path, searchPattern, searchOption));
        }

        public static Bitmap RemoveTransparency(Image bitmap)
        {
            var result = new Bitmap(bitmap.Size.Width, bitmap.Size.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            result.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.White);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.DrawImage(bitmap, 0, 0);
            }

            return result;
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
