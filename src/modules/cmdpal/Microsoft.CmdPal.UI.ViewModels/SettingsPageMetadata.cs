// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

// Snapshot of the provider's current SettingsPage signature.
// Do not rely on SettingsPageCommand identity alone: the toolkit SDK currently
// returns a fresh SettingsContentPage on each Settings.SettingsPage access, and
// those pages usually inherit the default empty Command.Id.
public sealed record SettingsPageMetadata(
    IContentPage SettingsPageCommand,
    Type SettingsPageType,
    string PageId,
    string PageName);
