using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

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
			fs.Write(2); // Version 2
			fs.Write(Chunks.Count);

			var chunkOffsets = new Dictionary<Coord2, long>();
			var headerOffset = fs.BaseStream.Position;

			const int headerEntrySize = sizeof(int) * 2 + sizeof(long);
			fs.BaseStream.Seek(headerEntrySize * Chunks.Count, SeekOrigin.Current);
			
			using var ms = new MemoryStream();
			
			foreach (var (coord, chunk) in Chunks)
			{
				chunkOffsets.Add(coord, fs.BaseStream.Position);

				var zs = fs; //new BinaryWriter(new GZipStream(fs.BaseStream, CompressionLevel.Optimal));

				zs.Write7BitEncodedInt(chunk.Tiles.Count);
				foreach (var (pos, tag) in chunk.Tiles)
				{
					zs.Write7BitEncodedInt(pos.X);
					zs.Write7BitEncodedInt(pos.Y);
					zs.Write7BitEncodedInt(pos.Z);

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
			}

			fs.BaseStream.Seek(headerOffset, SeekOrigin.Begin);
			foreach (var (coord, offset) in chunkOffsets)
			{
				fs.Write(coord.X);
				fs.Write(coord.Z);
				fs.Write(offset);
			}
		}
	}
}
