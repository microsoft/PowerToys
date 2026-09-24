// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZones.UITests.Utils;

/// <summary>
/// Snapshot/restore wrapper around one FancyZones JSON file.
/// </summary>
/// <remarks>
/// Ported from the legacy <c>FancyZonesEditor.UITests/Utils/IOTestHelper.cs</c>. Two deliberate
/// changes: it uses <see cref="File"/> directly instead of <c>System.IO.Abstractions</c> (the
/// <c>.Next</c> harness adds no third-party dependencies), and each retry loop stops on the first
/// success instead of writing ten times in a row.
/// </remarks>
public sealed class JsonFile
{
    private const int Attempts = 10;
    private const int RetryDelayMs = 50;

    private readonly string path;
    private readonly string? originalContent;

    public JsonFile(string path)
    {
        this.path = path;

        if (File.Exists(path))
        {
            originalContent = Read();
        }
        else
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    public string FilePath => path;

    public bool Exists => File.Exists(path);

    /// <summary>Put the file back the way the test found it (deleting it when the test created it).</summary>
    public void Restore()
    {
        if (string.IsNullOrEmpty(originalContent))
        {
            Delete();
        }
        else
        {
            Write(originalContent);
        }
    }

    public void Write(string data) => Retry(() => File.WriteAllText(path, data));

    public string Read()
    {
        var result = string.Empty;
        Retry(() => result = File.ReadAllText(path));
        return result;
    }

    public void Delete() => Retry(() =>
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    });

    private static void Retry(Action action)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
    }
}
