#pragma once
#include "pch.h"
#include "common/interop/shared_constants.h"

class NativeEventWaiter
{
    static const int timeout = 1000;

    HANDLE event_handle;
    std::function<void()> action;
    std::atomic<bool> aborting;

    void run();
    std::thread running_thread;

public:

    NativeEventWaiter(const std::wstring& event_name, std::function<void()> action);
    ~NativeEventWaiter();
};
