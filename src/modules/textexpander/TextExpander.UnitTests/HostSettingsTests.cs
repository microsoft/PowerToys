// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class HostSettingsTests
{
    [TestMethod]
    public void SettingsShapeParsesWrappedBareAndMalformedValues()
    {
        const string json = """
            {"properties":{"snippets_path":{"value":"C:\\snips"},"injection_backend":{"value":"type"},"clipboard_threshold_chars":{"value":42},"treat_remaps_as_typing":false,"require_word_boundary":{"value":true},"word_boundary_keeps_space":false,"case_sensitive":{"value":false}}}
            """;
        HostSettings.Values values = Parsed(json);
        Assert.AreEqual("type", values.InjectionBackend);
        Assert.AreEqual(42, values.ClipboardThresholdChars);
        Assert.AreEqual(false, values.TreatRemapsAsTyping);
        Assert.AreEqual(true, values.RequireWordBoundary);
        Assert.AreEqual(false, values.WordBoundaryKeepsSpace);
        Assert.AreEqual(false, values.CaseSensitive);
        Assert.AreEqual(@"C:\snips", values.SnippetsPath);

        Assert.AreEqual(9, Parsed("{\"properties\":{\"clipboard_threshold_chars\":\"9\"}}").ClipboardThresholdChars);
        Assert.IsNull(HostSettings.Parse("{\"properties\":42}")?.InjectionBackend);
        Assert.IsNull(Parsed("{\"properties\":{\"clipboard_threshold_chars\":\"abc\"}}").ClipboardThresholdChars);
    }

    [TestMethod]
    public void SettingsSwitchAndDataFolderAreResolvedSafely()
    {
        Assert.IsNull(HostSettings.Create(["--managed"]));
        Assert.IsNull(HostSettings.ParseSettingsPath(["--settings:  "]));
        Assert.AreEqual(@"C:\x y\settings.json", HostSettings.ParseSettingsPath(["--settings:\"C:\\x y\\settings.json\""]));
        Assert.AreEqual(@"C:\module", HostSettings.DataFolder(@"C:\module\settings.json"));
        Assert.IsNull(HostSettings.DataFolder(null));
        Assert.IsNull(HostSettings.DataFolder("   "));
    }

    [TestMethod]
    public void AbsentAndMalformedSettingsLeaveValuesUnspecified()
    {
        foreach (string json in new[]
        {
            "{\"properties\":{\"injection_backend\":",
            "[1,2,3]",
            string.Empty,
            "{\"name\":\"TextExpander\"}",
            "{\"properties\":42}",
            "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":\"abc\"},\"treat_remaps_as_typing\":\"yes\"}}",
        })
        {
            HostSettings.Values? parsed = HostSettings.Parse(json);
            Assert.IsNull(parsed?.InjectionBackend);
            Assert.IsNull(parsed?.ClipboardThresholdChars);
            Assert.IsNull(parsed?.TreatRemapsAsTyping);
            Assert.IsNull(parsed?.CaseSensitive);
        }
    }

    [TestMethod]
    public void BareSettingsValuesAreAccepted()
    {
        HostSettings.Values bare = Parsed("{\"properties\":{\"injection_backend\":\"clipboard\",\"clipboard_threshold_chars\":7,\"treat_remaps_as_typing\":true}}");
        Assert.AreEqual("clipboard", bare.InjectionBackend);
        Assert.AreEqual(7, bare.ClipboardThresholdChars);
        Assert.AreEqual(true, bare.TreatRemapsAsTyping);
    }

    [TestMethod]
    public void SettingsFileCanBeReadWhileHeldAndReloadedAfterChange()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string file = Path.Combine(scratch.Path, "settings.json");
        File.WriteAllText(file, "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":42}}}");
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        Assert.AreEqual(42, settings.Read()?.ClipboardThresholdChars);
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            Assert.AreEqual(42, settings.Read()?.ClipboardThresholdChars);
        }

        using var changed = new ManualResetEventSlim(false);
        settings.StartWatching(() =>
        {
            changed.Set();
            return true;
        });
        File.WriteAllText(file, "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":11}}}");
        Assert.IsTrue(changed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(11, settings.Read()?.ClipboardThresholdChars);
    }

    [TestMethod]
    public void MissingSettingsFileIsNotAnError()
    {
        // Nothing is read: at startup the engine uses the defaults, and once running it keeps what it has.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using HostSettings settings = HostSettings.Create([$"--settings:{Path.Combine(scratch.Path, "missing.json")}"])!;
        Assert.IsNull(settings.Read());
    }

    [TestMethod]
    public void AFileThatIsntSettingsIsNotAnEmptySnippetsPath()
    {
        // Both used to read as all nulls, and an empty snippets_path asks for the default library.
        foreach (string json in new[] { "{", string.Empty, "[1,2,3]", "{\"name\":\"TextExpander\"}", "{\"properties\":42}" })
        {
            Assert.IsNull(HostSettings.Parse(json), json);
        }

        Assert.IsTrue(HostSettings.Parse("{\"properties\":{\"snippets_path\":{\"value\":\"\"}}}") is { SnippetsPath: null });
        Assert.IsTrue(HostSettings.Parse("{\"properties\":{}}") is { SnippetsPath: null });
    }

    [TestMethod]
    public void SettingsThatCantBeReadKeepTheSnippetsLibraryInUse()
    {
        // An incomplete '{' written to settings.json switched a custom library to the default starters.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string custom = Path.Combine(scratch.Path, "Custom.json");
        File.WriteAllText(custom, SnippetJson.Of("!c=custom"));
        string file = Path.Combine(scratch.Path, "settings.json");
        File.WriteAllText(file, "{\"properties\":{\"snippets_path\":{\"value\":\"" + custom.Replace(@"\", @"\\", StringComparison.Ordinal) + "\"}}}");
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        using var store = new SnippetStore(custom);
        Assert.AreEqual(1, store.Load().Count);
        Assert.IsTrue(Program.ApplyChangedSettings(settings, store));
        Assert.AreEqual(custom, store.FilePath);

        File.WriteAllText(file, "{");
        Assert.IsFalse(Program.ApplyChangedSettings(settings, store));
        Assert.AreEqual(custom, store.FilePath);
        Assert.IsFalse(store.IsDefaultPath);
        Assert.AreEqual("custom", store.Current.Map["!c"]);

        // Read as settings, an empty path still means the default library, in the settings file's folder.
        File.WriteAllText(file, "{\"properties\":{\"snippets_path\":{\"value\":\"\"}}}");
        Assert.IsTrue(Program.ApplyChangedSettings(settings, store));
        Assert.IsTrue(store.IsDefaultPath);
        Assert.AreEqual(Path.Combine(scratch.Path, SnippetStore.FileName), store.FilePath);
    }

    [TestMethod]
    public void SettingsThatCantBeReadAreReadAgainAFewTimesAndReported()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string file = Path.Combine(scratch.Path, "settings.json");
        File.WriteAllText(file, "{\"properties\":{}}");
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        using var log = new StringWriter();
        using var listener = new TextWriterTraceListener(log);
        Trace.Listeners.Add(listener);
        try
        {
            int reads = 0;
            using var gaveUp = new ManualResetEventSlim(false);
            settings.StartWatching(() =>
            {
                if (Interlocked.Increment(ref reads) == 1 + HostSettings.MaxRetries)
                {
                    gaveUp.Set();
                }

                return false;
            });

            // A save in progress has usually finished by the next try; one that never does isn't tried forever.
            File.WriteAllText(file, "{");
            Assert.IsTrue(gaveUp.Wait(TimeSpan.FromSeconds(10)), $"Read {Volatile.Read(ref reads)} times.");
            Thread.Sleep(4 * HostSettings.DebounceMs);
            Assert.AreEqual(1 + HostSettings.MaxRetries, Volatile.Read(ref reads));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        StringAssert.Contains(log.ToString(), file);
    }

    [TestMethod]
    public void SettingsThatCouldntBeReadAtStartupAreReadAgainWithoutAChange()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string file = Path.Combine(scratch.Path, "settings.json");
        File.WriteAllText(file, "{");
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        Assert.IsNull(settings.Read());

        // A save caught half-written at startup is read again shortly, not only on the file's next change.
        using var read = new ManualResetEventSlim(false);
        settings.StartWatching(
            () =>
            {
                read.Set();
                return true;
            },
            readAgain: true);

        Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public void SettingsChangedAfterASuccessfulStartupReadAreReadAgainWhenWatchingStarts()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string file = Path.Combine(scratch.Path, "settings.json");
        File.WriteAllText(file, "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":5}}}");
        using var read = new ManualResetEventSlim(false);
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        HostSettings.Values? applied = settings.Read();
        Assert.AreEqual(5, applied?.ClipboardThresholdChars);

        // The Settings app can save while the engine loads its initial snippet library, before the watcher exists.
        File.WriteAllText(file, "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":42}}}");
        settings.StartWatching(
            () =>
            {
                applied = settings.Read();
                read.Set();
                return applied is not null;
            },
            readAgain: true);

        Assert.IsTrue(read.Wait(TimeSpan.FromSeconds(5)), "No second file change should be needed to apply the saved options.");
        Assert.AreEqual(42, applied?.ClipboardThresholdChars);
    }

    [TestMethod]
    [DoNotParallelize]
    public void OverlappingReloadsApplyTheLatestLibraryAfterTheEarlierCallbackFinishes()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string firstLibrary = Path.Combine(scratch.Path, "first.json");
        string latestLibrary = Path.Combine(scratch.Path, "latest.json");
        File.WriteAllText(firstLibrary, SnippetJson.Of("!which=first"));
        File.WriteAllText(latestLibrary, SnippetJson.Of("!which=latest"));
        string settingsFolder = Path.Combine(scratch.Path, "settings");
        string settingsFile = Path.Combine(settingsFolder, "settings.json");
        using HostSettings settings = HostSettings.Create([$"--settings:{settingsFile}"])!;
        int failNextRead = 0;
        int firstCallbackThread = 0;

        byte[] ReadBytes(string path)
        {
            if (Interlocked.Exchange(ref failNextRead, 0) != 0)
            {
                Volatile.Write(ref firstCallbackThread, Environment.CurrentManagedThreadId);
                throw new IOException("The first comparison read is temporarily unavailable.");
            }

            return File.ReadAllBytes(path);
        }

        using var store = new SnippetStore(
            firstLibrary,
            isDefault: false,
            caseSensitive: true,
            readAllBytes: ReadBytes,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.Zero,
            maxReloadAttempts: 1,
            log: _ => { });
        Assert.IsTrue(store.Load().Succeeded);
        using var firstPaused = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);
        using var secondStarted = new ManualResetEventSlim(false);
        using var secondEntered = new ManualResetEventSlim(false);
        int callbacks = 0;
        int applied = 0;

        // Register the real read/apply callback before the folder exists, so filesystem notifications cannot
        // introduce extra reloads. The two calls below model overlapping timer callbacks deterministically.
        settings.StartWatching(() =>
        {
            if (Interlocked.Increment(ref callbacks) == 2)
            {
                secondEntered.Set();
            }

            bool read = Program.ApplyChangedSettings(settings, store);
            if (read)
            {
                Interlocked.Increment(ref applied);
            }

            return read;
        });
        Directory.CreateDirectory(settingsFolder);

        void WriteSettings(string library) => File.WriteAllText(settingsFile, JsonSerializer.Serialize(new
        {
            properties = new { case_sensitive = false, snippets_path = new { value = library } },
        }));

        WriteSettings(firstLibrary);
        using var listener = new ComparisonFailureListener(() => Volatile.Read(ref firstCallbackThread), firstPaused, releaseFirst);
        var previousOptions = (Injector.Backend, Injector.ClipboardThresholdChars, HostInputTags.AcceptRemappedInput, WordBoundary.Required, WordBoundary.KeepTerminator);
        Task? first = null;
        Task? second = null;
        Trace.Listeners.Add(listener);
        try
        {
            Volatile.Write(ref failNextRead, 1);
            first = Task.Run(settings.Reload);
            Assert.IsTrue(firstPaused.Wait(TimeSpan.FromSeconds(5)), "The first callback did not pause between its comparison change and library repoint.");

            WriteSettings(latestLibrary);
            second = Task.Run(() =>
            {
                secondStarted.Set();
                settings.Reload();
            });
            Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(secondEntered.Wait(TimeSpan.FromMilliseconds(200)), "The latest settings must wait for the earlier read/apply callback to finish.");

            releaseFirst.Set();
            Assert.IsTrue(Task.WaitAll([first, second], TimeSpan.FromSeconds(5)));
            Assert.IsFalse(listener.TimedOut);
            Assert.AreEqual(2, callbacks);
            Assert.AreEqual(2, applied);
            Assert.AreEqual(latestLibrary, settings.Read()?.SnippetsPath);
            Assert.AreEqual(false, settings.Read()?.CaseSensitive);
            Assert.AreEqual(latestLibrary, store.FilePath);
            Assert.AreEqual("latest", store.Current.Map["!WHICH"], "The latest library and its case-insensitive comparison must both be applied.");
        }
        finally
        {
            releaseFirst.Set();
            try
            {
                first?.Wait(TimeSpan.FromSeconds(5));
                second?.Wait(TimeSpan.FromSeconds(5));
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                Injector.Backend = previousOptions.Backend;
                Injector.ClipboardThresholdChars = previousOptions.ClipboardThresholdChars;
                HostInputTags.AcceptRemappedInput = previousOptions.AcceptRemappedInput;
                WordBoundary.Required = previousOptions.Required;
                WordBoundary.KeepTerminator = previousOptions.KeepTerminator;
            }
        }
    }

    [TestMethod]
    public void AQueuedReloadDoesNotCallBackAfterDisposal()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string file = Path.Combine(scratch.Path, "not-created", "settings.json");
        using HostSettings settings = HostSettings.Create([$"--settings:{file}"])!;
        int calls = 0;
        settings.StartWatching(() =>
        {
            calls++;
            return true;
        });

        settings.Reload();
        Assert.AreEqual(1, calls, "An ordinary reload calls the registered reader.");

        settings.Dispose();
        settings.Reload();
        Assert.AreEqual(1, calls, "A callback queued before disposal must not read or apply settings afterwards.");
    }

    [TestMethod]
    public void WordBoundarySettingsParseIndependently()
    {
        HostSettings.Values keep = Parsed("{\"properties\":{\"word_boundary_keeps_space\":false}}");
        Assert.AreEqual(false, keep.WordBoundaryKeepsSpace);
        Assert.IsNull(Parsed("{\"properties\":{}}").WordBoundaryKeepsSpace);

        Assert.AreEqual(
            false,
            Parsed("{\"properties\":{\"require_word_boundary\":false}}").RequireWordBoundary);
        Assert.AreEqual(
            true,
            Parsed("{\"properties\":{\"require_word_boundary\":{\"value\":true}}}").RequireWordBoundary);
        Assert.IsNull(Parsed("{\"properties\":{}}").RequireWordBoundary);
    }

    [TestMethod]
    public void CaseSensitivityParsesWrappedAndBareValues()
    {
        Assert.AreEqual(false, Parsed("{\"properties\":{\"case_sensitive\":{\"value\":false}}}").CaseSensitive);
        Assert.AreEqual(true, Parsed("{\"properties\":{\"case_sensitive\":true}}").CaseSensitive);
        Assert.IsNull(Parsed("{\"properties\":{}}").CaseSensitive);
    }

    private static HostSettings.Values Parsed(string json)
        => HostSettings.Parse(json) ?? throw new AssertFailedException("Not read as settings: " + json);

    private sealed class ComparisonFailureListener(Func<int> firstThread, ManualResetEventSlim paused, ManualResetEventSlim release) : TraceListener
    {
        public bool TimedOut { get; private set; }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (Environment.CurrentManagedThreadId == firstThread()
                && message?.Contains("could not apply the case-sensitive setting", StringComparison.Ordinal) == true)
            {
                paused.Set();
                TimedOut = !release.Wait(TimeSpan.FromSeconds(10));
            }
        }
    }
}
