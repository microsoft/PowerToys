// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Signals the named event that the Keyboard Manager engine polls at the top of its low-level
    /// keyboard hook. While the event is set, the engine passes every key through untouched, so the
    /// remappings the user already configured do not fire while they are recording new ones in the
    /// editor.
    /// </summary>
    /// <remarks>
    /// This mirrors what the classic C++ editor does with <c>EventLocker</c> in
    /// <c>CreateEditKeyboardWindowImpl</c> / <c>CreateEditShortcutsWindowImpl</c>. The engine side is
    /// <c>KeyboardManager::HandleKeyboardHookEvent</c>, which returns early when
    /// <c>WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0</c>.
    /// </remarks>
    internal static class EditorWindowEventLock
    {
        /// <summary>
        /// Must stay in sync with <c>KeyboardManagerConstants::EditorWindowEventName</c>.
        /// </summary>
        private const string EditorWindowEventName = "PowerToys_KeyboardManager_Event_EditorWindow";

        private static EventWaitHandle? _handle;

        /// <summary>
        /// Suspends the engine. Safe to call more than once; only the first call has an effect.
        /// </summary>
        public static void Acquire()
        {
            if (_handle is not null)
            {
                return;
            }

            try
            {
                // Manual reset, matching the engine's own CreateEvent call. When the engine created
                // the event first this opens the existing one and the initial state is ignored.
                var handle = new EventWaitHandle(false, EventResetMode.ManualReset, EditorWindowEventName);
                handle.Set();
                _handle = handle;
                Logger.LogInfo($"Signaled {EditorWindowEventName} to suspend the KBM engine");
            }
            catch (Exception ex)
            {
                // Not fatal: the editor still works, the engine just keeps remapping while it is open.
                Logger.LogError($"Failed to signal {EditorWindowEventName}, the engine will stay active: {ex.Message}");
            }
        }

        /// <summary>
        /// Resumes the engine. Safe to call more than once.
        /// </summary>
        public static void Release()
        {
            if (_handle is not { } handle)
            {
                return;
            }

            _handle = null;

            try
            {
                handle.Reset();
                Logger.LogInfo($"Reset {EditorWindowEventName} to resume the KBM engine");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reset {EditorWindowEventName}: {ex.Message}");
            }
            finally
            {
                handle.Dispose();
            }
        }
    }
}
