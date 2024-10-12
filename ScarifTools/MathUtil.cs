using System;

namespace ScarifTools;

public class MathUtil
{
	public static int GetNextLargestPowerOfTwo(int value)
	{
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		value++;
		
		return value;
	}
	
	public static int GetMinRepresentableBits(int value)
	{
		return (int)Math.Log2(GetNextLargestPowerOfTwo(value));
	}
}