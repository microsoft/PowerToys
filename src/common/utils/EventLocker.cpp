#include "pch.h"
#include "EventLocker.h"

#include <utility>

EventLocker::EventLocker(HANDLE handle) :
    eventHandle(handle)
{
    if (eventHandle)
    {
        SetEvent(eventHandle);
    }
}

std::optional<EventLocker> EventLocker::Get(std::wstring eventName)
{
    EventLocker locker(std::move(eventName));
    if (!locker.eventHandle)
    {
        return std::nullopt;
    }

    return std::optional<EventLocker>(std::move(locker));
}

EventLocker::EventLocker(EventLocker&& other) noexcept :
    eventHandle(other.eventHandle)
{
    other.eventHandle = nullptr;
}

EventLocker& EventLocker::operator=(EventLocker&& other) noexcept
{
    if (this != &other)
    {
        eventHandle = other.eventHandle;
        other.eventHandle = nullptr;
    }

    return *this;
}

EventLocker::~EventLocker()
{
    if (eventHandle)
    {
        ResetEvent(eventHandle);
        CloseHandle(eventHandle);
        eventHandle = nullptr;
    }
}

EventLocker::EventLocker(std::wstring eventName)
{
    eventHandle = CreateEvent(nullptr, true, false, eventName.c_str());
    if (!eventHandle)
    {
        return;
    }

    SetEvent(eventHandle);
}
