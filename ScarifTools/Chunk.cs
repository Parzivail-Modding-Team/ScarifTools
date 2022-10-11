using System.Collections.Generic;
using System.Linq;
using Substrate.Nbt;

namespace ScarifTools;

public record Chunk(Coord2 Pos, ChunkSection[] Sections, Dictionary<Coord3, TagNodeCompound> Tiles)
{
    public BlockState? GetBlock(Coord3 block)
    {
        var chunk = block >> 4;
        var section = Sections.FirstOrDefault(chunkSection => chunkSection.Y == chunk.Y);

        return section?.GetBlockState(block - (chunk << 4));
    }

    public static Chunk? Load(NbtTree tag)
    {
        var dataVersion = tag.Root["DataVersion"].ToTagInt().Data;

        if (!tag.Root["Status"].ToTagString().Data.Equals("full"))
            return null;

        var sectionsList = tag.Root["sections"].ToTagList();
        if (sectionsList.Count == 0)
            return null;

        var x = tag.Root["xPos"].ToTagInt().Data;
        var z = tag.Root["zPos"].ToTagInt().Data;
        var pos = new Coord2(x, z);

        // with the equivalent LINQ method chain, c# does not understand section is not nullable (the compiled code is the same, though)
        var sections = (from section in from node in sectionsList select ChunkSection.Load(dataVersion, pos, node.ToTagCompound())
            where section != null
            select section).ToArray();
        var tiles = tag.Root["block_entities"]
            .ToTagList()
            .Select(node => node.ToTagCompound())
            .ToDictionary(node => new Coord3(node["x"].ToTagInt().Data, node["y"].ToTagInt().Data, node["z"].ToTagInt().Data));

        return new Chunk(pos, sections, tiles);
    }
}