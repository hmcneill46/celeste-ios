// PandorasBox 1.0.49 selected colored water. All authored profiles allow
// jumping on the surface; the disabling Player.NormalUpdate branch is excluded.
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestColoredWater : Water
{
	private Color baseColor;

	private Color surfaceColor;

	private Color fillColor;

	private Color rayTopColor;

	private bool visibleOnCamera;

	private bool hasTopSurface;

	private bool hasBottomSurface;

	private bool hasLeftSurface;

	private bool hasRightSurface;

	private Vector2 previousPosition;

	private bool trackedPosition = false;

	private bool hasUpdatedFill = false;

	public Surface LeftSurface;

	public Surface RightSurface;

	private bool hasTopRays;

	private bool hasBottomRays;

	private bool hasLeftRays;

	private bool hasRightRays;

	public bool CanJumpOnSurface;

	private static int horizontalVisiblityBuffer = 40;

	private static int verticalVisiblityBuffer = 40;

	private static int horizontalWaterHeightVisiblityBuffer = 24;

	private static int verticalWaterHeightVisiblityBuffer = 24;

	private static int rayMaxLength = 128;

	public static Color CurrentRayTopColor = Color.LightSkyBlue * 0.6f;

	public static bool CurrentlyUpdating;

	private static float cameraTop;

	private static float cameraBottom;

	private static float cameraLeft;

	private static float cameraRight;

	private Rectangle waterFill;

	private HashSet<WaterInteraction> interactionContains;

	public AppleEverestColoredWater(EntityData data, Vector2 offset)
		: base(data.Position + offset, data.Bool("hasTop", defaultValue: true), data.Bool("hasBottom"), data.Width, data.Height)
	{
		baseColor = AppleEverestPandoraColors.Get(data.Attr("color", "#87CEFA"));
		surfaceColor = baseColor * 0.8f;
		fillColor = baseColor * 0.3f;
		rayTopColor = baseColor * 0.6f;
		hasLeftSurface = data.Bool("hasLeft");
		hasRightSurface = data.Bool("hasRight");
		hasTopRays = data.Bool("hasTopRays", defaultValue: true);
		hasBottomRays = data.Bool("hasBottomRays", defaultValue: true);
		hasLeftRays = data.Bool("hasLeftRays", defaultValue: true);
		hasRightRays = data.Bool("hasRightRays", defaultValue: true);
		CanJumpOnSurface = data.Bool("canJumpOnSurface", defaultValue: true);
		waterFill = AppleEverestFill;
		interactionContains = AppleEverestContains;
	}

		private void initializeSurfaces()
	{
		Color color = Water.FillColor;
		Color color2 = Water.SurfaceColor;
		Water.FillColor = fillColor;
		Water.SurfaceColor = surfaceColor;
		hasTopSurface = Surfaces.Contains(TopSurface);
		hasBottomSurface = Surfaces.Contains(BottomSurface);
		Surfaces.Clear();
		if (hasTopSurface)
		{
			TopSurface = new Surface(Position + new Vector2(base.Width / 2f, 8f), new Vector2(0f, -1f), base.Width, base.Height);
			Surfaces.Add(TopSurface);
			if (!hasTopRays)
			{
				TopSurface.Rays.Clear();
			}
		}
		if (hasBottomSurface)
		{
			BottomSurface = new Surface(Position + new Vector2(base.Width / 2f, base.Height - 8f), new Vector2(0f, 1f), base.Width, base.Height);
			Surfaces.Add(BottomSurface);
			if (!hasBottomRays)
			{
				BottomSurface.Rays.Clear();
			}
		}
		if (hasLeftSurface)
		{
			LeftSurface = new Surface(Position + new Vector2(8f, base.Height / 2f), new Vector2(-1f, 0f), base.Height, base.Width);
			Surfaces.Add(LeftSurface);
			if (!hasLeftRays)
			{
				LeftSurface.Rays.Clear();
			}
		}
		if (hasRightSurface)
		{
			RightSurface = new Surface(Position + new Vector2(base.Width - 8f, base.Height / 2f), new Vector2(1f, 0f), base.Height, base.Width);
			Surfaces.Add(RightSurface);
			if (!hasRightRays)
			{
				RightSurface.Rays.Clear();
			}
		}
		if (!hasUpdatedFill && (hasLeftSurface || hasRightSurface))
		{
			Rectangle rectangle = AppleEverestFill;
			int num = rectangle.X;
			int num2 = rectangle.Width;
			if (hasLeftSurface)
			{
				num += 8;
				num2 -= 8;
			}
			if (hasRightSurface)
			{
				num2 -= 8;
			}
			Rectangle rectangle2 = new Rectangle(num, rectangle.Y, num2, rectangle.Height);
			AppleEverestFill = rectangle2;
			hasUpdatedFill = true;
		}
		Water.FillColor = color;
		Water.SurfaceColor = color2;
	}

	private void updateVisiblity()
	{
		bool flag = base.X < cameraRight + (float)horizontalVisiblityBuffer && base.X + base.Width > cameraLeft - (float)horizontalVisiblityBuffer;
		bool flag2 = base.Y < cameraBottom + (float)verticalVisiblityBuffer && base.Y + base.Height > cameraTop - (float)verticalVisiblityBuffer;
		visibleOnCamera = flag & flag2;
	}

	public override void Render()
	{
		if (visibleOnCamera)
		{
			Color color = Water.FillColor;
			Color color2 = Water.SurfaceColor;
			Water.FillColor = fillColor;
			Water.SurfaceColor = surfaceColor;
			base.Render();
			Water.FillColor = color;
			Water.SurfaceColor = color2;
		}
	}

	private void rippleLeftRightSurfaces()
	{
		if (!visibleOnCamera)
		{
			return;
		}
		foreach (WaterInteraction component in base.Scene.Tracker.GetComponents<WaterInteraction>())
		{
			Rectangle bounds = component.Bounds;
			bool flag = interactionContains.Contains(component);
			bool flag2 = CollideRect(bounds);
			if (flag != flag2)
			{
				if (LeftSurface != null && (float)bounds.Center.X <= base.Center.X)
				{
					LeftSurface.DoRipple(new Vector2(bounds.Center.X, bounds.Center.Y), 1f);
				}
				if (RightSurface != null && (float)bounds.Center.X > base.Center.X)
				{
					RightSurface.DoRipple(new Vector2(bounds.Center.X, bounds.Center.Y), 1f);
				}
			}
		}
	}

	private void updateSurfacePositionsAndSize()
	{
		if (!trackedPosition)
		{
			previousPosition = Position;
			trackedPosition = true;
		}
		if (previousPosition != Position)
		{
			Vector2 vector = new Vector2((float)Math.Floor(Position.X), (float)Math.Floor(Position.Y));
			if (hasTopSurface)
			{
				TopSurface.Position = vector + new Vector2(base.Width / 2f, 8f);
			}
			if (hasBottomSurface)
			{
				BottomSurface.Position = vector + new Vector2(base.Width / 2f, base.Height - 8f);
			}
			if (hasLeftSurface)
			{
				LeftSurface.Position = vector + new Vector2(8f, base.Height / 2f);
			}
			if (hasRightSurface)
			{
				RightSurface.Position = vector + new Vector2(base.Width - 8f, base.Height / 2f);
			}
			previousPosition = Position;
		}
	}

	private void updateCamera(Camera camera)
	{
		cameraTop = camera.Top;
		cameraBottom = camera.Bottom;
		cameraLeft = camera.Left;
		cameraRight = camera.Right;
	}

	public override void Update()
	{
		Level level = base.Scene as Level;
		Color currentRayTopColor = Water.RayTopColor;
		updateCamera(level.Camera);
		updateVisiblity();
		CurrentlyUpdating = true;
		CurrentRayTopColor = rayTopColor;
		rippleLeftRightSurfaces();
		base.Update();
		updateSurfacePositionsAndSize();
		CurrentRayTopColor = currentRayTopColor;
		CurrentlyUpdating = false;
	}

	public override void Added(Scene scene)
	{
		initializeSurfaces();
		base.Added(scene);
	}

	private static Vector2 getVisibleSurfaceRange(Surface surface)
	{
		int num = 0;
		int num2 = surface.Width;
		float x = surface.Position.X;
		float y = surface.Position.Y;
		float num3 = surface.Width / 2;
		if (surface.Outwards.Y == -1f)
		{
			x -= num3;
			num = (int)Calc.Clamp(cameraLeft - (float)horizontalWaterHeightVisiblityBuffer - x, 0f, surface.Width);
			num2 = (int)Calc.Clamp(cameraRight + (float)horizontalWaterHeightVisiblityBuffer - x, 0f, surface.Width);
		}
		else if (surface.Outwards.Y == 1f)
		{
			x -= num3;
			num2 = surface.Width - (int)Calc.Clamp(cameraLeft - (float)horizontalWaterHeightVisiblityBuffer - x, 0f, surface.Width);
			num = surface.Width - (int)Calc.Clamp(cameraRight + (float)horizontalWaterHeightVisiblityBuffer - x, 0f, surface.Width);
		}
		else if (surface.Outwards.X == -1f)
		{
			y -= num3;
			num2 = surface.Width - (int)Calc.Clamp(cameraTop - (float)verticalWaterHeightVisiblityBuffer - y, 0f, surface.Width);
			num = surface.Width - (int)Calc.Clamp(cameraBottom + (float)verticalWaterHeightVisiblityBuffer - y, 0f, surface.Width);
		}
		else if (surface.Outwards.X == 1f)
		{
			y -= num3;
			num = (int)Calc.Clamp(cameraTop - (float)verticalWaterHeightVisiblityBuffer - y, 0f, surface.Width);
			num2 = (int)Calc.Clamp(cameraBottom + (float)verticalWaterHeightVisiblityBuffer - y, 0f, surface.Width);
		}
		if (!isSurfaceSectionVisible(surface, num, num2))
		{
			num = 0;
			num2 = -1;
		}
		return new Vector2(num, num2);
	}

	private static bool isSurfaceSectionVisible(Surface surface, float position1, float position2, float depth = 6f)
	{
		float num = surface.Position.X;
		float num2 = surface.Position.Y;
		float num3 = surface.Position.X;
		float num4 = surface.Position.Y;
		float num5 = surface.Width;
		float num6 = num5 / 2f;
		if (surface.Outwards.Y == -1f)
		{
			num += position1 - num6;
			num3 += position2 - num6;
			num4 += depth;
		}
		else if (surface.Outwards.Y == 1f)
		{
			num += num6 - position2;
			num3 += num6 - position1;
			num2 -= depth;
		}
		else if (surface.Outwards.X == -1f)
		{
			num2 += num6 - position2;
			num4 += num6 - position1;
			num3 += depth;
		}
		else if (surface.Outwards.X == 1f)
		{
			num2 += position1 - num6;
			num4 += position2 - num6;
			num -= depth;
		}
		bool flag = num < cameraRight + (float)horizontalWaterHeightVisiblityBuffer && num3 > cameraLeft - (float)horizontalWaterHeightVisiblityBuffer;
		bool flag2 = num2 < cameraBottom + (float)verticalWaterHeightVisiblityBuffer && num4 > cameraTop - (float)verticalWaterHeightVisiblityBuffer;
		return flag & flag2;
	}

	private static float getCachedSurfaceHeight(Surface surface, float[] cache, float position, float timer)
	{
		int num = (int)Math.Floor(position / 4f);
		if (num < 0 || num >= cache.Length)
		{
			return 6f;
		}
		if (cache[num] == 0f)
		{
			cache[num] = customGetHeight(surface, position, timer);
		}
		return cache[num];
	}

	public static float customGetHeight(Surface surface, float position, float timer)
	{
		if (position < 0f || position > (float)surface.Width)
		{
			return 0f;
		}
		float num = 0f;
		foreach (Ripple ripple in surface.Ripples)
		{
			float num2 = Math.Abs(ripple.Position - position);
			float num3 = 0f;
			num3 = ((!(num2 < 12f)) ? ((!(num2 < 16f)) ? ((!(num2 <= 32f)) ? 0f : ((num2 - 32f) / 16f * 0.75f)) : (-0.75f)) : (num2 / 16f * -1.75f + 1f));
			num += num3 * ripple.Height * Ease.CubeIn(1f - ripple.Percent);
		}
		num = Calc.Clamp(num, -4f, 4f);
		foreach (Tension tension in surface.Tensions)
		{
			float t = Calc.ClampedMap(Math.Abs(tension.Position - position), 0f, 24f, 1f, 0f);
			num += Ease.CubeOut(t) * tension.Strength * 12f;
		}
		float num4 = position / (float)surface.Width;
		num *= Math.Min(0.5f + num4 * 5f, 1f);
		num *= Math.Min(0.5f + (1f - num4) * 5f, 1f);
		num += (float)Math.Sin(timer + position * 0.1f);
		return num + 6f;
	}

	internal static void UpdateSurface(Surface self)
	{
		float num = self.appleEverestTimer;
		self.appleEverestTimer = num + Engine.DeltaTime;
		Vector2 vector = self.Outwards.Perpendicular();
		for (int num2 = self.Ripples.Count - 1; num2 >= 0; num2--)
		{
			Ripple ripple = self.Ripples[num2];
			if (ripple.Percent > 1f)
			{
				self.Ripples.RemoveAt(num2);
			}
			else
			{
				ripple.Position += ripple.Speed * Engine.DeltaTime;
				if (ripple.Position < 0f || ripple.Position > (float)self.Width)
				{
					ripple.Speed = 0f - ripple.Speed;
					ripple.Position = Calc.Clamp(ripple.Position, 0f, self.Width);
				}
				ripple.Percent += Engine.DeltaTime / ripple.Duration;
			}
		}
		if (!isSurfaceSectionVisible(self, 0f, self.Width, rayMaxLength))
		{
			return;
		}
		VertexPositionColor[] array = self.appleEverestMesh;
		int num3 = self.appleEverestFillIndex;
		int num4 = self.appleEverestSurfaceIndex;
		int num5 = self.appleEverestRayIndex;
		float[] cache = new float[(int)Math.Ceiling((float)self.Width / 4f) + 1];
		Vector2 visibleSurfaceRange = getVisibleSurfaceRange(self);
		int num6 = (int)visibleSurfaceRange.X;
		int num7 = (int)visibleSurfaceRange.Y;
		int num8 = num6;
		int num9 = num3;
		int num10 = num4;
		float num11 = self.Width / 2;
		float num12 = getCachedSurfaceHeight(self, cache, num8, num);
		while (num8 < num7)
		{
			int num13 = num8;
			int num14 = Math.Min(num8 + 4, self.Width);
			float cachedSurfaceHeight = getCachedSurfaceHeight(self, cache, num14, num);
			Vector2 vector2 = self.Outwards * num12;
			Vector2 vector3 = self.Outwards * cachedSurfaceHeight;
			Vector2 vector4 = self.Position + vector * (0f - num11 + (float)num13);
			Vector2 vector5 = self.Position + vector * (0f - num11 + (float)num14);
			array[num9].Position = new Vector3(vector4 + vector2, 0f);
			array[num9 + 1].Position = new Vector3(vector5 + vector3, 0f);
			array[num9 + 2].Position = new Vector3(vector4, 0f);
			array[num9 + 3].Position = new Vector3(vector5 + vector3, 0f);
			array[num9 + 4].Position = new Vector3(vector5, 0f);
			array[num9 + 5].Position = new Vector3(vector4, 0f);
			array[num10].Position = new Vector3(vector4 + self.Outwards * (num12 + 1f), 0f);
			array[num10 + 1].Position = new Vector3(vector5 + self.Outwards * (cachedSurfaceHeight + 1f), 0f);
			array[num10 + 2].Position = new Vector3(vector4 + vector2, 0f);
			array[num10 + 3].Position = new Vector3(vector5 + self.Outwards * (cachedSurfaceHeight + 1f), 0f);
			array[num10 + 4].Position = new Vector3(vector5 + vector3, 0f);
			array[num10 + 5].Position = new Vector3(vector4 + vector2, 0f);
			num8 += 4;
			num9 += 6;
			num10 += 6;
			num12 = cachedSurfaceHeight;
		}
		Vector2 vector6 = self.Position + vector * ((float)(-self.Width) / 2f);
		int num15 = num5;
		bool flag = isSurfaceSectionVisible(self, 0f, self.Width, rayMaxLength);
		foreach (Ray ray in self.Rays)
		{
			if (ray.Percent > 1f)
			{
				ray.Reset(0f);
			}
			ray.Percent += Engine.DeltaTime / ray.Duration;
			float num16 = 1f;
			if (!flag)
			{
				num15 += 6;
				continue;
			}
			float num17 = Math.Max(0f, ray.Position - ray.Width / 2f);
			float num18 = Math.Min(self.Width, ray.Position + ray.Width / 2f);
			if (!isSurfaceSectionVisible(self, num17, num18, rayMaxLength))
			{
				num15 += 6;
				continue;
			}
			if (ray.Percent < 0.1f)
			{
				num16 = ray.Percent * 10f;
			}
			else if (ray.Percent > 0.9f)
			{
				num16 = 1f - (ray.Percent - 0.9f) * 10f;
			}
			float num19 = Math.Min(self.BodyHeight, 0.7f * ray.Length);
			Vector2 vector7 = self.Outwards * num19;
			Color color = CurrentRayTopColor * num16;
			float num20 = 0.3f * ray.Length;
			Vector2 value = vector6 + vector * num17 + self.Outwards * getCachedSurfaceHeight(self, cache, num17, num);
			Vector2 value2 = vector6 + vector * num18 + self.Outwards * getCachedSurfaceHeight(self, cache, num18, num);
			Vector2 value3 = vector6 + vector * (num18 - num20) - vector7;
			Vector2 value4 = vector6 + vector * (num17 - num20) - vector7;
			Vector3 position = new Vector3(value2, 0f);
			Vector3 position2 = new Vector3(value4, 0f);
			array[num15].Position = new Vector3(value, 0f);
			array[num15].Color = color;
			array[num15 + 1].Position = position;
			array[num15 + 1].Color = color;
			array[num15 + 2].Position = position2;
			array[num15 + 3].Position = position;
			array[num15 + 3].Color = color;
			array[num15 + 4].Position = new Vector3(value3, 0f);
			array[num15 + 5].Position = position2;
			num15 += 6;
		}
	}

}
