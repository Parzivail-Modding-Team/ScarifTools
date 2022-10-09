using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using ZstdNet;

namespace ScarifTools
{
	class Program
	{
		public static int Blocks = 0;

		static void Main(string[] args)
		{
			if (args.Length < 2) {
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
			region.Save(args[1]);

			Console.WriteLine($"Wrote {Blocks:N} blocks");
		}

		private static void AddRange<T1, T2>(Dictionary<T1, T2> dest, Dictionary<T1, T2> src)
		{
			foreach (var (key, value) in src) dest[key] = value;
		}
	}

	internal readonly struct ScarifStructure
	{
		public readonly Dictionary<Coord2, Chunk> Chunks;

		public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
		{
			Chunks = chunks;
		}

		public void Save(string filename)
		{
			using var fs = new BinaryWriter(File.Open(filename, FileMode.Create));

			fs.Write(0x46524353); // "SCRF"
			fs.Write(3); // Version 3
			fs.Write(Chunks.Count);

			var chunks = new Dictionary<Coord2, int>();
			var headerOffset = fs.BaseStream.Position;

			const int headerEntrySize = sizeof(int) * 2 + sizeof(long);
			fs.BaseStream.Seek(headerEntrySize * Chunks.Count, SeekOrigin.Current);

			using var chunkMs = new MemoryStream();
			var chunkData = new List<byte[]>();

			foreach (var (coord, chunk) in Chunks)
			{
				using var ms = new MemoryStream();

				var zs = new BinaryWriter(ms);

				zs.Write7BitEncodedInt(chunk.Tiles.Count);
				foreach (var ((x, y, z), originalTag) in chunk.Tiles)
				{
					zs.Write((byte)((x & 0x15) << 4 | z & 0x15));
					zs.Write7BitEncodedInt(y);

					var tag = originalTag.Copy();
					tag.Remove("x");
					tag.Remove("y");
					tag.Remove("z");
					zs.WriteNbt(tag);
				}

				zs.Write7BitEncodedInt(chunk.Sections.Length);
				foreach (var section in chunk.Sections)
				{
					zs.Write(section.Y);

					zs.Write7BitEncodedInt(section.Palette.Length);
					foreach (var paletteEntry in section.Palette)
					{
						zs.WriteNullTermString(paletteEntry.Name);
						var hasProperties = paletteEntry.Properties != null;

						zs.Write((byte)(hasProperties ? 1 : 0));

						if (hasProperties)
							zs.WriteNbt(paletteEntry.Properties);
					}

					// Section length always 4096 (16^3)
					foreach (var state in section.BlockStates) zs.Write7BitEncodedInt(state);

					Program.Blocks += section.BlockStates.Length;
				}

				var array = ms.ToArray();
				var index = chunkData.FindIndex(array.SequenceEqual);
				if (index < 0)
				{
					index = chunkData.Count;
					chunkData.Add(array);
				}
				chunks.Add(coord, index);
			}

			const int chunksPerRegion = 16;

			var regions = new byte[chunkData.Count / chunksPerRegion + 1][];
			var chunkOffset = new long[chunkData.Count];

			for (var i = 0; i < regions.Length; i++)
			{
				using var ms = new MemoryStream();
				for (var j = i * chunksPerRegion; j < chunkData.Count && j < (i + 1) * chunksPerRegion; j++)
				{
					chunkOffset[j] = ms.Position;
					ms.Write(chunkData[i]);
				}
				regions[i] = ms.ToArray();
			}

			var dict = DictBuilder.TrainFromBuffer(regions);
			byte[] compressedDict;
			using (var dictCompressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel)))
				compressedDict = dictCompressor.Wrap(dict);

			byte[][] compressedRegions;
			using (var regionCompressor = new Compressor(new CompressionOptions(dict, 10)))
				compressedRegions = (from region in regions select regionCompressor.Wrap(region)).ToArray();

			fs.BaseStream.Seek(headerOffset, SeekOrigin.Begin);
			foreach (var compressedRegion in compressedRegions)
			{
				fs.Write7BitEncodedInt(compressedRegion.Length);
			}
			fs.Write7BitEncodedInt(0);
			foreach (var (coord, index) in chunks)
			{
				fs.Write7BitEncodedInt(coord.X);
				fs.Write7BitEncodedInt(coord.Z);
				fs.Write7BitEncodedInt(index / chunksPerRegion);
				fs.Write7BitEncodedInt64(chunkOffset[index]);
			}
			fs.Write7BitEncodedInt(compressedDict.Length);
			fs.Write(compressedDict);
			foreach (var compressedRegion in compressedRegions)
			{
				fs.Write(compressedRegion);
			}
		}
	}
}
