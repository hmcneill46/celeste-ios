using System;
using System.Collections.Generic;
using System.IO;
using CelesteIOSFoundation;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

public static class IOSDataFiles
{
    public delegate void ChoicePresenter(
        string title,
        string message,
        IReadOnlyList<string> choices,
        Action<int?> completed);

    private const int MaximumImportBytes = CelesteFileDurabilityStore.MaximumLogicalFileBytes;

    public static bool Available => IOSFilePortabilityBridge.IsAvailable;
    public static bool MutationSafe => Engine.Scene is not Level && !UserIO.Saving;

    public static bool Exists(string logicalName) => IOSStorageHooks.LogicalFileExists(logicalName);
    public static bool HasPrevious(string logicalName) => IOSStorageHooks.HasPreviousGood(logicalName);

    public static void Export(string logicalName, bool share, Action<string> completed)
    {
        byte[] data = IOSStorageHooks.ReadLogicalFile(logicalName);
        if (data is null)
        {
            completed("Nothing to export.");
            return;
        }
        IOSPortableDocument document = new(FileName(logicalName), IOSPortableDocumentKind.CelesteLogicalFile, data);
        if (!IOSFilePortabilityBridge.RequestExport(new[] { document }, share,
                (success, error) => completed(error ?? (success ? "Export complete." : "Export cancelled."))))
            completed("Files is unavailable.");
    }

    public static void ExportAll(Action<string> completed)
    {
        List<IOSPortableDocument> documents = new();
        foreach (string logicalName in new[] { "settings", "0", "1", "2" })
        {
            byte[] data = IOSStorageHooks.ReadLogicalFile(logicalName);
            if (data is not null)
                documents.Add(new IOSPortableDocument(FileName(logicalName), IOSPortableDocumentKind.CelesteLogicalFile, data));
        }
        if (documents.Count == 0)
        {
            completed("Nothing to export.");
            return;
        }
        if (!IOSFilePortabilityBridge.RequestExport(documents, false,
                (success, error) => completed(error ?? (success ? "Export complete." : "Export cancelled."))))
            completed("Files is unavailable.");
    }

    public static void Import(
        string? destination,
        bool settings,
        ChoicePresenter presentChoice,
        Action<string> completed)
    {
        if (!MutationSafe)
        {
            completed("Return to the main menu before importing files.");
            return;
        }
        if (!IOSFilePortabilityBridge.RequestImport(
                IOSPortableDocumentKind.CelesteLogicalFile, MaximumImportBytes, result =>
                HandleImportRead(result, destination, settings, presentChoice, completed)))
            completed("Files is unavailable.");
    }

    public static void Restore(
        string logicalName,
        ChoicePresenter presentChoice,
        Action<string> completed)
    {
        if (!MutationSafe)
        {
            completed("Return to the main menu before restoring files.");
            return;
        }
        string role = logicalName == "settings" ? "Settings" : $"Save Slot {int.Parse(logicalName) + 1}";
        presentChoice(
            "Restore Previous", $"Restore the previous {role}? The current file will remain available as the next previous copy.",
            new[] { "Restore" }, choice =>
            {
                if (choice is null) { completed("Restore cancelled."); return; }
                try
                {
                    CelesteFileRestoreResult restored = IOSStorageHooks.RestorePreviousGood(logicalName);
                    if (!restored.Restored) { completed("No valid previous file is available."); return; }
                    RefreshAfterMutation(logicalName);
                    completed(restored.Reversible ? "Previous file restored. You can restore again to undo." : "Previous file restored.");
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    completed("The previous file could not be restored.");
                }
            });
    }

