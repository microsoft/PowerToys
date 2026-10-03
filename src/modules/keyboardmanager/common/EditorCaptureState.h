// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

// Keep this protocol in sync with the WinUI editor's EditorCaptureState.
// Releases from a gesture that started before recording must reach the engine.
class EditorCaptureState
{
public:
    bool ShouldCapture(const LowlevelKeyboardEvent& event, bool engineReady) noexcept
    {
        const auto extraInfo = event.lParam->dwExtraInfo;
        if ((extraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) ||
            extraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_REPLAY_FLAG)
        {
            return false;
        }

        if (!ready)
        {
            ready = engineReady;
            if (event.wParam == WM_KEYUP || event.wParam == WM_SYSKEYUP)
            {
                return false;
            }
        }

        return ready;
    }

    bool IsReady() const noexcept
    {
        return ready;
    }

    void Reset() noexcept
    {
        ready = false;
    }

private:
    bool ready = false;
};
