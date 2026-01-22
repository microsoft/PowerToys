// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public class TopLevelHotkey(HotkeySettings? hotkey, string commandId)
{
    public string CommandId { get; set; } = commandId;

    public HotkeySettings? Hotkey { get; set; } = hotkey;
}
