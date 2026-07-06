// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Pure keystroke display state machine, ported from the team4 KeystrokeOverlay
// KeystrokeProcessor.cs. Decides whether an incoming key adds a new pill,
// replaces/removes the current one, or is ignored, based on the display mode.
#pragma once

#include <string>

#include "KeystrokeTypes.h"

namespace InputHighlighter
{
    class KeystrokeProcessor
    {
    public:
        // Determine the visual action for an incoming key in the given display mode.
        KeystrokeResult Process(const KeystrokeEvent& e, DisplayMode displayMode);

        void ResetBuffer() { m_streamBuffer.clear(); }

    private:
        KeystrokeResult ProcessStreamMode(const KeystrokeEvent& e, bool isShortcut, const std::wstring& formattedText);

        std::wstring m_streamBuffer;
    };
}
