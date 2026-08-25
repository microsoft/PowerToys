// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PowerToys.Interop;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.Telemetry;
using KeyEventHandler = Microsoft.UI.Xaml.Input.KeyEventHandler;

namespace ShortcutGuide
{
    public partial class App : IDisposable
    {
        internal static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts { get; private set; } = new Dictionary<string, List<ShortcutEntry>>();

        internal static ShortcutGuideSettings ShortcutGuideSettings => SettingsRepository<ShortcutGuideSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;

        internal static ShortcutGuideProperties ShortcutGuideProperties => ShortcutGuideSettings.Properties;

        /// <summary>
        /// Gets the single transparent host that replaces the previous MainWindow +
        /// TaskbarWindow pair. The two surfaces are now XAML pseudo-windows
        /// inside this one window.
        /// </summary>
        internal static OverlayWindow OverlayWindow { get; private set; } = null!;

        private HotkeySettingsControlHook? _winKeyUpKeyboardHook;

        internal static string CurrentAppName { get; set; } = string.Empty;

        private readonly SemaphoreSlim _activationGate = new(1, 1);
        private readonly ManualResetEvent _listenerShutdownEvent = new(false);
        private EventWaitHandle? _regularHotkeyEvent;
        private EventWaitHandle? _winKeyHoldEvent;
        private EventWaitHandle? _exitEvent;
        private RegisteredWaitHandle? _runnerExitRegistration;
        private Thread? _listenForActivationEventsThread;
        private int _activeSource = (int)ShortcutGuideActivationSource.None;
        private int _activeSurface = (int)ShortcutGuideOverlaySurface.Hidden;
        private int _disposed;
        private int _shutdownStarted;

        private static readonly UIntPtr _ignoreKeyEventFlag = 0x5557;

        public App()
        {
            this.InitializeComponent();

            // Register process-wide exception handlers so a stray exception (e.g. an IO failure
            // during a fire-and-forget UI handler, or a background Task fault) gets logged
            // instead of taking the overlay down with an unhandled access violation in coreclr.
            // Without these the runtime tears the process down before our local catches can run.
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                _runnerExitRegistration = ThreadPool.RegisterWaitForSingleObject(
                    Program.RunnerExitEvent,
                    (_, _) =>
                    {
                        if (!dispatcher.TryEnqueue(Shutdown))
                        {
                            Logger.LogWarning("Failed to enqueue Shortcut Guide shutdown after the PowerToys runner exited.");
                        }
                    },
                    null,
                    Timeout.Infinite,
                    true);

                this.LoadData();
                OverlayWindow = new OverlayWindow();
                OverlayWindow.ClosingStarted += (_, _) => ResetActivationState();
                OverlayWindow.Activate();
                OverlayWindow.AppWindow.Hide();
                OverlayWindow.Closed += (_, _) =>
                {
                    PowerToysTelemetry.Log.WriteEvent(new ShortcutGuideSessionEvent(
                        OverlayWindow.SessionDurationMs,
                        OverlayWindow.CloseType));

                    Shutdown();
                };

                _regularHotkeyEvent = TryOpenActivationEvent(Constants.ShortcutGuideTriggerEvent());
                _winKeyHoldEvent = TryOpenActivationEvent(Constants.ShortcutGuideWinKeyHoldEvent());
                _exitEvent = TryOpenActivationEvent(Constants.ShortcutGuideExitEvent());

                _listenForActivationEventsThread = new Thread(ListenForActivationEvents)
                {
                    IsBackground = true,
                    Name = "ShortcutGuide-ActivationEventListener",
                };
                _listenForActivationEventsThread.Start();
                _winKeyUpKeyboardHook = new HotkeySettingsControlHook(
                (int key) =>
                {
                    SendSingleKeyboardInput((short)key, 0x0); // key down
                },
                (int key) =>
                {
                    if (key is not 0x5B and not 0x5C)
                    {
                        return;
                    }

                    var activeSource = (ShortcutGuideActivationSource)Volatile.Read(ref _activeSource);
                    var activeSurface = (ShortcutGuideOverlaySurface)Volatile.Read(ref _activeSurface);
                    bool isOverlayVisible = OverlayWindow.AppWindow.IsVisible;
                    if (isOverlayVisible || activeSource == ShortcutGuideActivationSource.WindowsKeyHold)
                    {
                        if (ShortcutGuideActivationPolicy.ShouldCloseOnWindowsKeyRelease(
                            activeSource,
                            activeSurface,
                            ShortcutGuideProperties.CloseOnWindowsKeyRelease.Value))
                        {
                            OverlayWindow.DispatcherQueue.TryEnqueue(CloseOverlay);
                        }

                        NativeMethods.SendInput(1, [new() { Type = 1, Data = new() { Keyboard = new NativeMethods.KEYBDINPUT { WVk = 0xFF, DwFlags = 0x2 } } }], Marshal.SizeOf<NativeMethods.INPUT>());
                        SendSingleKeyboardInput((short)key, 0x2); // key up
                    }
                },
                () => OverlayWindow.AppWindow.IsVisible || (ShortcutGuideActivationSource)Volatile.Read(ref _activeSource) == ShortcutGuideActivationSource.WindowsKeyHold,
                (int key, nuint specialFlags) => (key is 0x5B or 0x5C) && specialFlags != _ignoreKeyEventFlag);
            }
            catch (Exception ex)
            {
                // Any failure in launch is fatal for this short-lived overlay; log and exit
                // cleanly rather than letting WinUI surface a generic crash dialog.
                Logger.LogError("Failed to launch Shortcut Guide.", ex);
                Environment.ExitCode = 1;
                Shutdown();
            }
        }

