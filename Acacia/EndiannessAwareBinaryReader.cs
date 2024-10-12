using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Acacia;

public class EndiannessAwareBinaryReader : BinaryReader
{
	private delegate T Reader<out T>(ReadOnlySpan<byte> buffer);

	public Endianness StreamEndianness { get; }

	public EndiannessAwareBinaryReader(Stream input, Endianness endianness) : base(input)
	{
		StreamEndianness = endianness;
	}

	private T Read<T>(Reader<T> bigEndianReader, Reader<T> littleEndianReader)
	{
		var size = Marshal.SizeOf<T>();
		Span<byte> buffer = stackalloc byte[size];
		BaseStream.ReadExactly(buffer);
		return StreamEndianness == Endianness.Big ? bigEndianReader(buffer) : littleEndianReader(buffer);
	}

	public override string ReadString() => throw new NotSupportedException();

	public string ReadUshortPrefixedUtf8()
	{
		var length = ReadUInt16();
		var bytes = ReadBytes(length);
		return Encoding.UTF8.GetString(bytes);
	}

	public override ushort ReadUInt16() => Read(BinaryPrimitives.ReadUInt16BigEndian, BinaryPrimitives.ReadUInt16LittleEndian);

	public override short ReadInt16() => Read(BinaryPrimitives.ReadInt16BigEndian, BinaryPrimitives.ReadInt16LittleEndian);

	public override uint ReadUInt32() => Read(BinaryPrimitives.ReadUInt32BigEndian, BinaryPrimitives.ReadUInt32LittleEndian);

	public override int ReadInt32() => Read(BinaryPrimitives.ReadInt32BigEndian, BinaryPrimitives.ReadInt32LittleEndian);

	public override ulong ReadUInt64() => Read(BinaryPrimitives.ReadUInt64BigEndian, BinaryPrimitives.ReadUInt64LittleEndian);

	public override long ReadInt64() => Read(BinaryPrimitives.ReadInt64BigEndian, BinaryPrimitives.ReadInt64LittleEndian);

	public override float ReadSingle() => Read(BinaryPrimitives.ReadSingleBigEndian, BinaryPrimitives.ReadSingleLittleEndian);

	public override double ReadDouble() => Read(BinaryPrimitives.ReadDoubleBigEndian, BinaryPrimitives.ReadDoubleLittleEndian);

	public override decimal ReadDecimal() => throw new NotSupportedException();
	
	/// <summary>
	/// </summary>
	/// <remarks>
	/// This method will NOT respect the stream endianness. It should only be used for reading
	/// endianness-agnostic data.
	/// </remarks>
	/// <param name="count"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T[] ReadStructs<T>(int count) where T : struct
	{
		if (count == 0)
			return [];
		
		var a = new T[count];
		BaseStream.ReadExactly(GetStructSpan(ref a[0], count));
		return a;
	}

	/// <summary>
	/// </summary>
	/// <remarks>
	/// This method will NOT respect the stream endianness. It should only be used for reading
	/// endianness-agnostic data.
	/// </remarks>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T ReadStruct<T>() where T : struct
	{
		var s = new T();
		BaseStream.ReadExactly(GetStructSpan(ref s));
		return s;
	}
	
	private static Span<byte> GetStructSpan<T>(ref T data, int count = 1) where T : struct
	{
		return MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref data, count));
	}
}