// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class ShowToastCommand(string message) : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        return CommandResult.ShowToast(message);
    }
}

#pragma warning restore SA1402 // File may only contain a single type
