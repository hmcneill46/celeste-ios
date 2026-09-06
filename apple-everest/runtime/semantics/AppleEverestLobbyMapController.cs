#nullable disable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal class AppleEverestLobbyMapController : Entity
{
	public class ControllerInfo
	{
		private static readonly char[] commaSeparator = new char[1] { ',' };

		public string MapTexture;

		public int TotalMaps;

		public int RoomIndex;

		public string[] CustomMarkers;

		public string WarpIcon;

		public string RainbowBerryIcon;

		public string HeartGateIcon;

		public string GymIcon;

		public string MapIcon;

		public string JournalIcon;

		public string HeartSideIcon;

		public bool ShowWarps;

		public bool ShowRainbowBerry;

		public bool ShowHeartGate;

		public bool ShowGyms;

		public bool ShowMaps;

		public bool ShowJournals;

		public bool ShowHeartSide;

		public bool ShowHeartCount;

		public bool RevealWhenAllMarkersFound;

		public int RoomWidth;

		public int RoomHeight;

		public string LevelSet;

		public ControllerInfo(EntityData data, MapData mapData = null)
		{
			MapTexture = data.Attr("mapTexture");
			TotalMaps = data.Int("totalMaps");
			RoomIndex = data.Int("roomIndex");
			WarpIcon = data.Attr("warpIcon", "CollabUtils2/lobbies/warp");
			RainbowBerryIcon = data.Attr("rainbowBerryIcon", "CollabUtils2/lobbies/rainbowBerry");
			HeartGateIcon = data.Attr("heartGateIcon", "CollabUtils2/lobbies/heartgate");
			GymIcon = data.Attr("gymIcon", "CollabUtils2/lobbies/gym");
			MapIcon = data.Attr("mapIcon", "CollabUtils2/lobbies/map");
			JournalIcon = data.Attr("journalIcon", "CollabUtils2/lobbies/journal");
			HeartSideIcon = data.Attr("heartSideIcon", "CollabUtils2/lobbies/heartside");
			ShowWarps = data.Bool("showWarps", defaultValue: true);
			ShowRainbowBerry = data.Bool("showRainbowBerry", defaultValue: true);
			ShowHeartGate = data.Bool("showHeartGate", defaultValue: true);
			ShowGyms = data.Bool("showGyms", defaultValue: true);
			ShowMaps = data.Bool("showMaps", defaultValue: true);
			ShowJournals = data.Bool("showJournals", defaultValue: true);
			ShowHeartSide = data.Bool("showHeartSide", defaultValue: true);
			ShowHeartCount = data.Bool("showHeartCount", defaultValue: true);
			RevealWhenAllMarkersFound = data.Bool("revealWhenAllMarkersFound");
			string text = data.Attr("customMarkers");
			CustomMarkers = ((!string.IsNullOrWhiteSpace(text)) ? text.Split(commaSeparator, StringSplitOptions.RemoveEmptyEntries) : new string[0]);
			if (RoomWidth <= 0)
			{
				RoomWidth = data.Level.TileBounds.Width;
			}
			if (RoomHeight <= 0)
			{
				RoomHeight = data.Level.TileBounds.Height;
			}
			if (mapData != null)
			{
				LevelSet = AppleEverestCollabPresentation.GetLobbyLevelSet(mapData.Area.SID);
			}
		}

		public bool ShouldShowMarker(MarkerInfo marker)
		{
			return marker.Type switch
			{
				MarkerType.Custom => true,
				MarkerType.Warp => ShowWarps,
				MarkerType.RainbowBerry => ShowRainbowBerry,
				MarkerType.HeartGate => ShowHeartGate,
				MarkerType.Gym => ShowGyms,
				MarkerType.Map => ShowMaps,
				MarkerType.Journal => ShowJournals,
				MarkerType.HeartSide => ShowHeartSide,
				_ => true,
			};
		}
	}

	public struct MarkerInfo
	{
		public string Icon;

		public string DialogKey;

		public string MarkerId;

		public MarkerType Type;

		public Vector2 Position;

		public string SID;

		public string Room;

		public string Map;

		public MapInfo MapInfo;

		public string WipeType;

		public bool WarpRequiresActivation;

		public static bool TryParse(EntityData data, ControllerInfo controllerInfo, out MarkerInfo value)
		{
			value = default(MarkerInfo);
			if (data.Name == "CollabUtils2/LobbyMapMarker")
			{
				value.Type = MarkerType.Custom;
				value.DialogKey = data.Attr("dialogKey");
				value.Icon = data.Attr("icon");
			}
			else if (data.Name == "CollabUtils2/LobbyMapWarp")
			{
				value.Type = MarkerType.Warp;
				value.DialogKey = data.Attr("dialogKey");
				value.MarkerId = data.Attr("warpId");
				value.Icon = data.Attr("icon");
				value.WipeType = data.Attr("wipeType", "Celeste.Mountain");
				value.WarpRequiresActivation = data.Bool("warpRequiresActivation");
			}
			else if (data.Name == "CollabUtils2/RainbowBerry")
			{
				value.Type = MarkerType.RainbowBerry;
			}
			else if (data.Name == "CollabUtils2/MiniHeartDoor")
			{
				value.Type = MarkerType.HeartGate;
			}
			else if (data.Name == "CollabUtils2/JournalTrigger")
			{
				value.Type = MarkerType.Journal;
			}
			else
			{
				if (!(data.Name == "CollabUtils2/ChapterPanelTrigger") && (controllerInfo == null || !controllerInfo.CustomMarkers.Contains(data.Name)))
				{
					return false;
				}
				value.Map = data.Attr("map");
				value.Type = (AppleEverestCollabPresentation.IsCollabGym(value.Map) ? MarkerType.Gym : (AppleEverestCollabPresentation.IsHeartSide(value.Map) ? MarkerType.HeartSide : MarkerType.Map));
			}
			value.Position = new Vector2(data.Position.X + (float)data.Width / 2f, data.Position.Y + (float)data.Height / 2f);
			if (string.IsNullOrWhiteSpace(value.MarkerId))
			{
				value.MarkerId = $"{value.Type}_{data.ID}";
			}
			if (string.IsNullOrWhiteSpace(value.Icon))
			{
				switch (value.Type)
				{
				case MarkerType.Warp:
					value.Icon = controllerInfo?.WarpIcon ?? "CollabUtils2/lobbies/warp";
					break;
				case MarkerType.RainbowBerry:
					value.Icon = controllerInfo?.RainbowBerryIcon ?? "CollabUtils2/lobbies/rainbowBerry";
					break;
				case MarkerType.HeartGate:
					value.Icon = controllerInfo?.HeartGateIcon ?? "CollabUtils2/lobbies/heartgate";
					break;
				case MarkerType.Gym:
					value.Icon = controllerInfo?.GymIcon ?? "CollabUtils2/lobbies/gym";
					break;
				case MarkerType.Map:
					value.Icon = controllerInfo?.MapIcon ?? "CollabUtils2/lobbies/map";
					break;
				case MarkerType.HeartSide:
					value.Icon = controllerInfo?.HeartSideIcon ?? "CollabUtils2/lobbies/heartside";
					break;
				case MarkerType.Journal:
					value.Icon = controllerInfo?.JournalIcon ?? "CollabUtils2/lobbies/journal";
					break;
				}
			}
			if ((value.Type == MarkerType.Map && !string.IsNullOrWhiteSpace(value.Map)) || value.Type == MarkerType.HeartSide)
			{
				value.MapInfo = new MapInfo(value.Map);
			}
			return true;
		}
	}

	public struct MapInfo
	{
		public readonly string SID = string.Empty;

		public readonly bool Completed = false;

		public readonly int Difficulty = -1;

		public MapInfo(string mapName)
		{
			AreaData areaData = AppleEverestCollabPresentation.Area(mapName);
			if (areaData == null)
			{
				string knownIcon = AppleEverestCollabMapMetadata.Icon(mapName);
				if (knownIcon == null) return;
				SID = mapName;
				int.TryParse(knownIcon.Split('/').Last().Split('-').First(), out Difficulty);
				return;
			}
			SID = AppleEverestCollabPresentation.Sid(areaData);
			AreaStats areaStatsFor = AppleEverestCollabPresentation.Stats(areaData);
			Completed = areaStatsFor != null && areaStatsFor.Modes[0].Completed;
			string icon = areaData.Icon;
			if (!string.IsNullOrWhiteSpace(icon))
			{
				string text = (icon.Split('/').LastOrDefault() ?? string.Empty).Split('-').FirstOrDefault() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(text))
				{
					int.TryParse(text, out Difficulty);
				}
			}
		}
	}

	public enum MarkerType
	{
		Warp,
		RainbowBerry,
		HeartGate,
		Gym,
		Map,
		Journal,
		HeartSide,
		Custom
	}

	public readonly ControllerInfo Info;

	public AppleEverestLobbyVisitManager VisitManager;

	public AppleEverestLobbyMapController(EntityData data, Vector2 offset)
		: base(data.Position + offset)
	{
		Info = new ControllerInfo(data);
		base.Depth = -1000000;
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		if (scene is Level level)
		{
			VisitManager = new AppleEverestLobbyVisitManager(level.Session.Area.SID, level.Session.Level);
		}
	}

	public override void Update()
	{
		base.Update();
		if (!(base.Scene is Level level) || level.Tracker.GetEntity<AppleEverestLobbyMapUI>() != null)
		{
			return;
		}
		Player entity = level.Tracker.GetEntity<Player>();
		if (entity == null)
		{
			return;
		}
		if (AppleEverestCollabModule.Instance.Settings.DisplayLobbyMap.Pressed && level.CanRetry && ((entity.StateMachine.State == 0 && entity.OnGround()) || entity.StateMachine.State == 3) && level.Tracker.GetEntity<AppleEverestLobbyMapUI>() == null)
		{
			AppleEverestLobbyMapWarp lobbyMapWarp = entity.CollideFirst<AppleEverestLobbyMapWarp>();
			if (lobbyMapWarp != null)
			{
				lobbyMapWarp.OnTalk(entity);
				return;
			}
			AppleEverestLobbyMapUI.SetLocked(locked: true, level, entity);
			level.Add(new AppleEverestLobbyMapUI(viewOnly: true));
		}
		else if (level.OnInterval(0.2f) && entity.StateMachine.State != 11 && !AppleEverestCollabModule.Instance.SaveData.PauseVisitingPoints && !level.Paused && !level.Transitioning)
		{
			AppleEverestLobbyVisitManager visitManager = VisitManager;
			if (visitManager != null && !visitManager.VisitedAll)
			{
				Vector2 point = new Vector2(Math.Min((float)Math.Floor((entity.Center.X - (float)level.Bounds.X) / 8f), (float)Math.Round((float)level.Bounds.Width / 8f, MidpointRounding.AwayFromZero) - 1f), Math.Min((float)Math.Floor((entity.Center.Y - (float)level.Bounds.Y) / 8f), (float)Math.Round((float)level.Bounds.Height / 8f, MidpointRounding.AwayFromZero) + 1f));
				VisitManager?.VisitPoint(point);
			}
		}
	}

	public override void Render()
	{
		base.Render();
		if (!AppleEverestCollabModule.Instance.SaveData.ShowVisitedPoints || VisitManager == null || !(base.Scene is Level level))
		{
			return;
		}
		Player entity = level.Tracker.GetEntity<Player>();
		if (entity == null)
		{
			return;
		}
		bool pauseVisitingPoints = AppleEverestCollabModule.Instance.SaveData.PauseVisitingPoints;
		for (int i = 0; i < VisitManager.VisitedPoints.Count; i++)
		{
			AppleEverestLobbyVisitManager.VisitedPoint visitedPoint = VisitManager.VisitedPoints[i];
			if (pauseVisitingPoints || !(visitedPoint.DistanceSquared >= 10000f))
			{
				Vector2 vector = visitedPoint.Point * 8f + Vector2.One * 4f + new Vector2(level.Bounds.Left, level.Bounds.Top);
				if (!pauseVisitingPoints && i < 3)
				{
					Draw.Line(vector.X, vector.Y, entity.CenterX, entity.CenterY, Color.Green);
				}
				Draw.Rect(vector.X - 2f, vector.Y - 2f, 4f, 4f, (i == 0) ? Color.Blue : Color.Red);
				continue;
			}
			break;
		}
	}

	public override void SceneEnd(Scene scene)
	{
		VisitManager?.Save();
		base.SceneEnd(scene);
	}
}
