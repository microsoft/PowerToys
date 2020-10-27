#pragma once

#include <future>

#include <winrt/Windows.ApplicationModel.h>

namespace winstore
{
    using winrt::Windows::ApplicationModel::StartupTaskState;

    bool running_as_packaged();
    std::future<void> switch_startup_task_state_async(const bool enabled);
    std::future<StartupTaskState> get_startup_task_status_async();
}
