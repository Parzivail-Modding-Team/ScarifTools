using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Substrate.Nbt;

namespace ScarifTools
{
	internal static class BinaryWriterExtensions
	{
		public static void WriteNullTermString(this BinaryWriter bw, string s)
		{
			foreach (var c in s) bw.Write(c);
			bw.Write((byte)0);
		}

		public static void WriteNbt(this BinaryWriter bw, TagNodeCompound tag, MemoryStream ms)
		{
			ms.SetLength(0);
			new NbtTree(tag).WriteTo(ms);

			bw.Write(ms.Length);
			ms.CopyTo(bw.BaseStream);
		}

		public static void Write7BitEncodedInt(this BinaryWriter bw, int value)
		{
			// Write out an int 7 bits at a time.  The high bit of the byte,
			// when on, tells reader to continue reading more bytes.
			var v = (uint)value;   // support negative numbers
			while (v >= 0x80)
			{
				bw.Write((byte)(v | 0x80));
				v >>= 7;
			}
			bw.Write((byte)v);
		}
	}
}
