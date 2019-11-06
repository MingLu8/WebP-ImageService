using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using ImageProcessor;
using ImageProcessor.Plugins.WebP.Imaging.Formats;

namespace WebP.ConsoleApp
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please execute the run-me.bat file instead this executable directly.");
                Console.Read();
                return;
            }

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>((errs) => HandleParseError(errs));
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

            Parallel.ForEach(files, po ,(a) =>
            {
                Console.WriteLine(a);
                var outputPath = Path.GetDirectoryName(a.Replace(o.InputFolder, o.OutputFolder, true, CultureInfo.InvariantCulture));

                var newFilePath = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(a) + ".webp");
                Directory.CreateDirectory(outputPath);

                var quality = o.PngImageQuality != 0 && string.CompareOrdinal(Path.GetExtension(a), ".png") == 0 ? o.PngImageQuality : o.ImageQuality;
                CreateWebPImage(File.OpenRead(a), quality, newFilePath);
            });

            Console.WriteLine($"input: {o.InputFolder}, output: {o.OutputFolder}, conversion qulity: {o.ImageQuality}");
        }

        public static void CreateWebPImage(Stream stream, int quality, string filePath)
        {
            using (var webPFileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(stream)
                        .Format(new WebPFormat())
                        .Quality(quality)
                        .Save(webPFileStream);
                }
            }
        }
        public static IEnumerable<string> GetFiles(string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return searchPatterns.AsParallel()
                .SelectMany(searchPattern =>
                    Directory.EnumerateFiles(path, searchPattern, searchOption));
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


        [Option('q', "quality", Required = false, HelpText = "image quality, if not specified, then 10 percent quality is used. ex: -q 100")]
        public int _imageQuality { get; set; }
        public int ImageQuality => _imageQuality == 0 ? 10 : _imageQuality;

        [Option('p', "png-quality", Required = false, HelpText = "override png image quality, if not specified, then 40 percent quality is used. ex: -q 100")]
        public int _pngImageQuality { get; set; }
        public int PngImageQuality => _pngImageQuality == 0 ? 40 : _pngImageQuality;

        [Option('m', "max-parrallelism", Required = false, HelpText = "maximium parralellism, if not specified, then max value is used, if set to 0, then processor count x 10 is used. ex: -q 100")]
        public int? _maxParrallelism { get; set; }
        public int? MaxParrallelism => _maxParrallelism.HasValue && _maxParrallelism.Value == 0 ? Environment.ProcessorCount * 10 : _maxParrallelism;

    }

}
