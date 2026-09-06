// MaxHelpingHand 1.40.9 exact per-backdrop fade composition.
// The selected graph has the standard 320 x 180 gameplay buffer.
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestStylegroundFadeController : Entity
{
	private static readonly HashSet<AppleEverestStylegroundFadeController> controllerSet = new HashSet<AppleEverestStylegroundFadeController>();

	private static readonly Dictionary<string, Dictionary<bool, float>> fades = new Dictionary<string, Dictionary<bool, float>>();

	private static readonly Dictionary<string, Dictionary<bool, float>> fadeInTimes = new Dictionary<string, Dictionary<bool, float>>();

	private static readonly Dictionary<string, Dictionary<bool, float>> fadeOutTimes = new Dictionary<string, Dictionary<bool, float>>();

	private static readonly Dictionary<string, Dictionary<bool, AppleEverestStylegroundFadeController>> controllers = new Dictionary<string, Dictionary<bool, AppleEverestStylegroundFadeController>>();

	internal static readonly string[] StaticFieldsToClone = new string[5] { "controllerSet", "fades", "fadeInTimes", "fadeOutTimes", "controllers" };

	private static VirtualRenderTarget tempRenderTarget = null;

	private readonly string[] flags;

	private readonly bool notFlag;

	private readonly float fadeInTime;

	private readonly float fadeOutTime;

	private static bool tryGetValue<T>(Dictionary<string, Dictionary<bool, T>> dictionary, string flag, bool notFlag, out T value)
	{
		value = default(T);
		if (dictionary.TryGetValue(flag, out var value2))
		{
			return value2.TryGetValue(notFlag, out value);
		}
		return false;
	}

	private static void setValue<T>(Dictionary<string, Dictionary<bool, T>> dictionary, string flag, bool notFlag, T value)
	{
		if (!dictionary.TryGetValue(flag, out var value2))
		{
			value2 = (dictionary[flag] = new Dictionary<bool, T>());
		}
		value2[notFlag] = value;

	}

	private static void removeValue<T>(Dictionary<string, Dictionary<bool, T>> dictionary, string flag, bool notFlag)
	{
		dictionary[flag].Remove(notFlag);
		if (dictionary[flag].Count == 0)
		{
			dictionary.Remove(flag);
		}

	}

	public AppleEverestStylegroundFadeController(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		flags = data.Attr("flag").Split(',');
		notFlag = data.Bool("notFlag");
		fadeInTime = data.Float("fadeInTime");
		fadeOutTime = data.Float("fadeOutTime");
	}

	public override void Awake(Scene scene)
	{
		base.Awake(scene);
		initializeFlag();
	}

	public override void Update()
	{
		base.Update();
		if (!controllerSet.Contains(this))
		{
			initializeFlag();
		}
	}

	public override void Removed(Scene scene)
	{
		base.Removed(scene);
		deregisterFlag();
	}

	public override void SceneEnd(Scene scene)
	{
		base.SceneEnd(scene);
		deregisterFlag();
	}

	private static void ensureBufferIsCorrect()
	{
		if (tempRenderTarget == null || tempRenderTarget.Width != 320 || tempRenderTarget.Height != 180)
		{
			tempRenderTarget?.Dispose();
			tempRenderTarget = VirtualContent.CreateRenderTarget("max-helping-hand-styleground-fade-controller", 320, 180);
		}
	}

	private void initializeFlag()
	{
		if (controllers.Count == 0)
		{
			ensureBufferIsCorrect();
		}
		string[] array = flags;
		foreach (string flag in array)
		{
			bool flag2 = SceneAs<Level>().Session.GetFlag(flag);
			if (notFlag)
			{
				flag2 = !flag2;
			}
			setValue(fades, flag, notFlag, flag2 ? 1 : 0);
			setValue(fadeInTimes, flag, notFlag, fadeInTime);
			setValue(fadeOutTimes, flag, notFlag, fadeOutTime);
			setValue(controllers, flag, notFlag, this);
			controllerSet.Add(this);
		}
	}

	private void deregisterFlag()
	{
		string[] array = flags;
		foreach (string flag in array)
		{
			if (tryGetValue(controllers, flag, notFlag, out var value) && value == this)
			{
				removeValue(fades, flag, notFlag);
				removeValue(fadeInTimes, flag, notFlag);
				removeValue(fadeOutTimes, flag, notFlag);
				removeValue(controllers, flag, notFlag);
			}
		}
		controllerSet.Remove(this);
		if (controllers.Count == 0)
		{
			tempRenderTarget?.Dispose();
			tempRenderTarget = null;
		}
	}

	internal static bool ForceVisible(Backdrop self)
	{
		if (controllers.Count == 0)
		{
			return false;
		}
		if (self.OnlyIfFlag != null && tryGetValue(fades, self.OnlyIfFlag, notFlag: false, out var value) && value > 0f)
		{
			return true;
		}
		if (self.OnlyIfNotFlag != null && tryGetValue(fades, self.OnlyIfNotFlag, notFlag: true, out value) && value > 0f)
		{
			return true;
		}
		return false;
	}

	internal static void AfterRendererUpdate(Scene scene)
	{
		if (controllers.Count == 0 || !(scene is Level level))
		{
			return;
		}
		float num = Engine.DeltaTime / 2f;
		foreach (string key in fades.Keys)
		{
			foreach (bool key2 in fades[key].Keys)
			{
				if (level.Session.GetFlag(key) != key2)
				{
					fades[key][key2] = Calc.Approach(fades[key][key2], 1f, num / fadeInTimes[key][key2]);
				}
				else
				{
					fades[key][key2] = Calc.Approach(fades[key][key2], 0f, num / fadeOutTimes[key][key2]);
				}
			}
		}
	}

	internal static void RenderStart(BackdropRenderer self, Backdrop backdrop)
	{
		if (controllers.Count == 0)
		{
			return;
		}
		bool num = backdrop.OnlyIfFlag != null && tryGetValue(fades, backdrop.OnlyIfFlag, notFlag: false, out var value) && value < 1f;
		bool flag = backdrop.OnlyIfNotFlag != null && tryGetValue(fades, backdrop.OnlyIfNotFlag, notFlag: true, out value) && value < 1f;
		if (!(num | flag))
		{
			return;
		}
		self.EndSpritebatch();
		ensureBufferIsCorrect();
		Engine.Graphics.GraphicsDevice.SetRenderTarget(tempRenderTarget);
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		if (backdrop.UseSpritebatch)
		{
			if (backdrop is Parallax)
			{
				self.StartSpritebatchLooping(BlendState.AlphaBlend);
			}
			else
			{
				self.StartSpritebatch(BlendState.AlphaBlend);
			}
		}
	}

	internal static void RenderEnd(BackdropRenderer self, Backdrop backdrop, BlendState blendState)
	{
		if (controllers.Count != 0)
		{
			string text = null;
			bool key = false;
			if (backdrop.OnlyIfFlag != null && tryGetValue(fades, backdrop.OnlyIfFlag, notFlag: false, out var value) && value < 1f)
			{
				text = backdrop.OnlyIfFlag;
				key = false;
			}
			if (backdrop.OnlyIfNotFlag != null && tryGetValue(fades, backdrop.OnlyIfNotFlag, notFlag: true, out value) && value < 1f)
			{
				text = backdrop.OnlyIfNotFlag;
				key = true;
			}
			if (text != null)
			{
				self.EndSpritebatch();
				Engine.Graphics.GraphicsDevice.SetRenderTarget(GameplayBuffers.Level);
				self.StartSpritebatch(blendState);
				Draw.SpriteBatch.Draw((RenderTarget2D)tempRenderTarget, Vector2.Zero, Color.White * fades[text][key]);
			}
		}
	}
}
