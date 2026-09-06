#nullable disable
using System.Collections.Generic;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostDecalContainerMaker : Trigger
{
	public readonly int ChunkSize;

	public AppleEverestFrostDecalContainerMaker(EntityData data, Vector2 offset)
		: base(data, offset)
	{
		ChunkSize = data.Int("chunkSizeInTiles", 64) * 8;
		base.Depth = -1000000;
	}

	public override void Awake(Scene scene)
	{
		Dictionary<int, AppleEverestFrostDecalContainerRenderer> dictionary = new Dictionary<int, AppleEverestFrostDecalContainerRenderer>();
		Rectangle bounds = (base.Scene as Level).Bounds;
		EntityList entities = scene.Entities;
		foreach (Entity item in entities)
		{
			int key = item.Depth;
			if (!(item is Decal) || dictionary.TryGetValue(key, out var _))
			{
				continue;
			}
			AppleEverestFrostDecalContainerRenderer decalContainerRenderer = new AppleEverestFrostDecalContainerRenderer();
			decalContainerRenderer.Depth = key;
			dictionary[key] = decalContainerRenderer;
			scene.Add(decalContainerRenderer);
			AppleEverestFactoryCanary.Successor(this, decalContainerRenderer);
			int chunkSize = ChunkSize;
			for (int i = bounds.Left; i < bounds.Right; i += chunkSize)
			{
				for (int j = bounds.Top; j < bounds.Bottom; j += chunkSize)
				{
					AppleEverestFrostDecalContainer decalContainer = new AppleEverestFrostDecalContainer(new Hitbox(chunkSize, chunkSize, i, j));
					decalContainer.Position = new Vector2(i, j);
					decalContainer.Renderer = decalContainerRenderer;
					decalContainerRenderer.Containers.Add(decalContainer);
				}
			}
		}
		int num = 0;
		int num2 = 0;
		int transferred = 0;
		AppleEverestFrostDecalContainer decalContainer2 = null;
		foreach (Entity item2 in entities)
		{
			if (!(item2 is Decal decal) || AppleEverestSelectedDecalRegistry.IgnoredNames.Contains(decal.Name) || decal.Get<MirrorSurface>() != null)
			{
				continue;
			}
			AppleEverestFrostDecalContainerRenderer decalContainerRenderer2 = dictionary[decal.Depth];
			if (decal.AppleEverestParallax)
			{
				decalContainerRenderer2.Parallax.AddDecal(decal);
				transferred++;
				continue;
			}
			num++;
			AppleEverestFrostDecalContainer decalContainer3 = null;
			List<AppleEverestFrostDecalContainer> containers = decalContainerRenderer2.Containers;
			for (int num3 = containers.Count - 1; num3 >= 0; num3--)
			{
				AppleEverestFrostDecalContainer decalContainer4 = containers[num3];
				if (decalContainer4.IsDecalValid(decal))
				{
					if (decalContainer2 == null || decalContainer4 == decalContainer2)
					{
						decalContainer2 = decalContainer4;
						decalContainer4.AddDecal(decal);
						transferred++;
						break;
					}
					decalContainer3 = new AppleEverestFrostDecalContainer(decalContainer4.collider)
					{
						Renderer = decalContainer4.Renderer,
						Position = decalContainer4.Position
					};
					decalContainer3.AddDecal(decal);
					transferred++;
					decalContainer2 = decalContainer3;
					num2++;
					break;
				}
			}
			if (decalContainer3 != null)
			{
				decalContainerRenderer2.Containers.Add(decalContainer3);
			}
		}
		AppleEverestFactoryCanary.Completion(this, "decals-converted", transferred > 0);
		RemoveSelf();
	}
}
