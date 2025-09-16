// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;

namespace Awake
{
    /// <summary>
    /// Background ROT host for the automation object. No registry / class factory registration; discovery is by moniker.
    /// </summary>
    internal static class ComServerHost
    {
        private const string DefaultMonikerName = "Awake.Automation";

        private static readonly object SyncLock = new();
        private static readonly ManualResetEvent ShutdownEvent = new(false);

        private static Thread? _rotThread;
        private static int _rotCookie;
        private static object? _automationInstance; // keep alive
        private static IMoniker? _moniker;

        public static void StartBackground(string? monikerName = null)
        {
            lock (SyncLock)
            {
                if (_rotThread != null)
                {
                    return;
                }

                string name = string.IsNullOrWhiteSpace(monikerName) ? DefaultMonikerName : monikerName!;
                _rotThread = new Thread(() => RotThreadMain(name))
                {
                    Name = "AwakeAutomationRotThread",
                    IsBackground = true,
                };
                _rotThread.SetApartmentState(ApartmentState.STA);
                _rotThread.Start();
                Logger.LogInfo($"Starting Awake automation ROT host with moniker '{name}'");
            }
        }

        public static void Stop()
        {
            lock (SyncLock)
            {
                ShutdownEvent.Set();
            }

            _rotThread?.Join(3000);
            _rotThread = null;
        }

        private static void RotThreadMain(string monikerName)
        {
            int hr = Ole32.CoInitializeEx(IntPtr.Zero, Ole32.CoinitApartmentThreaded);
            if (hr < 0)
            {
                Logger.LogError($"CoInitializeEx failed: 0x{hr:X8}");
                return;
            }

            try
            {
                hr = Ole32.GetRunningObjectTable(0, out var rot);
                if (hr < 0 || rot == null)
                {
                    Logger.LogError($"GetRunningObjectTable failed: 0x{hr:X8}");
                    return;
                }

                hr = Ole32.CreateItemMoniker("!", monikerName, out _moniker);
                if (hr < 0 || _moniker == null)
                {
                    Logger.LogError($"CreateItemMoniker failed: 0x{hr:X8}");
                    return;
                }

                _automationInstance = new AwakeAutomation();
                var unk = Marshal.GetIUnknownForObject(_automationInstance);
                try
                {
                    hr = rot.Register(0x1 /* ROTFLAGS_REGISTRATIONKEEPSALIVE */, _automationInstance, _moniker, out _rotCookie);
                    if (hr < 0)
                    {
                        Logger.LogError($"IRunningObjectTable.Register failed: 0x{hr:X8}");
                        return;
                    }
                }
                finally
                {
                    Marshal.Release(unk);
                }

                Logger.LogInfo("Awake automation registered in ROT.");
                WaitHandle.WaitAny(new WaitHandle[] { ShutdownEvent });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Automation ROT exception: {ex}");
            }
            finally
            {
                try
                {
                    if (_rotCookie != 0 && Ole32.GetRunningObjectTable(0, out var rot) == 0 && rot != null)
                    {
                        rot.Revoke(_rotCookie);
                        _rotCookie = 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Exception revoking ROT registration: {ex.Message}");
                }

                Ole32.CoUninitialize();
                Logger.LogInfo("Awake automation ROT host stopped.");
            }
        }

        private static class Ole32
        {
            internal const int CoinitApartmentThreaded = 0x2;

            [DllImport("ole32.dll")]
            internal static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

            [DllImport("ole32.dll")]
            internal static extern void CoUninitialize();

            [DllImport("ole32.dll")]
            internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable? prot);

            [DllImport("ole32.dll")]
            internal static extern int CreateItemMoniker([MarshalAs(UnmanagedType.LPWStr)] string lpszDelim, [MarshalAs(UnmanagedType.LPWStr)] string lpszItem, out IMoniker? ppmk);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("00000010-0000-0000-C000-000000000046")]
        private interface IRunningObjectTable
        {
            int Register(int grfFlags, [MarshalAs(UnmanagedType.IUnknown)] object punkObject, IMoniker pmkObjectName, out int pdwRegister);

            int Revoke(int dwRegister);

            void IsRunning(IMoniker pmkObjectName);

            int GetObject(IMoniker pmkObjectName, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunkObject);

            void NoteChangeTime(int dwRegister, ref FileTime pfiletime);

            int GetTimeOfLastChange(IMoniker pmkObjectName, ref FileTime pfiletime);

            int EnumRunning(out object ppenumMoniker);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000000f-0000-0000-C000-000000000046")]
        private interface IMoniker
        {
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint DwLowDateTime;
            public uint DwHighDateTime;
        }
    }
}
