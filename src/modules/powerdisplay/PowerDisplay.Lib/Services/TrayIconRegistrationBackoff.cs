// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Provides the capped retry sequence for notification-icon registration recovery.
/// </summary>
public sealed class TrayIconRegistrationBackoff
{
    private static readonly int[] DelayMilliseconds = [250, 500, 1000, 2000, 5000];

    private int _index;

    /// <summary>
    /// Gets the next retry delay, capped at five seconds.
    /// </summary>
    /// <returns>The next retry delay.</returns>
    public TimeSpan NextDelay()
    {
        var delay = DelayMilliseconds[_index];
        if (_index < DelayMilliseconds.Length - 1)
        {
            _index++;
        }

        return TimeSpan.FromMilliseconds(delay);
    }

    /// <summary>
    /// Restarts the sequence at 250 milliseconds.
    /// </summary>
    public void Reset()
    {
        _index = 0;
    }
}
