#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Security.Cryptography;
using System.Text;
using Celeste;
using CoreGraphics;
using CoreImage;
using Foundation;

namespace CelesteTvOSHost;

// Core Image produces the standards-compliant QR module image. This helper
// then adds the mandatory four-module quiet zone and expands every module by
// an integer factor into an immutable RGBA texture. The Celeste overlay draws
// that texture 1:1, so no bilinear sampling can blur module boundaries.
internal static class Stage15QrCodeGenerator
{
    internal const string CorrectionLevel = "M";
    internal const int QuietZoneModules = 4;
    internal const int MaximumRenderedPixels = 420;

    internal static TvOSSaveManagerQrImage Create(string pairingUrl)
    {
        if (string.IsNullOrWhiteSpace(pairingUrl)) throw new ArgumentException("Pairing URL is required.", nameof(pairingUrl));
        byte[] message = Encoding.UTF8.GetBytes(pairingUrl);
        try
        {
            return CreateFromMessage(message);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    private static TvOSSaveManagerQrImage CreateFromMessage(byte[] message)
    {
        using NSData data = NSData.FromArray(message);
        using CIQRCodeGenerator generator = new()
        {
            Message = data,
            CorrectionLevel = CorrectionLevel
        };
        CIImage image = generator.OutputImage
            ?? throw new InvalidOperationException("Core Image did not produce a QR image.");
        CGRect extent = image.Extent;
        int modules = checked((int)Math.Round(extent.Width));
        if (modules <= 0 || modules != checked((int)Math.Round(extent.Height)) || modules > 177)
            throw new InvalidOperationException("Core Image returned invalid QR module dimensions.");

        using CIContext context = CIContext.Create();
        using CGColorSpace colorSpace = CGColorSpace.CreateDeviceRGB();
        using CGImage rendered = context.CreateCGImage(image, extent, CIFormat.Rgba8, colorSpace, false)
            ?? throw new InvalidOperationException("Core Image could not render the QR module image.");
        if (rendered.Width != modules || rendered.Height != modules || rendered.BitsPerPixel != 32)
            throw new InvalidOperationException("Core Image QR pixel format was not the expected RGBA8 module grid.");
        CGDataProvider provider = rendered.DataProvider
            ?? throw new InvalidOperationException("Core Image QR data provider was unavailable.");
        using NSData sourceData = provider.CopyData()
            ?? throw new InvalidOperationException("Core Image QR data could not be copied.");
        byte[] source = sourceData.ToArray();
        int sourceStride = checked((int)rendered.BytesPerRow);
        if (sourceStride < modules * 4 || source.Length < sourceStride * modules)
            throw new InvalidOperationException("Core Image QR data was truncated.");

        int totalModules = checked(modules + QuietZoneModules * 2);
        int scale = MaximumRenderedPixels / totalModules;
        if (scale < 1) throw new InvalidOperationException("QR content exceeds the bounded render surface.");
        int size = checked(totalModules * scale);
        byte[] rgba = new byte[checked(size * size * 4)];
        for (int pixel = 0; pixel < size * size; pixel++)
        {
            int offset = pixel * 4;
            rgba[offset] = rgba[offset + 1] = rgba[offset + 2] = rgba[offset + 3] = 255;
        }
        for (int y = 0; y < modules; y++)
        {
            for (int x = 0; x < modules; x++)
            {
                bool dark = source[y * sourceStride + x * 4] < 128;
                if (!dark) continue;
                int startX = (x + QuietZoneModules) * scale;
                int startY = (y + QuietZoneModules) * scale;
                for (int dy = 0; dy < scale; dy++)
                {
                    int row = (startY + dy) * size;
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int offset = (row + startX + dx) * 4;
                        rgba[offset] = rgba[offset + 1] = rgba[offset + 2] = 0;
                        rgba[offset + 3] = 255;
                    }
                }
            }
        }
        return new TvOSSaveManagerQrImage
        {
            Width = size,
            Height = size,
            ModuleCount = modules,
            IntegerScale = scale,
            Rgba = rgba
        };
    }
}
#endif
