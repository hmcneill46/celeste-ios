#nullable disable
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabHoloRainbowBerry : Entity
{
	private float wobble;

	private Sprite sprite;

	private Sprite desaturatedSprite;

	private List<ParticleType> particleColors;

	private int particleColor;

	private float particleDelay;

	private AppleEverestCollabMemorialText counterText;

	public ParticleSystem Particles { get; private set; }

	public AppleEverestCollabHoloRainbowBerry(Vector2 position, int currentBerries, int totalBerries)
	{
		float num = (float)currentBerries / (float)totalBerries;
		float num2 = Math.Min(0.9f, num);
		float num3 = num * 0.5f;
		particleColors = new List<ParticleType>();
		for (int i = 0; i < 360; i += 60)
		{
			particleColors.Add(getParticle((float)i / 360f, num2, num3));
		}
		sprite = AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_holoRainbowBerry");
		desaturatedSprite = AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_desaturatedHoloRainbowBerry");
		sprite.Color *= num2 * num3;
		desaturatedSprite.Color *= num2 * (1f - num3);
		Add(sprite);
		Add(desaturatedSprite);
		particleDelay = MathHelper.Lerp(1f, 0.08f, num);
		Position = position;
		if (currentBerries != 0)
		{
			string text = $"{currentBerries}/{totalBerries}";
			counterText = new AppleEverestCollabMemorialText(new Entity(Position + new Vector2(1.5f, 82f)), dreamy: false, text, 16f);
		}
	}

	private static ParticleType getParticle(float hue, float transparency, float saturation)
	{
		Color color = Calc.HsvToColor(hue, saturation, 1f);
		Color color2 = Calc.HsvToColor(hue, saturation * 0.5f, 1f);
		return new ParticleType(Strawberry.P_Glow)
		{
			Color = color * transparency,
			Color2 = color2 * transparency
		};
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		if (counterText != null)
		{
			scene.Add(counterText);
		}
		ParticleSystem entity = (Particles = new ParticleSystem(-50000, 800));
		scene.Add(entity);
		Particles.Tag = base.Tag;
	}

	public override void Update()
	{
		base.Update();
		wobble += Engine.DeltaTime * 4f;
		sprite.Y = (float)Math.Sin(wobble) * 2f;
		desaturatedSprite.Y = sprite.Y;
		if (base.Scene.OnInterval(particleDelay))
		{
			ParticleType type = particleColors[particleColor % particleColors.Count];
			Particles.Emit(type, Position + Calc.Random.Range(-Vector2.One * 6f, Vector2.One * 6f));
			particleColor++;
		}
		if (counterText != null)
		{
			Player entity = base.Scene.Tracker.GetEntity<Player>();
			counterText.Show = entity != null && (entity.Position - Position).LengthSquared() < 2500f;
		}
	}

	public override void Removed(Scene scene)
	{
		base.Removed(scene);
		if (Particles != null)
		{
			scene.Remove(Particles);
		}
	}

	public override void SceneEnd(Scene scene)
	{
		base.SceneEnd(scene);
		if (Particles != null)
		{
			scene.Remove(Particles);
		}
	}
}
