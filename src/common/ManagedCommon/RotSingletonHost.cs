// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

#nullable enable
#pragma warning disable IL2050 // Suppress COM interop trimming warnings for ROT hosting P/Invokes (desktop only scenario)

namespace ManagedCommon
{
    /// <summary>
    /// Generic helper to host a single COM-visible automation object in the Running Object Table (ROT)
    /// without registry/CLSID class factory registration. Used for lightweight cross-process automation.
    /// Pattern: create instance -> register with moniker -> wait until Stop.
    /// Threading: spins up a dedicated STA thread so objects needing STA semantics are safe.
    /// </summary>
    public sealed class RotSingletonHost : IDisposable
    {
        private readonly Lock _sync = new();
        private readonly Func<object> _factory;
        private readonly string _monikerName;
        private readonly string _threadName;
        private readonly ManualResetEvent _shutdown = new(false);

        private Thread? _thread;
        private int _rotCookie;
        private object? _instance; // keep alive
        private IMoniker? _moniker;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RotSingletonHost"/> class.
        /// </summary>
        /// <param name="monikerName">Moniker name (logical unique id), e.g. "Awake.Automation".</param>
        /// <param name="factory">Factory that creates the object to expose. Should return a COM-visible object.</param>
        /// <param name="threadName">Optional thread name for diagnostics.</param>
        public RotSingletonHost(string monikerName, Func<object> factory, string? threadName = null)
        {
            _monikerName = string.IsNullOrWhiteSpace(monikerName) ? throw new ArgumentException("Moniker required", nameof(monikerName)) : monikerName;
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _threadName = threadName ?? $"RotHost:{_monikerName}";
        }

        public bool IsRunning => _thread != null;

        public string MonikerName => _monikerName;

        public void Start()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                }

                if (_thread != null)
                {
                    return; // already running
                }

                _thread = new Thread(ThreadMain)
                {
                    IsBackground = true,
                    Name = _threadName,
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                Logger.LogInfo($"ROT host starting for moniker '{_monikerName}'");
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_thread == null)
                {
                    return;
                }

                _shutdown.Set();
            }

            _thread?.Join(3000);
            _thread = null;
            _shutdown.Reset();
        }

        private void ThreadMain()
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

                hr = Ole32.CreateItemMoniker("!", _monikerName, out _moniker);
                if (hr < 0 || _moniker == null)
                {
                    Logger.LogError($"CreateItemMoniker failed: 0x{hr:X8}");
                    return;
                }

                _instance = _factory();
                var unk = Marshal.GetIUnknownForObject(_instance);
                try
                {
                    hr = rot.Register(0x1 /* ROTFLAGS_REGISTRATIONKEEPSALIVE */, _instance, _moniker, out _rotCookie);
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

                Logger.LogInfo($"ROT registered: '{_monikerName}'");
                WaitHandle.WaitAny(new WaitHandle[] { _shutdown });
            }
            catch (Exception ex)
            {
                Logger.LogError($"ROT host exception: {ex}");
            }
            finally
            {
                try
                {
                    if (_rotCookie != 0 && Ole32.GetRunningObjectTable(0, out var rot2) == 0 && rot2 != null)
                    {
                        rot2.Revoke(_rotCookie);
                        _rotCookie = 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Exception revoking ROT registration: {ex.Message}");
                }

                Ole32.CoUninitialize();
                Logger.LogInfo($"ROT host stopped: '{_monikerName}'");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }

        private static class Ole32
        {
            internal const int CoinitApartmentThreaded = 0x2;

#pragma warning disable IL2050 // Suppress trimming warnings for COM interop P/Invokes; ROT hosting not used in trimmed scenarios.
            [DllImport("ole32.dll")]
            internal static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

            [DllImport("ole32.dll")]
            internal static extern void CoUninitialize();

            [DllImport("ole32.dll")]
            internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable? prot);

            [DllImport("ole32.dll")]
            internal static extern int CreateItemMoniker([MarshalAs(UnmanagedType.LPWStr)] string lpszDelim, [MarshalAs(UnmanagedType.LPWStr)] string lpszItem, out IMoniker? ppmk);
#pragma warning restore IL2050
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
