// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record HandleCommandResultMessage(ExtensionObject<ICommandResult> Result)
{
    public PageViewModel? SourcePage { get; set; }

    public AppExtensionHost? SourceExtensionHost { get; set; }

    public ICommandProviderContext? SourceProviderContext { get; set; }

    public Action? OnBeforeShowConfirmation { get; set; }

    public Func<ICommandResult, bool>? ResultHandler { get; set; }
}
