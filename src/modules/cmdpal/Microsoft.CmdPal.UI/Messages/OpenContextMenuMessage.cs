// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>
/// Requests opening a context menu in the palette.
/// </summary>
/// <param name="Request">The context and optional presentation overrides to open.</param>
/// <remarks>Send on the UI thread.</remarks>
public record OpenContextMenuMessage(ContextMenuRequest Request);
