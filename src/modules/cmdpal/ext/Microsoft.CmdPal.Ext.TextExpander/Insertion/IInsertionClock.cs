// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>The clock waits are timed by, so tests don't have to wait.</summary>
internal interface IInsertionClock
{
    long ElapsedMilliseconds { get; }

    void Sleep(int milliseconds);
}

/// <summary>The real clock.</summary>
internal sealed class SystemInsertionClock : IInsertionClock
{
    public long ElapsedMilliseconds => Environment.TickCount64;

    public void Sleep(int milliseconds) => Thread.Sleep(milliseconds);
}
