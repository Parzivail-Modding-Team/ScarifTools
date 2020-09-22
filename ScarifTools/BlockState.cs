using System;
using Substrate.Nbt;

namespace ScarifTools
{
	internal class BlockState
	{
		public readonly string Name;
		public readonly TagNodeCompound Properties;

		public BlockState(string name, TagNodeCompound props)
		{
			Name = name;
			Properties = props;
		}

		public static BlockState Load(TagNodeCompound tag)
		{
			var name = tag["Name"].ToTagString().Data;
			var props = tag.ContainsKey("Properties") ? tag["Properties"].ToTagCompound() : null;

			return new BlockState(name, props);
		}

		protected bool Equals(BlockState other)
		{
			return Name == other.Name && Equals(Properties, other.Properties);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((BlockState) obj);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(Name, Properties);
		}

		public static bool operator ==(BlockState left, BlockState right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(BlockState left, BlockState right)
		{
			return !Equals(left, right);
		}
	}
}