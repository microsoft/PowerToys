// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "KeystrokeProcessor.h"

#include <cwctype>

#include <Windows.h>

#include "KeystrokeFormatter.h"

namespace InputHighlighter
{
    namespace
    {
        std::wstring CharText(const KeystrokeEvent& e)
        {
            if (e.ch == 0)
            {
                return std::wstring();
            }

            if (e.ch <= 0xFFFF)
            {
                return std::wstring(1, static_cast<wchar_t>(e.ch));
            }

            const char32_t v = e.ch - 0x10000;
            std::wstring result;
            result.push_back(static_cast<wchar_t>(0xD800 + (v >> 10)));
            result.push_back(static_cast<wchar_t>(0xDC00 + (v & 0x3FF)));
            return result;
        }

        bool IsNullOrWhiteSpace(const std::wstring& s)
        {
            if (s.empty())
            {
                return true;
            }

            for (const wchar_t c : s)
            {
                if (!std::iswspace(c))
                {
                    return false;
                }
            }

            return true;
        }
    }

    KeystrokeResult KeystrokeProcessor::Process(const KeystrokeEvent& e, DisplayMode displayMode)
    {
        const std::wstring formattedText = Formatter::Format(e);

        // Nothing to display.
        if (formattedText.empty())
        {
            return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
        }

        const bool isShortcut = Formatter::IsShortcut(e);

        switch (displayMode)
        {
        case DisplayMode::Last5:
            return KeystrokeResult{ KeystrokeAction::Add, formattedText };

        case DisplayMode::SingleCharactersOnly:
            if (isShortcut)
            {
                return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
            }

            return KeystrokeResult{ KeystrokeAction::Add, formattedText };

        case DisplayMode::ShortcutsOnly:
            if (!isShortcut)
            {
                return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
            }

            return KeystrokeResult{ KeystrokeAction::Add, formattedText };

        case DisplayMode::Stream:
            return ProcessStreamMode(e, isShortcut, formattedText);

        default:
            return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
        }
    }

    KeystrokeResult KeystrokeProcessor::ProcessStreamMode(const KeystrokeEvent& e, bool isShortcut, const std::wstring& formattedText)
    {
        // Backspace edits the current stream buffer.
        if (e.vk == VK_BACK)
        {
            if (!m_streamBuffer.empty())
            {
                m_streamBuffer.pop_back();

                if (m_streamBuffer.empty())
                {
                    return KeystrokeResult{ KeystrokeAction::RemoveLast, std::wstring() };
                }

                return KeystrokeResult{ KeystrokeAction::ReplaceLast, m_streamBuffer };
            }

            return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
        }

        // A shortcut (other than Space) resets the stream and shows itself.
        if (isShortcut && e.vk != VK_SPACE)
        {
            ResetBuffer();
            return KeystrokeResult{ KeystrokeAction::Add, formattedText };
        }

        const std::wstring text = CharText(e);

        // Whitespace ends the current word so the next character starts fresh.
        if (IsNullOrWhiteSpace(text))
        {
            ResetBuffer();
            return KeystrokeResult{ KeystrokeAction::None, std::wstring() };
        }

        m_streamBuffer += text;

        if (m_streamBuffer.size() == 1)
        {
            return KeystrokeResult{ KeystrokeAction::Add, m_streamBuffer };
        }

        return KeystrokeResult{ KeystrokeAction::ReplaceLast, m_streamBuffer };
    }
}
