#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabMiniHeart : AppleEverestCollabAbstractMiniHeart
{
	private Sprite white;

	private bool hasBeenBroken;

	private readonly bool flash;

	private Coroutine smashRoutine;

	private EventInstance pauseMusicSnapshot;

	private SoundEmitter collectSound;

	public AppleEverestCollabMiniHeart(EntityData data, Vector2 position, EntityID gid)
		: base(data, position, gid)
	{
		flash = data.Bool("flash", defaultValue: true);
	}

	protected override void heartBroken(Player player, Holdable holdable, Level level)
	{
		if (!hasBeenBroken)
		{
			hasBeenBroken = true;
			Add(smashRoutine = new Coroutine(SmashRoutine(player, level)));
		}
	}

	private IEnumerator SmashRoutine(Player player, Level level)
	{
		level.CanRetry = false;
		Collidable = false;
		stopMusic();
        // The selected registration closure contains ordinary Strawberry and
        // these concrete strawberry subclasses; no runtime type discovery.
        var berries = new List<Strawberry>();
        foreach (Follower follower in player.Leader.Followers)
            if (follower.Entity is Strawberry berry && GeneratedAppleEverestGameplayRegistry.IsSelectedMiniHeartFollower(berry)) berries.Add(berry);
        foreach (Strawberry berry in berries) berry.OnCollect();
		collectSound = SoundEmitter.Play("event:/SC2020_heartShard_get", this);
		Add(white = new Sprite(GFX.Game, "CollabUtils2/miniheart/white/white"));
		white.AddLoop("idle", "", 0.1f, AppleEverestCollabAbstractMiniHeart.animationFrames);
		white.Play("idle");
		white.CenterOrigin();
		base.Depth = -2000000;
		yield return null;
		Celeste.Freeze(0.2f);
		yield return null;
		setTimeRate(0.5f);
		player.Depth = -2000000;
		for (int i = 0; i < 10; i++)
		{
			base.Scene.Add(new AbsorbOrb(Position));
		}
		level.Shake();
		Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
		if (flash)
		{
			level.Flash(Color.White);
		}
		light.Alpha = (bloom.Alpha = 0f);
		level.FormationBackdrop.Display = true;
		level.FormationBackdrop.Alpha = 1f;
		Visible = false;
		for (float time = 0f; time < 2f; time += Engine.RawDeltaTime)
		{
			setTimeRate(Calc.Approach(getTimeRate(), 0f, Engine.RawDeltaTime * 0.25f));
			yield return null;
		}
		base.Depth = 0;
		base.Depth = -2000000;
		yield return null;
		if (player.Dead)
		{
			yield return 100f;
		}
		setTimeRate(1f);
		base.Tag = Tags.FrozenUpdate;
		level.Frozen = true;
		SaveData.Instance.RegisterHeartGem(level.Session.Area);
		level.TimerStopped = true;
		level.PauseLock = true;
		level.RegisterAreaComplete();
		Audio.SetMusic(null);
		Audio.SetAmbience(null);
		float timer = 0f;
        while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed && timer <= 1f)
        {
            yield return null;
            timer += Engine.DeltaTime;
        }
        AppleEverestCollabRuntime.BeginCompletedReturn(level);
	}

	private static void setTimeRate(float timeRate)
	{
		Engine.TimeRate = timeRate;
	}

	private static float getTimeRate()
	{
		return Engine.TimeRate;
	}

	public override void Update()
	{
		base.Update();
		if (white != null)
		{
			white.Position = sprite.Position;
			white.Scale = sprite.Scale;
			white.SetAnimationFrame(sprite.CurrentAnimationFrame);
		}
		if (hasBeenBroken)
		{
			Player entity = base.Scene.Tracker.GetEntity<Player>();
			if (entity == null || entity.Dead)
			{
				interruptCollection();
			}
		}
	}

	private void interruptCollection()
	{
		Level obj = base.Scene as Level;
		obj.Frozen = false;
		obj.CanRetry = true;
		obj.FormationBackdrop.Display = false;
		setTimeRate(1f);
		if (collectSound != null)
		{
			collectSound.RemoveSelf();
			collectSound = null;
		}
		if (smashRoutine != null)
		{
			smashRoutine.RemoveSelf();
			smashRoutine = null;
		}
	}

	public override void Removed(Scene scene)
	{
		base.Removed(scene);
		resumeMusic();
	}

	public override void SceneEnd(Scene scene)
	{
		base.SceneEnd(scene);
		resumeMusic();
	}

	private void stopMusic()
	{
		if (pauseMusicSnapshot == null)
		{
			pauseMusicSnapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute");
		}
		Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
	}

	private void resumeMusic()
	{
		if (pauseMusicSnapshot != null)
		{
			Audio.ReleaseSnapshot(pauseMusicSnapshot);
			pauseMusicSnapshot = null;
		}
	}
}
