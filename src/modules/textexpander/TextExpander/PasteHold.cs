// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// How the paste's Ctrl hold ended: whether the target was seen reading the clipboard, and how long after the V. The settle
/// carries on from there, so the hold doesn't add to the time the clipboard is borrowed.
/// </summary>
internal readonly record struct PasteHold(bool ReadSeen, long ElapsedMs);
