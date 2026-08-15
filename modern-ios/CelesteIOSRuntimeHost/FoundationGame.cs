using CelesteIOSFoundation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CelesteIOSRuntimeHost;

internal sealed class FoundationGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly LifecyclePolicy lifecycle = new();
    private SceneMetricsCoordinator? sceneMetrics;
    private FmodFoundation? fmod;
    private SpriteBatch? spriteBatch;
    private Texture2D? pixel;
    private RenderTarget2D? logicalTarget;
    private AtomicFileStore? storage;
    private long frameCount;
    private TimeSpan nextHeartbeat = TimeSpan.FromSeconds(3);
    private Vector2 markerPosition = new(560, 300);

    internal FoundationGame()
    {
        if (!lifecycle.Connect())
            throw new InvalidOperationException("The modern iOS foundation permits exactly one FNA runtime per process.");

        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = PresentationPolicy.LogicalWidth,
            PreferredBackBufferHeight = PresentationPolicy.LogicalHeight,
            IsFullScreen = true,
            PreferMultiSampling = false,
            SynchronizeWithVerticalRetrace = true,
        };
        graphics.DeviceCreated += (_, _) => RuntimeLog.Info("graphics-event=device-created; runtime-count=1");
        graphics.DeviceResetting += (_, _) => RuntimeLog.Info("graphics-event=device-resetting");
        graphics.DeviceReset += (_, _) => RuntimeLog.Info("graphics-event=device-reset");
        Activated += HandleActivated;
        Deactivated += HandleDeactivated;
        Exiting += (_, _) => RuntimeLog.Info("game-event=exiting");
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
    }

    protected override void Initialize()
    {
        sceneMetrics = new SceneMetricsCoordinator(Window);
        IOSSceneMetrics metrics = sceneMetrics.Capture("initialize");
        if (metrics.DrawableWidth <= 0 || metrics.DrawableHeight <= 0)
            throw new InvalidOperationException("The iOS Metal drawable has invalid dimensions.");

        PresentationParameters presentation = GraphicsDevice.PresentationParameters;
        if (presentation.BackBufferWidth != metrics.DrawableWidth ||
            presentation.BackBufferHeight != metrics.DrawableHeight)
        {
            graphics.PreferredBackBufferWidth = metrics.DrawableWidth;
            graphics.PreferredBackBufferHeight = metrics.DrawableHeight;
            graphics.ApplyChanges();
        }

        storage = ApplicationSupportStorage.Create();
        ControllerFoundation.LogInventory();
        RuntimeLog.Info(
            $"runtime-foundation scene-count=1; fna-game-count={lifecycle.RuntimeStartCount}; " +
            "foundation-only=true; touch-controls=not-implemented; celeste=not-included");
        base.Initialize();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        pixel.SetData([Color.White]);
        logicalTarget = new RenderTarget2D(
            GraphicsDevice,
            PresentationPolicy.LogicalWidth,
            PresentationPolicy.LogicalHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.DiscardContents);
        fmod = new FmodFoundation();

        PresentationParameters presentation = GraphicsDevice.PresentationParameters;
        PixelRect fitted = PresentationPolicy.AspectFit(
            PresentationPolicy.LogicalWidth,
            PresentationPolicy.LogicalHeight,
            presentation.BackBufferWidth,
            presentation.BackBufferHeight);
        RuntimeLog.Info(
            $"first-draw-ready renderer=direct-FNA3D-Metal; adapter={GraphicsDevice.Adapter.Description}; " +
            $"backbuffer={presentation.BackBufferWidth}x{presentation.BackBufferHeight}; " +
            $"logical={PresentationPolicy.LogicalWidth}x{PresentationPolicy.LogicalHeight}; " +
            $"aspect-fit={fitted.X},{fitted.Y},{fitted.Width},{fitted.Height}; profile={GraphicsDevice.GraphicsProfile}");
    }

    protected override void Update(GameTime gameTime)
    {
        (float x, float y) = ControllerFoundation.ReadMovement();
        markerPosition += new Vector2(x, y) * (float)gameTime.ElapsedGameTime.TotalSeconds * 260f;
        markerPosition.X = Math.Clamp(markerPosition.X, 0f, PresentationPolicy.LogicalWidth - 120f);
        markerPosition.Y = Math.Clamp(markerPosition.Y, 0f, PresentationPolicy.LogicalHeight - 120f);
        fmod?.Update();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (spriteBatch is null || pixel is null || logicalTarget is null)
            return;

        double seconds = gameTime.TotalGameTime.TotalSeconds;
        GraphicsDevice.SetRenderTarget(logicalTarget);
        GraphicsDevice.Clear(new Color(
            (byte)(32 + 42 * (1 + Math.Sin(seconds * 0.55))),
            (byte)(42 + 34 * (1 + Math.Sin(seconds * 0.43 + 2))),
            (byte)(86 + 48 * (1 + Math.Sin(seconds * 0.37 + 4)))));
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        spriteBatch.Draw(pixel, new Rectangle((int)markerPosition.X, (int)markerPosition.Y, 120, 120), new Color(255, 229, 92));
        int pulse = 14 + (int)(8 * (1 + Math.Sin(seconds * 2.0)));
        spriteBatch.Draw(pixel, new Rectangle(220, 350 - pulse / 2, 840, pulse), new Color(255, 255, 255, 175));
        spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        PresentationParameters presentation = GraphicsDevice.PresentationParameters;
        PixelRect fitted = PresentationPolicy.AspectFit(
            PresentationPolicy.LogicalWidth,
            PresentationPolicy.LogicalHeight,
            presentation.BackBufferWidth,
            presentation.BackBufferHeight);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        spriteBatch.Draw(logicalTarget, new Rectangle(fitted.X, fitted.Y, fitted.Width, fitted.Height), Color.White);
        spriteBatch.End();

        frameCount++;
        if (gameTime.TotalGameTime >= nextHeartbeat)
        {
            RuntimeLog.Info(
                $"frame-heartbeat frame={frameCount}; elapsed={gameTime.TotalGameTime.TotalSeconds:F1}s; " +
                $"active={IsActive}; runtime-count={lifecycle.RuntimeStartCount}; " +
                $"backbuffer={presentation.BackBufferWidth}x{presentation.BackBufferHeight}");
            nextHeartbeat += TimeSpan.FromSeconds(30);
        }
        base.Draw(gameTime);
    }

    private void HandleActivated(object? sender, EventArgs args)
    {
        lifecycle.BecomeActive();
        sceneMetrics?.Capture("activated");
        fmod?.Resume();
        RuntimeLog.Info("lifecycle=active; existing-runtime-resumed=true");
    }

    private void HandleDeactivated(object? sender, EventArgs args)
    {
        lifecycle.ResignActive();
        fmod?.Suspend();
        RuntimeLog.Info("lifecycle=inactive; runtime-disposed=false");
    }

    protected override void UnloadContent()
    {
        fmod?.Dispose();
        logicalTarget?.Dispose();
        pixel?.Dispose();
        spriteBatch?.Dispose();
        sceneMetrics?.Dispose();
        fmod = null;
        logicalTarget = null;
        pixel = null;
        spriteBatch = null;
        sceneMetrics = null;
        storage = null;
        base.UnloadContent();
    }
}
