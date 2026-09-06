#nullable disable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestButtonHelper
{
	public struct ButtonRenderInfo
	{
		public readonly string Label;
		public readonly VirtualButton Button1, Button2, Button3, Button4;
		public readonly int ButtonCount;
		public readonly bool ShowFallback;
		public readonly Wiggler Wiggler;
		public float Button1Alpha, Button2Alpha, Button3Alpha, Button4Alpha;
		public ButtonRenderInfo(string label, VirtualButton button1 = null, VirtualButton button2 = null,
			VirtualButton button3 = null, VirtualButton button4 = null, Wiggler wiggler = null, bool showFallback = true)
		{
			Label = label; Button1 = button1; Button2 = button2; Button3 = button3; Button4 = button4;
			ButtonCount = button1 == null ? 0 : button2 == null ? 1 : button3 == null ? 2 : button4 == null ? 3 : 4;
			ShowFallback = showFallback; Wiggler = wiggler;
			Button1Alpha = Button2Alpha = Button3Alpha = Button4Alpha = 1f;
		}

		public float AlphaForButtonIndex(int index)
		{
			return index switch
			{
				0 => Button1Alpha,
				1 => Button2Alpha,
				2 => Button3Alpha,
				3 => Button4Alpha,
				_ => 0f,
			};
		}
	}

	private static readonly List<MTexture> multiButtonTextures = new List<MTexture>();

	public static void RenderMultiButton(ref Vector2 position, float xAdvance, ButtonRenderInfo renderInfo, float scale = 1f, float alpha = 1f, float justifyX = 0.5f, float wiggle = 1f, Wiggler wiggler = null)
	{
		float num = RenderMultiButton(position, renderInfo, scale, alpha, justifyX, wiggle, wiggler);
		if (justifyX < 0.5f)
		{
			position.X += num + xAdvance;
		}
		else
		{
			position.X -= num + xAdvance;
		}
	}

	public static float RenderMultiButton(Vector2 position, ButtonRenderInfo renderInfo, float scale = 1f, float alpha = 1f, float justifyX = 0.5f, float wiggle = 1f, Wiggler wiggler = null)
	{
		List<MTexture> textures = getTextures(renderInfo);
		float num = 0f;
		foreach (MTexture item in textures)
		{
			num += (float)(item?.Width ?? 0);
		}
		float x = ActiveFont.Measure(renderInfo.Label).X;
		float num2 = x + 8f + num;
		float justifyX2 = num2 / 2f / x;
		position.X += scale * num2 * (0.5f - justifyX);
		wiggle *= (wiggler ?? renderInfo.Wiggler)?.Value ?? 1f;
		drawText(renderInfo.Label, position, justifyX2, scale + wiggle, alpha);
		float num3 = x + 8f - num2 / 2f;
		for (int i = 0; i < textures.Count; i++)
		{
			MTexture mTexture = multiButtonTextures[i];
			if (mTexture != null)
			{
				Vector2 origin = new Vector2(0f - num3, (float)mTexture.Height / 2f);
				num3 += (float)mTexture.Width;
				float num4 = renderInfo.AlphaForButtonIndex(i);
				if (num4 > 0f)
				{
					mTexture.Draw(position, origin, Color.White * alpha * num4, scale + wiggle);
				}
			}
		}
		return num2 * scale;
	}

	private static void drawText(string text, Vector2 position, float justifyX, float scale, float alpha)
	{
		ActiveFont.DrawOutline(text, position, new Vector2(justifyX, 0.5f), Vector2.One * scale, Color.White * alpha, 2f, Color.Black * alpha);
	}

	private static List<MTexture> getTextures(ButtonRenderInfo renderInfo)
	{
		multiButtonTextures.Clear();
		string fallback = (renderInfo.ShowFallback ? "controls/keyboard/oemquestion" : null);
		if (renderInfo.Button1 != null)
		{
			multiButtonTextures.Add(Input.GuiButton(renderInfo.Button1, fallback: fallback));
		}
		if (renderInfo.Button2 != null)
		{
			multiButtonTextures.Add(Input.GuiButton(renderInfo.Button2, fallback: fallback));
		}
		if (renderInfo.Button3 != null)
		{
			multiButtonTextures.Add(Input.GuiButton(renderInfo.Button3, fallback: fallback));
		}
		if (renderInfo.Button4 != null)
		{
			multiButtonTextures.Add(Input.GuiButton(renderInfo.Button4, fallback: fallback));
		}
		return multiButtonTextures;
	}
}
