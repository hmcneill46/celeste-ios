// CherryHelper 1.8.2 selected pedestal/collider behavior.
#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestItemCrystalPedestal : Entity
{
	public Image sprite;

	private AppleEverestItemCrystalCollider collid;

	public AppleEverestItemCrystalPedestal(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		Position = data.Position + offset;
		Add(sprite = AppleEverestStaticRuntime.CreateStaticModSprite("itemCrystalPedestal"));
		Add(collid = new AppleEverestItemCrystalCollider(OnHoldable, new Hitbox(24f, 24f, -12f, -8f)));
		collid.Visible = true;
		collid.Active = true;
	}

	public void OnHoldable(AppleEverestItemCrystal crystal)
	{
		if (crystal != null)
		{
			crystal.PlayerRelease();
			crystal.PlayerRelease();
			Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.4f, start: true);
			tween.OnUpdate = delegate(Tween t)
			{
				crystal.PlayerRelease();
				crystal.Position = Vector2.Lerp(crystal.Position, Position, t.Eased);
				crystal.sprite.Play("fill");
			};
			tween.OnComplete = delegate
			{
				crystal.Position = Position;
				crystal.Add(new Coroutine(crystal.DestroySequence()));
			};
			Add(tween);
		}
	}
}
