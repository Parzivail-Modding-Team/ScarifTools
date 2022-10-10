namespace ScarifTools;

public readonly record struct Coord2(int X, int Z)
{
    public static readonly Coord2 Zero = new(0, 0);

    public static explicit operator Coord2(long left)
    {
        return new Coord2((int)(left >> 32), (int)(left & 0xFFFFFFFF));
    }

    public static explicit operator long(Coord2 left)
    {
        return ((long)left.X << 32) | (uint)left.Z;
    }

    public static Coord2 operator +(in Coord2 left, in Coord2 right)
    {
        return new Coord2(left.X + right.X, left.Z + right.Z);
    }

    public static Coord2 operator -(in Coord2 left, in Coord2 right)
    {
        return new Coord2(left.X + right.X, left.Z + right.Z);
    }

    public static Coord2 operator <<(in Coord2 left, in int right)
    {
        return new Coord2(left.X << right, left.Z << right);
    }

    public static Coord2 operator >> (in Coord2 left, in int right)
    {
        return new Coord2(left.X >> right, left.Z >> right);
    }
}