        private static bool IsExtendedVirtualKey(short vk)
        {
            return vk switch
            {
                0xA5 => true, // VK_RMENU (Right Alt - AltGr)
                0xA3 => true, // VK_RCONTROL
                0x5B => true, // VK_LWIN
                0x5C => true, // VK_RWIN
                0x2D => true, // VK_INSERT
                0x2E => true, // VK_DELETE
                0x23 => true, // VK_END
                0x24 => true, // VK_HOME
                0x21 => true, // VK_PRIOR (Page Up)
                0x22 => true, // VK_NEXT (Page Down)
                0x90 => true, // VK_NUMLOCK
                _ => false,
            };
        }

        private static void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            if (IsExtendedVirtualKey(keyCode))
            {
                keyStatus |= 0x1; // KEYEVENTF_EXTENDEDKEY
            }

            NativeMethods.INPUT input = new()
            {
                Type = 0x1, // INPUT_KEYBOARD
                Data = new NativeMethods.MOUSEKEYBDHARDWAREINPUT
                {
                    Keyboard = new NativeMethods.KEYBDINPUT
                    {
                        WVk = (ushort)keyCode,
                        DwFlags = keyStatus,
                        DwExtraInfo = (nint)_ignoreKeyEventFlag,
                    },
                },
            };

            NativeMethods.INPUT[] inputs = [input];

            NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }

        private void ListenForActivationEvents()
        {
            List<(WaitHandle Handle, ShortcutGuideActivationSource Source)> activationEvents = [];
            if (_regularHotkeyEvent != null)
            {
                activationEvents.Add((_regularHotkeyEvent, ShortcutGuideActivationSource.RegularHotkey));
            }

            if (_winKeyHoldEvent != null)
            {
                activationEvents.Add((_winKeyHoldEvent, ShortcutGuideActivationSource.WindowsKeyHold));
            }

            List<WaitHandle> handles = activationEvents.ConvertAll(item => item.Handle);
            int exitEventIndex = -1;
            if (_exitEvent != null)
            {
                exitEventIndex = handles.Count;
                handles.Add(_exitEvent);
            }

            if (handles.Count == 0)
            {
                Logger.LogError("Failed to open any Shortcut Guide events.");
                return;
            }

            int listenerShutdownEventIndex = handles.Count;
            handles.Add(_listenerShutdownEvent);
            WaitHandle[] waitHandles = handles.ToArray();
            Logger.LogInfo("Shortcut Guide activation-event listener started.");
            while (true)
            {
                int eventIndex = WaitHandle.WaitAny(waitHandles);
                if (eventIndex == listenerShutdownEventIndex)
                {
                    return;
                }

                if (eventIndex == exitEventIndex)
                {
                    Logger.LogInfo("Shortcut Guide exit event signaled.");
                    OverlayWindow.DispatcherQueue.TryEnqueue(Shutdown);
                    return;
                }

                var activationSource = activationEvents[eventIndex].Source;
                Logger.LogInfo($"Shortcut Guide trigger event signaled by {activationSource}.");
                OverlayWindow.DispatcherQueue.TryEnqueue(() => _ = HandleActivationAsync(activationSource));
            }
        }

        private static EventWaitHandle? TryOpenActivationEvent(string eventName)
        {
            try
            {
                var activationEvent = EventWaitHandle.OpenExisting(eventName);
                Logger.LogInfo($"Opened Shortcut Guide trigger event '{eventName}'.");
                return activationEvent;
            }
            catch (WaitHandleCannotBeOpenedException ex)
            {
                Logger.LogError($"Failed to open Shortcut Guide trigger event '{eventName}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogError($"Failed to open Shortcut Guide trigger event '{eventName}': {ex.Message}");
            }
            catch (IOException ex)
            {
                Logger.LogError($"Failed to open Shortcut Guide trigger event '{eventName}': {ex.Message}");
            }

            return null;
        }

        private async Task HandleActivationAsync(ShortcutGuideActivationSource activationSource)
        {
            await _activationGate.WaitAsync();
            try
            {
                bool isOverlayVisible = OverlayWindow.AppWindow.IsVisible;
                bool isCurrentWindowExcluded =
                    !isOverlayVisible && NativeMethods.IsCurrentWindowExcludedFromShortcutGuide();
                var activeSource = (ShortcutGuideActivationSource)Volatile.Read(ref _activeSource);
                var activeSurface = (ShortcutGuideOverlaySurface)Volatile.Read(ref _activeSurface);
                var windowsKeyAction = (ShortcutGuideWindowsKeyAction)ShortcutGuideProperties.WindowsKeyAction.Value;
                var action = ShortcutGuideActivationPolicy.GetActivationAction(
                    activationSource,
                    isOverlayVisible,
                    isCurrentWindowExcluded,
                    activeSource,
                    activeSurface,
                    windowsKeyAction);

                if (isCurrentWindowExcluded)
                {
                    Logger.LogInfo("Shortcut Guide activation suppressed because the foreground application is excluded.");
                }

                Logger.LogInfo($"Shortcut Guide activation action: {action}.");
                switch (action)
                {
                    case ShortcutGuideActivationAction.ShowTaskbarIndicators:
                        if (!TryBeginActivation(activationSource, ShortcutGuideOverlaySurface.TaskbarIndicators))
                        {
                            break;
                        }

                        Program.ForegroundWindowHandle = NativeMethods.GetForegroundWindow();
                        OverlayWindow.MainPaneControl.Visibility = Visibility.Collapsed;
                        OverlayWindow.ShowOverlay();
                        OverlayWindow.UpdateTaskbarPaneLayout();
                        OverlayWindow.TaskbarPaneControl.Visibility = Visibility.Visible;
                        break;

                    case ShortcutGuideActivationAction.ShowFullGuide:
                        bool isFullGuideVisible = isOverlayVisible && activeSurface == ShortcutGuideOverlaySurface.FullGuide;
                        if (!TryBeginActivation(activationSource, ShortcutGuideOverlaySurface.FullGuide))
                        {
                            break;
                        }

                        if (isFullGuideVisible)
                        {
                            OverlayWindow.MainPaneControl.Visibility = Visibility.Visible;
                            OverlayWindow.MainPaneControl.FocusSearch();
                            break;
                        }

                        if (!isOverlayVisible)
                        {
                            Program.ForegroundWindowHandle = NativeMethods.GetForegroundWindow();
                        }

                        OverlayWindow.MainPaneControl.Visibility = Visibility.Collapsed;
                        OverlayWindow.ShowOverlay();
                        await OverlayWindow.MainPaneControl.Open();
                        if ((ShortcutGuideActivationSource)Volatile.Read(ref _activeSource) != activationSource ||
                            (ShortcutGuideOverlaySurface)Volatile.Read(ref _activeSurface) != ShortcutGuideOverlaySurface.FullGuide)
                        {
                            return;
                        }

                        OverlayWindow.UpdateTaskbarPaneLayout();
                        OverlayWindow.MainPaneControl.Visibility = Visibility.Visible;
                        OverlayWindow.MainPaneControl.FocusSearch();
                        break;

                    case ShortcutGuideActivationAction.Close:
                        CloseOverlay();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to handle Shortcut Guide activation.", ex);
                CloseOverlay();
            }
            finally
            {
                _activationGate.Release();
            }
        }

        private static void CloseOverlay()
        {
            OverlayWindow.CloseAnimated();
        }

        private void SetActivationState(ShortcutGuideActivationSource source, ShortcutGuideOverlaySurface surface)
        {
            Volatile.Write(ref _activeSource, (int)source);
            Volatile.Write(ref _activeSurface, (int)surface);
        }

        private void ResetActivationState()
        {
            SetActivationState(ShortcutGuideActivationSource.None, ShortcutGuideOverlaySurface.Hidden);
        }

        private bool TryBeginActivation(ShortcutGuideActivationSource source, ShortcutGuideOverlaySurface surface)
        {
            SetActivationState(source, surface);
            if (source != ShortcutGuideActivationSource.WindowsKeyHold || IsWindowsKeyPressed())
            {
                return true;
            }

            ResetActivationState();
            return false;
        }

        private static bool IsWindowsKeyPressed()
        {
            const int VirtualKeyLeftWindows = 0x5B;
            const int VirtualKeyRightWindows = 0x5C;
            return (NativeMethods.GetAsyncKeyState(VirtualKeyLeftWindows) & 0x8000) != 0 ||
                   (NativeMethods.GetAsyncKeyState(VirtualKeyRightWindows) & 0x8000) != 0;
        }

        private void LoadData()
        {
            SettingsUtils settingsUtils = SettingsUtils.Default;

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                try
                {
                    var loaded = JsonSerializer.Deserialize(File.ReadAllText(pinnedPath), typeof(Dictionary<string, List<ShortcutEntry>>), ShortcutGuideJsonContext.Default);
                    if (loaded != null)
                    {
                        PinnedShortcuts = (Dictionary<string, List<ShortcutEntry>>)loaded;
                    }
                }
                catch (Exception ex) when (ex is JsonException
                                        or IOException
                                        or UnauthorizedAccessException)
                {
                    // Fall back to the empty default if the file is corrupt or unreadable.
                    Logger.LogWarning($"Failed to load pinned shortcuts from '{pinnedPath}'. Falling back to empty list. Reason: {ex.Message}");
                }
            }

            try
            {
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
                settingsUtils.SaveSettings(JsonSerializer.Serialize(App.ShortcutGuideSettings, new JsonSerializerOptions { WriteIndented = true }), "Shortcut Guide");
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Persisting the round-tripped settings is best-effort; the in-memory copy is still valid.
                Logger.LogWarning($"Failed to persist Shortcut Guide settings on launch. Reason: {ex.Message}");
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Exceptions raised on the UI thread land here. Mark handled so the runtime
            // does not terminate the process; the overlay can usually continue.
            Logger.LogError("Unhandled UI exception in Shortcut Guide.", e.Exception);
            e.Handled = true;
        }

        private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            // Background-thread exceptions reach here as a last resort; we cannot prevent
            // termination when IsTerminating is true, but at least we leave a log trail.
            if (e.ExceptionObject is Exception ex)
            {
                Logger.LogError($"Unhandled background exception in Shortcut Guide (IsTerminating={e.IsTerminating}).", ex);
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.LogError("Unobserved Task exception in Shortcut Guide.", e.Exception);
            e.SetObserved();
        }

        private void Shutdown()
        {
            if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            {
                return;
            }

            Dispose();
            Current.Exit();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _winKeyUpKeyboardHook?.Dispose();
            _runnerExitRegistration?.Unregister(null);

            this.UnhandledException -= App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

            _listenerShutdownEvent.Set();
            if (_listenForActivationEventsThread != null)
            {
                try
                {
                    if (!_listenForActivationEventsThread.Join(TimeSpan.FromSeconds(1)))
                    {
                        Logger.LogWarning("Shortcut Guide activation-event listener did not stop within the timeout.");
                    }
                }
                catch (ThreadStateException ex)
                {
                    Logger.LogWarning($"Failed to join Shortcut Guide activation-event listener: {ex.Message}");
                }

                _listenForActivationEventsThread = null;
            }

            _regularHotkeyEvent?.Dispose();
            _winKeyHoldEvent?.Dispose();
            _exitEvent?.Dispose();
            _listenerShutdownEvent.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
