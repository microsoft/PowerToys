// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

public partial class SelfImmolateCommand : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        Process.GetCurrentProcess().Kill();
        return CommandResult.KeepOpen();
    }
}
