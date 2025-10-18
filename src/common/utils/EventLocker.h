#pragma once

#include <optional>
#include <string>
#include <windows.h>

class EventLocker
{
public:
    explicit EventLocker(HANDLE handle);

    static std::optional<EventLocker> Get(std::wstring eventName);

    EventLocker(EventLocker&) = delete;
    EventLocker& operator=(EventLocker&) = delete;

    EventLocker(EventLocker&& other) noexcept;
    EventLocker& operator=(EventLocker&& other) noexcept;

    ~EventLocker();

private:
    explicit EventLocker(std::wstring eventName);

    HANDLE eventHandle = nullptr;
};
