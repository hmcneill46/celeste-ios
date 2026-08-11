#if TVOS_STAGE10A
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste;

public sealed class TvOSSaveManagerDisplayState
{
    public string Phase { get; init; } = "stopped";
    public string[] Urls { get; init; } = Array.Empty<string>();
    public string AccessCode { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool RestartRequired { get; init; }
    public string PairingStatus { get; init; } = "unavailable";
    public TvOSSaveManagerQrImage PairingQr { get; init; }
}

public sealed class TvOSSaveManagerQrImage
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int ModuleCount { get; init; }
    public int IntegerScale { get; init; }
    public byte[] Rgba { get; init; } = Array.Empty<byte>();
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
    #if TVOS_STAGE13B
    private TvOSSoftReloadDisplayState reload = new();
    #endif
    private bool closing;
    private TvOSSaveManagerQrImage qrSource;
    private Texture2D qrTexture;

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
        #if TVOS_STAGE13B
        reload = TvOSSoftReloadHooks.Status();
        #endif
        UpdateQrTexture();
        if (!closing && (Input.MenuCancel.Pressed || Input.MenuConfirm.Pressed))
        {
            if (state.RestartRequired)
            {
                // A successful external mutation makes the old game graph
                // stale. Back may stop networking, but can never resume it.
                if (Input.MenuCancel.Pressed)
                    TvOSSaveManagerHooks.Stop("ui-reload-required-back-blocked");
                #if TVOS_STAGE13B
                else if (reload.CanRequest)
                    _ = TvOSSoftReloadHooks.RequestReload();
                #else
                TvOSSaveManagerHooks.Stop("ui-restart-required");
                #endif
                return;
            }
            closing = true;
            TvOSSaveManagerHooks.Stop("ui-back");
            RemoveSelf();
            OnClose?.Invoke();
        }
    }

    public override void Removed(Scene scene)
    {
        DisposeQrTexture();
        TvOSSaveManagerHooks.Stop("ui-removed");
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene)
    {
        DisposeQrTexture();
        TvOSSaveManagerHooks.Stop("scene-ended");
        base.SceneEnd(scene);
    }

    public override void Render()
    {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.96f);
        DrawLine("SAVE MANAGER", 90f, 1.45f, Color.White);
        DrawLine("Manage your Celeste saves from another device.", 180f, 0.72f, Color.LightGray);
        DrawLine("Make sure this Apple TV and your phone or computer", 235f, 0.62f, Color.LightGray);
        DrawLine("are connected to the same network.", 280f, 0.62f, Color.LightGray);

        if (state.RestartRequired || state.Phase == "restart-required")
        {
            #if TVOS_STAGE13B
            if (reload.Phase == TvOSSoftReloadPhase.Failure)
            {
                DrawLine("RELOAD FAILED", 450f, 1.05f, Color.White);
                DrawLine("Your changes are safely stored,", 535f, 0.68f, Color.White);
                DrawLine("but Celeste could not reload them.", 600f, 0.68f, Color.White);
                DrawLine("Fully close Celeste from the Apple TV app switcher,", 690f, 0.54f, Color.LightGray);
                DrawLine("then open it again.", 750f, 0.54f, Color.LightGray);
            }
            else if (reload.IsActive)
            {
                DrawLine("RELOADING CELESTE...", 525f, 1.0f, Color.White);
                DrawLine("Please keep Celeste open.", 620f, 0.6f, Color.LightGray);
            }
            else
            {
                DrawLine("CHANGES SAVED", 450f, 1.05f, Color.White);
                DrawLine("Your changes were saved successfully.", 535f, 0.68f, Color.White);
                DrawLine("Press Confirm to reload Celeste", 620f, 0.65f, Color.White);
                DrawLine("and use the new save data.", 680f, 0.65f, Color.White);
                DrawLine("If reloading fails, fully close Celeste from the", 780f, 0.5f, Color.LightGray);
                DrawLine("Apple TV app switcher and reopen it.", 830f, 0.5f, Color.LightGray);
            }
            #else
            DrawLine("RESTART CELESTE", 450f, 1.05f, Color.White);
            DrawLine("Your changes were saved successfully.", 535f, 0.68f, Color.White);
            DrawLine("Close Celeste from the Apple TV app switcher, then open it again.", 700f, 0.53f, Color.LightGray);
            #endif
        }
        else if (state.Phase == "starting")
        {
            DrawLine("Starting Save Manager...", 525f, 0.9f, Color.White);
        }
        else if (state.Phase == "ready")
        {
            if (qrTexture != null && state.PairingStatus == "available")
            {
                float qrX = (float)Math.Floor(285f + (420f - qrTexture.Width) * 0.5f);
                float qrY = (float)Math.Floor(320f + (420f - qrTexture.Height) * 0.5f);
                Draw.SpriteBatch.Draw(qrTexture, new Vector2(qrX, qrY), Color.White);
                DrawLineAt("Scan with your phone camera to connect.", 1300f, 380f, 0.62f, Color.White);
                DrawLineAt("Or open manually:", 1300f, 495f, 0.55f, Color.LightGray);
                DrawManualConnection(1300f, 555f);
            }
            else
            {
                string pairingMessage = state.PairingStatus == "connected"
                    ? "Device connected"
                    : state.PairingStatus == "expired"
                        ? "QR code expired. Use the address and access code below."
                        : "QR pairing is unavailable. Use the address and access code below.";
                DrawLine(pairingMessage, 485f, state.PairingStatus == "connected" ? 0.85f : 0.58f, Color.White);
                DrawManualConnection(960f, 585f);
            }
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

        if (!state.RestartRequired)
            DrawLine("Press Confirm or Back to stop and return to Options", 965f, 0.58f, Color.LightGray);
    }

    private static string GroupCode(string value) => value?.Length == 6 ? value[..3] + " " + value[3..] : "";

    private void DrawManualConnection(float x, float firstY)
    {
        int shown = Math.Min(2, state.Urls.Length);
        for (int i = 0; i < shown; i++) DrawLineAt(state.Urls[i], x, firstY + i * 48f, shown == 1 ? 0.56f : 0.46f, Color.White);
        float codeLabelY = firstY + shown * 48f + 35f;
        DrawLineAt("Access code:", x, codeLabelY, 0.52f, Color.LightGray);
        DrawLineAt(GroupCode(state.AccessCode), x, codeLabelY + 65f, 1.0f, Color.White);
    }

    private void UpdateQrTexture()
    {
        if (ReferenceEquals(qrSource, state.PairingQr)) return;
        DisposeQrTexture();
        qrSource = state.PairingQr;
        if (qrSource == null || qrSource.Width <= 0 || qrSource.Height <= 0 ||
            qrSource.Rgba == null || qrSource.Rgba.Length != qrSource.Width * qrSource.Height * 4) return;
        qrTexture = new Texture2D(Engine.Graphics.GraphicsDevice, qrSource.Width, qrSource.Height, false, SurfaceFormat.Color);
        qrTexture.SetData(qrSource.Rgba);
    }

    private void DisposeQrTexture()
    {
        qrTexture?.Dispose();
        qrTexture = null;
        qrSource = null;
    }

    private static void DrawLine(string text, float y, float scale, Color color) =>
        DrawLineAt(text, 960f, y, scale, color);

    private static void DrawLineAt(string text, float x, float y, float scale, Color color) =>
        ActiveFont.DrawOutline(text ?? "", new Vector2(x, y), new Vector2(0.5f, 0.5f),
            Vector2.One * scale, color, 2f, Color.Black);
}
#endif
