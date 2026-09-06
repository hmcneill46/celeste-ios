// CherryHelper 1.8.2 selected pedestal/collider behavior.
#nullable disable
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestItemCrystalCollider : Component
{
	public Action<AppleEverestItemCrystal> OnCollide;

	public Collider Collider;

	public Collider FeatherCollider;

	public AppleEverestItemCrystalCollider(Action<AppleEverestItemCrystal> onCollide, Collider collider = null)
		: base(active: false, visible: false)
	{
		OnCollide = onCollide;
		Collider = collider;
	}

	public bool Check(AppleEverestItemCrystal AppleEverestItemCrystal)
	{
		Collider collider = Collider;
		if (collider == null)
		{
			if (AppleEverestItemCrystal.CollideCheck(base.Entity))
			{
				OnCollide(AppleEverestItemCrystal);
				return true;
			}
			return false;
		}
		Collider collider2 = base.Entity.Collider;
		base.Entity.Collider = collider;
		bool flag = AppleEverestItemCrystal.CollideCheck(base.Entity);
		base.Entity.Collider = collider2;
		if (flag)
		{
			OnCollide(AppleEverestItemCrystal);
			return true;
		}
		return false;
	}

	public override void DebugRender(Camera camera)
	{
		if (Collider != null)
		{
			Collider collider = base.Entity.Collider;
			base.Entity.Collider = Collider;
			Collider.Render(camera, Color.Green);
			base.Entity.Collider = collider;
		}
	}
}
