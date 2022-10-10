namespace ScarifTools;

/// <summary>
/// 3D Hibert curve utility functions
/// </summary>
/// <cite href="http://and-what-happened.blogspot.com/2011/08/fast-2d-and-3d-hilbert-curves-and.html"/>
/// <cite href="http://threadlocalmutex.com/?p=149"/>
public class HibertUtil
{
    private static readonly byte[] HilbertToMortonTable =
    {
        48, 33, 35, 26, 30, 79, 77, 44,
        78, 68, 64, 50, 51, 25, 29, 63,
        27, 87, 86, 74, 72, 52, 53, 89,
        83, 18, 16, 1, 5, 60, 62, 15,
        0, 52, 53, 57, 59, 87, 86, 66,
        61, 95, 91, 81, 80, 2, 6, 76,
        32, 2, 6, 12, 13, 95, 91, 17,
        93, 41, 40, 36, 38, 10, 11, 31,
        14, 79, 77, 92, 88, 33, 35, 82,
        70, 10, 11, 23, 21, 41, 40, 4,
        19, 25, 29, 47, 46, 68, 64, 34,
        45, 60, 62, 71, 67, 18, 16, 49
    };

    private static uint HilbertToMorton3d(uint index, int bits)
    {
        var transform = 0u;
        var transformedBits = 0u;

        for (var i = 3 * (bits - 1); i >= 0; i -= 3)
        {
            transform = HilbertToMortonTable[transform | ((index >> i) & 7)];
            transformedBits = (transformedBits << 3) | (transform & 7);
            transform &= ~7u;
        }

        return transformedBits;
    }

    private static Coord3 Decode5BitMorton3d(uint morton)
    {
        // unpack 3 5-bit indices from a 15-bit Morton code
        var value1 = morton;
        var value2 = value1 >> 1;
        var value3 = value1 >> 2;
        value1 &= 0x00001249;
        value2 &= 0x00001249;
        value3 &= 0x00001249;
        value1 |= value1 >> 2;
        value2 |= value2 >> 2;
        value3 |= value3 >> 2;
        value1 &= 0x000010c3;
        value2 &= 0x000010c3;
        value3 &= 0x000010c3;
        value1 |= value1 >> 4;
        value2 |= value2 >> 4;
        value3 |= value3 >> 4;
        value1 &= 0x0000100f;
        value2 &= 0x0000100f;
        value3 &= 0x0000100f;
        value1 |= value1 >> 8;
        value2 |= value2 >> 8;
        value3 |= value3 >> 8;
        value1 &= 0x0000001f;
        value2 &= 0x0000001f;
        value3 &= 0x0000001f;
        return new Coord3((int)value1, (int)value2, (int)value3);
    }

    public static Coord3 SampleCurve(int i) => Decode5BitMorton3d(HilbertToMorton3d((uint)i, 4));
}