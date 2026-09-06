#nullable disable
using System.Collections.Generic;
using Celeste;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostDecalContainerRenderer : Entity
{
	public List<AppleEverestFrostDecalContainer> Containers = new List<AppleEverestFrostDecalContainer>();

	public AppleEverestFrostParallaxDecalRenderer Parallax = new AppleEverestFrostParallaxDecalRenderer();

	public override void Awake(Scene scene)
	{
		foreach (AppleEverestFrostDecalContainer container in Containers)
		{
			container.Awake(scene);
		}
		base.Awake(scene);
	}

	public override void Render()
	{
		Level level = base.Scene as Level;
		foreach (AppleEverestFrostDecalContainer container in Containers)
		{
			container.Render(level);
		}
		Parallax.Render();
		base.Render();
	}
}
