// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EnvironmentVariables.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using VariableSnapshot = Microsoft.EnvironmentVariables.UITests.TestState.UserVariableSnapshot;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class EnvironmentVariablesTestStateTests
{
    private static readonly string[] PathVariableNames = ["Path", "Created"];
    private static readonly string[] OrderedVariableNames = ["First", "Second", "Third"];
    private static readonly string[] FileKeys = ["profiles", "moduleSettings", "globalSettings"];

    [TestMethod]
    public void ResetRestoresEveryVariableEvenWhenEditorStopFails()
    {
        using var fixture = new StateFixture();
        var original = new VariableSnapshot(@"%USERPROFILE%\bin;;%OTHER%", RegistryValueKind.ExpandString);
        fixture.Variables["Path"] = original;
        var state = fixture.CreateState();
        state.TrackUserVariable("Path");
        state.TrackUserVariable("Created");
        fixture.Variables["Path"] = new("changed", RegistryValueKind.String);
        fixture.Variables["Created"] = new("created", RegistryValueKind.String);
        fixture.FailStop = true;

        var failure = Assert.ThrowsExactly<AggregateException>(state.Reset);

        Assert.HasCount(1, failure.InnerExceptions);
        CollectionAssert.AreEqual(PathVariableNames, fixture.RestoreAttempts);
        Assert.AreEqual(original, fixture.Variables["Path"]);
        Assert.IsFalse(fixture.Variables.ContainsKey("Created"));
        Assert.AreEqual("[]", File.ReadAllText(fixture.ProfilesPath));
        Assert.IsTrue(File.Exists(fixture.JournalPath));

        fixture.FailStop = false;
        state.Dispose();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void ResetAggregatesVariableFailuresAndRetriesWithoutSkippingLaterEntries()
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        foreach (string name in OrderedVariableNames)
        {
            fixture.Variables[name] = new("original", RegistryValueKind.String);
            state.TrackUserVariable(name);
            fixture.Variables[name] = new("changed", RegistryValueKind.String);
        }

        byte[] journal = File.ReadAllBytes(fixture.JournalPath);
        fixture.FailedVariables.UnionWith(["First", "Third"]);

        var failure = Assert.ThrowsExactly<AggregateException>(state.Reset);

        Assert.HasCount(2, failure.InnerExceptions);
        CollectionAssert.AreEqual(OrderedVariableNames, fixture.RestoreAttempts);
        Assert.AreEqual("original", fixture.Variables["Second"].Value);
        Assert.AreEqual("changed", fixture.Variables["First"].Value);
        CollectionAssert.AreEqual(journal, File.ReadAllBytes(fixture.JournalPath));

        fixture.FailedVariables.Clear();
        fixture.RestoreAttempts.Clear();
        state.Reset();
        CollectionAssert.AreEqual(OrderedVariableNames, fixture.RestoreAttempts);
        Assert.IsTrue(fixture.Variables.Values.All(snapshot => snapshot.Value == "original"));
        state.Dispose();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void DisposeAggregatesStopVariableAndFileFailuresAndRestoresRemainingState()
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        state.TrackUserVariable("First");
        state.TrackUserVariable("Second");
        fixture.Variables["First"] = new("created", RegistryValueKind.String);
        fixture.Variables["Second"] = new("created", RegistryValueKind.String);
        fixture.FailedVariables.Add("First");
        fixture.FailStop = true;
        fixture.MutateFiles();

        using (var lockedFile = new FileStream(fixture.ProfilesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(state.Dispose);

            Assert.HasCount(3, failure.InnerExceptions);
            Assert.IsFalse(fixture.Variables.ContainsKey("Second"));
            fixture.AssertOriginalFile("moduleSettings");
            fixture.AssertOriginalFile("globalSettings");
            Assert.IsTrue(File.Exists(fixture.JournalPath));
        }

        fixture.FailStop = false;
        fixture.FailedVariables.Clear();
        state.Dispose();
        fixture.AssertOriginalFiles();
        Assert.IsEmpty(fixture.Variables);
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    [DataRow("profiles")]
    [DataRow("moduleSettings")]
    public void DisposeContinuesAfterFileRestorationFailure(string failedFile)
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        fixture.MutateFiles();
        byte[] journal = File.ReadAllBytes(fixture.JournalPath);

        using (var lockedFile = new FileStream(fixture.FilePaths[failedFile], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(state.Dispose);

            Assert.HasCount(1, failure.InnerExceptions);
            foreach (string key in fixture.FilePaths.Keys.Where(key => key != failedFile))
            {
                fixture.AssertOriginalFile(key);
            }

            CollectionAssert.AreEqual(journal, File.ReadAllBytes(fixture.JournalPath));
        }

        state.Dispose();
        fixture.AssertOriginalFiles();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void DisposeRestoresOriginalBytesAndIsIdempotentAfterRemovingJournal()
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        var journal = fixture.ReadJournal();
        Assert.AreEqual(1, journal["version"]!.GetValue<int>());
        CollectionAssert.AreEquivalent(
            FileKeys,
            journal["files"]!.AsObject().Select(property => property.Key).ToArray());
        Assert.IsEmpty(journal["userVariables"]!.AsObject());
        foreach (var (key, original) in fixture.OriginalFiles)
        {
            CollectionAssert.AreEqual(original, Convert.FromBase64String(journal["files"]![key]!.GetValue<string>()));
        }

        fixture.MutateFiles();
        state.Dispose();

        fixture.AssertOriginalFiles();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
        Assert.IsFalse(File.Exists(fixture.JournalPath + ".new"));
        int stops = fixture.StopAttempts;
        state.Dispose();
        Assert.AreEqual(stops, fixture.StopAttempts);
    }

    [TestMethod]
    public void DisposeDeletesFilesThatDidNotExistBeforeTheScope()
    {
        using var fixture = new StateFixture();
        foreach (string path in fixture.FilePaths.Values)
        {
            File.Delete(path);
        }

        var state = fixture.CreateState();
        Assert.IsTrue(fixture.ReadJournal()["files"]!.AsObject().All(property => property.Value is null));
        state.Reset();
        fixture.MutateFiles();

        state.Dispose();

        Assert.IsTrue(fixture.FilePaths.Values.All(path => !File.Exists(path)));
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void NewScopeRecoversAbortedRegistryAndFilesBeforeTakingItsBaseline()
    {
        using var fixture = new StateFixture();
        var originalPath = new VariableSnapshot(@"%USERPROFILE%\bin;%UNEXPANDED%", RegistryValueKind.ExpandString);
        var originalEmpty = new VariableSnapshot(string.Empty, RegistryValueKind.String);
        fixture.Variables["Path"] = originalPath;
        fixture.Variables["Empty"] = originalEmpty;
        var abandoned = fixture.CreateState();
        abandoned.TrackUserVariable("Path");
        abandoned.TrackUserVariable("Empty");
        abandoned.TrackUserVariable("Created");
        fixture.Variables["Path"] = new("changed", RegistryValueKind.String);
        fixture.Variables["Empty"] = new("changed", RegistryValueKind.ExpandString);
        fixture.Variables["Created"] = new("created", RegistryValueKind.String);
        fixture.MutateFiles();

        // No Dispose: simulate losing the test host after mutations, including run_elevated=false.
        var recovered = fixture.CreateState();

        Assert.AreEqual(originalPath, fixture.Variables["Path"]);
        Assert.AreEqual(originalEmpty, fixture.Variables["Empty"]);
        Assert.IsFalse(fixture.Variables.ContainsKey("Created"));
        fixture.AssertOriginalFiles();
        Assert.IsTrue(JsonNode.Parse(File.ReadAllBytes(fixture.GlobalSettingsPath))!["run_elevated"]!.GetValue<bool>());
        Assert.IsEmpty(fixture.ReadJournal()["userVariables"]!.AsObject());

        recovered.TrackUserVariable("Path");
        fixture.Variables["Path"] = new("changed again", RegistryValueKind.String);
        fixture.MutateFiles();
        recovered.Dispose();
        Assert.AreEqual(originalPath, fixture.Variables["Path"]);
        fixture.AssertOriginalFiles();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void AbortedRecoveryPreservesAnOriginallyMissingGlobalSettingsFile()
    {
        using var fixture = new StateFixture();
        File.Delete(fixture.GlobalSettingsPath);
        _ = fixture.CreateState();
        fixture.MutateFiles();

        var recovered = fixture.CreateState();

        Assert.IsFalse(File.Exists(fixture.GlobalSettingsPath));
        Assert.IsNull(fixture.ReadJournal()["files"]!["globalSettings"]);
        File.WriteAllText(fixture.GlobalSettingsPath, """{"run_elevated":false}""");
        recovered.Dispose();
        Assert.IsFalse(File.Exists(fixture.GlobalSettingsPath));
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void FailedRecoveryKeepsTheOriginalJournalAndContinuesRestoringAllState()
    {
        using var fixture = new StateFixture();
        var abandoned = fixture.CreateState();
        abandoned.TrackUserVariable("First");
        abandoned.TrackUserVariable("Second");
        fixture.Variables["First"] = new("created", RegistryValueKind.String);
        fixture.Variables["Second"] = new("created", RegistryValueKind.String);
        fixture.MutateFiles();
        byte[] journal = File.ReadAllBytes(fixture.JournalPath);
        fixture.FailStop = true;
        fixture.FailedVariables.Add("First");

        using (var lockedFile = new FileStream(fixture.ProfilesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(() => fixture.CreateState());

            Assert.HasCount(3, failure.InnerExceptions);
            Assert.IsFalse(fixture.Variables.ContainsKey("Second"));
            fixture.AssertOriginalFile("moduleSettings");
            fixture.AssertOriginalFile("globalSettings");
            CollectionAssert.AreEqual(journal, File.ReadAllBytes(fixture.JournalPath));
        }

        fixture.FailStop = false;
        fixture.FailedVariables.Clear();
        var recovered = fixture.CreateState();
        Assert.IsEmpty(fixture.Variables);
        fixture.AssertOriginalFiles();
        recovered.Dispose();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    public void SuccessfulResetRetainsSnapshotsForRecoveryOfSubsequentWrites()
    {
        using var fixture = new StateFixture();
        var original = new VariableSnapshot(@"%USERPROFILE%\bin", RegistryValueKind.ExpandString);
        fixture.Variables["Path"] = original;
        var abandoned = fixture.CreateState();
        abandoned.TrackUserVariable("Path");
        fixture.Variables["Path"] = new("changed", RegistryValueKind.String);
        abandoned.Reset();
        Assert.AreEqual(original, fixture.Variables["Path"]);
        Assert.IsNotNull(fixture.ReadJournal()["userVariables"]!["Path"]);
        fixture.Variables["Path"] = new("late write", RegistryValueKind.String);

        using var recovered = fixture.CreateState();

        Assert.AreEqual(original, fixture.Variables["Path"]);
        fixture.AssertOriginalFiles();
    }

    [TestMethod]
    [DataRow("malformed")]
    [DataRow("unsupportedVersion")]
    [DataRow("missingVersion")]
    [DataRow("unknownFile")]
    [DataRow("missingFile")]
    [DataRow("invalidFileBytes")]
    [DataRow("nullFiles")]
    [DataRow("nullVariable")]
    [DataRow("unsupportedKind")]
    [DataRow("missingValue")]
    [DataRow("missingKind")]
    [DataRow("duplicateName")]
    [DataRow("invalidName")]
    [DataRow("unknownProperty")]
    [DataRow("numericValue")]
    [DataRow("duplicateProperty")]
    public void InvalidJournalFailsBeforeAnyRestorationOrReplacement(string corruption)
    {
        using var fixture = new StateFixture();
        fixture.Variables["Path"] = new("private original", RegistryValueKind.ExpandString);
        var abandoned = fixture.CreateState();
        abandoned.TrackUserVariable("Path");
        fixture.Variables["Path"] = new("test mutation", RegistryValueKind.String);
        fixture.MutateFiles();
        var journal = fixture.ReadJournal();
        string unrelatedFile = Path.Combine(fixture.RootPath, "unrelated.json");
        File.WriteAllText(unrelatedFile, "must remain untouched");

        switch (corruption)
        {
            case "unsupportedVersion": journal["version"] = 2; break;
            case "missingVersion": journal.Remove("version"); break;
            case "unknownFile": journal["files"]![unrelatedFile] = Convert.ToBase64String([1, 2, 3]); break;
            case "missingFile": journal["files"]!.AsObject().Remove("profiles"); break;
            case "invalidFileBytes": journal["files"]!["profiles"] = "not base64"; break;
            case "nullFiles": journal["files"] = null; break;
            case "nullVariable": journal["userVariables"]!["Path"] = null; break;
            case "unsupportedKind": journal["userVariables"]!["Path"]!["kind"] = (int)RegistryValueKind.MultiString; break;
            case "missingValue": journal["userVariables"]!["Path"]!.AsObject().Remove("value"); break;
            case "missingKind": journal["userVariables"]!["Path"]!.AsObject().Remove("kind"); break;
            case "duplicateName": journal["userVariables"]!["PATH"] = journal["userVariables"]!["Path"]!.DeepClone(); break;
            case "invalidName": journal["userVariables"]!["bad=name"] = journal["userVariables"]!["Path"]!.DeepClone(); break;
            case "unknownProperty": journal["unexpected"] = true; break;
            case "numericValue": journal["userVariables"]!["Path"]!["value"] = 42; break;
        }

        string contents = corruption switch
        {
            "malformed" => "{",
            "duplicateProperty" => journal.ToJsonString().Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal),
            _ => journal.ToJsonString(),
        };
        File.WriteAllText(fixture.JournalPath, contents);
        byte[] corruptJournal = File.ReadAllBytes(fixture.JournalPath);
        int stops = fixture.StopAttempts;

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => fixture.CreateState());

        Assert.IsFalse(failure.ToString().Contains("private original", StringComparison.Ordinal));
        Assert.AreEqual(stops, fixture.StopAttempts);
        Assert.IsEmpty(fixture.RestoreAttempts);
        Assert.AreEqual("test mutation", fixture.Variables["Path"].Value);
        Assert.AreEqual("""{"run_elevated":false}""", File.ReadAllText(fixture.GlobalSettingsPath));
        Assert.AreEqual("must remain untouched", File.ReadAllText(unrelatedFile));
        CollectionAssert.AreEqual(corruptJournal, File.ReadAllBytes(fixture.JournalPath));
    }

    [TestMethod]
    [DataRow(RegistryValueKind.Binary)]
    [DataRow(RegistryValueKind.DWord)]
    [DataRow(RegistryValueKind.QWord)]
    [DataRow(RegistryValueKind.MultiString)]
    [DataRow(RegistryValueKind.Unknown)]
    [DataRow(RegistryValueKind.None)]
    public void TrackRejectsUnsupportedRegistryKindsWithoutSavingALossySnapshot(RegistryValueKind kind)
    {
        using var fixture = new StateFixture();
        using var state = fixture.CreateState();
        fixture.Variables["Unsupported"] = new("not a raw registry string", kind);
        byte[] journal = File.ReadAllBytes(fixture.JournalPath);

        Assert.ThrowsExactly<NotSupportedException>(() => state.TrackUserVariable("Unsupported"));

        CollectionAssert.AreEqual(journal, File.ReadAllBytes(fixture.JournalPath));
        fixture.Variables["Unsupported"] = new("supported", RegistryValueKind.String);
        state.TrackUserVariable("Unsupported");
        Assert.AreEqual(2, fixture.ReadAttempts);
        Assert.AreEqual("supported", fixture.ReadJournal()["userVariables"]!["Unsupported"]!["value"]!.GetValue<string>());
    }

    [TestMethod]
    [DataRow(RegistryValueKind.String, "")]
    [DataRow(RegistryValueKind.String, @"%TEMP%\literal")]
    [DataRow(RegistryValueKind.ExpandString, @"%USERPROFILE%\bin;;%OTHER%")]
    public void TrackPersistsRawValuesAndKindsOnlyOnceIgnoringNameCase(RegistryValueKind kind, string value)
    {
        using var fixture = new StateFixture();
        var original = new VariableSnapshot(value, kind);
        fixture.Variables["Path"] = original;
        var state = fixture.CreateState();

        state.TrackUserVariable("Path");
        fixture.Variables["Path"] = new("changed", RegistryValueKind.String);
        state.TrackUserVariable("PATH");

        var snapshot = fixture.ReadJournal()["userVariables"]!["Path"]!;
        Assert.AreEqual(value, snapshot["value"]!.GetValue<string>());
        Assert.AreEqual((int)kind, snapshot["kind"]!.GetValue<int>());
        Assert.AreEqual(1, fixture.ReadAttempts);
        state.Dispose();
        Assert.AreEqual(original, fixture.Variables["Path"]);
    }

    [TestMethod]
    public void TrackDoesNotAcceptAnUnpersistedSnapshotWhenJournalReplacementFails()
    {
        using var fixture = new StateFixture();
        using var state = fixture.CreateState();
        fixture.Variables["Path"] = new("first read", RegistryValueKind.String);
        byte[] originalJournal = File.ReadAllBytes(fixture.JournalPath);

        using (var lockedJournal = new FileStream(fixture.JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsExactly<IOException>(() => state.TrackUserVariable("Path"));
        }

        CollectionAssert.AreEqual(originalJournal, File.ReadAllBytes(fixture.JournalPath));
        Assert.IsFalse(File.Exists(fixture.JournalPath + ".new"));
        fixture.Variables["Path"] = new("retry baseline", RegistryValueKind.String);
        state.TrackUserVariable("Path");
        Assert.AreEqual(2, fixture.ReadAttempts);
        Assert.AreEqual("retry baseline", fixture.ReadJournal()["userVariables"]!["Path"]!["value"]!.GetValue<string>());
    }

    [TestMethod]
    public void InitialSnapshotPersistenceFailureDoesNotChangeOriginalFiles()
    {
        using var fixture = new StateFixture();
        using (var lockedPending = new FileStream(fixture.JournalPath + ".new", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(() => fixture.CreateState());

            Assert.HasCount(2, failure.InnerExceptions);
            fixture.AssertOriginalFiles();
            Assert.IsFalse(File.Exists(fixture.JournalPath));
        }

        using var state = fixture.CreateState();
        Assert.IsTrue(File.Exists(fixture.JournalPath));
        Assert.IsFalse(File.Exists(fixture.JournalPath + ".new"));
    }

    [TestMethod]
    public void ResetStillRestoresVariablesWhenClearingProfilesFails()
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        state.TrackUserVariable("Created");
        fixture.Variables["Created"] = new("created", RegistryValueKind.String);

        using (var lockedProfiles = new FileStream(fixture.ProfilesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(state.Reset);

            Assert.HasCount(1, failure.InnerExceptions);
            Assert.IsFalse(fixture.Variables.ContainsKey("Created"));
            Assert.IsTrue(File.Exists(fixture.JournalPath));
        }

        state.Dispose();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".new")]
    public void DisposeKeepsTheJournalWhenJournalRemovalFails(string suffix)
    {
        using var fixture = new StateFixture();
        var state = fixture.CreateState();
        fixture.MutateFiles();
        if (suffix.Length > 0)
        {
            File.WriteAllText(fixture.JournalPath + suffix, "interrupted pending write");
        }

        using (var lockedJournal = new FileStream(fixture.JournalPath + suffix, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failure = Assert.ThrowsExactly<AggregateException>(state.Dispose);

            Assert.HasCount(1, failure.InnerExceptions);
            fixture.AssertOriginalFiles();
            Assert.IsTrue(File.Exists(fixture.JournalPath));
        }

        state.Dispose();
        Assert.IsFalse(File.Exists(fixture.JournalPath));
        Assert.IsFalse(File.Exists(fixture.JournalPath + ".new"));
    }

    private sealed class StateFixture : IDisposable
    {
        internal StateFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"EnvironmentVariablesTestStateTests-{Guid.NewGuid():N}");
            ModuleDirectory = Path.Combine(RootPath, "EnvironmentVariables");
            GlobalSettingsPath = Path.Combine(RootPath, "settings.json");
            Directory.CreateDirectory(ModuleDirectory);
            FilePaths = new()
            {
                ["profiles"] = Path.Combine(ModuleDirectory, "profiles.json"),
                ["moduleSettings"] = Path.Combine(ModuleDirectory, "settings.json"),
                ["globalSettings"] = GlobalSettingsPath,
            };
            OriginalFiles = new()
            {
                ["profiles"] = [0xEF, 0xBB, 0xBF, 0x5B, 0x5D],
                ["moduleSettings"] = Encoding.UTF8.GetBytes("""{"properties":{"launch_as_admin":true}}"""),
                ["globalSettings"] = Encoding.UTF8.GetBytes("""{"run_elevated":true,"unrelated":"preserved"}"""),
            };
            foreach (var (key, original) in OriginalFiles)
            {
                File.WriteAllBytes(FilePaths[key], original);
            }
        }

        internal string RootPath { get; }

        internal string ModuleDirectory { get; }

        internal string GlobalSettingsPath { get; }

        internal string ProfilesPath => FilePaths["profiles"];

        internal string JournalPath => Path.Combine(ModuleDirectory, TestState.JournalFileName);

        internal Dictionary<string, string> FilePaths { get; }

        internal Dictionary<string, byte[]> OriginalFiles { get; }

        internal Dictionary<string, VariableSnapshot> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal HashSet<string> FailedVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<string> RestoreAttempts { get; } = [];

        internal bool FailStop { get; set; }

        internal int StopAttempts { get; private set; }

        internal int ReadAttempts { get; private set; }

        public void Dispose()
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(RootPath, recursive: true);
                    return;
                }
                catch (IOException error) when (attempt < 9 && (error.HResult & 0xFFFF) is 32 or 33)
                {
                    // Filesystem scanners can briefly retain a just-written fixture file.
                    Thread.Sleep(100);
                }
            }
        }

        internal TestState CreateState()
        {
            return new(
                ModuleDirectory,
                GlobalSettingsPath,
                () =>
                {
                    StopAttempts++;
                    if (FailStop)
                    {
                        throw new InvalidOperationException("Simulated editor stop failure.");
                    }
                },
                name =>
                {
                    ReadAttempts++;
                    return Variables.GetValueOrDefault(name) ?? new(null, RegistryValueKind.String);
                },
                (name, snapshot) =>
                {
                    RestoreAttempts.Add(name);
                    if (FailedVariables.Contains(name))
                    {
                        throw new IOException("Simulated registry write failure.");
                    }

                    if (snapshot.Value is null)
                    {
                        Variables.Remove(name);
                    }
                    else
                    {
                        Variables[name] = snapshot;
                    }
                });
        }

        internal JsonObject ReadJournal()
        {
            return JsonNode.Parse(File.ReadAllBytes(JournalPath))!.AsObject();
        }

        internal void MutateFiles()
        {
            File.WriteAllText(ProfilesPath, "[\"changed profiles\"]");
            File.WriteAllText(FilePaths["moduleSettings"], """{"launch_as_admin":false}""");
            File.WriteAllText(GlobalSettingsPath, """{"run_elevated":false}""");
        }

        internal void AssertOriginalFile(string key)
        {
            CollectionAssert.AreEqual(OriginalFiles[key], File.ReadAllBytes(FilePaths[key]), key);
        }

        internal void AssertOriginalFiles()
        {
            foreach (string key in FilePaths.Keys)
            {
                AssertOriginalFile(key);
            }
        }
    }
}
