// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A visual ancestor that hosts a <see cref="SearchBar"/>, such as the palette's shell page
/// or a dock page flyout.
/// </summary>
internal interface ISearchBarHost
{
    /// <summary>
    /// Gets a value indicating whether the host is on screen, so the search bar can take
    /// keyboard focus without stealing it from another window.
    /// </summary>
    bool CanFocusSearchBar { get; }
}
