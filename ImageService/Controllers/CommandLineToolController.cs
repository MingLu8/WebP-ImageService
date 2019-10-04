using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ImageService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommandLineToolController
        : ControllerBase
    {
        private readonly string _rootPath;

        public CommandLineToolController()
        {
            _rootPath = AppDomain.CurrentDomain.BaseDirectory;
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName">if requested file is .webp extension, the requesting browser is not Edge, Chrome, or Firefox, then .jpeg file format is returned. </param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            var fileName = "WebPConverter-CommandLine.7z";
            var file = Path.Combine(_rootPath, fileName);

            return File(System.IO.File.ReadAllBytes(file), "APPLICATION/octet-stream", fileName);
        }

    }
}
