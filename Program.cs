using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageComparison
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            string folder1 = "/home/snorre/bachelor-test/ImageTest/Frame Art"; // First set of images
            string folder2 = "/home/snorre/bachelor-test/ImageTest/Frame Art"; // Second set of images

            var files1 = Directory.GetFiles(folder1);
            var files2 = Directory.GetFiles(folder2);

            if (files1.Length != files2.Length)
            {
                Console.WriteLine("Folders contain a different number of images!");
                return;
            }

            try
            {
                var watch = Stopwatch.StartNew();

                var similarities = new ConcurrentBag<double>(); // Thread-safe collection
                int imagePairs = files1.Length;

                Parallel.For(0, imagePairs, i =>
                {
                    using Image<Rgba32> img1 = Image.Load<Rgba32>(files1[i]);
                    using Image<Rgba32> img2 = Image.Load<Rgba32>(files2[i]);

                    int width = img1.Width;
                    int height = img1.Height;

                    if (width != img2.Width || height != img2.Height)
                    {
                        img2.Mutate(x => x.Resize(width, height));
                    }

                    double totalDifference = 0;
                    int pixelCount = width * height;

                    img1.ProcessPixelRows(img2, (rows1, rows2) =>
                    {
                        for (int y = 0; y < rows1.Height; y++)
                        {
                            Span<Rgba32> row1 = rows1.GetRowSpan(y);
                            Span<Rgba32> row2 = rows2.GetRowSpan(y);

                            for (int x = 0; x < row1.Length; x++)
                            {
                                Rgba32 pixel1 = row1[x];
                                Rgba32 pixel2 = row2[x];

                                totalDifference += Math.Abs(pixel1.R - pixel2.R) +
                                                   Math.Abs(pixel1.G - pixel2.G) +
                                                   Math.Abs(pixel1.B - pixel2.B);
                            }
                        }
                    });

                    double averageDifference = totalDifference / (pixelCount * 3);
                    double similarity = 100 - (averageDifference / 255 * 100);
                    similarities.Add(similarity);
                });

                watch.Stop();
                double averageSimilarity = similarities.Count > 0 ? similarities.Average() : 0;

                Console.WriteLine($"Total processing time: {watch.ElapsedMilliseconds} ms");
                Console.WriteLine($"Average similarity: {averageSimilarity:F2}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