    private static void HandleImportRead(
        IOSExternalReadResult result,
        string? destination,
        bool settings,
        ChoicePresenter presentChoice,
        Action<string> completed)
    {
        if (result.Cancelled) { completed("Import cancelled."); return; }
        if (result.Data is null) { completed(result.ErrorMessage ?? "The selected file could not be read."); return; }
        byte[] data = result.Data;
        bool validExpected = IOSStorageHooks.ValidateLogicalFile(settings ? "settings" : "0", data);
        if (!validExpected)
        {
            bool opposite = IOSStorageHooks.ValidateLogicalFile(settings ? "0" : "settings", data);
            completed(opposite
                ? (settings ? "This file contains SaveData, not Celeste Settings." : "This file contains Celeste Settings, not SaveData.")
                : (settings ? "This isn't a valid Celeste Settings file." : "This isn't a valid Celeste save."));
            return;
        }

        if (settings)
        {
            ConfirmAndCommit("settings", data, presentChoice, completed);
            return;
        }
        if (destination is not null)
        {
            ConfirmAndCommit(destination, data, presentChoice, completed);
            return;
        }
        presentChoice(
            "Choose Save Slot", "Where should this save be imported?", new[] { "Save Slot 1", "Save Slot 2", "Save Slot 3" },
            choice =>
            {
                if (choice is null) { completed("Import cancelled."); return; }
                ConfirmAndCommit(choice.Value.ToString(), data, presentChoice, completed);
            });
    }

    private static void ConfirmAndCommit(
        string logicalName,
        byte[] data,
        ChoicePresenter presentChoice,
        Action<string> completed)
    {
        bool exists = IOSStorageHooks.LogicalFileExists(logicalName);
        if (!exists)
        {
            Commit(logicalName, data, completed);
            return;
        }
        string role = logicalName == "settings" ? "Celeste Settings" : $"Save Slot {int.Parse(logicalName) + 1}";
        presentChoice(
            "Replace Existing File", $"{role} will be replaced. A previous copy will be retained.", new[] { "Replace" },
            choice =>
            {
                if (choice is null) { completed("Import cancelled."); return; }
                Commit(logicalName, data, completed);
            });
    }

    private static void Commit(string logicalName, byte[] data, Action<string> completed)
    {
        try
        {
            IOSStorageHooks.SaveLogicalFile(logicalName, data);
            byte[] exact = IOSStorageHooks.ReadLogicalFile(logicalName);
            if (exact is null || !exact.AsSpan().SequenceEqual(data))
                throw new IOException("The committed logical bytes did not verify.");
            RefreshAfterMutation(logicalName);
            completed(logicalName == "settings" ? "Settings imported." : "Save imported.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            completed("The file could not be imported safely.");
        }
    }

    private static void RefreshAfterMutation(string logicalName)
    {
        if (logicalName == "settings")
        {
            Settings.Reload();
            Input.Initialize();
            Input.ResetGrab();
        }
        else
        {
            OuiFileSelect.Loaded = false;
        }
    }

    private static string FileName(string logicalName) => logicalName switch
    {
        "settings" => "Celeste-Settings.celeste",
        "0" => "Celeste-Slot-1.celeste",
        "1" => "Celeste-Slot-2.celeste",
        "2" => "Celeste-Slot-3.celeste",
        _ => throw new ArgumentException("Unsupported logical file.", nameof(logicalName)),
    };
}

[Tracked(false)]
public sealed class IOSDataFilesUI : TextMenu
{
    private bool closing;
    private string? logicalName;
    private SubHeader? status;
    private Action? cancelChoice;

