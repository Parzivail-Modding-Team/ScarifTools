using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using Substrate.Nbt;

namespace ScarifTools;

public record Region(Coord2 Pos, Dictionary<Coord2, Chunk> Chunks)
{
    public Chunk? GetChunk(Coord2 chunkPos)
    {
        return Chunks.GetValueOrDefault(chunkPos);
    }

    public BlockState? GetBlock(Coord3 blockPos)
    {
        var chunk = GetChunk((blockPos >> 4).Flatten());
        return chunk?.GetBlock(blockPos);
    }

    public static Region Load(string filename)
    {
        var chunks = new Dictionary<Coord2, Chunk>();

        using var file = new BinaryReader(File.Open(filename, FileMode.Open));

        const int regionSizeChunks = 32 * 32;
        var chunkLocations = file.ReadBytes(regionSizeChunks * sizeof(int));
        var chunkTimestamps = file.ReadBytes(regionSizeChunks * sizeof(int));

        for (var headerPosition = 0; headerPosition < regionSizeChunks * sizeof(int); headerPosition += sizeof(int))
        {
            //Get info about the chunk in the file
            var chunkOffsetInfo = new byte[4];
            chunkOffsetInfo[0] = 0;
            Array.Copy(chunkLocations, headerPosition, chunkOffsetInfo, 1, 3);

            var chunkOffset = BitConverter.ToInt32(BitConverter.IsLittleEndian ? chunkOffsetInfo.Reverse().ToArray() : chunkOffsetInfo, 0) * regionSizeChunks * sizeof(int);
            var chunkSectors = chunkLocations[headerPosition + 3];

            var chunkTimestampInfo = new byte[4];
            Array.Copy(chunkTimestamps, headerPosition, chunkTimestampInfo, 0, 4);

            var chunkTimestamp = BitConverter.ToInt32(BitConverter.IsLittleEndian ? chunkTimestampInfo.Reverse().ToArray() : chunkTimestampInfo, 0);

            if (chunkOffset < 2 || chunkSectors < 1)
                continue; // Nonexistent or invalid chunk

            //Read the chunk info
            file.BaseStream.Seek(chunkOffset, SeekOrigin.Begin);

            var chunkLengthInfo = file.ReadBytes(4);
            var chunkLength = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? chunkLengthInfo.Reverse().ToArray() : chunkLengthInfo, 0);
            var chunkCompressionType = file.ReadByte();
            var compressedChunkData = file.ReadBytes((int)chunkLength - 1);

            var uncompressedChunkData = new MemoryStream();

            switch (chunkCompressionType)
            {
                case 1:
                    new GZipStream(new MemoryStream(compressedChunkData), CompressionMode.Decompress).CopyTo(uncompressedChunkData);
                    break;

                case 2:
                    new ZLibStream(new MemoryStream(compressedChunkData), CompressionMode.Decompress).CopyTo(uncompressedChunkData);
                    break;

                default:
                    throw new Exception("Unrecognized compression type");
            }

            uncompressedChunkData.Seek(0, SeekOrigin.Begin);

            var chunk = Chunk.Load(new NbtTree(uncompressedChunkData));
            if (chunk == null)
                continue;

            chunks.Add(chunk.Pos, chunk);
        }

        var parts = Path.GetFileNameWithoutExtension(filename).Split('.');

        var x = parts[1];
        var z = parts[2];

        return new Region(new Coord2(int.Parse(x), int.Parse(z)), chunks);
    }
}