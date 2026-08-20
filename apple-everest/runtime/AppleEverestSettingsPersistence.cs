using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Foundation;

namespace Celeste.Mod;

internal static class AppleEverestSettingsPersistence
{
    private const string DefaultsKey = "CelesteAppleEverest.Settings.v1";
    private const string RelativeFile = "AppleEverest/ModuleSettings.v1";
    private static AppleEverestSettingDescriptor[] descriptors = Array.Empty<AppleEverestSettingDescriptor>();

    internal static void LoadAndApply(AppleEverestSettingDescriptor[] values)
    {
        descriptors = values;
        string payload = Read();
        if (string.IsNullOrEmpty(payload))
        {
            AppleEverestStaticRuntime.Log($"module-settings=defaults count={descriptors.Length}");
            return;
        }
        if (!AppleEverestSettingsCodec.TryDecode(payload, out AppleEverestSettingRecord[] records))
        {
            AppleEverestStaticRuntime.Log("module-settings=corrupt action=defaults");
            return;
        }
        Dictionary<string, AppleEverestSettingDescriptor> known = descriptors.ToDictionary(Key, StringComparer.Ordinal);
        List<(AppleEverestSettingDescriptor Descriptor, int Value)> applicable =
            new List<(AppleEverestSettingDescriptor Descriptor, int Value)>();
        foreach (AppleEverestSettingRecord record in records)
        {
            if (!known.TryGetValue(record.Module + "\0" + record.Property, out AppleEverestSettingDescriptor descriptor)) continue;
            if (!descriptor.Accepts(record.Value))
            {
                AppleEverestStaticRuntime.Log("module-settings=invalid-value action=defaults");
                return;
            }
            applicable.Add((descriptor, record.Value));
        }
        foreach ((AppleEverestSettingDescriptor descriptor, int value) in applicable) descriptor.Set(value);
        AppleEverestStaticRuntime.Log($"module-settings=restored count={applicable.Count}");
    }

    internal static void Set(AppleEverestSettingDescriptor descriptor, int value)
    {
        if (!descriptor.Accepts(value)) return;
        if (descriptor.Get() == value) return;
        descriptor.Set(value);
        Save();
        AppleEverestStaticRuntime.ShowStatus($"{descriptor.Module}\n{descriptor.Label}: {Display(descriptor, value)}");
        AppleEverestStaticRuntime.Log($"module-setting=changed module={descriptor.Module} property={descriptor.Property}");
    }

    internal static string Display(AppleEverestSettingDescriptor descriptor, int value)
    {
        if (descriptor.Kind == AppleEverestSettingKind.Boolean)
            return value == 0 ? global::Celeste.Dialog.Clean("options_off") : global::Celeste.Dialog.Clean("options_on");
        if (descriptor.Kind == AppleEverestSettingKind.Enum)
        {
            int index = Array.IndexOf(descriptor.EnumValues, value);
            return index >= 0 ? descriptor.EnumNames[index] : value.ToString();
        }
        return value.ToString();
    }

    private static void Save()
    {
        try
        {
            string payload = AppleEverestSettingsCodec.Encode(descriptors.Select(descriptor =>
                new AppleEverestSettingRecord(descriptor.Module, descriptor.Property, descriptor.Get())));
            Write(payload);
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"module-settings=write-failed category={exception.GetType().Name}");
        }
    }

    private static string Key(AppleEverestSettingDescriptor descriptor) => descriptor.Module + "\0" + descriptor.Property;

    private static string Read()
    {
        try
        {
#if TVOS
            return NSUserDefaults.StandardUserDefaults.StringForKey(DefaultsKey);
#else
            string path = StoragePath(create: false);
            using NSData data = NSData.FromFile(path);
            return data == null || data.Length > AppleEverestSettingsCodec.MaximumBytes ? null : Encoding.UTF8.GetString(data.ToArray());
#endif
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"module-settings=read-failed category={exception.GetType().Name}");
            return null;
        }
    }

    private static void Write(string payload)
    {
#if TVOS
        NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
        defaults.SetString(payload, DefaultsKey);
        defaults.Synchronize();
#else
        string path = StoragePath(create: true);
        using NSData data = NSData.FromArray(Encoding.UTF8.GetBytes(payload));
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, true)) throw new IOException("atomic module settings write failed");
#endif
    }

    private static string StoragePath(bool create)
    {
        string root = Environment.GetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            NSUrl[] urls = NSFileManager.DefaultManager.GetUrls(
                NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
            root = urls.FirstOrDefault()?.Path;
        }
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("Application Support is unavailable");
        string path = Path.Combine(root, RelativeFile);
        if (create)
        {
            string directory = Path.GetDirectoryName(path);
            NSFileManager manager = NSFileManager.DefaultManager;
            NSError error = null;
            if (!manager.FileExists(directory) && !manager.CreateDirectory(directory, true, (NSFileAttributes)null, out error))
            {
                string detail = error?.LocalizedDescription ?? "unknown";
                error?.Dispose();
                throw new IOException("module settings directory creation failed: " + detail);
            }
            error?.Dispose();
        }
        return path;
    }
}
