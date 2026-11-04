// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>One clipboard format and the bytes that were on the clipboard for it.</summary>
internal readonly record struct ClipboardEntry(uint Format, byte[] Data);
