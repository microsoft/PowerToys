// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>A clock that moves only when slept on, and tells the test each time it does.</summary>
internal sealed class FakeClock : IInsertionClock
{
    public long ElapsedMilliseconds { get; private set; }

    /// <summary>Gets or sets what changes as time passes: called with the new time after every sleep.</summary>
    public Action<long>? Tick { get; set; }

    public long TotalSlept { get; private set; }

    public void Sleep(int milliseconds)
    {
        // A real sleep lasts at least a moment, so a test can't spin forever on zero.
        var step = Math.Max(1, milliseconds);
        ElapsedMilliseconds += step;
        TotalSlept += step;
        Tick?.Invoke(ElapsedMilliseconds);
        if (ElapsedMilliseconds > 60_000)
        {
            throw new InvalidOperationException("The code under test waited more than a minute.");
        }
    }
}
