using System;
using System.Collections.Generic;
using System.Linq;
using Substrate.Nbt;

namespace ScarifTools
{
	internal class Chunk
	{
		public readonly Dictionary<Coord3, TagNodeCompound> Tiles;
		public readonly Coord2 Pos;
		public readonly ChunkSection[] Sections;

		public Chunk(Coord2 pos, ChunkSection[] sections, Dictionary<Coord3, TagNodeCompound> tiles)
		{
			Tiles = tiles;
			Pos = pos;
			Sections = sections;
		}

		public BlockState GetBlock(Coord3 block)
		{
			var chunk = block >> 4;
			var section = Sections.FirstOrDefault(chunkSection => chunkSection.Y == chunk.Y);

			return section?.GetBlockState(block - (chunk << 4));
		}

		public static Chunk Load(NbtTree tag)
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

			var sections = sectionsList.Select(node => ChunkSection.Load(dataVersion, pos, node.ToTagCompound())).Where(section => section != null).ToArray();
			var tiles = tag.Root["block_entities"]
				.ToTagList()
				.Select(node => node.ToTagCompound())
				.ToDictionary(node => new Coord3(node["x"].ToTagInt().Data, node["y"].ToTagInt().Data, node["z"].ToTagInt().Data));

			return new Chunk(pos, sections, tiles);
		}

		protected bool Equals(Chunk other)
		{
			return Pos.Equals(other.Pos);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((Chunk) obj);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return Pos.GetHashCode();
		}

		public static bool operator ==(Chunk left, Chunk right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(Chunk left, Chunk right)
		{
			return !Equals(left, right);
		}
	}
}