#include "pch.h"
#include "winstore.h"

#include <appmodel.h>

#include <winrt/Windows.ApplicationModel.h>

using winrt::Windows::ApplicationModel::StartupTask;

namespace
{
    const wchar_t* STARTUP_TASKID = L"PowerToysStartupTaskID";
}

namespace winstore
{
    bool running_as_packaged()
    {
        UINT32 length = 0;
        const auto rc = GetPackageFamilyName(GetCurrentProcess(), &length, nullptr);
        return rc != APPMODEL_ERROR_NO_PACKAGE;
    }

    std::future<StartupTaskState> get_startup_task_status_async()
    {
        const auto startupTask = co_await StartupTask::GetAsync(STARTUP_TASKID);
        co_return startupTask.State();
    }

    std::future<void> switch_startup_task_state_async(const bool enabled)
    {
        const auto startupTask = co_await StartupTask::GetAsync(STARTUP_TASKID);
        enum class action
        {
            none,
            enable,
            disable,
        } action_to_try = action::none;
        switch (startupTask.State())
        {
        case StartupTaskState::Disabled:
            if (enabled)
            {
                action_to_try = action::enable;
            }
            break;
        case StartupTaskState::Enabled:
            if (!enabled)
            {
                action_to_try = action::disable;
            }
            break;
        }
        try
        {
            switch (action_to_try)
            {
            case action::enable:
                co_await startupTask.RequestEnableAsync();
                break;
            case action::disable:
                startupTask.Disable();
                break;
            }
        }
        catch (...)
        {
            // We can't handle the error, in case we don't have a permission to change startup task state
        }
    }

}
