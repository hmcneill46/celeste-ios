#nullable disable
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabAssistSkipConfirmUI : Entity
{
	private float openingEase;

	private bool opened;

	private int currentlySelectedOption;

	private Action onConfirm;

	private Action onCancel;

	private Wiggler wiggler;

	public AppleEverestCollabAssistSkipConfirmUI(Action onConfirm, Action onCancel)
	{
		this.onConfirm = onConfirm;
		this.onCancel = onCancel;
		base.Tag = (int)Tags.HUD | (int)Tags.PauseUpdate;
		Add(wiggler = Wiggler.Create(0.4f, 4f));
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		opened = true;
	}

	public override void Update()
	{
		base.Update();
		openingEase = Calc.Approach(openingEase, opened ? 1f : 0f, Engine.DeltaTime * 4f);
		if (opened)
		{
			if (Input.MenuCancel.Pressed)
			{
				opened = false;
				wiggler.Start();
				Audio.Play("event:/ui/main/button_back");
				onCancel();
			}
			else if (Input.MenuUp.Pressed && currentlySelectedOption > 0)
			{
				currentlySelectedOption = 0;
				wiggler.Start();
				Audio.Play("event:/ui/main/rollover_up");
			}
			else if (Input.MenuDown.Pressed && currentlySelectedOption < 1)
			{
				currentlySelectedOption = 1;
				wiggler.Start();
				Audio.Play("event:/ui/main/rollover_down");
			}
			else if (Input.MenuConfirm.Pressed)
			{
				if (currentlySelectedOption == 1)
				{
					opened = false;
					wiggler.Start();
					Audio.Play("event:/ui/main/button_back");
					onCancel();
				}
				else
				{
					RemoveSelf();
					Audio.Play("event:/ui/main/button_select");
					onConfirm();
				}
			}
		}
		else if (openingEase <= 0f)
		{
			RemoveSelf();
		}
	}

	public override void Render()
	{
		base.Render();
		float num = wiggler.Value * 8f;
		if (openingEase > 0f)
		{
			float num2 = Ease.CubeOut(openingEase);
			Vector2 vector = new Vector2(960f, 540f);
			float lineHeight = ActiveFont.LineHeight;
			Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * num2 * 0.9f);
			ActiveFont.Draw(Dialog.Clean("collabutils2_assist_skip_confirm"), vector + new Vector2(0f, -16f - 64f * (1f - num2)), new Vector2(0.5f, 1f), Vector2.One, Color.White * num2);
			ActiveFont.DrawOutline(Dialog.Clean("collabutils2_assist_skip_confirm_yes"), vector + new Vector2(((opened && currentlySelectedOption == 0) ? num : 0f) * 1.2f * num2, 16f + 64f * (1f - num2)), new Vector2(0.5f, 0f), Vector2.One * 0.8f, opened ? selectionColor(currentlySelectedOption == 0) : Color.Gray, 2f, Color.Black * num2);
			ActiveFont.DrawOutline(Dialog.Clean("collabutils2_assist_skip_confirm_no"), vector + new Vector2(((opened && currentlySelectedOption == 1) ? num : 0f) * 1.2f * num2, 16f + lineHeight + 64f * (1f - num2)), new Vector2(0.5f, 0f), Vector2.One * 0.8f, opened ? selectionColor(currentlySelectedOption == 1) : Color.Gray, 2f, Color.Black * num2);
		}
	}

	private Color selectionColor(bool selected)
	{
		if (selected)
		{
			if (!Settings.Instance.DisableFlashes && !base.Scene.BetweenInterval(0.1f))
			{
				return TextMenu.HighlightColorB;
			}
			return TextMenu.HighlightColorA;
		}
		return Color.White;
	}
}
