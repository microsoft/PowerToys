// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power;

internal sealed partial class FallbackPowerItem : FallbackCommandItem
{
    private const string FallbackId = "com.microsoft.cmdpal.builtin.power.fallback";

    private readonly PowerListPage _listPage;

    public FallbackPowerItem(PowerListPage listPage)
        : base(listPage, Resources.power_fallback_title, FallbackId)
    {
        _listPage = listPage;
        Clear();
        Icon = Icons.PowerExtensionIcon;
    }

    public override void UpdateQuery(string query)
    {
        if (!PowerFallbackQueryMatcher.Matches(query))
        {
            Clear();
            return;
        }

        Command = _listPage;
        Title = Resources.power_fallback_title;
        Subtitle = Resources.power_fallback_subtitle;
        Icon = Icons.PowerExtensionIcon;
    }

    private void Clear()
    {
        Command = null;
        Title = string.Empty;
        Subtitle = string.Empty;
    }
}
