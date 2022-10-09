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
			bw.Write(s.AsSpan());
			bw.Write((byte)0);
		}

		public static void WriteNbt(this BinaryWriter bw, TagNodeCompound tag)
		{
			using var ms = new MemoryStream();
			new NbtTree(tag).WriteTo(ms);

			bw.Write((int)ms.Length);

			ms.Seek(0, SeekOrigin.Begin);
			ms.CopyTo(bw.BaseStream);
		}
	}
}
