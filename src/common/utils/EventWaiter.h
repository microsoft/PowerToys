#pragma once

#include <functional>
#include <thread>
#include <string>
#include <windows.h>

class EventWaiter
{
public:
    EventWaiter() = default;
    EventWaiter(const std::wstring& name, std::function<void(DWORD)> callback);
    EventWaiter(EventWaiter&) = delete;
    EventWaiter& operator=(EventWaiter&) = delete;
    EventWaiter(EventWaiter&& other) noexcept;
    EventWaiter& operator=(EventWaiter&& other) noexcept;
    ~EventWaiter();

private:
    HANDLE exitThreadEvent = nullptr;
    HANDLE waitingEvent = nullptr;
};
