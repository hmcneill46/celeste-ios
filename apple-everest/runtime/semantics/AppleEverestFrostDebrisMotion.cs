#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestFrostDebrisMotion
{
	public class MoveFastData
	{
		private readonly Entity entity;
		public MoveFastData(Entity entity) { this.entity = entity; }
		public readonly List<Entity> Broadest = new List<Entity>();

		private readonly List<Entity> _temp = new List<Entity>();

		private readonly List<Entity> _jumpthrus = new List<Entity>();

		public Rectangle HitboxRect;

		public Vector2 MovementCounter;

		public bool MoveBoth(Vector2 move, Collision collideH = null, Collision collideV = null)
		{
			entity.Scene.CollideIntoBroadPhase<Solid>(Merge(HitboxRect, MovedBy(HitboxRect, move)), Broadest);
			int num = 0 | (MoveH(move.X, out var moved, collideH) ? 1 : 0);
			HitboxRect.X += moved;
			int result = num | (MoveV(move.Y, collideV) ? 1 : 0);
			Broadest.Clear();
			_temp.Clear();
			_jumpthrus.Clear();
			return (byte)result != 0;
		}

		public bool MoveH(float moveH, out int moved, Collision onCollide = null, Solid pusher = null)
		{
			moved = 0;
			MovementCounter.X += moveH;
			int num = (int)Math.Round(MovementCounter.X, MidpointRounding.ToEven);
			if (num == 0)
			{
				return false;
			}
			MovementCounter.X -= num;
			return MoveHExact(num, out moved, onCollide, pusher);
		}

		public bool MoveHExact(int moveH, out int moved, Collision onCollide = null, Solid pusher = null)
		{
			moved = 0;
			Entity entity2 = entity;
			List<Entity> temp = _temp;
			temp.Clear();
			Rectangle hitboxRect = HitboxRect;
			CollideIntoBroadPhase(Broadest, Merge(hitboxRect, MovedBy(hitboxRect, moveH, 0)), temp);
			if (temp.Count == 0)
			{
				entity2.X += moveH;
				return false;
			}
			Vector2 targetPosition = entity2.Position + Vector2.UnitX * moveH;
			int num = Math.Sign(moveH);
			while (moveH != 0)
			{
				Solid solid = entity2.CollideFirst<Solid>(entity2.Position + Vector2.UnitX * num, temp);
				if (solid != null)
				{
					MovementCounter.X = 0f;
					onCollide?.Invoke(new CollisionData
					{
						Direction = Vector2.UnitX * num,
						Moved = Vector2.UnitX * moved,
						TargetPosition = targetPosition,
						Hit = solid,
						Pusher = pusher
					});
					return true;
				}
				moved += num;
				moveH -= num;
				entity2.X += num;
			}
			return false;
		}

		public bool MoveV(float moveV, Collision onCollide = null, Solid pusher = null)
		{
			MovementCounter.Y += moveV;
			int num = (int)Math.Round(MovementCounter.Y, MidpointRounding.ToEven);
			if (num == 0)
			{
				return false;
			}
			MovementCounter.Y -= num;
			return MoveVExact(num, onCollide, pusher);
		}

		public bool MoveVExact(int moveV, Collision onCollide = null, Solid pusher = null)
		{
			Entity entity2 = entity;
			Rectangle hitboxRect = HitboxRect;
			List<Entity> temp = _temp;
			List<Entity> jumpthrus = _jumpthrus;
			bool flag = moveV > 0;
			temp.Clear();
			CollideIntoBroadPhase(Broadest, Merge(hitboxRect, MovedBy(hitboxRect, 0, moveV)), temp);
			if (flag)
			{
				jumpthrus.Clear();
				entity2.Scene.CollideIntoBroadPhase<JumpThru>(Merge(hitboxRect, MovedBy(hitboxRect, 0, moveV)), jumpthrus);
			}
			if (temp.Count == 0 && jumpthrus.Count == 0)
			{
				entity2.Y += moveV;
				return false;
			}
			Vector2 targetPosition = entity2.Position + Vector2.UnitY * moveV;
			int num = Math.Sign(moveV);
			int num2 = 0;
			while (moveV != 0)
			{
				Solid solid = entity2.CollideFirst<Solid>(entity2.Position + Vector2.UnitY * num, temp);
				if (solid != null)
				{
					MovementCounter.Y = 0f;
					onCollide?.Invoke(new CollisionData
					{
						Direction = Vector2.UnitY * num,
						Moved = Vector2.UnitY * num2,
						TargetPosition = targetPosition,
						Hit = solid,
						Pusher = pusher
					});
					return true;
				}
				if (flag)
				{
					JumpThru jumpThru = entity2.CollideFirstOutside<JumpThru>(entity2.Position + Vector2.UnitY * num, jumpthrus);
					if (jumpThru != null)
					{
						MovementCounter.Y = 0f;
						onCollide?.Invoke(new CollisionData
						{
							Direction = Vector2.UnitY * num,
							Moved = Vector2.UnitY * num2,
							TargetPosition = targetPosition,
							Hit = jumpThru,
							Pusher = pusher
						});
						return true;
					}
				}
				num2 += num;
				moveV -= num;
				entity2.Y += num;
			}
			return false;
		}
	}

	public static void CollideIntoBroadPhase(List<Entity> src, Rectangle rect, List<Entity> hits)
	{
		foreach (Entity item in src)
		{
			if (item.Collidable)
			{
				Collider collider = item.Collider;
				if (collider != null && collider.Collide(rect))
				{
					hits.Add(item);
				}
			}
		}
	}

	public static void CollideIntoBroadPhase<T>(this Scene scene, Rectangle rect, List<Entity> hits) where T : Entity
	{
		Span<Entity> span = CollectionsMarshal.AsSpan(scene.Tracker.GetEntities<T>());
		for (int i = 0; i < span.Length; i++)
		{
			Entity entity = span[i];
			if (entity.Collidable)
			{
				Collider collider = entity.Collider;
				if (collider != null && collider.Collide(rect))
				{
					hits.Add(entity);
				}
			}
		}
	}

	public static T CollideFirst<T>(this Entity entity, Vector2 at, List<Entity> hits) where T : Entity
	{
		Vector2 position = entity.Position;
		entity.Position = at;
		Collider collider = entity.Collider;
		if (collider.GetType() == typeof(Hitbox))
		{
			Hitbox hitbox = (Hitbox)collider;
			Span<Entity> span = CollectionsMarshal.AsSpan(hits);
			for (int i = 0; i < span.Length; i++)
			{
				Entity entity2 = span[i];
				if (entity2.Collider.Collide(hitbox))
				{
					entity.Position = position;
					return entity2 as T;
				}
			}
		}
		else
		{
			Span<Entity> span2 = CollectionsMarshal.AsSpan(hits);
			for (int i = 0; i < span2.Length; i++)
			{
				Entity entity3 = span2[i];
				if (collider.Collide(entity3.Collider))
				{
					entity.Position = position;
					return entity3 as T;
				}
			}
		}
		entity.Position = position;
		return null;
	}

	public static T CollideFirstOutside<T>(this Entity entity, Vector2 at, List<Entity> hits) where T : Entity
	{
		foreach (Entity hit in hits)
		{
			if (!Collide.Check(entity, hit) && Collide.Check(entity, hit, at))
			{
				return hit as T;
			}
		}
		return null;
	}

    // Preserve the source SIMD edge convention as well as its scalar fallback.
    internal static bool Contains(Rectangle r, Vector2 value)
    {
        if (Vector128.IsHardwareAccelerated)
            return Vector128.LessThanOrEqualAll(
                Vector128.Create((float)r.X - value.X, (float)r.Y - value.Y,
                    value.X - (float)r.X - (float)r.Width, value.Y - (float)r.Y - (float)r.Height),
                Vector128.Create(0f, 0f, -1f, -1f));
        return r.Y <= value.Y && value.Y < r.Y + r.Height && r.X <= value.X && value.X < r.X + r.Width;
    }
    private static Rectangle MovedBy(Rectangle r, Vector2 offset) =>
        new Rectangle(r.X + (int)offset.X, r.Y + (int)offset.Y, r.Width, r.Height);
    private static Rectangle MovedBy(Rectangle r, int x, int y) =>
        new Rectangle(r.X + x, r.Y + y, r.Width, r.Height);
    private static Rectangle Merge(Rectangle a, Rectangle b)
    {
        int left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
        return new Rectangle(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }
}
