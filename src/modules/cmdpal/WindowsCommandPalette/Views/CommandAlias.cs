// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WindowsCommandPalette.Views;

public class CommandAlias(string shortcut, string commandId, bool direct = false)
{
    public string CommandId { get; set; } = commandId;

    public string Alias { get; set; } = shortcut;

    public bool IsDirect { get; set; } = direct;

    public string SearchPrefix => Alias + (IsDirect ? string.Empty : " ");
}
