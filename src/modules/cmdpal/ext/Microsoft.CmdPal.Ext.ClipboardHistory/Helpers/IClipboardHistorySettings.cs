// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal interface IClipboardHistorySettings : ISettingOptions
{
    event EventHandler? Changed;

    PrimaryAction PrimaryAction { get; }
}
