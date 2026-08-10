#if TVOS_STAGE12B
using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

public enum TvOSQuitPreparationResult
{
    Busy,
    Ready,
    SaveFailed,
    FlushFailed,
    RestartRequired,
    Failed
}

public static class TvOSQuitHooks
{
    public static Func<bool> BeginRequested { get; set; }
    public static Func<bool, bool, TvOSQuitPreparationResult> PrepareRequested { get; set; }
    public static Action<string> CancelRequested { get; set; }

    private static bool modalActive;
    private static int foregroundMainMenuGeneration;

    public static bool Show(Action restoreMainMenuFocus)
    {
        if (modalActive || !(BeginRequested?.Invoke() ?? false)) return false;
        modalActive = true;
        Engine.Scene.Add(new TvOSLeaveCelesteUI(restoreMainMenuFocus, Volatile.Read(ref foregroundMainMenuGeneration)));
        return true;
    }

    public static TvOSQuitPreparationResult Prepare(bool waitedForSave, bool savingResult) =>
        PrepareRequested?.Invoke(waitedForSave, savingResult) ?? TvOSQuitPreparationResult.Failed;

    public static void Cancel(string reason) => CancelRequested?.Invoke(reason);

    public static void RequestForegroundMainMenu() =>
        Interlocked.Increment(ref foregroundMainMenuGeneration);

    internal static int ForegroundMainMenuGeneration => Volatile.Read(ref foregroundMainMenuGeneration);
    internal static void ModalRemoved() => modalActive = false;

    public static void ResetHostCallbacks()
    {
        BeginRequested = null;
        PrepareRequested = null;
        CancelRequested = null;
    }
}

// A normal Celeste entity placed over the existing main menu. The game/FNA
// runtime remains alive, so Back and foreground reactivation need no restart.
public sealed class TvOSLeaveCelesteUI : Entity
{
    private readonly Action restoreMainMenuFocus;
    private readonly int initialForegroundGeneration;
    private TvOSQuitPreparationResult result = TvOSQuitPreparationResult.Busy;
    private bool waitedForSave;
    private bool preparationAttempted;
    private bool closing;

    public TvOSLeaveCelesteUI(Action restoreFocus, int foregroundGeneration)
    {
        restoreMainMenuFocus = restoreFocus;
        initialForegroundGeneration = foregroundGeneration;
        Tag = (int)Tags.PauseUpdate | (int)Tags.HUD | (int)Tags.FrozenUpdate;
        Depth = -1000000;
    }

    public override void Update()
    {
        base.Update();
        if (closing) return;

        if (TvOSQuitHooks.ForegroundMainMenuGeneration != initialForegroundGeneration)
        {
            Close("foreground-main-menu", notifyHost: false);
            return;
        }

        if (!preparationAttempted)
        {
            if (UserIO.Saving)
            {
                waitedForSave = true;
                return;
            }
            else
            {
                result = TvOSQuitHooks.Prepare(waitedForSave, UserIO.SavingResult);
                if (result == TvOSQuitPreparationResult.Busy) return;
                preparationAttempted = true;
            }
        }

        if (result != TvOSQuitPreparationResult.Ready && Input.MenuConfirm.Pressed)
        {
            if (TvOSQuitHooks.BeginRequested?.Invoke() ?? false)
            {
                result = TvOSQuitPreparationResult.Busy;
                waitedForSave = false;
                preparationAttempted = false;
            }
            return;
        }

        if (Input.MenuCancel.Pressed) Close("ui-back", notifyHost: true);
    }

    public override void Removed(Scene scene)
    {
        TvOSQuitHooks.ModalRemoved();
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene)
    {
        if (!closing) TvOSQuitHooks.Cancel("scene-ended");
        TvOSQuitHooks.ModalRemoved();
        base.SceneEnd(scene);
    }

    public override void Render()
    {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.96f);
        if (!preparationAttempted)
        {
            Line("PREPARING TO LEAVE", 190f, 1.35f, Color.White);
            Line(UserIO.Saving ? "Finishing the current save..." : "Checking your saved progress...", 480f, 0.78f, Color.LightGray);
            Line("Back — Return to Celeste", 900f, 0.58f, Color.LightGray);
            return;
        }

        if (result == TvOSQuitPreparationResult.Ready)
        {
            Line("LEAVE CELESTE", 160f, 1.45f, Color.White);
            Line("Your progress has been saved.", 330f, 0.82f, Color.White);
            Line("Press the TV/Home button to return to", 465f, 0.68f, Color.LightGray);
            Line("the Apple TV Home Screen.", 525f, 0.68f, Color.LightGray);
            Line("When you open Celeste again,", 665f, 0.62f, Color.LightGray);
            Line("you'll return to the main menu.", 720f, 0.62f, Color.LightGray);
            Line("Back — Return to Celeste", 920f, 0.58f, Color.LightGray);
            return;
        }

        Line("COULD NOT PREPARE TO LEAVE", 175f, 1.15f, Color.White);
        Line(FailureText(result), 440f, 0.68f, Color.LightGray);
        Line("Your current Celeste session is still open.", 530f, 0.62f, Color.LightGray);
        Line("Confirm — Try Again", 825f, 0.58f, Color.LightGray);
        Line("Back — Return to Celeste", 900f, 0.58f, Color.LightGray);
    }

    private void Close(string reason, bool notifyHost)
    {
        if (closing) return;
        closing = true;
        if (notifyHost) TvOSQuitHooks.Cancel(reason);
        RemoveSelf();
        restoreMainMenuFocus?.Invoke();
    }

    private static string FailureText(TvOSQuitPreparationResult value) => value switch
    {
        TvOSQuitPreparationResult.SaveFailed => "Celeste could not finish saving. Please try again.",
        TvOSQuitPreparationResult.FlushFailed => "Saved progress could not be verified. Please try again.",
        TvOSQuitPreparationResult.RestartRequired => "Celeste must first be fully closed from the app switcher.",
        _ => "Saved progress could not be verified. Please try again."
    };

    private static void Line(string text, float y, float scale, Color color) =>
        ActiveFont.DrawOutline(text, new Vector2(960f, y), new Vector2(0.5f, 0.5f),
            Vector2.One * scale, color, 2f, Color.Black);
}
#endif