    public IOSDataFilesUI()
    {
        BuildRoot();
        OnESC = OnCancel = Back;
        MinWidth = 760f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    private void BuildRoot()
    {
        logicalName = null;
        Clear();
        Add(new Header("DATA & FILES"));
        Add(new SubHeader("Celeste files remain private until you choose Import or Export", false));
        for (int slot = 0; slot < 3; slot++)
        {
            string logical = slot.ToString();
            string presence = IOSDataFiles.Exists(logical) ? "" : " (Empty)";
            Add(new Button($"Save Slot {slot + 1}{presence}").Pressed(() => BuildFile(logical)));
        }
        Add(new Button("Settings").Pressed(() => BuildFile("settings")));
        Add(new Button("Export All Saves...").Pressed(() => Run(IOSDataFiles.ExportAll)));
        Add(new Button("Import Save...").Pressed(() => Run(
            (choose, done) => IOSDataFiles.Import(null, false, choose, done))));
        status = new SubHeader(IOSDataFiles.MutationSafe
            ? "Imports are validated and copied into Celeste's durable storage"
            : "Return to the main menu before importing or restoring", false);
        Add(status);
        FirstSelection();
        RecalculateSize();
    }

    private void BuildFile(string logical)
    {
        logicalName = logical;
        Clear();
        string title = logical == "settings" ? "SETTINGS" : $"SAVE SLOT {int.Parse(logical) + 1}";
        Add(new Header(title));
        bool exists = IOSDataFiles.Exists(logical);
        Add(new SubHeader(exists ? "File available" : "No file saved", false));
        Button export = new("Export to Files...") { Disabled = !exists };
        export.Pressed(() => Run(done => IOSDataFiles.Export(logical, false, done)));
        Add(export);
        Button share = new("Share...") { Disabled = !exists };
        share.Pressed(() => Run(done => IOSDataFiles.Export(logical, true, done)));
        Add(share);
        Add(new Button("Import...").Pressed(() => Run(
            (choose, done) => IOSDataFiles.Import(logical, logical == "settings", choose, done))));
        Button restore = new(logical == "settings" ? "Restore Previous Settings..." : "Restore Previous Save...")
        {
            Disabled = !IOSDataFiles.HasPrevious(logical),
        };
        restore.Pressed(() => Run((choose, done) => IOSDataFiles.Restore(logical, choose, done)));
        Add(restore);
        status = new SubHeader("", false);
        Add(status);
        FirstSelection();
        RecalculateSize();
    }

    private void Run(Action<Action<string>> operation)
    {
        if (!IOSDataFiles.Available) { SetStatus("Files is unavailable."); return; }
        Focused = false;
        SetStatus("Opening Files...");
        operation(message =>
        {
            Focused = true;
            SetStatus(message);
        });
    }

    private void Run(Action<IOSDataFiles.ChoicePresenter, Action<string>> operation)
    {
        if (!IOSDataFiles.Available) { SetStatus("Files is unavailable."); return; }
        string? returnLogicalName = logicalName;
        Focused = false;
        SetStatus("Opening Files...");
        operation(PresentChoice, message =>
        {
            cancelChoice = null;
            if (returnLogicalName is null) BuildRoot();
            else BuildFile(returnLogicalName);
            Focused = true;
            SetStatus(message);
        });
    }

    private void PresentChoice(
        string title,
        string message,
        IReadOnlyList<string> choices,
        Action<int?> completed)
    {
        Clear();
        Add(new Header(title.ToUpperInvariant()));
        Add(new SubHeader(message, false));
        for (int index = 0; index < choices.Count; index++)
        {
            int selected = index;
            Add(new Button(choices[index]).Pressed(() => CompleteChoice(selected, completed)));
        }
        Add(new Button("Cancel").Pressed(() => CompleteChoice(null, completed)));
        cancelChoice = () => CompleteChoice(null, completed);
        Focused = true;
        FirstSelection();
        RecalculateSize();
    }

    private void CompleteChoice(int? selected, Action<int?> completed)
    {
        if (cancelChoice is null) return;
        cancelChoice = null;
        Focused = false;
        completed(selected);
    }

    private void SetStatus(string message)
    {
        if (status is not null) status.Title = message;
        RecalculateSize();
    }

    private void Back()
    {
        if (!Focused) return;
        if (cancelChoice is not null)
        {
            Action cancel = cancelChoice;
            cancel();
            return;
        }
        if (logicalName is not null) { BuildRoot(); return; }
        Focused = false;
        closing = true;
    }

    public override void Update()
    {
        base.Update();
        Alpha = Calc.Approach(Alpha, closing ? 0f : 1f, Engine.RawDeltaTime * 8f);
        if (closing && Alpha <= 0f) Close();
    }

    public override void Render()
    {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
    }
}
