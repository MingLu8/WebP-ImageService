using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ASI.Barista.Plugins.Imaging.Similarity
{
    internal static class ImageUtility
    {
        public static int[] ToArray(this Bitmap image)
        {
            var data = new int[image.Width * image.Height];

            for (var x = 0; x < image.Width; x++)
            {
                for (var y = 0; y < image.Height; y++)
                {
                    var pixel = image.GetPixel(x, y);
                    data[y * image.Width + x] = pixel.R | pixel.G | pixel.B;
                }
            }

            return data;
        }

        public static Color ToGrayScaleColor(Color color)
        {
            var level = (byte)((color.R + color.G + color.B) / 3);
            var result = Color.FromArgb(level, level, level);
            return result;
        }

        public static Bitmap ToGrayScale2(this Bitmap image)
        {
            var result = new Bitmap(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var grayColor = ToGrayScaleColor(image.GetPixel(x, y));
                    result.SetPixel(x, y, grayColor);
                }
            }
            return result;
        }

        public static Bitmap ToGrayScale(this Bitmap original)
        {
            //create a blank bitmap the same size as original
            var bitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            using (var graphic = Graphics.FromImage(bitmap))
            {
                //create the grayscale ColorMatrix
                var colorMatrix = new ColorMatrix(new[]
                {
                    new[] { .3f, .3f, .3f, 0, 0 },
                    new[] { .59f, .59f, .59f, 0, 0 },
                    new[] { .11f, .11f, .11f, 0, 0 },
                    new[] { 0f, 0, 0, 1, 0 },
                    new[] { 0f, 0, 0, 0, 1 }
                });

                //create some image attributes
                var attributes = new ImageAttributes();

                //set the color matrix attribute
                attributes.SetColorMatrix(colorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                graphic.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }

            return bitmap;
        }

        public static Bitmap Resize(this Image image, int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using (var graphic = Graphics.FromImage(bitmap))
            {
                graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = CompositingQuality.HighQuality;
                graphic.DrawImage(image, 0, 0, width, height);
            }
            return bitmap;
        }
    }
}