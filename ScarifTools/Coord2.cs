using System;

namespace ScarifTools
{
	internal readonly struct Coord2
	{
		private static readonly Coord2 CoordZero = new Coord2(0, 0);
		public static ref readonly Coord2 Zero => ref CoordZero;

		public readonly int X;
		public readonly int Z;

		public Coord2(int x, int z)
		{
			X = x;
			Z = z;
		}

		public static implicit operator Coord2(long left)
		{
			return new Coord2((int)(left >> 32), (int)(left & 0xFFFFFFFF));
		}

		public static implicit operator long(Coord2 left)
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

		public static Coord2 operator >>(in Coord2 left, in int right)
		{
			return new Coord2(left.X >> right, left.Z >> right);
		}

		public bool Equals(Coord2 other)
		{
			return X == other.X && Z == other.Z;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is Coord2 other && Equals(other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(X, Z);
		}

		public static bool operator ==(Coord2 left, Coord2 right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Coord2 left, Coord2 right)
		{
			return !left.Equals(right);
		}

		public void Deconstruct(out int x, out int z)
		{
			x = X;
			z = Z;
		}
	}
}