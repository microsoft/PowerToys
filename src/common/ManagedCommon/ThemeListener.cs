// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ManagedCommon
{
    /// <summary>
    /// The Delegate for a ThemeChanged Event.
    /// </summary>
    /// <param name="sender">Sender ThemeListener</param>
    public delegate void ThemeChangedEvent(ThemeListener sender);

    public partial class ThemeListener : IDisposable
    {
        // RegNotifyChangeKeyValue filter: fire when a value inside the watched key is set.
        private const int RegNotifyChangeLastSet = 0x00000004;

        // Keep the registration alive independently of the watcher thread (Windows 8+).
        private const int RegNotifyThreadAgnostic = 0x10000000;

        /// <summary>
        /// Gets the App Theme.
        /// </summary>
        public AppTheme AppTheme { get; private set; }

        /// <summary>
        /// An event that fires if the Theme changes.
        /// </summary>
        public event ThemeChangedEvent ThemeChanged;

        // Watches HKCU\...\Themes\Personalize (where AppsUseLightTheme lives) using a native
        // registry change notification. Replaces the previous WMI ManagementEventWatcher, which
        // relied on COM interop and is therefore not compatible with Native AOT publishing.
        private readonly RegistryKey personalizeKey;
        private readonly AutoResetEvent registryChangedEvent;
        private readonly ManualResetEvent stopEvent;
        private readonly Thread watcherThread;
        private volatile bool disposed;

        public ThemeListener()
        {
            AppTheme = ThemeHelpers.GetAppTheme();

            personalizeKey = Registry.CurrentUser.OpenSubKey(ThemeHelpers.HkeyWindowsPersonalizeTheme);
            if (personalizeKey == null)
            {
                // The Personalize key is missing; there is nothing to watch. AppTheme still holds
                // the value read above and ThemeChanged simply never fires.
                return;
            }

            registryChangedEvent = new AutoResetEvent(false);
            stopEvent = new ManualResetEvent(false);

            watcherThread = new Thread(WatchLoop)
            {
                IsBackground = true,
                Name = nameof(ThemeListener),
            };
            watcherThread.Start();
        }

        private void WatchLoop()
        {
            WaitHandle[] handles = new WaitHandle[] { stopEvent, registryChangedEvent };

            while (!disposed)
            {
                int result = RegNotifyChangeKeyValue(
                    personalizeKey.Handle,
                    false,
                    RegNotifyChangeLastSet | RegNotifyThreadAgnostic,
                    registryChangedEvent.SafeWaitHandle,
                    true);

                if (result != 0)
                {
                    // Failed to register for notifications; stop watching rather than spin.
                    break;
                }

                // Index 0 (stopEvent) wins over a pending registry change, so Dispose stops promptly.
                if (WaitHandle.WaitAny(handles) == 0 || disposed)
                {
                    break;
                }

                AppTheme appTheme = ThemeHelpers.GetAppTheme();
                if (appTheme != AppTheme)
                {
                    AppTheme = appTheme;
                    ThemeChanged?.Invoke(this);
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopEvent?.Set();

            // Let the watcher thread finish using the handles before disposing them, unless Dispose
            // runs on that same thread (a ThemeChanged handler calling Dispose), which would deadlock.
            if (watcherThread != null && watcherThread.IsAlive && watcherThread != Thread.CurrentThread)
            {
                watcherThread.Join();
            }

            stopEvent?.Dispose();
            registryChangedEvent?.Dispose();
            personalizeKey?.Dispose();

            GC.SuppressFinalize(this);
        }

        [LibraryImport("advapi32.dll", SetLastError = true)]
        private static partial int RegNotifyChangeKeyValue(
            SafeRegistryHandle hKey,
            [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
            int notifyFilter,
            SafeWaitHandle hEvent,
            [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
    }
}
