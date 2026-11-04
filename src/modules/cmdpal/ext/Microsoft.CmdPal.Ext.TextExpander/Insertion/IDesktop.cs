// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>The windows and keyboard a snippet goes into.</summary>
internal interface IDesktop
{
    /// <summary>Gets the window in front, or zero.</summary>
    nint ForegroundWindow { get; }

    /// <summary>Gets this process's id: Command Palette's own windows aren't a target.</summary>
    uint CurrentProcessId { get; }

    /// <summary>Gets the Shift, Ctrl, Alt and Windows keys the user is holding, as left- or right-hand virtual keys.</summary>
    IReadOnlyList<ushort> HeldModifiers();

    uint ProcessIdOf(nint window);

    /// <summary>
    /// True when input sent from here reaches <paramref name="window"/>. Windows silently drops input to a window
    /// running as administrator, and, as in Text Expander, a snippet never goes above medium integrity.
    /// </summary>
    bool CanDrive(nint window);

    /// <summary>True when <paramref name="window"/>'s app answers messages, or is gone. A busy app still has a paste queued.</summary>
    bool IsTakingMessages(nint window);

    /// <summary>Sends <paramref name="strokes"/> in one call, so nothing else gets between them. False unless all went.</summary>
    bool Send(IReadOnlyList<KeyStroke> strokes);

    /// <summary>Hides Command Palette, so the app it was opened over comes back to the front.</summary>
    void HideCommandPalette();
}
