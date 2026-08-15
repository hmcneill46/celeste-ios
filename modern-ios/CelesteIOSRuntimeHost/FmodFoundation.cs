using System.Runtime.InteropServices;

namespace CelesteIOSRuntimeHost;

internal sealed class FmodFoundation : IDisposable
{
#if IOS_DEVICE_FMOD
    private const uint ExpectedVersion = 0x00011009;
    private IntPtr studio;
    private IntPtr lowLevel;
    private bool suspended;
    private bool disposed;

    internal FmodFoundation()
    {
        Check(Native.FMOD_System_Create(out IntPtr standalone), "low-level create");
        try
        {
            Check(Native.FMOD_System_GetVersion(standalone, out uint version), "low-level version");
            if (version != ExpectedVersion) throw new InvalidOperationException($"Unexpected FMOD version 0x{version:X8}.");
            Check(Native.FMOD_System_Init(standalone, 32, 0, IntPtr.Zero), "low-level init");
            Check(Native.FMOD_System_Update(standalone), "low-level update");
            Check(Native.FMOD_System_Close(standalone), "low-level close");
        }
        finally
        {
            _ = Native.FMOD_System_Release(standalone);
        }
        Check(Native.FMOD_Studio_System_Create(out studio, ExpectedVersion), "Studio create");
        Check(Native.FMOD_Studio_System_GetLowLevelSystem(studio, out lowLevel), "Studio low-level");
        Check(Native.FMOD_Studio_System_Initialize(studio, 64, 0, 0, IntPtr.Zero), "Studio init");
        Check(Native.FMOD_Studio_System_Update(studio), "Studio update");
        RuntimeLog.Info("fmod-foundation version=1.10.09; low-level=initialized; studio=initialized; banks=not-loaded; foundation-smoke=true");
    }

    internal void Update()
    {
        if (!disposed && !suspended && studio != IntPtr.Zero)
            Check(Native.FMOD_Studio_System_Update(studio), "Studio update");
    }

    internal void Suspend()
    {
        if (disposed || suspended || lowLevel == IntPtr.Zero) return;
        Check(Native.FMOD_System_MixerSuspend(lowLevel), "mixer suspend");
        suspended = true;
        RuntimeLog.Info("fmod-foundation lifecycle=suspended");
    }

    internal void Resume()
    {
        if (disposed || !suspended || lowLevel == IntPtr.Zero) return;
        Check(Native.FMOD_System_MixerResume(lowLevel), "mixer resume");
        suspended = false;
        RuntimeLog.Info("fmod-foundation lifecycle=resumed");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        IntPtr ownedStudio = studio;
        IntPtr ownedLowLevel = lowLevel;
        bool wasSuspended = suspended;
        studio = lowLevel = IntPtr.Zero;
        suspended = false;
        if (ownedStudio == IntPtr.Zero) return;
        if (wasSuspended && ownedLowLevel != IntPtr.Zero)
            _ = Native.FMOD_System_MixerResume(ownedLowLevel);
        int release = Native.FMOD_Studio_System_Release(ownedStudio);
        Check(release, "Studio release");
        RuntimeLog.Info("fmod-foundation shutdown=clean");
    }

    private static void Check(int result, string operation)
    {
        if (result != 0) throw new InvalidOperationException($"FMOD {operation} failed with result {result}.");
    }

    private static class Native
    {
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_Create(out IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_GetVersion(IntPtr system, out uint version);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_Init(IntPtr system, int channels, uint flags, IntPtr extra);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_Update(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_Close(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_Release(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_MixerSuspend(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_System_MixerResume(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_Studio_System_Create(out IntPtr system, uint headerVersion);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_Studio_System_GetLowLevelSystem(IntPtr system, out IntPtr lowLevel);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_Studio_System_Initialize(IntPtr system, int channels, uint studioFlags, uint lowLevelFlags, IntPtr extra);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_Studio_System_Update(IntPtr system);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)] internal static extern int FMOD_Studio_System_Release(IntPtr system);
    }
#else
    internal FmodFoundation() => RuntimeLog.Info("fmod-foundation unavailable=expected-simulator-policy");
    internal void Update() { }
    internal void Suspend() { }
    internal void Resume() { }
    public void Dispose() { }
#endif
}
