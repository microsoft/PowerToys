// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Abstraction for providers that can extract metadata and offer actions for a clipboard context.
/// </summary>
internal interface IClipboardMetadataProvider
{
    /// <summary>
    /// Gets the section title to show in the UI for this provider's metadata.
    /// </summary>
    string SectionTitle { get; }

    /// <summary>
    /// Returns true if this provider can produce metadata for the given item.
    /// </summary>
    bool CanHandle(ClipboardItem item);

    /// <summary>
    /// Returns metadata elements for the UI. Caller decides section grouping.
    /// </summary>
    IEnumerable<DetailsElement> GetDetails(ClipboardItem item);

    /// <summary>
    /// Returns context actions to be appended to MoreCommands. Use unique IDs for de-duplication.
    /// </summary>
    IEnumerable<ProviderAction> GetActions(ClipboardItem item);
}
