using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Acacia;
using Microsoft.IO;
using SubstreamSharp;

namespace ScarifTools;

public record Region(Coord2 Pos, Dictionary<Coord2, Chunk> Chunks)
{
    private static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new();
    
    public Chunk? GetChunk(Coord2 chunkPos)
    {
        return Chunks.GetValueOrDefault(chunkPos);
    }

    public BlockState? GetBlock(Coord3 blockPos)
    {
        var chunk = GetChunk((blockPos >> 4).Flatten());
        return chunk?.GetBlock(blockPos);
    }

    public static Region? Load(string filename)
    {
        var chunks = new Dictionary<Coord2, Chunk>();

        using var file = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length == 0)
            return null;
        
        var reader = new EndiannessAwareBinaryReader(file, Endianness.Big);

        const int regionSizeChunks = 32 * 32;
        var chunkLocations = reader.ReadBytes(regionSizeChunks * sizeof(int));
        var chunkTimestamps = reader.ReadBytes(regionSizeChunks * sizeof(int));

        var chunkLocationsSpan = chunkLocations.AsSpan();
        var chunkTimestampsSpan = chunkTimestamps.AsSpan();
        
        for (var headerPosition = 0; headerPosition < regionSizeChunks * sizeof(int); headerPosition += sizeof(int))
        {
            //Get info about the chunk in the file
            var chunkOffset = (BinaryPrimitives.ReadInt32BigEndian(chunkLocationsSpan[headerPosition..]) >> 8) * regionSizeChunks * sizeof(int);
            var chunkSectors = chunkLocationsSpan[headerPosition + 3];

            var chunkTimestamp = BinaryPrimitives.ReadInt32BigEndian(chunkTimestampsSpan[headerPosition..]);

            if (chunkOffset < 2 || chunkSectors < 1)
                continue; // Nonexistent or invalid chunk

            //Read the chunk info
            file.Seek(chunkOffset, SeekOrigin.Begin);

            var chunkLength = reader.ReadInt32();
            var chunkCompressionType = reader.ReadByte();

            var substream = new Substream(file, file.Position, chunkLength - 1);
            var decompressedChunkDataStream = RecyclableMemoryStreamManager.GetStream("decompressed_chunk");
            
            switch (chunkCompressionType)
            {
                case 1:
                {
                    using var gzip = new GZipStream(substream, CompressionMode.Decompress);
                    gzip.CopyTo(decompressedChunkDataStream);
                    break;
                }
                case 2:
                {
                    using var zlib = new ZLibStream(substream, CompressionMode.Decompress);
                    zlib.CopyTo(decompressedChunkDataStream);
                    break;
                }
                default:
                    throw new Exception("Unrecognized compression type");
            }

            decompressedChunkDataStream.Position = 0;
            var chunkElement = NbtReader.ReadUncompressedRoot(decompressedChunkDataStream, out _);
            var chunk = Chunk.Load(chunkElement);
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