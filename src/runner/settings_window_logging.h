// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>

#include <common/logger/logger.h>
#include <common/utils/json.h>

namespace settings_window_logging
{
    inline void log_received_settings(const std::wstring& message)
    {
        try
        {
            // Parsing again isolates all nested values from the settings being dispatched.
            const auto logCopy = json::JsonObject::Parse(message);
            json::redact_security_keys(logCopy);
            Logger::info(L"dispatch_received_json: {}", logCopy.Stringify().c_str());
        }
        catch (...)
        {
            // Logging must neither expose the original payload nor block settings updates.
            Logger::info(L"dispatch_received_json: received settings message");
        }
    }
}
