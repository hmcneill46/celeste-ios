#nullable disable
using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestXaphanFakePlayerPlatform : Solid
{
	private Vector2 StartPosition;

	private Vector2 EndPosition;

	public string Side;

	private bool Gentle;

	public bool UpsideDown;

	public bool StickyDash;

	public bool CanJumpThrough;

	private int SlopeHeight;

	public float platfromWidth;

	public float slopeTop;

	public AppleEverestXaphanFakePlayerPlatform(Vector2 position, int width, bool gentle, string side, int soundIndex, int slopeHeight, float top, bool upsideDown = false, bool stickyDash = false, bool canJumpThrough = false)
		: base(position, width, 4f, safe: true)
	{
		AllowStaticMovers = false;
		Gentle = gentle;
		Side = side;
		UpsideDown = upsideDown;
		Collidable = false;
		base.Collider = new Hitbox(width, 4f, Gentle ? ((!(Side == "Left")) ? (-8) : 0) : 0, UpsideDown ? 4 : 8);
		SurfaceSoundIndex = soundIndex;
		SlopeHeight = slopeHeight;
		platfromWidth = width;
		slopeTop = top;
		StickyDash = stickyDash;
		CanJumpThrough = canJumpThrough;
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		StartPosition = Position;
	}

	public override void Update()
	{
		// No selected producer creates Xaphan FakePlayer or Drone. Preserve the
		// ordinary Solid update before the exact source's missing-player return.
		base.Update();
	}



	public override void DebugRender(Camera camera)
	{
	}
}
