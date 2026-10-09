// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CliProtocol.h"

#include <functional>
#include <memory>
#include <string>
#include <Windows.h>

namespace light_switch_cli
{
    std::wstring GetPipeName();

    class CliPipeServer
    {
    public:
        using Handler = std::function<winrt::Windows::Data::Json::JsonObject(const Request&)>;

        struct Options
        {
            std::wstring pipeName;
            DWORD readTimeoutMs = 10000;
            DWORD writeTimeoutMs = 10000;
        };

        explicit CliPipeServer(Handler handler);
        CliPipeServer(Handler handler, Options options);
        ~CliPipeServer();

        CliPipeServer(const CliPipeServer&) = delete;
        CliPipeServer& operator=(const CliPipeServer&) = delete;

        // Acquires the only pipe instance before returning. Failure leaves GetLastError set.
        bool Start();

        // Cancels pipe I/O and joins the server thread. A running handler is allowed to finish;
        // callers must retain the state captured by the handler until Stop has returned.
        // Start/Stop must not be called from the handler itself.
        void Stop() noexcept;

    private:
        class Impl;
        std::unique_ptr<Impl> m_impl;
    };
}
