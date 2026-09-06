#nullable disable
using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestXaphanPlayerPlatform : Solid
{
	private Vector2 StartPosition;

	private Vector2 EndPosition;

	public string Side;

	public bool Gentle;

	private bool CanSlide;

	private bool ForceSlide;

	public bool UpsideDown;

	public bool StickyDash;

	public bool CanJumpThrough;

	private int SlopeHeight;

	public float platfromWidth;

	public float slopeTop;

	public bool AffectPlayerSpeed;

	public bool Sliding;

	public bool preventCollision;

	public string PlayerPose = "";

	public bool CollidableForPlayer;

	private bool PreventRefillOnSliding;

	public AppleEverestXaphanPlayerPlatform(Vector2 position, int width, bool gentle, string side, int soundIndex, int slopeHeight, bool canSlide, bool forceSlide, float top, bool affectPlayerSpeed, bool upsideDown = false, bool stickyDash = false, bool canJumpThrough = false, bool preventRefillOnSliding = false)
		: base(position, width, 4f, safe: true)
	{
		AllowStaticMovers = false;
		Gentle = gentle;
		Side = side;
		UpsideDown = upsideDown;
		base.Collider = new Monocle.Hitbox(width, 4f, Gentle ? ((!(Side == "Left")) ? (-8) : 0) : 0, UpsideDown ? 4 : 8);
		SurfaceSoundIndex = soundIndex;
		SlopeHeight = slopeHeight;
		platfromWidth = width;
		CanSlide = canSlide;
		ForceSlide = forceSlide;
		slopeTop = top;
		AffectPlayerSpeed = affectPlayerSpeed;
		StickyDash = stickyDash;
		CanJumpThrough = canJumpThrough;
		PreventRefillOnSliding = preventRefillOnSliding;
	}

	public override void Added(Monocle.Scene scene)
	{
		base.Added(scene);
		StartPosition = Position;
	}

	public override void Update()
	{
		base.Update();
		PlayerPose = "";
		Player entity = SceneAs<Level>().Tracker.GetEntity<Player>();
		if (entity == null)
		{
			return;
		}
		if (Sliding && (entity.Sprite.CurrentAnimationID != "duck" || entity.Speed.X == 0f || Input.Jump.Pressed))
		{
			Sliding = false;
		}
		if (entity.Right <= base.Left - 16f || entity.Left >= base.Right + 16f)
		{
			Position = StartPosition;
		}
		else
		{
			if (entity.Right <= base.Left || entity.Left >= base.Right)
			{
				Position = StartPosition;
			}
			if (!UpsideDown)
			{
				if (Position.Y > StartPosition.Y)
				{
					Position.Y = StartPosition.Y;
				}
			}
			else if (Position.Y < StartPosition.Y)
			{
				Position.Y = StartPosition.Y;
			}
			if (entity.Sprite.Rate != 2f || (entity.Sprite.Rate == 2f && entity.Sprite.CurrentAnimationID == "wakeUp"))
			{
				if (!UpsideDown)
				{
					if (entity.Bottom > StartPosition.Y + 4f)
					{
						Collidable = false;
					}
					else
					{
						SetCollision(entity);
					}
					if (Side == "Left")
					{
						if (CanSlide && entity.IsRiding(this) && (ForceSlide || ((int)Input.MoveY == 1 && (int)Input.MoveX != -1)) && entity.Left >= base.Left)
						{
							Sliding = true;
							PlayerPose = "XaphanHelper_slopeSlide";
							entity.Sprite.Scale = new Vector2(1f, 1f);
							entity.Ducking = false;
							if (entity.Facing != Facings.Right)
							{
								entity.Facing = Facings.Right;
							}
							if (entity.Hair.Facing != Facings.Right)
							{
								entity.Hair.Facing = Facings.Right;
							}
							if (entity.Speed.X <= 250f - (Gentle ? 15f : 18f))
							{
								entity.Speed.X += ((!ForceSlide) ? (Gentle ? 15f : 18f) : (Gentle ? 30f : 28f));
							}
							else
							{
								entity.Speed.X = 250f;
							}
						}
						if (entity.BottomCenter.X < base.Right + 16f && Position.Y >= StartPosition.Y - (float)(8 * SlopeHeight) - 4f)
						{
							EndPosition = new Vector2(StartPosition.X, StartPosition.Y - (base.Right - (float)(Gentle ? (-4) : 0) - entity.BottomCenter.X + (SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Shinesparking") ? 16f : 4f)) / (float)((!Gentle) ? 1 : 2));
							Add(new Monocle.Coroutine(MoveSlope()));
						}
					}
					else if (Side == "Right")
					{
						if (CanSlide && entity.IsRiding(this) && (ForceSlide || ((int)Input.MoveY == 1 && (int)Input.MoveX != 1)) && entity.Right <= base.Right)
						{
							Sliding = true;
							PlayerPose = "XaphanHelper_slopeSlide";
							entity.Sprite.Scale = new Vector2(1f, 1f);
							entity.Ducking = false;
							if (entity.Facing != Facings.Left)
							{
								entity.Facing = Facings.Left;
							}
							if (entity.Hair.Facing != Facings.Left)
							{
								entity.Hair.Facing = Facings.Left;
							}
							if (entity.Speed.X >= -250f + (Gentle ? 15f : 18f))
							{
								entity.Speed.X -= ((!ForceSlide) ? (Gentle ? 15f : 18f) : (Gentle ? 30f : 28f));
							}
							else
							{
								entity.Speed.X = -250f;
							}
						}
						if (entity.BottomCenter.X > base.Left - 16f && Position.Y >= StartPosition.Y - (float)(8 * SlopeHeight) - 4f)
						{
							EndPosition = new Vector2(StartPosition.X, StartPosition.Y + (base.Left + (float)(Gentle ? (-4) : 0) - entity.BottomCenter.X - (SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Shinesparking") ? 16f : 4f)) / (float)((!Gentle) ? 1 : 2));
							Add(new Monocle.Coroutine(MoveSlope()));
						}
					}
				}
				else if (!SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Ceiling"))
				{
					if (entity.Top < StartPosition.Y + 12f)
					{
						Collidable = false;
					}
					else
					{
						SetCollision(entity);
					}
					if (Side == "Left")
					{
						if (entity.BottomCenter.X < base.Right && entity.BottomCenter.X > base.Left + 7f && Position.Y >= StartPosition.Y - (float)(8 * SlopeHeight) - 4f)
						{
							EndPosition = new Vector2(StartPosition.X, StartPosition.Y - (base.Right - (float)(Gentle ? (-4) : 0) - entity.BottomCenter.X + (!SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Shinesparking") ? (Gentle ? 8f : 4f) : (Gentle ? 16f : 8f))) / (float)((!Gentle) ? 1 : 2) * -1f - (float)(Gentle ? 2 : 0));
							Add(new Monocle.Coroutine(MoveSlope()));
						}
						if (entity.BottomCenter.X < base.Left + 7f)
						{
							Position.Y = StartPosition.Y + (float)(SlopeHeight * 8) + 4f;
						}
					}
					else if (Side == "Right")
					{
						if (entity.BottomCenter.X > base.Left && entity.BottomCenter.X < base.Right - 7f && Position.Y >= StartPosition.Y - (float)(8 * SlopeHeight) - 4f)
						{
							EndPosition = new Vector2(StartPosition.X, StartPosition.Y + (base.Left + (float)(Gentle ? (-4) : 0) - entity.BottomCenter.X - (!SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Shinesparking") ? (Gentle ? 8f : 4f) : (Gentle ? 16f : 8f))) / (float)((!Gentle) ? 1 : 2) * -1f - (float)(Gentle ? 2 : 0));
							Add(new Monocle.Coroutine(MoveSlope()));
						}
						if (entity.BottomCenter.X > base.Right - 7f)
						{
							Position.Y = StartPosition.Y + (float)(SlopeHeight * 8) + 4f;
						}
					}
				}
			}
			if (Collidable && !SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Ceiling"))
			{
				if (!UpsideDown && CollideCheck<Player>())
				{
					entity.Position -= Vector2.UnitY;
				}
				if (UpsideDown && CollideCheck<Player>())
				{
					entity.Position += Vector2.UnitY;
				}
			}
		}
		CollidableForPlayer = Collidable;
	}

	public IEnumerator MoveSlope()
	{
		if (!SceneAs<Level>().Session.GetFlag("Xaphan_Helper_Ceiling"))
		{
			MoveToY(Math.Max(EndPosition.Y, StartPosition.Y - (float)(8 * SlopeHeight) - 4f), 0f);
		}
		yield return null;
	}

	public void TurnOffCollision(bool state)
	{
		preventCollision = state;
	}

	public bool InView()
	{
		Monocle.Camera camera = (base.Scene as Level).Camera;
		if (base.X > camera.X - 16f && base.Y > camera.Y - 16f && base.X < camera.X + 320f + 16f)
		{
			return base.Y < camera.Y + 180f + 16f;
		}
		return false;
	}

	public void SetCollision(Player player)
	{
		if (!preventCollision)
		{
			if (player != null)
			{
				if (!UpsideDown)
				{
					if (CanJumpThrough)
					{
						if (player.Bottom <= base.Top + 2f)
						{
							Collidable = true;
						}
						else
						{
							Collidable = false;
						}
					}
					else
					{
						Collidable = true;
					}
				}
				else if (CanJumpThrough)
				{
					if (player.Top >= base.Bottom)
					{
						Collidable = true;
					}
					else
					{
						Collidable = false;
					}
				}
				else
				{
					Collidable = true;
				}
			}
			else
			{
				Collidable = false;
			}
		}
		else
		{
			Collidable = false;
		}
	}

	public override void DebugRender(Monocle.Camera camera) { }

	public void RestoreCollisionForPlayer()
	{
		Collidable = CollidableForPlayer;
	}
	public override void MoveVExact(int move)
	{
		Player entity = Scene.Tracker.GetEntity<Player>();
		if (!this.UpsideDown)
		{
			if (entity != null)
			{
				if (move < 0)
				{
					if (entity.IsRiding(this))
					{
						this.Collidable = false;
						if (entity.TreatNaive)
						{
							entity.NaiveMove(Vector2.UnitY * move);
						}
						else
						{
							entity.MoveVExact(move);
						}
						this.Collidable = true;
					}
					else if (!entity.TreatNaive && this.CollideCheck(entity, this.Position + Vector2.UnitY * move) && !this.CollideCheck(entity))
					{
						this.Collidable = false;
						entity.MoveVExact((int)(this.Top + (float)move - entity.Bottom));
						this.Collidable = true;
					}
				}
				else if (entity.IsRiding(this) && (this.StickyDash || entity.StateMachine.State != 2))
				{
					this.Collidable = false;
					if (entity.TreatNaive)
					{
						entity.NaiveMove(Vector2.UnitY * move);
					}
					else
					{
						entity.MoveVExact(move);
					}
					this.Collidable = true;
				}
			}
			this.Y += move;
			this.MoveStaticMovers(Vector2.UnitY * move);
		}
		else
		{
			base.MoveVExact(move);
		}
	}
}
