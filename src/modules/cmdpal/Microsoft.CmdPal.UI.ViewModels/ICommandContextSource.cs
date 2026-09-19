// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Supplies invocation ownership for host-owned items. Presentation wrappers forward
/// these values from their source, or return null to inherit the current page's context.
/// </summary>
public interface ICommandContextSource
{
    AppExtensionHost? ExtensionHost { get; }

    ICommandProviderContext? ProviderContext { get; }
}
