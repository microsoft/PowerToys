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
        private static readonly Dictionary<HotkeyEx, Action> _hotkeyActions = [];

        public static void EnableHotkey(HotkeyEx hotkey, Action onHotkey)
        {
            if (_hotkeyActions.ContainsKey(hotkey))
            {
                return;
            }

            _hotkeyActions[hotkey] = onHotkey;

            if (!RegisterHotKey(Runner.RunnerHwnd, hotkey.GetHashCode(), hotkey.ModifiersMask, hotkey.VkCode))
            {
                Console.WriteLine("Failed to register hotkey: " + hotkey);
                var lastError = Marshal.GetLastWin32Error();
                Console.WriteLine("LastError: " + lastError);
            }
        }

        public static void DisableHotkey(HotkeyEx hotkey)
        {
            if (!_hotkeyActions.ContainsKey(hotkey))
            {
                return;
            }

            _hotkeyActions.Remove(hotkey);
            UnregisterHotKey(IntPtr.Zero, hotkey.GetHashCode());
        }

        public static void ProcessHotkey(nuint hotkeyId)
        {
            ulong hashId = hotkeyId.ToUInt64();
            if (_hotkeyActions.Any(h => h.Key.GetHashCode() == (int)hashId))
            {
                _hotkeyActions.First(h => h.Key.GetHashCode() == (int)hashId).Value();
            }
        }
    }
}
