#nullable disable
using System;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestFrostDecalContainerHelpers
{
	internal static AppleEverestFrostDecalInfo CreateInfo(Decal item)
    {
        // Neither selected placement nor winning registry sets rotation.
        MTexture texture = item.AppleEverestFirstTexture;
        float width = (float)texture.Width * Math.Abs(item.AppleEverestScale.X);
        float height = (float)texture.Height * Math.Abs(item.AppleEverestScale.Y);
        return new AppleEverestFrostDecalInfo { decal = item, HalfWidth = width / 2f, HalfHeight = height / 2f };
    }

	internal static bool IsInside(Vector2 cam, AppleEverestFrostDecalInfo decal)
	{
		Decal decal2 = decal.decal;
		float halfWidth = decal.HalfWidth;
		float halfHeight = decal.HalfHeight;
		float x = decal2.Position.X;
		float y = decal2.Position.Y;
		float num = halfWidth;
		float num2 = halfHeight;
		VirtualRenderTarget gameplay = GameplayBuffers.Gameplay;
		if (x + halfWidth >= cam.X - num && x - halfWidth <= cam.X + (float)gameplay.Width + num && y + halfHeight >= cam.Y - num2)
		{
			return y - halfHeight <= cam.Y + (float)gameplay.Height + num2;
		}
		return false;
	}

	internal static void SetScene(Decal item, Scene scene)
	{
		item.AppleEverestBindScene(scene);
		foreach (Component component in item.Components)
		{
			component.AppleEverestBindEntity(item);
		}
	}
}
