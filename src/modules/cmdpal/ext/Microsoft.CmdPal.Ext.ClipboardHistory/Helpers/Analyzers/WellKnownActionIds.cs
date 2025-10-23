// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Well-known action id constants used to de-duplicate provider actions.
/// </summary>
internal static class WellKnownActionIds
{
    public const string Open = "open";
    public const string OpenLocation = "openLocation";
    public const string CopyPath = "copyPath";
    public const string OpenConsole = "openConsole";
}
