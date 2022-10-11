using System;
using System.IO;
using System.IO.Compression;
using Substrate.Nbt;

namespace Substrate.Core;

public enum CompressionType
{
    None,
    Zlib,
    Deflate,
    GZip,
}

public class NBTFile
{
    public NBTFile(string path)
    {
        FileName = path;
    }

    public string FileName { get; protected init; }

    public bool Exists()
    {
        return File.Exists(FileName);
    }

    public void Delete()
    {
        File.Delete(FileName);
    }

    public int GetModifiedTime()
    {
        return Timestamp(File.GetLastWriteTime(FileName));
    }

    public virtual Stream GetDataInputStream(CompressionType compression = CompressionType.GZip)
    {
        try
        {
            switch (compression)
            {
                case CompressionType.None:
                    using (var fstr = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var length = fstr.Seek(0, SeekOrigin.End);
                        fstr.Seek(0, SeekOrigin.Begin);

                        var data = new byte[length];
                        fstr.Read(data, 0, data.Length);

                        return new MemoryStream(data);
                    }
                case CompressionType.GZip:
                    Stream stream1 = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return new GZipStream(stream1, CompressionMode.Decompress);
                case CompressionType.Zlib:
                    Stream stream2 = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return new ZLibStream(stream2, CompressionMode.Decompress);
                case CompressionType.Deflate:
                    Stream stream3 = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return new DeflateStream(stream3, CompressionMode.Decompress);
                default:
                    throw new ArgumentException("Invalid CompressionType specified", "compression");
            }
        }
        catch (Exception ex)
        {
            throw new NbtIOException("Failed to open compressed NBT data stream for input.", ex);
        }
    }

    public Stream GetDataOutputStream()
    {
        return GetDataOutputStream(CompressionType.GZip);
    }

    public virtual Stream GetDataOutputStream(CompressionType compression)
    {
        try
        {
            switch (compression)
            {
                case CompressionType.None:
                    return new NBTBuffer(this);
                case CompressionType.GZip:
                    return new GZipStream(new NBTBuffer(this), CompressionMode.Compress);
                case CompressionType.Zlib:
                    return new ZLibStream(new NBTBuffer(this), CompressionMode.Compress);
                case CompressionType.Deflate:
                    return new DeflateStream(new NBTBuffer(this), CompressionMode.Compress);
                default:
                    throw new ArgumentException("Invalid CompressionType specified", nameof(compression));
            }
        }
        catch (Exception ex)
        {
            throw new NbtIOException("Failed to initialize compressed NBT data stream for output.", ex);
        }
    }

    class NBTBuffer : MemoryStream
    {
        private NBTFile file;

        public NBTBuffer(NBTFile c)
            : base(8096)
        {
            this.file = c;
        }

        public override void Close()
        {
            try
            {
                using (Stream fstr = new FileStream(file.FileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    try
                    {
                        fstr.Write(this.GetBuffer(), 0, (int)this.Length);
                    }
                    catch (Exception ex)
                    {
                        throw new NbtIOException("Failed to write out NBT data stream.", ex);
                    }
                }
            }
            catch (NbtIOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new NbtIOException("Failed to open NBT data stream for output.", ex);
            }
        }
    }

    private int Timestamp(DateTime time)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return (int)((time - epoch).Ticks / (10000L * 1000L));
    }
}