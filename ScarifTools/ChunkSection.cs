using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Substrate.Nbt;

namespace ScarifTools
{
	internal class ChunkSection
	{
		public readonly Coord2 Pos;
		public readonly byte Y;
		public readonly int[] BlockStates;
		public readonly long[] PackedBlockStates;
		public readonly BlockState[] Palette;

		public ChunkSection(int dataVersion, Coord2 pos, byte y, long[] packedStates, BlockState[] palette)
		{
			Pos = pos;
			Y = y;
			Palette = palette;

			PackedBlockStates = packedStates;

			var paletteLength = palette.Length;
			paletteLength--;
			paletteLength |= paletteLength >> 1;
			paletteLength |= paletteLength >> 2;
			paletteLength |= paletteLength >> 4;
			paletteLength |= paletteLength >> 8;
			paletteLength |= paletteLength >> 16;
			paletteLength++;

			var bitWidth = Math.Max((int)Math.Log2(paletteLength), 4);

			BlockStates = new int[4096];
			
			var isTightlyPacked = dataVersion < 2529; // 20w17a (2529) changed the data packing

			if (isTightlyPacked)
			{
				var bArr = new BitArray(packedStates.SelectMany(BitConverter.GetBytes).ToArray());

				var bitIdx = 0;
				for (var i = 0; i < BlockStates.Length; i++)
				{
					BlockStates[i] = TakeBits(bArr, bitIdx, bitWidth);
					bitIdx += bitWidth;
				}
			}
			else
			{
				// In 20w17a+ the remaining bits in a long are left unused instead of
				// the data being split and rolling over into the next long

				var blocksPerLong = sizeof(long) * 8 / bitWidth;

				var longIdx = 0;
				var bitIdx = 0;
				BitArray bArr = null;

				for (var i = 0; i < BlockStates.Length; i++)
				{
					if (i % blocksPerLong == 0)
					{
						bArr = new BitArray(BitConverter.GetBytes(packedStates[longIdx]));
						longIdx++;
						bitIdx = 0;
					}

					BlockStates[i] = TakeBits(bArr, bitIdx, bitWidth);
					bitIdx += bitWidth;
				}
			}
		}

		private static int TakeBits(BitArray b, int i, int len)
		{
			var value = 0;

			for (var j = i + len - 1; j >= i; j--) 
				value = (value << 1) | (b[j] ? 1 : 0);

			return value;
		}

		public BlockState GetBlockState(Coord3 block)
		{
			return Palette[BlockStates[block.Y * 256 + block.Z * 16 + block.X]];
		}

		public static ChunkSection Load(int dataVersion, Coord2 pos, TagNodeCompound tag)
		{
			var y = tag["Y"].ToTagByte().Data;

			if (!tag.ContainsKey("Palette"))
				return null;

			var palette = tag["Palette"].ToTagList().Select(tagNode => BlockState.Load(tagNode.ToTagCompound())).ToArray();

			var blockStates = tag["BlockStates"].ToTagLongArray().Data;

			return new ChunkSection(dataVersion, pos, y, blockStates, palette);
		}

		protected bool Equals(ChunkSection other)
		{
			return Pos.Equals(other.Pos) && Y == other.Y;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ChunkSection) obj);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(Pos, Y);
		}

		public static bool operator ==(ChunkSection left, ChunkSection right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ChunkSection left, ChunkSection right)
		{
			return !Equals(left, right);
		}
	}
}