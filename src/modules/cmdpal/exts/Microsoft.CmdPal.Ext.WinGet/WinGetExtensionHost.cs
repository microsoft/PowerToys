// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.Ext.WinGet;

public partial class WinGetExtensionHost
{
    internal static ExtensionHostInstance Instance { get; } = new();
}
