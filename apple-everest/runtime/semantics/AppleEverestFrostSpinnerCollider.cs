#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostSpinnerCollider : ColliderList
{
	private const float HitboxX = -8f;

	private const float HitboxY = -3f;

	private const float HitboxW = 16f;

	private const float HitboxH = 4f;

	private const float CircleRadius = 6f;

	internal static Collider[] MakeColliders(float scale)
	{
		return new Collider[2]
		{
			new Circle(6f * scale),
			new Hitbox(16f * scale, 4f * scale, -8f * scale, -3f * scale)
		};
	}

	public AppleEverestFrostSpinnerCollider()
		: base(MakeColliders(1f))
	{
	}

	public override bool Collide(Hitbox hitbox)
	{
		Vector2 position = base.Entity.Position;
		float absoluteLeft = hitbox.AbsoluteLeft;
		if (position.X + -8f + 16f <= absoluteLeft)
		{
			return false;
		}
		float width = hitbox.Width;
		if (position.X + -8f >= absoluteLeft + width)
		{
			return false;
		}
		float absoluteTop = hitbox.AbsoluteTop;
		float num = position.Y + -3f + 4f - absoluteTop;
		if (num <= -5f)
		{
			return false;
		}
		float height = hitbox.Height;
		float num2 = position.Y + -3f - (absoluteTop + height);
		if (num2 >= 3f)
		{
			return false;
		}
		if (num > 0f && num2 < 0f)
		{
			return true;
		}
		if (RectToCircle_NoHorizontal(absoluteLeft, absoluteTop, width, height, position, 6f))
		{
			return true;
		}
		return false;
	}

	private static bool RectToCircle_NoHorizontal(float rX, float rY, float rW, float rH, Vector2 cPosition, float cRadius)
	{
		if (cPosition.Y >= rY && cPosition.Y < rY + rH)
		{
			return true;
		}
		if (cPosition.Y < rY && Monocle.Collide.CircleToLine(lineFrom: new Vector2(rX, rY), lineTo: new Vector2(rX + rW, rY), cPosiition: cPosition, cRadius: cRadius))
		{
			return true;
		}
		if (cPosition.Y >= rY + rH && Monocle.Collide.CircleToLine(lineFrom: new Vector2(rX, rY + rH), lineTo: new Vector2(rX + rW, rY + rH), cPosiition: cPosition, cRadius: cRadius))
		{
			return true;
		}
		return false;
	}
}
