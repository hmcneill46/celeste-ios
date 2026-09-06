#nullable disable
using System.Collections.Generic;
using Celeste;
using Microsoft.Xna.Framework;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostParallaxDecalRenderer
{
	internal List<AppleEverestFrostDecalInfo> Decals = new List<AppleEverestFrostDecalInfo>();

	public void AddDecal(Decal d)
	{
		Decals.Add(AppleEverestFrostDecalContainerHelpers.CreateInfo(d));
		d.RemoveSelf();
	}

	public void Render()
	{
		Level currentLevel = (Level)Monocle.Engine.Scene;
		Vector2 position = currentLevel.Camera.Position;
		Vector2 vector = position + new Vector2(160f, 90f);
		bool paused = currentLevel.Paused;
		foreach (AppleEverestFrostDecalInfo decal2 in Decals)
		{
			Decal decal = decal2.decal;
			if (decal.Scene == null)
			{
				AppleEverestFrostDecalContainerHelpers.SetScene(decal, currentLevel);
			}
			Vector2 position2 = decal.Position;
			Vector2 position3 = position2 + (position2 - vector) * decal.AppleEverestParallaxAmount;
			decal.Position = position3;
			bool num = AppleEverestFrostDecalContainerHelpers.IsInside(position, decal2);
			decal.Position = position2;
			if (num)
			{
				if (!paused && decal.Active)
				{
					decal.Update();
				}
				decal.Render();
			}
		}
	}
}
