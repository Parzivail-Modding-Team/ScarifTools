using System.Collections.Generic;
using System.IO;
using System.Linq;
using Substrate.Core;
using Substrate.Nbt;

namespace ScarifTools;

internal readonly struct World
{
    private readonly string _worldPath;
    private readonly Dictionary<RegionId, Region> _loadedRegions;
    public readonly int DataVersion;

    public World(string worldPath, int dataVersion)
    {
        _worldPath = worldPath;
        DataVersion = dataVersion;

        _loadedRegions = new Dictionary<RegionId, Region>();
    }

    public static World Load(string worldPath)
    {
        var nf = new NBTFile(Path.Combine(worldPath, "level.dat"));

        using var nbtstr = nf.GetDataInputStream();
        var tree = new NbtTree(nbtstr);

        var root = tree.Root["Data"].ToTagCompound();

        var dataVersion = root["DataVersion"].ToTagInt().Data;

        return new World(worldPath, dataVersion);
    }

    public List<RegionId> GetRegions(string? dimension = null)
    {
        var path = dimension == null ? Path.Combine(_worldPath, "region") : Path.Combine(_worldPath, dimension, "region");

        return Directory.GetFiles(path, "*.mca")
            .Select(regionFile => Path.GetFileNameWithoutExtension(regionFile))
            .Select(name => name.Split('.'))
            .Select(parts => new RegionId(new Coord2(int.Parse(parts[1]), int.Parse(parts[2])), dimension))
            .ToList();
    }

    public BlockState? GetBlock(Coord3 blockPos, string? dimension = null)
    {
        var region = GetRegion((blockPos >> 9).Flatten(), dimension);
        return region?.GetBlock(blockPos);
    }

    public Region? GetRegion(Coord2 pos, string? dimension = null)
    {
        return GetRegion(new RegionId(pos, dimension));
    }

    public Region? GetRegion(RegionId regionId)
    {
        var (pos, dimension) = regionId;

        if (_loadedRegions.ContainsKey(regionId))
            return _loadedRegions[regionId];

        var path = dimension == null ? Path.Combine(_worldPath, "region") : Path.Combine(_worldPath, dimension, "region");

        var regionPath = Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
        if (!File.Exists(regionPath))
            return null;

        var region = Region.Load(regionPath);
        _loadedRegions[regionId] = region;

        return region;
    }
}