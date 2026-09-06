#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestVivSpinner : Entity
{
	public enum Types
	{
		White,
		RainbowClassic,
		CustomRainbow
	}

	private class Border : Entity
	{
		public Entity parent;

		public Entity filler;

		public Color color = Color.Black;

		public Border(Entity parent, Entity filler, Color color)
		{
			this.parent = parent;
			this.filler = filler;
			base.Depth = parent.Depth + 2;
			this.color = color;
		}

		public override void Render()
		{
			if (parent.Visible)
			{
				if ((parent as AppleEverestVivSpinner).customBorder)
				{
					DrawBorder_Custom(parent);
					DrawBorder_Custom(filler);
				}
				else
				{
					DrawBorder(parent);
					DrawBorder(filler);
				}
			}
		}

		private void DrawBorder(Entity entity)
		{
			if (entity == null)
			{
				return;
			}
			foreach (Component component in entity.Components)
			{
				if (component is Image { Color: var color, Position: var position } image)
				{
					image.Color = this.color;
					image.Position = position - Vector2.UnitY;
					image.Render();
					image.Position = position + Vector2.UnitY;
					image.Render();
					image.Position = position - Vector2.UnitX;
					image.Render();
					image.Position = position + Vector2.UnitX;
					image.Render();
					image.Color = color;
					image.Position = position;
				}
			}
		}

		private void DrawBorder_Custom(Entity entity)
		{
			if (entity == null)
			{
				return;
			}
			foreach (Component component in entity.Components)
			{
				if (component is Image { Color: var color, Position: var position } image)
				{
					image.Color = this.color;
					image.Position = position - Vector2.UnitY;
					image.Render();
					image.Position = position + Vector2.UnitY;
					image.Render();
					image.Position = position - Vector2.UnitX;
					image.Render();
					image.Position = position + Vector2.UnitX;
					image.Render();
					image.Color = color;
					image.Position = position;
				}
			}
		}
	}

	public bool AttachToSolid;

	private Entity filler;

	private Border border;

	private float offset;

	private bool expanded;

	private int randomSeed;

	private bool customBorder = false;

	private Types type;

	private string[] hitboxString;

	private Color color;

	public Color borderColor;

	public float scale;

	public float imageScale;

	public bool debrisToScale;

	public bool customDebris;

	private int ID;

	private string shatterFlag;

	private DashCollision OnDashCollide;

	private bool shatterDash;

	private Color shatterColor;

	private string directory;

	private string bgdirectory;

	private string fgdirectory;

	private string subdirectory;

	private bool isSeeded;

	private int seed = -1;

	private string flagToggle;

	private bool flagToggleInvert;

	private bool createConnectors;



	public AppleEverestVivSpinner(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		ID = data.ID;
		type = data.Enum("Type", Types.White);
		AttachToSolid = data.Bool("AttachToSolid");
		if (data.Has("ref"))
		{
			string text = data.Attr("ref", "VivHelper/customSpinner/white/fg_white00");
			int num = text.LastIndexOf('/');
			directory = text.Substring(0, num);
			subdirectory = text.Substring(num + 3);
		}
		else
		{
			directory = data.Attr("Directory").TrimStart(new char[1] { ' ' }).TrimEnd('/', ' ');
			if (directory == "")
			{
				directory = "VivHelper/customSpinner/white";
			}
			subdirectory = data.Attr("Subdirectory");
			if (data.Bool("FrostHelper") || (data.Has("CurrentVersion") && subdirectory == ""))
			{
				subdirectory = "";
			}
			else
			{
				if (subdirectory == "")
				{
					subdirectory = "white";
				}
				subdirectory = "_" + subdirectory;
			}
		}
		string text2 = data.Attr("HitboxType", data.Bool("removeRectHitbox") ? "C:6" : "C:6|R:16,4;-8,*1@-4");
		if (text2 == "")
		{
			text2 = "C:6|R:16,4;-8,*1@-4";
		}
		hitboxString = text2.Split(new char[1] { '|' });
		bgdirectory = directory + "/bg";
		fgdirectory = directory + "/fg";
		shatterDash = data.Bool("shatterOnDash");
		string text3 = data.Attr("Color");
		if (text3 == "")
		{
			text3 = "ffffff";
		}
		color = (text3 == "White" ? Color.White : Calc.HexToColor(text3));
		string text4 = data.Attr("ShatterColor");
		if (text4 == "")
		{
			text4 = text3;
		}
		shatterColor = (text4 == "White" ? Color.White : Calc.HexToColor(text4));
		text3 = data.Attr("BorderColor");
		if (text3 == "")
		{
			text3 = "000000";
		}
		borderColor = (text3 == "White" ? Color.White : Calc.HexToColor(text3));
		this.offset = Calc.Random.NextFloat();
		base.Tag = Tags.TransitionUpdate;
		scale = data.Float("Scale", 1f);
		scale = ((scale == -1f) ? 1f : Math.Max(1f / 3f, scale));
		imageScale = data.Float("ImageScale", 1f);
		imageScale = ((imageScale == -1f) ? 1f : Math.Max(1f / 3f, imageScale));
		debrisToScale = data.Bool("DebrisToScale", defaultValue: true);
		customDebris = data.Bool("CustomDebris");
		base.Collider = new ColliderList(new Circle(6f), new Hitbox(16f, 4f, -8f, -3f));
		Visible = false;
		Add(new PlayerCollider(OnPlayer));
		Add(new HoldableCollider(OnHoldable));
		Add(new LedgeBlocker());
		base.Depth = data.Int("Depth", -8500);
		if (AttachToSolid)
		{
			Add(new StaticMover
			{
				OnShake = OnShake,
				SolidChecker = IsRiding,
				OnDestroy = base.RemoveSelf
			});
		}
		shatterFlag = data.Attr("ShatterFlag");
		randomSeed = Calc.Random.Next();
		seed = data.Int("seed", -1);
		isSeeded = data.Bool("isSeeded");
		flagToggle = data.Attr("flagToggle");
		if (flagToggle.StartsWith("!"))
		{
			flagToggleInvert = true;
			flagToggle = flagToggle.Substring(1);
		}
		createConnectors = !data.Bool("ignoreConnection");
	}

	public override void Awake(Scene scene)
	{
		base.Awake(scene);
		if (InView())
		{
			CreateSprites();
		}
	}

	private void ForceInstantiate()
	{
		CreateSprites();
		Visible = true;
	}

	private bool InView()
	{
		Camera camera = (base.Scene as Level).Camera;
		if (base.Right > camera.X - 16f && base.Bottom > camera.Y - 16f && base.Left < camera.X + 320f * camera.Zoom + 16f)
		{
			return base.Top < camera.Y + 180f * camera.Zoom + 16f;
		}
		return false;
	}



	public override void Update()
	{
		if (!string.IsNullOrWhiteSpace(flagToggle) && SceneAs<Level>().Session.GetFlag(flagToggle) == flagToggleInvert)
		{
			Visible = (Collidable = false);
		}
		else if (!Visible)
		{
			Collidable = false;
			if (InView())
			{
				Visible = true;
				if (!expanded)
				{
					CreateSprites();
				}

			}
		}
		else
		{
			base.Update();

			if (base.Scene.OnInterval(0.25f, offset) && !InView())
			{
				Visible = false;
			}
			if (base.Collider != null && base.Scene.OnInterval(0.05f, offset))
			{
				Player entity = base.Scene.Tracker.GetEntity<Player>();
				if (entity != null)
				{
					Collidable = Math.Abs(entity.X - base.X) < 128f && Math.Abs(entity.Y - base.Y) < 128f;
				}
			}
			if (Visible && shatterFlag != "" && (base.Scene as Level).Session.GetFlag(shatterFlag))
			{
				Destroy();
			}
		}
		if (filler != null)
		{
			filler.Position = Position;
		}
	}

	private void OnPlayer(Player player)
	{
		player.Die((player.Position - Position).SafeNormalize());
	}

	private void OnHoldable(Holdable h)
	{
		h.HitSpinner(this);
	}

	private void CreateSprites()
	{
		if (expanded)
		{
			return;
		}
		Calc.PushRandom(randomSeed);
		List<MTexture> atlasSubtextures = GFX.Game.GetAtlasSubtextures(fgdirectory + subdirectory);
		MTexture mTexture = Calc.Random.Choose(atlasSubtextures);

		if (createConnectors)
		{
			foreach (AppleEverestVivSpinner entity in base.Scene.Tracker.GetEntities<AppleEverestVivSpinner>())
			{
				if (entity.createConnectors && entity.ID > ID && entity.AttachToSolid == AttachToSolid && (entity.Position - Position).LengthSquared() < (float)Math.Pow(12f * (scale + entity.scale), 2.0))
				{
					float num = Calc.Angle(entity.Position, Position);
					AddSprite(Vector2.Lerp(Position + Vector2.UnitX.RotateTowards(0f - num, 6.3f), entity.Position + Vector2.UnitX.RotateTowards(num, 6.3f), 0.5f) - Position, (entity.scale + scale) / 2f, Color.Lerp(entity.color, color, 0.5f), isSeeded);
				}
			}
		}
		int num2 = 0;
		if (!SolidCheck(new Vector2(base.X - 4f * scale, base.Y - 4f * scale)))
		{
			num2++;
		}
		if (!SolidCheck(new Vector2(base.X + 4f * scale, base.Y - 4f * scale)))
		{
			num2 += 2;
		}
		if (!SolidCheck(new Vector2(base.X - 4f * scale, base.Y + 4f * scale)))
		{
			num2 += 4;
		}
		if (!SolidCheck(new Vector2(base.X + 4f * scale, base.Y + 4f * scale)))
		{
			num2 += 8;
		}
		expanded = true;
		Calc.PopRandom();
		if (num2 == 15)
		{
			Image image = new Image(mTexture).CenterOrigin().SetColor(color);
			image.Scale = Vector2.One * scale / imageScale;
			Add(image);
		}
		else
		{
			if ((num2 & 1) > 0)
			{
				Image image = new Image(mTexture.GetSubtexture(0, 0, (int)(14f * imageScale), (int)(14f * imageScale))).SetOrigin(12f * imageScale, 12f * imageScale).SetColor(color);
				image.Scale = Vector2.One * scale / imageScale;
				Add(image);
			}
			if ((num2 & 2) > 0)
			{
				Image image = new Image(mTexture.GetSubtexture((int)(10f * imageScale), 0, (int)(14f * imageScale), (int)(14f * imageScale))).SetOrigin(2f * imageScale, 12f * imageScale).SetColor(color);
				image.Scale = Vector2.One * scale / imageScale;
				Add(image);
			}
			if ((num2 & 8) > 0)
			{
				Image image = new Image(mTexture.GetSubtexture((int)(10f * imageScale), (int)(10f * imageScale), (int)(14f * imageScale), (int)(14f * imageScale))).SetOrigin(2f * imageScale, 2f * imageScale).SetColor(color);
				image.Scale = Vector2.One * scale / imageScale;
				Add(image);
			}
			if ((num2 & 4) > 0)
			{
				Image image = new Image(mTexture.GetSubtexture(0, (int)(10f * imageScale), (int)(14f * imageScale), (int)(14f * imageScale))).SetOrigin(12f * imageScale, 2f * imageScale).SetColor(color);
				image.Scale = Vector2.One * scale / imageScale;
				Add(image);
			}
		}
		if (borderColor != Color.Transparent)
		{
			base.Scene.Add(border = new Border(this, filler, borderColor));
		}
	}

	private void AddSprite(Vector2 offset, float scale, Color c, bool seeded)
	{
		if (filler == null)
		{
			Scene.Add(filler = new Entity(Position));
			filler.Depth = Depth + 1;
		}
		List<MTexture> textures = GFX.Game.GetAtlasSubtextures(bgdirectory + subdirectory);
		Image image = new Image(Calc.Random.Choose(textures));
		image.Rotation = Calc.Random.Choose(0, 1, 2, 3) * (MathF.PI / 2f);
		image.Position = offset;
		image.CenterOrigin();
		image.Scale = Vector2.One * scale / imageScale;
		image.Color = c;
		filler.Add(image);
	}

	private void OnShake(Vector2 pos)
	{
		foreach (Component component in base.Components)
		{
			if (component is Image)
			{
				(component as Image).Position = pos;
			}
		}
	}

	private bool IsRiding(Solid solid)
	{
		return CollideCheck(solid);
	}

	private bool SolidCheck(Vector2 position)
	{
		if (AttachToSolid)
		{
			return false;
		}
		foreach (Solid item in base.Scene.CollideAll<Solid>(position))
		{
			if (item is SolidTiles)
			{
				return true;
			}
		}
		return false;
	}

	private void ClearSprites()
	{
		if (filler != null)
		{
			filler.RemoveSelf();
		}
		filler = null;
		if (border != null)
		{
			border.RemoveSelf();
		}
		border = null;
		foreach (Image item in base.Components.GetAll<Image>())
		{
			item.RemoveSelf();
		}
		expanded = false;
	}

	public void Destroy(bool boss = false)
	{
		if (InView())
		{
			Audio.Play("event:/game/06_reflection/fall_spike_smash", Position);
			Color color = shatterColor;
			AppleEverestVivCrystalDebris.Burst(Position, color, boss, (int)(8f * scale), customDebris ? (directory + "/debris") : "particles/shard", debrisToScale ? scale : 1f);
		}
		border?.RemoveSelf();
		filler?.RemoveSelf();
		RemoveSelf();
	}


    internal static void BeforePlayerStateReturn(Player player)
    {
        Rectangle rectangle = new((int)(player.X - 4f), (int)(player.Y - 40f), 8, 12);
        AppleEverestVivSpinner spinner = player.Scene.CollideFirst<AppleEverestVivSpinner>(rectangle);
        if (spinner == null) return;
        spinner.Destroy();
        ((Level)player.Scene).Shake();
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
        global::Celeste.Celeste.Freeze(0.01f);
    }
}
