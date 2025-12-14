// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ManagedCommon;
using static RunnerV2.NativeMethods;

namespace RunnerV2.Helpers
{
    internal static partial class HotkeyManager
    {
        private static readonly Dictionary<int, Action> _hotkeyActions = [];

        [STAThread]
        public static void EnableHotkey(HotkeyEx hotkey, Action onHotkey)
        {
            if (_hotkeyActions.ContainsKey(hotkey.Identifier))
            {
                DisableHotkey(hotkey);
            }

            _hotkeyActions[hotkey.Identifier] = onHotkey;

            if (!RegisterHotKey(Runner.RunnerHwnd, hotkey.Identifier, hotkey.ModifiersMask, hotkey.VkCode))
            {
                Console.WriteLine("Failed to register hotkey: " + hotkey);
                var lastError = Marshal.GetLastWin32Error();
                Console.WriteLine("LastError: " + lastError);
            }
        }

        [STAThread]
        public static void DisableHotkey(HotkeyEx hotkey)
        {
            if (!_hotkeyActions.ContainsKey(hotkey.Identifier))
            {
                return;
            }

            _hotkeyActions.Remove(hotkey.Identifier);
            if (!UnregisterHotKey(Runner.RunnerHwnd, hotkey.Identifier))
            {
                Console.WriteLine("Failed to unregister hotkey: " + hotkey);
                var lastError = Marshal.GetLastWin32Error();
                Console.WriteLine("LastError: " + lastError);
            }
        }

        public static void ProcessHotkey(nuint hotkeyId)
        {
            ulong hashId = hotkeyId.ToUInt64();
            if (_hotkeyActions.Any(h => h.Key == (int)hashId))
            {
                _hotkeyActions.First(h => h.Key == (int)hashId).Value();
            }
        }
    }
}
