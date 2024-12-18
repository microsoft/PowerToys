// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public class CommandAlias(string shortcut, string commandId, bool direct = false)
{
    public string CommandId { get; set; } = commandId;

    public string Alias { get; set; } = shortcut;

    public bool IsDirect { get; set; } = direct;

    public string SearchPrefix => Alias + (IsDirect ? string.Empty : " ");
}
