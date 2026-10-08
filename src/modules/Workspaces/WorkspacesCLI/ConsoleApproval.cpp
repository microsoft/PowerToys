// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include <windows.h>
#include <algorithm>
#include "ConsoleApproval.h"

namespace WorkspacesCli
{
    std::wstring EscapeApprovalText(std::wstring_view text)
    {
        static constexpr wchar_t hex[] = L"0123456789ABCDEF";
        std::wstring result;
        for (const auto value : text)
        {
            if (value < 0x20 || (value >= 0x7f && value <= 0x9f) ||
                value == 0x061c || value == 0x200e || value == 0x200f ||
                (value >= 0x2028 && value <= 0x202e) || (value >= 0x2066 && value <= 0x2069))
            {
                result += L"\\u";
                result += hex[(value >> 12) & 0xf];
                result += hex[(value >> 8) & 0xf];
                result += hex[(value >> 4) & 0xf];
                result += hex[value & 0xf];
            }
            else
                result += value;
        }
        return result;
    }

    std::optional<LaunchDecision> ApprovalInput::Push(wchar_t value)
    {
        m_invalidChoice = false;
        if (value == 3 || value == 27)
            return LaunchDecision::Canceled;
        if (value == 4 || value == 26)
            return LaunchDecision::UiUnavailable;
        if (value == L'\b')
        {
            if (!m_line.empty())
                m_line.pop_back();
            return std::nullopt;
        }
        if (value == L'\r' || value == L'\n')
        {
            if (!m_overflow && (m_line.empty() || m_line == L"s" || m_line == L"S"))
                return LaunchDecision::Skipped;
            if (!m_overflow && (m_line == L"a" || m_line == L"A"))
                return LaunchDecision::Approved;
            m_line.clear();
            m_overflow = false;
            m_invalidChoice = true;
            return std::nullopt;
        }
        if (m_line.size() >= 16)
            m_overflow = true;
        else
            m_line += value;
        return std::nullopt;
    }

    ConsoleApproval::ConsoleApproval(HANDLE input, HANDLE error, ApprovalPromptText text) :
        m_input(input), m_error(error), m_text(std::move(text))
    {
    }

    ConsoleApproval::~ConsoleApproval()
    {
        End();
    }

    bool ConsoleApproval::Write(std::wstring_view text)
    {
        size_t offset = 0;
        while (offset < text.size())
        {
            DWORD written{};
            if (!WriteConsoleW(m_error, text.data() + offset, static_cast<DWORD>((std::min)(text.size() - offset, size_t{ 4096 })), &written, nullptr) || !written)
                return false;
            offset += written;
        }
        return true;
    }

    bool ConsoleApproval::Begin(const json::JsonObject& request)
    {
        End();
        DWORD outputMode = 0;
        // Do not open CONIN$ to bypass redirected input, or solicit blind approval when stderr is redirected.
        if (!GetConsoleMode(m_input, &m_originalMode) || !GetConsoleMode(m_error, &outputMode))
            return false;
        const auto text = m_text.introduction + L"\r\n" +
                          m_text.appLabel + EscapeApprovalText(request.GetNamedString(L"appName")) + L"\r\n" +
                          m_text.pathLabel + EscapeApprovalText(request.GetNamedString(L"path")) + L"\r\n" +
                          m_text.argumentsLabel + EscapeApprovalText(request.GetNamedString(L"arguments")) + L"\r\n" +
                          m_text.reasonLabel + EscapeApprovalText(request.GetNamedString(L"reason")) + L"\r\n" +
                          m_text.statusLabel + EscapeApprovalText(request.GetNamedString(L"status")) + L"\r\n" +
                          m_text.choices;
        m_promptMode = (m_originalMode & ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_QUICK_EDIT_MODE)) |
                       ENABLE_EXTENDED_FLAGS | ENABLE_PROCESSED_INPUT;
        if (!SetConsoleMode(m_input, m_promptMode))
            return false;
        m_active = true;
        m_choice = {};
        // Pre-existing type-ahead must not approve a warning the user has not seen.
        if (!FlushConsoleInputBuffer(m_input) || !Write(text))
        {
            End();
            return false;
        }
        return true;
    }

    std::optional<LaunchDecision> ConsoleApproval::Poll()
    {
        if (!m_active)
            return LaunchDecision::UiUnavailable;
        DWORD available{};
        if (!GetNumberOfConsoleInputEvents(m_input, &available))
            return LaunchDecision::UiUnavailable;
        if (!available)
            return std::nullopt;
        INPUT_RECORD events[32]{};
        DWORD count{};
        if (!ReadConsoleInputW(m_input, events, (std::min)(available, DWORD{ ARRAYSIZE(events) }), &count))
            return LaunchDecision::UiUnavailable;
        for (DWORD i = 0; i < count; ++i)
        {
            if (events[i].EventType != KEY_EVENT || !events[i].Event.KeyEvent.bKeyDown)
                continue;
            const auto& key = events[i].Event.KeyEvent;
            const auto value = key.uChar.UnicodeChar;
            if (!value)
                continue;
            if ((key.dwControlKeyState & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0)
                continue;
            if ((key.dwControlKeyState & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0 &&
                value != 3 && value != 4 && value != 26)
                continue;
            const auto previousLength = m_choice.Length();
            if (const auto decision = m_choice.Push(value))
                return decision;
            if (m_choice.InvalidChoice())
            {
                if (!Write(L"\r\n" + m_text.invalidChoice + L"\r\n" + m_text.choices))
                    return LaunchDecision::UiUnavailable;
            }
            else if (value == L'\b')
            {
                if (previousLength && !Write(L"\b \b"))
                    return LaunchDecision::UiUnavailable;
            }
            else if (!Write(EscapeApprovalText(std::wstring_view(&value, 1))))
                return LaunchDecision::UiUnavailable;
        }
        return std::nullopt;
    }

    void ConsoleApproval::End()
    {
        if (!m_active)
            return;
        DWORD currentMode{};
        if (GetConsoleMode(m_input, &currentMode) && currentMode == m_promptMode)
        {
            if (!SetConsoleMode(m_input, m_originalMode))
                OutputDebugStringW(L"Workspaces CLI: could not restore console input mode.\n");
        }
        if (!Write(L"\r\n"))
            OutputDebugStringW(L"Workspaces CLI: confirmation output is unavailable.\n");
        m_active = false;
    }
}
