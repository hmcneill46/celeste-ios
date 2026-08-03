#if TVOS_STAGE3B
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste;

// A reflection-free serializer for the exact Celeste 1.4.0.0 Settings graph.
// SaveData deliberately remains on the legacy path until its later storage stage.
public static class TvOSSettingsSerializer
{
    public static byte[] SerializeToBytes(Settings settings)
    {
        using MemoryStream stream = new();
        Serialize(stream, settings);
        return stream.ToArray();
    }

    public static void Serialize(Stream stream, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(settings);

        XmlWriterSettings writerSettings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
            CloseOutput = false,
            OmitXmlDeclaration = false
        };
        using XmlWriter writer = XmlWriter.Create(stream, writerSettings);
        writer.WriteStartDocument();
        writer.WriteStartElement("Settings");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

        Write(writer, "Version", settings.Version);
        Write(writer, "DefaultFileName", settings.DefaultFileName);
        Write(writer, "Fullscreen", settings.Fullscreen);
        Write(writer, "WindowScale", settings.WindowScale);
        Write(writer, "ViewportPadding", settings.ViewportPadding);
        Write(writer, "VSync", settings.VSync);
        Write(writer, "DisableFlashes", settings.DisableFlashes);
        Write(writer, "ScreenShake", settings.ScreenShake switch
        {
            ScreenshakeAmount.Off => "false",
            ScreenshakeAmount.Half => "true",
            _ => settings.ScreenShake.ToString()
        });
        Write(writer, "Rumble", settings.Rumble switch
        {
            RumbleAmount.Off => "false",
            RumbleAmount.On => "true",
            _ => settings.Rumble.ToString()
        });
        Write(writer, "GrabMode", settings.GrabMode);
        Write(writer, "CrouchDashMode", settings.CrouchDashMode);
        Write(writer, "MusicVolume", settings.MusicVolume);
        Write(writer, "SFXVolume", settings.SFXVolume);
        Write(writer, "SpeedrunClock", settings.SpeedrunClock switch
        {
            SpeedrunType.Off => "false",
            SpeedrunType.Chapter => "true",
            _ => settings.SpeedrunClock.ToString()
        });
        Write(writer, "LastSaveFile", settings.LastSaveFile);
        Write(writer, "Language", settings.Language);
        Write(writer, "Pico8OnMainMenu", settings.Pico8OnMainMenu);
        Write(writer, "SetViewportOnce", settings.SetViewportOnce);
        Write(writer, "VariantsUnlocked", settings.VariantsUnlocked);

        WriteBinding(writer, "Left", settings.Left);
        WriteBinding(writer, "Right", settings.Right);
        WriteBinding(writer, "Down", settings.Down);
        WriteBinding(writer, "Up", settings.Up);
        WriteBinding(writer, "MenuLeft", settings.MenuLeft);
        WriteBinding(writer, "MenuRight", settings.MenuRight);
        WriteBinding(writer, "MenuDown", settings.MenuDown);
        WriteBinding(writer, "MenuUp", settings.MenuUp);
        WriteBinding(writer, "Grab", settings.Grab);
        WriteBinding(writer, "Jump", settings.Jump);
        WriteBinding(writer, "Dash", settings.Dash);
        WriteBinding(writer, "Talk", settings.Talk);
        WriteBinding(writer, "Pause", settings.Pause);
        WriteBinding(writer, "Confirm", settings.Confirm);
        WriteBinding(writer, "Cancel", settings.Cancel);
        WriteBinding(writer, "Journal", settings.Journal);
        WriteBinding(writer, "QuickRestart", settings.QuickRestart);
        WriteBinding(writer, "DemoDash", settings.DemoDash);
        WriteBinding(writer, "RightMoveOnly", settings.RightMoveOnly);
        WriteBinding(writer, "LeftMoveOnly", settings.LeftMoveOnly);
        WriteBinding(writer, "UpMoveOnly", settings.UpMoveOnly);
        WriteBinding(writer, "DownMoveOnly", settings.DownMoveOnly);
        WriteBinding(writer, "RightDashOnly", settings.RightDashOnly);
        WriteBinding(writer, "LeftDashOnly", settings.LeftDashOnly);
        WriteBinding(writer, "UpDashOnly", settings.UpDashOnly);
        WriteBinding(writer, "DownDashOnly", settings.DownDashOnly);

        Write(writer, "LaunchWithFMODLiveUpdate", settings.LaunchWithFMODLiveUpdate);
        Write(writer, "LaunchInDebugMode", settings.LaunchInDebugMode);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    public static Settings Deserialize(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            CloseInput = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };
        using XmlReader reader = XmlReader.Create(stream, settings);
        XDocument document;
        try
        {
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Malformed Celeste settings XML.", exception);
        }

