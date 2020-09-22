using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zlib;
using Substrate.Nbt;

namespace ScarifTools
{
	internal class Region
	{
		public readonly Coord2 Pos;
		public readonly Dictionary<Coord2, Chunk> Chunks;

		private Region(Coord2 pos, Dictionary<Coord2, Chunk> chunks)
		{
			Pos = pos;
			Chunks = chunks;
		}

		public Chunk GetChunk(Coord2 chunkPos)
		{
			return Chunks.ContainsKey(chunkPos) ? Chunks[chunkPos] : null;
		}

		public BlockState GetBlock(Coord3 blockPos)
		{
			var chunk = GetChunk((blockPos >> 4).Flatten());
			return chunk == null ? null : chunk.GetBlock(blockPos);
		}

		public static Region Load(string filename)
		{
			var chunks = new Dictionary<Coord2, Chunk>();

			using var file = new BinaryReader(File.Open(filename, FileMode.Open));

			var chunkLocations = new byte[4096];
			var chunkTimestamps = new byte[4096];

			file.Read(chunkLocations, 0, 4096);
			file.Read(chunkTimestamps, 0, 4096);

			for (var headerPosition = 0; headerPosition < 4096; headerPosition += 4) // 32*32 chunks
			{
				//Get info about the chunk in the file
				var chunkOffsetInfo = new byte[4];
				chunkOffsetInfo[0] = 0;
				Array.Copy(chunkLocations, headerPosition, chunkOffsetInfo, 1, 3);

				long chunkOffset = BitConverter.ToInt32(BitConverter.IsLittleEndian ? chunkOffsetInfo.Reverse().ToArray() : chunkOffsetInfo, 0) * 4096;
				var chunkSectors = chunkLocations[headerPosition + 3];

				var chunkTimestampInfo = new byte[4];
				Array.Copy(chunkTimestamps, headerPosition, chunkTimestampInfo, 0, 4);

				var chunkTimestamp = BitConverter.ToInt32(BitConverter.IsLittleEndian ? chunkTimestampInfo.Reverse().ToArray() : chunkTimestampInfo, 0);

				if (chunkOffset < 2 || chunkSectors < 1) continue; //Non existing or invalid chunk

				//Read the chunk info
				file.BaseStream.Seek(chunkOffset, SeekOrigin.Begin);

				var chunkLengthInfo = new byte[4];
				file.Read(chunkLengthInfo, 0, 4);

				var chunkLength = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? chunkLengthInfo.Reverse().ToArray() : chunkLengthInfo, 0);

				var chunkCompressionType = file.ReadByte();

				var compressedChunkData = new byte[chunkLength - 1];
				file.Read(compressedChunkData, 0, (int)chunkLength - 1);

				var uncompressedChunkData = new MemoryStream();

				switch (chunkCompressionType)
				{
					case 1:
						new GZipStream(new MemoryStream(compressedChunkData), CompressionMode.Decompress).CopyTo(uncompressedChunkData);
						break;

					case 2:
						new ZlibStream(new MemoryStream(compressedChunkData), CompressionMode.Decompress).CopyTo(uncompressedChunkData);
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
}