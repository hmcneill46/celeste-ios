#nullable disable
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabRainbowBerry : Strawberry
{
	private readonly string levelSet;

	private readonly string mapsRaw;

	private readonly string[] maps;

	private readonly int? requiredBerries;

	internal AppleEverestCollabHoloRainbowBerry HologramForCutscene;

	internal int CutsceneTotalBerries;

	public AppleEverestCollabRainbowBerry(EntityData data, Vector2 offset, EntityID gid)
		: base(data, offset, gid)
	{
		levelSet = data.Attr("levelSet");
		if (string.IsNullOrEmpty(data.Attr("maps")))
		{
			maps = null;
			mapsRaw = null;
		}
		else
		{
			maps = data.Attr("maps").Split(',');
			mapsRaw = data.Attr("maps");
			for (int i = 0; i < maps.Length; i++)
			{
				maps[i] = levelSet + "/" + maps[i];
			}
		}
		if (!string.IsNullOrEmpty(data.Attr("requires")))
		{
			requiredBerries = int.Parse(data.Attr("requires"));
		}
	}

	public override void Added(Scene scene)
	{
		base.Added(scene);
		base.AppleEverestCollabBloom.Alpha = 0.5f;
		(int collected, int total) = AppleEverestCollabPresentation.SilverBerries(levelSet, maps);
        if (total == 0) return;
        int num = total - collected;
        int num2 = total;
		if (requiredBerries.HasValue)
		{
			int num3 = num2 - num;
			num = Math.Max(0, requiredBerries.Value - num3);
			num2 = requiredBerries.Value;
		}
		if (num != 0)
		{
			AppleEverestCollabHoloRainbowBerry entity = new AppleEverestCollabHoloRainbowBerry(Position, num2 - num, num2);
			scene.Add(entity);
			AppleEverestFactoryCanary.Successor(this, entity);
			RemoveSelf();
		}
		else if (!AppleEverestCollabModule.Instance.SaveData.CombinedRainbowBerries.Contains(GetCombinedRainbowId(scene as Level)))
		{
			AppleEverestCollabHoloRainbowBerry holoRainbowBerry = new AppleEverestCollabHoloRainbowBerry(Position, num2, num2);
			holoRainbowBerry.Tag = Tags.FrozenUpdate;
			scene.Add(holoRainbowBerry);
			Visible = false;
			Collidable = false;
			base.AppleEverestCollabBloom.Visible = (base.AppleEverestCollabLight.Visible = false);
			HologramForCutscene = holoRainbowBerry;
			CutsceneTotalBerries = num2;
		}
	}

	public string GetCombinedRainbowId(Level level)
	{
		if (maps != null)
		{
			return string.Join(",", maps);
		}
		return level.Session.Area.SID;
	}

	public bool MatchesRainbowBerryTriggerWithSettings(string levelSet, string maps)
	{
		if (!string.IsNullOrEmpty(levelSet) && this.levelSet != levelSet)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(maps) && mapsRaw != maps)
		{
			return false;
		}
		return true;
	}
}
