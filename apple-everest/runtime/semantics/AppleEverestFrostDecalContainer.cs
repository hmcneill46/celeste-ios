#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostDecalContainer
{
	internal AppleEverestFrostDecalContainerRenderer Renderer;

	internal List<AppleEverestFrostDecalInfo> Decals = new List<AppleEverestFrostDecalInfo>();

	internal Hitbox collider;

	internal int maxW;

	internal int maxH;

	public Vector2 Position;

	internal bool hasSetScene;

	public AppleEverestFrostDecalContainer(Hitbox collider)
	{
		this.collider = collider;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsDecalValid(Decal item)
	{
		return collider.Collide(item.Position);
	}

	public void AddDecal(Decal item)
	{
		hasSetScene = false;
		AppleEverestFrostDecalInfo info = AppleEverestFrostDecalContainerHelpers.CreateInfo(item);
		float val = info.HalfWidth * 2f;
		float val2 = info.HalfHeight * 2f;
		Decals.Add(info);
		maxW = (int)Math.Max(maxW, val);
		maxH = (int)Math.Max(maxH, val2);
		foreach (Component item2 in item.Components)
		{
			if (!(item2 is VertexLight vertexLight))
			{
				if (!(item2 is BloomPoint bloomPoint))
				{
					if (item2 is StaticMover staticMover)
					{
						staticMover.OnDestroy = (Action)Delegate.Combine(staticMover.OnDestroy, (Action)delegate
						{
							Decals.Remove(info);
						});
					}
				}
				else
				{
					Renderer.Add(new BloomPoint(bloomPoint.Position + item.Position, bloomPoint.Alpha, bloomPoint.Radius));
				}
			}
			else
			{
				Renderer.Add(new VertexLight(vertexLight.Position + item.Position, vertexLight.Color, vertexLight.Alpha, (int)vertexLight.StartRadius, (int)vertexLight.EndRadius));
			}
		}
		item.RemoveSelf();
	}

	public void Awake(Scene scene)
	{
		collider.Width += maxW;
		collider.Height += maxH;
		Position.X -= maxW / 2;
		Position.Y -= maxH / 2;
	}

	private bool IsInside(Vector2 cam)
	{
		float x = Position.X;
		float y = Position.Y;
		float width = collider.Width;
		float height = collider.Height;
		float x2 = cam.X;
		float y2 = cam.Y;
		VirtualRenderTarget gameplay = GameplayBuffers.Gameplay;
		if (x + width >= x2 - 64f && x <= x2 + (float)gameplay.Width + 64f && y + height >= y2 - 64f)
		{
			return y <= y2 + (float)gameplay.Height + 64f;
		}
		return false;
	}

	public void Render(Level level)
	{
		Vector2 position = level.Camera.Position;
		if (!IsInside(position))
		{
			return;
		}
		if (!hasSetScene)
		{
			hasSetScene = true;
			foreach (AppleEverestFrostDecalInfo decal4 in Decals)
			{
				Decal decal = decal4.decal;
				if (decal.Scene == null)
				{
					AppleEverestFrostDecalContainerHelpers.SetScene(decal, level);
				}
			}
		}
		if (level.Paused)
		{
			Span<AppleEverestFrostDecalInfo> span = CollectionsMarshal.AsSpan(Decals);
			for (int i = 0; i < span.Length; i++)
			{
				AppleEverestFrostDecalInfo decalInfo = span[i];
				Decal decal2 = decalInfo.decal;
				if (decal2.Visible && AppleEverestFrostDecalContainerHelpers.IsInside(position, decalInfo))
				{
					decal2.Render();
				}
			}
			return;
		}
		Span<AppleEverestFrostDecalInfo> span2 = CollectionsMarshal.AsSpan(Decals);
		for (int i = 0; i < span2.Length; i++)
		{
			AppleEverestFrostDecalInfo decalInfo2 = span2[i];
			Decal decal3 = decalInfo2.decal;
			if (decal3.Visible && AppleEverestFrostDecalContainerHelpers.IsInside(position, decalInfo2))
			{
				if (decal3.Active)
				{
					decal3.Update();
				}
				decal3.Render();
			}
		}
	}
}
