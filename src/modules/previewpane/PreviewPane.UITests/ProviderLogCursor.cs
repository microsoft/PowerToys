// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.PreviewPane.UITests;

internal sealed class ProviderLogCursor
{
    private readonly string directory;
    private readonly Dictionary<string, long> offsets;

    internal ProviderLogCursor(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = directory;
        offsets = LogFiles().ToDictionary(path => path, path => new FileInfo(path).Length, StringComparer.OrdinalIgnoreCase);
    }

    internal string ReadNewText() =>
        string.Join(Environment.NewLine, LogFiles().Select(ReadNewText).Where(text => text.Length > 0));

    private string[] LogFiles() =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, "*.log", SearchOption.TopDirectoryOnly) : [];

    private string ReadNewText(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (offsets.TryGetValue(path, out long offset) && offset <= stream.Length)
                {
                    stream.Position = offset;
                }

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
        }
    }
}
