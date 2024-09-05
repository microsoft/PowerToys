// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;

namespace MediaControlsExtension;

public class MediaActionsProvider : ICommandProvider
{
    public string DisplayName => $"Media controls actions";

    public IconDataType Icon => new(string.Empty);

    private readonly IListItem[] _actions = [
        new MediaListItem()
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return _actions;
    }
}
