// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class KeyboardManagerSettingsScope : IDisposable
{
    private readonly Dictionary<string, FileSnapshot> snapshots;
    private bool disposed;

    public KeyboardManagerSettingsScope()
    {
        snapshots = KeyboardManagerSettings.ManagedPaths.ToDictionary(path => path, FileSnapshot.Capture);
        try
        {
            KeyboardManagerSettings.ConfigureUnifiedEditorBaseline();
        }
        catch (Exception setupException)
        {
            try
            {
                RestoreSettings();
            }
            catch (Exception restoreException)
            {
                throw new AggregateException(
                    "Keyboard Manager test setup failed and its settings could not be fully restored.",
                    setupException,
                    restoreException);
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!KeyboardManagerTestBase.CloseEditor())
        {
            throw new InvalidOperationException("The Keyboard Manager editor process survived class cleanup; settings were not restored to avoid a write race.");
        }

        RestoreSettings();
    }

    private void RestoreSettings()
    {
        var failures = new List<Exception>();
        foreach (var (path, snapshot) in snapshots)
        {
            try
            {
                snapshot.Restore(path);
            }
            catch (Exception exception)
            {
                failures.Add(new IOException($"Could not restore '{path}'.", exception));
            }
        }

        try
        {
            KeyboardManagerSettings.SignalSettingsChanged();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Keyboard Manager settings could not be fully restored.", failures);
        }
    }

    private sealed record FileSnapshot(bool Existed, byte[]? Content)
    {
        public static FileSnapshot Capture(string path) =>
            File.Exists(path)
                ? new FileSnapshot(true, File.ReadAllBytes(path))
                : new FileSnapshot(false, null);

        public void Restore(string path)
        {
            if (!Existed)
            {
                File.Delete(path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Content!);
        }
    }
}
