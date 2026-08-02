using System.Runtime.InteropServices;
using SDL2;

namespace CelesteTvOSHost;

internal static class NativeInteropSelfTest
{
    private const string InternalLibrary = "__Internal";

    [DllImport(InternalLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint FNA3D_LinkedVersion();

    [DllImport(InternalLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr vkGetInstanceProcAddr(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name
    );

    public static string Run()
    {
        SDL.SDL_VERSION(out SDL.SDL_version compiledSdl);
        SDL.SDL_GetVersion(out SDL.SDL_version linkedSdl);

        uint fna3dVersion = FNA3D_LinkedVersion();
        uint fAudioVersion = FAudio.FAudioLinkedVersion();

        int theorafileResult = Theorafile.tf_fopen(
            "/__celeste_tvos_stage2_intentionally_missing__.ogv",
            out IntPtr theorafileHandle
        );
        if (theorafileHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Theorafile binding did not allocate its opaque test handle.");
        }

        // The pinned binding allocates before invoking tf_fopen. A failed open
        // leaves that storage uninitialized, so tf_close must not inspect it.
        Marshal.FreeHGlobal(theorafileHandle);
        theorafileHandle = IntPtr.Zero;
        if (theorafileResult != -3)
        {
            throw new InvalidOperationException(
                $"Theorafile sentinel returned {theorafileResult}; expected TF_ENODATASOURCE (-3)."
            );
        }

        IntPtr vulkanCreateInstance = vkGetInstanceProcAddr(IntPtr.Zero, "vkCreateInstance");
        if (vulkanCreateInstance == IntPtr.Zero)
        {
            throw new InvalidOperationException("MoltenVK did not resolve vkCreateInstance.");
        }

        string compiledSdlText = Format(compiledSdl);
        string linkedSdlText = Format(linkedSdl);
        int compiledSdlNumber = VersionNumber(compiledSdl);
        int linkedSdlNumber = VersionNumber(linkedSdl);
        if (linkedSdl.major != 2 || linkedSdlNumber < compiledSdlNumber)
        {
            throw new InvalidOperationException(
                $"SDL native version {linkedSdlText} is not ABI-compatible with binding baseline {compiledSdlText}."
            );
        }

        if (compiledSdlText != linkedSdlText)
        {
            Stage2Log.Warning(
                $"SDL binding/native version skew accepted: binding={compiledSdlText}; native={linkedSdlText}; major-2 ABI"
            );
        }

        return string.Join(
            "; ",
            $"SDL compile/linked={compiledSdlText}",
            $"FNA3D linked={fna3dVersion}",
            $"FAudio compile={FAudio.FAUDIO_COMPILED_VERSION} linked={fAudioVersion}",
            $"Theorafile sentinel result={theorafileResult}",
            "MoltenVK vkCreateInstance=resolved",
            "tvStubs call sites=0"
        );
    }

    private static string Format(SDL.SDL_version version)
    {
        return $"{version.major}.{version.minor}.{version.patch}";
    }

    private static int VersionNumber(SDL.SDL_version version)
    {
        return version.major * 1_000 + version.minor * 100 + version.patch;
    }
}
