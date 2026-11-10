// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <mutex>
#include <optional>
#include <string>
#include <utility>

// Settings persists the modules' runtime state when it starts. Defer requests only
// until module initialization finishes; subsequent requests use the normal window path.
class SettingsWindowStartup
{
public:
    struct Request
    {
        std::optional<std::wstring> page;
    };

    struct Completion
    {
        bool canceled = false;
        std::optional<Request> request;
    };

    bool DeferOrCancel(std::optional<std::wstring> page)
    {
        std::scoped_lock lock(mutex);
        if (stopped)
        {
            return true;
        }
        if (initialized)
        {
            return false;
        }
        pending = Request{ std::move(page) };
        return true;
    }

    Completion CompleteStartup(bool openSettings = false, std::optional<std::wstring> page = std::nullopt)
    {
        std::scoped_lock lock(mutex);
        if (stopped)
        {
            return { true, std::nullopt };
        }
        initialized = true;

        // A request received during startup is newer than the command-line request.
        auto request = std::exchange(pending, std::nullopt);
        if (!request && openSettings)
        {
            request = Request{ std::move(page) };
        }
        return { false, std::move(request) };
    }

    template<typename Launch>
    bool TryLaunch(Launch&& launch)
    {
        std::scoped_lock lock(mutex);
        if (stopped || !initialized)
        {
            return false;
        }

        // Publish the process ID before releasing this lock. Stop() either cancels
        // creation or waits until close_settings_window() can see the child.
        return std::forward<Launch>(launch)();
    }

    void Stop()
    {
        std::scoped_lock lock(mutex);
        stopped = true;
        pending.reset();
    }

private:
    std::mutex mutex;
    bool initialized = false;
    bool stopped = false;
    std::optional<Request> pending;
};
