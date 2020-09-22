using BidirectionalMap;
using Substrate.Core;
using Substrate.Nbt;

namespace ScarifTools
{
	internal class FabricRegistry
	{
		public int Version { get; }
		public BiMap<string, int> BlockMap { get; }
		public BiMap<string, int> BlockEntityTypeMap { get; }
		public BiMap<string, int> ItemMap { get; }

		private FabricRegistry(int version, BiMap<string, int> blockMap, BiMap<string, int> blockEntityTypeMap, BiMap<string, int> itemMap)
		{
			Version = version;
			BlockMap = blockMap;
			BlockEntityTypeMap = blockEntityTypeMap;
			ItemMap = itemMap;
		}

		public static FabricRegistry Load(string filename)
		{
			var nf = new NBTFile(filename);

			using var nbtstr = nf.GetDataInputStream();
			var tree = new NbtTree(nbtstr);

			var version = tree.Root["version"].ToTagInt().Data;

			var registries = tree.Root["registries"].ToTagCompound();

			var blockMap = CreateMap(registries["minecraft:block"]);
			var blockEntityTypeMap = CreateMap(registries["minecraft:block_entity_type"]);
			var itemMap = CreateMap(registries["minecraft:item"]);

			return new FabricRegistry(version, blockMap, blockEntityTypeMap, itemMap);
		}

		private static BiMap<string, int> CreateMap(TagNode node)
		{
			var map = new BiMap<string, int>();
			foreach (var (key, value) in node.ToTagCompound()) map.Add(key, value.ToTagInt().Data);
			return map;
		}
	}
}