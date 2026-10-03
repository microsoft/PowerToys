// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Owns a named mutex while the editor is open. The engine suspends remapping while this
    /// mutex has an owner, and finishes any key gesture already in progress before suspending.
    /// </summary>
    /// <remarks>
    /// Unlike a manual-reset event, mutex ownership is abandoned when its owning thread exits,
    /// including when the editor is terminated without running any managed cleanup.
    /// </remarks>
    internal static class EditorWindowLifetime
    {
        /// <summary>
        /// Must stay in sync with <c>KeyboardManagerConstants::EditorWindowMutexName</c>.
        /// </summary>
        private const string EditorWindowMutexName = @"Local\PowerToys_KeyboardManager_Mutex_EditorWindow";

        private const string EditorCaptureReadyEventName = @"Local\PowerToys_KeyboardManager_Event_CaptureReady";

        private static Mutex? _handle;

        private static int _ownerThreadId;

        /// <summary>
        /// The recorder must pass through any gesture that the engine started before recording.
        /// </summary>
        public static bool CanCaptureKeys()
        {
            try
            {
                // Do not retain this handle: if the engine exits, its event must disappear
                // instead of keeping a stale unsignaled state in the editor process.
                if (EventWaitHandle.TryOpenExisting(EditorCaptureReadyEventName, out var readyEvent))
                {
                    using (readyEvent)
                    {
                        return readyEvent.WaitOne(0);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                // If readiness cannot be checked, preserve releases for the engine.
                return false;
            }
        }

        /// <summary>
        /// Injected output must reach the engine and foreground app even if recording has begun.
        /// </summary>
        public static bool IsSourceKey(ulong extraInfo)
        {
            // Keep in sync with CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG and
            // KeyboardManagerConstants::KEYBOARDMANAGER_REPLAY_FLAG.
            return (extraInfo & 0x1) == 0 && extraInfo != 0x200;
        }

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
                var handle = new Mutex(false, EditorWindowMutexName);
                try
                {
                    // The engine may already have created this mutex. Explicitly acquire it, and
                    // allow the engine's nonblocking ownership probe to finish first.
                    if (!handle.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        handle.Dispose();
                        Logger.LogWarning("Could not acquire the editor lifetime mutex");
                        return;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // WaitOne grants ownership when the previous editor was terminated.
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }

                _handle = handle;
                _ownerThreadId = Environment.CurrentManagedThreadId;
                Logger.LogInfo("Acquired the editor lifetime mutex to suspend the KBM engine");
            }
            catch (Exception ex)
            {
                // Not fatal: the editor still works, the engine just keeps remapping while it is open.
                Logger.LogError($"Failed to acquire the editor lifetime mutex, the engine will stay active: {ex.Message}");
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
                if (Environment.CurrentManagedThreadId == _ownerThreadId)
                {
                    handle.ReleaseMutex();
                    Logger.LogInfo("Released the editor lifetime mutex to resume the KBM engine");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to release the editor lifetime mutex: {ex.Message}");
            }
            finally
            {
                handle.Dispose();
            }
        }
    }
}
