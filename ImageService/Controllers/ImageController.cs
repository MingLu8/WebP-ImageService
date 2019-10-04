using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
                return File(GetBytes(bitmap), "image/png");
            }
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

                _webPService.CreateWebPImage(image.OpenReadStream(), quality, Path.Combine(_rootPath, fileName));

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
                using (FileStream normalFileStream = new FileStream(Path.Combine(_rootPath, image.FileName), FileMode.Create))
                {
                    image.CopyTo(normalFileStream);
                }
            }

            private byte[] GetBytes(Image image)
            {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, ImageFormat.Png);
                return memoryStream.ToArray();
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

            private bool IsWebPSupported()
            {
                var userAgent = Request.Headers["User-Agent"].ToString();
                return userAgent.Contains("Edge") || userAgent.Contains("Chrome") || userAgent.Contains("Firefox");
            }
        }
    }