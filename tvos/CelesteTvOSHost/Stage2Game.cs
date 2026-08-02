using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CelesteTvOSHost;

internal sealed class Stage2Game : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? spriteBatch;
    private Texture2D? pixel;
    private long frameCount;
    private TimeSpan nextHeartbeat = TimeSpan.Zero;

    public Stage2Game()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1920,
            PreferredBackBufferHeight = 1080,
            IsFullScreen = true,
            PreferMultiSampling = false,
            SynchronizeWithVerticalRetrace = true
        };

        graphics.DeviceCreated += (_, _) => Stage2Log.Info("graphics device-created event");
        graphics.DeviceResetting += (_, _) => Stage2Log.Info("graphics device-resetting event");
        graphics.DeviceReset += (_, _) => Stage2Log.Info("graphics device-reset event");
        Activated += (_, _) => Stage2Log.Info("FNA game activated");
        Deactivated += (_, _) => Stage2Log.Info("FNA game deactivated");
        Exiting += (_, _) => Stage2Log.Info("FNA game exiting");

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        pixel.SetData(new[] { Color.White });

        GraphicsAdapter adapter = GraphicsDevice.Adapter;
        PresentationParameters presentation = GraphicsDevice.PresentationParameters;
        Stage2Log.Info(
            $"graphics initialized: adapter={adapter.Description}; device={adapter.DeviceName}; " +
            $"backbuffer={presentation.BackBufferWidth}x{presentation.BackBufferHeight}; " +
            $"format={presentation.BackBufferFormat}; profile={GraphicsDevice.GraphicsProfile}"
        );
        Stage2Log.Info("renderer selected: Metal (forced through FNA3D_FORCE_DRIVER and confirmed by FNA3D log)");
        nextHeartbeat = TimeSpan.FromSeconds(5);
    }

    protected override void UnloadContent()
    {
        pixel?.Dispose();
        spriteBatch?.Dispose();
        pixel = null;
        spriteBatch = null;
        Stage2Log.Info("runtime-generated graphics resources disposed");
    }

    protected override void Draw(GameTime gameTime)
    {
        double seconds = gameTime.TotalGameTime.TotalSeconds;
        byte red = (byte)(35 + (Math.Sin(seconds * 0.71) + 1.0) * 55.0);
        byte green = (byte)(45 + (Math.Sin(seconds * 0.53 + 2.0) + 1.0) * 45.0);
        byte blue = (byte)(80 + (Math.Sin(seconds * 0.37 + 4.0) + 1.0) * 70.0);
        GraphicsDevice.Clear(new Color(red, green, blue));

        if (spriteBatch is not null && pixel is not null)
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            float cycle = (float)((seconds % 6.0) / 6.0);
            int x = (int)(cycle * (width + 280)) - 140;
            int y = (int)(height * (0.5 + 0.22 * Math.Sin(seconds * 1.17))) - 70;
            int pulse = 110 + (int)(25 * (Math.Sin(seconds * 2.0) + 1.0));

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(pixel, new Rectangle(x, y, pulse, pulse), new Color(255, 229, 92));
            spriteBatch.Draw(
                pixel,
                new Rectangle(width / 2 - 320, height / 2 - 8, 640, 16),
                new Color(255, 255, 255, 150)
            );
            spriteBatch.End();
        }

        frameCount += 1;
        if (gameTime.TotalGameTime >= nextHeartbeat)
        {
            Stage2Log.Info(
                $"frame-heartbeat frame={frameCount}; elapsed={gameTime.TotalGameTime.TotalSeconds:F1}s; " +
                $"active={IsActive}; backbuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x" +
                $"{GraphicsDevice.PresentationParameters.BackBufferHeight}"
            );
            nextHeartbeat += TimeSpan.FromSeconds(5);
        }

        base.Draw(gameTime);
    }
}
