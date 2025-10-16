// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Messages;

/// <summary>
/// Message to request hiding the window.
///
/// Yes, it's a little weird that this lives in the ClipboardHistory extension.
/// Until we need it somewhere else, this is good enough.
/// </summary>
public partial record HideWindowMessage()
{
}
