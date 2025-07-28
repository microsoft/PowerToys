// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class OpenUrlWithHistoryCommand : OpenUrlCommand
{
    private readonly Action<string>? _addToHistory;
    private readonly string _url;

    public OpenUrlWithHistoryCommand(string url, Action<string>? addToHistory = null)
        : base(url)
    {
        _addToHistory = addToHistory;
        _url = url;
    }

    public override CommandResult Invoke()
    {
        _addToHistory?.Invoke(_url);
        var result = base.Invoke();
        return result;
    }
}
