namespace Substrate.Nbt;

public sealed class TagNodeLongArray : TagNode
{
    /// <summary>
    /// Converts the node to itself.
    /// </summary>
    /// <returns>A reference to itself.</returns>
    public override TagNodeLongArray ToTagLongArray()
    {
        return this;
    }

    /// <summary>
    /// Gets the tag type of the node.
    /// </summary>
    /// <returns>The TAG_LONG_ARRAY tag type.</returns>
    public override TagType GetTagType()
    {
        return TagType.TAG_LONG_ARRAY;
    }

    /// <summary>
    /// Gets or sets an long array of tag data.
    /// </summary>
    public long[] Data { get; set; }

    /// <summary>
    /// Gets the length of the stored byte array.
    /// </summary>
    public int Length => Data.Length;

    /// <summary>
    /// Constructs a new byte array node with a null data value.
    /// </summary>
    public TagNodeLongArray()
    {
    }

    /// <summary>
    /// Constructs a new byte array node.
    /// </summary>
    /// <param name="d">The value to set the node's tag data value.</param>
    public TagNodeLongArray(long[] d)
    {
        Data = d;
    }

    /// <summary>
    /// Makes a deep copy of the node.
    /// </summary>
    /// <returns>A new long array node representing the same data.</returns>
    public override TagNode Copy()
    {
        var arr = new long[Data.Length];
        Data.CopyTo(arr, 0);

        return new TagNodeLongArray(arr);
    }

    /// <summary>
    /// Gets a string representation of the node's data.
    /// </summary>
    /// <returns>String representation of the node's data.</returns>
    public override string ToString()
    {
        return Data.ToString();
    }

    /// <summary>
    /// Gets or sets a single long at the specified index.
    /// </summary>
    /// <param name="index">Valid index within stored long array.</param>
    /// <returns>The long value at the given index of the stored byte array.</returns>
    public long this[int index]
    {
        get { return Data[index]; }
        set { Data[index] = value; }
    }

    /// <summary>
    /// Converts a system long array to a long array node representing the same data.
    /// </summary>
    /// <param name="i">A long array.</param>
    /// <returns>A new long array node containing the given value.</returns>
    public static implicit operator TagNodeLongArray(long[] i)
    {
        return new TagNodeLongArray(i);
    }

    /// <summary>
    /// Converts an long array node to a system long array representing the same data.
    /// </summary>
    /// <param name="i">An long array node.</param>
    /// <returns>A system long array set to the node's data.</returns>
    public static implicit operator long[](TagNodeLongArray i)
    {
        return i.Data;
    }
}