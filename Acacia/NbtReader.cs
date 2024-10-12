using System.IO.Compression;

namespace Acacia;

public class NbtReader
{
	private static Stream GetReader(Stream source, CompressionType compression = CompressionType.GZip, bool leaveOpen = false)
	{
		try
		{
			return compression switch
			{
				CompressionType.None => source,
				CompressionType.GZip => new GZipStream(source, CompressionMode.Decompress, leaveOpen),
				CompressionType.ZLib => new ZLibStream(source, CompressionMode.Decompress, leaveOpen),
				CompressionType.Deflate => new DeflateStream(source, CompressionMode.Decompress, leaveOpen),
				_ => throw new ArgumentException("Invalid compression type", nameof(compression))
			};
		}
		catch (Exception ex)
		{
			throw new NbtIOException("Failed to open compressed NBT data stream for input.", ex);
		}
	}
	
	public static NbtElement ReadFile(string filename, CompressionType compression = CompressionType.GZip, Endianness endianness = Endianness.Big)
	{
		return ReadCompressedRoot(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), out _, compression, endianness);
	}
	
	public static NbtElement ReadCompressedRoot(Stream stream, out string? name, CompressionType compression = CompressionType.GZip, Endianness endianness = Endianness.Big, bool leaveOpen = false)
	{
		using var reader = GetReader(stream, compression, leaveOpen);
		var br = new EndiannessAwareBinaryReader(reader, endianness);
		(name, var element) = ReadNamedElement(br);
		return element;
	}
	
	public static NbtElement ReadUncompressedRoot(Stream stream, out string? name, Endianness endianness = Endianness.Big)
	{
		var br = new EndiannessAwareBinaryReader(stream, endianness);
		(name, var element) = ReadNamedElement(br);
		return element;
	}

	public static NbtCompound ReadCompound(EndiannessAwareBinaryReader br)
	{
		var elements = new Dictionary<string, NbtElement>();

		while (true)
		{
			var (elementName, element) = ReadNamedElement(br);
			if (element is NbtEnd)
				break;

			elements[elementName!] = element;
		}

		return new NbtCompound(elements);
	}

	public static NbtList ReadList(EndiannessAwareBinaryReader br)
	{
		var elementType = (TagType)br.ReadByte();
		var listLength = br.ReadInt32();

		var elements = new List<NbtElement>(listLength);

		for (var i = 0; i < listLength; i++)
		{
			var element = ReadElement(br, elementType);
			elements.Add(element);
		}

		return new NbtList(elementType, elements);
	}

	public static NbtByteArray ReadByteArray(EndiannessAwareBinaryReader br)
	{
		var listLength = br.ReadInt32();
		return new NbtByteArray(br.ReadStructs<sbyte>(listLength));
	}

	public static NbtIntArray ReadIntArray(EndiannessAwareBinaryReader br)
	{
		var listLength = br.ReadInt32();
		var elements = new int[listLength];

		for (var i = 0; i < listLength; i++) elements[i] = br.ReadInt32();

		return new NbtIntArray(elements);
	}

	public static NbtLongArray ReadLongArray(EndiannessAwareBinaryReader br)
	{
		var listLength = br.ReadInt32();
		var elements = new long[listLength];

		for (var i = 0; i < listLength; i++) elements[i] = br.ReadInt64();

		return new NbtLongArray(elements);
	}

	public static (string? Name, NbtElement Element) ReadNamedElement(EndiannessAwareBinaryReader br, TagType? type = null)
	{
		type ??= (TagType)br.ReadByte();

		var name = ReadName(br, type.Value);

		return (name, ReadElement(br, type.Value));
	}

	private static string? ReadName(EndiannessAwareBinaryReader br, TagType type)
	{
		return type switch
		{
			TagType.End => null,
			_ => br.ReadUshortPrefixedUtf8()
		};
	}

	public static NbtElement ReadElement(EndiannessAwareBinaryReader br, TagType type)
	{
		switch (type)
		{
			case TagType.End:
				return new NbtEnd();
			case TagType.Byte:
				return new NbtByte(br.ReadSByte());
			case TagType.Short:
				return new NbtShort(br.ReadInt16());
			case TagType.Int:
				return new NbtInt(br.ReadInt32());
			case TagType.Long:
				return new NbtLong(br.ReadInt64());
			case TagType.Float:
				return new NbtFloat(br.ReadSingle());
			case TagType.Double:
				return new NbtDouble(br.ReadDouble());
			case TagType.ByteArray:
				return ReadByteArray(br);
			case TagType.String:
				return new NbtString(br.ReadUshortPrefixedUtf8());
			case TagType.List:
				return ReadList(br);
			case TagType.Compound:
				return ReadCompound(br);
			case TagType.IntArray:
				return ReadIntArray(br);
			case TagType.LongArray:
				return ReadLongArray(br);
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}