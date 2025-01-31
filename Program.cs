using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageComparison
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            List<string> imagePaths = new List<string>
            {
                "/home/snorre/bachelor-test/ImageTest/test.jpg",
                "/home/snorre/bachelor-test/ImageTest/test1.jpeg"
            };

            if (imagePaths.Count < 2)
            {
                Console.WriteLine("Need at least two images to compare.");
                return;
            }

            try
            {
                // Load images
                using Image<Rgba32> img1 = Image.Load<Rgba32>(imagePaths[0]);
                using Image<Rgba32> img2 = Image.Load<Rgba32>(imagePaths[1]);

                // Resize both images to the same small size (e.g., 100x100)
                const int commonWidth = 100;
                const int commonHeight = 100;
                img1.Mutate(x => x.Resize(commonWidth, commonHeight));
                img2.Mutate(x => x.Resize(commonWidth, commonHeight));

                Console.WriteLine($"Resized images to {commonWidth}x{commonHeight}");

                // Calculate similarity score
                double totalDifference = 0;
                const int pixelCount = commonWidth * commonHeight;

                for (var x = 0; x < commonWidth; x++)
                {
                    for (var y = 0; y < commonHeight; y++)
                    {
                        Rgba32 pixel1 = img1[x, y];
                        Rgba32 pixel2 = img2[x, y];

                        double diff = Math.Abs(pixel1.R - pixel2.R) +
                                      Math.Abs(pixel1.G - pixel2.G) +
                                      Math.Abs(pixel1.B - pixel2.B);
                        totalDifference += diff;
                    }
                }

                var averageDifference = totalDifference / (pixelCount * 3);
                var similarity = 100 - (averageDifference / 255 * 100);

                Console.WriteLine($"Image similarity score: {similarity:F2}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
