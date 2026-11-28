// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Spots only Text Expander can fill in, because they need its own window: a clipboard variable that picks a copy
/// from clipboard history when the snippet runs (<c>{{clipboard:pick}}</c>, <c>{{clipboard:pick:email}}</c> and the
/// like). Command Palette lists these snippets but leaves inserting them to Text Expander. A part of the copy with
/// several matches isn't one of these: Command Palette asks for it on a page of its own (<see cref="Pages.SnippetPicksPage"/>).
/// </summary>
internal static class TextExpanderOnlyVariables
{
    public static bool AreUsedIn(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        return replacement.Contains("{{clipboard:", StringComparison.Ordinal)
            && SnippetTemplate.Parse(replacement).Any(token => token.Kind == SnippetTokenKind.Clipboard && token.Clipboard.PickCopy);
    }
}
