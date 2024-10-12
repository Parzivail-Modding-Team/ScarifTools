using System;
using System.Collections.Generic;
using System.Linq;
using Acacia;

namespace ScarifTools;

public class BlockState
{
    private readonly int _hashCode;

    public string Name { get; }
    public Dictionary<string, string> Properties { get; }

    public BlockState(string name, NbtCompound? properties)
    {
        Name = name;
        Properties = properties?.Elements.ToDictionary(pair => pair.Key, pair => ((NbtString)pair.Value).Value) ?? new Dictionary<string, string>();

        _hashCode = HashCode.Combine(Name, Properties);
    }

    public static BlockState Load(NbtCompound tag)
    {
        var name = tag.GetString("Name");
        var props = tag.TryGetCompound("Properties", out var value) ? value : null;

        return new BlockState(name, props);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    private bool Equals(BlockState other)
    {
        return Name == other.Name && Properties.SequenceEqual(other.Properties);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((BlockState)obj);
    }
}