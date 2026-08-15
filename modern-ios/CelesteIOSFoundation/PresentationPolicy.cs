namespace CelesteIOSFoundation;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public static class PresentationPolicy
{
    public const int LogicalWidth = 1280;
    public const int LogicalHeight = 720;

    public static PixelRect AspectFit(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            return default;
        double scale = Math.Min((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight);
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new PixelRect((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
    }

    public static bool IsLandscape(int width, int height) => width > 0 && height > 0 && width > height;
}
