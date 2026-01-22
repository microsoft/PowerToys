// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Common.Models;

public class TopLevelHotkey(Hotkey? hotkey, string commandId)
{
    public string CommandId { get; set; } = commandId;

    public Hotkey? Hotkey { get; set; } = hotkey;
}
