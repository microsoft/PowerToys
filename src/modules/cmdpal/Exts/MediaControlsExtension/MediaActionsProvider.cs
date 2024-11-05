// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace MediaControlsExtension;

public partial class MediaActionsProvider : CommandProvider
{
    public MediaActionsProvider()
    {
        DisplayName = "Media controls actions";
    }

    private readonly IListItem[] _actions = [
        new MediaListItem()
    ];

    public override IListItem[] TopLevelCommands()
    {
        return _actions;
    }
}
