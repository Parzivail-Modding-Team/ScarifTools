using System.Collections.Generic;
using System.IO;
using System.Linq;
using Acacia;

namespace ScarifTools;

internal readonly struct World
{
	private readonly string _worldPath;
	private readonly Dictionary<RegionId, Region?> _loadedRegions;
	public readonly int DataVersion;

	public World(string worldPath, int dataVersion)
	{
		_worldPath = worldPath;
		DataVersion = dataVersion;

		_loadedRegions = new Dictionary<RegionId, Region?>();
	}

	public static World Load(string worldPath)
	{
		var tree = NbtReader.ReadFile(Path.Combine(worldPath, "level.dat"));

		if (tree is not NbtCompound root)
			throw new InvalidDataException("Level data was not NbtCompound");

		var data = root.GetCompound("Data");
		var dataVersion = data.GetInt("DataVersion");

		return new World(worldPath, dataVersion);
	}

	public List<RegionId> GetRegions(string? dimension = null)
	{
		var path = dimension == null ? Path.Combine(_worldPath, "region") : Path.Combine(_worldPath, dimension, "region");

		return Directory.GetFiles(path, "*.mca")
			.Select(Path.GetFileNameWithoutExtension)
			.Select(name => name.Split('.'))
			.Select(parts => new RegionId(new Coord2(int.Parse(parts[1]), int.Parse(parts[2])), dimension))
			.ToList();
	}

	public BlockState? GetBlock(Coord3 blockPos, string? dimension = null, bool cacheRegion = true)
	{
		var region = GetRegion((blockPos >> 9).Flatten(), dimension, cacheRegion);
		return region?.GetBlock(blockPos);
	}

	public Region? GetRegion(Coord2 pos, string? dimension = null, bool cache = true)
	{
		return GetRegion(new RegionId(pos, dimension), cache);
	}

	public Region? GetRegion(RegionId regionId, bool cache = true)
	{
		var (pos, dimension) = regionId;

		if (_loadedRegions.TryGetValue(regionId, out var region))
			return region;

		var path = dimension == null ? Path.Combine(_worldPath, "region") : Path.Combine(_worldPath, dimension, "region");

		var regionPath = Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
		if (!File.Exists(regionPath))
			return null;

		region = Region.Load(regionPath);

		if (cache)
			_loadedRegions[regionId] = region;

		return region;
	}
}