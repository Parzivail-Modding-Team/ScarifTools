using System.IO.Compression;

namespace Acacia;

public class NbtWriter
{
	private static Stream GetWriter(Stream destination, CompressionType compression = CompressionType.GZip, CompressionLevel compressionLevel = CompressionLevel.Optimal, bool leaveOpen = false)
	{
		try
		{
			return compression switch
			{
				CompressionType.None => destination,
				CompressionType.GZip => new GZipStream(destination, compressionLevel, leaveOpen),
				CompressionType.ZLib => new ZLibStream(destination, compressionLevel, leaveOpen),
				CompressionType.Deflate => new DeflateStream(destination, compressionLevel, leaveOpen),
				_ => throw new ArgumentException("Invalid compression type", nameof(compression))
			};
		}
		catch (Exception ex)
		{
			throw new NbtIOException("Failed to initialize compressed NBT data stream for output.", ex);
		}
	}
	
	public static void WriteUncompressedRoot(Stream stream, NbtElement element, string name = "", Endianness endianness = Endianness.Big)
	{
		throw new NotImplementedException();
	}
}