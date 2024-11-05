// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace TemplateExtension;

public partial class TemplateExtensionActionsProvider : CommandProvider
{
    public TemplateExtensionActionsProvider()
    {
        DisplayName = "TemplateDisplayName Commands";
    }

    private readonly IListItem[] _commands = [
        new ListItem(new TemplateExtensionPage()),
    ];

    public override IListItem[] TopLevelCommands()
    {
        return _commands;
    }
}
