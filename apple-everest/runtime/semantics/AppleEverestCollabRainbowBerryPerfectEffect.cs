#nullable disable
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabRainbowBerryPerfectEffect : Entity
{
	public AppleEverestCollabRainbowBerryPerfectEffect(Vector2 position)
		: base(position)
	{
		base.Depth = -1000000;
		Sprite sprite;
		Add(sprite = AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_perfectAnimation"));
		sprite.OnLastFrame = delegate
		{
			RemoveSelf();
		};
		sprite.Play("perfect");
	}
}
