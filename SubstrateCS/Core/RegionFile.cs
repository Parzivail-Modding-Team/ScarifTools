﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace Substrate.Core;

public class RegionFile : IDisposable
{
    private static Regex _namePattern = new Regex("r\\.(-?[0-9]+)\\.(-?[0-9]+)\\.mc[ar]$");

    private const int VERSION_GZIP = 1;
    private const int VERSION_DEFLATE = 2;

    private const int SECTOR_BYTES = 4096;
    private const int SECTOR_INTS = SECTOR_BYTES / 4;

    const int CHUNK_HEADER_SIZE = 5;

    private static byte[] emptySector = new byte[4096];

    protected string fileName;
    #nullable disable
    private FileStream file;
    #nullable enable

    /// <summary>
    /// The file lock used so that we do not seek in different areas
    /// of the same file at the same time. All file access should lock this
    /// object before moving the file pointer.
    /// The lock should also surround all access to the sectorFree free variable.
    /// </summary>
    private object fileLock = new object();

    private int[] offsets;
    private int[] chunkTimestamps;
    #nullable disable
    private List<bool> sectorFree;
    #nullable enable
    private int sizeDelta;
    private long lastModified = 0;

    private bool _disposed = false;

    public RegionFile(string path)
    {
        offsets = new int[SectorInts];
        chunkTimestamps = new int[SectorInts];

        fileName = path;
        Debugln("REGION LOAD " + fileName);

        sizeDelta = 0;

        ReadFile();
    }

