// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

// Reference : https://github.com/MicrosoftEdge/edge-launcher/blob/108e63df0b4cb5cd9d5e45aa7a264690851ec51d/MIcrosoftEdgeLauncherCsharp/Program.cs
[Flags]
public enum ActivateOptions
{
    None = 0x00000000,
    DesignMode = 0x00000001,
    NoErrorUI = 0x00000002,
    NoSplashScreen = 0x00000004,
}

// ApplicationActivationManager
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
public partial interface IApplicationActivationManager
{
    void ActivateApplication(string appUserModelId, string arguments, ActivateOptions options, out uint processId);

    void ActivateForFile(string appUserModelId, IntPtr /*IShellItemArray* */ itemArray, string verb, out uint processId);

    void ActivateForProtocol(string appUserModelId, IntPtr /* IShellItemArray* */itemArray, out uint processId);
}
