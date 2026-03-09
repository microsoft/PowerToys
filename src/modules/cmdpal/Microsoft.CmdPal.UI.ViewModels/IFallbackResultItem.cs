// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public interface IFallbackResultItem
{
    string FallbackSourceId { get; }

    string ExtensionName { get; }

    bool HasAlias { get; }

    string AliasText { get; }

    AppExtensionHost ExtensionHost { get; }

    ICommandProviderContext ProviderContext { get; }

    IFallbackCommandInvocationArgs? InvocationArgs { get; }

    bool IsCurrent { get; }
}
