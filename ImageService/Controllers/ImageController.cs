using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace ImageService.Controllers
{
    /// <summary>
    /// Using local build of image processor nuget package
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly IWebPService _webPService;
        private readonly string _rootPath;

        public ImageController(IWebPService webPService)
        {
            _webPService = webPService;
            _rootPath = AppDomain.CurrentDomain.BaseDirectory;
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName">if requested file is .webp extension, the requesting browser is not Edge, Chrome, or Firefox, then .jpeg file format is returned. </param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get([FromQuery]string fileName)
        {
            var imagesPath = Path.Combine(_rootPath, fileName);
            if (!fileName.ToLower().EndsWith(".webp") || IsWebPSupported())
                return File(System.IO.File.ReadAllBytes(imagesPath), "image/" + Path.GetExtension(fileName).Substring(1));

            using (var fileStream = new FileStream(imagesPath, FileMode.Open))
            {
                var bitmap = _webPService.ToBitmapImage(fileStream);
                //bitmap.Save("C:\\temp\\a.png");
                //return File(System.IO.File.ReadAllBytes("C:\\temp\\a.png"), "image/jpeg");
                var cd = new ContentDisposition
                {
                    FileName = Path.GetFileNameWithoutExtension(fileName) + ".jpg",
                    Inline = true  // false = prompt the user for downloading;  true = browser to try to show the file inline
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());

                return File(GetBytes(bitmap), "image/jpeg");

            }
        }

        [HttpGet("Resize")]
        public IActionResult Resize([FromQuery]int size, [FromQuery] string fileName)
        {
            var imagesPath = Path.Combine(_rootPath, fileName);

            var bitmap = _webPService.Resize(size, imagesPath);
            var cd = new ContentDisposition
            {
                FileName = fileName,
                Inline = true  // false = prompt the user for downloading;  true = browser to try to show the file inline
            };
            Response.Headers.Add("Content-Disposition", cd.ToString());

            return File(bitmap, "image/webp");

        }

        [HttpGet("download")]
        public IActionResult Download()
        {
            if (!Directory.Exists("c:\\temp\\images"))
                Directory.CreateDirectory("c:\\temp\\images");

            var client = new HttpClient();
            System.IO.File.ReadAllLines("images.csv").AsParallel().ForAll(url =>
            {
                var response = client.GetAsync(url).Result;
                var format = Path.GetExtension(url) == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
                Image.FromStream(response.Content.ReadAsStreamAsync().Result).Save($"c:\\temp\\images\\{Path.GetFileName(url)}", format);
            });
            return Ok();
        }



        private IActionResult CreateFileResult(Stream stream, string fileName, string mimeType)
        {
            var cd = new ContentDisposition
            {
                FileName = fileName,
                Inline = true  // false = prompt the user for downloading;  true = browser to try to show the file inline
            };
            Response.Headers.Add("Content-Disposition", cd.ToString());
            return new FileStreamResult(stream, mimeType);
        }


        /// <summary>
        /// upload file, no conversion
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        [HttpPost("upload")]
        public IActionResult Post(IFormFile image)
        {
            SaveImage(image);

            return Created(image.FileName, image.Length, image.Length);
        }

        /// <summary>
        /// Create jpg or png image from uploaded webp image.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="format">supported file extensions are jpg, jpeg, and png, note: don't enter '.'</param>
        /// <returns></returns>
        [HttpPost("Decode")]
        public IActionResult Decode(IFormFile image, [FromQuery] string format)
        {
            var fileName = Path.GetFileNameWithoutExtension(image.FileName) + "." + format;

            CreateBitmapImage(image, Path.Combine(_rootPath, fileName));

            return Created(fileName, new FileInfo(Path.Combine(_rootPath, fileName)).Length, image.Length);
        }

        /// <summary>
        /// Create webp image from uploaded jpg or png image.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="quality">1 to 100, means 1 too 100 percent, no decimals</param>
        /// <returns></returns>
        [HttpPost("Encode")]
        public IActionResult Encode(IFormFile image, [FromQuery] int quality)
        {
            var fileName = Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp";
            //if (IsTransparent(image.OpenReadStream()))
            //{
            //    var b = RemoveAlphaChannel(image.OpenReadStream());

            //    _webPService.CreateWebPImage(GetStream(b), quality, Path.Combine(_rootPath, fileName));
            //}
            //else
            _webPService.CreateWebPImage(image.OpenReadStream(), quality, Path.Combine(_rootPath, fileName));

            return Created(fileName, new FileInfo(Path.Combine(_rootPath, fileName)).Length, image.Length);
        }


        [HttpGet("savings")]
        public IActionResult GetSavings([FromQuery]string bitmapImagePath, [FromQuery]string webPImagePath)
        {
            var webpFiles = new DirectoryInfo(webPImagePath).GetFiles("*.webp", SearchOption.AllDirectories);
            var bitmapImageFiles = new DirectoryInfo(bitmapImagePath).GetFiles("*.jpg", SearchOption.AllDirectories).ToList();
            bitmapImageFiles.AddRange(new DirectoryInfo(bitmapImagePath).GetFiles("*.png", SearchOption.AllDirectories));
            var sb = new StringBuilder();
            sb.AppendLine("filename,dimensions,size,webp size, size reduction");

            bitmapImageFiles.ForEach(s =>
            {
                var fileName = Path.GetFileName(s.Name);
                var webpFileName = Path.ChangeExtension(fileName, ".webp");
                var webpFile = webpFiles.FirstOrDefault(a => a.Name == webpFileName);
                using (var image = Image.FromFile(s.FullName))
                    sb.AppendLine($"{fileName},{image.Width}x{image.Height},{s.Length},{webpFile?.Length}, {((s.Length-webpFile?.Length)*100/s.Length):F2}%");
            });

            return CreateFileResult(sb.ToString(), Path.GetFileName(webPImagePath) + ".csv");
        }

        /// <summary>
        /// Create webp image from uploaded jpg or png image.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="quality">1 to 100, means 1 too 100 percent, no decimals</param>
        /// <returns></returns>
        [HttpPost("xx")]
        public IActionResult Encode2(IFormFile image, [FromQuery] int quality)
        {
            var fileName = Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp";

            var b = RemoveAlphaChannel(image.OpenReadStream());
            b.Save("C:\\temp\\xx.jpg", ImageFormat.Jpeg);
            //_webPService.CreateWebPImage(GetStream(b), quality, Path.Combine(_rootPath, fileName));
            // _webPService.CreateWebPImage(image.OpenReadStream(), quality, Path.Combine(_rootPath, fileName));

            return Created(fileName, new FileInfo(Path.Combine(_rootPath, fileName)).Length, image.Length);
        }

        /// <summary>
        /// 100% quality.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        [HttpPost("Lossless")]
        public IActionResult Lossless(IFormFile image)
        {
            return Encode(image, 100);
        }

        /// <summary>
        /// 40% quality
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        [HttpPost("NearLossless")]
        public IActionResult NearLossless(IFormFile image)
        {
            return Encode(image, 40);
        }

        private IActionResult Created(string fileName, long newSize, long oldSize) => CreatedAtAction(nameof(Get), new { fileName }, new { oldSize, newSize });

        private void CreateBitmapImage(IFormFile image, string filePath)
        {
            var bitmap = _webPService.ToBitmapImage(image.OpenReadStream());
            bitmap.Save(filePath);
        }

        private void SaveImage(IFormFile image)
        {
            using (var normalFileStream = new FileStream(Path.Combine(_rootPath, image.FileName), FileMode.Create))
            {
                image.CopyTo(normalFileStream);
            }
        }


        public static Stream GetStream(Image image)
        {
            var memoryStream = new MemoryStream();

            image.Save(memoryStream, ImageFormat.Jpeg);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        public static Image RemoveAlphaChannel(Stream stream)
        {
            using (var image = Image.FromStream(stream))
            {
                var b = new Bitmap(image.Width, image.Height);

                b.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.DrawImage(image, 0, 0);

                }

                return b;
            }
        }

        public static bool IsTransparent(Stream stream)
        {
            using (var bitmap = new Bitmap(Bitmap.FromStream(stream)))
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                try
                {
                    var buffer = new byte[data.Height * data.Stride];
                    Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                    for (var i = 3; i < buffer.Length; i += 4)
                    {
                        if (buffer[i] != 255) return true;
                    }
                    return false;
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
        }
        private byte[] GetBytes(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private Bitmap Resize(Image image, int width, int height, bool maintainAspectRatio = false, Color? fillColor = null)
        {
            int newWidth = width, newHeight = height;
            if (maintainAspectRatio)
            {
                if (image.Width > image.Height)
                {
                    var ratio = (float)width / image.Width;
                    newHeight = Convert.ToInt32(image.Height * ratio);
                }
                else if (image.Height > image.Width)
                {
                    var ratio = (float)height / image.Height;
                    newWidth = Convert.ToInt32(image.Width * ratio);
                }
            }

            //a holder for the result
            var result = new Bitmap(width, height);

            //use a graphics object to draw the resized image into the bitmap
            using (var graphics = Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                //draw the image into the target bitmap
                if (maintainAspectRatio)
                {
                    float x = 0, y = 0;
                    if (image.Width > image.Height)
                    {
                        y = (height - newHeight) / 2.0f;
                    }
                    else if (image.Height > image.Width)
                    {
                        x = (width - newWidth) / 2.0f;
                    }
                    if (x > 0 || y > 0)
                    {
                        graphics.Clear(fillColor.GetValueOrDefault(Color.White));
                    }
                    graphics.DrawImage(image, x, y, newWidth, newHeight);
                }
                else
                {
                    graphics.DrawImage(image, 0, 0, result.Width, result.Height);
                }
            }

            //return the resulting bitmap
            return result;
        }

        private byte[] GetBytes(Image image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, ImageFormat.Png);
                return memoryStream.ToArray();
            }
        }

        private bool IsWebPSupported()
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            return userAgent.Contains("Edge") || userAgent.Contains("Chrome") || userAgent.Contains("Firefox");
        }

        private IActionResult CreateFileResult(string fileContent, string fileName)
        {
            var cd = new ContentDisposition
            {
                FileName = fileName,
                Inline = false  // false = prompt the user for downloading;  true = browser to try to show the file inline
            };
            Response.Headers.Add("Content-Disposition", cd.ToString());

            return File(Encoding.UTF8.GetBytes(fileContent), "text/csv");
        }
    }
}