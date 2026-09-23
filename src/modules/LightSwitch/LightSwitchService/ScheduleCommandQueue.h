// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

#pragma once

#include "CliProtocol.h"
#include <wil/resource.h>
#include <chrono>
#include <functional>
#include <future>
#include <memory>
#include <mutex>
#include <utility>

// Schedule changes also start/stop the Night Light observer, which belongs to the
// service worker. The pipe thread waits for that worker's result, never while
// holding a state/settings lock. This synchronous producer needs one pending
// slot; the worker owns a started request separately. Theme commands bypass it.
class ScheduleCommandQueue
{
public:
    using Response = winrt::Windows::Data::Json::JsonObject;
    using Handler = std::function<Response(const light_switch_cli::Request&)>;

    ScheduleCommandQueue() :
        _event(CreateEventW(nullptr, FALSE, FALSE, nullptr))
    {
        if (!_event)
        {
            winrt::throw_last_error();
        }
    }

    ~ScheduleCommandQueue()
    {
        Stop();
    }

    HANDLE Event() const noexcept
    {
        return _event.get();
    }

    Response Submit(
        const light_switch_cli::Request& request,
        std::chrono::milliseconds timeout = std::chrono::seconds(55))
    {
        auto pending = std::make_shared<Pending>(request);
        auto result = pending->completion.get_future();
        {
            std::lock_guard lock(_mutex);
            if (_stopped)
            {
                return Unavailable();
            }
            if (_pending)
            {
                return light_switch_cli::MakeError(L"SERVER_BUSY", L"Light Switch is busy. Try again.");
            }
            _pending = pending;
            if (!SetEvent(_event.get()))
            {
                _pending.reset();
                return light_switch_cli::MakeError(L"EXECUTION_FAILED", L"Could not notify the Light Switch worker.");
            }
        }

        if (result.wait_for(timeout) == std::future_status::ready)
        {
            return result.get();
        }

        std::lock_guard lock(_mutex);
        // Never run a queued operation after its caller has already timed out.
        // An operation that has started cannot be rolled back by cancelling IPC.
        if (_pending == pending)
        {
            _pending.reset();
        }
        return light_switch_cli::MakeError(
            L"TIMEOUT",
            pending->started ? L"Light Switch did not finish in time. The schedule may have changed; query status before retrying." : L"The schedule request timed out before it could be executed.");
    }

    void ProcessPending(const Handler& handler)
    {
        std::shared_ptr<Pending> pending;
        {
            std::lock_guard lock(_mutex);
            if (!_pending || _stopped)
            {
                return;
            }
            pending = std::move(_pending);
            pending->started = true;
        }

        try
        {
            pending->completion.set_value(handler(pending->request));
        }
        catch (...)
        {
            pending->completion.set_value(light_switch_cli::MakeError(
                L"EXECUTION_FAILED", L"Light Switch could not apply the schedule."));
        }
    }

    void Stop()
    {
        std::shared_ptr<Pending> abandoned;
        {
            std::lock_guard lock(_mutex);
            _stopped = true;
            abandoned = std::move(_pending);
        }
        if (abandoned)
        {
            abandoned->completion.set_value(Unavailable());
        }
    }

private:
    struct Pending
    {
        explicit Pending(const light_switch_cli::Request& value) :
            request(value)
        {
        }

        light_switch_cli::Request request;
        std::promise<Response> completion;
        bool started = false;
    };

    static Response Unavailable()
    {
        return light_switch_cli::MakeError(L"SERVICE_UNAVAILABLE", L"Light Switch is stopping.");
    }

    wil::unique_handle _event;
    std::mutex _mutex;
    std::shared_ptr<Pending> _pending;
    bool _stopped = false;
};
