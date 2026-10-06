// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI;

internal sealed class RunHistoryService : IRunHistoryService
{
    private readonly IAppStateService _appStateService;

    public RunHistoryService(IAppStateService appStateService)
    {
        _appStateService = appStateService;
    }

    public IReadOnlyList<string> GetRunHistory()
    {
        if (_appStateService.State.RunHistory.IsEmpty)
        {
            var history = CreateRunHistory();

            _appStateService.UpdateState(state => state with
            {
                RunHistory = history.ToImmutableList(),
            });
        }

        return _appStateService.State.RunHistory;
    }

    public void ClearRunHistory()
    {
        _appStateService.UpdateState(state => state with
        {
            RunHistory = ImmutableList<string>.Empty,
        });
    }

    public void AddRunHistoryItem(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return;
        }

        _appStateService.UpdateState(state => state with
        {
            RunHistory = state.RunHistory
                .Remove(item)
                .Insert(0, item),
        });
    }

    public long RunCommand(string commandLine, string workingDir, bool asAdmin, ulong hwnd)
    {
        return RunHistory.ExecuteCommandline(commandLine, workingDir, hwnd, asAdmin);
    }

    public ParseCommandlineResult ParseCommandline(string commandLine, string workingDirectory)
    {
        return RunHistory.ParseCommandline(commandLine, workingDirectory);
    }

    public string QualifyCommandLineDirectory(string commandLine, string fullFilePath, string defaultDirectory)
    {
        return RunHistory.QualifyCommandLineDirectory(commandLine, fullFilePath, defaultDirectory);
    }

    private static IReadOnlyList<string> CreateRunHistory()
    {
        const string runMruKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";
        const int mruCacheWrite = 0x0001;

        var history = new List<string>();

        try
        {
            var hLib = NativeMethods.LoadLibraryExW("comctl32.dll", nint.Zero, NativeMethods.LoadLibrarySearchSystemDirs);
            if (hLib == nint.Zero)
            {
                return history;
            }

            try
            {
                var createMruListAddr = NativeMethods.GetProcAddress(hLib, "CreateMRUListW");
                var enumMruListAddr = NativeMethods.GetProcAddress(hLib, "EnumMRUListW");
                var freeMruListAddr = NativeMethods.GetProcAddress(hLib, "FreeMRUList");
                if (createMruListAddr == nint.Zero || enumMruListAddr == nint.Zero || freeMruListAddr == nint.Zero)
                {
                    return history;
                }

                var createMruList = Marshal.GetDelegateForFunctionPointer<NativeMethods.CreateMruListWDelegate>(createMruListAddr);
                var enumMruListCount = Marshal.GetDelegateForFunctionPointer<NativeMethods.EnumMruListWCountDelegate>(enumMruListAddr);
                var enumMruListData = Marshal.GetDelegateForFunctionPointer<NativeMethods.EnumMruListWDataDelegate>(enumMruListAddr);
                var freeMruList = Marshal.GetDelegateForFunctionPointer<NativeMethods.FreeMruListDelegate>(freeMruListAddr);

                var mruInfo = new NativeMethods.MruInfoW
                {
                    CbSize = (uint)Marshal.SizeOf<NativeMethods.MruInfoW>(),
                    UMax = 26,
                    FFlags = mruCacheWrite,
                    HKey = NativeMethods.HkeyCurrentUser,
                    LpszSubKey = runMruKey,
                };

                var hMruList = createMruList(ref mruInfo);
                if (hMruList == nint.Zero)
                {
                    return history;
                }

                try
                {
                    var count = enumMruListCount(hMruList, -1, nint.Zero, 0);
                    var buffer = new char[262];

                    for (var i = 0; i < count; i++)
                    {
                        var length = enumMruListData(hMruList, i, buffer, buffer.Length);
                        if (length > 1)
                        {
                            var text = new string(buffer, 0, length - 1);
                            if (text.Length > 0 && text[^1] == '\\')
                            {
                                text = text[..^1];
                            }

                            if (!string.IsNullOrEmpty(text))
                            {
                                history.Add(text);
                            }
                        }
                    }
                }
                finally
                {
                    freeMruList(hMruList);
                }
            }
            finally
            {
                NativeMethods.FreeLibrary(hLib);
            }
        }
        catch
        {
            // An empty history is a safe fallback when the system MRU APIs are unavailable.
        }

        return history;
    }

    private static class NativeMethods
    {
        internal static readonly nint HkeyCurrentUser = unchecked((nint)0x80000001);
        internal const uint LoadLibrarySearchSystemDirs = 0x00000800;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MruInfoW
        {
            public uint CbSize;
            public uint UMax;
            public uint FFlags;
            public nint HKey;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? LpszSubKey;
            public nint LpfnCompare;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate nint CreateMruListWDelegate(ref MruInfoW mruInfo);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int EnumMruListWCountDelegate(nint mruList, int item, nint data, int length);

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        internal delegate int EnumMruListWDataDelegate(nint mruList, int item, [Out] char[] data, int length);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void FreeMruListDelegate(nint mruList);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint LoadLibraryExW(string fileName, nint file, uint flags);

#pragma warning disable CA2101
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        internal static extern nint GetProcAddress(nint module, string procedureName);
#pragma warning restore CA2101

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(nint module);
    }
}
