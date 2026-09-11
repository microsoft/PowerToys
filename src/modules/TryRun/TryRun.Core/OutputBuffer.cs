// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerToys.TryRun.Core;

public sealed class OutputBuffer
{
    public const int MaximumCharacters = 65536;

    private readonly StringBuilder content = new();

    public bool IsTruncated { get; private set; }

    public void Append(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var remaining = MaximumCharacters - content.Length;
        content.Append(value.AsSpan(0, Math.Min(remaining, value.Length)));
        IsTruncated |= value.Length > remaining;
    }

    public override string ToString()
    {
        return IsTruncated ? content + "\n[Output limit reached; remaining output was discarded.]" : content.ToString();
    }
}
