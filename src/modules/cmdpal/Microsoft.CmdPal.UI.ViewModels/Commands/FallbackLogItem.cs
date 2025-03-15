// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackLogItem : FallbackCommandItem
{
    private readonly LogMessagesPage _logMessagesPage;

    public FallbackLogItem()
        : base(new LogMessagesPage(), Resources.builtin_log_subtitle)
    {
        _logMessagesPage = (LogMessagesPage)Command!;
        Title = string.Empty;
        _logMessagesPage.Name = string.Empty;
        Subtitle = Properties.Resources.builtin_log_subtitle;
    }

    public override void UpdateQuery(string query)
    {
        _logMessagesPage.Name = query.StartsWith('l') ? Properties.Resources.builtin_log_title : string.Empty;
        Title = _logMessagesPage.Name;
    }
}
