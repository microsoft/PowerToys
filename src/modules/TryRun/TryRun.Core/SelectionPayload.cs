// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerToys.TryRun.Core;

public static class SelectionPayload
{
    public const string InputSwitch = "--selection-stdin";
    public const int MaximumCharacters = 200000;

    public static string Encode(IEnumerable<string> paths)
    {
        var selection = TaskBundle.ParseLaunchArguments(paths);
        if (selection.Length == 0)
        {
            throw new ArgumentException("Select at least one file or folder.");
        }

        var json = JsonSerializer.Serialize(new Payload(1, selection));
        if (json.Length > MaximumCharacters)
        {
            throw new ArgumentException("The selection is too large. Select a containing folder instead.");
        }

        return json;
    }

    public static string[] Decode(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumCharacters)
        {
            throw new ArgumentException("The selection is too large.");
        }

        var payload = JsonSerializer.Deserialize<Payload>(json);
        if (payload is null || payload.Version != 1 || payload.Paths is null || payload.Paths.Length == 0)
        {
            throw new ArgumentException("The selection message is missing or unsupported.");
        }

        // Do not accept launch switches in a selection supplied by Explorer.
        if (payload.Paths[0] == "--")
        {
            throw new ArgumentException("A selection message can contain only file paths.");
        }

        return TaskBundle.ParseLaunchArguments(payload.Paths);
    }

    public static async Task<string[]> ReadAsync(TextReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var buffer = new char[MaximumCharacters + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await reader.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return Decode(new string(buffer, 0, length));
            }

            length += count;
        }

        throw new ArgumentException("The selection is too large.");
    }

    private sealed record Payload(int Version, string[] Paths);
}
