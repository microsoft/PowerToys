// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Provides access to the shared root page instance without forcing consumers
/// to depend directly on <see cref="IRootPageService"/> during construction.
/// </summary>
public interface IRootPageAccessor
{
    /// <summary>
    /// Gets the shared root page instance for this Command Palette session.
    /// Implementations may create the page lazily and may be invoked during
    /// built-in provider loading before the shell captures its own root-page reference.
    /// </summary>
    IPage GetRootPage();
}
