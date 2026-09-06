// BrokemiaHelper exact cave wall: group fading, talk UI, death and displacement.
#nullable disable
using System.Collections.Generic;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCaveWall : Entity
{
	private char fillTile;

	private bool disableTransitionFading;

	private TileGrid tiles;

	private bool fadeOut;

	private bool fadeIn = true;

	private bool deadBodyCollided;

	private global::Celeste.EffectCutout cutout;

	private float transitionStartAlpha;

	private bool transitionFade;

	private AppleEverestCaveWall master;

	private bool awake;

	private bool blocksDisplacement;

	public List<AppleEverestCaveWall> Group;

	public Point GroupBoundsMin;

	public Point GroupBoundsMax;

	public float Alpha => tiles.Alpha;

	public bool FadeIn => fadeIn;

	public bool HasGroup { get; private set; }

	public bool MasterOfGroup { get; private set; }

	internal static void AfterDeadBodyAwake(global::Celeste.PlayerDeadBody self, Scene scene)
	{
		foreach (AppleEverestCaveWall entity in scene.Tracker.GetEntities<AppleEverestCaveWall>())
		{
			if (self.Position.X >= entity.Left && self.Position.X <= entity.Right && self.Position.Y >= entity.Top && self.Position.Y <= entity.Bottom)
			{
				(entity.MasterOfGroup ? entity : entity.master).deadBodyCollided = true;
			}
		}
	}

	internal static void AfterTalkUpdate(global::Celeste.TalkComponent.TalkComponentUI self)
	{
		if (self.Handler.Entity != null && self.Scene.CollideCheck<AppleEverestCaveWall>(self.Handler.Entity.Position))
		{
			AppleEverestCaveWall caveWall = self.Scene.CollideFirst<AppleEverestCaveWall>(self.Handler.Entity.Position);
			if (caveWall.Alpha >= 1f || caveWall.FadeIn)
			{
				float num = (self.Scene.CollideCheck<global::Celeste.FakeWall>(self.Handler.Entity.Position) ? 2f : 4f);
				self.AppleEverestCaveAlpha = Calc.Approach(self.AppleEverestCaveAlpha, 0f, num * Engine.DeltaTime);
			}
		}
	}

	internal static void AfterTalkAwake(global::Celeste.TalkComponent.TalkComponentUI self)
	{
		if (self.Handler.Entity == null || self.Scene.CollideCheck<AppleEverestCaveWall>(self.Handler.Entity.Position))
		{
			self.AppleEverestCaveAlpha = 0f;
		}
	}

	public AppleEverestCaveWall(Vector2 position, char tile, float width, float height, bool disableTransitionFading, bool blockDisplacement)
		: base(position)
	{
		blocksDisplacement = blockDisplacement;
		fillTile = tile;
		this.disableTransitionFading = disableTransitionFading;
		base.Collider = new Hitbox(width, height);
		base.Depth = -13001;
		Add(cutout = new global::Celeste.EffectCutout());
		Add(new global::Celeste.DisplacementRenderHook(RenderDisplacement));
	}

	public AppleEverestCaveWall(global::Celeste.EntityData data, Vector2 offset)
		: this(data.Position + offset, data.Char("tiletype", '3'), data.Width, data.Height, data.Bool("disableTransitionFading"), data.Bool("blockDisplacement", defaultValue: true))
	{
	}

	public void RenderDisplacement()
	{
		if (!MasterOfGroup)
		{
			return;
		}
		foreach (AppleEverestCaveWall item in Group)
		{
			if ((double)item.Alpha > 0.2 && item.blocksDisplacement)
			{
				Draw.Rect(item.X, item.Y, item.Width, item.Height, new Color(0.5f, 0.5f, 0f, 1f));
			}
		}
	}

	public override void Awake(Scene scene)
	{
		base.Awake(scene);
		awake = true;
		if (!HasGroup)
		{
			MasterOfGroup = true;
			Group = new List<AppleEverestCaveWall>();
			GroupBoundsMin = new Point((int)base.X, (int)base.Y);
			GroupBoundsMax = new Point((int)base.Right, (int)base.Bottom);
			AddToGroupAndFindChildren(this);
			Rectangle rectangle = new Rectangle(GroupBoundsMin.X / 8, GroupBoundsMin.Y / 8, (GroupBoundsMax.X - GroupBoundsMin.X) / 8 + 1, (GroupBoundsMax.Y - GroupBoundsMin.Y) / 8 + 1);
			global::Celeste.Level level = SceneAs<global::Celeste.Level>();
			Rectangle tileBounds = level.Session.MapData.TileBounds;
			VirtualMap<char> virtualMap = new VirtualMap<char>(level.SolidsData.Columns, level.SolidsData.Rows, '\0');
			VirtualMap<bool> virtualMap2 = new VirtualMap<bool>(level.SolidsData.Columns, level.SolidsData.Rows, emptyValue: false);
			foreach (AppleEverestCaveWall item in Group)
			{
				int num = (int)(item.X / 8f) - level.Session.MapData.TileBounds.X;
				int num2 = (int)(item.Y / 8f - (float)level.Session.MapData.TileBounds.Y);
				int num3 = (int)(item.Width / 8f);
				int num4 = (int)(item.Height / 8f);
				for (int i = num; i < num + num3; i++)
				{
					for (int j = num2; j < num2 + num4; j++)
					{
						if (!virtualMap2[i, j])
						{
							virtualMap[i, j] = level.SolidsData[i, j];
							virtualMap2[i, j] = true;
						}
						level.SolidsData[i, j] = item.fillTile;
					}
				}
			}
			foreach (AppleEverestCaveWall item2 in Group)
			{
				int x = (int)item2.X / 8 - tileBounds.Left;
				int y = (int)item2.Y / 8 - tileBounds.Top;
				int tilesX = (int)item2.Width / 8;
				int tilesY = (int)item2.Height / 8;
				item2.tiles = global::Celeste.GFX.FGAutotiler.GenerateOverlay(item2.fillTile, x, y, tilesX, tilesY, level.SolidsData).TileGrid;
				item2.Add(item2.tiles);
				item2.Add(new global::Celeste.TileInterceptor(item2.tiles, highPriority: false));
			}
			foreach (AppleEverestCaveWall item3 in Group)
			{
				int num5 = (int)(item3.X / 8f) - level.Session.MapData.TileBounds.X;
				int num6 = (int)(item3.Y / 8f - (float)level.Session.MapData.TileBounds.Y);
				int num7 = (int)(item3.Width / 8f);
				int num8 = (int)(item3.Height / 8f);
				for (int k = num5; k < num5 + num7; k++)
				{
					for (int l = num6; l < num6 + num8; l++)
					{
						if (virtualMap2[k, l])
						{
							level.SolidsData[k, l] = virtualMap[k, l];
						}
					}
				}
			}
		}
		TryToInitPosition();
		if (CollideCheck<global::Celeste.Player>())
		{
			foreach (AppleEverestCaveWall item4 in Group)
			{
				item4.tiles.Alpha = 0f;
				item4.fadeOut = true;
				item4.fadeIn = false;
				item4.cutout.Alpha = 0f;
			}
		}
		if (!disableTransitionFading)
		{
			global::Celeste.TransitionListener transitionListener = new global::Celeste.TransitionListener();
			transitionListener.OnOut = OnTransitionOut;
			transitionListener.OnOutBegin = OnTransitionOutBegin;
			transitionListener.OnIn = OnTransitionIn;
			transitionListener.OnInBegin = OnTransitionInBegin;
			Add(transitionListener);
		}
	}

	private void TryToInitPosition()
	{
		if (MasterOfGroup)
		{
			foreach (AppleEverestCaveWall item in Group)
			{
				if (!item.awake)
				{
					break;
				}
			}
			return;
		}
		master.TryToInitPosition();
	}

	private void AddToGroupAndFindChildren(AppleEverestCaveWall from)
	{
		if (from.X < (float)GroupBoundsMin.X)
		{
			GroupBoundsMin.X = (int)from.X;
		}
		if (from.Y < (float)GroupBoundsMin.Y)
		{
			GroupBoundsMin.Y = (int)from.Y;
		}
		if (from.Right > (float)GroupBoundsMax.X)
		{
			GroupBoundsMax.X = (int)from.Right;
		}
		if (from.Bottom > (float)GroupBoundsMax.Y)
		{
			GroupBoundsMax.Y = (int)from.Bottom;
		}
		from.HasGroup = true;
		Group.Add(from);
		if (from != this)
		{
			from.master = this;
			from.Group = Group;
		}
		foreach (AppleEverestCaveWall entity in base.Scene.Tracker.GetEntities<AppleEverestCaveWall>())
		{
			if (!entity.HasGroup && (base.Scene.CollideCheck(new Rectangle((int)from.X - 1, (int)from.Y, (int)from.Width + 2, (int)from.Height), entity) || base.Scene.CollideCheck(new Rectangle((int)from.X, (int)from.Y - 1, (int)from.Width, (int)from.Height + 2), entity)))
			{
				AddToGroupAndFindChildren(entity);
			}
		}
	}

	private void OnTransitionOutBegin()
	{
		if (Collide.CheckRect(this, SceneAs<global::Celeste.Level>().Bounds))
		{
			transitionFade = true;
			transitionStartAlpha = tiles.Alpha;
		}
		else
		{
			transitionFade = false;
		}
	}

	private void OnTransitionOut(float percent)
	{
		if (transitionFade)
		{
			tiles.Alpha = transitionStartAlpha * (1f - percent);
		}
	}

	private void OnTransitionInBegin()
	{
		global::Celeste.Level level = SceneAs<global::Celeste.Level>();
		if (!MasterOfGroup)
		{
			return;
		}
		global::Celeste.Player player = null;
		foreach (AppleEverestCaveWall item in Group)
		{
			if (level.PreviousBounds.HasValue && Collide.CheckRect(item, level.PreviousBounds.Value))
			{
				item.transitionFade = true;
				item.tiles.Alpha = 0f;
			}
			else
			{
				item.transitionFade = false;
			}
			player = item.CollideFirst<global::Celeste.Player>();
			if (player != null)
			{
				break;
			}
		}
		if (player == null)
		{
			return;
		}
		foreach (AppleEverestCaveWall item2 in Group)
		{
			item2.transitionFade = false;
			item2.tiles.Alpha = 0f;
		}
	}

	private void OnTransitionIn(float percent)
	{
		if (transitionFade)
		{
			tiles.Alpha = percent;
		}
	}

	public override void Update()
	{
		base.Update();
		cutout.Alpha = tiles.Alpha;
		if (fadeOut)
		{
			tiles.Alpha = Calc.Approach(tiles.Alpha, 0f, 3f * Engine.DeltaTime);
			if (tiles.Alpha <= 0f)
			{
				tiles.Alpha = 0f;
			}
		}
		else if (fadeIn)
		{
			tiles.Alpha = Calc.Approach(tiles.Alpha, 1f, 3f * Engine.DeltaTime);
			if (tiles.Alpha >= 1f)
			{
				tiles.Alpha = 1f;
			}
		}
		if (!MasterOfGroup)
		{
			return;
		}
		global::Celeste.Player player = null;
		foreach (AppleEverestCaveWall item in Group)
		{
			player = item.CollideFirst<global::Celeste.Player>();
			if (player != null)
			{
				break;
			}
		}
		if ((player != null && player.StateMachine.State != 9) || deadBodyCollided)
		{
			fadeOut = true;
			fadeIn = false;
			{
				foreach (AppleEverestCaveWall item2 in Group)
				{
					item2.fadeOut = true;
					item2.fadeIn = false;
				}
				return;
			}
		}
		if (!fadeOut)
		{
			return;
		}
		fadeOut = false;
		fadeIn = true;
		foreach (AppleEverestCaveWall item3 in Group)
		{
			item3.fadeOut = false;
			item3.fadeIn = true;
		}
	}

	public override void Render()
	{
		global::Celeste.Level level = base.Scene as global::Celeste.Level;
		if (level.ShakeVector.X < 0f && level.Camera.X <= (float)level.Bounds.Left && base.X <= (float)level.Bounds.Left)
		{
			tiles.RenderAt(Position + new Vector2(-3f, 0f));
		}
		if (level.ShakeVector.X > 0f && level.Camera.X + 320f >= (float)level.Bounds.Right && base.X + base.Width >= (float)level.Bounds.Right)
		{
			tiles.RenderAt(Position + new Vector2(3f, 0f));
		}
		if (level.ShakeVector.Y < 0f && level.Camera.Y <= (float)level.Bounds.Top && base.Y <= (float)level.Bounds.Top)
		{
			tiles.RenderAt(Position + new Vector2(0f, -3f));
		}
		if (level.ShakeVector.Y > 0f && level.Camera.Y + 180f >= (float)level.Bounds.Bottom && base.Y + base.Height >= (float)level.Bounds.Bottom)
		{
			tiles.RenderAt(Position + new Vector2(0f, 3f));
		}
		base.Render();
	}
}
