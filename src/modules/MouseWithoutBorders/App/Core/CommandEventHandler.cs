// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using MouseWithoutBorders.Class;
using PowerToys.Interop;

namespace MouseWithoutBorders.Core
{
    /// <summary>
    /// Handles command events from external sources (e.g., Command Palette).
    /// Uses named events for inter-process communication, following the same pattern as other PowerToys modules.
    /// </summary>
    internal static class CommandEventHandler
    {
        private static CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Starts listening for command events on background threads.
        /// </summary>
        public static void StartListening()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken exitToken = _cancellationTokenSource.Token;

            // Start listener for Toggle Easy Mouse event
            StartEventListener(Constants.MWBToggleEasyMouseEvent(), ToggleEasyMouse, exitToken);

            // Start listener for Reconnect event
            StartEventListener(Constants.MWBReconnectEvent(), Reconnect, exitToken);
        }

        /// <summary>
        /// Stops listening for command events.
        /// </summary>
        public static void StopListening()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private static void StartEventListener(string eventName, Action callback, CancellationToken cancel)
        {
            new System.Threading.Thread(() =>
            {
                try
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                    WaitHandle[] waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };

                    while (!cancel.IsCancellationRequested)
                    {
                        int result = WaitHandle.WaitAny(waitHandles);
                        if (result == 1) // Event signaled
                        {
                            // Execute callback on UI thread using Common.DoSomethingInUIThread
                            Common.DoSomethingInUIThread(callback);
                        }
                        else
                        {
                            // Cancellation requested
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error in event listener for {eventName}: {ex.Message}");
                }
            })
            { IsBackground = true, Name = $"MWB-{eventName}-Listener" }.Start();
        }

        /// <summary>
        /// Toggles Easy Mouse between Enabled and Disabled states.
        /// This is the same logic used by the hotkey handler.
        /// </summary>
        public static void ToggleEasyMouse()
        {
            if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
            {
                return;
            }

            EasyMouseOption easyMouseOption = (EasyMouseOption)Setting.Values.EasyMouse;

            if (easyMouseOption is EasyMouseOption.Disable or EasyMouseOption.Enable)
            {
                Setting.Values.EasyMouse = (int)(easyMouseOption == EasyMouseOption.Disable ? EasyMouseOption.Enable : EasyMouseOption.Disable);

                Common.ShowToolTip($"Easy Mouse has been toggled to [{(EasyMouseOption)Setting.Values.EasyMouse}].", 3000);

                Logger.Log($"Easy Mouse toggled to {(EasyMouseOption)Setting.Values.EasyMouse} via command event.");
            }
        }

        /// <summary>
        /// Initiates a reconnection attempt to all machines.
        /// This is the same logic used by the hotkey handler.
        /// </summary>
        public static void Reconnect()
        {
            Common.ShowToolTip("Reconnecting...", 2000);
            Common.LastReconnectByHotKeyTime = Common.GetTick();
            InitAndCleanup.PleaseReopenSocket = InitAndCleanup.REOPEN_WHEN_HOTKEY;

            Logger.Log("Reconnect initiated via command event.");
        }
    }
}
