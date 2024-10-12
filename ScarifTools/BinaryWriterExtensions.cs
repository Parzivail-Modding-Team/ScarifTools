using System.IO;
using Acacia;

namespace ScarifTools;

internal static class BinaryWriterExtensions
{
    public static void WriteNbt(this BinaryWriter bw, NbtCompound tag)
    {
        using var ms = new MemoryStream();
        NbtWriter.WriteUncompressedRoot(ms, tag);

        bw.Write7BitEncodedInt((int)ms.Length);
        bw.Write(ms.ToArray());
    }
}