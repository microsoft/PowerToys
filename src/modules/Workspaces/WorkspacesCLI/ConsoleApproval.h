// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <optional>
#include <string>
#include <string_view>
#include <common/utils/json.h>
#include <WorkspacesLib/LaunchDecision.h>

namespace WorkspacesCli
{
    std::wstring EscapeApprovalText(std::wstring_view text);

    struct ApprovalPromptText
    {
        std::wstring introduction;
        std::wstring appLabel;
        std::wstring pathLabel;
        std::wstring argumentsLabel;
        std::wstring reasonLabel;
        std::wstring statusLabel;
        std::wstring choices;
        std::wstring invalidChoice;
    };

    class ApprovalInput
    {
    public:
        std::optional<LaunchDecision> Push(wchar_t value);
        bool InvalidChoice() const { return m_invalidChoice; }
        size_t Length() const { return m_line.size(); }

    private:
        std::wstring m_line;
        bool m_overflow = false;
        bool m_invalidChoice = false;
    };

    class ConsoleApproval
    {
    public:
        ConsoleApproval(HANDLE input, HANDLE error, ApprovalPromptText text);
        ~ConsoleApproval();
        ConsoleApproval(const ConsoleApproval&) = delete;
        ConsoleApproval& operator=(const ConsoleApproval&) = delete;
        bool Begin(const json::JsonObject& request);
        std::optional<LaunchDecision> Poll();
        void End();
        bool Active() const { return m_active; }

    private:
        bool Write(std::wstring_view text);
        HANDLE m_input;
        HANDLE m_error;
        ApprovalPromptText m_text;
        ApprovalInput m_choice;
        DWORD m_originalMode = 0;
        DWORD m_promptMode = 0;
        bool m_active = false;
    };
}
