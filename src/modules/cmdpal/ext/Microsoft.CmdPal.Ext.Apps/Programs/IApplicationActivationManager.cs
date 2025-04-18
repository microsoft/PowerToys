// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

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
[ComImport]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IApplicationActivationManager
{
    IntPtr ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);

    IntPtr ActivateForFile([In] string appUserModelId, [In] IntPtr /*IShellItemArray* */ itemArray, [In] string verb, [Out] out uint processId);

    IntPtr ActivateForProtocol([In] string appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out uint processId);
}

// Application Activation Manager Class
[ComImport]
[Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
public class ApplicationActivationManager : IApplicationActivationManager
{
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)/*, PreserveSig*/]
    public extern IntPtr ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public extern IntPtr ActivateForFile([In] string appUserModelId, [In] IntPtr /*IShellItemArray* */ itemArray, [In] string verb, [Out] out uint processId);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    public extern IntPtr ActivateForProtocol([In] string appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out uint processId);
}
