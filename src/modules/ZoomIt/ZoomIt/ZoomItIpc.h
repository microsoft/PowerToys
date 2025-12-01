// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>

namespace ZoomItIpc
{
    inline constexpr wchar_t PIPE_NAME[] = L"\\\\.\\pipe\\powertoys_zoomit_cmd";

    enum class Command
    {
        Unknown = 0,
        Zoom,
        Draw,
        Break,
        LiveZoom,
        Snip,
        Record,
    };

    // Simple UTF-8 JSON payload: { "action": "<name>" }
    Command ParseCommand(const std::string& payloadUtf8);

    bool RunServer();

    bool SendCommand(Command cmd);
} // namespace ZoomItIpc
