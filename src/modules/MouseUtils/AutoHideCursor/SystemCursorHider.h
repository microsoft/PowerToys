// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <array>
#include <vector>
#include <windows.h>

namespace auto_hide_cursor
{
    inline void RefreshCurrentCursor() noexcept
    {
        POINT cursorPosition{};
        if (!GetCursorPos(&cursorPosition))
        {
            return;
        }

        const auto window = WindowFromPoint(cursorPosition);
        if (!window)
        {
            return;
        }

        DWORD_PTR hitTest = HTCLIENT;
        SendMessageTimeoutW(
            window,
            WM_NCHITTEST,
            0,
            MAKELPARAM(cursorPosition.x, cursorPosition.y),
            SMTO_ABORTIFHUNG | SMTO_BLOCK,
            100,
            &hitTest);
        SendMessageTimeoutW(
            window,
            WM_SETCURSOR,
            reinterpret_cast<WPARAM>(window),
            MAKELPARAM(hitTest, WM_MOUSEMOVE),
            SMTO_ABORTIFHUNG | SMTO_BLOCK,
            100,
            nullptr);
    }

    inline bool RestoreSystemCursors() noexcept
    {
        if (!SystemParametersInfoW(SPI_SETCURSORS, 0, nullptr, 0))
        {
            return false;
        }

        RefreshCurrentCursor();
        return true;
    }

    // SetSystemCursor is the only supported API that crosses application boundaries.
    // The worker/module supervision pair guarantees that the user's configured scheme is reloaded.
    class SystemCursorHider
    {
    public:
        ~SystemCursorHider()
        {
            Restore();
        }

        bool Hide() noexcept
        {
            if (m_hidden)
            {
                return true;
            }

            for (const auto cursorId : systemCursorIds)
            {
                const auto transparentCursor = CreateTransparentCursor();
                if (!transparentCursor)
                {
                    RestoreSystemCursors();
                    return false;
                }

                if (!SetSystemCursor(transparentCursor, cursorId))
                {
                    const auto error = GetLastError();
                    DestroyCursor(transparentCursor);
                    RestoreSystemCursors();
                    SetLastError(error);
                    return false;
                }
            }

            m_hidden = true;
            return true;
        }

        bool Restore() noexcept
        {
            if (!m_hidden)
            {
                return true;
            }

            if (!RestoreSystemCursors())
            {
                return false;
            }

            m_hidden = false;
            return true;
        }

    private:
        static HCURSOR CreateTransparentCursor() noexcept
        {
            const auto width = GetSystemMetrics(SM_CXCURSOR);
            const auto height = GetSystemMetrics(SM_CYCURSOR);
            if (width <= 0 || height <= 0)
            {
                return nullptr;
            }

            const auto bytesPerScanLine = ((static_cast<size_t>(width) + 15u) / 16u) * 2u;
            const auto maskSize = bytesPerScanLine * static_cast<size_t>(height);
            std::vector<BYTE> andMask(maskSize, 0xFF);
            std::vector<BYTE> xorMask(maskSize, 0x00);

            return CreateCursor(
                nullptr,
                0,
                0,
                width,
                height,
                andMask.data(),
                xorMask.data());
        }

        inline static constexpr std::array<DWORD, 13> systemCursorIds = {
            32512, // OCR_NORMAL
            32513, // OCR_IBEAM
            32514, // OCR_WAIT
            32515, // OCR_CROSS
            32516, // OCR_UP
            32642, // OCR_SIZENWSE
            32643, // OCR_SIZENESW
            32644, // OCR_SIZEWE
            32645, // OCR_SIZENS
            32646, // OCR_SIZEALL
            32648, // OCR_NO
            32649, // OCR_HAND
            32650, // OCR_APPSTARTING
        };

        bool m_hidden = false;
    };
}
