﻿namespace FlexGuard.Benchmark
{
    internal class Program
    {
        static void Main()
        {
            string testFolder = @"C:\Users\RSH\OneDrive"; // Ret hvis du vil teste noget andet
            CompressionBenchmark.Run(testFolder);
        }
    }
}
