// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class HostSettingsTests
{
    [TestMethod]
    public void SettingsShapeParsesWrappedBareAndMalformedValues()
    {
        const string json = """
            {"properties":{"snippets_path":{"value":"C:\\snips"},"injection_backend":{"value":"type"},"clipboard_threshold_chars":{"value":42},"treat_remaps_as_typing":false,"require_word_boundary":{"value":true},"word_boundary_keeps_space":false}}
            """;
        HostSettings.Values values = HostSettings.Parse(json);
        Assert.AreEqual("type", values.InjectionBackend);
        Assert.AreEqual(42, values.ClipboardThresholdChars);
        Assert.AreEqual(false, values.TreatRemapsAsTyping);
        Assert.AreEqual(true, values.RequireWordBoundary);
        Assert.AreEqual(false, values.WordBoundaryKeepsSpace);
        Assert.AreEqual(@"C:\snips", values.SnippetsPath);

        Assert.AreEqual(9, HostSettings.Parse("{\"properties\":{\"clipboard_threshold_chars\":\"9\"}}").ClipboardThresholdChars);
        Assert.IsNull(HostSettings.Parse("{\"properties\":42}").InjectionBackend);
        Assert.IsNull(HostSettings.Parse("{\"properties\":{\"clipboard_threshold_chars\":\"abc\"}}").ClipboardThresholdChars);
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
            HostSettings.Values parsed = HostSettings.Parse(json);
            Assert.IsNull(parsed.InjectionBackend);
            Assert.IsNull(parsed.ClipboardThresholdChars);
            Assert.IsNull(parsed.TreatRemapsAsTyping);
        }
    }

    [TestMethod]
    public void BareSettingsValuesAreAccepted()
    {
        HostSettings.Values bare = HostSettings.Parse("{\"properties\":{\"injection_backend\":\"clipboard\",\"clipboard_threshold_chars\":7,\"treat_remaps_as_typing\":true}}");
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
        Assert.AreEqual(42, settings.Read().ClipboardThresholdChars);
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            Assert.AreEqual(42, settings.Read().ClipboardThresholdChars);
        }

        using var changed = new ManualResetEventSlim(false);
        settings.StartWatching(() => changed.Set());
        File.WriteAllText(file, "{\"properties\":{\"clipboard_threshold_chars\":{\"value\":11}}}");
        Assert.IsTrue(changed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(11, settings.Read().ClipboardThresholdChars);
    }

    [TestMethod]
    public void MissingSettingsFileIsNotAnError()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using HostSettings settings = HostSettings.Create([$"--settings:{Path.Combine(scratch.Path, "missing.json")}"])!;
        Assert.IsNull(settings.Read().InjectionBackend);
        Assert.IsNull(settings.Read().ClipboardThresholdChars);
    }

    [TestMethod]
    public void WordBoundarySettingsParseIndependently()
    {
        HostSettings.Values keep = HostSettings.Parse("{\"properties\":{\"word_boundary_keeps_space\":false}}");
        Assert.AreEqual(false, keep.WordBoundaryKeepsSpace);
        Assert.IsNull(HostSettings.Parse("{\"properties\":{}}").WordBoundaryKeepsSpace);

        HostSettings.Values required = HostSettings.Parse("{\"properties\":{\"require_word_boundary\":{\"value\":true}}}");
        Assert.AreEqual(true, required.RequireWordBoundary);
        Assert.IsNull(HostSettings.Parse("{\"properties\":{}}").RequireWordBoundary);
    }
}
