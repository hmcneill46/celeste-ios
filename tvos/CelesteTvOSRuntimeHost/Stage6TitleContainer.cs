#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2021 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See FNA/LICENSE for details.
 */
#endregion

using System;
using System.IO;
using Foundation;

namespace Microsoft.Xna.Framework;

public static class TitleContainer
{
    public static Stream OpenStream(string name)
    {
        string safeName = MonoGame.Utilities.FileHelpers.NormalizeFilePathSeparators(name);
        string resolved = Path.IsPathRooted(safeName)
            ? safeName
            : Path.Combine(TitleLocation.Path, safeName);
        string fullPath = Path.GetFullPath(resolved);
        string bundleRoot = Path.GetFullPath(NSBundle.MainBundle.BundlePath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(bundleRoot, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("FNA title content must remain inside the application bundle.");

        using NSData data = NSData.FromFile(fullPath)
            ?? throw new FileNotFoundException("FNA could not read bundled title content.", safeName);
        return new MemoryStream(data.ToArray(), writable: false);
    }

    internal static IntPtr ReadToPointer(string name, out IntPtr size)
    {
        string safeName = MonoGame.Utilities.FileHelpers.NormalizeFilePathSeparators(name);
        if (Path.IsPathRooted(safeName))
            return FNAPlatform.ReadFileToPointer(safeName, out size);
        return FNAPlatform.ReadFileToPointer(Path.Combine(TitleLocation.Path, safeName), out size);
    }
}
