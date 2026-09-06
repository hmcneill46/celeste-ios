#nullable disable
using System;

namespace Celeste.Mod;

internal class AppleEverestByteArray2D
{
	private readonly byte[] data;

	public int Width { get; }

	public int Height { get; }

	public byte[] Data => data;

	public byte this[int x, int y]
	{
		get
		{
			return data[x + y * Width];
		}
		set
		{
			data[x + y * Width] = value;
		}
	}

	public bool TryGet(int x, int y, out byte value)
	{
		if (x >= 0 && x < Width && y >= 0 && y < Height)
		{
			value = this[x, y];
			return true;
		}
		value = 0;
		return false;
	}

	public AppleEverestByteArray2D(int width, int height)
	{
		Width = width;
		Height = height;
		data = new byte[width * height];
	}

	public void Max(AppleEverestByteArray2D other, int dx, int dy)
	{
		int num = Math.Max(dx, 0);
		int num2 = Math.Max(dy, 0);
		int num3 = Math.Min(dx + other.Width, Width);
		int num4 = Math.Min(dy + other.Height, Height);
		int num5 = num2;
		int num6 = num2 - dy;
		while (num5 < num4)
		{
			int num7 = num;
			int num8 = num - dx;
			while (num7 < num3)
			{
				byte val = data[num7 + num5 * Width];
				byte val2 = other.data[num8 + num6 * other.Width];
				data[num7 + num5 * Width] = Math.Max(val, val2);
				num7++;
				num8++;
			}
			num5++;
			num6++;
		}
	}
}
