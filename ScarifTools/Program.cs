using System;
using System.Collections.Generic;

namespace ScarifTools;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Too few arguments, expected 2");
            Environment.Exit(1);
        }

        var world = World.Load(args[0]);

        var chunks = new Dictionary<Coord2, Chunk>();

        foreach (var regionId in world.GetRegions())
            AddRange(chunks, world.GetRegion(regionId).Chunks);

        var region = new ScarifStructure(chunks);
        var (numBlocks, fileSize, dictSize, regionSize, compressedRegionSize) = region.Save(args[1]);

        Console.WriteLine($"Total blocks          : {numBlocks:N0}");
        Console.WriteLine($"Total size            : {fileSize:N0} bytes");
        Console.WriteLine($"Dictionary size       : {dictSize:N0} bytes");
        Console.WriteLine($"Metadata overhead size: {fileSize - compressedRegionSize - dictSize:N0} bytes");
        Console.WriteLine($"Region size           : {regionSize:N0} bytes");
        Console.WriteLine($"Compressed region size: {compressedRegionSize:N0} bytes");
        Console.WriteLine($"Region compression    : {compressedRegionSize / (double)regionSize * 100:F3}%");
        Console.WriteLine($"Region bits per block : {compressedRegionSize * 8 / (double)numBlocks:F7}");
        Console.WriteLine($"Blocks per region bits: {(double)numBlocks / (compressedRegionSize * 8):F2}");
    }

    private static void AddRange<T1, T2>(Dictionary<T1, T2> dest, Dictionary<T1, T2> src)
    {
        foreach (var (key, value) in src) dest[key] = value;
    }
}