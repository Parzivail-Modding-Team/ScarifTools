using System;

namespace ScarifTools
{
	internal readonly struct Coord3
	{
		private static readonly Coord3 CoordZero = new Coord3(0, 0, 0);
		public static ref readonly Coord3 Zero => ref CoordZero;

		public readonly int X;
		public readonly int Y;
		public readonly int Z;

		public Coord3(int x, int y, int z)
		{
			X = x;
			Y = y;
			Z = z;
		}

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

		public static Coord3 operator >>(in Coord3 left, in int right)
		{
			return new Coord3(left.X >> right, left.Y >> right, left.Z >> right);
		}

		public bool Equals(Coord3 other)
		{
			return X == other.X && Y == other.Y && Z == other.Z;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is Coord3 other && Equals(other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(X, Y, Z);
		}

		public static bool operator ==(Coord3 left, Coord3 right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Coord3 left, Coord3 right)
		{
			return !left.Equals(right);
		}
	}
}