using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Substrate.Core;
using Substrate.Nbt;

namespace ScarifTools
{
	internal readonly struct World
	{
		private readonly string _levelPath;
		private readonly Dictionary<RegionId, Region> _loadedRegions;
		public readonly int DataVersion;


		public World(string levelPath, int dataVersion)
		{
			_levelPath = levelPath;
			DataVersion = dataVersion;

			_loadedRegions = new Dictionary<RegionId, Region>();
		}

		public static World Load(string filename)
		{
			var nf = new NBTFile(filename);

			using var nbtstr = nf.GetDataInputStream();
			var tree = new NbtTree(nbtstr);

			var root = tree.Root["Data"].ToTagCompound();

			var dataVersion = root["DataVersion"].ToTagInt().Data;

			return new World(filename, dataVersion);
		}

		public List<RegionId> GetRegions(string dimension = null)
		{
			var path = Path.GetDirectoryName(_levelPath);
			if (path == null)
				return null;

			path = dimension == null ? Path.Combine(path, "region") : Path.Combine(path, dimension, "region");

			var regions = new List<RegionId>();

			foreach (var regionFile in Directory.GetFiles(path, "*.mca"))
			{
				var name = Path.GetFileNameWithoutExtension(regionFile);
				var parts = name.Split('.');
				regions.Add(new RegionId(new Coord2(int.Parse(parts[1]), int.Parse(parts[2])), dimension));
			}

			return regions;
		}

		public BlockState GetBlock(Coord3 blockPos, string dimension = null)
		{
			var region = GetRegion((blockPos >> 9).Flatten(), dimension);
			return region?.GetBlock(blockPos);
		}

		public Region GetRegion(Coord2 pos, string dimension = null)
		{
			return GetRegion(new RegionId(pos, dimension));
		}

		public Region GetRegion(RegionId regionId)
		{
			var (pos, dimension) = regionId;

			if (_loadedRegions.ContainsKey(regionId))
				return _loadedRegions[regionId];

			var path = Path.GetDirectoryName(_levelPath);
			if (path == null)
				return null;

			path = dimension == null ? Path.Combine(path, "region") : Path.Combine(path, dimension, "region");

			var regionPath = Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
			if (!File.Exists(regionPath))
				return null;

			var region = Region.Load(regionPath);
			_loadedRegions[regionId] = region;

			return region;
		}
	}

	internal readonly struct RegionId
	{
		public readonly Coord2 Pos;
		public readonly string Dimension;

		public RegionId(Coord2 pos, string dimension)
		{
			Pos = pos;
			Dimension = dimension;
		}

		public bool Equals(RegionId other)
		{
			return Pos.Equals(other.Pos) && Dimension == other.Dimension;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is RegionId other && Equals(other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(Pos, Dimension);
		}

		public static bool operator ==(RegionId left, RegionId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(RegionId left, RegionId right)
		{
			return !left.Equals(right);
		}

		public void Deconstruct(out Coord2 pos, out string dimension)
		{
			pos = Pos;
			dimension = Dimension;
		}
	}
}