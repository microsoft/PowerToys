// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What one paste of a snippet takes from the clipboard, worked out by <see cref="SnippetText.PlanClipboard(string, IReadOnlyList{string}, string)"/>.</summary>
/// <param name="Copy">The clipboard's text, read once, or null when it held none.</param>
/// <param name="Choices">One choice for each spot whose part has several matches, in the order Text Expander asks them.</param>
/// <param name="Gaps">The spots Core leaves empty because <paramref name="Copy"/> has nothing for them, in the snippet's order.</param>
internal sealed record ClipboardPlan(string? Copy, IReadOnlyList<ClipboardChoice> Choices, IReadOnlyList<ClipboardGap> Gaps);
