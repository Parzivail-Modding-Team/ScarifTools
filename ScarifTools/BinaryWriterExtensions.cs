﻿using System.IO;
using Substrate.Nbt;

namespace ScarifTools;

internal static class BinaryWriterExtensions
{
    public static void WriteNbt(this BinaryWriter bw, TagNodeCompound tag)
    {
        using var ms = new MemoryStream();
        new NbtTree(tag).WriteTo(ms);

        bw.Write7BitEncodedInt((int)ms.Length);
        bw.Write(ms.ToArray());
    }
}