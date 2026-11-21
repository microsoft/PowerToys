// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SeamlessCursorEngine.h"

#include <windows.h>

#include <atomic>
#include <functional>
#include <mutex>
#include <optional>
#include <thread>
#include <vector>

namespace seamless_cursor
{
    // All callbacks run on the overlay thread.
    struct CalibrationOverlayCallbacks
    {
        std::function<void()> opened;

        // The lines as currently placed, to try while adjusting; nothing to stop remapping until the overlay closes.
        std::function<void(std::optional<std::vector<BorderCalibration>>)> preview;

        // Receives true when a new calibration was saved.
        std::function<void(bool)> closed;

        // The overlay's "Stop at empty edges" switch changed that setting; receives the new value.
        std::function<void(bool)> stopChanged;
    };

    // Full-screen windows, one per monitor, where the user aligns marker lines across each shared border.
    // Runs on its own thread, so rendering never delays the mouse hook.
    class CalibrationOverlay
    {
    public:
        explicit CalibrationOverlay(CalibrationOverlayCallbacks callbacks);
        CalibrationOverlay(const CalibrationOverlay&) = delete;
        CalibrationOverlay& operator=(const CalibrationOverlay&) = delete;
        ~CalibrationOverlay();

        // Opens the overlay, or brings it to the front when it is already open.
        void Show();

        // Closes without saving and waits for the overlay thread to end.
        void Close();

        // Used by the edge map and the "Stop at empty edges" switch; takes effect the next time the overlay opens.
        void SetEngineOptions(EngineOptions options) noexcept;

    private:
        void Run(HANDLE started);

        CalibrationOverlayCallbacks m_callbacks;
        std::mutex m_lifecycleMutex; // Show and Close are called from the runner and from the calibrate listener.
        std::thread m_thread;
        std::mutex m_threadMutex; // Guards posting to the overlay thread against the thread deciding to exit.
        std::atomic<DWORD> m_threadId = 0; // Non-zero while the overlay thread accepts requests.
        std::atomic_bool m_stopWhereNothingIsAcross = true;
    };
}
