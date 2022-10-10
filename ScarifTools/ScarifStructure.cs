using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZstdNet;

namespace ScarifTools;

internal readonly struct ScarifStructure
{
    public readonly Dictionary<Coord2, Chunk> Chunks;

    public ScarifStructure(Dictionary<Coord2, Chunk> chunks)
    {
        Chunks = chunks;
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

        var chunkData = new List<byte[]>();

        // TODO: entities?
        // TODO: can all of the Write7BitEncodedInt in the chunk data be replaced with Write since it'll be compressed anyway? how does it affect compression?

        // Encode each chunk
        foreach (var (coord, chunk) in Chunks)
        {
            using var chunkMemStream = new MemoryStream();
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

                // Section length always 4096 (16^3)
                for (var i = 0; i < section.BlockStates.Length; i++)
                {
                    var hilbertSample = HibertUtil.SampleCurve(i);
                    var state = section.BlockStates[ChunkSection.GetBlockIndex(hilbertSample)];
                    chunkWriter.Write7BitEncodedInt(state);
                }

                numBlocks += section.BlockStates.Length;
            }

            var array = chunkMemStream.ToArray();
            var index = chunkData.FindIndex(array.SequenceEqual);

            if (index < 0)
            {
                index = chunkData.Count;
                chunkData.Add(array);
            }

            chunks[coord] = index;
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
                ms.Write(chunkData[i]);
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