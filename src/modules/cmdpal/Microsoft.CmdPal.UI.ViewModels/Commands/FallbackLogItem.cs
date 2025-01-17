// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Commands;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackLogItem : FallbackCommandItem
{
    private readonly LogMessagesPage _logMessagesPage;

    public FallbackLogItem()
        : base(new LogMessagesPage())
    {
        _logMessagesPage = (LogMessagesPage)Command!;
        Title = string.Empty;
        _logMessagesPage.Name = string.Empty;
        Subtitle = "View log messages";
    }

    public override void UpdateQuery(string query)
    {
        _logMessagesPage.Name = query.StartsWith('l') ? "View log" : string.Empty;
        Title = _logMessagesPage.Name;
    }
}
