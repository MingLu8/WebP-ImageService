using System.Drawing;
using System.IO;
using ImageProcessor;
using ImageProcessor.Plugins.WebP.Imaging.Formats;

namespace ImageService
{
    public interface IWebPService
    {
        void CreateWebPImage(Stream stream, int quality, string filePath);
        Image ToWebPImage(Stream imageStream, int quality);
        Image ToBitmapImage(Stream stream);
    }

    public class WebPService : IWebPService
    {
        public void CreateWebPImage(Stream stream, int quality, string filePath)
        {
            using (FileStream webPFileStream = new FileStream(filePath, FileMode.Create))
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(stream)
                        .Format(new WebPFormat())
                        .Quality(quality)
                        .Save(webPFileStream);
                }
            }
        }

        public Image ToWebPImage(Stream imageStream, int quality)
        {
            using (var webPStream = new MemoryStream())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(imageStream)
                        .Format(new WebPFormat())
                        .Quality(quality)
                        .Save(webPStream);
                }

                return Image.FromStream(webPStream);
            }
        }


        public Image ToBitmapImage(Stream stream)
        {
            return new WebPFormat().Load(stream);
        }

    }
}
