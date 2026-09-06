#nullable disable
using System;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Registered by the generated static pool registry.
internal sealed class AppleEverestFrostCrystalDebris : Entity
{
	private readonly Image _image;

	private float _percent;

	private float _duration;

	private Vector2 _speed;

	private readonly Collision _collideH;

	private readonly Collision _collideV;

	private bool _bossShatter;

	private Rectangle _levelBounds;

	private readonly AppleEverestFrostDebrisMotion.MoveFastData _moveFast;

	public static ParticleType PDust => CrystalDebris.P_Dust;

	public AppleEverestFrostCrystalDebris()
		: base(Vector2.Zero)
	{
		base.Depth = -9990;
		base.Collider = new Hitbox(2f, 2f, -1f, -1f);
		_collideH = OnCollideH;
		_collideV = OnCollideV;
		_image = new Image(GFX.Game["particles/shard"]);
		_image.CenterOrigin();
		Add(new AppleEverestFrostOutlineImage(_image, Color.Black));
		_image.AppleEverestBindEntity(this);
		Visible = false;
		_moveFast = new AppleEverestFrostDebrisMotion.MoveFastData(this);
	}

	private void Init(Vector2 position, Color color, bool boss)
	{
		Position = position;
		_image.Color = color;
		_image.Scale = Vector2.One;
		_percent = 0f;
		_duration = 20f;
		_speed = Calc.AngleToVector(Calc.Random.NextAngle(), boss ? Calc.Random.Range(200, 240) : Calc.Random.Range(60, 160));
		_bossShatter = boss;
	}

	public override void Awake(Scene scene)
	{
		base.Awake(scene);
		_levelBounds = ((Level)scene).Bounds;
		_levelBounds.Inflate(8, 8);
		if (CollideCheck<Solid>())
		{
			RemoveSelf();
		}
	}

	public override void Update()
	{
		_speed.X = Calc.Clamp(_speed.X, -100000f, 100000f);
		_speed.Y = Calc.Clamp(_speed.Y, -100000f, 100000f);
		Level level = ((Level)base.Scene);
		if (_percent > 1f || !AppleEverestFrostDebrisMotion.Contains(_levelBounds, Position))
		{
			RemoveSelf();
			return;
		}
		_percent += Engine.DeltaTime / _duration;
		if (!_bossShatter)
		{
			_speed.X = Calc.Approach(_speed.X, 0f, Engine.DeltaTime * 20f);
			_speed.Y += 200f * Engine.DeltaTime;
		}
		else
		{
			_speed = _speed.SafeNormalize() * Calc.Approach(_speed.Length(), 0f, 300f * Engine.DeltaTime);
		}
		float num = _speed.Length();
		if (num > 0f)
		{
			_image.Rotation = _speed.Angle();
		}
		_image.Scale = Vector2.One * Calc.ClampedMap(_percent, 0.8f, 1f, 1f, 0f);
		_image.Scale.X *= Calc.ClampedMap(num, 0f, 400f, 1f, 2f);
		_image.Scale.Y *= Calc.ClampedMap(num, 0f, 400f, 1f, 0.2f);
		Vector2 position = Position;
		Hitbox hitbox = (Hitbox)base.Collider;
		Rectangle absRect = new Rectangle((int)hitbox.AbsoluteLeft, (int)hitbox.AbsoluteTop, (int)hitbox.Width, (int)hitbox.Height);
		Vector2 move = _speed * Engine.DeltaTime;
		_moveFast.HitboxRect = absRect;
		_moveFast.MoveBoth(move, _collideH, _collideV);
		if (Position == position && CollideCheck<Solid>())
		{
			RemoveSelf();
		}
		else if (level.OnInterval(0.05f))
		{
			level.ParticlesFG.Emit(PDust, Position);
		}
	}

	private void OnCollideH(CollisionData hit)
	{
		_speed.X *= -0.8f;
	}

	private void OnCollideV(CollisionData hit)
	{
		if (_bossShatter)
		{
			RemoveSelf();
			return;
		}
		if (Math.Sign(_speed.X) != 0)
		{
			_speed.X += Math.Sign(_speed.X) * 5;
		}
		else
		{
			_speed.X += Calc.Random.Choose(-1, 1) * 5;
		}
		_speed.Y *= -1.2f;
	}

	public static void Burst(Vector2 position, Color color, bool boss, int count = 1)
	{
		for (int i = 0; i < count; i++)
		{
			AppleEverestFrostCrystalDebris fastCrystalDebris = Engine.Pooler.Create<AppleEverestFrostCrystalDebris>();
			Vector2 position2 = position + new Vector2(Calc.Random.Range(-4, 4), Calc.Random.Range(-4, 4));
			fastCrystalDebris.Init(position2, color, boss);
			Engine.Scene.Add(fastCrystalDebris);
		}
	}
}
