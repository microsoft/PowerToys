// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

/// <summary>
/// The parts of a clipboard history entry needed to find the entries a paste created.
/// </summary>
internal sealed record ClipboardHistoryEntry(string Id, DateTimeOffset Timestamp, string? Text, bool HasBitmap);
