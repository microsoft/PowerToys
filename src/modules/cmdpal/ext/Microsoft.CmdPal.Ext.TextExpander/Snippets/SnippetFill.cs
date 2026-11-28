// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>Everything one paste of a snippet that takes part of the copy goes in with, gathered by its pick pages.</summary>
/// <param name="Answers">The answers to the snippet's questions, in the order it asks them.</param>
/// <param name="Plan">What the snippet takes from the clipboard, from the one read of it.</param>
/// <param name="Picks">The matches picked so far, one for each of <see cref="ClipboardPlan.Choices"/> in turn.</param>
internal sealed record SnippetFill(IReadOnlyList<string> Answers, ClipboardPlan Plan, IReadOnlyList<string> Picks);
