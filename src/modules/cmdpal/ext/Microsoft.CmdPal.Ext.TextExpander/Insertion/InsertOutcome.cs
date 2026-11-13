// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>How inserting a snippet went.</summary>
internal enum InsertOutcome
{
    Inserted,

    /// <summary>The snippet came out empty, so there was nothing to insert.</summary>
    Empty,

    /// <summary>No other app came to the front once Command Palette hid.</summary>
    NoTarget,

    /// <summary>The app in front runs as administrator, or can't be inspected, so input wouldn't reach it.</summary>
    TargetOutOfReach,

    /// <summary>Another window came to the front before the snippet was all in.</summary>
    TargetChanged,

    /// <summary>
    /// A snippet with lines couldn't be pasted into a terminal, and typing them would run each one, so nothing went in.
    /// </summary>
    TerminalNeedsPaste,

    /// <summary>
    /// Text Expander is set to always type, and a snippet with lines and copied text wasn't typed into a terminal, where
    /// each line would run, so nothing went in.
    /// </summary>
    TerminalCopiedLines,

    /// <summary>Windows didn't take the key events.</summary>
    Failed,
}
