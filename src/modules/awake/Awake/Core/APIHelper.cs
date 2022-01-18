// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NLog;

namespace Awake.Core
{
    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001,
    }

    // See: https://docs.microsoft.com/windows/console/handlerroutine
    public enum ControlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6,
    }

    public delegate bool ConsoleEventHandler(ControlType ctrlType);

    /// <summary>
    /// Helper class that allows talking to Win32 APIs without having to rely on PInvoke in other parts
    /// of the codebase.
    /// </summary>
    public class APIHelper
    {
        private const string BuildRegistryLocation = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        private const int StdOutputHandle = -11;
        private const uint GenericWrite = 0x40000000;
        private const uint GenericRead = 0x80000000;

        private static readonly Logger _log;
        private static CancellationTokenSource _tokenSource;
        private static CancellationToken _threadToken;

        private static Task? _runnerThread;
        private static System.Timers.Timer _timedLoopTimer;

        static APIHelper()
        {
            _timedLoopTimer = new System.Timers.Timer();
            _log = LogManager.GetCurrentClassLogger();
            _tokenSource = new CancellationTokenSource();
        }

        public static void SetConsoleControlHandler(ConsoleEventHandler handler, bool addHandler)
        {
            NativeMethods.SetConsoleCtrlHandler(handler, addHandler);
        }

        public static void AllocateConsole()
        {
            _log.Debug("Bootstrapping the console allocation routine.");
            NativeMethods.AllocConsole();
            _log.Debug($"Console allocation result: {Marshal.GetLastWin32Error()}");

            var outputFilePointer = NativeMethods.CreateFile("CONOUT$", GenericRead | GenericWrite, FileShare.Write, IntPtr.Zero, FileMode.OpenOrCreate, 0, IntPtr.Zero);
            _log.Debug($"CONOUT creation result: {Marshal.GetLastWin32Error()}");

            NativeMethods.SetStdHandle(StdOutputHandle, outputFilePointer);
            _log.Debug($"SetStdHandle result: {Marshal.GetLastWin32Error()}");

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });
        }

        /// <summary>
        /// Sets the computer awake state using the native Win32 SetThreadExecutionState API. This
        /// function is just a nice-to-have wrapper that helps avoid tracking the success or failure of
        /// the call.
        /// </summary>
        /// <param name="state">Single or multiple EXECUTION_STATE entries.</param>
        /// <returns>true if successful, false if failed</returns>
        private static bool SetAwakeState(EXECUTION_STATE state)
        {
            try
            {
                var stateResult = NativeMethods.SetThreadExecutionState(state);
                return stateResult != 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SetIndefiniteKeepAwake(Action<bool> callback, Action failureCallback, bool keepDisplayOn = false)
        {
            _tokenSource.Cancel();

            try
            {
                if (_runnerThread != null && !_runnerThread.IsCanceled)
                {
                    _runnerThread.Wait(_threadToken);
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Confirmed background thread cancellation when setting indefinite keep awake.");
            }

            _tokenSource = new CancellationTokenSource();
            _threadToken = _tokenSource.Token;

            _runnerThread = Task.Run(() => RunIndefiniteLoop(keepDisplayOn), _threadToken)
                .ContinueWith((result) => callback(result.Result), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith((result) => failureCallback, TaskContinuationOptions.NotOnRanToCompletion);
        }

        public static void SetNoKeepAwake()
        {
            _tokenSource.Cancel();

            try
            {
                if (_runnerThread != null && !_runnerThread.IsCanceled)
                {
                    _runnerThread.Wait(_threadToken);
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Confirmed background thread cancellation when disabling explicit keep awake.");
            }
        }

        public static void SetTimedKeepAwake(uint seconds, Action<bool> callback, Action failureCallback, bool keepDisplayOn = true)
        {
            _tokenSource.Cancel();

            try
            {
                if (_runnerThread != null && !_runnerThread.IsCanceled)
                {
                    _runnerThread.Wait(_threadToken);
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Confirmed background thread cancellation when setting timed keep awake.");
            }

            _tokenSource = new CancellationTokenSource();
            _threadToken = _tokenSource.Token;

            _runnerThread = Task.Run(() => RunTimedLoop(seconds, keepDisplayOn), _threadToken)
                .ContinueWith((result) => callback(result.Result), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith((result) => failureCallback, TaskContinuationOptions.NotOnRanToCompletion);
        }

        private static bool RunIndefiniteLoop(bool keepDisplayOn = false)
        {
            bool success;
            if (keepDisplayOn)
            {
                success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            }
            else
            {
                success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            }

            try
            {
                if (success)
                {
                    _log.Info($"Initiated indefinite keep awake in background thread: {NativeMethods.GetCurrentThreadId()}. Screen on: {keepDisplayOn}");

                    WaitHandle.WaitAny(new[] { _threadToken.WaitHandle });

                    return success;
                }
                else
                {
                    _log.Info("Could not successfully set up indefinite keep awake.");
                    return success;
                }
            }
            catch (OperationCanceledException ex)
            {
                // Task was clearly cancelled.
                _log.Info($"Background thread termination: {NativeMethods.GetCurrentThreadId()}. Message: {ex.Message}");
                return success;
            }
        }

        private static bool RunTimedLoop(uint seconds, bool keepDisplayOn = true)
        {
            bool success = false;

            // In case cancellation was already requested.
            _threadToken.ThrowIfCancellationRequested();
            try
            {
                if (keepDisplayOn)
                {
                    success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                }
                else
                {
                    success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                }

                if (success)
                {
                    _log.Info($"Initiated temporary keep awake in background thread: {NativeMethods.GetCurrentThreadId()}. Screen on: {keepDisplayOn}");

                    _timedLoopTimer = new System.Timers.Timer(seconds * 1000);
                    _timedLoopTimer.Elapsed += (s, e) =>
                    {
                        _tokenSource.Cancel();

                        _timedLoopTimer.Stop();
                    };

                    _timedLoopTimer.Disposed += (s, e) =>
                    {
                        _log.Info("Old timer disposed.");
                    };

                    _timedLoopTimer.Start();

                    WaitHandle.WaitAny(new[] { _threadToken.WaitHandle });
                    _timedLoopTimer.Stop();
                    _timedLoopTimer.Dispose();

                    return success;
                }
                else
                {
                    _log.Info("Could not set up timed keep-awake with display on.");
                    return success;
                }
            }
            catch (OperationCanceledException ex)
            {
                // Task was clearly cancelled.
                _log.Info($"Background thread termination: {NativeMethods.GetCurrentThreadId()}. Message: {ex.Message}");
                return success;
            }
        }

        public static string GetOperatingSystemBuild()
        {
            try
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(BuildRegistryLocation);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                if (registryKey != null)
                {
                    var versionString = $"{registryKey.GetValue("ProductName")} {registryKey.GetValue("DisplayVersion")} {registryKey.GetValue("BuildLabEx")}";
                    return versionString;
                }
                else
                {
                    _log.Info("Registry key acquisition for OS failed.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _log.Info($"Could not get registry key for the build number. Error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
