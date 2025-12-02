#pragma once

#include <functional>
#include <string>
#include <thread>
#include <Windows.h>

namespace ProcessWaiter
{
    void OnProcessTerminate(std::wstring parent_pid, std::function<void(DWORD)> callback);
}
