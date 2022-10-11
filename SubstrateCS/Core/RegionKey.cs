using System;

namespace Substrate.Core;

public readonly record struct RegionKey(int X, int Z)
{
    public static RegionKey InvalidRegion = new(int.MinValue, int.MinValue);

    public override string ToString()
    {
        return "(" + X + ", " + Z + ")";
    }
}