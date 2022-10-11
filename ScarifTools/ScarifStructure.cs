using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.IO;
using ZstdNet;

namespace ScarifTools;

internal readonly struct ScarifStructure
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    public readonly Dictionary<Coord2, Chunk> Chunks;

    public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
    {
        Chunks = chunks;
    }

    private static (BlockState[] Palette, int[] States) SortPaletteByUsage(BlockState[] palette, int[] states)
    {
        var histogram = new Dictionary<BlockState, int>();
        foreach (var state in palette)
            histogram[state] = 0;

        foreach (var state in states)
            histogram[palette[state]]++;

        var orderedPalette = histogram.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToArray();
        var orderedStateIndices = new Dictionary<BlockState, int>();

        for (var i = 0; i < orderedPalette.Length; i++)
            orderedStateIndices[orderedPalette[i]] = i;

        for (var i = 0; i < states.Length; i++)
            states[i] = orderedStateIndices[palette[states[i]]];

        return (orderedPalette, states);
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

            chunkWriter.Write7BitEncodedInt(chunk.Sections.Length);
            foreach (var section in chunk.Sections)
            {
                chunkWriter.Write(section.Y);

                // Greatly improve compression on sections that have > 127
                // unique states, generally improve compression otherwise
                var (palette, states) = SortPaletteByUsage(section.Palette, section.BlockStates);

                // Write section palette
                chunkWriter.Write7BitEncodedInt(palette.Length);
                foreach (var paletteEntry in palette)
                {
                    chunkWriter.Write(paletteEntry.Name);
                    if (paletteEntry.Properties != null)
                        chunkWriter.WriteNbt(paletteEntry.Properties);
                    else
                        // Leverage the fact that WriteNbt prefixes NBT data with the
                        // payload length -- write a zero-length payload if there are
                        // no properties
                        chunkWriter.Write7BitEncodedInt(0);
                }

                // Skip writing state data for sections filled with one block
                if (section.Palette.Length < 2)
                    continue;

                // Section length always 4096 (16^3)
                foreach (var state in states)
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