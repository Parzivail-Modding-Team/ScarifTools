using Substrate.Nbt;

namespace ScarifTools;

public record BlockState(string Name, TagNodeCompound Properties)
{
    public static BlockState Load(TagNodeCompound tag)
    {
        var name = tag["Name"].ToTagString().Data;
        var props = tag.ContainsKey("Properties") ? tag["Properties"].ToTagCompound() : null;

        return new BlockState(name, props);
    }
}