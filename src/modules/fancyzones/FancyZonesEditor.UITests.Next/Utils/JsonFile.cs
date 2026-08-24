// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.UITests.Utils;

public sealed class JsonFile
{
    private const int Attempts = 10;
    private const int RetryDelayMs = 50;

    private readonly string path;
    private readonly string? originalContent;
    private string? stagedContent;

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

    public bool Exists => File.Exists(path);

    public void Write(string data)
    {
        stagedContent = data;
        WriteRaw(data);
    }

    public void Restage()
    {
        if (stagedContent is not null)
        {
            WriteRaw(stagedContent);
        }
    }

    public string Read()
    {
        var result = string.Empty;
        Retry(() => result = File.ReadAllText(path));
        return result;
    }

    public void Restore()
    {
        if (originalContent is null)
        {
            Retry(() => File.Delete(path));
        }
        else
        {
            WriteRaw(originalContent);
        }
    }

    private void WriteRaw(string data) => Retry(() => File.WriteAllText(path, data));

    private static void Retry(Action action)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < Attempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
    }
}