        XElement root = document.Root ?? throw new InvalidDataException("Celeste settings XML has no root element.");
        if (root.Name.LocalName != "Settings" || root.Name.NamespaceName.Length != 0)
        {
            throw new InvalidDataException($"Unexpected Celeste settings root '{root.Name}'.");
        }
        RejectNonNamespaceAttributes(root, "Settings");

        Settings result = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement element in root.Elements())
        {
            string name = element.Name.LocalName;
            if (element.Name.NamespaceName.Length != 0 || !seen.Add(name))
            {
                throw new InvalidDataException($"Unexpected or duplicate Celeste settings element '{element.Name}'.");
            }
            RejectNonNamespaceAttributes(element, name);
            switch (name)
            {
                case "Version": result.Version = element.Value; break;
                case "DefaultFileName": result.DefaultFileName = element.Value; break;
                case "Fullscreen": result.Fullscreen = ReadBool(element); break;
                case "WindowScale": result.WindowScale = ReadInt(element); break;
                case "ViewportPadding": result.ViewportPadding = ReadInt(element); break;
                case "VSync": result.VSync = ReadBool(element); break;
                case "DisableFlashes": result.DisableFlashes = ReadBool(element); break;
                case "ScreenShake": result.ScreenShake = ReadScreenshake(element); break;
                case "Rumble": result.Rumble = ReadRumble(element); break;
                case "GrabMode": result.GrabMode = ReadEnum<GrabModes>(element); break;
                case "CrouchDashMode": result.CrouchDashMode = ReadEnum<CrouchDashModes>(element); break;
                case "MusicVolume": result.MusicVolume = ReadInt(element); break;
                case "SFXVolume": result.SFXVolume = ReadInt(element); break;
                case "SpeedrunClock": result.SpeedrunClock = ReadSpeedrun(element); break;
                case "LastSaveFile": result.LastSaveFile = ReadInt(element); break;
                case "Language": result.Language = element.Value; break;
                case "Pico8OnMainMenu": result.Pico8OnMainMenu = ReadBool(element); break;
                case "SetViewportOnce": result.SetViewportOnce = ReadBool(element); break;
                case "VariantsUnlocked": result.VariantsUnlocked = ReadBool(element); break;
                case "Left": result.Left = ReadBinding(element); break;
                case "Right": result.Right = ReadBinding(element); break;
                case "Down": result.Down = ReadBinding(element); break;
                case "Up": result.Up = ReadBinding(element); break;
                case "MenuLeft": result.MenuLeft = ReadBinding(element); break;
                case "MenuRight": result.MenuRight = ReadBinding(element); break;
                case "MenuDown": result.MenuDown = ReadBinding(element); break;
                case "MenuUp": result.MenuUp = ReadBinding(element); break;
                case "Grab": result.Grab = ReadBinding(element); break;
                case "Jump": result.Jump = ReadBinding(element); break;
                case "Dash": result.Dash = ReadBinding(element); break;
                case "Talk": result.Talk = ReadBinding(element); break;
                case "Pause": result.Pause = ReadBinding(element); break;
                case "Confirm": result.Confirm = ReadBinding(element); break;
                case "Cancel": result.Cancel = ReadBinding(element); break;
                case "Journal": result.Journal = ReadBinding(element); break;
                case "QuickRestart": result.QuickRestart = ReadBinding(element); break;
                case "DemoDash": result.DemoDash = ReadBinding(element); break;
                case "RightMoveOnly": result.RightMoveOnly = ReadBinding(element); break;
                case "LeftMoveOnly": result.LeftMoveOnly = ReadBinding(element); break;
                case "UpMoveOnly": result.UpMoveOnly = ReadBinding(element); break;
                case "DownMoveOnly": result.DownMoveOnly = ReadBinding(element); break;
                case "RightDashOnly": result.RightDashOnly = ReadBinding(element); break;
                case "LeftDashOnly": result.LeftDashOnly = ReadBinding(element); break;
                case "UpDashOnly": result.UpDashOnly = ReadBinding(element); break;
                case "DownDashOnly": result.DownDashOnly = ReadBinding(element); break;
                case "LaunchWithFMODLiveUpdate": result.LaunchWithFMODLiveUpdate = ReadBool(element); break;
                case "LaunchInDebugMode": result.LaunchInDebugMode = ReadBool(element); break;
                default: throw new InvalidDataException($"Unsupported Celeste settings element '{name}'.");
            }
        }
        return result;
    }

    private static void WriteBinding(XmlWriter writer, string name, Binding binding)
    {
        writer.WriteStartElement(name);
        writer.WriteStartElement("Keyboard");
        foreach (Keys key in binding.Keyboard)
        {
            Write(writer, "Keys", key);
        }
        writer.WriteEndElement();
        writer.WriteStartElement("Controller");
        foreach (Buttons button in binding.Controller)
        {
            Write(writer, "Buttons", button);
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static Binding ReadBinding(XElement element)
    {
        Binding binding = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement group in element.Elements())
        {
            string name = group.Name.LocalName;
            if (group.Name.NamespaceName.Length != 0 || !seen.Add(name))
            {
                throw new InvalidDataException($"Unexpected or duplicate binding element '{group.Name}'.");
            }
            RejectNonNamespaceAttributes(group, name);
            if (name == "Keyboard")
            {
                foreach (XElement value in group.Elements())
                {
                    RequireElementName(value, "Keys");
                    binding.Keyboard.Add(ReadEnum<Keys>(value));
                }
            }
            else if (name == "Controller")
            {
                foreach (XElement value in group.Elements())
                {
                    RequireElementName(value, "Buttons");
                    binding.Controller.Add(ReadEnum<Buttons>(value));
                }
            }
            else
            {
                throw new InvalidDataException($"Unsupported binding element '{name}'.");
            }
        }
        return binding;
    }

    private static void RequireElementName(XElement element, string expected)
    {
        RejectNonNamespaceAttributes(element, expected);
        if (element.Name.LocalName != expected || element.Name.NamespaceName.Length != 0 || element.HasElements)
        {
            throw new InvalidDataException($"Expected binding value '{expected}', found '{element.Name}'.");
        }
    }

    private static void RejectNonNamespaceAttributes(XElement element, string context)
    {
        foreach (XAttribute attribute in element.Attributes())
        {
            if (!attribute.IsNamespaceDeclaration)
            {
                throw new InvalidDataException($"Unsupported attribute '{attribute.Name}' on '{context}'.");
            }
        }
    }

    private static bool ReadBool(XElement element)
    {
        try { return XmlConvert.ToBoolean(element.Value); }
        catch (FormatException exception) { throw InvalidValue(element, exception); }
    }

    private static int ReadInt(XElement element)
    {
        try { return XmlConvert.ToInt32(element.Value); }
        catch (FormatException exception) { throw InvalidValue(element, exception); }
    }

    private static T ReadEnum<T>(XElement element) where T : struct, Enum
    {
        if (Enum.TryParse(element.Value, ignoreCase: false, out T value) && Enum.IsDefined(value))
        {
            return value;
        }
        throw new InvalidDataException($"Invalid {typeof(T).Name} value '{element.Value}' in '{element.Name.LocalName}'.");
    }

    private static ScreenshakeAmount ReadScreenshake(XElement element) => element.Value switch
    {
        "false" => ScreenshakeAmount.Off,
        "true" => ScreenshakeAmount.Half,
        "On" => ScreenshakeAmount.On,
        _ => throw new InvalidDataException($"Invalid ScreenshakeAmount value '{element.Value}'.")
    };

    private static RumbleAmount ReadRumble(XElement element) => element.Value switch
    {
        "false" => RumbleAmount.Off,
        "Half" => RumbleAmount.Half,
        "true" => RumbleAmount.On,
        _ => throw new InvalidDataException($"Invalid RumbleAmount value '{element.Value}'.")
    };

    private static SpeedrunType ReadSpeedrun(XElement element) => element.Value switch
    {
        "false" => SpeedrunType.Off,
        "true" => SpeedrunType.Chapter,
        "File" => SpeedrunType.File,
        _ => throw new InvalidDataException($"Invalid SpeedrunType value '{element.Value}'.")
    };

    private static InvalidDataException InvalidValue(XElement element, Exception inner) =>
        new($"Invalid value '{element.Value}' in Celeste settings element '{element.Name.LocalName}'.", inner);

    private static void Write(XmlWriter writer, string name, string value) => writer.WriteElementString(name, value ?? "");
    private static void Write(XmlWriter writer, string name, bool value) => writer.WriteElementString(name, XmlConvert.ToString(value));
    private static void Write(XmlWriter writer, string name, int value) => writer.WriteElementString(name, XmlConvert.ToString(value));
    private static void Write<T>(XmlWriter writer, string name, T value) where T : struct, Enum => writer.WriteElementString(name, value.ToString());
}
#endif
