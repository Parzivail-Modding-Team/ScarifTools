using System;
using System.Collections.Generic;
using System.Linq;
using Substrate.Nbt;

namespace ScarifTools;

public class BlockState
{
    private readonly int _hashCode;
    private readonly string _encoded;

    public string Name { get; }
    public Dictionary<string, string>? Properties { get; }

    public BlockState(string name, TagNodeCompound? properties)
    {
        Name = name;
        Properties = properties?.ToDictionary(pair => pair.Key, pair => pair.Value.ToTagString().Data);

        _hashCode = HashCode.Combine(Name, Properties);
        _encoded = Properties == null ? name : $"{name}[{string.Join(",", Properties.Select(pair => $"{pair.Key}={pair.Value}"))}]";
    }

    public static BlockState Load(TagNodeCompound tag)
    {
        var name = tag["Name"].ToTagString().Data;
        var props = tag.ContainsKey("Properties") ? tag["Properties"].ToTagCompound() : null;

        return new BlockState(name, props);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    private bool Equals(BlockState other)
    {
        return _encoded == other._encoded;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((BlockState)obj);
    }
}