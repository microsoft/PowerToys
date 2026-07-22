// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>
/// Asks the host window to temporarily make the visible card fill the entire window,
/// ignoring the compact-mode clamps, so a modal dialog (e.g. a confirmation) isn't clipped
/// by the card's HWND region. Sent with <see cref="Maximize"/> = <see langword="true"/> while
/// the dialog is showing and <see langword="false"/> once it closes to restore the normal
/// compact/expanded behavior.
/// </summary>
public record MaximizeForDialogMessage(bool Maximize);
