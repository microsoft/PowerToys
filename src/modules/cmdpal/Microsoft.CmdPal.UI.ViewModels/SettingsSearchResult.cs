// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial record SettingsSearchResult(
    string Title,
    string Breadcrumb,
    OpenSettingsMessage? Destination,
    string Description = "",
    string Keywords = "",
    string IconGlyph = "\uE713",
    IconInfoViewModel? Icon = null,
    bool IsShowAllResults = false,
    string GroupName = "",
    string AssignedHotkey = "",
    string Category = "")
{
    public bool HasIcon => Icon?.IsSet == true;

    public override string ToString()
    {
        var text = string.IsNullOrEmpty(Breadcrumb) ? Title : $"{Title}, {Breadcrumb}";
        return string.IsNullOrEmpty(Category) ? text : $"{text}, {Category}";
    }
}
