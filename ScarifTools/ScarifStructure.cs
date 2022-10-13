using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.IO;
using ZstdNet;

namespace ScarifTools;

public enum SpecialBlockProperties : byte
{
	ExtendedProperty = 255,
	AxisX = 0,
	AxisY = 1,
	AxisZ = 2,
	LitFalse = 3,
	LitTrue = 4,
	Level0 = 5,
	Level1 = 6,
	Level2 = 7,
	Level3 = 8,
	Level4 = 9,
	Level5 = 10,
	Level6 = 11,
	Level7 = 12,
	Level8 = 13,
	Level9 = 14,
	Level10 = 15,
	Level11 = 16,
	Level12 = 17,
	Level13 = 18,
	Level14 = 19,
	Level15 = 20,
	HalfLower = 21,
	HalfUpper = 22,
	WaterloggedFalse = 23,
	WaterloggedTrue = 24,
	FacingUp = 25,
	FacingDown = 26,
	FacingNorth = 27,
	FacingSouth = 28,
	FacingEast = 29,
	FacingWest = 30,
	Age = 31,
	SnowyFalse = 32,
	SnowyTrue = 33,
	Distance = 34,
	PersistentFalse = 35,
	PersistentTrue = 36,
	Power0 = 37,
	Power1 = 38,
	Power2 = 39,
	Power3 = 40,
	Power4 = 41,
	Power5 = 42,
	Power6 = 43,
	Power7 = 44,
	Power8 = 45,
	Power9 = 46,
	Power10 = 47,
	Power11 = 48,
	Power12 = 49,
	Power13 = 50,
	Power14 = 51,
	Power15 = 52,
	UpFalse = 53,
	UpTrue = 54,
	DownFalse = 55,
	DownTrue = 56,
	NorthFalse = 57,
	NorthTrue = 58,
	SouthFalse = 59,
	SouthTrue = 60,
	EastFalse = 61,
	EastTrue = 62,
	WestFalse = 63,
	WestTrue = 64,
	HalfTop = 65,
	HalfBottom = 66,
	NorthNone = 67,
	NorthLow = 68,
	NorthTall = 69,
	SouthNone = 70,
	SouthLow = 71,
	SouthTall = 72,
	EastNone = 73,
	EastLow = 74,
	EastTall = 75,
	WestNone = 76,
	WestLow = 77,
	WestTall = 78,
}

internal record SortedChunkPalette(BlockState[] Palette, int[] States, int Y);

