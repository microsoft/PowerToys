// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>Reads text and keeps each change with when it was seen, so a failure shows what happened after typing.</summary>
internal sealed class TextHistory
{
    private readonly Func<string?> read;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<string> changes = [];
    private string? last;

    public TextHistory(Func<string?> read) => this.read = read;

    /// <summary>Gets <see cref="Environment.TickCount64"/> when reading began, the zero of the times shown.</summary>
    public long StartTick { get; } = Environment.TickCount64;

    public string? Read()
    {
        var text = read();
        if (changes.Count == 0 || text != last)
        {
            changes.Add($"'{TextExpanderFiles.Visible(text)}' at {clock.ElapsedMilliseconds} ms");
            last = text;
        }

        return text;
    }

    public override string ToString() => changes.Count == 0 ? "<not read>" : string.Join(", then ", changes);
}
