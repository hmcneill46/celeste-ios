#if TVOS_STAGE10A
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

public sealed class TvOSSaveManagerDisplayState
{
    public string Phase { get; init; } = "stopped";
    public string[] Urls { get; init; } = Array.Empty<string>();
    public string AccessCode { get; init; } = "";
    public string Detail { get; init; } = "";
}

public static class TvOSSaveManagerHooks
{
    public static Func<TvOSSaveManagerDisplayState> StartRequested { get; set; }
    public static Func<TvOSSaveManagerDisplayState> StatusRequested { get; set; }
    public static Action<string> StopRequested { get; set; }

    public static TvOSSaveManagerDisplayState Start() =>
        StartRequested?.Invoke() ?? new TvOSSaveManagerDisplayState
        {
            Phase = "failed",
            Detail = "The Save Manager host is unavailable."
        };

    public static TvOSSaveManagerDisplayState Status() =>
        StatusRequested?.Invoke() ?? new TvOSSaveManagerDisplayState { Phase = "stopped" };

    public static void Stop(string reason) => StopRequested?.Invoke(reason);

    public static void Reset()
    {
        StartRequested = null;
        StatusRequested = null;
        StopRequested = null;
    }
}

// This is a normal Celeste entity, not another Oui. It is added only after the
// explicit Options button is pressed and is tagged for paused/HUD updates so
// no gameplay input reaches a scene behind it.
public sealed class TvOSSaveManagerUI : Entity
{
    private TvOSSaveManagerDisplayState state = new();
    private bool closing;

    public Action OnClose { get; set; }

    public TvOSSaveManagerUI()
    {
        Tag = (int)Tags.PauseUpdate | (int)Tags.HUD | (int)Tags.FrozenUpdate;
        Depth = -1000000;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        state = TvOSSaveManagerHooks.Start();
    }

    public override void Update()
    {
        base.Update();
        state = TvOSSaveManagerHooks.Status();
        if (!closing && (Input.MenuCancel.Pressed || Input.MenuConfirm.Pressed))
        {
            closing = true;
            TvOSSaveManagerHooks.Stop("ui-back");
            RemoveSelf();
            OnClose?.Invoke();
        }
    }

    public override void Removed(Scene scene)
    {
        TvOSSaveManagerHooks.Stop("ui-removed");
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene)
    {
        TvOSSaveManagerHooks.Stop("scene-ended");
        base.SceneEnd(scene);
    }

    public override void Render()
    {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.96f);
        DrawLine("SAVE MANAGER", 160f, 1.45f, Color.White);
        DrawLine("Manage your Celeste saves from another device.", 285f, 0.72f, Color.LightGray);
        DrawLine("Make sure this Apple TV and your phone or computer", 350f, 0.62f, Color.LightGray);
        DrawLine("are connected to the same network.", 400f, 0.62f, Color.LightGray);

        if (state.Phase == "starting")
        {
            DrawLine("Starting Save Manager...", 525f, 0.9f, Color.White);
        }
        else if (state.Phase == "ready")
        {
            DrawLine("Open:", 495f, 0.65f, Color.LightGray);
            int shown = Math.Min(2, state.Urls.Length);
            for (int i = 0; i < shown; i++) DrawLine(state.Urls[i], 555f + i * 55f, 0.72f, Color.White);
            DrawLine("Access code:", 690f, 0.65f, Color.LightGray);
            DrawLine(GroupCode(state.AccessCode), 755f, 1.15f, Color.White);
            DrawLine("The server stops automatically when you leave this screen.", 865f, 0.5f, Color.Gray);
        }
        else if (state.Phase == "unavailable")
        {
            DrawLine("No local network connection is available.", 525f, 0.82f, Color.White);
        }
        else if (state.Phase == "failed")
        {
            DrawLine("Save Manager could not start.", 525f, 0.82f, Color.White);
            if (!string.IsNullOrWhiteSpace(state.Detail)) DrawLine(state.Detail, 590f, 0.55f, Color.LightGray);
        }
        else
        {
            DrawLine("Save Manager has stopped.", 525f, 0.82f, Color.White);
        }

        DrawLine("Press Confirm or Back to stop and return to Options", 965f, 0.58f, Color.LightGray);
    }

    private static string GroupCode(string value) => value?.Length == 6 ? value[..3] + " " + value[3..] : "";

    private static void DrawLine(string text, float y, float scale, Color color) =>
        ActiveFont.DrawOutline(text ?? "", new Vector2(960f, y), new Vector2(0.5f, 0.5f),
            Vector2.One * scale, color, 2f, Color.Black);
}
#endif
