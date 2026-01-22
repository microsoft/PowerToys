// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Represents an action exposed by a metadata provider.
/// </summary>
/// <param name="Id">Unique identifier for de-duplication (case-insensitive).</param>
/// <param name="Action">The actual context menu item to be shown.</param>
internal readonly record struct ProviderAction(string Id, CommandContextItem Action);
