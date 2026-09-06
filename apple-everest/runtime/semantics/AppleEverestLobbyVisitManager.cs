#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Celeste.Mod;

internal class AppleEverestLobbyVisitManager
{
	public class VisitedPoint
	{
		public Vector2 Point;

		public float DistanceSquared;

		public VisitedPoint(Vector2 point, float distanceSquared = float.MaxValue)
		{
			Point = point;
			DistanceSquared = distanceSquared;
		}
	}

	public const int EXPLORATION_RADIUS = 20;

	private const ushort currentVersion = 1;

	public List<VisitedPoint> VisitedPoints { get; } = new List<VisitedPoint>();

	public List<string> ActivatedWarps { get; } = new List<string>();

	public bool VisitedAll { get; private set; }

	public string SID { get; }

	public string Room { get; }

	public string Key => GetKey(SID, Room);

	private static string GetKey(string sid, string room = null)
	{
		if (!string.IsNullOrWhiteSpace(room))
		{
			return sid + "." + room;
		}
		return sid;
	}

	public bool MatchesKey(string sid, string room = null)
	{
		return Key == GetKey(sid, room);
	}

	public AppleEverestLobbyVisitManager(string sid, string room = null)
	{
		SID = sid;
		Room = room;
		Load();
	}

	public void VisitAll(bool shouldSave = true)
	{
		VisitedPoints.Clear();
		VisitedAll = true;
		if (shouldSave)
		{
			Save();
		}
	}

	public void Reset(bool shouldSave = true)
	{
		VisitedPoints.Clear();
		ActivatedWarps.Clear();
		VisitedAll = false;
		if (shouldSave)
		{
			Save();
		}
	}

	public void Save()
	{
		try
		{
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write((ushort)1);
			binaryWriter.Write(VisitedAll);
			if (!VisitedAll)
			{
				binaryWriter.Write((uint)VisitedPoints.Count);
				foreach (VisitedPoint visitedPoint in VisitedPoints)
				{
					binaryWriter.Write((short)visitedPoint.Point.X);
					binaryWriter.Write((short)visitedPoint.Point.Y);
				}
			}
			binaryWriter.Write((uint)ActivatedWarps.Count);
			foreach (string activatedWarp in ActivatedWarps)
			{
				binaryWriter.Write(activatedWarp);
			}
			AppleEverestCollabModule.Instance.SaveData.VisitedLobbyPositions[Key] = Convert.ToBase64String(memoryStream.ToArray());
		}
		catch (Exception)
		{
			Logger.Log(LogLevel.Error, "CollabUtils2/LobbyVisitManager", "Save: Error trying to serialise visited points.");
		}
	}

	private void Load()
	{
		VisitedPoints.Clear();
		ActivatedWarps.Clear();
		VisitedAll = false;
		if (!AppleEverestCollabModule.Instance.SaveData.VisitedLobbyPositions.TryGetValue(Key, out var value))
		{
			return;
		}
		try
		{
			bool flag = false;
			List<VisitedPoint> list = new List<VisitedPoint>();
			List<string> list2 = new List<string>();
			using (MemoryStream input = new MemoryStream(Convert.FromBase64String(value)))
			{
				using BinaryReader binaryReader = new BinaryReader(input);
				if (binaryReader.ReadUInt16() != 1)
				{
					Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyVisitManager", "Load: Wrong version found, clearing stored data instead.");
					AppleEverestCollabModule.Instance.SaveData.VisitedLobbyPositions.Remove(Key);
					return;
				}
				flag = binaryReader.ReadBoolean();
				if (!flag)
				{
					uint num = binaryReader.ReadUInt32();
					for (int i = 0; i < num; i++)
					{
						short num2 = binaryReader.ReadInt16();
						short num3 = binaryReader.ReadInt16();
						list.Add(new VisitedPoint(new Vector2(num2, num3)));
					}
				}
				uint num4 = binaryReader.ReadUInt32();
				for (int j = 0; j < num4; j++)
				{
					string item = binaryReader.ReadString();
					list2.Add(item);
				}
			}
			VisitedAll = flag;
			VisitedPoints.AddRange(list);
			ActivatedWarps.AddRange(list2);
		}
		catch (Exception)
		{
			Logger.Log(LogLevel.Error, "CollabUtils2/LobbyVisitManager", "Load: Error trying to deserialise visited points, clearing stored data instead.");
			AppleEverestCollabModule.Instance.SaveData.VisitedLobbyPositions.Remove(Key);
		}
	}

	public void ActivateWarp(string id, bool shouldSave = true)
	{
		if (!ActivatedWarps.Contains(id))
		{
			ActivatedWarps.Add(id);
			if (shouldSave)
			{
				Save();
			}
		}
	}

	public void VisitPoint(Vector2 point, bool shouldSave = true)
	{
		if (VisitedAll)
		{
			return;
		}
		bool flag = !VisitedPoints.Any();
		if (!flag && (point - VisitedPoints.First().Point).LengthSquared() > 100f)
		{
			foreach (VisitedPoint visitedPoint in VisitedPoints)
			{
				visitedPoint.DistanceSquared = (visitedPoint.Point - point).LengthSquared();
			}
			VisitedPoints.Sort((VisitedPoint a, VisitedPoint b) => Math.Sign(a.DistanceSquared - b.DistanceSquared));
			flag = VisitedPoints.First().DistanceSquared > 100f;
		}
		if (flag)
		{
			VisitedPoints.Insert(0, new VisitedPoint(point, 0f));
			if (shouldSave)
			{
				Save();
			}
		}
	}
}
