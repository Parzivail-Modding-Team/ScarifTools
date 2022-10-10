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
        {
            AddRange(chunks, world.GetRegion(regionId).Chunks);
        }

        var region = new ScarifStructure(chunks);
        var (numBlocks, fileSize) = region.Save(args[1]);

        Console.WriteLine($"Wrote {numBlocks:N} blocks into {fileSize:N} bytes ({(fileSize * 8) / (double)numBlocks:F3} bits/block)");
    }

    private static void AddRange<T1, T2>(Dictionary<T1, T2> dest, Dictionary<T1, T2> src)
    {
        foreach (var (key, value) in src) dest[key] = value;
    }
}