    ~RegionFile()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup managed resources
            }

            // Cleanup unmanaged resources
            if (file != null)
            {
                lock (this.fileLock)
                {
                    file.Close();
                    file = null;
                }
            }
        }

        _disposed = true;
    }

    protected void ReadFile()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("RegionFile", "Attempting to use a RegionFile after it has been disposed.");
        }

        // Get last udpate time
        long newModified = -1;
        try
        {
            if (File.Exists(fileName))
            {
                newModified = Timestamp(File.GetLastWriteTime(fileName));
            }
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine(e.Message);
            return;
        }

        // If it hasn't been modified, we don't need to do anything
        if (newModified == lastModified)
        {
            return;
        }

        try
        {
            lock (this.fileLock)
            {
                file = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                //using (file) {
                if (file.Length < SectorBytes)
                {
                    var int0 = BitConverter.GetBytes((int)0);

                    /* we need to write the chunk offset table */
                    for (var i = 0; i < SectorInts; ++i)
                    {
                        file.Write(int0, 0, 4);
                    }

                    // write another sector for the timestamp info
                    for (var i = 0; i < SectorInts; ++i)
                    {
                        file.Write(int0, 0, 4);
                    }

                    file.Flush();

                    sizeDelta += SectorBytes * 2;
                }

                if ((file.Length & 0xfff) != 0)
                {
                    /* the file size is not a multiple of 4KB, grow it */
                    file.Seek(0, SeekOrigin.End);
                    for (var i = 0; i < (file.Length & 0xfff); ++i)
                    {
                        file.WriteByte(0);
                    }

                    file.Flush();
                }

                /* set up the available sector map */
                var nSectors = (int)file.Length / SectorBytes;
                sectorFree = new List<bool>(nSectors);

                for (var i = 0; i < nSectors; ++i)
                {
                    sectorFree.Add(true);
                }

                sectorFree[0] = false; // chunk offset table
                sectorFree[1] = false; // for the last modified info

                file.Seek(0, SeekOrigin.Begin);
                for (var i = 0; i < SectorInts; ++i)
                {
                    var offsetBytes = new byte[4];
                    file.Read(offsetBytes, 0, 4);

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(offsetBytes);
                    }

                    var offset = BitConverter.ToInt32(offsetBytes, 0);

                    offsets[i] = offset;
                    if (offset != 0 && (offset >> 8) + (offset & 0xFF) <= sectorFree.Count)
                    {
                        for (var sectorNum = 0; sectorNum < (offset & 0xFF); ++sectorNum)
                        {
                            sectorFree[(offset >> 8) + sectorNum] = false;
                        }
                    }
                }

                for (var i = 0; i < SectorInts; ++i)
                {
                    var modBytes = new byte[4];
                    file.Read(modBytes, 0, 4);

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(modBytes);
                    }

                    var lastModValue = BitConverter.ToInt32(modBytes, 0);

                    chunkTimestamps[i] = lastModValue;
                }
            }
        }
        catch (IOException e)
        {
            System.Console.WriteLine(e.Message);
            System.Console.WriteLine(e.StackTrace);
        }
    }

    /* the modification date of the region file when it was first opened */
    public long LastModified()
    {
        return lastModified;
    }

    /* gets how much the region file has grown since it was last checked */
    public int GetSizeDelta()
    {
        var ret = sizeDelta;
        sizeDelta = 0;
        return ret;
    }

    // various small debug printing helpers
    private void Debug(string str)
    {
        //        System.Consle.Write(str);
    }

    private void Debugln(string str)
    {
        Debug(str + "\n");
    }

    private void Debug(string mode, int x, int z, string str)
    {
        Debug("REGION " + mode + " " + fileName + "[" + x + "," + z + "] = " + str);
    }

    private void Debug(string mode, int x, int z, int count, string str)
    {
        Debug("REGION " + mode + " " + fileName + "[" + x + "," + z + "] " + count + "B = " + str);
    }

    private void Debugln(string mode, int x, int z, string str)
    {
        Debug(mode, x, z, str + "\n");
    }

    /*
     * gets an (uncompressed) stream representing the chunk data returns null if
     * the chunk is not found or an error occurs
     */
    public Stream? GetChunkDataInputStream(int x, int z)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("RegionFile", "Attempting to use a RegionFile after it has been disposed.");
        }

        if (OutOfBounds(x, z))
        {
            Debugln("READ", x, z, "out of bounds");
            return null;
        }

        try
        {
            var offset = GetOffset(x, z);
            if (offset == 0)
            {
                // Debugln("READ", x, z, "miss");
                return null;
            }

            var sectorNumber = offset >> 8;
            var numSectors = offset & 0xFF;

            lock (this.fileLock)
            {
                if (sectorNumber + numSectors > sectorFree.Count)
                {
                    Debugln("READ", x, z, "invalid sector");
                    return null;
                }

                file.Seek(sectorNumber * SectorBytes, SeekOrigin.Begin);
                var lengthBytes = new byte[4];
                file.Read(lengthBytes, 0, 4);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthBytes);
                }

                var length = BitConverter.ToInt32(lengthBytes, 0);

                if (length > SectorBytes * numSectors)
                {
                    Debugln("READ", x, z, "invalid length: " + length + " > 4096 * " + numSectors);
                    return null;
                }

                var version = (byte)file.ReadByte();
                if (version == VERSION_GZIP)
                {
                    var data = new byte[length - 1];
                    file.Read(data, 0, data.Length);
                    Stream ret = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);

                    return ret;
                }
                else if (version == VERSION_DEFLATE)
                {
                    var data = new byte[length - 1];
                    file.Read(data, 0, data.Length);

                    Stream ret = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress, true);
                    return ret;

                    /*MemoryStream sinkZ = new MemoryStream();
                    ZlibStream zOut = new ZlibStream(sinkZ, CompressionMode.Decompress, true);
                    zOut.Write(data, 0, data.Length);
                    zOut.Flush();
                    zOut.Close();

                    sinkZ.Seek(0, SeekOrigin.Begin);
                    return sinkZ;*/
                }

                Debugln("READ", x, z, "unknown version " + version);
                return null;
            }
        }
        catch (IOException)
        {
            Debugln("READ", x, z, "exception");
            return null;
        }
    }

    public Stream? GetChunkDataOutputStream(int x, int z)
    {
        if (OutOfBounds(x, z)) return null;

        return new ZLibStream(new ChunkBuffer(this, x, z), CompressionMode.Compress);
    }

    public Stream? GetChunkDataOutputStream(int x, int z, int timestamp)
    {
        if (OutOfBounds(x, z)) return null;

        return new ZLibStream(new ChunkBuffer(this, x, z, timestamp), CompressionMode.Compress);
    }

    /*
     * lets chunk writing be multithreaded by not locking the whole file as a
     * chunk is serializing -- only writes when serialization is over
     */
    class ChunkBuffer : MemoryStream
    {
        private int x, z;
        private RegionFile region;

        private int? _timestamp;

        public ChunkBuffer(RegionFile r, int x, int z)
            : base(8096)
        {
            this.region = r;
            this.x = x;
            this.z = z;
        }

        public ChunkBuffer(RegionFile r, int x, int z, int timestamp)
            : this(r, x, z)
        {
            _timestamp = timestamp;
        }

        public override void Close()
        {
            if (_timestamp == null)
            {
                region.Write(x, z, this.GetBuffer(), (int)this.Length);
            }
            else
            {
                region.Write(x, z, this.GetBuffer(), (int)this.Length, (int)_timestamp);
            }
        }
    }

    protected void Write(int x, int z, byte[] data, int length)
    {
        Write(x, z, data, length, Timestamp());
    }

    /* write a chunk at (x,z) with length bytes of data to disk */
    protected void Write(int x, int z, byte[] data, int length, int timestamp)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("RegionFile", "Attempting to use a RegionFile after it has been disposed.");
        }

        try
        {
            var offset = GetOffset(x, z);
            var sectorNumber = offset >> 8;
            var sectorsAllocated = offset & 0xFF;
            var sectorsNeeded = (length + CHUNK_HEADER_SIZE) / SectorBytes + 1;

            // maximum chunk size is 1MB
            if (sectorsNeeded >= 256)
            {
                return;
            }

            if (sectorNumber != 0 && sectorsAllocated == sectorsNeeded)
            {
                /* we can simply overwrite the old sectors */
                Debug("SAVE", x, z, length, "rewrite");
                Write(sectorNumber, data, length);
            }
            else
            {
                /* we need to allocate new sectors */

                lock (this.fileLock)
                {
                    /* mark the sectors previously used for this chunk as free */
                    for (var i = 0; i < sectorsAllocated; ++i)
                    {
                        sectorFree[sectorNumber + i] = true;
                    }

                    /* scan for a free space large enough to store this chunk */
                    var runStart = sectorFree.FindIndex(b => b == true);
                    var runLength = 0;
                    if (runStart != -1)
                    {
                        for (var i = runStart; i < sectorFree.Count; ++i)
                        {
                            if (runLength != 0)
                            {
                                if (sectorFree[i]) runLength++;
                                else runLength = 0;
                            }
                            else if (sectorFree[i])
                            {
                                runStart = i;
                                runLength = 1;
                            }

                            if (runLength >= sectorsNeeded)
                            {
                                break;
                            }
                        }
                    }

                    if (runLength >= sectorsNeeded)
                    {
                        /* we found a free space large enough */
                        Debug("SAVE", x, z, length, "reuse");
                        sectorNumber = runStart;
                        SetOffset(x, z, (sectorNumber << 8) | sectorsNeeded);
                        for (var i = 0; i < sectorsNeeded; ++i)
                        {
                            sectorFree[sectorNumber + i] = false;
                        }

                        Write(sectorNumber, data, length);
                    }
                    else
                    {
                        /*
                         * no free space large enough found -- we need to grow the
                         * file
                         */
                        Debug("SAVE", x, z, length, "grow");
                        file.Seek(0, SeekOrigin.End);
                        sectorNumber = sectorFree.Count;
                        for (var i = 0; i < sectorsNeeded; ++i)
                        {
                            file.Write(emptySector, 0, emptySector.Length);
                            sectorFree.Add(false);
                        }

                        sizeDelta += SectorBytes * sectorsNeeded;

                        Write(sectorNumber, data, length);
                        SetOffset(x, z, (sectorNumber << 8) | sectorsNeeded);
                    }
                }
            }

            SetTimestamp(x, z, timestamp);
        }
        catch (IOException e)
        {
            Console.WriteLine(e.StackTrace);
        }
    }

    /* write a chunk data to the region file at specified sector number */
    private void Write(int sectorNumber, byte[] data, int length)
    {
        lock (this.fileLock)
        {
            Debugln(" " + sectorNumber);
            file.Seek(sectorNumber * SectorBytes, SeekOrigin.Begin);

            var bytes = BitConverter.GetBytes(length + 1);
            if (BitConverter.IsLittleEndian)
            {
                ;
                Array.Reverse(bytes);
            }

            file.Write(bytes, 0, 4); // chunk length
            file.WriteByte(VERSION_DEFLATE); // chunk version number
            file.Write(data, 0, length); // chunk data
        }
    }

    public void DeleteChunk(int x, int z)
    {
        lock (this.fileLock)
        {
            var offset = GetOffset(x, z);
            var sectorNumber = offset >> 8;
            var sectorsAllocated = offset & 0xFF;

            file.Seek(sectorNumber * SectorBytes, SeekOrigin.Begin);
            for (var i = 0; i < sectorsAllocated; i++)
            {
                file.Write(emptySector, 0, SectorBytes);
            }

            SetOffset(x, z, 0);
            SetTimestamp(x, z, 0);
        }
    }

    /* is this an invalid chunk coordinate? */
    private bool OutOfBounds(int x, int z)
    {
        return x < 0 || x >= 32 || z < 0 || z >= 32;
    }

    private int GetOffset(int x, int z)
    {
        return offsets[x + z * 32];
    }

    public bool HasChunk(int x, int z)
    {
        return GetOffset(x, z) != 0;
    }

    private void SetOffset(int x, int z, int offset)
    {
        lock (this.fileLock)
        {
            offsets[x + z * 32] = offset;
            file.Seek((x + z * 32) * 4, SeekOrigin.Begin);

            var bytes = BitConverter.GetBytes(offset);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            file.Write(bytes, 0, 4);
        }
    }

    private int Timestamp()
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return (int)((DateTime.UtcNow - epoch).Ticks / (10000L * 1000L));
    }

    private int Timestamp(DateTime time)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return (int)((time - epoch).Ticks / (10000L * 1000L));
    }

    public int GetTimestamp(int x, int z)
    {
        return chunkTimestamps[x + z * 32];
    }

    public void SetTimestamp(int x, int z, int value)
    {
        lock (this.fileLock)
        {
            chunkTimestamps[x + z * 32] = value;
            file.Seek(SectorBytes + (x + z * 32) * 4, SeekOrigin.Begin);

            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                ;
                Array.Reverse(bytes);
            }

            file.Write(bytes, 0, 4);
        }
    }

    public void Close()
    {
        lock (this.fileLock)
        {
            file.Close();
        }
    }

    public virtual RegionKey parseCoordinatesFromName()
    {
        var x = 0;
        var z = 0;

        var match = _namePattern.Match(fileName);
        if (!match.Success)
        {
            return RegionKey.InvalidRegion;
        }

        x = Convert.ToInt32(match.Groups[1].Value);
        z = Convert.ToInt32(match.Groups[2].Value);

        return new RegionKey(x, z);
    }

    protected virtual int SectorBytes => SECTOR_BYTES;

    protected virtual int SectorInts => SECTOR_BYTES / 4;

    protected virtual byte[] EmptySector => emptySector;
}