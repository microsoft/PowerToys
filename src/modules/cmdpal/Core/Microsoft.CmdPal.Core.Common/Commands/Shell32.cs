// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Core.Common.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.Common.Commands;

public static partial class Shell32
{
    [LibraryImport("SHELL32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShellExecuteEx(ref SHELLEXECUTEINFOW lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFOW
    {
        public uint CbSize;
        public uint FMask;
        public IntPtr Hwnd;

        public IntPtr LpVerb;
        public IntPtr LpFile;
        public IntPtr LpParameters;
        public IntPtr LpDirectory;
        public int Show;
        public IntPtr HInstApp;
        public IntPtr LpIDList;
        public IntPtr LpClass;
        public IntPtr HkeyClass;
        public uint DwHotKey;
        public IntPtr HIconOrMonitor;
        public IntPtr Process;
    }
}
