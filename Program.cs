using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageComparison
{
    internal class Program
    {
        private const bool IncludeAlphaChannel = false; // this may need to be "true" if checking text-documents, in-case of transparent text. 
        
        public static void Main(string[] args)
        {
            const string folder1 = "/home/snorre/bachelor-thesis/Frame Art";
            const string folder2 = "/home/snorre/bachelor-thesis/Frame Art";

            try
            {
                var files1 = Directory.GetFiles(folder1);
                var files2 = Directory.GetFiles(folder2);

                if (files1.Length != files2.Length)
                {
                    Console.WriteLine($"Folders contain different number of images: {files1.Length} vs {files2.Length}");
                    return;
                }

                var watch = Stopwatch.StartNew();
                var similarities = new ConcurrentBag<double>();
                var errorLogs = new ConcurrentBag<string>();
                var processedCount = 0;
                var progressLock = new object();
                int vectorSize = Vector<byte>.Count;
                int componentsPerPixel = IncludeAlphaChannel ? 4 : 3;
                
                // Allowing for multithreading, processes 2 images per 
                // Parallel.For: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-simple-parallel-for-loop
                Parallel.For(0, files1.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                {
                    try
                    {
                        //var watchPerImage = Stopwatch.StartNew(); // For debugging purposes, checking time per picture
                        using var img1 = Image.Load<Rgba32>(files1[i]);
                        using var img2 = Image.Load<Rgba32>(files2[i]);
                        
                        // Pictures have to be the same size to check for comparisons
                        if (img1.Width != img2.Width || img1.Height != img2.Height)
                        {
                            img2.Mutate(x => x.Resize(img1.Size));
                            errorLogs.Add($"Image {Path.GetFileName(files2[i])}: {img2.Width}x{img2.Height} was resized to {img1.Width}x{img1.Height}");
                        }

                        double totalDifference = 0;
                        int pixelCount = img1.Width * img1.Height;
                        
                        // Processing the images efficiently,
                        // both the original and the new image is processed  at the same time. 
                        // Span works on the stack. 
                        // ProcessPixelRows and buffers: https://docs.sixlabors.com/articles/imagesharp/pixelbuffers.html
                        // Span: https://www.codemag.com/Article/1807051/Introducing-.NET-Core-2.1-Flagship-Types-Span-T-and-Memory-T
                        img1.ProcessPixelRows(img2, (img1Accessor, img2Accessor) =>
                        {
                            Span<byte> img1Buffer = stackalloc byte[vectorSize];
                            Span<byte> img2Buffer = stackalloc byte[vectorSize];

                            for (int y = 0; y < img1Accessor.Height; y++)
                            {
                                var img1Row = MemoryMarshal.Cast<Rgba32, byte>(img1Accessor.GetRowSpan(y));
                                var img2Row = MemoryMarshal.Cast<Rgba32, byte>(img2Accessor.GetRowSpan(y));

                                // Process SIMD-able portion. The part that processes both images at the same time. 
                                int x = 0;
                                for (; x <= img1Row.Length - vectorSize; x += vectorSize)
                                {
                                    var slice1 = img1Row.Slice(x, vectorSize);
                                    var slice2 = img2Row.Slice(x, vectorSize);
                                    
                                    slice1.CopyTo(img1Buffer);
                                    slice2.CopyTo(img2Buffer);

                                    var v1 = new Vector<byte>(img1Buffer);
                                    var v2 = new Vector<byte>(img2Buffer);
                                    totalDifference += Vector.Sum(Vector.Abs(v1 - v2));
                                }
                            }
                        });
                        
                        double normalizationFactor = pixelCount * componentsPerPixel;
                        double averageDifference = totalDifference / normalizationFactor;
                        double similarity = 100 - (averageDifference / 255 * 100);
                        similarities.Add(similarity);
                        
                        
                        lock (progressLock)
                        {
                            processedCount++;
                            Console.Write($"\rProcessed {processedCount}/{files1.Length} images ({processedCount * 100 / files1.Length}%)");
                        }
                        //watchPerImage.Stop(); // This is only for debugging purposes. If there is any problems with a specific file 
                        //Console.WriteLine($"Processed {files1[i]} in {watchPerImage.ElapsedMilliseconds} ms.");
                    }
                    catch (Exception ex)
                    {
                        errorLogs.Add($"Error processing {Path.GetFileName(files1[i])} and {Path.GetFileName(files2[i])}: {ex.Message}");
                    }
                });

                Console.WriteLine("\n\nProcessing complete!");
                Console.WriteLine($"Total execution time: {watch.Elapsed}");

                if (!errorLogs.IsEmpty)
                {
                    Console.WriteLine("\nErrors encountered:");
                    foreach (var error in errorLogs)
                    {
                        Console.WriteLine($" - {error}");
                    }
                }

                if (similarities.Count > 0)
                {
                    Console.WriteLine("\nResults:");
                    Console.WriteLine($"Average similarity: {similarities.Average():F2}%");
                    Console.WriteLine($"Highest similarity: {similarities.Max():F2}%");
                    Console.WriteLine($"Lowest similarity: {similarities.Min():F2}%");
                    Console.WriteLine($"Successful comparisons: {similarities.Count}/{files1.Length}");
                }
                else
                {
                    Console.WriteLine("No successful comparisons completed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
        }
    }
}