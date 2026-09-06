#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

internal class AppleEverestLobbyMapUI : Entity
{
	public class MarkerImage : Image
	{
		public readonly AppleEverestLobbyMapController.MarkerInfo Info;

		public MarkerImage(AppleEverestLobbyMapController.MarkerInfo info)
			: base(null)
		{
			Info = info;
			string text = info.Icon;
			if (info.Type == AppleEverestLobbyMapController.MarkerType.Map || info.Type == AppleEverestLobbyMapController.MarkerType.HeartSide)
			{
				if (info.Type != AppleEverestLobbyMapController.MarkerType.HeartSide && info.MapInfo.Difficulty >= 0 && GFX.Gui.Has(text + info.MapInfo.Difficulty))
				{
					text += info.MapInfo.Difficulty;
				}
				if (info.MapInfo.Completed && GFX.Gui.Has(text + "Completed"))
				{
					text += "Completed";
				}
			}
			Texture = GFX.Gui[text];
			CenterOrigin();
		}
	}

	private class LobbySelection
	{
		public readonly AppleEverestLobbyMapController.ControllerInfo Info;

		public readonly EntityData Data;

		public readonly string SID;

		public readonly string Room;

		public readonly int RoomIndex;

		public AppleEverestLobbyMapController.MarkerInfo[] Markers;

		public LobbySelection(EntityData data, MapData map)
		{
			Info = new AppleEverestLobbyMapController.ControllerInfo(data, map);
			Data = data;
			SID = map.Area.SID;
			Room = data.Level.Name;
			RoomIndex = Info.RoomIndex;
		}
	}

	private readonly VirtualJoystick lobbyMapJoystick;

	private readonly VirtualButton lobbyMapUpButton;

	private readonly VirtualButton lobbyMapDownButton;

	private readonly VirtualButton lobbyMapLeftButton;

	private readonly VirtualButton lobbyMapRightButton;

	private AppleEverestButtonHelper.ButtonRenderInfo changeDestinationButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo changeLobbyButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo closeButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo confirmButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo zoomButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo holdToPanButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo panButtonRenderInfo;

	private AppleEverestButtonHelper.ButtonRenderInfo aimButtonRenderInfo;

	private readonly List<LobbySelection> lobbySelections = new List<LobbySelection>();

	private readonly List<AppleEverestLobbyMapController.MarkerInfo> activeWarps = new List<AppleEverestLobbyMapController.MarkerInfo>();

	private int selectedLobbyIndex;

	private int[] selectedWarpIndexes;

	private AppleEverestLobbyMapController.ControllerInfo lobbyMapInfo;

	private AppleEverestByteArray2D visitedTiles;

	private AppleEverestLobbyVisitManager visitManager;

	private int heartCount;

	private int initialLobbyIndex;

	private int initialWarpIndex;

	private Texture2D mapTexture;

	private Texture2D overlayTexture;

	private VirtualRenderTarget renderTarget;

	private readonly List<Component> markerComponents = new List<Component>();

	private readonly MTexture arrowTexture = GFX.Gui["towerarrow"];

	private Sprite heartSprite;

	private Sprite maddyRunSprite;

	private readonly Wiggler selectWarpWiggler;

	private readonly Wiggler selectLobbyWiggler;

	private readonly Wiggler closeWiggler;

	private readonly Wiggler confirmWiggler;

	private readonly Wiggler zoomWiggler;

	private readonly float[] zoomLevels = new float[3] { 1f, 2f, 3f };

	private const int defaultZoomLevel = 1;

	private int zoomLevel = -1;

	private float actualScale;

	private Vector2 actualOrigin;

	private float targetScale = 1f;

	private Vector2 targetOrigin = Vector2.Zero;

	private Vector2 selectedOrigin = Vector2.Zero;

	private bool shouldShowMaddy;

	private bool shouldCentreOrigin;

	private float scaleTimeRemaining;

	private float translateTimeRemaining;

	private const float scale_time_seconds = 0.3f;

	private const float translate_time_seconds = 0.3f;

	private int lastSelectedWarpIndex = -1;

	private float scaleMultiplier = 1f;


	private Rectangle windowBounds;

	private Rectangle mapBounds;

	private bool focused;

	private bool closing;

	private bool openedWithRevealMap;

	private readonly bool viewOnly;

	private Vector2 initialPlayerCenter;

	private float finalScale => actualScale * scaleMultiplier;

