// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>External-command consent metadata.</summary>
/// <param name="Route">The requested protocol route.</param>
/// <param name="Permission">The command identity and display metadata.</param>
/// <param name="IsPage">Whether the command opens a page.</param>
/// <param name="IsReload">Whether the command reloads extensions.</param>
/// <param name="ListPageOptions">Optional list-page state.</param>
/// <param name="CanRemember">Whether persistent consent is permitted.</param>
/// <param name="Icon">Optional command preview icon.</param>
internal sealed record ExternalCommandConsentRequest(
    CmdPalProtocolRoute Route,
    ExternalCommandPermission Permission,
    bool IsPage,
    bool IsReload,
    ListPageLaunchOptions? ListPageOptions = null,
    bool CanRemember = true,
    IconInfoViewModel? Icon = null);
