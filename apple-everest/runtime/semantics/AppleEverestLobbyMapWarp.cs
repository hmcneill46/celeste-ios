#nullable disable
using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

internal class AppleEverestLobbyMapWarp : Entity
{
	private readonly string warpSpritePath;

	private readonly bool warpSpriteFlipX;

	private readonly bool playActivateSprite;

	private readonly bool activateSpriteFlipX;

	private readonly Facings playerFacing;

	private AppleEverestLobbyMapController.MarkerInfo info;

	private Image image;

	public AppleEverestLobbyMapWarp(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		warpSpritePath = data.Attr("warpSpritePath", "decals/1-forsakencity/bench_concrete");
		warpSpriteFlipX = data.Bool("warpSpriteFlipX");
		playActivateSprite = data.Bool("playActivateSprite", defaultValue: true);
		activateSpriteFlipX = data.Bool("activateSpriteFlipX");
		playerFacing = data.Enum("playerFacing", Facings.Right);
		base.Depth = data.Int("depth", 2000);
		AppleEverestLobbyMapController.MarkerInfo.TryParse(data, null, out info);
		if (!string.IsNullOrWhiteSpace(warpSpritePath))
		{
			Add(image = new Image(GFX.Game[warpSpritePath]));
			image.Effects = (warpSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			image.JustifyOrigin(0.5f, 1f);
		}
		Rectangle bounds = new Rectangle(-16, -32, 32, 32);
		Collidable = true;
		base.Collider = new Hitbox(bounds.Width, bounds.Height, bounds.X, bounds.Y);
		Add(new TalkComponent(bounds, new Vector2(0f, data.Float("interactOffsetY", -16f)), OnTalk)
		{
			PlayerMustBeFacing = false
		});
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		Level level = scene as Level;
		info.SID = level.Session.Area.SID;
		info.Room = level.Session.Level;
	}

	public void OnTalk(Player player)
	{
		if (player.Scene is Level { CanRetry: not false } level)
		{
			AppleEverestLobbyMapUI.SetLocked(locked: true);
			level.Tracker.GetEntity<AppleEverestLobbyMapController>()?.VisitManager?.ActivateWarp(info.MarkerId);
			if (playActivateSprite)
			{
				Add(new Coroutine(activateRoutine(player)));
			}
			else
			{
				level.Add(new AppleEverestLobbyMapUI());
			}
		}
	}

	private IEnumerator activateRoutine(Player player)
	{
		if (player == null)
		{
			yield break;
		}
		AppleEverestLobbyMapUI.SetLocked(locked: true, base.Scene, player);
		yield return player.DummyWalkToExact((int)base.X, walkBackwards: false, 1f, cancelOnFall: true);
		if (!validPlayer(player))
		{
			if (!player.Dead)
			{
				AppleEverestLobbyMapUI.SetLocked(locked: false, base.Scene, player);
			}
			yield break;
		}
		player.Facing = playerFacing;
		PlayerSprite sprite = player.Sprite;
		PlayerHair hair = player.Hair;
		bool visible = false;
		hair.Visible = false;
		sprite.Visible = visible;
		Sprite playerSprite = AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_sitBench");
		playerSprite.Effects = (activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		Add(playerSprite);
		playerSprite.Play("sit");
		Sprite playerHairSprite = AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_sitBench");
		playerHairSprite.Effects = (activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		playerHairSprite.Color = player.Hair.Color;
		Add(playerHairSprite);
		playerHairSprite.Play("sitHair");
		Audio.Play("event:/char/madeline/backpack_drop");
		while (playerSprite.Animating && validPlayer(player))
		{
			AppleEverestLobbyMapUI.SetLocked(locked: true, base.Scene, player);
			yield return null;
		}
		if (validPlayer(player))
		{
			player.Scene.Add(new AppleEverestLobbyMapUI());
		}
		else
		{
			AppleEverestLobbyMapUI.SetLocked(locked: false, base.Scene, player);
		}
		yield return 0.5f;
		playerSprite.RemoveSelf();
		playerHairSprite.RemoveSelf();
		PlayerSprite sprite2 = player.Sprite;
		PlayerHair hair2 = player.Hair;
		visible = true;
		hair2.Visible = true;
		sprite2.Visible = visible;
	}

	private bool validPlayer(Player player)
	{
		if ((base.Center - player.Center).LengthSquared() < 256f && !player.Dead)
		{
			return player.OnGround();
		}
		return false;
	}
}
