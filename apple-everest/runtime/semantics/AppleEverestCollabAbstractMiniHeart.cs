#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal abstract class AppleEverestCollabAbstractMiniHeart : Entity
{
	protected static readonly int[] animationFrames = new int[23]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
		11, 12, 13
	};

	protected Sprite sprite;

	private string spriteName;

	private bool refillDash;

	private bool requireDashToBreak;

	private bool noGhostSprite;

	private string particleColor;

	private bool playPulseSound;

	protected Wiggler scaleWiggler;

	private Vector2 moveWiggleDir;

	private Wiggler moveWiggler;

	private float bounceSfxDelay;

	protected VertexLight light;

	protected BloomPoint bloom;

	private ParticleType shineParticle;

	private HoldableCollider holdableCollider;

	public AppleEverestCollabAbstractMiniHeart(EntityData data, Vector2 position, EntityID gid)
		: base(data.Position + position)
	{
		spriteName = data.Attr("sprite");
		refillDash = data.Bool("refillDash", defaultValue: true);
		requireDashToBreak = data.Bool("requireDashToBreak", defaultValue: true);
		noGhostSprite = data.Bool("noGhostSprite");
		particleColor = data.Attr("particleColor");
		playPulseSound = data.Bool("playPulseSound", defaultValue: true);
		base.Collider = new Hitbox(12f, 12f, -6f, -6f);
		Add(scaleWiggler = Wiggler.Create(0.5f, 4f, delegate(float f)
		{
			sprite.Scale = Vector2.One * (1f + f * 0.3f);
		}));
		moveWiggler = Wiggler.Create(0.8f, 2f);
		moveWiggler.StartZero = true;
		Add(moveWiggler);
		Add(new PlayerCollider(onPlayer));
		Add(holdableCollider = new HoldableCollider(onHoldable));
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		AreaKey area = (scene as Level).Session.Area;
		string text = "CollabUtils2/miniheart/" + spriteName + "/";
		bool heartGem = SaveData.Instance.Areas[area.ID].Modes[(int)area.Mode].HeartGem;
		if (heartGem && !noGhostSprite)
		{
			text += "ghost";
			if (!GFX.Game.Has(text + "00"))
			{
				text = "CollabUtils2/miniheart/ghost/ghost";
			}
		}
		Add(sprite = new Sprite(GFX.Game, text));
		sprite.AddLoop("idle", "", 0.1f, animationFrames);
		sprite.Play("idle");
		sprite.CenterOrigin();
		sprite.OnLoop = delegate
		{
			if (Visible)
			{
				if (playPulseSound)
				{
					Audio.Play("event:/SC2020_heartShard_pulse", Position);
				}
				scaleWiggler.Start();
				(base.Scene as Level).Displacement.AddBurst(Position + sprite.Position, 0.35f, 4f, 24f, 0.25f);
			}
		};
		Color value;
		switch (spriteName)
		{
		default:
			value = Color.Aqua;
			shineParticle = HeartGem.P_BlueShine;
			break;
		case "intermediate":
			value = Color.Red;
			shineParticle = HeartGem.P_RedShine;
			break;
		case "advanced":
			value = Color.Gold;
			shineParticle = HeartGem.P_GoldShine;
			break;
		case "expert":
			value = Color.Orange;
			shineParticle = new ParticleType(HeartGem.P_BlueShine)
			{
				Color = Color.Orange
			};
			break;
		case "grandmaster":
			value = Color.DarkViolet;
			shineParticle = new ParticleType(HeartGem.P_BlueShine)
			{
				Color = Color.DarkViolet
			};
			break;
		}
		if (!string.IsNullOrEmpty(particleColor))
		{
			shineParticle = new ParticleType(HeartGem.P_BlueShine)
			{
				Color = Calc.HexToColor(particleColor)
			};
		}
		if (heartGem && !noGhostSprite)
		{
			value = Color.White * 0.8f;
			shineParticle = new ParticleType(HeartGem.P_BlueShine)
			{
				Color = Calc.HexToColor("7589FF")
			};
		}
		value = Color.Lerp(value, Color.White, 0.5f);
		Add(light = new VertexLight(value, 1f, 32, 64));
		Add(bloom = new BloomPoint(0.75f, 16f));
	}

	public override void Update()
	{
		base.Update();
		bounceSfxDelay -= Engine.DeltaTime;
		sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;
		if (Visible && base.Scene.OnInterval(0.1f))
		{
			SceneAs<Level>().Particles.Emit(shineParticle, 1, base.Center + sprite.Position, Vector2.One * 4f);
		}
	}

	private void onPlayer(Player player)
	{
		Level level = base.Scene as Level;
		if (player.DashAttacking || !requireDashToBreak)
		{
			heartBroken(player, null, level);
			return;
		}
		int dashes = player.Dashes;
		player.PointBounce(base.Center);
		if (!refillDash)
		{
			player.Dashes = dashes;
		}
		moveWiggler.Start();
		scaleWiggler.Start();
		moveWiggleDir = (base.Center - player.Center).SafeNormalize(Vector2.UnitY);
		Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
		if (bounceSfxDelay <= 0f)
		{
			Audio.Play("event:/game/general/crystalheart_bounce", Position);
			bounceSfxDelay = 0.1f;
		}
	}

	public void onHoldable(Holdable holdable)
	{
		Player entity = base.Scene.Tracker.GetEntity<Player>();
		if (entity != null && holdable.Dangerous(holdableCollider))
		{
			heartBroken(entity, holdable, SceneAs<Level>());
		}
	}

	protected abstract void heartBroken(Player player, Holdable holdable, Level level);
}
