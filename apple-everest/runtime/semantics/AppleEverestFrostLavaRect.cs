// FrostHelper 1.80.1 exact selected non-rainbow lava mesh and bubbles.
#nullable disable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFrostLavaRect : Component
{
	internal struct WaveData
	{
		public float Amplitude, WaveNumber, Frequency, Phase;
		public WaveData(float amplitude, float waveNumber, float frequency)
		{ Amplitude = amplitude; WaveNumber = waveNumber; Frequency = frequency; Phase = 0f; }
		public float Get(float val, float timer) => Sin(val * WaveNumber + timer * Frequency + Phase) * Amplitude;
	}

	[Flags]
	internal enum RainbowModes
	{
		None = 0,
		Surface = 1,
		Edge = 2,
		Bubble = 4,
		All = Surface | Edge | Bubble
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct VertexPositionColorNumerics
	{
		public System.Numerics.Vector2 Position;

		public float PositionZ;

		public Color Color;
	}

	private interface IWaveProvider
	{
		float Wave(AppleEverestFrostLavaRect rect, int step, float length);
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	private struct DefaultWaveProvider : IWaveProvider
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float Wave(AppleEverestFrostLavaRect rect, int step, float length)
		{
			return rect.Wave(step, length);
		}
	}

	private unsafe struct PrecalculatedWaveProvider : IWaveProvider
	{
		private float* waves;
		public PrecalculatedWaveProvider(float* value) { waves = value; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe float Wave(AppleEverestFrostLavaRect rect, int step, float length)
		{
			return waves[step];
		}
	}

	public enum OnlyModes
	{
		All,
		OnlyTop,
		OnlyBottom
	}

	public struct Bubble
	{
		public Microsoft.Xna.Framework.Vector2 Position;

		public float Speed;

		public float Alpha;

		public byte Type;
	}

	public struct SurfaceBubble
	{
		public float X;

		public float Frame;

		public byte Animation;
	}

	internal List<WaveData> Waves;

	public Microsoft.Xna.Framework.Vector2 Position;

	public float Fade = 16f;

	public float Spikey;

	public OnlyModes OnlyMode;

	public float CurveAmplitude = 12f;

	public float UpdateMultiplier = 1f;

	public Color SurfaceColor = Color.White;

	public Color EdgeColor = Color.LightGray;

	public Color CenterColor = Color.DarkGray;

	internal RainbowModes IsRainbow;

	private float _timer = Calc.Random.NextFloat(100f);

	private VertexPositionColor[] _verts;

	private bool _dirty;

	private int _vertCount;

	private Bubble[]? _bubbles;

	private SurfaceBubble[]? _surfaceBubbles;

	private int _surfaceBubbleIndex;

	private List<List<MTexture>>? _surfaceBubbleAnimations;

	private float _bubbleAmountMultiplier;

	public bool HasBubbles => _bubbleAmountMultiplier > 0f;

	public int SurfaceStep { get; set; }

	public float Width { get; set; }

	public float Height { get; set; }

	private List<MTexture> _bubbleTextures { get; set; }

	private void SetupBubbles(string config)
	{
		if (config != "1") throw new InvalidOperationException("Unreviewed lava bubble profile");
		_bubbleAmountMultiplier = 1f;
		_bubbleTextures = new List<MTexture> { GFX.Game["particles/bubble"] };
		_surfaceBubbleAnimations = new List<List<MTexture>> { GFX.Game.GetAtlasSubtextures("danger/lava/bubble_a") };
	}

	public AppleEverestFrostLavaRect(float width, float height, int step, string bubbleConfig)
		: base(active: true, visible: true)
	{
		SetupBubbles(bubbleConfig);
		Resize(width, height, step);
	}

	public void Resize(float width, float height, int step)
	{
		Width = width;
		Height = height;
		SurfaceStep = step;
		_dirty = true;
		_verts = new VertexPositionColor[(int)((double)width / (double)SurfaceStep * 2.0 + (double)height / (double)SurfaceStep * 2.0 + 4.0) * 3 * 6 + 6];
		if (HasBubbles)
		{
			_bubbles = new Bubble[(int)((double)width * (double)height * 0.004999999888241291 * (double)_bubbleAmountMultiplier)];
			_surfaceBubbles = new SurfaceBubble[(int)Math.Max(4.0, (double)_bubbles.Length * 0.25)];
			for (int i = 0; i < _bubbles.Length; i++)
			{
				ref Bubble reference = ref _bubbles[i];
				reference.Position = new Microsoft.Xna.Framework.Vector2(1f + Calc.Random.NextFloat(Width - 2f), Calc.Random.NextFloat(Height));
				reference.Speed = Calc.Random.Range(4, 12);
				reference.Alpha = Calc.Random.Range(0.4f, 0.8f);
				reference.Type = (byte)((_bubbleTextures.Count != 1) ? ((byte)Calc.Random.Next(_bubbleTextures.Count)) : 0);
			}
			for (int j = 0; j < _surfaceBubbles.Length; j++)
			{
				_surfaceBubbles[j].X = -1f;
			}
		}
	}

	private Rectangle GetCullRect()
	{
		Microsoft.Xna.Framework.Vector2 vector = base.Entity.Position + Position;
		Rectangle result = new Rectangle((int)vector.X, (int)vector.Y, (int)Width, (int)Height);
		result.Inflate(16, 16);
		return result;
	}

	private bool IsVisible(out Rectangle cullRect)
	{
		Camera camera = (base.Scene as Level).Camera;
		cullRect = GetCullRect();
		return AppleEverestFrostLavaGeometry.Visible(cullRect, camera);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public override void Update()
	{
		if (!IsVisible(out var _))
		{
			return;
		}
		_timer += UpdateMultiplier * Engine.DeltaTime;
		if ((double)UpdateMultiplier != 0.0 || IsRainbow != RainbowModes.None)
		{
			_dirty = true;
		}
		if (HasBubbles)
		{
			Bubble[] bubbles = _bubbles;
			for (int i = 0; i < bubbles.Length; i++)
			{
				ref Bubble reference = ref bubbles[i];
				reference.Position.Y -= UpdateMultiplier * reference.Speed * Engine.DeltaTime;
				if ((double)reference.Position.Y < 2.0 - (double)Wave((int)((double)reference.Position.X / (double)SurfaceStep), Width))
				{
					reference.Position.Y = Height - 1f;
					if (Calc.Random.Chance(0.75f))
					{
						ref SurfaceBubble reference2 = ref _surfaceBubbles[_surfaceBubbleIndex];
						reference2.X = reference.Position.X;
						reference2.Frame = 0f;
						reference2.Animation = (byte)Calc.Random.Next(_surfaceBubbleAnimations.Count);
						_surfaceBubbleIndex = (_surfaceBubbleIndex + 1) % _surfaceBubbles.Length;
					}
				}
			}
			SurfaceBubble[] surfaceBubbles = _surfaceBubbles;
			for (int j = 0; j < surfaceBubbles.Length; j++)
			{
				ref SurfaceBubble reference3 = ref surfaceBubbles[j];
				if ((double)reference3.X >= 0.0)
				{
					reference3.Frame += Engine.DeltaTime * 6f;
					if (reference3.Frame >= (float)_surfaceBubbleAnimations[reference3.Animation].Count)
					{
						reference3.X = -1f;
					}
				}
			}
		}
		base.Update();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Sin(float value)
	{
		return (1f + float.Sin(value)) / 2f;
	}

	public float Wave(int step, float length)
	{
		int num = step * SurfaceStep;
		float num2 = ((OnlyMode != OnlyModes.All) ? 1f : (Calc.ClampedMap(num, 0f, length * 0.1f) * Calc.ClampedMap(num, length * 0.9f, length, 1f, 0f)));
		float num3 = 0f;
		Span<WaveData> span = CollectionsMarshal.AsSpan(Waves);
		for (int i = 0; i < span.Length; i++)
		{
			WaveData waveData = span[i];
			num3 += waveData.Get(num, _timer);
		}
		if (step % 2 == 0)
		{
			num3 += Spikey;
		}
		if (OnlyMode != OnlyModes.All)
		{
			num3 += (1f - Calc.YoYo((float)num / length)) * CurveAmplitude;
		}
		return num3 * num2;
	}

	public void Quad(ref int vert, System.Numerics.Vector2 va, System.Numerics.Vector2 vb, System.Numerics.Vector2 vc, System.Numerics.Vector2 vd, Color color)
	{
		Quad(ref vert, va, color, vb, color, vc, color, vd, color);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe void Quad(ref int vert, System.Numerics.Vector2 va, Color ca, System.Numerics.Vector2 vb, Color cb, System.Numerics.Vector2 vc, Color cc, System.Numerics.Vector2 vd, Color cd)
	{
		fixed (VertexPositionColor* ptr = &_verts[vert])
		{
			VertexPositionColorNumerics* ptr2 = (VertexPositionColorNumerics*)ptr;
			ptr2->Position = va;
			ptr2->Color = ca;
			ptr2++;
			ptr2->Position = vb;
			ptr2->Color = cb;
			ptr2++;
			ptr2->Position = vc;
			ptr2->Color = cc;
			ptr2++;
			*ptr2 = ptr2[-3];
			ptr2++;
			*ptr2 = ptr2[-2];
			ptr2++;
			ptr2->Position = vd;
			ptr2->Color = cd;
		}
		vert += 6;
	}

	private void Edge<TWave>(ref int vert, System.Numerics.Vector2 a, System.Numerics.Vector2 b, float fade, float insetFade, TWave waveProvider, Rectangle cullRect) where TWave : struct, IWaveProvider
	{
		float num = (a - b).Length();
		float num2 = num / (float)SurfaceStep;
		System.Numerics.Vector2 vector = new System.Numerics.Vector2(float.Clamp(a.X, cullRect.Left, cullRect.Right), float.Clamp(a.Y, cullRect.Top, cullRect.Bottom));
		System.Numerics.Vector2 vector2 = new System.Numerics.Vector2(float.Clamp(b.X, cullRect.Left, cullRect.Right), float.Clamp(b.Y, cullRect.Top, cullRect.Bottom));
		float num3 = float.Max(fade, insetFade) / (float)SurfaceStep / 2f;
		float num4 = (vector - vector2).Length() / (float)SurfaceStep;
		int x = (int)float.Ceiling((a - vector).Length() / (float)SurfaceStep - num3);
		x = int.Max(x, 0);
		num4 += (float)x;
		num4 = float.Min(num2, num4 + num3 * 2f);
		if ((float)x > num4)
		{
			return;
		}
		float num5 = ((OnlyMode == OnlyModes.All) ? (insetFade / num) : 0f);
		System.Numerics.Vector2 vector3 = AppleEverestFrostLavaGeometry.Normal(b - a);
		float num6 = waveProvider.Wave(this, x, num);
		System.Numerics.Vector2 vector4 = a - vector3 * num6;
		Color surfaceColor = GetSurfaceColor(vector4);
		Color cd = GetSurfaceColor(vector4 + vector3);
		Color edgeColor = GetEdgeColor(vector4 + vector3);
		System.Numerics.Vector2 vector5 = System.Numerics.Vector2.Lerp(a, b, num5);
		Color color = GetCenterColor(vector5 + vector3 * (fade - num6));
		for (int i = x + 1; (float)i <= num4; i++)
		{
			float num7 = (float)i / num2;
			if (num7 > 1f)
			{
				num7 = 1f;
			}
			System.Numerics.Vector2 vector6 = System.Numerics.Vector2.Lerp(a, b, num7);
			float num8 = waveProvider.Wave(this, i, num);
			System.Numerics.Vector2 vector7 = vector6 - vector3 * num8;
			System.Numerics.Vector2 vector8 = System.Numerics.Vector2.Lerp(a, b, Calc.ClampedMap(num7, 0f, 1f, num5, 1f - num5));
			System.Numerics.Vector2 vector9 = vector8 + vector3 * (fade - num8);
			Color centerColor = GetCenterColor(vector9);
			System.Numerics.Vector2 vector10 = vector5 + vector3 * (fade - num6);
			Quad(ref vert, vector4 + vector3, edgeColor, vector7 + vector3, edgeColor = GetEdgeColor(vector7 + vector3), vector9, centerColor, vector10, color);
			Quad(ref vert, vector10, color, vector9, centerColor, vector8 + vector3 * fade, GetCenterColor(vector5 + vector3 * fade), vector5 + vector3 * fade, GetCenterColor(vector5 + vector3 * fade));
			Color surfaceColor2 = GetSurfaceColor(vector7 + vector3);
			Quad(ref vert, vector4, surfaceColor, vector7, surfaceColor = GetSurfaceColor(vector7), vector7 + vector3, surfaceColor2, vector4 + vector3, cd);
			num6 = num8;
			vector4 = vector7;
			cd = surfaceColor2;
			vector5 = vector8;
			color = centerColor;
		}
	}

	public unsafe override void Render()
	{
		if (!IsVisible(out var cullRect))
		{
			return;
		}
		GameplayRenderer.End();
		Level level = base.Scene as Level;
		Camera camera = level.Camera;
		Microsoft.Xna.Framework.Vector2 vector = base.Entity.Position + Position;
		Rectangle r = ((UpdateMultiplier == 0f || level.Transitioning) ? cullRect : AppleEverestFrostLavaGeometry.VisibleSection(cullRect, camera));
		if (_dirty || (level.Transitioning && IsRainbow != RainbowModes.None))
		{
			System.Numerics.Vector2 vector2 = default(System.Numerics.Vector2);
			System.Numerics.Vector2 vector3 = new System.Numerics.Vector2(Width, 0f);
			System.Numerics.Vector2 vector4 = new System.Numerics.Vector2(0f, Height);
			System.Numerics.Vector2 vector5 = new System.Numerics.Vector2(Width, Height);
			System.Numerics.Vector2 vector6 = new System.Numerics.Vector2(Math.Min(Fade, Width / 2f), Math.Min(Fade, Height / 2f));
			Rectangle cullRect2 = new Rectangle(r.X + (int)-vector.X, r.Y + (int)-vector.Y, r.Width, r.Height);
			_vertCount = 0;
			if (OnlyMode == OnlyModes.All)
			{
				int num = (int)float.Ceiling(Width / (float)SurfaceStep) + 1;
				Span<float> span = stackalloc float[num];
				for (int i = 0; i < num; i++)
				{
					span[i] = Wave(i, Width);
				}
				int num2 = (int)float.Ceiling(Height / (float)SurfaceStep) + 1;
				Span<float> span2 = ((Width != Height) ? stackalloc float[num2] : span);
				Span<float> span3 = span2;
				if (Width != Height)
				{
					for (int j = 0; j < num2; j++)
					{
						span3[j] = Wave(j, Height);
					}
				}
				fixed (float* widthWaves = span)
				fixed (float* heightWaves = span3)
				{
				PrecalculatedWaveProvider waveProvider = new PrecalculatedWaveProvider(widthWaves);
				PrecalculatedWaveProvider waveProvider2 = new PrecalculatedWaveProvider(heightWaves);
				Edge(ref _vertCount, vector2, vector3, vector6.Y, vector6.X, waveProvider, cullRect2);
				Edge(ref _vertCount, vector3, vector5, vector6.X, vector6.Y, waveProvider2, cullRect2);
				Edge(ref _vertCount, vector5, vector4, vector6.Y, vector6.X, waveProvider, cullRect2);
				Edge(ref _vertCount, vector4, vector2, vector6.X, vector6.Y, waveProvider2, cullRect2);
				}
				Quad(ref _vertCount, vector2 + vector6, GetCenterColor(vector2 + vector6), vector3 + new System.Numerics.Vector2(0f - vector6.X, vector6.Y), GetCenterColor(vector3 + new System.Numerics.Vector2(0f - vector6.X, vector6.Y)), vector5 - vector6, GetCenterColor(vector5 - vector6), vector4 + new System.Numerics.Vector2(vector6.X, 0f - vector6.Y), GetCenterColor(vector4 + new System.Numerics.Vector2(vector6.X, 0f - vector6.Y)));
			}
			else if (OnlyMode == OnlyModes.OnlyTop)
			{
				Edge(ref _vertCount, vector2, vector3, vector6.Y, 0f, default(DefaultWaveProvider), cullRect2);
				Quad(ref _vertCount, vector2 + new System.Numerics.Vector2(0f, vector6.Y), vector3 + new System.Numerics.Vector2(0f, vector6.Y), vector5, vector4, CenterColor);
			}
			else if (OnlyMode == OnlyModes.OnlyBottom)
			{
				Edge(ref _vertCount, vector5, vector4, vector6.Y, 0f, default(DefaultWaveProvider), cullRect2);
				Quad(ref _vertCount, vector2, vector3, vector5 + new System.Numerics.Vector2(0f, 0f - vector6.Y), vector4 + new System.Numerics.Vector2(0f, 0f - vector6.Y), CenterColor);
			}
			_dirty = false;
		}
		VirtualRenderTarget virtualRenderTarget = AppleEverestFrostLavaGeometry.Buffer();
		GraphicsDevice graphicsDevice = Draw.SpriteBatch.GraphicsDevice;
		RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
		graphicsDevice.SetRenderTarget(virtualRenderTarget);
		graphicsDevice.Clear(Color.Transparent);
		GFX.DrawVertices(Matrix.CreateTranslation(new Microsoft.Xna.Framework.Vector3(vector, 0f)) * camera.Matrix, _verts, _vertCount, null, BlendState.Opaque);
		graphicsDevice.SetRenderTargets(previousTargets);
		GameplayRenderer.Begin();
		Draw.SpriteBatch.Draw((RenderTarget2D)virtualRenderTarget, camera.Position, Color.White);
		if (!HasBubbles)
		{
			return;
		}
		Bubble[] bubbles = _bubbles;
		Span<MTexture> span4 = CollectionsMarshal.AsSpan(_bubbleTextures);
		for (int k = 0; k < bubbles.Length; k++)
		{
			ref Bubble reference = ref bubbles[k];
			Microsoft.Xna.Framework.Vector2 vector7 = vector + reference.Position;
			if (r.Contains(new Point((int)vector7.X, (int)vector7.Y)))
			{
				span4[reference.Type].DrawCentered(vector7, GetBubbleColor(new System.Numerics.Vector2(vector7.X, vector7.Y)) * bubbles[k].Alpha);
			}
		}
		SurfaceBubble[] surfaceBubbles = _surfaceBubbles;
		for (int l = 0; l < surfaceBubbles.Length; l++)
		{
			ref SurfaceBubble reference2 = ref surfaceBubbles[l];
			if ((double)reference2.X >= 0.0)
			{
				MTexture mTexture = _surfaceBubbleAnimations[reference2.Animation][(int)reference2.Frame];
				int num3 = (int)(reference2.X / (float)SurfaceStep);
				float y = 1f - Wave(num3, Width);
				Microsoft.Xna.Framework.Vector2 vector8 = vector + new Microsoft.Xna.Framework.Vector2(num3 * SurfaceStep, y);
				if (r.Contains(new Point((int)vector8.X, (int)vector8.Y)))
				{
					mTexture.DrawJustified(vector8, new Microsoft.Xna.Framework.Vector2(0.5f, 1f), GetBubbleColor(new System.Numerics.Vector2(vector8.X, vector8.Y)));
				}
			}
		}
	}

	public Color GetBubbleColor(System.Numerics.Vector2 pos) => SurfaceColor;
	public Color GetSurfaceColor(System.Numerics.Vector2 pos) => SurfaceColor;
	public Color GetEdgeColor(System.Numerics.Vector2 pos) => EdgeColor;
	public Color GetCenterColor(System.Numerics.Vector2 pos) => CenterColor;
}
