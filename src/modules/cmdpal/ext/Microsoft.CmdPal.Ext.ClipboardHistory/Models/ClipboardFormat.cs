// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Models;

[Flags]
public enum ClipboardFormat
{
    None,
    Text = 1 << 0,
    Html = 1 << 1,
    Audio = 1 << 2,
    Image = 1 << 3,
    ImageFile = 1 << 4,
}
