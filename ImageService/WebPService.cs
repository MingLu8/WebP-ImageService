using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ImageProcessor;
using ImageProcessor.Formats;
using ImageProcessor.Processing;
//using ImageProcessor.Plugins.WebP.Imaging.Formats;

namespace ImageService
{
    public interface IWebPService
    {
        void CreateWebPImage(Stream stream, int quality, string filePath);
        Image ToWebPImage(Stream imageStream, int quality);
        Image ToBitmapImage(Stream stream);
        byte[] Resize(int size, string filePath);
    }

    public class WebPService : IWebPService
    {
        public byte[] Resize(int size, string filePath)
        {
            using (var webPFileStream = new MemoryStream())
            {
                var s = GetStream(new WebPFormat().Load(File.OpenRead(filePath)));
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(s);
                    imageFactory.Resize(size, size, ResizeMode.Max);
                    imageFactory.Save(webPFileStream, new WebPFormat());
                }
                return webPFileStream.ToArray();
            }
        }

        public static Stream GetStream(Image image)
        {
            var memoryStream = new MemoryStream();

            image.Save(memoryStream, ImageFormat.Jpeg);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        public void CreateWebPImage(Stream stream, int quality, string filePath)
        {
            using (var webPFileStream = new FileStream(filePath, FileMode.Create))
                using (var imageFactory = CreateImageFactory(stream, quality))
                    imageFactory.Save(webPFileStream, new WebPFormat());
        }

        public Image ToWebPImage(Stream imageStream, int quality)
        {
            using (var webPStream = new MemoryStream())
            {
                using (var imageFactory = CreateImageFactory(imageStream, quality))
                    imageFactory.Save(webPStream, new WebPFormat());

                return Image.FromStream(webPStream);
            }
        }

        private ImageFactory CreateImageFactory(Stream stream, int quality)
        {
            var imageFactory = new ImageFactory();
            imageFactory.Load(stream);
            imageFactory.Quality = quality;
            imageFactory.BackgroundColor(Color.White);
            return imageFactory;
        }


        public Image ToBitmapImage(Stream stream)
        {
            return new WebPFormat().Load(stream);
        }

    }
}
