// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Storage;

/// <summary>
/// The default, file-backed <see cref="ISettingsStore"/>. Each blob key is a file name resolved under
/// a fixed base directory (the module data directory in production), so the on-disk layout is exactly
/// what the module has always written (<c>trust.json</c>, <c>config.json</c>). This is what keeps the
/// current developer/demo experience — including choosing a scripts folder — working unchanged.
///
/// A corrupt or unreadable blob is reported as <c>null</c> (absent) rather than throwing, matching the
/// module's long-standing "unreadable settings fall back to defaults" behavior.
/// </summary>
public sealed class FileSettingsStore : ISettingsStore
{
    private readonly string _baseDirectory;

    public FileSettingsStore(string baseDirectory)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? throw new ArgumentException("A base directory is required.", nameof(baseDirectory))
            : baseDirectory;
    }

    /// <summary>The directory blob keys are resolved against.</summary>
    public string BaseDirectory => _baseDirectory;

    public string? ReadBlob(string key)
    {
        var path = ResolvePath(key);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void WriteBlob(string key, string contents)
    {
        Directory.CreateDirectory(_baseDirectory);
        File.WriteAllText(ResolvePath(key), contents);
    }

    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A blob key is required.", nameof(key));
        }

        return Path.Combine(_baseDirectory, key);
    }
}
