// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Detects three shortcut presses within two seconds.</summary>
/// <remarks>Uses input timestamps so UI dispatch delays do not change the breakthrough window.</remarks>
internal sealed class ShortcutBreakthroughDetector
{
    private readonly Queue<long> _presses = new(3);

    /// <summary>Records a press and resets the sequence when breakthrough is triggered.</summary>
    /// <param name="timestamp">Original input timestamp in <see cref="Stopwatch.GetTimestamp"/> ticks.</param>
    /// <returns>Whether this press triggers breakthrough.</returns>
    public bool RegisterPress(long timestamp)
    {
        while (_presses.TryPeek(out var oldest) &&
            (timestamp < oldest || timestamp - oldest > 2 * Stopwatch.Frequency))
        {
            _presses.Dequeue();
        }

        _presses.Enqueue(timestamp);
        if (_presses.Count < 3)
        {
            return false;
        }

        Reset();
        return true;
    }

    /// <summary>Discards the current sequence of presses.</summary>
    public void Reset() => _presses.Clear();
}
