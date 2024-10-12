using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StronglyTypedIds;

namespace ScarifTools;

[StronglyTypedId(Template.String)]
public readonly partial struct Biome;

[StronglyTypedId(Template.String)]
public readonly partial struct BlockId;

public interface IDataset
{
	public void Insert(Coord3 position, Biome biome, BlockId block);

	public void Write(StreamWriter file);
}

public class HierarchicalDataset : IDataset
{
	private readonly ConcurrentDictionary<Biome, ConcurrentDictionary<int, ConcurrentDictionary<BlockId, int>>> _blocks = new();
		
	public void Insert(Coord3 position, Biome biome, BlockId block)
	{
		if (block.Value is "minecraft:air" or "minecraft:cave_air")
			return;
		
		if (!_blocks.ContainsKey(biome))
			_blocks[biome] = new ConcurrentDictionary<int, ConcurrentDictionary<BlockId, int>>();

		if (!_blocks[biome].ContainsKey(position.Y))
			_blocks[biome][position.Y] = new ConcurrentDictionary<BlockId, int>();

		if (!_blocks[biome][position.Y].ContainsKey(block))
			_blocks[biome][position.Y][block] = 0;
		
		_blocks[biome][position.Y][block]++;
	}

	public void Write(StreamWriter file)
	{
		foreach (var (biome, levels) in _blocks)
		{
			foreach (var (level, histogram) in levels)
			{
				foreach (var (block, count) in histogram)
				{
					file.WriteLine($"{biome},{level},{block},{count}");
				}
			}
		}
	}
}

class Program
{
	static void Main(string[] args)
	{
		if (args.Length < 1)
		{
			Console.Error.WriteLine("Too few arguments, expected 1");
			Environment.Exit(1);
		}

		var dataset = new HierarchicalDataset();

		var world = World.Load(args[0]);

		var chunkCount = 0;
		var regionCount = 0;
		
		var regions = world.GetRegions();
		var totalRegions = regions.Count;

		var writeLock = new object();

		// foreach (var regionId in regions)
		Parallel.ForEach(regions, new ParallelOptions()
			{
				MaxDegreeOfParallelism = 12
			}, regionId =>
			{
				var region = world.GetRegion(regionId, false);
				if (region != null)
				{
					foreach (var (coord, chunk) in region.Chunks)
						AnalyzeChunk(dataset, chunk);
					
					Interlocked.Add(ref chunkCount, region.Chunks.Count);
				}
				
				Interlocked.Increment(ref regionCount);

				lock (writeLock)
				{
					Console.WriteLine($"regions: {regionCount}/{totalRegions}, chunks: {chunkCount}");
				}
			}
		);
		
		using var file = new StreamWriter("out.csv");
		dataset.Write(file);

		Console.WriteLine($"Total chunks: {chunkCount:N0}");
	}

	private static void AnalyzeChunk(IDataset dataset, Chunk chunk)
	{
		foreach (var section in chunk.Sections)
		{
			var sectionXz = section.Pos << 4;
			var sectionY = section.Y << 4;

			for (var x = 0; x < 16; x++)
				for (var z = 0; z < 16; z++)
					for (var y = 0; y < 16; y++)
					{
						var pos = new Coord3(x, y, z);
						var block = section.GetBlockState(pos);
						var biome = section.GetBiome(pos);
						dataset.Insert(new Coord3(x + sectionXz.X, y + sectionY, z + sectionXz.Z), new Biome(biome), new BlockId(block.Name));
					}
		}
	}

	static void Main2(string[] args)
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
			var region = world.GetRegion(regionId, false);
			if (region == null)
				continue;

			AddRange(chunks, region.Chunks);
		}

		Console.WriteLine($"Total chunks          : {chunks.Count:N0}");

		var structure = new ScarifStructure(chunks);
		var (numBlocks, fileSize, dictSize, regionSize, compressedRegionSize) = structure.Save(args[1]);

		Console.WriteLine($"Total blocks          : {numBlocks:N0}");
		Console.WriteLine($"Total size            : {fileSize:N0} bytes");
		Console.WriteLine($"Dictionary size       : {dictSize:N0} bytes");
		Console.WriteLine($"Metadata overhead size: {fileSize - compressedRegionSize - dictSize:N0} bytes");
		Console.WriteLine($"Region size           : {regionSize:N0} bytes");
		Console.WriteLine($"Compressed region size: {compressedRegionSize:N0} bytes");
		Console.WriteLine($"Region compression    : {compressedRegionSize / (double)regionSize * 100:F3}%");
		Console.WriteLine($"Region bits per block : {compressedRegionSize * 8 / (double)numBlocks:F7}");
		Console.WriteLine($"Blocks per region bit : {(double)numBlocks / (compressedRegionSize * 8):F2}");
	}

	private static void AddRange<T1, T2>(Dictionary<T1, T2> dest, Dictionary<T1, T2> src)
	{
		foreach (var (key, value) in src) dest[key] = value;
	}
}