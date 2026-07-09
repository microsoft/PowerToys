// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Sent when auto-hide registration fails because another app already owns the
/// auto-hide slot on the target edge. The dock falls back to pinned mode.
/// </summary>
public record DockAutoHideConflictMessage(bool IsConflict);