	public AppleEverestLobbyMapUI(bool viewOnly = false)
	{
		base.Tag = (int)Tags.PauseUpdate | (int)Tags.HUD;
		base.Depth = -10002;
		Visible = false;
		lobbyMapJoystick = new VirtualJoystick(AppleEverestCollabModule.Instance.Settings.PanLobbyMapUp.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapDown.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapLeft.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0.1f);
		lobbyMapUpButton = new VirtualButton(AppleEverestCollabModule.Instance.Settings.PanLobbyMapUp.Binding, Input.Gamepad, 0f, 0.4f);
		lobbyMapDownButton = new VirtualButton(AppleEverestCollabModule.Instance.Settings.PanLobbyMapDown.Binding, Input.Gamepad, 0f, 0.4f);
		lobbyMapLeftButton = new VirtualButton(AppleEverestCollabModule.Instance.Settings.PanLobbyMapLeft.Binding, Input.Gamepad, 0f, 0.4f);
		lobbyMapRightButton = new VirtualButton(AppleEverestCollabModule.Instance.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0f, 0.4f);
		this.viewOnly = viewOnly;
		windowBounds = new Rectangle(100, 140, Engine.Width - 100 - 100, Engine.Height - 140 - 80);
		mapBounds = new Rectangle(windowBounds.Left + 10, windowBounds.Top + 10, windowBounds.Width - 10 - 10, windowBounds.Height - 10 - 40);
		renderTarget = VirtualContent.CreateRenderTarget("CU2_LobbyMapUI", windowBounds.Width, windowBounds.Height);
		Add(selectWarpWiggler = Wiggler.Create(0.4f, 4f));
		Add(selectLobbyWiggler = Wiggler.Create(0.4f, 4f));
		Add(closeWiggler = Wiggler.Create(0.4f, 4f));
		Add(confirmWiggler = Wiggler.Create(0.4f, 4f));
		Add(zoomWiggler = Wiggler.Create(0.4f, 4f));
		Add(new BeforeRenderHook(beforeRender));
		Add(new Coroutine(mapFocusRoutine()));
		changeDestinationButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_change_destination"), Input.MenuUp, Input.MenuDown, null, null, selectWarpWiggler);
		changeLobbyButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_change_lobby"), Input.MenuLeft, Input.MenuRight, null, null, selectLobbyWiggler);
		closeButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_close"), Input.MenuCancel, null, null, null, closeWiggler);
		confirmButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_confirm"), Input.MenuConfirm, null, null, null, confirmWiggler);
		zoomButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_zoom"), Input.MenuJournal, null, null, null, zoomWiggler);
		holdToPanButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_hold_to_pan"), new VirtualButton
		{
			Binding = AppleEverestCollabModule.Instance.Settings.HoldToPan.Binding
		});
		panButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_pan"), lobbyMapUpButton, lobbyMapDownButton, lobbyMapLeftButton, lobbyMapRightButton);
		aimButtonRenderInfo = new AppleEverestButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_pan"), new VirtualButton
		{
			Binding = Settings.Instance.Up
		}, new VirtualButton
		{
			Binding = Settings.Instance.Down
		}, new VirtualButton
		{
			Binding = Settings.Instance.Left
		}, new VirtualButton
		{
			Binding = Settings.Instance.Right
		});
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		openedWithRevealMap = AppleEverestCollabModule.Instance.SaveData.RevealMap;
		if (!(scene is Level level))
		{
			return;
		}
		Player entity = level.Tracker.GetEntity<Player>();
		if (entity != null)
		{
			initialPlayerCenter = entity.Center;
			SetLocked(locked: true);
			string path = (entity.Inventory.Backpack ? "marker/runBackpack" : "marker/runNoBackpack");
			Add(maddyRunSprite = new Sprite(MTN.Mountain, path));
			maddyRunSprite.Justify = new Vector2(0.5f, 1f);
			maddyRunSprite.Scale = new Vector2(0.3f);
			maddyRunSprite.Visible = false;
			maddyRunSprite.AddLoop("idle", "", 0.125f);
			maddyRunSprite.Play("idle");
			getLobbyControllers(level);
			if (updateSelectedLobby(first: true))
			{
				openScreen();
			}
		}
	}

	public override void Removed(Scene scene)
	{
		base.Removed(scene);
		renderTarget?.Dispose();
		overlayTexture?.Dispose();
		renderTarget = null;
		overlayTexture = null;
		mapTexture = null;
		SetLocked(locked: false);
	}

	public override void Update()
	{
		base.Update();
		if (!CheckLocked())
		{
			closeScreen();
			return;
		}
		if (focused)
		{
			bool check = AppleEverestCollabModule.Instance.Settings.HoldToPan.Check;
			Vector2 value = Input.Aim.Value;
			Vector2 value2 = lobbyMapJoystick.Value;
			bool flag = value.LengthSquared() > float.Epsilon;
			Vector2 vec = ((check & flag) ? value : value2);
			bool flag2 = vec.LengthSquared() > float.Epsilon;
			if (!viewOnly && activeWarps.Count > 0 && !check)
			{
				int num = 0;
				if (Input.MenuUp.Pressed)
				{
					if (!Input.MenuUp.Repeating && selectedWarpIndexes[selectedLobbyIndex] == 0)
					{
						selectedWarpIndexes[selectedLobbyIndex] = activeWarps.Count - 1;
						num = -1;
					}
					else if (selectedWarpIndexes[selectedLobbyIndex] > 0)
					{
						selectedWarpIndexes[selectedLobbyIndex]--;
						num = -1;
					}
				}
				else if (Input.MenuDown.Pressed)
				{
					if (!Input.MenuDown.Repeating && selectedWarpIndexes[selectedLobbyIndex] == activeWarps.Count - 1)
					{
						selectedWarpIndexes[selectedLobbyIndex] = 0;
						num = 1;
					}
					else if (selectedWarpIndexes[selectedLobbyIndex] < activeWarps.Count - 1)
					{
						selectedWarpIndexes[selectedLobbyIndex]++;
						num = 1;
					}
				}
				if (num != 0)
				{
					Audio.Play((num < 0) ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
					selectWarpWiggler.Start();
				}
			}
			if (!check && Input.MenuLeft.Pressed)
			{
				if (selectedLobbyIndex > 0)
				{
					Audio.Play("event:/ui/main/rollover_up");
					selectLobbyWiggler.Start();
					lastSelectedWarpIndex = -1;
					selectedLobbyIndex--;
					updateSelectedLobby();
				}
			}
			else if (!check && Input.MenuRight.Pressed)
			{
				if (selectedLobbyIndex < lobbySelections.Count - 1)
				{
					Audio.Play("event:/ui/main/rollover_down");
					selectLobbyWiggler.Start();
					lastSelectedWarpIndex = -1;
					selectedLobbyIndex++;
					updateSelectedLobby();
				}
			}
			else if (Input.MenuJournal.Pressed)
			{
				zoomWiggler.Start();
				zoomLevel--;
				if (zoomLevel < 0)
				{
					zoomLevel = zoomLevels.Length - 1;
					Audio.Play("event:/ui/main/rollover_up");
				}
				else
				{
					Audio.Play("event:/ui/main/rollover_down");
				}
				targetScale = zoomLevels[zoomLevel];
				scaleTimeRemaining = 0.3f;
				shouldCentreOrigin = zoomLevel == 0;
				if (shouldCentreOrigin || zoomLevel == zoomLevels.Length - 1)
				{
					targetOrigin = (shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin);
					translateTimeRemaining = 0.3f;
				}
			}
			else if (((!shouldCentreOrigin && translateTimeRemaining <= 0f && scaleTimeRemaining <= 0f) & flag2) && mapTexture != null)
			{
				float num2 = (float)mapTexture.Width / (float)mapTexture.Height;
				Vector2 vector = vec.SafeNormalize() * 2f / actualScale;
				if (num2 > 0f)
				{
					vector.X /= num2;
				}
				else
				{
					vector.Y *= num2;
				}
				Vector2 val = actualOrigin + vector * Engine.DeltaTime;
				actualOrigin = val.Clamp(0f, 0f, 1f, 1f);
				if (!viewOnly && !shouldCentreOrigin)
				{
					int num3 = nearestWarpIndexToActualOrigin();
					if (num3 != selectedWarpIndexes[selectedLobbyIndex])
					{
						Audio.Play((num3 < selectedWarpIndexes[selectedLobbyIndex]) ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
						selectedWarpIndexes[selectedLobbyIndex] = (lastSelectedWarpIndex = num3);
						selectedOrigin = originForPosition(activeWarps[num3].Position);
					}
				}
				updateMarkers();
			}
			bool flag3 = false;
			int num4 = selectedWarpIndexes[selectedLobbyIndex];
			if (!viewOnly && Input.MenuConfirm.Pressed && num4 >= 0 && num4 < activeWarps.Count)
			{
				if (selectedLobbyIndex == initialLobbyIndex && num4 == initialWarpIndex)
				{
					flag3 = true;
				}
				else
				{
					AppleEverestLobbyMapController.MarkerInfo warp = activeWarps[num4];
					confirmWiggler.Start();
					teleportToWarp(warp);
				}
			}
			else if (Input.MenuCancel.Pressed)
			{
				flag3 = true;
			}
			else if (Input.ESC.Pressed)
			{
				flag3 = true;
				Input.ESC.ConsumeBuffer();
			}
			else if (Input.Pause.Pressed)
			{
				flag3 = true;
				Input.Pause.ConsumeBuffer();
			}
			if (flag3)
			{
				closeWiggler.Start();
				closeScreen();
				return;
			}
		}
		if (!viewOnly && activeWarps.Count > 0 && lastSelectedWarpIndex != selectedWarpIndexes[selectedLobbyIndex])
		{
			selectedOrigin = originForPosition(activeWarps[selectedWarpIndexes[selectedLobbyIndex]].Position);
			if (lastSelectedWarpIndex < 0)
			{
				actualOrigin = (shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin);
			}
			else if (!shouldCentreOrigin)
			{
				targetOrigin = selectedOrigin;
				translateTimeRemaining = 0.3f;
			}
			lastSelectedWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
		}
	}

	private int nearestWarpIndexToActualOrigin()
	{
		int result = -1;
		float num = float.MaxValue;
		for (int i = 0; i < activeWarps.Count; i++)
		{
			Vector2 vector = originForPosition(activeWarps[i].Position);
			float num2 = (actualOrigin - vector).LengthSquared();
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private bool isVisited(Vector2 position, byte threshold = 127)
	{
		if (visitedTiles.TryGet((int)(position.X / 8f), (int)(position.Y / 8f), out var value))
		{
			return value > threshold;
		}
		return false;
	}

	private Vector2 originForPosition(Vector2 point)
	{
		float num = point.X / 8f;
		return new Vector2(y: point.Y / 8f / (float)(lobbyMapInfo?.RoomHeight ?? 1), x: num / (float)(lobbyMapInfo?.RoomWidth ?? 1));
	}

	private void getLobbyControllers(Level level)
	{
		string collabName = AppleEverestCollabPresentation.GetCollabNameForSID(level.Session.Area.SID);
		List<string> list = AppleEverestCollabModule.Instance.SaveData.VisitedLobbyPositions.Keys.Where((string k) => k.StartsWith(collabName)).ToList();
		string text = level.Session.Area.SID + "." + level.Session.Level;
		if (!list.Contains(text))
		{
			list.Add(text);
		}
		lobbySelections.Clear();
		foreach (string item in list)
		{
			string text2 = string.Empty;
			string sid = item;
			if (item.LastIndexOf('.') > item.LastIndexOf('/'))
			{
				text2 = item.Substring(item.LastIndexOf('.') + 1);
				sid = item.Substring(0, item.LastIndexOf('.'));
			}
			if (item != text && !getLobbyVisitManager(level, sid, text2).ActivatedWarps.Any())
			{
				continue;
			}
			MapData mapData = AppleEverestCollabPresentation.Area(sid)?.Mode.FirstOrDefault()?.MapData;
			if (mapData == null)
			{
				continue;
			}
			LevelData levelData = (string.IsNullOrWhiteSpace(text2) ? null : mapData.Get(text2));
			EntityData entityData = ((levelData == null) ? mapData.Levels.Select((LevelData l) => findEntityData(l, "CollabUtils2/LobbyMapController")).FirstOrDefault() : findEntityData(levelData, "CollabUtils2/LobbyMapController"));
			if (entityData == null)
			{
				continue;
			}
			LobbySelection lobbySelection = new LobbySelection(entityData, mapData);
			List<AppleEverestLobbyMapController.MarkerInfo> list2 = new List<AppleEverestLobbyMapController.MarkerInfo>();
			foreach (EntityData item2 in lobbySelection.Data.Level.Entities.Concat(lobbySelection.Data.Level.Triggers))
			{
				if (AppleEverestLobbyMapController.MarkerInfo.TryParse(item2, lobbySelection.Info, out var value))
				{
					value.SID = lobbySelection.SID;
					value.Room = lobbySelection.Room;
					list2.Add(value);
				}
			}
			lobbySelection.Markers = list2.ToArray();
			lobbySelections.Add(lobbySelection);
		}
		lobbySelections.Sort(delegate(LobbySelection lhs, LobbySelection rhs)
		{
			int num = string.CompareOrdinal(lhs.SID, rhs.SID);
			if (num != 0)
			{
				return num;
			}
			int num2 = Math.Sign(lhs.RoomIndex - rhs.RoomIndex);
			return (num2 != 0) ? num2 : string.CompareOrdinal(lhs.Room, rhs.Room);
		});
		selectedWarpIndexes = new int[lobbySelections.Count];
		selectedLobbyIndex = lobbySelections.FindIndex((LobbySelection s) => s.SID == level.Session.Area.SID && s.Room == level.Session.Level);
		if (selectedLobbyIndex < 0)
		{
			AppleEverestStaticRuntime.Log("collab-map=missing-controller");
			selectedLobbyIndex = 0;
		}
	}

	private static AppleEverestLobbyVisitManager getLobbyVisitManager(Scene scene, string sid, string room)
	{
		AppleEverestLobbyMapController entity = scene.Tracker.GetEntity<AppleEverestLobbyMapController>();
		if (entity != null)
		{
			AppleEverestLobbyVisitManager lobbyVisitManager = entity.VisitManager;
			if (lobbyVisitManager != null && lobbyVisitManager.MatchesKey(sid, room))
			{
				return entity.VisitManager;
			}
		}
		return new AppleEverestLobbyVisitManager(sid, room);
	}

	public bool updateSelectedLobby(bool first = false)
	{
		if (selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbySelections.Count)
		{
			AppleEverestStaticRuntime.Log("collab-map=invalid-selection");
			return false;
		}
		if (Engine.Scene is Level level2)
		{
			Player entity = level2.Tracker.GetEntity<Player>();
			if (entity != null)
			{
				LobbySelection lobbySelection = lobbySelections[selectedLobbyIndex];
				AppleEverestLobbyMapController.MarkerInfo[] array = lobbySelections[selectedLobbyIndex].Markers;
				lobbyMapInfo = lobbySelection.Info;
				visitManager = getLobbyVisitManager(base.Scene, lobbySelection.SID, lobbySelection.Room);
				if (openedWithRevealMap || visitManager.VisitedAll)
				{
					visitedTiles = new AppleEverestByteArray2D(0, 0);
				}
				else
				{
					visitedTiles = generateVisitedTiles(lobbyMapInfo, visitManager);
					AppleEverestLobbyMapController.MarkerInfo[] array2 = array.Where((AppleEverestLobbyMapController.MarkerInfo m) => isVisited(m.Position, 127)).ToArray();
					if (array2.Length == array.Length && lobbyMapInfo.RevealWhenAllMarkersFound)
					{
						visitManager.VisitAll();
					}
					array = array2;
				}
				activeWarps.Clear();
				activeWarps.AddRange(array.Where((AppleEverestLobbyMapController.MarkerInfo f) => f.Type == AppleEverestLobbyMapController.MarkerType.Warp && (!f.WarpRequiresActivation || visitManager.ActivatedWarps.Contains(f.MarkerId))));
				activeWarps.Sort((AppleEverestLobbyMapController.MarkerInfo lhs, AppleEverestLobbyMapController.MarkerInfo rhs) => (!int.TryParse(lhs.MarkerId.Trim(), out var result) || !int.TryParse(rhs.MarkerId.Trim(), out var result2)) ? string.CompareOrdinal(lhs.MarkerId, rhs.MarkerId) : Math.Sign(result - result2));
				bool rainbowBerryUnlocked = isRainbowBerryUnlocked(lobbyMapInfo.LevelSet);
				markerComponents.ForEach(delegate(Component c)
				{
					c.RemoveSelf();
				});
				markerComponents.Clear();
				markerComponents.AddRange((from f in array.Where(delegate(AppleEverestLobbyMapController.MarkerInfo f)
					{
						if (!lobbyMapInfo.ShouldShowMarker(f))
						{
							return false;
						}
						if (f.Type == AppleEverestLobbyMapController.MarkerType.Warp && f.WarpRequiresActivation && !visitManager.ActivatedWarps.Contains(f.MarkerId))
						{
							return false;
						}
						return (f.Type != AppleEverestLobbyMapController.MarkerType.RainbowBerry || rainbowBerryUnlocked) ? true : false;
					})
					orderby f.Type descending
					select f).Select(createMarkerComponent));
				markerComponents.ForEach(base.Add);
				if (!viewOnly & first)
				{
					selectedWarpIndexes[selectedLobbyIndex] = 0;
					float num = float.MaxValue;
					for (int num2 = 0; num2 < activeWarps.Count; num2++)
					{
						float num3 = (activeWarps[num2].Position + level2.LevelOffset - entity.Position).LengthSquared();
						if (num3 < num)
						{
							num = num3;
							selectedWarpIndexes[selectedLobbyIndex] = num2;
						}
					}
					initialLobbyIndex = selectedLobbyIndex;
					initialWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
				}
				mapTexture = GFX.Gui[lobbyMapInfo.MapTexture].Texture.Texture;
				overlayTexture?.Dispose();
				overlayTexture = null;
				if (!openedWithRevealMap && !visitManager.VisitedAll)
				{
					overlayTexture = new Texture2D(Engine.Instance.GraphicsDevice, lobbyMapInfo.RoomWidth, lobbyMapInfo.RoomHeight, mipMap: false, SurfaceFormat.Alpha8);
					overlayTexture.SetData(visitedTiles.Data);
				}
				if (zoomLevel < 0)
				{
					zoomLevel = 1;
				}
				zoomLevel = Calc.Clamp(zoomLevel, 0, zoomLevels.Length);
				actualScale = zoomLevels[zoomLevel];
				shouldCentreOrigin = zoomLevel == 0;
				bool flag = lobbySelection.SID == level2.Session.Area.SID && lobbySelection.Room == level2.Session.Level;
				shouldShowMaddy = !viewOnly | flag;
				if (!viewOnly)
				{
					int num4 = selectedWarpIndexes[selectedLobbyIndex];
					selectedOrigin = ((num4 >= 0 && num4 < activeWarps.Count) ? originForPosition(activeWarps[num4].Position) : new Vector2(0.5f));
					actualOrigin = (shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin);
				}
				else if (flag)
				{
					selectedOrigin = originForPosition(entity.Position - new Vector2(level2.Bounds.X, level2.Bounds.Y));
					actualOrigin = (shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin);
				}
				else
				{
					selectedOrigin = (actualOrigin = new Vector2(0.5f));
				}
				translateTimeRemaining = 0f;
				scaleTimeRemaining = 0f;
				Rectangle rectangle = mapBounds;
				float num5 = (float)mapTexture.Width / (float)mapTexture.Height;
				float num6 = (float)rectangle.Width / (float)rectangle.Height;
				scaleMultiplier = ((num5 > num6) ? ((float)rectangle.Width / (float)mapTexture.Width) : ((float)rectangle.Height / (float)mapTexture.Height));
				updateMarkers();
				int animationFrame = heartSprite?.CurrentAnimationFrame ?? 0;
				heartSprite?.RemoveSelf();
				heartSprite = null;
				if (lobbyMapInfo.ShowHeartCount)
				{
					// The selected Beginner lobby has no heart-sprite override.
					heartSprite = GFX.GuiSpriteBank.Create("heartgem0");
					heartSprite.Scale = Vector2.One / 2f;
					heartSprite.Position = new Vector2(windowBounds.Left + 10, windowBounds.Top + 10);
					heartSprite.Justify = Vector2.Zero;
					heartSprite.Play("spin");
					heartSprite.SetAnimationFrame(animationFrame);
					heartSprite.JustifyOrigin(Vector2.Zero);
					Add(heartSprite);
					heartCount = AppleEverestProgressionRuntime.TotalHearts(lobbyMapInfo.LevelSet, SaveData.Instance);
				}
				return true;
			}
		}
		Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyMapUI", "updateSelectedLobby: Couldn't find level or player");
		return false;
	}

	private Component createMarkerComponent(AppleEverestLobbyMapController.MarkerInfo markerInfo)
	{
		return new MarkerImage(markerInfo);
	}

	private void beforeRender()
	{
		VirtualRenderTarget virtualRenderTarget = renderTarget;
		if (virtualRenderTarget == null || virtualRenderTarget.IsDisposed)
		{
			return;
		}
		Texture2D texture2D = mapTexture;
		if (texture2D == null || texture2D.IsDisposed)
		{
			return;
		}
		Vector2 vector = new Vector2(mapBounds.Center.X - windowBounds.Left, mapBounds.Center.Y - windowBounds.Top);
		float num = finalScale;
		float num2 = (float)mapTexture.Width * num;
		float num3 = (float)mapTexture.Height * num;
		Rectangle rectangle = new Rectangle((int)(vector.X - actualOrigin.X * num2), (int)(vector.Y - actualOrigin.Y * num3), (int)num2, (int)num3);
		Engine.Graphics.GraphicsDevice.SetRenderTarget(renderTarget);
		Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
		if (!openedWithRevealMap && !visitManager.VisitedAll)
		{
			Texture2D texture2D2 = overlayTexture;
			if (texture2D2 != null && !texture2D2.IsDisposed)
			{
				Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState
				{
					AlphaSourceBlend = Blend.One,
					AlphaDestinationBlend = Blend.Zero,
					ColorSourceBlend = Blend.Zero,
					ColorDestinationBlend = Blend.Zero
				});
				Draw.SpriteBatch.Draw(overlayTexture, rectangle, Color.White);
				Draw.SpriteBatch.End();
				Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState
				{
					AlphaSourceBlend = Blend.Zero,
					AlphaDestinationBlend = Blend.One,
					ColorSourceBlend = Blend.DestinationAlpha,
					ColorDestinationBlend = Blend.Zero
				});
				goto IL_01af;
			}
		}
		Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
		goto IL_01af;
		IL_01af:
		Draw.SpriteBatch.Draw(mapTexture, rectangle, Color.White);
		Draw.SpriteBatch.End();
		if (Engine.Commands.Open)
		{
			Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
			Draw.HollowRect(rectangle, Color.Red);
			Draw.SpriteBatch.End();
		}
	}

	public override void Render()
	{
		drawBackground();
		drawMap();
		base.Render();
		if (shouldShowMaddy)
		{
			maddyRunSprite?.Render();
		}
		if (AppleEverestCollabModule.Instance.SaveData.ShowVisitedPoints)
		{
			drawVisitedPoints();
		}
		// No selected provider registers a custom map-render callback.
		drawForeground();
	}

	private void drawBackground()
	{
		Draw.Rect(0f, 0f, Engine.Width, Engine.Height, Color.Black * 0.9f);
	}

	private void drawForeground()
	{
		Rectangle rectangle = new Rectangle(-10, -10, Engine.Width + 20, Engine.Height + 20);
		Rectangle rectangle2 = windowBounds;
		rectangle2.Inflate(16, 16);
		Draw.Rect(rectangle.Left, rectangle.Top, rectangle.Width, windowBounds.Top - rectangle.Top, Color.Black);
		Draw.Rect(rectangle.Left, windowBounds.Bottom, rectangle.Width, rectangle.Bottom - windowBounds.Bottom, Color.Black);
		Draw.Rect(rectangle.Left, windowBounds.Top - 10, windowBounds.Left - rectangle.Left, windowBounds.Height + 20, Color.Black);
		Draw.Rect(windowBounds.Right, windowBounds.Top - 10, rectangle.Right - windowBounds.Right, windowBounds.Height + 20, Color.Black);
		Draw.Rect(rectangle2.Left, rectangle2.Top, rectangle2.Width, 8f, Color.White);
		Draw.Rect(rectangle2.Left, rectangle2.Bottom - 8, rectangle2.Width, 8f, Color.White);
		Draw.Rect(rectangle2.Left, rectangle2.Top, 8f, rectangle2.Height, Color.White);
		Draw.Rect(rectangle2.Right - 8, rectangle2.Top, 8f, rectangle2.Height, Color.White);
		string text = Dialog.Clean(lobbySelections[selectedLobbyIndex].SID);
		float num = 1f;
		float num2 = ActiveFont.Measure(text).X * 1.75f;
		float y = (float)rectangle2.Top * 0.5f;
		ActiveFont.DrawEdgeOutline(text, new Vector2(960f, y), new Vector2(0.5f, 0.5f), Vector2.One * 1.75f, Color.Gray * num, 4f, Color.DarkSlateBlue * num, 2f, Color.Black * num);
		if (selectedLobbyIndex > 0)
		{
			arrowTexture.DrawCentered(new Vector2(960f - num2 / 2f - 100f, y), Color.White * num, 0.9f);
		}
		if (selectedLobbyIndex < lobbySelections.Count - 1)
		{
			arrowTexture.DrawCentered(new Vector2(960f + num2 / 2f + 100f, y), Color.White * num, 0.9f, (float)Math.PI);
		}
		if (lobbyMapInfo.ShowHeartCount && heartSprite != null)
		{
			Color color = ((heartCount >= lobbyMapInfo.TotalMaps) ? Color.Gold : Color.White);
			string text2 = $"{heartCount} / {lobbyMapInfo.TotalMaps}";
			Vector2 vector = ActiveFont.Measure(text2);
			Vector2 position = new Vector2(heartSprite.Position.X + heartSprite.Width / 2f + 10f + vector.X / 2f, heartSprite.Position.Y + heartSprite.Height / 4f);
			ActiveFont.DrawOutline(text2, position, new Vector2(0.5f), Vector2.One, color, 2f, Color.Black);
		}
		if (!viewOnly)
		{
			int num3 = selectedWarpIndexes[selectedLobbyIndex];
			if (num3 >= 0 && num3 < activeWarps.Count && !string.IsNullOrWhiteSpace(activeWarps[num3].DialogKey))
			{
				ActiveFont.DrawOutline(Dialog.Clean(activeWarps[num3].DialogKey), new Vector2(windowBounds.Center.X, (float)windowBounds.Bottom + -18f), new Vector2(0.5f), new Vector2(0.8f), Color.White * 1f, 2f, Color.Black);
			}
		}
		Vector2 position2 = new Vector2(windowBounds.Left, (float)windowBounds.Bottom + 45f);
		bool check = AppleEverestCollabModule.Instance.Settings.HoldToPan.Check;
		if (!viewOnly && activeWarps.Count > 1 && !check)
		{
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, changeDestinationButtonRenderInfo, 0.5f, 1f, 0f, 0.05f);
		}
		if (lobbySelections.Count > 1 && !check)
		{
			changeLobbyButtonRenderInfo.Button1Alpha = ((selectedLobbyIndex > 0) ? 1f : 0.4f);
			changeLobbyButtonRenderInfo.Button2Alpha = ((selectedLobbyIndex < lobbySelections.Count - 1) ? 1f : 0.4f);
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, changeLobbyButtonRenderInfo, 0.5f, 1f, 0f, 0.05f);
		}
		position2.X = windowBounds.Right;
		AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, closeButtonRenderInfo, 0.5f, 1f, 1f, 0.05f);
		if (!viewOnly)
		{
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, confirmButtonRenderInfo, 0.5f, 1f, 1f, 0.05f);
		}
		AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, zoomButtonRenderInfo, 0.5f, 1f, 1f, 0.05f);
		float alpha = (shouldCentreOrigin ? 0.4f : 1f);
		if (check)
		{
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, aimButtonRenderInfo, 0.5f, alpha, 1f, 0.05f);
		}
		else if (hasLatestBinding(AppleEverestCollabModule.Instance.Settings.PanLobbyMapUp.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapDown.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapLeft.Binding, AppleEverestCollabModule.Instance.Settings.PanLobbyMapRight.Binding))
		{
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, panButtonRenderInfo, 0.5f, alpha, 1f, 0.05f);
		}
		else if (hasLatestBinding(AppleEverestCollabModule.Instance.Settings.HoldToPan.Binding))
		{
			AppleEverestButtonHelper.RenderMultiButton(ref position2, 32f, holdToPanButtonRenderInfo, 0.5f, alpha, 1f, 0.05f);
		}
	}

	private void drawMap()
	{
		VirtualRenderTarget virtualRenderTarget = renderTarget;
		if (virtualRenderTarget != null && !virtualRenderTarget.IsDisposed)
		{
			Draw.SpriteBatch.Draw((RenderTarget2D)renderTarget, new Vector2(windowBounds.Left, windowBounds.Top), Color.White);
		}
	}

	private void drawVisitedPoints()
	{
		if (visitManager == null)
		{
			return;
		}
		float num = finalScale;
		float num2 = (float)mapTexture.Width * num;
		float num3 = (float)mapTexture.Height * num;
		foreach (AppleEverestLobbyVisitManager.VisitedPoint visitedPoint in visitManager.VisitedPoints)
		{
			Vector2 vector = originForPosition(visitedPoint.Point * Vector2.One * 8f) - actualOrigin;
			float num4 = (float)mapBounds.Center.X + vector.X * num2;
			float num5 = (float)mapBounds.Center.Y + vector.Y * num3;
			Draw.Rect(num4 - 1f, num5 - 1f, 3f, 3f, Color.Red);
		}
	}

	private void updateMarkers()
	{
		float num = finalScale;
		float num2 = (float)mapTexture.Width * num;
		float num3 = (float)mapTexture.Height * num;
		float num4 = zoomLevels[1] - actualScale;
		float value = ((num4 <= 0f) ? 1f : Calc.LerpClamp(1f, 0.75f, num4 / (zoomLevels[1] - zoomLevels[0])));
		foreach (MarkerImage markerComponent in markerComponents)
		{
			Vector2 vector = originForPosition(markerComponent.Info.Position) - actualOrigin;
			markerComponent.Position = new Vector2((float)mapBounds.Center.X + vector.X * num2, (float)mapBounds.Center.Y + vector.Y * num3);
			markerComponent.Scale = new Vector2(value);
		}
		if (maddyRunSprite != null)
		{
			Vector2 vector2 = selectedOrigin - actualOrigin;
			maddyRunSprite.Position = new Vector2((float)mapBounds.Center.X + vector2.X * num2, (float)mapBounds.Center.Y + vector2.Y * num3);
		}
	}

	private void openScreen()
	{
		SetLocked(locked: true, base.Scene);
		Audio.Play("event:/ui/game/pause");
		Add(new Coroutine(transitionRoutine(0.5f, delegate
		{
			Visible = true;
		})));
	}

	private void closeScreen(bool force = false)
	{
		if (closing)
		{
			return;
		}
		closing = true;
		if (!force)
		{
			Audio.Play("event:/ui/game/unpause");
			Add(new Coroutine(transitionRoutine(0.5f, delegate
			{
				Visible = false;
			}, DoClose)));
		}
		else
		{
			Visible = false;
			DoClose();
		}
		void DoClose()
		{
			SetLocked(locked: false, base.Scene);
			RemoveSelf();
		}
	}

	private void teleportToWarp(AppleEverestLobbyMapController.MarkerInfo warp)
	{
		Scene scene = base.Scene;
		Level level = scene as Level;
		if (level != null)
		{
			focused = false;
			Audio.Play("event:/game/04_cliffside/snowball_spawn");
			createWipe(warp.WipeType, 0.5f, level, wipeIn: false, onComplete);
		}
		void onComplete()
		{
			closeScreen(force: true);
			if (warp.SID != level.Session.Area.SID)
			{
				throw new InvalidOperationException("cross-SID lobby warp is outside the selected profile");
			}
			else
			{
				level.OnEndOfFrame += delegate
				{
					Player entity = level.Tracker.GetEntity<Player>();
					if (entity != null)
					{
						Leader.StoreStrawberries(entity.Leader);
						level.Remove(entity);
					}
					level.UnloadLevel();
					level.Session.Level = warp.Room;
					level.Session.FirstLevel = false;
					level.Session.RespawnPoint = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top) + warp.Position);
					level.LoadLevel(Player.IntroTypes.Respawn);
					level.Wipe?.Cancel();
					Player entity2 = level.Tracker.GetEntity<Player>();
					if (entity2 != null)
					{
						level.Camera.Position = entity2.CameraTarget;
						Leader.RestoreStrawberries(entity2.Leader);
					}
					createWipe(warp.WipeType, 0.5f, level, wipeIn: true);
				};
			}
		}
	}

	private IEnumerator mapFocusRoutine()
	{
		float scaleFrom = actualScale;
		Vector2 translateFrom = actualOrigin;
		while (true)
		{
			if (scaleTimeRemaining == 0.3f)
			{
				scaleFrom = actualScale;
			}
			if (translateTimeRemaining == 0.3f)
			{
				translateFrom = actualOrigin;
			}
			if (scaleTimeRemaining > 0f)
			{
				actualScale = Calc.LerpClamp(scaleFrom, targetScale, Ease.QuintOut(1f - scaleTimeRemaining / 0.3f));
				scaleTimeRemaining -= Engine.DeltaTime;
				if (scaleTimeRemaining <= 0f)
				{
					actualScale = targetScale;
				}
			}
			if (translateTimeRemaining > 0f)
			{
				actualOrigin = Vector2.Lerp(translateFrom, targetOrigin, Ease.QuintOut(1f - translateTimeRemaining / 0.3f));
				translateTimeRemaining -= Engine.DeltaTime;
				if (translateTimeRemaining <= 0f)
				{
					actualOrigin = targetOrigin;
				}
			}
			updateMarkers();
			yield return null;
		}
	}

	private IEnumerator transitionRoutine(float duration = 0.5f, Action onFadeOut = null, Action onFadeIn = null)
	{
		duration = Math.Max(0f, duration);
		focused = false;
		yield return new FadeWipe(base.Scene, wipeIn: false)
		{
			Duration = duration / 2f,
			OnComplete = onFadeOut
		}.Wait();
		yield return new FadeWipe(base.Scene, wipeIn: true)
		{
			Duration = duration / 2f,
			OnComplete = onFadeIn
		}.Wait();
		focused = true;
	}

	private static AppleEverestByteArray2D generateVisitedTiles(AppleEverestLobbyMapController.ControllerInfo config, AppleEverestLobbyVisitManager visitManager)
	{
		AppleEverestByteArray2D byteArray2D = createCircleData(19, 21);
		AppleEverestByteArray2D byteArray2D2 = new AppleEverestByteArray2D(config.RoomWidth, config.RoomHeight);
		foreach (AppleEverestLobbyVisitManager.VisitedPoint visitedPoint in visitManager.VisitedPoints)
		{
			byteArray2D2.Max(byteArray2D, (int)visitedPoint.Point.X - byteArray2D.Width / 2, (int)visitedPoint.Point.Y - byteArray2D.Height / 2);
		}
		for (int i = 0; i < config.RoomWidth; i++)
		{
			byteArray2D2[i, 0] = 0;
			byteArray2D2[i, 1] = Math.Min(byteArray2D2[i, 1], (byte)63);
			byteArray2D2[i, config.RoomHeight - 1] = 0;
			byteArray2D2[i, config.RoomHeight - 2] = Math.Min(byteArray2D2[i, config.RoomHeight - 2], (byte)63);
		}
		for (int j = 1; j < config.RoomHeight - 1; j++)
		{
			byteArray2D2[0, j] = 0;
			byteArray2D2[1, j] = Math.Min(byteArray2D2[1, j], (byte)63);
			byteArray2D2[config.RoomWidth - 1, j] = 0;
			byteArray2D2[config.RoomWidth - 2, j] = Math.Min(byteArray2D2[config.RoomWidth - 2, j], (byte)63);
		}
		return byteArray2D2;
	}

	private static AppleEverestByteArray2D createCircleData(int hardRadius, int softRadius)
	{
		int num = 2 * softRadius;
		AppleEverestByteArray2D byteArray2D = new AppleEverestByteArray2D(num, num);
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				int num2 = (softRadius - j) * (softRadius - j) + (softRadius - i) * (softRadius - i);
				float num3 = ((num2 < hardRadius * hardRadius) ? 0f : ((num2 > softRadius * softRadius) ? 1f : (((float)num2 - (float)(hardRadius * hardRadius)) / (float)(softRadius * softRadius - hardRadius * hardRadius))));
				byteArray2D[j, i] = (byte)((1f - num3) * 255f);
			}
		}
		return byteArray2D;
	}

	private static ScreenWipe createWipe(string wipeTypeName, float wipeDuration, Level level, bool wipeIn, Action onComplete = null)
	{
		if (wipeTypeName != "Celeste.DreamWipe") throw new InvalidOperationException("unreviewed lobby-map wipe");
		return new DreamWipe(level, wipeIn, onComplete) { Duration = wipeDuration };
	}

	private static bool hasLatestBinding(Binding binding1, Binding binding2 = null, Binding binding3 = null, Binding binding4 = null)
	{
		if (Input.GuiInputPrefix() == "keyboard")
		{
			if ((binding1 == null || !binding1.Keyboard.Any()) && (binding2 == null || !binding2.Keyboard.Any()) && (binding3 == null || !binding3.Keyboard.Any()))
			{
				return binding4?.Keyboard.Any() ?? false;
			}
			return true;
		}
		if ((binding1 == null || !binding1.Controller.Any()) && (binding2 == null || !binding2.Controller.Any()) && (binding3 == null || !binding3.Controller.Any()))
		{
			return binding4?.Controller.Any() ?? false;
		}
		return true;
	}

	private static EntityData findEntityData(LevelData levelData, string entityName)
	{
		return levelData.Entities.FirstOrDefault((EntityData e) => e.Name == entityName);
	}

	private static bool isRainbowBerryUnlocked(string levelSet)
	{
		return AppleEverestCollabPresentation.SilverBerries(levelSet).Collected > 0;
	}

	public static void SetLocked(bool locked, Scene scene = null, Player player = null)
	{
		Level level = (scene ?? Engine.Scene) as Level;
		player = player ?? level?.Tracker.GetEntity<Player>();
		if (level != null && player != null)
		{
			level.CanRetry = !locked;
			level.PauseLock = locked;
			player.Speed = Vector2.Zero;
			player.DummyGravity = !locked;
			player.StateMachine.State = (locked ? 11 : 0);
			player.DummyAutoAnimate = !locked || player.OnGround();
		}
	}

	public bool CheckLocked()
	{
		if (base.Scene is Level level)
		{
			Player entity = level.Tracker.GetEntity<Player>();
			if (entity != null)
			{
				if ((initialPlayerCenter - entity.Center).LengthSquared() < 256f && !entity.Dead && !level.CanRetry && level.PauseLock && !entity.DummyGravity && entity.Speed == Vector2.Zero)
				{
					return entity.StateMachine.State == 11;
				}
				return false;
			}
		}
		return false;
	}
}