internal readonly struct ScarifStructure
{
	private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

	public readonly Dictionary<Coord2, Chunk> Chunks;

	public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
	{
		Chunks = chunks;
	}

	private static SortedChunkPalette SortPaletteByUsage(BlockState[] palette, int[] states)
	{
		var histogram = new Dictionary<int, int>(palette.Length);
		for (var i = 0; i < palette.Length; i++)
			histogram[i] = 0;

		foreach (var state in states)
			histogram[state]++;

		var orderedPalette = histogram.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToArray();
		var orderedStateIndices = new Dictionary<int, int>(palette.Length);

		for (var i = 0; i < orderedPalette.Length; i++)
			orderedStateIndices[orderedPalette[i]] = i;

		for (var i = 0; i < states.Length; i++)
			states[i] = orderedStateIndices[states[i]];

		return new SortedChunkPalette(orderedPalette.Select(i => palette[i]).ToArray(), states, 0);
	}

	private static void WriteBlockStateProperties(BinaryWriter w, string block, Dictionary<string, string> props)
	{
		foreach (var (key, value) in props)
		{
			switch (key)
			{
				case "axis":
				{
					var p = value switch
					{
						"x" => SpecialBlockProperties.AxisX,
						"y" => SpecialBlockProperties.AxisY,
						"z" => SpecialBlockProperties.AxisZ,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "lit":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.LitFalse,
						"true" => SpecialBlockProperties.LitTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "level":
				{
					var p = value switch
					{
						"0" => SpecialBlockProperties.Level0,
						"1" => SpecialBlockProperties.Level1,
						"2" => SpecialBlockProperties.Level2,
						"3" => SpecialBlockProperties.Level3,
						"4" => SpecialBlockProperties.Level4,
						"5" => SpecialBlockProperties.Level5,
						"6" => SpecialBlockProperties.Level6,
						"7" => SpecialBlockProperties.Level7,
						"8" => SpecialBlockProperties.Level8,
						"9" => SpecialBlockProperties.Level9,
						"10" => SpecialBlockProperties.Level10,
						"11" => SpecialBlockProperties.Level11,
						"12" => SpecialBlockProperties.Level12,
						"13" => SpecialBlockProperties.Level13,
						"14" => SpecialBlockProperties.Level14,
						"15" => SpecialBlockProperties.Level15,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "power":
				{
					var p = value switch
					{
						"0" => SpecialBlockProperties.Power0,
						"1" => SpecialBlockProperties.Power1,
						"2" => SpecialBlockProperties.Power2,
						"3" => SpecialBlockProperties.Power3,
						"4" => SpecialBlockProperties.Power4,
						"5" => SpecialBlockProperties.Power5,
						"6" => SpecialBlockProperties.Power6,
						"7" => SpecialBlockProperties.Power7,
						"8" => SpecialBlockProperties.Power8,
						"9" => SpecialBlockProperties.Power9,
						"10" => SpecialBlockProperties.Power10,
						"11" => SpecialBlockProperties.Power11,
						"12" => SpecialBlockProperties.Power12,
						"13" => SpecialBlockProperties.Power13,
						"14" => SpecialBlockProperties.Power14,
						"15" => SpecialBlockProperties.Power15,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "half":
				{
					var p = value switch
					{
						"lower" => SpecialBlockProperties.HalfLower,
						"upper" => SpecialBlockProperties.HalfUpper,
						"top" => SpecialBlockProperties.HalfTop,
						"bottom" => SpecialBlockProperties.HalfBottom,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "waterlogged":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.WaterloggedFalse,
						"true" => SpecialBlockProperties.WaterloggedTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "facing":
				{
					var p = value switch
					{
						"up" => SpecialBlockProperties.FacingUp,
						"down" => SpecialBlockProperties.FacingDown,
						"north" => SpecialBlockProperties.FacingNorth,
						"south" => SpecialBlockProperties.FacingSouth,
						"east" => SpecialBlockProperties.FacingEast,
						"west" => SpecialBlockProperties.FacingWest,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "snowy":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.SnowyFalse,
						"true" => SpecialBlockProperties.SnowyTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "persistent":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.PersistentFalse,
						"true" => SpecialBlockProperties.PersistentTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "up":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.UpFalse,
						"true" => SpecialBlockProperties.UpTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "down":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.DownFalse,
						"true" => SpecialBlockProperties.DownTrue,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "north":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.NorthFalse,
						"true" => SpecialBlockProperties.NorthTrue,
						"none" => SpecialBlockProperties.NorthNone,
						"low" => SpecialBlockProperties.NorthLow,
						"tall" => SpecialBlockProperties.NorthTall,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "south":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.SouthFalse,
						"true" => SpecialBlockProperties.SouthTrue,
						"none" => SpecialBlockProperties.SouthNone,
						"low" => SpecialBlockProperties.SouthLow,
						"tall" => SpecialBlockProperties.SouthTall,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "east":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.EastFalse,
						"true" => SpecialBlockProperties.EastTrue,
						"none" => SpecialBlockProperties.EastNone,
						"low" => SpecialBlockProperties.EastLow,
						"tall" => SpecialBlockProperties.EastTall,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "west":
				{
					var p = value switch
					{
						"false" => SpecialBlockProperties.WestFalse,
						"true" => SpecialBlockProperties.WestTrue,
						"none" => SpecialBlockProperties.WestNone,
						"low" => SpecialBlockProperties.WestLow,
						"tall" => SpecialBlockProperties.WestTall,
						_ => throw new NotSupportedException()
					};

					w.Write((byte)p);
					break;
				}
				case "age":
				{
					w.Write((byte)SpecialBlockProperties.Age);
					w.Write(byte.Parse(value));
					break;
				}
				case "distance":
				{
					w.Write((byte)SpecialBlockProperties.Distance);
					w.Write(byte.Parse(value));
					break;
				}
				default:
				{
					w.Write((byte)SpecialBlockProperties.ExtendedProperty);
					w.Write(key);
					w.Write(value);
					break;
				}
			}
		}
	}

	public (int NumBlocks, long FileLength, int DictionarySize, int TotalRegionSize, int CompressedRegionSize) Save(string filename)
	{
		/*
		 * Process chunks and build regions
		 */

		var numBlocks = 0;

		var chunks = new Dictionary<Coord2, int>();
		var chunkData = new List<byte[]>();
		var chunkChecksums = new Dictionary<string, int>();

		// TODO: entities?

		using var sha = SHA512.Create();

		// Encode each chunk
		foreach (var (coord, chunk) in Chunks)
		{
			numBlocks += chunk.Sections.Sum(section => section.BlockStates.Length);

			using var chunkMemStream = MemoryStreamManager.GetStream("chunk");
			var chunkWriter = new BinaryWriter(chunkMemStream);

			chunkWriter.Write7BitEncodedInt(chunk.Tiles.Count);
			foreach (var ((x, y, z), originalTag) in chunk.Tiles)
			{
				chunkWriter.Write((byte)(((x & 0x15) << 4) | z & 0x15));
				chunkWriter.Write7BitEncodedInt(y);

				// Write tag without redundant position data
				var tag = originalTag.Copy();
				tag.Remove("x");
				tag.Remove("y");
				tag.Remove("z");
				chunkWriter.WriteNbt(tag);
			}

			// Greatly improve compression on sections that have > 127
			// unique states, generally improve compression otherwise
			var sectionsWithSortedPalettes = chunk.Sections
				.Select(section => SortPaletteByUsage(section.Palette, section.BlockStates) with { Y = section.Y })
				.ToArray();

			chunkWriter.Write7BitEncodedInt(chunk.Sections.Length);

			foreach (var section in sectionsWithSortedPalettes)
			{
				// Write section palette
				chunkWriter.Write7BitEncodedInt(section.Palette.Length);
				foreach (var paletteEntry in section.Palette)
				{
					chunkWriter.Write(paletteEntry.Name.Replace("minecraft:", ""));
					chunkWriter.Write7BitEncodedInt(paletteEntry.Properties?.Count ?? 0);
					if (paletteEntry.Properties != null)
						WriteBlockStateProperties(chunkWriter, paletteEntry.Name, paletteEntry.Properties);
				}
			}

			foreach (var section in sectionsWithSortedPalettes)
				chunkWriter.Write7BitEncodedInt(section.Y);

			foreach (var palette in sectionsWithSortedPalettes)
			{
				// On the reading end, these sections can be implicitly
				// skipped by checking the palette size of this section
				// before attempting to read the state array
				if (palette.Palette.Length < 2)
					continue;

				// Section length always 4096 (16^3)
				foreach (var state in palette.States)
					chunkWriter.Write7BitEncodedInt(state);
			}

			var array = chunkMemStream.ToArray();
			var checksum = Convert.ToHexString(sha.ComputeHash(array));

			// Check for chunks with duplicate data checksums
			if (chunkChecksums.TryGetValue(checksum, out var chunkIdx))
			{
				// If a duplicate is found, use the previous index
				chunks[coord] = chunkIdx;
			}
			else
			{
				// If a duplicate chunk is NOT found, insert this chunk
				chunks[coord] = chunkChecksums[checksum] = chunkData.Count;
				chunkData.Add(array);
			}
		}

		// Compile chunks into regions
		const int chunksPerRegion = 16;
		var numRegions = Chunks.Count / chunksPerRegion + 1;

		var regions = new byte[numRegions][];
		var chunkOffset = new long[chunkData.Count];

		for (var i = 0; i < regions.Length; i++)
		{
			using var ms = MemoryStreamManager.GetStream("region");

			// TODO: Cursed chunk grouping math
			for (var j = i * chunksPerRegion; j < chunkData.Count && j < (i + 1) * chunksPerRegion; j++)
			{
				chunkOffset[j] = ms.Position;
				ms.Write(chunkData[i]);
			}

			regions[i] = ms.ToArray();
		}

		/*
		 * Compress regions
		 */

		// Train compression dictionary
		var dict = DictBuilder.TrainFromBuffer(regions);
		byte[] compressedDict;
		using (var dictCompressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel)))
			compressedDict = dictCompressor.Wrap(dict);

		// Compress regions
		byte[][] compressedRegions;
		using (var regionCompressor = new Compressor(new CompressionOptions(dict, CompressionOptions.MaxCompressionLevel)))
			compressedRegions = regions
				.Select(region => regionCompressor.Wrap(region))
				.ToArray();

		/*
		 * Write and compress offset headers
		 */

		// Write offset headers to temporary buffer for compression
		using var offsetsMemStream = MemoryStreamManager.GetStream("offsets");
		var offsetWriter = new BinaryWriter(offsetsMemStream);

		// Write chunk keys and offsets
		foreach (var (coord, index) in chunks)
		{
			// Chunk position
			offsetWriter.Write(coord.X);
			offsetWriter.Write(coord.Z);
			// Parent region
			offsetWriter.Write(index / chunksPerRegion);
			// Seek offset within region
			offsetWriter.Write(chunkOffset[index]);
		}

		// Write region lengths
		foreach (var compressedRegion in compressedRegions)
			offsetWriter.Write(compressedRegion.Length);

		// Compress offset headers
		byte[] compressedOffsets;
		using (var headerCompressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel)))
			compressedOffsets = headerCompressor.Wrap(offsetsMemStream.ToArray());

		/*
		 * Write data to SCRF file
		 */

		using var fs = File.Open(filename, FileMode.Create);
		var scrfWriter = new BinaryWriter(fs);

		// Write header
		scrfWriter.Write(0x46524353); // "SCRF"
		scrfWriter.Write(3); // Version 3
		scrfWriter.Write(chunks.Count);
		scrfWriter.Write(compressedRegions.Length);

		scrfWriter.Write(compressedOffsets);

		// Write compression dictionary
		scrfWriter.Write7BitEncodedInt(compressedDict.Length);
		scrfWriter.Write(compressedDict);

		// Write regions
		foreach (var compressedRegion in compressedRegions)
			scrfWriter.Write(compressedRegion);

		var totalRegionSize = regions.Sum(bytes => bytes.Length);
		var compressedRegionSize = compressedRegions.Sum(bytes => bytes.Length);

		return (numBlocks, scrfWriter.BaseStream.Position, compressedDict.Length, totalRegionSize, compressedRegionSize);
	}
}