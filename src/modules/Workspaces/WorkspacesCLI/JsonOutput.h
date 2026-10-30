// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <string>
#include <windows.h>
#include <common/utils/json.h>

namespace WorkspacesCli
{
    class ConsoleOutputCodePage
    {
    public:
        explicit ConsoleOutputCodePage(bool enabled = true)
        {
            if (!enabled)
                return;
            const auto original = GetConsoleOutputCP();
            if (original && original != CP_UTF8)
            {
                if (SetConsoleOutputCP(CP_UTF8))
                    m_original = original;
                else
                    OutputDebugStringW(L"Workspaces CLI: could not set console output code page.\n");
            }
        }

        ~ConsoleOutputCodePage()
        {
            if (m_original && GetConsoleOutputCP() == CP_UTF8 && !SetConsoleOutputCP(m_original))
                OutputDebugStringW(L"Workspaces CLI: could not restore console output code page.\n");
        }

        ConsoleOutputCodePage(const ConsoleOutputCodePage&) = delete;
        ConsoleOutputCodePage& operator=(const ConsoleOutputCodePage&) = delete;

    private:
        UINT m_original = 0;
    };

    inline std::string FormatJson(const json::JsonObject& value)
    {
        // Keep the native serializer's tokens, ordering and escaping; add only insignificant whitespace.
        const auto compact = winrt::to_string(value.Stringify());
        std::string output;
        output.reserve(compact.size());
        size_t depth = 0;
        bool inString = false;
        bool escaped = false;
        const auto newline = [&] {
            output.push_back('\n');
            output.append(depth * 2, ' ');
        };
        for (size_t i = 0; i < compact.size(); ++i)
        {
            const char character = compact[i];
            if (inString)
            {
                output.push_back(character);
                if (escaped)
                    escaped = false;
                else if (character == '\\')
                    escaped = true;
                else if (character == '"')
                    inString = false;
                continue;
            }
            switch (character)
            {
            case '"':
                output.push_back(character);
                inString = true;
                break;
            case '{':
            case '[':
                output.push_back(character);
                ++depth;
                if (i + 1 < compact.size() && compact[i + 1] != (character == '{' ? '}' : ']'))
                    newline();
                break;
            case '}':
            case ']':
                --depth;
                if (output.back() != (character == '}' ? '{' : '['))
                    newline();
                output.push_back(character);
                break;
            case ',':
                output.push_back(character);
                newline();
                break;
            case ':':
                output.append(": ");
                break;
            default:
                output.push_back(character);
                break;
            }
        }
        return output;
    }
}
