namespace Substrate.Core;

public readonly record struct ChunkKey(int cx, int cz)
{
    public override string ToString()
    {
        return "(" + cx + ", " + cz + ")";
    }
}