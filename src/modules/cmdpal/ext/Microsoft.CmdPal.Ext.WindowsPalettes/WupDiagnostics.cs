// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal static class WupDiagnostics
{
    internal static void Write(string message) => Logger.LogInfo($"Windows Palettes: {message}");
}