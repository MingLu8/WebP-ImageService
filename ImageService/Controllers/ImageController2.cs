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

        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName">if requested file is .webp extension, the requesting browser is not Edge, Chrome, or Firefox, then .jpeg file format is returned. </param>
        /// <returns></returns>
        [HttpGet("{fileName}")]
        public IActionResult Get(string fileName)
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

        [HttpPost("upload")]
        public IActionResult Post(IFormFile image)
        {
            using (FileStream normalFileStream = new FileStream(Path.Combine(_rootPath, image.FileName), FileMode.Create))
            {
                image.CopyTo(normalFileStream);
            }

            return Created(nameof(Get), new { fileName = image.FileName });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="image"></param>
        /// <param name="format">supported file extensions are jpg, jpeg, and png, note: don't enter '.'</param>
        /// <returns></returns>
        [HttpPost("Decode")]
        public IActionResult Decode(IFormFile image, [FromQuery] string format)
        {
            var fileName = Path.GetFileNameWithoutExtension(image.FileName) + "." + format;
            var decoded = new WebPFormat().Load(image.OpenReadStream());
            decoded.Save(Path.Combine(_rootPath, fileName));

            return Created(nameof(Get), new { fileName });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="image"></param>
        /// <param name="quality">1 to 100, means 1 too 100 percent, no decimals</param>
        /// <returns></returns>
        [HttpPost("Encode")]
        public IActionResult Encode(IFormFile image, [FromQuery] int quality)
        {
            var fileName = Path.GetFileNameWithoutExtension(image.FileName) + "." + "webp";
            using (FileStream webPFileStream = new FileStream(Path.Combine(_rootPath, fileName), FileMode.Create))
            {
                using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                {
                    imageFactory.Load(image.OpenReadStream())
                        .Format(new WebPFormat())
                        .Quality(quality)
                        .Save(webPFileStream);
                }
            }

            return Created(nameof(Get), new { fileName });
        }

        [HttpPost("Lossless")]
        public IActionResult Lossless(IFormFile image)
        {
            return Encode(image, 10);
        }

        [HttpPost("NearLossless")]
        public IActionResult NearLossless(IFormFile image)
        {
            return Encode(image, 50);
        }

        private IActionResult Created(string actionName, object routeValues) => CreatedAtAction(actionName, routeValues, null);
    }
}
