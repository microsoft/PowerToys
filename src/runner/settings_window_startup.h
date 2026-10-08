// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cstdint>
#include <mutex>
#include <optional>
#include <string>
#include <utility>

// Settings persists the modules' runtime state when it starts. Queue requests until
// module initialization finishes, and while the Settings process is being created.
class SettingsWindowStartup
{
public:
    enum class Action
    {
        None,
        Launch,
        Show,
    };

    struct Request
    {
        Action action = Action::None;
        std::optional<std::wstring> page;
    };

    Request Open(std::optional<std::wstring> page)
    {
        std::scoped_lock lock(mutex);
        if (stopped)
        {
            return {};
        }
        pending = Request{ Action::None, std::move(page) };
        return take_pending();
    }

    Request CompleteStartup()
    {
        std::scoped_lock lock(mutex);
        initialized = true;
        return take_pending();
    }

    // OOBE/SCOOBE are requested by the Runner only after modules are initialized.
    // Reserve their launch before CompleteStartup releases ordinary Settings requests.
    bool BeginOnboardingLaunch()
    {
        std::scoped_lock lock(mutex);
        if (stopped || launching || running)
        {
            return false;
        }
        launching = true;
        return true;
    }

    void ProcessCreated(std::uint32_t id)
    {
        std::scoped_lock lock(mutex);
        if (!stopped && launching)
        {
            processId = id;
        }
    }

    Request LaunchCompleted(std::uint32_t id)
    {
        std::scoped_lock lock(mutex);
        // Readiness can already be queued on the Runner thread when the worker
        // observes process exit. It must not complete a closed or newer launch.
        if (stopped || !launching || id == 0 || processId != id)
        {
            return {};
        }
        launching = false;
        running = true;
        return take_pending();
    }

    Request Closed(std::uint32_t id)
    {
        std::scoped_lock lock(mutex);
        if (processId != id)
        {
            return {};
        }
        launching = false;
        running = false;
        processId = 0;
        return take_pending();
    }

    void Stop()
    {
        std::scoped_lock lock(mutex);
        stopped = true;
        pending.reset();
    }

private:
    Request take_pending()
    {
        if (stopped || !initialized || launching || !pending)
        {
            return {};
        }

        auto request = std::move(*pending);
        pending.reset();
        request.action = running ? Action::Show : Action::Launch;
        launching = !running;
        return request;
    }

    std::mutex mutex;
    bool initialized = false;
    bool launching = false;
    bool running = false;
    bool stopped = false;
    std::uint32_t processId = 0;
    std::optional<Request> pending;
};
