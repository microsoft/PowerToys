// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Asks the dashboard to show itself, or hide itself if it's already showing.
/// </summary>
/// <param name="FromHotkey">
/// True when the user pressed the dashboard shortcut. Showing from the shortcut honors
/// the "ignore while a full-screen app is running" setting.
/// </param>
public record ToggleDashboardMessage(bool FromHotkey = false);
