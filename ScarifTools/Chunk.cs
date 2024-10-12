using System.Collections.Generic;
using System.IO;
using System.Linq;
using Acacia;

namespace ScarifTools;

public record Chunk(Coord2 Pos, ChunkSection[] Sections, Dictionary<Coord3, NbtCompound> Tiles)
{
	public BlockState? GetBlock(Coord3 block)
	{
		var chunk = block >> 4;
		var section = Sections.FirstOrDefault(chunkSection => chunkSection.Y == chunk.Y);

		return section?.GetBlockState(block - (chunk << 4));
	}

	public static Chunk? Load(NbtElement tag)
	{
		if (tag is not NbtCompound root)
			throw new InvalidDataException("Chunk was not NbtCompound");
		
		var dataVersion = root.GetInt("DataVersion");

		var status = root.GetString("Status");
		if (status != "minecraft:full")
			return null;

		var sectionsList = root.GetList("sections");
		if (sectionsList.Elements.Count == 0)
			return null;

		var x = root.GetInt("xPos");
		var z = root.GetInt("zPos");
		var pos = new Coord2(x, z);

		var sections = sectionsList
			.Elements
			.Select(node => ChunkSection.Load(dataVersion, pos, (NbtCompound)node))
			.Where(section => section != null)
			.Select(section => section!) // Require section to be non-null at this point
			.ToArray();
		var tiles = root.GetList("block_entities")
			.Elements
			.Select(node => (NbtCompound)node)
			.ToDictionary(node => new Coord3(node.GetInt("x"), node.GetInt("y"), node.GetInt("z")));

		return new Chunk(pos, sections, tiles);
	}
}