// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal interface IClipboardHistorySource : IDisposable
{
    event EventHandler? HistoryChanged;

    event EventHandler? HistoryEnabledChanged;

    Task<IReadOnlyList<ClipboardHistoryEntry>> ReadAsync(CancellationToken cancellationToken);
}

internal sealed record ClipboardHistoryEntry(string Id, Func<CancellationToken, Task<IListItem>> LoadAsync);
