namespace ScarifTools;

public record Coord3(int X, int Y, int Z)
{
    public static readonly Coord3 Zero = new(0, 0, 0);

    public Coord2 Flatten()
    {
        return new Coord2(X, Z);
    }

    public static Coord3 operator +(in Coord3 left, in Coord3 right)
    {
        return new Coord3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    public static Coord3 operator -(in Coord3 left, in Coord3 right)
    {
        return new Coord3(left.X + right.X, left.Y - right.Y, left.Z + right.Z);
    }

    public static Coord3 operator <<(in Coord3 left, in int right)
    {
        return new Coord3(left.X << right, left.Y << right, left.Z << right);
    }

    public static Coord3 operator >> (in Coord3 left, in int right)
    {
        return new Coord3(left.X >> right, left.Y >> right, left.Z >> right);
    }
}