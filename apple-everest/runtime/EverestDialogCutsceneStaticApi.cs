using System.Collections;
using Monocle;

namespace Celeste.Mod.Entities;

// Exact pinned Everest surface used by the reviewed Strawberry Jam helpers.
// This is a normal managed type in the closed closure; it does not introduce
// Everest's desktop loader or any runtime patching mechanism.
[Tracked]
public class DialogCutscene : CutsceneEntity
{
	private readonly Player player;
	private readonly string dialogID;
	private readonly bool endLevel;

	public DialogCutscene(string dialogID, Player player, bool endLevel)
	{
		this.dialogID = dialogID;
		this.player = player;
		this.endLevel = endLevel;
	}

	public override void OnBegin(Level level)
	{
		if (endLevel)
		{
			level.RegisterAreaComplete();
		}
		Add(new Coroutine(Cutscene(level)));
	}

	private IEnumerator Cutscene(Level level)
	{
		player.StateMachine.State = 11;
		player.StateMachine.Locked = true;
		player.ForceCameraUpdate = true;
		yield return Textbox.Say(dialogID);
		EndCutscene(level, true);
	}

	public override void OnEnd(Level level)
	{
		player.StateMachine.Locked = false;
		player.StateMachine.State = 0;
		player.ForceCameraUpdate = false;
		if (WasSkipped)
		{
			level.Camera.Position = player.CameraTarget;
		}
		if (endLevel)
		{
			level.CompleteArea();
			player.StateMachine.State = Player.StDummy;
			RemoveSelf();
		}
	}

	public static bool IsInProgress(string dialogID)
	{
		foreach (DialogCutscene cutscene in Engine.Scene.Tracker.GetEntities<DialogCutscene>())
		{
			if (cutscene.dialogID == dialogID)
			{
				return true;
			}
		}
		return false;
	}
}
