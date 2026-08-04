#if FMOD_DIAGNOSTIC_DEVICE
using System.Runtime.InteropServices;

namespace CelesteTvOSHost;

internal static class FmodDiagnosticNative
{
    internal delegate int BufferReader(IntPtr buffer, int capacity, out int actual);

    internal const uint ManagedHeaderVersion = 0x00011014;
    internal const uint RequiredNativeVersion = 0x00011009;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ParameterDescription
    {
        internal IntPtr Name;
        internal int Index;
        internal float Minimum;
        internal float Maximum;
        internal float DefaultValue;
        internal int Type;
    }

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_Create(out IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_GetVersion(IntPtr system, out uint version);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_Init(IntPtr system, int maxChannels, uint flags, IntPtr extraDriverData);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_Update(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_Close(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_System_Release(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void FMOD_SDL_Register(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_Create(out IntPtr system, uint headerVersion);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_GetLowLevelSystem(IntPtr system, out IntPtr lowLevelSystem);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_Initialize(
        IntPtr system,
        int maxChannels,
        uint studioFlags,
        uint lowLevelFlags,
        IntPtr extraDriverData
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_Update(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_FlushCommands(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_LoadBankFile(
        IntPtr system,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
        uint flags,
        out IntPtr bank
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_GetBankCount(IntPtr system, out int count);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_GetBankList(IntPtr system, IntPtr array, int capacity, out int count);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_GetEvent(
        IntPtr system,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out IntPtr description
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_GetBus(
        IntPtr system,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out IntPtr bus
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_UnloadAll(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_System_Release(IntPtr system);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bank_GetPath(IntPtr bank, IntPtr buffer, int size, out int retrieved);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bank_GetEventCount(IntPtr bank, out int count);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bank_GetEventList(IntPtr bank, IntPtr array, int capacity, out int count);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventDescription_GetPath(
        IntPtr description,
        IntPtr buffer,
        int size,
        out int retrieved
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventDescription_GetLength(IntPtr description, out int milliseconds);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventDescription_GetParameterCount(IntPtr description, out int count);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventDescription_GetParameterByIndex(
        IntPtr description,
        int index,
        out ParameterDescription parameter
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventDescription_CreateInstance(IntPtr description, out IntPtr instance);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventInstance_Start(IntPtr instance);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventInstance_Stop(IntPtr instance, int mode);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventInstance_GetPlaybackState(IntPtr instance, out int state);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventInstance_SetParameterValue(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        float value
    );

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_EventInstance_Release(IntPtr instance);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_GetVolume(IntPtr bus, out float volume, out float finalVolume);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_SetVolume(IntPtr bus, float volume);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_GetMute(IntPtr bus, out int mute);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_SetMute(IntPtr bus, int mute);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_GetPaused(IntPtr bus, out int paused);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int FMOD_Studio_Bus_SetPaused(IntPtr bus, int paused);

    internal static void Check(int result, string operation)
    {
        if (result == 0)
        {
            return;
        }

        throw new InvalidOperationException($"{operation} failed: {ResultName(result)} ({result})");
    }

    internal static string ResultName(int result) => result switch
    {
        0 => "OK",
        20 => "ERR_HEADER_MISMATCH",
        26 => "ERR_INITIALIZATION",
        31 => "ERR_INVALID_PARAM",
        46 => "ERR_NOTREADY",
        50 => "ERR_OUTPUT_FORMAT",
        51 => "ERR_OUTPUT_INIT",
        52 => "ERR_OUTPUT_NODRIVERS",
        54 => "ERR_PLUGIN_MISSING",
        56 => "ERR_PLUGIN_VERSION",
        67 => "ERR_UNINITIALIZED",
        68 => "ERR_UNSUPPORTED",
        69 => "ERR_VERSION",
        74 => "ERR_EVENT_NOTFOUND",
        75 => "ERR_STUDIO_UNINITIALIZED",
        76 => "ERR_STUDIO_NOT_LOADED",
        _ => "FMOD_RESULT"
    };

    internal static string ReadUtf8Path(BufferReader reader)
    {
        const int capacity = 2048;
        IntPtr buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Check(reader(buffer, capacity, out _), "FMOD path lookup");
            return Marshal.PtrToStringUTF8(buffer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static IntPtr[] ReadPointerList(int capacity, BufferReader reader)
    {
        if (capacity <= 0)
        {
            return Array.Empty<IntPtr>();
        }

        IntPtr buffer = Marshal.AllocHGlobal(capacity * IntPtr.Size);
        try
        {
            Check(reader(buffer, capacity, out int actual), "FMOD pointer-list lookup");
            if (actual < 0 || actual > capacity)
            {
                throw new InvalidOperationException($"FMOD returned an invalid list count {actual}/{capacity}.");
            }
            IntPtr[] result = new IntPtr[actual];
            for (int index = 0; index < actual; index += 1)
            {
                result[index] = Marshal.ReadIntPtr(buffer, index * IntPtr.Size);
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
#endif
