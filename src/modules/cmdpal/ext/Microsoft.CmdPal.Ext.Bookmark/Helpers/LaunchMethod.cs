// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public enum LaunchMethod
{
    ShellExecute, // UseShellExecute = true (Explorer/associations/protocols)
    ExplorerOpen, // explorer.exe <folder/shell:uri>
    ActivateAppId, // IApplicationActivationManager (AUMID / pkgfamily!app)
}
