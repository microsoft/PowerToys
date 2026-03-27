// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Represents abstract host window functionality.
/// </summary>
public interface IHostWindow
{
    /// <summary>
    /// Gets a value indicating whether the window is visible to the user, taking account not only window visibility but also cloaking.
    /// </summary>
    bool IsVisibleToUser { get; }
}
