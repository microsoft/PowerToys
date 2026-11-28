// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>An example of text that counts as a part of a copy, or that doesn't, as the editor shows by each part.</summary>
/// <param name="Text">The example, with numbers written the region's way.</param>
/// <param name="Counts">Whether <see cref="ClipboardParts.Find"/> finds it as the part.</param>
/// <param name="Why">
/// For one that doesn't count, the editor's word for why, such as <c>AddPlusOrSpaces</c>, which it shows as the resource
/// <c>TextExpander_ClipboardExample_{Why}</c>; null for one that counts.
/// </param>
public sealed record ClipboardPartExample(string Text, bool Counts, string? Why = null);
