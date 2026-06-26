// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PowerToys.Interop;
using ShortcutGuide.Models;
using ShortcutGuide.Telemetry;
using KeyEventHandler = Microsoft.UI.Xaml.Input.KeyEventHandler;

namespace ShortcutGuide
{
    public partial class App : IDisposable
    {
        internal static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts { get; private set; } = new Dictionary<string, List<ShortcutEntry>>();

        internal static ShortcutGuideSettings ShortcutGuideSettings { get; private set; } = null!;

        internal static ShortcutGuideProperties ShortcutGuideProperties { get; private set; } = null!;

        /// <summary>
        /// The single transparent host that replaces the previous MainWindow +
        /// TaskbarWindow pair. The two surfaces are now XAML pseudo-windows
        /// inside this one window.
        /// </summary>
        internal static OverlayWindow OverlayWindow { get; private set; } = null!;

        private HotkeySettingsControlHook _winKeyUpKeyboardHook = null!;

        internal static string CurrentAppName { get; set; } = string.Empty;

        private EventWaitHandle? _launchedEvent;

        private Thread? _listenForLaunchedEventThread;

        private static readonly UIntPtr _ignoreKeyEventFlag = 0x5557;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            this.LoadData();
            OverlayWindow = new OverlayWindow();
            OverlayWindow.Activate();
            OverlayWindow.AppWindow.Hide();
            OverlayWindow.Closed += (_, _) =>
            {
                PowerToysTelemetry.Log.WriteEvent(new ShortcutGuideSessionEvent(
                    OverlayWindow.SessionDurationMs,
                    OverlayWindow.CloseType));

                // WinUI3's dispatcher loop does not terminate when the last
                // window closes; without Exit() the SG.exe process stays
                // alive, holds the AppInstance single-instance lock, and
                // blocks the next launch (the well-known "every other
                // long-press works" bug).
                Current.Exit();
            };

            try
            {
                _launchedEvent = EventWaitHandle.OpenExisting(Constants.ShortcutGuideTriggerEvent());
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open existing event '{Constants.ShortcutGuideTriggerEvent()}': {ex.Message}");
            }

            _listenForLaunchedEventThread = new Thread(ListenForLaunchedEvents)
            {
                IsBackground = true,
                Name = "ShortcutGuide-ShowEventListener",
            };
            _listenForLaunchedEventThread.Start();
            _winKeyUpKeyboardHook = new HotkeySettingsControlHook(
            (int key) =>
            {
                SendSingleKeyboardInput((short)key, 0x0); // key down
            },
            (int key) =>
            {
                if (OverlayWindow.AppWindow.IsVisible)
                {
                    OverlayWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        OverlayWindow.CloseAnimated();
                    });

                    NativeMethods.SendInput(1, [new() { Type = 1, Data = new() { Keyboard = new NativeMethods.KEYBDINPUT { WVk = 0xFF, DwFlags = 0x2 } } }], Marshal.SizeOf<NativeMethods.INPUT>());
                    SendSingleKeyboardInput((short)key, 0x2); // key up
                }
                else
                {
                    SendSingleKeyboardInput((short)key, 0x2); // key up
                }
            },
            () => true,
            (int key, nuint specialFlags) => key == 91 && specialFlags != _ignoreKeyEventFlag);
        }

        private static bool IsExtendedVirtualKey(short vk)
        {
            return vk switch
            {
                0xA5 => true, // VK_RMENU (Right Alt - AltGr)
                0xA3 => true, // VK_RCONTROL
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

        private void ListenForLaunchedEvents()
        {
            if (_launchedEvent == null)
            {
                return;
            }

            var handles = new WaitHandle[] { _launchedEvent };
            try
            {
                while (true)
                {
                    var index = WaitHandle.WaitAny(handles);
                    if (index == 0)
                    {
                        OverlayWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            if (Keyboard.IsKeyDown(Key.LWin))
                            {
                                if (OverlayWindow.AppWindow.IsVisible)
                                {
                                    return;
                                }

                                OverlayWindow.AppWindow.Show();
                                OverlayWindow.MainPaneControl.Visibility = Visibility.Collapsed;
                                OverlayWindow.AppWindow.MoveInZOrderAtTop();
                                OverlayWindow.UpdateTaskbarPaneLayout();
                                OverlayWindow.TaskbarPaneControl.Visibility = Visibility.Visible;
                                return;
                            }

                            if (OverlayWindow.AppWindow.IsVisible)
                            {
                                OverlayWindow.CloseAnimated();
                                OverlayWindow.MainPaneControl.Hide();
                            }
                            else
                            {
                                Program.ForegroundWindowHandle = NativeMethods.GetForegroundWindow();
                                OverlayWindow.MainPaneControl.Visibility = Visibility.Collapsed;
                                OverlayWindow.AppWindow.Show();
                                await OverlayWindow.MainPaneControl.Open();
                                OverlayWindow.AppWindow.MoveInZOrderAtTop();
                                OverlayWindow.UpdateTaskbarPaneLayout();
                                OverlayWindow.MainPaneControl.Visibility = Visibility.Visible;
                            }
                        });
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (ThreadInterruptedException)
            {
            }
        }

        private void LoadData()
        {
            SettingsUtils settingsUtils = SettingsUtils.Default;

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                try
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath));
                    if (loaded != null)
                    {
                        PinnedShortcuts = loaded;
                    }
                }
                catch (JsonException)
                {
                    // Fall back to the empty default if the file is corrupt.
                }
            }

            ShortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig;
            ShortcutGuideProperties = ShortcutGuideSettings.Properties;

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            settingsUtils.SaveSettings(JsonSerializer.Serialize(App.ShortcutGuideSettings, new JsonSerializerOptions { WriteIndented = true }), "Shortcut Guide");
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        }

        public void Dispose()
        {
            _launchedEvent?.Dispose();

            if (_listenForLaunchedEventThread == null)
            {
                return;
            }

            try
            {
                if (!_listenForLaunchedEventThread.Join(TimeSpan.FromMilliseconds(250)))
                {
                    _listenForLaunchedEventThread.Interrupt();
                    _listenForLaunchedEventThread.Join(TimeSpan.FromMilliseconds(250));
                }
            }
            catch (ThreadInterruptedException)
            {
            }
            catch (ThreadStateException)
            {
            }

            _listenForLaunchedEventThread = null;
            GC.SuppressFinalize(this);
        }
    }
}
