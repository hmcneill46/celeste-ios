#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestXaphanSlope : Solid
{
	public class LightOccludeBlock : Entity
	{
		public LightOccludeBlock(Vector2 position, float width, float height)
			: base(position)
		{
			base.Collider = new Hitbox(width, height);
			Add(new LightOcclude());
			base.Depth = -10003;
		}

		public override void DebugRender(Camera camera)
		{
		}
	}

	public string Side;

	public string TilesTop;

	public string TilesBottom;

	public string Directory;

	public string FlagDirectory;

	public string Texture;

	public string FlagTexture;

	public bool Gentle;

	public bool CanSlide;

	public bool ForceSlide;

	public bool UpsideDown;

	public bool NoRender;

	public bool StickyDash;

	public int SoundIndex;

	public int SlopeHeight;

	public int[] variation = new int[35];

	public int[] variationInner = new int[35];

	private MTexture[,] BaseTextures;

	private MTexture[] SlopeTextures;

	public bool Rainbow;

	public bool CanJumpThrough;

	public bool VisualOnly;

	private bool AffectPlayerSpeed;

	public string Flag;

	public ColliderList colliderList;

	private List<LightOccludeBlock> lightOccludeBlocks = new List<LightOccludeBlock>();

	private string RenderMethod;

	private bool PreventRefillOnSliding;

	public Vector2 SlopeTop;

	public Vector2 SlopeBottom;

	public AppleEverestXaphanSlope(Vector2 position, Vector2 offset, bool gentle, string side, int soundIndex, int slopeHeight, string tilesTop, string tilesBottom, string texture, string flagTexture, bool canSlide, bool forceSlide, string directory, string flagDirectory, bool upsideDown, bool noRender, bool stickyDash, bool rainbow, bool canJumpThrough, string flag, bool affectPlayerSpeed, string renderMethod = "Type A", bool visualOnly = false, bool preventRefillOnSliding = false)
		: base(position + offset, 0f, 0f, safe: true)
	{
		base.Tag = Tags.TransitionUpdate;
		AllowStaticMovers = false;
		Collidable = false;
		Gentle = gentle;
		Side = side;
		SoundIndex = soundIndex;
		SlopeHeight = slopeHeight;
		TilesTop = tilesTop;
		TilesBottom = tilesBottom;
		Texture = texture;
		FlagTexture = flagTexture;
		CanSlide = canSlide;
		ForceSlide = forceSlide;
		Directory = directory;
		FlagDirectory = flagDirectory;
		UpsideDown = upsideDown;
		NoRender = noRender;
		StickyDash = stickyDash;
		Rainbow = rainbow;
		CanJumpThrough = canJumpThrough;
		VisualOnly = visualOnly;
		Flag = flag;
		AffectPlayerSpeed = affectPlayerSpeed;
		RenderMethod = renderMethod;
		PreventRefillOnSliding = preventRefillOnSliding;
		if (!VisualOnly)
		{
			if (!upsideDown)
			{
				if (side == "Left")
				{
					colliderList = new ColliderList(new Hitbox(4f, 1f));
					if (!CanJumpThrough)
					{
						lightOccludeBlocks.Add(new LightOccludeBlock(Position, 8f, 2f));
					}
					for (int i = 0; i <= SlopeHeight - 1; i++)
					{
						if (i > 0)
						{
							colliderList.Add(new Hitbox(4f, 1f, Gentle ? (i * 16) : (i * 8), i * 8));
						}
						colliderList.Add(new Hitbox(Gentle ? 6 : 5, 1f, Gentle ? (i * 16) : (i * 8), 1 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 8 : 6, 1f, Gentle ? (i * 16) : (i * 8), 2 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 10 : 7, 1f, Gentle ? (i * 16) : (i * 8), 3 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 12 : 8, 1f, Gentle ? (i * 16) : (i * 8), 4 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 14 : 9, 1f, Gentle ? (i * 16) : (i * 8), 5 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 16 : 10, 1f, Gentle ? (i * 16) : (i * 8), 6 + i * 8));
						colliderList.Add(new Hitbox(Gentle ? 18 : 11, 1f, Gentle ? (i * 16) : (i * 8), 7 + i * 8));
						if (!CanJumpThrough)
						{
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (i * 16) : (i * 8), 1 + i * 8), 4 + (Gentle ? 6 : 5), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (i * 16) : (i * 8), 3 + i * 8), 4 + (Gentle ? 10 : 7), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (i * 16) : (i * 8), 5 + i * 8), 4 + (Gentle ? 14 : 9), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (i * 16) : (i * 8), 7 + i * 8), 4 + (Gentle ? 18 : 11), 2f));
						}
					}
					base.Collider = colliderList;
				}
				else
				{
					colliderList = new ColliderList(new Hitbox(4f, 1f, 20f));
					if (!CanJumpThrough)
					{
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(16f, 0f), 8f, 2f));
					}
					for (int j = 0; j <= SlopeHeight - 1; j++)
					{
						if (j > 0)
						{
							colliderList.Add(new Hitbox(4f, 1f, 20 + (Gentle ? (j * -16) : (j * -8)), j * 8));
						}
						colliderList.Add(new Hitbox(Gentle ? 6 : 5, 1f, Gentle ? (18f - (float)(j * 16)) : (19f - (float)(j * 8)), 1 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 8 : 6, 1f, Gentle ? (16f - (float)(j * 16)) : (18f - (float)(j * 8)), 2 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 10 : 7, 1f, Gentle ? (14f - (float)(j * 16)) : (17f - (float)(j * 8)), 3 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 12 : 8, 1f, Gentle ? (12f - (float)(j * 16)) : (16f - (float)(j * 8)), 4 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 14 : 9, 1f, Gentle ? (10f - (float)(j * 16)) : (15f - (float)(j * 8)), 5 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 16 : 10, 1f, Gentle ? (8f - (float)(j * 16)) : (14f - (float)(j * 8)), 6 + j * 8));
						colliderList.Add(new Hitbox(Gentle ? 18 : 11, 1f, Gentle ? (6f - (float)(j * 16)) : (13f - (float)(j * 8)), 7 + j * 8));
						if (!CanJumpThrough)
						{
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (14f - (float)(j * 16)) : (15f - (float)(j * 8)), 1 + j * 8), 4 + (Gentle ? 6 : 5), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (10f - (float)(j * 16)) : (13f - (float)(j * 8)), 3 + j * 8), 4 + (Gentle ? 10 : 7), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (6f - (float)(j * 16)) : (11f - (float)(j * 8)), 5 + j * 8), 4 + (Gentle ? 14 : 9), 2f));
							lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (2f - (float)(j * 16)) : (9f - (float)(j * 8)), 7 + j * 8), 4 + (Gentle ? 18 : 11), 2f));
						}
					}
					base.Collider = colliderList;
				}
			}
			else if (side == "Left")
			{
				colliderList = new ColliderList(new Hitbox(8f, 1f, 0f, 15f));
				if (!CanJumpThrough)
				{
					lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(0f, 14f), 8f, 2f));
				}
				for (int k = 0; k <= SlopeHeight - 1; k++)
				{
					if (k > 0)
					{
						colliderList.Add(new Hitbox(8f, 1f, Gentle ? (k * 16) : (k * 8), 15 + k * -8));
					}
					colliderList.Add(new Hitbox(Gentle ? 10 : 9, 1f, Gentle ? (k * 16) : (k * 8), 14 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 12 : 10, 1f, Gentle ? (k * 16) : (k * 8), 13 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 14 : 11, 1f, Gentle ? (k * 16) : (k * 8), 12 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 16 : 12, 1f, Gentle ? (k * 16) : (k * 8), 11 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 18 : 13, 1f, Gentle ? (k * 16) : (k * 8), 10 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 20 : 14, 1f, Gentle ? (k * 16) : (k * 8), 9 - k * 8));
					colliderList.Add(new Hitbox(Gentle ? 22 : 15, 1f, Gentle ? (k * 16) : (k * 8), 8 - k * 8));
					if (!CanJumpThrough)
					{
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (k * 16) : (k * 8), 13 - k * 8), Gentle ? 10 : 9, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (k * 16) : (k * 8), 11 - k * 8), Gentle ? 14 : 11, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (k * 16) : (k * 8), 9 - k * 8), Gentle ? 18 : 13, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (k * 16) : (k * 8), 7 - k * 8), Gentle ? 22 : 15, 2f));
					}
				}
				base.Collider = colliderList;
			}
			else
			{
				colliderList = new ColliderList(new Hitbox(8f, 1f, 16f, 15f));
				if (!CanJumpThrough)
				{
					lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(16f, 14f), 8f, 2f));
				}
				for (int l = 0; l <= SlopeHeight - 1; l++)
				{
					if (l > 0)
					{
						colliderList.Add(new Hitbox(8f, 1f, 16 + (Gentle ? (l * -16) : (l * -8)), 15 + l * -8));
					}
					colliderList.Add(new Hitbox(Gentle ? 10 : 9, 1f, Gentle ? (14f - (float)(l * 16)) : (15f - (float)(l * 8)), 14 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 12 : 10, 1f, Gentle ? (12f - (float)(l * 16)) : (14f - (float)(l * 8)), 13 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 14 : 11, 1f, Gentle ? (10f - (float)(l * 16)) : (13f - (float)(l * 8)), 12 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 16 : 12, 1f, Gentle ? (8f - (float)(l * 16)) : (12f - (float)(l * 8)), 11 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 18 : 13, 1f, Gentle ? (6f - (float)(l * 16)) : (11f - (float)(l * 8)), 10 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 20 : 14, 1f, Gentle ? (4f - (float)(l * 16)) : (10f - (float)(l * 8)), 9 - l * 8));
					colliderList.Add(new Hitbox(Gentle ? 22 : 15, 1f, Gentle ? (2f - (float)(l * 16)) : (9f - (float)(l * 8)), 8 - l * 8));
					if (!CanJumpThrough)
					{
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (14f - (float)(l * 16)) : (15f - (float)(l * 8)), 13 - l * 8), Gentle ? 10 : 9, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (10f - (float)(l * 16)) : (13f - (float)(l * 8)), 11 - l * 8), Gentle ? 14 : 11, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (6f - (float)(l * 16)) : (11f - (float)(l * 8)), 9 - l * 8), Gentle ? 18 : 13, 2f));
						lightOccludeBlocks.Add(new LightOccludeBlock(Position + new Vector2(Gentle ? (2f - (float)(l * 16)) : (9f - (float)(l * 8)), 7 - l * 8), Gentle ? 22 : 15, 2f));
					}
				}
				base.Collider = colliderList;
			}
		}
		if (SlopeHeight < 1)
		{
			SlopeHeight = 1;
		}
		else if (SlopeHeight > 30)
		{
			SlopeHeight = 30;
		}
		if (string.IsNullOrEmpty(Directory))
		{
			Directory = "objects/XaphanHelper/Slope";
		}
		if (string.IsNullOrEmpty(FlagDirectory))
		{
			FlagDirectory = "objects/XaphanHelper/Slope";
		}
		base.Depth = -10003;
	}

	public AppleEverestXaphanSlope(EntityData data, Vector2 offset)
		: this(data.Position, offset, data.Bool("gentle"), data.Attr("side"), data.Int("soundIndex"), data.Int("slopeHeight", 1), data.Attr("tilesTop"), data.Attr("tilesBottom"), data.Attr("texture", "cement"), data.Attr("flagTexture"), data.Bool("canSlide"), data.Bool("forceSlide"), data.Attr("customDirectory"), data.Attr("flagCustomDirectory"), data.Bool("upsideDown"), data.Bool("noRender"), data.Bool("stickyDash"), data.Bool("rainbow"), data.Bool("canJumpThrough"), data.Attr("flag"), data.Bool("affectPlayerSpeed"), data.Attr("renderMethod", "Type A"), data.Bool("visualOnly"), data.Bool("preventRefillOnSliding"))
	{
	}

	public static void SetCollisionBeforeUpdate(Actor actor)
	{
		List<Entity> list = actor.Scene.Tracker.GetEntities<AppleEverestXaphanPlayerPlatform>().ToList();
		List<Entity> list2 = actor.Scene.Tracker.GetEntities<AppleEverestXaphanSlope>().ToList();
		foreach (AppleEverestXaphanSlope item in list2)
		{
			if (item.CollideCheck(actor))
			{
				item.Collidable = true;
			}
			else if (item.CanJumpThrough)
			{
				if (!item.UpsideDown)
				{
					if (item.Side == "Right")
					{
						if ((item.SlopeBottom.X - item.SlopeTop.X) * (actor.BottomCenter.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (actor.BottomCenter.X - item.SlopeTop.X) >= 0f)
						{
							item.Collidable = true;
						}
					}
					else if (item.Side == "Left" && (item.SlopeBottom.X - item.SlopeTop.X) * (actor.BottomCenter.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (actor.BottomCenter.X - item.SlopeTop.X) <= 0f)
					{
						item.Collidable = true;
					}
				}
				else if (item.Side == "Right")
				{
					if ((item.SlopeBottom.X - item.SlopeTop.X) * (actor.TopRight.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (actor.TopRight.X - item.SlopeTop.X) >= 0f)
					{
						item.Collidable = true;
					}
				}
				else if (item.Side == "Left" && (item.SlopeBottom.X - item.SlopeTop.X) * (actor.TopLeft.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (actor.TopLeft.X - item.SlopeTop.X) <= 0f)
				{
					item.Collidable = true;
				}
			}
			else if (!item.UpsideDown || (item.UpsideDown && ((item.Side == "Right" && actor.Right < item.Right) || (item.Side == "Left" && actor.Left > item.Left))))
			{
				item.Collidable = true;
			}
		}
		foreach (AppleEverestXaphanPlayerPlatform item2 in list)
		{
			item2.Collidable = false;
		}
	}

	public static void SetCollisionBeforeUpdate(Solid solid)
	{
		List<Entity> list = solid.Scene.Tracker.GetEntities<AppleEverestXaphanPlayerPlatform>().ToList();
		List<Entity> list2 = solid.Scene.Tracker.GetEntities<AppleEverestXaphanSlope>().ToList();
		foreach (AppleEverestXaphanSlope item in list2)
		{
			if (item.CollideCheck(solid))
			{
				item.Collidable = true;
			}
			else if (item.CanJumpThrough)
			{
				if (!item.UpsideDown)
				{
					if (item.Side == "Right")
					{
						if ((item.SlopeBottom.X - item.SlopeTop.X) * (solid.BottomCenter.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (solid.BottomCenter.X - item.SlopeTop.X) >= 0f)
						{
							item.Collidable = true;
						}
					}
					else if (item.Side == "Left" && (item.SlopeBottom.X - item.SlopeTop.X) * (solid.BottomCenter.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (solid.BottomCenter.X - item.SlopeTop.X) <= 0f)
					{
						item.Collidable = true;
					}
				}
				else if (item.Side == "Right")
				{
					if ((item.SlopeBottom.X - item.SlopeTop.X) * (solid.TopRight.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (solid.TopRight.X - item.SlopeTop.X) >= 0f)
					{
						item.Collidable = true;
					}
				}
				else if (item.Side == "Left" && (item.SlopeBottom.X - item.SlopeTop.X) * (solid.TopLeft.Y - item.SlopeTop.Y) - (item.SlopeBottom.Y - item.SlopeTop.Y) * (solid.TopLeft.X - item.SlopeTop.X) <= 0f)
				{
					item.Collidable = true;
				}
			}
			else if (!item.UpsideDown || (item.UpsideDown && ((item.Side == "Right" && solid.Right < item.Right) || (item.Side == "Left" && solid.Left > item.Left))))
			{
				item.Collidable = true;
			}
		}
		foreach (AppleEverestXaphanPlayerPlatform item2 in list)
		{
			item2.Collidable = false;
		}
	}

	public static void SetCollisionAfterUpdate(Entity entity)
	{
		List<Entity> list = entity.Scene.Tracker.GetEntities<AppleEverestXaphanSlope>().ToList();
		Player entity2 = entity.Scene.Tracker.GetEntity<Player>();
		list.ForEach(delegate(Entity entity3)
		{
			entity3.Collidable = false;
		});
		foreach (AppleEverestXaphanPlayerPlatform entity3 in entity.Scene.Tracker.GetEntities<AppleEverestXaphanPlayerPlatform>())
		{
			entity3.RestoreCollisionForPlayer();
		}
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		foreach (LightOccludeBlock lightOccludeBlock in lightOccludeBlocks)
		{
			SceneAs<Level>().Add(lightOccludeBlock);
		}
		MTexture mTexture = GFX.Game[((!string.IsNullOrEmpty(Flag) && SceneAs<Level>().Session.GetFlag(Flag)) ? FlagDirectory : Directory) + "/" + ((!string.IsNullOrEmpty(Flag) && SceneAs<Level>().Session.GetFlag(Flag)) ? FlagTexture : Texture)];
		BaseTextures = new MTexture[6, 15];
		for (int i = 0; i < 6; i++)
		{
			for (int j = 0; j < 15; j++)
			{
				BaseTextures[i, j] = mTexture.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
			}
		}
		SlopeTextures = new MTexture[96];
		if (!UpsideDown)
		{
			for (int k = 0; k < 32; k++)
			{
				SlopeTextures[k] = mTexture.GetSubtexture(new Rectangle(k, 0, 1, 8));
			}
			for (int l = 32; l < 64; l++)
			{
				SlopeTextures[l] = mTexture.GetSubtexture(new Rectangle(l - 32, 88, 1, 8));
			}
			for (int m = 64; m < 96; m++)
			{
				SlopeTextures[m] = mTexture.GetSubtexture(new Rectangle(m - 64, 96, 1, 8));
			}
		}
		else
		{
			for (int n = 0; n < 32; n++)
			{
				SlopeTextures[n] = mTexture.GetSubtexture(new Rectangle(n, 8, 1, 8));
			}
			for (int num = 32; num < 64; num++)
			{
				SlopeTextures[num] = mTexture.GetSubtexture(new Rectangle(num - 32, 104, 1, 8));
			}
			for (int num2 = 64; num2 < 96; num2++)
			{
				SlopeTextures[num2] = mTexture.GetSubtexture(new Rectangle(num2 - 64, 112, 1, 8));
			}
		}
		Math.DivRem((int)Position.X / 8, 4, out var result);
		result = Math.Abs(result);
		variation[0] = result;
		for (int num3 = 1; num3 < 35; num3++)
		{
			result++;
			if (result >= 4)
			{
				result = 0;
			}
			variation[num3] = result;
		}
		Math.DivRem((int)Position.X / 8, 12, out result);
		result = Math.Abs(result);
		variationInner[0] = result;
		for (int num4 = 1; num4 < 35; num4++)
		{
			result++;
			if (result >= 12)
			{
				result = 0;
			}
			variationInner[num4] = result;
		}
		if (!VisualOnly)
		{
			SceneAs<Level>().Add(new AppleEverestXaphanPlayerPlatform(Position + new Vector2((Side == "Right") ? (((Gentle ? (-(SlopeHeight - 1) * 16) : (-(SlopeHeight - 1) * 8)) + 8) * ((!UpsideDown) ? 1 : (-1))) : 0, 8 * (SlopeHeight - 1) + 4) * ((!UpsideDown) ? 1 : (-1)), Gentle ? (8 + 16 * SlopeHeight) : (8 + 8 * SlopeHeight), Gentle, Side, SoundIndex, SlopeHeight, CanSlide, ForceSlide, base.Top, AffectPlayerSpeed, UpsideDown, StickyDash, CanJumpThrough, PreventRefillOnSliding));
			if (!UpsideDown)
			{
				SceneAs<Level>().Add(new AppleEverestXaphanFakePlayerPlatform(Position + new Vector2((Side == "Right") ? (((Gentle ? (-(SlopeHeight - 1) * 16) : (-(SlopeHeight - 1) * 8)) + 8) * ((!UpsideDown) ? 1 : (-1))) : 0, 8 * (SlopeHeight - 1) + 4) * ((!UpsideDown) ? 1 : (-1)), Gentle ? (8 + 16 * SlopeHeight) : (8 + 8 * SlopeHeight), Gentle, Side, SoundIndex, SlopeHeight, base.Top, UpsideDown, StickyDash, CanJumpThrough));
			}
		}
		if (!UpsideDown)
		{
			if (Side == "Left")
			{
				SlopeTop = Position + new Vector2(7f, 0f);
				SlopeBottom = Position + new Vector2(7 + (Gentle ? 16 : 8) * SlopeHeight, 8 * SlopeHeight);
			}
			else if (Side == "Right")
			{
				SlopeTop = Position + new Vector2(17f, 0f);
				SlopeBottom = Position + new Vector2(17 + (Gentle ? 16 : 8) * -SlopeHeight, 8 * SlopeHeight);
			}
		}
		else if (Side == "Left")
		{
			SlopeTop = Position + new Vector2(7 + (Gentle ? 16 : 8) * SlopeHeight, -16f);
			SlopeBottom = Position + new Vector2(7f, -16 + 8 * SlopeHeight);
		}
		else if (Side == "Right")
		{
			SlopeTop = Position + new Vector2(17 + (Gentle ? 16 : 8) * -SlopeHeight, -16f);
			SlopeBottom = Position + new Vector2(17f, -16 + 8 * SlopeHeight);
		}
	}


    // The selected profile has no DroneDebris producer. The package does not
    // call base.Update here; its only effect in this closure is disabling itself.
    public override void Update() { Collidable = false; }
    // All 14 exact authored profiles set noRender, after the base render call.
    public override void Render() { base.Render(); }
}
