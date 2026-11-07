// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The Text Expander files as they were before a test. Disposing it puts them back byte for byte, removes the
/// database's backups and staging files that the test left, and deletes files that weren't there.
/// </summary>
internal sealed class TextExpanderDataSnapshot : IDisposable
{
    private readonly Dictionary<string, byte[]?> files;
    private readonly HashSet<string> existingNames;
    private bool disposed;

    private TextExpanderDataSnapshot(Dictionary<string, byte[]?> files, HashSet<string> existingNames)
    {
        this.files = files;
        this.existingNames = existingNames;
    }

    public static TextExpanderDataSnapshot Capture()
    {
        var files = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
        {
            [TextExpanderFiles.SettingsPath] = TextExpanderFiles.ReadAllBytesShared(TextExpanderFiles.SettingsPath),
            [TextExpanderFiles.SnippetsPath] = TextExpanderFiles.ReadAllBytesShared(TextExpanderFiles.SnippetsPath),
        };

        var names = Directory.Exists(TextExpanderFiles.Folder)
            ? Directory.GetFiles(TextExpanderFiles.Folder).Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new TextExpanderDataSnapshot(files, names);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        var failures = new List<Exception>();
        if (Directory.Exists(TextExpanderFiles.Folder))
        {
            foreach (var path in Directory.GetFiles(TextExpanderFiles.Folder))
            {
                var name = Path.GetFileName(path);
                bool leftByTest = !existingNames.Contains(name) && !files.ContainsKey(path) &&
                    (name.StartsWith(TextExpanderFiles.SnippetsFileName, StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("settings.json", StringComparison.OrdinalIgnoreCase));
                if (leftByTest)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        failures.Add(ex);
                    }
                }
            }
        }

        foreach (var (path, content) in files)
        {
            try
            {
                if (content is null)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                else
                {
                    TextExpanderFiles.WriteAtomically(path, content);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("The Text Expander files could not be fully restored.", failures);
        }
    }
}
