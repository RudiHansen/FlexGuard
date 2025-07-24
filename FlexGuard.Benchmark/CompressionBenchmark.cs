using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ZstdSharp;

namespace FlexGuard.Benchmark
{
    public static class CompressionBenchmark
    {
        private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".mp3", ".mp4", ".mkv", ".avi", ".gz"
        };

        public static void Run(string rootPath)
        {
            Console.WriteLine($"Starting compression benchmark on: {rootPath}");

            var allFiles = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
            Console.WriteLine($"Total files found: {allFiles.Length}");

            var filteredFiles = FilterFiles(allFiles);

            var results = new List<BenchmarkResult>();

            Console.WriteLine("\n--- Test with ALL files ---");
            results.AddRange(RunAllMethods("All Files", allFiles));

            Console.WriteLine("\n--- Test with ONLY compressible files ---");
            results.AddRange(RunAllMethods("Compressible Files", filteredFiles));

            SaveResultsToCsv(results);
            Console.WriteLine("\nResults written to compression_benchmark_results.csv");
        }

        private static string[] FilterFiles(string[] allFiles)
        {
            var result = new List<string>();
            foreach (var file in allFiles)
            {
                if (!ExcludedExtensions.Contains(Path.GetExtension(file)))
                    result.Add(file);
            }
            Console.WriteLine($"Compressible files (after filtering): {result.Count}");
            return result.ToArray();
        }

        private static List<BenchmarkResult> RunAllMethods(string testSetName, string[] files)
        {
            var results = new List<BenchmarkResult>();

            results.Add(TestMethod("GZip", testSetName, files, CompressWithGZip));
            results.Add(TestMethod("Brotli", testSetName, files, CompressWithBrotli));
            results.Add(TestMethod("Zstd", testSetName, files, CompressWithZstd));

            return results;
        }

        private static BenchmarkResult TestMethod(
            string methodName,
            string testSetName,
            string[] files,
            Func<string[], long> compressMethod)
        {
            Console.WriteLine($"\n[{methodName}] - {testSetName}");
            var stopwatch = Stopwatch.StartNew();
            var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

            long totalCompressed = compressMethod(files);

            stopwatch.Stop();
            var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuTime = cpuAfter - cpuBefore;
            double cpuUsage = (cpuTime.TotalMilliseconds / stopwatch.Elapsed.TotalMilliseconds) * 100;

            Console.WriteLine($"  Total compressed size: {FormatSize(totalCompressed)}");
            Console.WriteLine($"  Wall time: {stopwatch.Elapsed}");
            Console.WriteLine($"  CPU time: {cpuTime}");
            Console.WriteLine($"  Approx CPU usage: {cpuUsage:F1}%");

            return new BenchmarkResult
            {
                Method = methodName,
                TestSet = testSetName,
                CompressedSize = totalCompressed,
                WallTime = stopwatch.Elapsed,
                CpuTime = cpuTime,
                CpuUsage = cpuUsage
            };
        }

        private static long CompressWithGZip(string[] files)
        {
            long totalSize = 0;
            foreach (var file in files)
            {
                try
                {
                    using var input = File.OpenRead(file);
                    using var ms = new MemoryStream();
                    using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        input.CopyTo(gzip);
                    }
                    totalSize += ms.Length;
                }
                catch (IOException)
                {
                    // Skip unreadable files silently
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files without read permissions silently
                }
            }
            return totalSize;
        }

        private static long CompressWithBrotli(string[] files)
        {
            long totalSize = 0;
            foreach (var file in files)
            {
                try
                {
                    using var input = File.OpenRead(file);
                    using var ms = new MemoryStream();
                    using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        input.CopyTo(brotli);
                    }
                    totalSize += ms.Length;
                }
                catch (IOException)
                {
                    // Skip unreadable files silently
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files without read permissions silently
                }
            }
            return totalSize;
        }

        private static long CompressWithZstd(string[] files)
        {
            long totalSize = 0;
            using var compressor = new Compressor(level: 3); // 1=fast, 22=best compression
            foreach (var file in files)
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    var compressed = compressor.Wrap(data);
                    totalSize += compressed.Length;
                }
                catch (IOException)
                {
                    // Skip unreadable files silently
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files without read permissions silently
                }
            }
            return totalSize;
        }
        private static void SaveResultsToCsv(List<BenchmarkResult> results, string baseFileName = "compression_benchmark")
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            string fileName = $"{baseFileName}_{timestamp}.csv";

            using var writer = new StreamWriter(fileName);
            writer.WriteLine("TestSet,Method,CompressedSize,WallTime,CpuTime,CpuUsage");
            foreach (var r in results)
            {
                writer.WriteLine($"{r.TestSet},{r.Method},{r.CompressedSize},{r.WallTime},{r.CpuTime},{r.CpuUsage:F1}%");
            }

            Console.WriteLine($"\nResults written to {fileName}");
        }

        private static string FormatSize(long bytes)
        {
            if (bytes > 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes > 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes > 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }

        private class BenchmarkResult
        {
            public string TestSet { get; set; } = "";
            public string Method { get; set; } = "";
            public long CompressedSize { get; set; }
            public TimeSpan WallTime { get; set; }
            public TimeSpan CpuTime { get; set; }
            public double CpuUsage { get; set; }
        }
    }
}
