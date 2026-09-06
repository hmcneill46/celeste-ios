#nullable disable
using System.Linq;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCollabRainbowBerryUnlockTrigger : Trigger
{
	private AppleEverestCollabRainbowBerry berry;

	private readonly string levelSet;

	private readonly string maps;

	public AppleEverestCollabRainbowBerryUnlockTrigger(EntityData data, Vector2 offset)
		: base(data, offset)
	{
		levelSet = data.Attr("levelSet");
		maps = data.Attr("maps");
	}

	public override void Awake(Scene scene)
	{
		base.Awake(scene);
		berry = (from b in base.Scene.Entities.OfType<AppleEverestCollabRainbowBerry>()
			where b.MatchesRainbowBerryTriggerWithSettings(levelSet, maps)
			select b).FirstOrDefault();
	}

	public override void OnEnter(Player player)
	{
		base.OnEnter(player);
		if (berry != null && berry.HologramForCutscene != null && player != null)
		{
			base.Scene.Add(new AppleEverestCollabRainbowBerryUnlockCutscene(berry, berry.HologramForCutscene, berry.CutsceneTotalBerries));
			berry.HologramForCutscene = null;
			AppleEverestCollabModule.Instance.SaveData.CombinedRainbowBerries.Add(berry.GetCombinedRainbowId(base.Scene as Level));
		}
		RemoveSelf();
	}
}
