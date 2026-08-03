#if TVOS_AUDIO_DISABLED
using System;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

// Stage 3B's deliberately removable high-level audio boundary. No method in
// this implementation calls FMOD. The game-visible state retained here is the
// minimum needed for ordinary music/ambience transitions to remain coherent.
public static class Audio
{
    public static class Banks
    {
        public static Bank Master;
        public static Bank Music;
        public static Bank Sfxs;
        public static Bank UI;
        public static Bank DlcMusic;
        public static Bank DlcSfxs;

        public static Bank Load(string name, bool loadStrings)
        {
            TvOSStage3Bridge.RecordNoAudioCall("bank-load");
            return null;
        }
    }

    public static Dictionary<string, EventDescription> cachedEventDescriptions = new();
    public static string CurrentMusic = "";

    private static string currentAmbience = "";
    private static string currentAltMusic = "";
    private static float musicVolume = 1f;
    private static float sfxVolume = 1f;
    private static bool pauseMusic;
    private static bool pauseGameplaySfx;
    private static bool pauseUiSfx;
    private static bool musicUnderwater;

    public static EventInstance CurrentMusicEventInstance => null;
    public static EventInstance CurrentAmbienceEventInstance => null;

    public static float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = value;
            TvOSStage3Bridge.RecordNoAudioCall("volume");
        }
    }

    public static float SfxVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = value;
            TvOSStage3Bridge.RecordNoAudioCall("volume");
        }
    }

    public static bool PauseMusic
    {
        get => pauseMusic;
        set
        {
            pauseMusic = value;
            TvOSStage3Bridge.RecordNoAudioCall("pause");
        }
    }

    public static bool PauseGameplaySfx
    {
        get => pauseGameplaySfx;
        set
        {
            pauseGameplaySfx = value;
            TvOSStage3Bridge.RecordNoAudioCall("pause");
        }
    }

    public static bool PauseUISfx
    {
        get => pauseUiSfx;
        set
        {
            pauseUiSfx = value;
            TvOSStage3Bridge.RecordNoAudioCall("pause");
        }
    }

    public static bool MusicUnderwater
    {
        get => musicUnderwater;
        set
        {
            musicUnderwater = value;
            TvOSStage3Bridge.RecordNoAudioCall("snapshot");
        }
    }

    public static void Init()
    {
        TvOSStage3Bridge.Checkpoint("no-audio-initialized");
        TvOSStage3Bridge.RecordNoAudioCall("initialize");
    }

    public static void Update()
    {
        TvOSStage3Bridge.RecordNoAudioUpdate();
    }

    public static void Unload()
    {
        cachedEventDescriptions.Clear();
        TvOSStage3Bridge.RecordNoAudioCall("unload");
    }

    public static void SetListenerPosition(Vector3 forward, Vector3 up, Vector3 position)
    {
        TvOSStage3Bridge.RecordNoAudioCall("listener");
    }

    public static void SetCamera(Camera camera)
    {
        TvOSStage3Bridge.RecordNoAudioCall("listener");
    }

    internal static void CheckFmod(RESULT result)
    {
        if (result != RESULT.OK)
        {
            throw new InvalidOperationException($"Unexpected FMOD result reached the Stage 3B no-audio boundary: {result}");
        }
    }

    public static EventInstance Play(string path) => InertEvent("play");
    public static EventInstance Play(string path, string param, float value) => InertEvent("play");
    public static EventInstance Play(string path, Vector2 position) => InertEvent("play");
    public static EventInstance Play(string path, Vector2 position, string param, float value) => InertEvent("play");
    public static EventInstance Play(string path, Vector2 position, string param, float value, string param2, float value2) => InertEvent("play");
    public static EventInstance Loop(string path) => InertEvent("loop");
    public static EventInstance Loop(string path, string param, float value) => InertEvent("loop");
    public static EventInstance Loop(string path, Vector2 position) => InertEvent("loop");
    public static EventInstance Loop(string path, Vector2 position, string param, float value) => InertEvent("loop");

    public static void Pause(EventInstance instance) => TvOSStage3Bridge.RecordNoAudioCall("event-control");
    public static void Resume(EventInstance instance) => TvOSStage3Bridge.RecordNoAudioCall("event-control");
    public static void Position(EventInstance instance, Vector2 position) => TvOSStage3Bridge.RecordNoAudioCall("event-control");
    public static void SetParameter(EventInstance instance, string param, float value) => TvOSStage3Bridge.RecordNoAudioCall("parameter");
    public static void Stop(EventInstance instance, bool allowFadeOut = true) => TvOSStage3Bridge.RecordNoAudioCall("event-control");
    public static EventInstance CreateInstance(string path, Vector2? position = null) => InertEvent("create-instance");

    public static EventDescription GetEventDescription(string path)
    {
        TvOSStage3Bridge.RecordNoAudioCall("event-description");
        return null;
    }

    public static void ReleaseUnusedDescriptions()
    {
        cachedEventDescriptions.Clear();
        TvOSStage3Bridge.RecordNoAudioCall("event-description");
    }

    public static string GetEventName(EventInstance instance) => "";
    public static bool IsPlaying(EventInstance instance) => false;

    public static bool BusPaused(string path, bool? pause = null)
    {
        if (pause.HasValue)
        {
            pauseMusic = pause.Value;
        }
        TvOSStage3Bridge.RecordNoAudioCall("bus");
        return pauseMusic;
    }

    public static bool BusMuted(string path, bool? mute)
    {
        TvOSStage3Bridge.RecordNoAudioCall("bus");
        return mute ?? false;
    }

    public static void BusStopAll(string path, bool immediate = false) => TvOSStage3Bridge.RecordNoAudioCall("bus");

    public static float VCAVolume(string path, float? volume = null)
    {
        if (volume.HasValue)
        {
            if (path.Contains("music", StringComparison.OrdinalIgnoreCase))
            {
                musicVolume = volume.Value;
            }
            else
            {
                sfxVolume = volume.Value;
            }
        }
        TvOSStage3Bridge.RecordNoAudioCall("volume");
        return path.Contains("music", StringComparison.OrdinalIgnoreCase) ? musicVolume : sfxVolume;
    }

    public static EventInstance CreateSnapshot(string name, bool start = true) => InertEvent("snapshot");
    public static void ResumeSnapshot(EventInstance snapshot) => TvOSStage3Bridge.RecordNoAudioCall("snapshot");
    public static bool IsSnapshotRunning(EventInstance snapshot) => false;
    public static void EndSnapshot(EventInstance snapshot) => TvOSStage3Bridge.RecordNoAudioCall("snapshot");
    public static void ReleaseSnapshot(EventInstance snapshot) => TvOSStage3Bridge.RecordNoAudioCall("snapshot");

    public static bool SetMusic(string path, bool startPlaying = true, bool allowFadeOut = true)
    {
        string next = string.IsNullOrEmpty(path) || path == "null" ? "" : path;
        bool changed = !CurrentMusic.Equals(next, StringComparison.OrdinalIgnoreCase);
        CurrentMusic = next;
        TvOSStage3Bridge.RecordNoAudioCall("music-transition");
        return changed;
    }

    public static bool SetAmbience(string path, bool startPlaying = true)
    {
        string next = string.IsNullOrEmpty(path) || path == "null" ? "" : path;
        bool changed = !currentAmbience.Equals(next, StringComparison.OrdinalIgnoreCase);
        currentAmbience = next;
        TvOSStage3Bridge.RecordNoAudioCall("ambience-transition");
        return changed;
    }

    public static void SetMusicParam(string path, float value) => TvOSStage3Bridge.RecordNoAudioCall("parameter");

    public static void SetAltMusic(string path)
    {
        currentAltMusic = path ?? "";
        TvOSStage3Bridge.RecordNoAudioCall("music-transition");
    }

    private static EventInstance InertEvent(string category)
    {
        TvOSStage3Bridge.RecordNoAudioCall(category);
        return null;
    }
}
#endif
