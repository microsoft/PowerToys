#include "pch.h"
#include "bug_report.h"
#include "Generated files/resource.h"
#include <common/utils/process_path.h>
#include <common/utils/resources.h>

BugReportManager& BugReportManager::instance()
{
    static BugReportManager instance;
    return instance;
}

void BugReportManager::register_callback(const BugReportCallback& callback)
{
    std::lock_guard<std::mutex> lock(m_callbacksMutex);
    m_callbacks.push_back(callback);
}

void BugReportManager::clear_callbacks()
{
    std::lock_guard<std::mutex> lock(m_callbacksMutex);
    m_callbacks.clear();
}

void BugReportManager::notify_observers(bool isRunning)
{
    std::lock_guard<std::mutex> lock(m_callbacksMutex);
    for (const auto& callback : m_callbacks)
    {
        try
        {
            callback(isRunning);
        }
        catch (...)
        {
            // Ignore callback exceptions to prevent one bad callback from affecting others
        }
    }
}

void BugReportManager::launch_bug_report() noexcept
{
    std::wstring bug_report_path = get_module_folderpath();
    bug_report_path += L"\\Tools\\PowerToys.BugReportTool.exe";

    bool expected_isBugReportRunning = false;
    if (m_isBugReportRunning.compare_exchange_strong(expected_isBugReportRunning, true))
    {
        // Notify observers that bug report is starting
        notify_observers(true);

        std::thread([this, bug_report_path]() {
            SHELLEXECUTEINFOW sei{ sizeof(sei) };
            sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
            sei.lpFile = bug_report_path.c_str();
            sei.nShow = SW_HIDE;
            if (ShellExecuteExW(&sei))
            {
                WaitForSingleObject(sei.hProcess, INFINITE);
                CloseHandle(sei.hProcess);
                static const std::wstring bugreport_success = GET_RESOURCE_STRING(IDS_BUGREPORT_SUCCESS);
                MessageBoxW(nullptr, bugreport_success.c_str(), L"PowerToys", MB_OK);
            }

            m_isBugReportRunning.store(false);
            // Notify observers that bug report has finished
            notify_observers(false);
        }).detach();
    }
    else
    {
        notify_observers(false);
    }
}

bool BugReportManager::is_bug_report_running() const noexcept
{
    return m_isBugReportRunning.load();
}

// Legacy functions for backward compatibility
void launch_bug_report() noexcept
{
    BugReportManager::instance().launch_bug_report();
}

bool is_bug_report_running() noexcept
{
    return BugReportManager::instance().is_bug_report_running();
}
