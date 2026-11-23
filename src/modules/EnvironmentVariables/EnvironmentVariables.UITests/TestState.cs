// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.EnvironmentVariables.UITests;

internal sealed class TestState : IDisposable
{
    internal const string ProcessName = "PowerToys.EnvironmentVariables";
    internal const string JournalFileName = "ui-tests-state.json";

    private const int JournalVersion = 1;
    private const string ProfilesKey = "profiles";
    private const string ModuleSettingsKey = "moduleSettings";
    private const string GlobalSettingsKey = "globalSettings";

    internal static readonly string ModuleDirectory = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "EnvironmentVariables");
    internal static readonly string ProfilesPath = Path.Combine(ModuleDirectory, "profiles.json");
    private static readonly string GlobalSettingsPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");

    private static readonly JsonSerializerOptions JournalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
    };

    private readonly Dictionary<string, UserVariableSnapshot> userVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]?> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> filePaths;
    private readonly string journalPath;
    private readonly Action stopEditor;
    private readonly Func<string, UserVariableSnapshot> readUserVariable;
    private readonly Action<string, UserVariableSnapshot> restoreUserVariable;
    private bool disposed;

    internal TestState()
        : this(ModuleDirectory, GlobalSettingsPath, StopEditor, ReadUserVariableSnapshot, RestoreUserVariable)
    {
    }

    internal TestState(
        string moduleDirectory,
        string globalSettingsPath,
        Action stopEditor,
        Func<string, UserVariableSnapshot> readUserVariable,
        Action<string, UserVariableSnapshot> restoreUserVariable)
    {
        ArgumentNullException.ThrowIfNull(stopEditor);
        ArgumentNullException.ThrowIfNull(readUserVariable);
        ArgumentNullException.ThrowIfNull(restoreUserVariable);

        this.stopEditor = stopEditor;
        this.readUserVariable = readUserVariable;
        this.restoreUserVariable = restoreUserVariable;
        filePaths = new(StringComparer.Ordinal)
        {
            [ProfilesKey] = Path.Combine(moduleDirectory, "profiles.json"),
            [ModuleSettingsKey] = Path.Combine(moduleDirectory, "settings.json"),
            [GlobalSettingsKey] = globalSettingsPath,
        };
        journalPath = Path.Combine(moduleDirectory, JournalFileName);
        Directory.CreateDirectory(moduleDirectory);

        byte[]? previousJournal = ReadFileSnapshot(journalPath);
        if (previousJournal is not null)
        {
            LoadJournal(previousJournal);
            RestoreClassState();
            userVariables.Clear();
            files.Clear();
        }
        else
        {
            stopEditor();
        }

        foreach (var (key, path) in filePaths)
        {
            files.Add(key, ReadFileSnapshot(path));
        }

        SaveJournal(userVariables);
    }

    internal static void ConfigureNonElevatedRunner()
    {
        var settings = JsonNode.Parse(File.ReadAllText(GlobalSettingsPath))!.AsObject();
        settings["run_elevated"] = false;
        File.WriteAllText(GlobalSettingsPath, settings.ToJsonString());
    }

    internal static string? ReadUserVariable(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment");
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    internal static string? ReadSystemVariable(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    internal static void StopEditor()
    {
        Assert.IsTrue(
            WindowControl.TryKillProcessTreeByNameAndWait(ProcessName, timeoutMS: 10_000),
            "Environment Variables did not exit; state restoration will still be attempted.");
    }

    internal void TrackUserVariable(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsValidVariableName(name))
        {
            throw new ArgumentException("A valid environment variable name is required.", nameof(name));
        }

        if (userVariables.ContainsKey(name))
        {
            return;
        }

        UserVariableSnapshot original = readUserVariable(name);
        if (!IsSupportedSnapshot(original))
        {
            throw new NotSupportedException("Only string and expandable-string environment variables can be preserved.");
        }

        var updatedVariables = new Dictionary<string, UserVariableSnapshot>(userVariables, StringComparer.OrdinalIgnoreCase)
        {
            [name] = original,
        };

        // Do not permit mutation until the original raw value is durably recoverable.
        SaveJournal(updatedVariables);
        userVariables.Add(name, original);
    }

    internal void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var failures = new List<Exception>();
        RestoreVariables(failures);
        AttemptRestore(() => File.WriteAllText(filePaths[ProfilesKey], "[]"), "reset profiles", failures);
        ThrowRestorationFailures(failures);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        RestoreClassState();
        disposed = true;
    }

    private static byte[]? ReadFileSnapshot(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static bool IsValidVariableName(string name)
    {
        return !string.IsNullOrEmpty(name) && !name.Contains('=') && !name.Contains('\0');
    }

    private static bool IsSupportedSnapshot(UserVariableSnapshot? snapshot)
    {
        return snapshot is not null && snapshot.Kind is RegistryValueKind.String or RegistryValueKind.ExpandString;
    }

    private static UserVariableSnapshot ReadUserVariableSnapshot(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment");
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return new(null, RegistryValueKind.String);
        }

        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var snapshot = new UserVariableSnapshot(value as string, key.GetValueKind(name));
        if (value is not string || !IsSupportedSnapshot(snapshot))
        {
            throw new NotSupportedException("Only string and expandable-string environment variables can be preserved.");
        }

        return snapshot;
    }

    private static void RestoreUserVariable(string name, UserVariableSnapshot original)
    {
        var failures = new List<Exception>();
        AttemptRestore(
            () => Environment.SetEnvironmentVariable(name, original.Value, EnvironmentVariableTarget.User),
            "notify the user environment change",
            failures);
        AttemptRestore(
            () =>
            {
                // Preserve raw text and kind even if the notifying OS API failed or converted the value.
                using var key = Registry.CurrentUser.CreateSubKey("Environment", writable: true);
                if (original.Value is null)
                {
                    key.DeleteValue(name, throwOnMissingValue: false);
                }
                else
                {
                    key.SetValue(name, original.Value, original.Kind);
                }
            },
            "restore the raw user registry value",
            failures);
        ThrowRestorationFailures(failures);
    }

    private static void AttemptRestore(Action action, string operation, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add(new InvalidOperationException($"Failed to {operation}.", exception));
        }
    }

    private static void ThrowRestorationFailures(List<Exception> failures)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException("Environment Variables test state restoration failed; the journal is retained for retry.", failures);
        }
    }

    private void RestoreVariables(List<Exception> failures)
    {
        AttemptRestore(stopEditor, "stop the Environment Variables editor", failures);
        foreach (var (name, original) in userVariables)
        {
            AttemptRestore(() => restoreUserVariable(name, original), $"restore user variable '{name}'", failures);
        }

        // Retain even successfully restored entries until class cleanup, since an editor that
        // failed to stop can write them again before a retry or a subsequent suite run.
    }

    private void RestoreClassState()
    {
        var failures = new List<Exception>();
        RestoreVariables(failures);
        foreach (var (key, path) in filePaths)
        {
            AttemptRestore(
                () =>
                {
                    byte[]? original = files[key];
                    if (original is null)
                    {
                        File.Delete(path);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllBytes(path, original);
                    }
                },
                $"restore {key}",
                failures);
        }

        ThrowRestorationFailures(failures);
        AttemptRestore(() => File.Delete(journalPath + ".new"), "remove the pending recovery journal", failures);
        ThrowRestorationFailures(failures);
        AttemptRestore(() => File.Delete(journalPath), "remove the recovery journal", failures);
        ThrowRestorationFailures(failures);
    }

    private void LoadJournal(byte[] contents)
    {
        Journal? journal;
        try
        {
            journal = JsonSerializer.Deserialize<Journal>(contents, JournalOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Environment Variables recovery journal is corrupt or unsupported; it has not been replaced.", exception);
        }

        if (journal is null || journal.Version != JournalVersion || journal.Files is null || journal.UserVariables is null ||
            journal.Files.Count != filePaths.Count || journal.Files.Keys.Any(key => !filePaths.ContainsKey(key)))
        {
            throw new InvalidDataException("The Environment Variables recovery journal has an unsupported version or file snapshot set; it has not been replaced.");
        }

        foreach (var (name, snapshot) in journal.UserVariables)
        {
            if (!IsValidVariableName(name) || !IsSupportedSnapshot(snapshot) || !userVariables.TryAdd(name, snapshot))
            {
                throw new InvalidDataException("The Environment Variables recovery journal contains an invalid or unsupported variable snapshot; it has not been replaced.");
            }
        }

        foreach (var (key, original) in journal.Files)
        {
            files.Add(key, original);
        }
    }

    private void SaveJournal(Dictionary<string, UserVariableSnapshot> variables)
    {
        string pendingPath = journalPath + ".new";
        try
        {
            using (var stream = new FileStream(pendingPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new Journal(JournalVersion, files, variables), JournalOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(journalPath))
            {
                File.Replace(pendingPath, journalPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(pendingPath, journalPath);
            }
        }
        catch (Exception exception)
        {
            var failures = new List<Exception> { exception };
            AttemptRestore(() => File.Delete(pendingPath), "remove the incomplete recovery journal", failures);
            if (failures.Count > 1)
            {
                throw new AggregateException("Could not persist the Environment Variables recovery journal.", failures);
            }

            throw;
        }
    }

    internal sealed record UserVariableSnapshot(string? Value, RegistryValueKind Kind);

    private sealed record Journal(
        int Version,
        Dictionary<string, byte[]?> Files,
        Dictionary<string, UserVariableSnapshot> UserVariables);
}
