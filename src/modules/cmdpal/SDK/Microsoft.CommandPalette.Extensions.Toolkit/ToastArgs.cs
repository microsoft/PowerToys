// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ToastArgs : IToastArgs
{
    public string? Message { get; set; }

    public ICommandResult? Result { get; set; } = CommandResult.Dismiss();
}
