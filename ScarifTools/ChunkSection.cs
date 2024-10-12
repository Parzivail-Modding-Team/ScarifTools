using System;
using System.Collections;
using System.Linq;
using System.Security.AccessControl;
using Acacia;

namespace ScarifTools;

public class ChunkSection
{
	public readonly Coord2 Pos;
	public readonly sbyte Y;
	public readonly int[] BlockStates;
	public readonly BlockState[] BlockStatePalette;
	public readonly int[] Biomes;
	public readonly string[] BiomePalette;

	public ChunkSection(int dataVersion, Coord2 pos, sbyte y, long[]? packedBlockStates, BlockState[] blockStatePalette, long[]? packedBiomes, string[] biomePalette)
	{
		Pos = pos;
		Y = y;
		
		BlockStatePalette = blockStatePalette;
		BlockStates = UnpackLongs(packedBlockStates, blockStatePalette.Length, 4, 4096);

		BiomePalette = biomePalette;
		Biomes = UnpackLongs(packedBiomes, biomePalette.Length, 0, 64);
	}

	private static int[] UnpackLongs(long[]? packedValues, int paletteSize, int minBitWidth, int indices)
	{
		var values = new int[indices];

		if (packedValues == null)
			// A null packed value represents a single-entry palette
			return values;
		
		var bitWidth = Math.Max(MathUtil.GetMinRepresentableBits(paletteSize), minBitWidth);

		// In 20w17a+ the remaining bits in a long are left unused instead of
		// the data being split and rolling over into the next long

		var valuesPerLong = sizeof(long) * 8 / bitWidth;

		var longIdx = 0;
		var bitIdx = 0;
		BitArray? bArr = null;

		for (var i = 0; i < values.Length; i++)
		{
			if (i % valuesPerLong == 0)
			{
				bArr = new BitArray(BitConverter.GetBytes(packedValues[longIdx]));
				longIdx++;
				bitIdx = 0;
			}

			values[i] = TakeBits(bArr!, bitIdx, bitWidth);
			bitIdx += bitWidth;
		}

		return values;
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
		return BlockStatePalette[BlockStates[GetBlockIndex(block)]];
	}

	public static int GetBlockIndex(Coord3 pos)
	{
		return GetBlockIndex(pos.X, pos.Y, pos.Z);
	}

	public static int GetBlockIndex(int x, int y, int z)
	{
		return y * 256 + z * 16 + x;
	}

	public string GetBiome(Coord3 block)
	{
		return BiomePalette[Biomes[GetBiomeIndex(block)]];
	}

	public static int GetBiomeIndex(Coord3 pos)
	{
		return GetBiomeIndex(pos.X, pos.Y, pos.Z);
	}

	public static int GetBiomeIndex(int x, int y, int z)
	{
		x /= 4;
		y /= 4;
		z /= 4;
		
		return y * 16 + z * 4 + x;
	}

	public static ChunkSection? Load(int dataVersion, Coord2 pos, NbtCompound tag)
	{
		var y = tag.GetByte("Y");

		if (!tag.TryGetCompound("block_states", out var blockStatesTag))
			return null;
		
		var blockStatePalette = blockStatesTag.GetList("palette").Elements.Select(tagNode => BlockState.Load((NbtCompound)tagNode)).ToArray();
		blockStatesTag.TryGetLongArray("data", out var blockStates);
		
		var biomesTag = tag.GetCompound("biomes");
		var biomePalette = biomesTag.GetList("palette").Elements.Select(tagNode =>((NbtString)tagNode).Value).ToArray();
		biomesTag.TryGetLongArray("data", out var biomes);

		return new ChunkSection(dataVersion, pos, y, blockStates?.Elements, blockStatePalette, biomes?.Elements, biomePalette);
	}

	protected bool Equals(ChunkSection other)
	{
		return Pos.Equals(other.Pos) && Y == other.Y;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != this.GetType()) return false;
		return Equals((ChunkSection)obj);
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		return HashCode.Combine(Pos, Y);
	}

	public static bool operator ==(ChunkSection? left, ChunkSection? right)
	{
		return Equals(left, right);
	}

	public static bool operator !=(ChunkSection? left, ChunkSection? right)
	{
		return !Equals(left, right);
	}
}