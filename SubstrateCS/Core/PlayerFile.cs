﻿using System.IO;

namespace Substrate.Core;

public class PlayerFile : NBTFile
{
    public PlayerFile(string path)
        : base(path)
    {
    }

    public PlayerFile(string path, string name)
        : base("")
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var file = name + ".dat";
        FileName = Path.Combine(path, file);
    }

    public static string NameFromFilename(string filename)
    {
        if (filename.EndsWith(".dat"))
        {
            return filename.Remove(filename.Length - 4);
        }

        return filename;
    }
}