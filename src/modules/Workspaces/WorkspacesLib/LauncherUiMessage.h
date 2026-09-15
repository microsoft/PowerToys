// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <optional>
#include <string>
#include <common/utils/json.h>
#include "LaunchDecision.h"

struct LauncherUiMessage
{
    enum class Type
    {
        Ready,
        Cancel,
        WarningShown,
        Heartbeat,
        Response,
    };

    Type type;
    std::wstring requestId;
    LaunchDecision decision{ LaunchDecision::InvalidResponse };

    static std::optional<LauncherUiMessage> Parse(const std::wstring& text)
    {
        try
        {
            const auto message = json::JsonValue::Parse(text).GetObjectW();
            if (message.GetNamedNumber(L"protocolVersion") != 1)
            {
                return std::nullopt;
            }
            const auto type = message.GetNamedString(L"type");
            if (type == L"ready" || type == L"cancel")
            {
                return LauncherUiMessage{ type == L"ready" ? Type::Ready : Type::Cancel };
            }
            LauncherUiMessage result{};
            result.requestId = message.GetNamedString(L"requestId");
            GUID id{};
            if (result.requestId.empty() || result.requestId.size() > 38 ||
                result.requestId.find(L'\0') != std::wstring::npos ||
                FAILED(CLSIDFromString(result.requestId.c_str(), &id)))
            {
                return std::nullopt;
            }
            if (type == L"warning-shown")
            {
                result.type = Type::WarningShown;
            }
            else if (type == L"heartbeat")
            {
                result.type = Type::Heartbeat;
            }
            else if (type == L"elevation-response")
            {
                result.type = Type::Response;
                const auto choice = message.GetNamedString(L"choice");
                if (choice != L"run" && choice != L"skip")
                {
                    return std::nullopt;
                }
                result.decision = choice == L"run" ? LaunchDecision::Approved : LaunchDecision::Skipped;
            }
            else
            {
                return std::nullopt;
            }
            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }
};
