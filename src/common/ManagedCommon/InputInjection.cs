// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ManagedCommon
{
    /// <summary>
    /// Audited helper for injecting a keyboard chord (for example Ctrl+V) with SendInput. Managed twin of
    /// src/common/utils/input_injection.h; keep the two in sync.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Modifier key-downs are never injected to "restore" an earlier keyboard state. A key-down built from a stale
    /// GetAsyncKeyState snapshot can land after the user's physical key-up and leave the modifier logically held system-wide.</item>
    /// <item>It can wait (bounded) for the user to physically release the modifiers of the hotkey that triggered it, then
    /// injects key-ups for any modifier that is still held, so the target sees exactly the requested chord.</item>
    /// <item>The whole chord goes out in one SendInput batch, every event tagged with the caller's dwExtraInfo. If SendInput
    /// sends fewer events than requested, key-ups are sent for every chord key-down that went out without its key-up.</item>
    /// </list>
    /// Waiting blocks the calling thread, so don't wait on a UI thread or a low-level keyboard hook thread.
    /// </remarks>
    public static class InputInjection
    {
        /// <summary>
        /// Key-up for an unassigned virtual key. Sent while Win is held, it keeps the Win key-up from opening Start.
        /// </summary>
        public const ushort StartMenuSuppressionKey = 0xFF;

        private const uint KeyEventKeyUp = 0x0002;

        private static readonly Win32InputInjectionApi DefaultApi = new();

        /// <summary>
        /// Gets the left/right modifier keys (Ctrl, Win, Shift, Alt) that are waited for and released.
        /// </summary>
        public static IReadOnlyList<ushort> ModifierKeys { get; } = new ushort[]
        {
            0xA2, // VK_LCONTROL
            0xA3, // VK_RCONTROL
            0x5B, // VK_LWIN
            0x5C, // VK_RWIN
            0xA0, // VK_LSHIFT
            0xA1, // VK_RSHIFT
            0xA4, // VK_LMENU
            0xA5, // VK_RMENU
        };

        /// <summary>
        /// Presses <paramref name="keys"/> in order and releases them in reverse order, e.g. { VK_CONTROL, 'V' } sends Ctrl+V.
        /// </summary>
        public static InputChordResult SendChord(IReadOnlyList<ushort> keys, InputChordOptions options)
        {
            return SendChord(keys, options, DefaultApi);
        }

        /// <summary>
        /// Polls until no modifier key is down.
        /// </summary>
        /// <returns><see langword="false"/> if a modifier is still down after <paramref name="timeout"/>.</returns>
        public static bool WaitForModifiersRelease(TimeSpan timeout, TimeSpan pollInterval)
        {
            return WaitForModifiersRelease(timeout, pollInterval, DefaultApi);
        }

        internal static bool WaitForModifiersRelease(TimeSpan timeout, TimeSpan pollInterval, IInputInjectionApi api)
        {
            var interval = pollInterval > TimeSpan.FromMilliseconds(1) ? pollInterval : TimeSpan.FromMilliseconds(1);
            var waited = TimeSpan.Zero;
            while (AnyModifierDown(api))
            {
                if (waited >= timeout)
                {
                    return false;
                }

                api.Sleep(interval);
                waited += interval;
            }

            return true;
        }

        internal static InputChordResult SendChord(IReadOnlyList<ushort> keys, InputChordOptions options, IInputInjectionApi api)
        {
            ArgumentNullException.ThrowIfNull(keys);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(api);

            bool waitTimedOut = false;
            if (options.ModifierReleaseTimeout > TimeSpan.Zero)
            {
                waitTimedOut = !WaitForModifiersRelease(options.ModifierReleaseTimeout, options.PollInterval, api);
            }

            var inputs = new List<NativeMethods.INPUT>(1 + ModifierKeys.Count + (keys.Count * 2));

            if (options.SuppressStartMenu)
            {
                inputs.Add(MakeKeyInput(StartMenuSuppressionKey, keyUp: true, options.ExtraInfo));
            }

            if (options.ReleaseHeldModifiers)
            {
                foreach (var vk in ModifierKeys)
                {
                    if (api.IsKeyDown(vk))
                    {
                        inputs.Add(MakeKeyInput(vk, keyUp: true, options.ExtraInfo));
                    }
                }
            }

            int firstKeyDown = inputs.Count;
            for (int i = 0; i < keys.Count; i++)
            {
                inputs.Add(MakeKeyInput(keys[i], keyUp: false, options.ExtraInfo));
            }

            for (int i = keys.Count - 1; i >= 0; i--)
            {
                inputs.Add(MakeKeyInput(keys[i], keyUp: true, options.ExtraInfo));
            }

            uint requested = (uint)inputs.Count;
            if (requested == 0)
            {
                return new InputChordResult { ModifierWaitTimedOut = waitTimedOut };
            }

            uint sent = api.SendInput(inputs.ToArray(), out int error);
            if (sent >= requested)
            {
                return new InputChordResult { Requested = requested, Sent = sent, ModifierWaitTimedOut = waitTimedOut };
            }

            // Release, last pressed first, every key whose key-down went out but whose key-up didn't.
            var cleanup = new List<NativeMethods.INPUT>();
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                long keyDownIndex = firstKeyDown + i;
                long keyUpIndex = firstKeyDown + (2L * keys.Count) - 1 - i;
                if (keyDownIndex < sent && keyUpIndex >= sent)
                {
                    cleanup.Add(MakeKeyInput(keys[i], keyUp: true, options.ExtraInfo));
                }
            }

            uint cleanupSent = 0;
            if (cleanup.Count > 0)
            {
                cleanupSent = api.SendInput(cleanup.ToArray(), out _);
            }

            return new InputChordResult
            {
                Requested = requested,
                Sent = sent,
                CleanupRequested = (uint)cleanup.Count,
                CleanupSent = cleanupSent,
                Error = error,
                ModifierWaitTimedOut = waitTimedOut,
            };
        }

        private static bool AnyModifierDown(IInputInjectionApi api)
        {
            foreach (var vk in ModifierKeys)
            {
                if (api.IsKeyDown(vk))
                {
                    return true;
                }
            }

            return false;
        }

        private static NativeMethods.INPUT MakeKeyInput(ushort vk, bool keyUp, UIntPtr extraInfo)
        {
            return new NativeMethods.INPUT
            {
                type = NativeMethods.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (short)vk,
                        dwFlags = keyUp ? KeyEventKeyUp : 0,
                        dwExtraInfo = extraInfo,
                    },
                },
            };
        }
    }
}
