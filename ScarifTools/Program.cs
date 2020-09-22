using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ScarifTools
{
	class Program
	{
		static void Main(string[] args)
		{
			var worldPath = @"E:\Forge\Mods\PSWG\PSWG15\run\saves\Development";

			var levelPath = Path.Combine(worldPath, "level.dat");
			var registryPath = Path.Combine(worldPath, "data", "fabricRegistry.dat");

			var registry = FabricRegistry.Load(registryPath);
			var world = World.Load(levelPath);

			var region = new ScarifStructure(world.GetRegion(new Coord2(-1, -1)).Chunks);
			region.Save("out.scrf2");
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

			foreach (var (coord, chunk) in Chunks)
			{
				fs.Write(coord.X);
				fs.Write(coord.Z);
				fs.Write(0L);
			}

			foreach (var (coord, chunk) in Chunks)
			{
				chunkOffsets.Add(coord, fs.BaseStream.Position);

				var zs = new BinaryWriter(new GZipStream(fs.BaseStream, CompressionLevel.Optimal));
				zs.Write(chunk.Tiles.Count);

				using var ms = new MemoryStream();
				foreach (var (pos, tag) in chunk.Tiles)
				{
					zs.Write(pos.X);
					zs.Write(pos.Y);
					zs.Write(pos.Z);

					zs.WriteNbt(tag, ms);
				}

				zs.Write(chunk.Sections.Length);
				foreach (var section in chunk.Sections)
				{
					zs.Write(section.Y);

					zs.Write(section.Palette.Length);
					foreach (var paletteEntry in section.Palette)
					{
						zs.WriteNullTermString(paletteEntry.Name);
						zs.Write((byte)(paletteEntry.Properties != null ? 1 : 0));

						if (paletteEntry.Properties != null)
							zs.WriteNbt(paletteEntry.Properties, ms);
					}

					zs.Write(section.BlockStates.Length);
					foreach (var state in section.BlockStates) zs.Write7BitEncodedInt(state);
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
