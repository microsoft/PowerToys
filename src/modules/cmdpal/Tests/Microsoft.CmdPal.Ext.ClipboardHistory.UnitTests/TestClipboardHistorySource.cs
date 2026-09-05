// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

internal sealed partial class TestClipboardHistorySource : IClipboardHistorySource
{
    private EventHandler _historyChanged;
    private EventHandler _historyEnabledChanged;

    public event EventHandler HistoryChanged
    {
        add => _historyChanged += value;
        remove => _historyChanged -= value;
    }

    public event EventHandler HistoryEnabledChanged
    {
        add => _historyEnabledChanged += value;
        remove => _historyEnabledChanged -= value;
    }

    public Func<CancellationToken, Task<IReadOnlyList<ClipboardHistoryEntry>>> Read { get; set; } = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([]);

    public int HistorySubscribers => _historyChanged?.GetInvocationList().Length ?? 0;

    public int EnabledSubscribers => _historyEnabledChanged?.GetInvocationList().Length ?? 0;

    public bool IsDisposed { get; private set; }

    public Task<IReadOnlyList<ClipboardHistoryEntry>> ReadAsync(CancellationToken cancellationToken) => Read(cancellationToken);

    public void RaiseHistoryChanged() => _historyChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseHistoryEnabledChanged() => _historyEnabledChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;
}
