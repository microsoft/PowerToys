// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal interface IFallbackInvocationContext
{
    AppExtensionHost ExtensionHost { get; }

    ICommandProviderContext ProviderContext { get; }

    object? InvocationContext { get; }

    ICommand? ResolveCommand(ICommand requestedCommand);
}
