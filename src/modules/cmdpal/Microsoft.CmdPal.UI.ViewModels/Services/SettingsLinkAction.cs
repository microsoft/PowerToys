// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Post-navigation behavior for a settings link.</summary>
public enum SettingsLinkAction
{
    /// <summary>No post-navigation action.</summary>
    None,

    /// <summary>Open the fallback-order dialog.</summary>
    OpenFallbackOrder,
}
