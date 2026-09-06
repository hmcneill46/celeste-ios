#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestEntityActivator : Trigger
{
	public enum EffectModes
	{
		ActivateInsideDeactivateOutside,
		ActivateInside,
		ActivateOutside,
		DeactivateInside,
		DeactivateOutside,
		ActivateOnScreenDeactivateOffScreen
	}

	public enum ActivationModes
	{
		OnEnter,
		OnStay,
		OnLeave,
		OnFlagActive,
		OnFlagInactive,
		OnFlagActivated,
		OnFlagDeactivated,
		OnUpdate,
		OnCameraMoved,
		OnAwake
	}

	public EffectModes Mode;

	public ActivationModes ActivationMode;

	public HashSet<Type> Targets;

	public bool UseTracked;

	public string Flag;

	public float UpdateInterval;

	public bool ChangeCollidable;

	public bool ChangeActive;

	public bool ChangeVisible;

	public bool CacheTargets;

	public bool AffectComponents;

	private bool previousFlagValue = false;

	private bool updateFlagValues = false;

	private List<Entity> cachedTargets;

	private Vector2 previousCameraPosition;

	private float previousCameraZoom;

	public AppleEverestEntityActivator(EntityData data, Vector2 offset)
		: base(data, offset)
	{
		base.Tag = Tags.TransitionUpdate;
		Mode = data.Enum("mode", EffectModes.ActivateInsideDeactivateOutside);
		ActivationMode = data.Enum("activationMode", ActivationModes.OnEnter);
		Targets = SelectedTargetTypes(data.Attr("targets"));
		UseTracked = data.Bool("useTracked", defaultValue: true);
		Flag = data.Attr("flag");
		ChangeCollidable = data.Bool("changeCollision", defaultValue: true);
		ChangeActive = data.Bool("changeActive", defaultValue: true);
		ChangeVisible = data.Bool("changeVisible", defaultValue: true);
		CacheTargets = data.Bool("cacheTargets");
		AffectComponents = data.Bool("affectComponents");
		UpdateInterval = data.Float("updateInterval", -1f);
	}

	public override void OnEnter(Player player)
	{
		if (ActivationMode == ActivationModes.OnEnter)
		{
			UpdateEntities();
		}
	}

	public override void OnStay(Player player)
	{
		if (ActivationMode == ActivationModes.OnStay)
		{
			UpdateEntities();
		}
	}

	public override void OnLeave(Player player)
	{
		base.OnLeave(player);
		if (ActivationMode == ActivationModes.OnLeave)
		{
			UpdateEntities();
		}
	}

	public override void Update()
	{
		if (ActivationMode == ActivationModes.OnUpdate && OnInterval())
		{
			UpdateEntities();
		}
		else if (ActivationMode == ActivationModes.OnCameraMoved)
		{
			Camera camera = SceneAs<Level>().Camera;
			if (Math.Abs(camera.X - previousCameraPosition.X) > 8f || Math.Abs(camera.Y - previousCameraPosition.Y) > 8f || camera.Zoom != previousCameraZoom)
			{
				UpdateEntities();
				previousCameraPosition = camera.Position;
				previousCameraZoom = camera.Zoom;
			}
		}
		else if (updateFlagValues)
		{
			bool flag = SceneAs<Level>().Session.GetFlag(Flag);
			if (((ActivationMode == ActivationModes.OnFlagActive) & flag) && OnInterval())
			{
				UpdateEntities();
			}
			else if (ActivationMode == ActivationModes.OnFlagInactive && !flag && OnInterval())
			{
				UpdateEntities();
			}
			else if (((ActivationMode == ActivationModes.OnFlagActivated) & flag) && !previousFlagValue)
			{
				UpdateEntities();
			}
			else if (ActivationMode == ActivationModes.OnFlagDeactivated && !flag && previousFlagValue)
			{
				UpdateEntities();
			}
			previousFlagValue = flag;
		}
		base.Update();
	}

	public override void Awake(Scene scene)
	{
		if (CacheTargets)
		{
			UpdateTargetCache();
		}
		if (!string.IsNullOrEmpty(Flag))
		{
			previousFlagValue = SceneAs<Level>()?.Session?.GetFlag(Flag) == true;
			updateFlagValues = true;
		}
		Camera camera = SceneAs<Level>().Camera;
		previousCameraPosition = camera.Position;
		previousCameraZoom = camera.Zoom;
		if (ActivationMode == ActivationModes.OnAwake)
		{
			UpdateEntities();
		}
		if (ActivationMode == ActivationModes.OnCameraMoved)
		{
			scene.OnEndOfFrame += delegate
			{
				UpdateEntities();
			};
		}
		if (updateFlagValues)
		{
			if (ActivationMode == ActivationModes.OnFlagActive && previousFlagValue)
			{
				UpdateEntities();
			}
			else if (ActivationMode == ActivationModes.OnFlagInactive && !previousFlagValue)
			{
				UpdateEntities();
			}
		}
	}

	public void UpdateEntities()
	{
		switch (Mode)
		{
		case EffectModes.ActivateInsideDeactivateOutside:
			ActivateInsideDeactivateOutside();
			break;
		case EffectModes.ActivateInside:
			ActivateInside();
			break;
		case EffectModes.DeactivateInside:
			DeactivateInside();
			break;
		case EffectModes.ActivateOutside:
			ActivateOutside();
			break;
		case EffectModes.DeactivateOutside:
			DeactivateOutside();
			break;
		case EffectModes.ActivateOnScreenDeactivateOffScreen:
			ActivateOnScreenDeactivateOffScreen();
			break;
		default:
			throw new InvalidOperationException($"unregistered entity activator mode: {Mode}");
			break;
		}
	}

	public void UpdateTargetCache()
	{
		cachedTargets = FindTargetEntities(skipCache: true);
	}

	public List<Entity> FindTargetEntities(bool skipCache = false)
	{
		if (CacheTargets && !skipCache)
		{
			return cachedTargets;
		}
		// Exact authored useTracked=false: TypeHelper tests exact CLR identity,
        // including the nested vanilla types, never an assignable subclass.
        List<Entity> matches = new();
        foreach (Entity entity in Scene.Entities)
            if (Targets.Contains(entity.GetType())) matches.Add(entity);
        return matches;
	}

	public bool OnInterval()
	{
		return UpdateInterval <= 0f || base.Scene.OnInterval(UpdateInterval);
	}

	public void UpdateTarget(Entity target, bool visible, bool active, bool collidable)
	{
		if (ChangeCollidable)
		{
			target.Collidable = collidable;
		}
		if (ChangeVisible)
		{
			target.Visible = visible;
		}
		if (ChangeActive)
		{
			target.Active = active;
		}
		if (!AffectComponents || (!ChangeVisible && !ChangeActive))
		{
			return;
		}
		foreach (Component component in target.Components)
		{
			if (ChangeVisible)
			{
				component.Visible = visible;
			}
			if (ChangeActive)
			{
				component.Active = active;
			}
		}
	}

	public bool EntityInside(Entity target)
	{
		if (target.Collider == null)
		{
			return target.X >= base.Collider.AbsoluteLeft && target.X <= base.Collider.AbsoluteRight && target.Y >= base.Collider.AbsoluteTop && target.Y <= base.Collider.AbsoluteBottom;
		}
		return target.Collider.Collide(base.Collider);
	}

	public void ActivateInsideDeactivateOutside()
	{
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (EntityInside(item))
			{
				UpdateTarget(item, visible: true, active: true, collidable: true);
			}
			else
			{
				UpdateTarget(item, visible: false, active: false, collidable: false);
			}
		}
	}

	public void ActivateInside()
	{
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (EntityInside(item))
			{
				UpdateTarget(item, visible: true, active: true, collidable: true);
			}
		}
	}

	public void DeactivateInside()
	{
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (EntityInside(item))
			{
				UpdateTarget(item, visible: false, active: false, collidable: false);
			}
		}
	}

	public void ActivateOutside()
	{
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (!EntityInside(item))
			{
				UpdateTarget(item, visible: true, active: true, collidable: true);
			}
		}
	}

	public void DeactivateOutside()
	{
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (!EntityInside(item))
			{
				UpdateTarget(item, visible: false, active: false, collidable: false);
			}
		}
	}

	private void ActivateOnScreenDeactivateOffScreen()
	{
		Collider collider = base.Collider;
		Camera camera = SceneAs<Level>().Camera;
		float num = camera.Right - camera.Left;
		float num2 = camera.Bottom - camera.Top;
		base.Collider = new Hitbox(num * 3f, num2 * 3f, camera.Position.X - num - Position.X, camera.Position.Y - num2 - Position.Y);
		List<Entity> list = FindTargetEntities();
		foreach (Entity item in list)
		{
			if (EntityInside(item))
			{
				UpdateTarget(item, visible: true, active: true, collidable: true);
			}
			else
			{
				UpdateTarget(item, visible: false, active: false, collidable: false);
			}
		}
		base.Collider = collider;
	}

    private static HashSet<Type> SelectedTargetTypes(string targets)
    {
        if (targets == "FrostHelper.EntityMover")
            return new() { typeof(AppleEverestFrostEntityMover) };
        // The exact camera profile also names unavailable factory families.
        // Those types have no producer in the selected static closure. Keep
        // every reachable exact type, including both slope child platforms.
        return new()
        {
            typeof(AppleEverestXaphanPlayerPlatform), typeof(AppleEverestXaphanFakePlayerPlatform),
            typeof(ReflectionTentacles), typeof(Decal), typeof(Water), typeof(JumpthruPlatform),
            typeof(HangingLamp), typeof(MoonCreature), typeof(StarJumpBlock), typeof(PlaybackBillboard.FG),
            typeof(LightBeam), typeof(TempleEye), typeof(FloatySpaceBlock), typeof(Door), typeof(GlassBlock),
            typeof(FloatingDebris), typeof(FireBarrier), typeof(DustStaticSpinner), typeof(FlutterBird),
            typeof(Spring), typeof(SwapBlock.PathRenderer), typeof(DashBlock)
        };
    }
}
