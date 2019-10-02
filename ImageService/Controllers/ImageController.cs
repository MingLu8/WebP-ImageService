using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebPWrapper;


namespace ImageService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly string _rootPath;

        public ImageController()
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
            using var webp = new WebP();
            var bitmap = webp.Decode(GetBytes(image.OpenReadStream()));
            bitmap.Save(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + format));
            return Ok();
        }

        [HttpPost("Encode")]
        public IActionResult Encode(IFormFile image)
        {
            using var webp = new WebP();
            var encoded = webp.EncodeLossless(new Bitmap(image.OpenReadStream()));
            using(var webpFileStream = new FileStream(Path.Combine(_rootPath, Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp"), FileMode.Create))
                webpFileStream.Write(encoded, 0, encoded.Length);
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
