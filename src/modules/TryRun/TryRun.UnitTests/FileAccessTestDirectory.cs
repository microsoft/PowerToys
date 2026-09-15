// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.UnitTests;

internal sealed class FileAccessTestDirectory : IDisposable
{
    public FileAccessTestDirectory()
    {
        // Keep the target outside the per-run workspace that a retry discards.
        WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "FileAccessFixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkingDirectory);
    }

    public string WorkingDirectory { get; }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(WorkingDirectory))
        {
            File.Delete(file);
        }

        Directory.Delete(WorkingDirectory);
    }
}
