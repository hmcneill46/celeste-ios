#nullable disable
using System.Linq;
using System;
using System.Collections;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabRainbowBerryUnlockCutscene : CutsceneEntity
{
	private AppleEverestCollabRainbowBerry strawberry;

	private AppleEverestCollabHoloRainbowBerry holoBerry;

	private int silverBerryCount;

	private ParticleSystem system;

	private EventInstance snapshot;

	private EventInstance sfx;

	private Image[] silverBerries;

	public AppleEverestCollabRainbowBerryUnlockCutscene(AppleEverestCollabRainbowBerry strawberry, AppleEverestCollabHoloRainbowBerry holoBerry, int silverBerryCount)
	{
		this.strawberry = strawberry;
		this.holoBerry = holoBerry;
		this.silverBerryCount = silverBerryCount;
	}

	public override void OnBegin(Level level)
	{
		Add(new Coroutine(Cutscene(level)));
	}

	private IEnumerator Cutscene(Level level)
	{
		Player player = base.Scene.Tracker.GetEntity<Player>();
		if (player != null)
		{
			while (!player.InControl)
			{
				yield return null;
			}
			player.StateMachine.State = 11;
		}
		sfx = Audio.Play("event:/game/general/seed_complete_main", Position);
		snapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute");
		silverBerries = new Image[silverBerryCount];
		for (int i = 0; i < silverBerryCount; i++)
		{
			silverBerries[i] = new Image(GFX.Game["CollabUtils2/silverBerry/idle00"]);
			silverBerries[i].Color = Color.White * 0f;
			silverBerries[i].CenterOrigin();
			if (player != null)
			{
				silverBerries[i].Position = player.Position;
			}
			Add(silverBerries[i]);
		}
		base.Depth = -2000003;
		strawberry.Depth = -2000002;
		holoBerry.Depth = 1999998;
		holoBerry.Particles.Depth = -2000002;
		strawberry.AddTag(Tags.FrozenUpdate);
		yield return 0.35f;
		base.Tag = Tags.FrozenUpdate;
		level.Frozen = true;
		level.FormationBackdrop.Display = true;
		level.FormationBackdrop.Alpha = 0.5f;
		level.Displacement.Clear();
		level.Displacement.Enabled = false;
		Audio.BusPaused("bus:/gameplay_sfx/ambience", true);
		Audio.BusPaused("bus:/gameplay_sfx/char", true);
		Audio.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", true);
		Audio.BusPaused("bus:/gameplay_sfx/game/chapters", true);
		yield return 0.1f;
		system = new ParticleSystem(-2000002, 50);
		system.Tag = Tags.FrozenUpdate;
		level.Add(system);
		float num = (float)Math.PI * 2f / (float)silverBerryCount;
		float num2 = (float)Math.PI / 2f;
		Image[] array = silverBerries;
		foreach (Image image in array)
		{
			startSpinAnimation(image, image.Position, strawberry.Position, num2, 4f);
			num2 -= num;
		}
		Vector2 cameraTarget = strawberry.Position - new Vector2(160f, 90f);
		cameraTarget = cameraTarget.Clamp(level.Bounds.Left, level.Bounds.Top, level.Bounds.Right - 320, level.Bounds.Bottom - 180);
		yield return 0.1f;
		Add(new Coroutine(CutsceneEntity.CameraTo(cameraTarget, 2f, Ease.CubeInOut)));
		yield return 3.9f;
		Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
		Audio.Play("event:/game/general/seed_complete_berry", strawberry.Position);
		array = silverBerries;
		foreach (Image silverBerry in array)
		{
			startCombineAnimation(silverBerry, strawberry.Position, 0.6f, system);
		}
		yield return 0.6f;
		Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
		array = silverBerries;
		for (int j = 0; j < array.Length; j++)
		{
			array[j].RemoveSelf();
		}
		holoBerry.RemoveSelf();
		strawberry.CollectedSeeds();
		yield return 0.5f;
		yield return CutsceneEntity.CameraTo(player.CameraTarget, 1f, Ease.CubeOut);
		level.EndCutscene();
		OnEnd(level);
	}

	public override void OnEnd(Level level)
	{
		if (WasSkipped)
		{
			Audio.Stop(sfx);
		}
		Player player = base.Scene.Tracker.GetEntity<Player>();
		if (player != null)
		{
			player.StateMachine.State = 0;
		}
		level.OnEndOfFrame += delegate
		{
			if (WasSkipped)
			{
				if (silverBerries != null)
				{
					Image[] array = silverBerries;
					for (int i = 0; i < array.Length; i++)
					{
						array[i].RemoveSelf();
					}
				}
				holoBerry.RemoveSelf();
				strawberry.CollectedSeeds();
				level.Camera.Position = player.CameraTarget;
			}
			strawberry.Depth = -100;
			strawberry.RemoveTag(Tags.FrozenUpdate);
			level.Frozen = false;
			level.FormationBackdrop.Display = false;
			level.Displacement.Enabled = true;
		};
		RemoveSelf();
	}

	private void endSfx()
	{
		Audio.BusPaused("bus:/gameplay_sfx/ambience", false);
		Audio.BusPaused("bus:/gameplay_sfx/char", false);
		Audio.BusPaused("bus:/gameplay_sfx/game/general/yes_pause", false);
		Audio.BusPaused("bus:/gameplay_sfx/game/chapters", false);
		Audio.ReleaseSnapshot(snapshot);
	}

	private void startSpinAnimation(Image silverBerry, Vector2 averagePos, Vector2 centerPos, float angleOffset, float time)
	{
		float spinLerp = 0f;
		Vector2 start = silverBerry.Position;
		Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time / 2f, start: true);
		tween.OnUpdate = delegate(Tween t)
		{
			spinLerp = t.Eased;
		};
		Add(tween);
		tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time, start: true);
		tween.OnUpdate = delegate(Tween t)
		{
			float angleRadians = (float)Math.PI / 2f + angleOffset - MathHelper.Lerp(0f, 32.201324f, t.Eased);
			Vector2 value = Vector2.Lerp(averagePos, centerPos, spinLerp) + Calc.AngleToVector(angleRadians, 25f);
			silverBerry.Position = Vector2.Lerp(start, value, spinLerp);
			silverBerry.Color = Color.White * spinLerp;
		};
		Add(tween);
	}

	private void startCombineAnimation(Image silverBerry, Vector2 centerPos, float time, ParticleSystem particleSystem)
	{
		Vector2 position = silverBerry.Position;
		float startAngle = Calc.Angle(centerPos, position);
		Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.BigBackIn, time, start: true);
		tween.OnUpdate = delegate(Tween t)
		{
			float angleRadians = MathHelper.Lerp(startAngle, startAngle - (float)Math.PI * 2f, Ease.CubeIn(t.Percent));
			float length = MathHelper.Lerp(25f, 0f, t.Eased);
			silverBerry.Position = centerPos + Calc.AngleToVector(angleRadians, length);
		};
		tween.OnComplete = delegate
		{
			silverBerry.Visible = false;
			for (int i = 0; i < 6; i++)
			{
				float num = Calc.Random.NextFloat((float)Math.PI * 2f);
				particleSystem.Emit(StrawberrySeed.P_Burst, 1, silverBerry.Position + Calc.AngleToVector(num, 4f), Vector2.Zero, num);
			}
			silverBerry.RemoveSelf();
		};
		Add(tween);
	}

	public override void Removed(Scene scene)
	{
		endSfx();
		base.Removed(scene);
	}

	public override void SceneEnd(Scene scene)
	{
		endSfx();
		base.SceneEnd(scene);
	}
}
