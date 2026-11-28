// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Models;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

/// <summary>
/// What a paste put on the clipboard: plain text, or a bitmap (<see cref="Text"/> is <see langword="null"/>).
/// </summary>
internal sealed record PastedContent(ClipboardFormat Format, string? Text);
