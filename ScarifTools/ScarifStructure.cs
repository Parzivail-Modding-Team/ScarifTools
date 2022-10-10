using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.IO;
using ZstdNet;

namespace ScarifTools;

public enum SectionLayoutFormat : byte
{
    Linear = 0,
    Hilbert = 1
}

public record struct SectionData(SectionLayoutFormat Format, byte[] Data, int CompressionScore);

public record struct ChunkData(Coord2 Position, byte[] Data, string DataChecksum);

internal readonly struct ScarifStructure
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    public readonly Dictionary<Coord2, Chunk> Chunks;

    public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
    {
        Chunks = chunks;
    }

    private static SectionData EncodeSectionStateIndices(int[] states)
    {
        using var regionCompressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel));

        SectionData? smallestSectionData = null;

        foreach (var layout in Enum.GetValues(typeof(SectionLayoutFormat)).Cast<SectionLayoutFormat>())
        {
            using var memStream = MemoryStreamManager.GetStream("section");
            var sectionWriter = new BinaryWriter(memStream);

            // Section length always 4096 (16^3)
            for (var i = 0; i < states.Length; i++)
            {
                var stateIndex = layout switch
                {
                    SectionLayoutFormat.Linear => i,
                    SectionLayoutFormat.Hilbert => ChunkSection.GetBlockIndex(HilbertUtil.SampleCurve(i)),
                    _ => throw new ArgumentOutOfRangeException(nameof(layout))
                };

                var state = states[stateIndex];
                sectionWriter.Write7BitEncodedInt(state);
            }

            var data = memStream.ToArray();
            var layoutData = new SectionData(layout, data, regionCompressor.Wrap(data).Length);
            if (smallestSectionData == null || layoutData.CompressionScore < smallestSectionData.Value.CompressionScore)
                smallestSectionData = layoutData;
        }

        if (smallestSectionData == null)
            throw new InvalidOperationException();

        return smallestSectionData.Value;
    }

    public (int NumBlocks, long FileLength, int DictionarySize, int TotalRegionSize, int CompressedRegionSize) Save(string filename)
    {
        var numBlocks = 0;

        var chunks = new Dictionary<Coord2, int>();
        var chunkData = new List<ChunkData>();

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
                chunkWriter.Write((byte)((x & 0x15) << 4 | z & 0x15));
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

                chunkWriter.Write7BitEncodedInt(section.Palette.Length);
                foreach (var paletteEntry in section.Palette)
                {
                    chunkWriter.Write(paletteEntry.Name);
                    var hasProperties = paletteEntry.Properties != null;

                    chunkWriter.Write((byte)(hasProperties ? 1 : 0));

                    if (hasProperties)
                        chunkWriter.WriteNbt(paletteEntry.Properties);
                }

                var (layout, stateIndices, _) = EncodeSectionStateIndices(section.BlockStates);
                chunkWriter.Write((byte)layout);
                chunkWriter.Write(stateIndices);
            }

            var array = chunkMemStream.ToArray();
            var checksum = Convert.ToHexString(sha.ComputeHash(array));

            // Check for chunks with duplicate data checksums
            var chunkIdx = chunkData.FindIndex(key => key.DataChecksum == checksum);

            if (chunkIdx < 0)
            {
                // If a duplicate chunk is NOT found, insert this chunk
                chunkIdx = chunkData.Count;
                chunkData.Add(new ChunkData(coord, array, checksum));
            }

            chunks[coord] = chunkIdx;
        }

        const int chunksPerRegion = 16;
        var numRegions = Chunks.Count / chunksPerRegion + 1;

        // Compile chunks into regions
        var regions = new byte[numRegions][];
        var chunkOffset = new long[chunkData.Count];

        for (var i = 0; i < regions.Length; i++)
        {
            using var ms = MemoryStreamManager.GetStream("region");

            // TODO: Cursed chunk grouping math
            for (var j = i * chunksPerRegion; j < chunkData.Count && j < (i + 1) * chunksPerRegion; j++)
            {
                chunkOffset[j] = ms.Position;
                ms.Write(chunkData[i].Data);
            }

            regions[i] = ms.ToArray();
        }

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

        using var fs = File.Open(filename, FileMode.Create);
        var scrfWriter = new BinaryWriter(fs);

        // Write header
        scrfWriter.Write(0x46524353); // "SCRF"
        scrfWriter.Write(3); // Version 3
        scrfWriter.Write(chunks.Count);
        scrfWriter.Write(compressedRegions.Length);

        // Write offset headers to temporary buffer for compression
        using var headerMemStream = MemoryStreamManager.GetStream("header");
        var headerWriter = new BinaryWriter(headerMemStream);

        // Write chunk keys and offsets
        foreach (var (coord, index) in chunks)
        {
            // Chunk position
            headerWriter.Write7BitEncodedInt(coord.X);
            headerWriter.Write7BitEncodedInt(coord.Z);
            // Parent region
            headerWriter.Write7BitEncodedInt(index / chunksPerRegion);
            // Seek offset within region
            headerWriter.Write7BitEncodedInt64(chunkOffset[index]);
        }

        // Write region lengths
        foreach (var compressedRegion in compressedRegions)
            headerWriter.Write7BitEncodedInt(compressedRegion.Length);

        // Compress and write offset headers
        byte[] compressedHeader;
        using (var headerCompressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel)))
            compressedHeader = headerCompressor.Wrap(headerMemStream.ToArray());

        scrfWriter.Write7BitEncodedInt(compressedHeader.Length);
        scrfWriter.Write(compressedHeader);

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