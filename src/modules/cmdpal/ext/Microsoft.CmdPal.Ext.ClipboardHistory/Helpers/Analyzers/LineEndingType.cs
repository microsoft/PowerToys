// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal enum LineEndingType
{
    None,
    Windows, // \r\n (CRLF)
    Unix, // \n (LF)
    Mac, // \r (CR)
    Mixed,
}
