using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ImageProcessor;
using ImageProcessor.Plugins.WebP.Imaging.Formats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebPWrapper;


namespace ImageService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ImageController2 : ControllerBase
    {
        private readonly string _rootPath;

        public ImageController2()
        {
            _rootPath = AppDomain.CurrentDomain.BaseDirectory;
        }

        [HttpGet]
        public IActionResult Get([FromQuery ]string fileName)
        {
            var imagesPath = Path.Combine(_rootPath, fileName);
            var b = System.IO.File.ReadAllBytes(imagesPath);

            if (!fileName.ToLower().EndsWith(".webp"))
                return File(b, "image/" + Path.GetExtension(fileName).Substring(1));


            var userAgent = Request.Headers["User-Agent"];
            if ( userAgent.Contains("Edge") || userAgent.Contains("Chrome") || userAgent.Contains("Firefox"))
                return File(b, "image/webp");

            using var webp = new WebP();
            var bitmap = webp.Decode(b);
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Jpeg);
            return File(memoryStream.ToArray(), "image/jpeg");
        }

        [HttpPost]
        public IActionResult Post(IFormFile image)
        {
            using (FileStream normalFileStream = new FileStream(Path.Combine(_rootPath, image.FileName), FileMode.Create))
            {
                image.CopyTo(normalFileStream);
            }

            return Ok();
        }

        [HttpPost("Decode")]
        public IActionResult Decode(IFormFile image, [FromQuery] string format)
        {
            var decoded = new WebPFormat().Load(image.OpenReadStream());
            decoded.Save(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + format));

            return Ok();
        }

        [HttpPost("Encode")]
        public IActionResult Encode(IFormFile image, [FromQuery] int quality)
        {
            using (FileStream webPFileStream = new FileStream(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp"), FileMode.Create))
            {
                using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                {
                    imageFactory.Load(image.OpenReadStream())
                        .Format(new WebPFormat())
                        .Quality(quality)
                        .Save(webPFileStream);
                }
            }

            return Ok();
        }

        [HttpPost("Lossless")]
        public IActionResult Lossless(IFormFile image)
        {
            using (FileStream webPFileStream = new FileStream(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp"), FileMode.Create))
            {
                using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                {
                    imageFactory.Load(image.OpenReadStream())
                        .Format(new WebPFormat())
                        .Quality(100)
                        .Save(webPFileStream);
                }
            }

            return Ok();
        }

        [HttpPost("NearLossless")]
        public IActionResult NearLossless(IFormFile image)
        {
            using (FileStream webPFileStream = new FileStream(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp"), FileMode.Create))
            {
                using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                {
                    imageFactory.Load(image.OpenReadStream())
                        .Format(new WebPFormat())
                        .Quality(50)
                        .Save(webPFileStream);
                }
            }

            return Ok();
        }

        private byte[] GetBytes(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
