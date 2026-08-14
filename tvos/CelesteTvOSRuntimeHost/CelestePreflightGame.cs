#if CELESTE_RUNTIME
using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CelesteTvOSHost;

internal sealed class CelestePreflightGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? spriteBatch;
    private Texture2D? pixel;
    private SpriteFont? font;
    private bool passed;
    private long frames;

    public CelestePreflightGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1920,
            PreferredBackBufferHeight = 1080,
            IsFullScreen = true,
            PreferMultiSampling = false,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
        graphics.DeviceCreated += (_, _) => RuntimeLog.Info("CelestePreflight graphics device-created event");
    }

    protected override void LoadContent()
    {
        TvOSStage3Bridge.Checkpoint("preflight-start");
        string sessionRoot = Environment.GetEnvironmentVariable("CELESTE_TVOS_SESSION_ROOT")
            ?? throw new InvalidOperationException("Preflight session root is unavailable.");
        string settings = TvOSStage3Bridge.RunSettingsPreflight(sessionRoot);
        RuntimeLog.Info($"settings preflight PASS: {settings}");
        string saveData = TvOSStage3CBridge.RunSaveDataPreflight(sessionRoot);
        RuntimeLog.Info($"SaveData preflight PASS: {saveData}");

        foreach (string manifest in TvOSStage3Bridge.DiscoveryManifestLines())
        {
            RuntimeLog.Info($"reflection discovery PASS: {manifest}");
        }

        Effect effect = Content.Load<Effect>("Effects/Border");
        if (effect.Techniques.Count == 0)
        {
            throw new InvalidOperationException("EffectReader returned an Effect without techniques.");
        }
        font = Content.Load<SpriteFont>("Monocle/MonocleDefault");
        if (font.Characters.Count == 0 || font.LineSpacing <= 0 || font.MeasureString("Celeste").X <= 0)
        {
            throw new InvalidOperationException("SpriteFont reader graph returned an invalid font.");
        }

        RuntimeLog.Info("XNB reader PASS: EffectReader asset=Effects/Border type=Effect");
        foreach (string reader in new[]
                 {
                     "CharReader", "ListReader<T>", "RectangleReader", "SpriteFontReader", "Texture2DReader", "Vector3Reader"
                 })
        {
            RuntimeLog.Info($"XNB reader PASS: {reader} asset=Monocle/MonocleDefault type=SpriteFont");
        }

        spriteBatch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        pixel.SetData(new[] { Color.White });
        RuntimeLog.Info(
            $"preflight graphics PASS: adapter={GraphicsDevice.Adapter.Description}; " +
            $"backbuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x" +
            GraphicsDevice.PresentationParameters.BackBufferHeight
        );
        if (TvOSStage3Bridge.LowLevelFmodCallCount != 0)
        {
            throw new InvalidOperationException("Preflight reached an FMOD low-level guard.");
        }
        passed = true;
        TvOSStage3Bridge.Checkpoint("preflight-complete", "settings=PASS; savedata=PASS; reflection=PASS; xnb-readers=7/7; fmod-low-level=0");
    }

    protected override void Update(GameTime gameTime)
    {
        if (passed && gameTime.TotalGameTime >= TimeSpan.FromSeconds(5))
        {
            RuntimeLog.Info($"CelestePreflight clean exit after {frames} rendered frames");
            Exit();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(passed ? new Color(18, 88, 60) : new Color(100, 25, 25));
        if (spriteBatch is not null && pixel is not null && font is not null)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(pixel, new Rectangle(180, 180, 1560, 720), new Color(8, 24, 28, 220));
            spriteBatch.DrawString(font, "Celeste tvOS Stage 3B Preflight PASS", new Vector2(260, 300), Color.White);
            spriteBatch.DrawString(font, "Settings / Reflection / 7 XNB readers", new Vector2(260, 420), Color.LightGreen);
            spriteBatch.End();
        }
        frames += 1;
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        pixel?.Dispose();
        spriteBatch?.Dispose();
        pixel = null;
        spriteBatch = null;
        font = null;
        RuntimeLog.Info("CelestePreflight resources disposed");
        base.UnloadContent();
    }
}
#endif
