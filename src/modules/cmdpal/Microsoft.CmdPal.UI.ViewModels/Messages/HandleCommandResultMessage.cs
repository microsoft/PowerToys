// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record HandleCommandResultMessage
{
    public ExtensionObject<ICommandResult> Result { get; }

    public SourceContext? Context { get; private set; }

    public Action? OnBeforeShowConfirmation { get; set; }

    public Func<ICommandResult, bool>? ResultHandler { get; set; }

    public HandleCommandResultMessage(ExtensionObject<ICommandResult> result, PageViewModel? sourcePage = null)
    {
        Result = result;
        Context = sourcePage is null ? null : new(sourcePage);
    }
}
