using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ZstdNet;

namespace ScarifTools;

public enum ChunkLayoutFormat : byte
{
    Linear = 0,
    Hilbert = 1
}

public record struct ChunkLayoutData(Coord2 Position, ChunkLayoutFormat Layout, byte[] Data, string DataChecksum, int CompressionScore);

internal readonly struct ScarifStructure
{
    public readonly Dictionary<Coord2, Chunk> Chunks;

    public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
    {
        Chunks = chunks;
    }

    private static int GetCompressionScore(byte[] data)
    {
        using var compressor = new Compressor(new CompressionOptions(10));
        return compressor.Wrap(data).Length;
    }

    public (int NumBlocks, long FileLength) Save(string filename)
    {
        var numBlocks = 0;
        using var fs = File.Open(filename, FileMode.Create);
        var scrfWriter = new BinaryWriter(fs);

        const int chunksPerRegion = 16;
        var numRegions = Chunks.Count / chunksPerRegion + 1;

        scrfWriter.Write(0x46524353); // "SCRF"
        scrfWriter.Write(3); // Version 3
        scrfWriter.Write(Chunks.Count);
        scrfWriter.Write(numRegions);

        var chunks = new Dictionary<Coord2, int>();
        var headerOffset = scrfWriter.BaseStream.Position;

        const int chunkHeaderEntrySize = sizeof(int) * 3 + sizeof(long);
        const int regionHeaderEntrySize = sizeof(int);
        const int compressionDictHeaderEntrySize = sizeof(int);
        var headerLength = chunkHeaderEntrySize * Chunks.Count + compressionDictHeaderEntrySize + regionHeaderEntrySize * numRegions;
        scrfWriter.BaseStream.Seek(headerLength, SeekOrigin.Current);

        var chunkData = new List<ChunkLayoutData>();

        // TODO: entities?

        using var sha = SHA512.Create();

        // Encode each chunk
        foreach (var (coord, chunk) in Chunks)
        {
            numBlocks += chunk.Sections.Sum(section => section.BlockStates.Length);

            var layout = ChunkLayoutFormat.Linear;
            // foreach (var layout in Enum.GetValues(typeof(ChunkLayoutFormat)).Cast<ChunkLayoutFormat>())
            {
                using var chunkMemStream = new MemoryStream();
                var chunkWriter = new BinaryWriter(chunkMemStream);

                chunkWriter.Write((byte)layout);

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

                    // Section length always 4096 (16^3)
                    for (var i = 0; i < section.BlockStates.Length; i++)
                    {
                        var stateIndex = layout switch
                        {
                            ChunkLayoutFormat.Linear => i,
                            ChunkLayoutFormat.Hilbert => ChunkSection.GetBlockIndex(HibertUtil.SampleCurve(i)),
                            _ => throw new ArgumentOutOfRangeException(nameof(layout))
                        };

                        var state = section.BlockStates[stateIndex];
                        chunkWriter.Write7BitEncodedInt(state);
                    }
                }

                var array = chunkMemStream.ToArray();
                var checksum = Convert.ToHexString(sha.ComputeHash(array));
                var compressionScore = GetCompressionScore(array);

                // Check for chunks with duplicate data checksums
                var chunkIdx = chunkData.FindIndex(key => key.DataChecksum == checksum);

                if (chunkIdx < 0)
                {
                    // If a duplicate chunk is NOT found, see if a higher-entropy encoding of this chunk exists

                    chunkIdx = chunkData.FindIndex(key => key.Position == coord);
                    if (chunkIdx < 0)
                    {
                        // If no duplicate chunk exists AND no other encodings of this chunk exist, insert this one
                        chunkIdx = chunkData.Count;
                        chunkData.Add(new ChunkLayoutData(coord, layout, array, checksum, compressionScore));
                    }
                    else if (chunkData[chunkIdx].CompressionScore <= compressionScore)
                    {
                        // A lower-entropy encoding exists, do nothing
                    }
                    else
                    {
                        // This encoding has lower entropy, replace the old one
                        chunkData[chunkIdx] = new ChunkLayoutData(coord, layout, array, checksum, compressionScore);
                    }
                }

                chunks[coord] = chunkIdx;
            }
        }

        // Compile chunks into regions
        var regions = new byte[numRegions][];
        var chunkOffset = new long[chunkData.Count];

        for (var i = 0; i < regions.Length; i++)
        {
            using var ms = new MemoryStream();

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
        using (var regionCompressor = new Compressor(new CompressionOptions(dict, 10)))
            compressedRegions = regions
                .Select(region => regionCompressor.Wrap(region))
                .ToArray();

        // Write header data
        // No need to compress/encode integers here
        // since the header is a fixed size
        scrfWriter.BaseStream.Seek(headerOffset, SeekOrigin.Begin);

        // Write chunk keys and offsets
        foreach (var (coord, index) in chunks)
        {
            // Chunk position
            scrfWriter.Write(coord.X);
            scrfWriter.Write(coord.Z);
            // Parent region
            scrfWriter.Write(index / chunksPerRegion);
            // Seek offset within region
            scrfWriter.Write(chunkOffset[index]);
        }

        // Write region lengths
        foreach (var compressedRegion in compressedRegions)
            scrfWriter.Write(compressedRegion.Length);

        // Write compression dictionary length
        scrfWriter.Write(compressedDict.Length);

        // Write compression dictionary
        scrfWriter.Write(compressedDict);

        // Write regions
        foreach (var compressedRegion in compressedRegions)
            scrfWriter.Write(compressedRegion);

        return (numBlocks, scrfWriter.BaseStream.Position);
    }
}