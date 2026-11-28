// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>The key events that type one character, sent together.</summary>
/// <param name="Strokes">The key events.</param>
/// <param name="IsKey">True for Enter or Tab, which go as real key presses.</param>
/// <param name="Character">The character typed, or the first half of a surrogate pair.</param>
internal readonly record struct TypedCharacter(IReadOnlyList<KeyStroke> Strokes, bool IsKey, char Character)
{
    /// <summary>
    /// Gets a value indicating whether to pause longer after this character: editors do extra work at the end of a
    /// sentence and at line breaks (autocorrect, capitals), which is where Text Expander found typed characters drop.
    /// </summary>
    public bool PausesLonger => Character is '.' or '!' or '?' or '\n';
}
