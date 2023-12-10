#include "pch.h"
#include "bug_report.h"
#include "Generated files/resource.h"
#include <common/utils/process_path.h>
#include <common/utils/resources.h>

std::atomic_bool isBugReportThreadRunning = false;

void launch_bug_report() noexcept
{
    std::wstring bug_report_path = get_module_folderpath();
    bug_report_path += L"\\Tools\\PowerToys.BugReportTool.exe";

    bool expected_isBugReportThreadRunning = false;
    if (isBugReportThreadRunning.compare_exchange_strong(expected_isBugReportThreadRunning, true))
    {
        std::thread([bug_report_path]() {
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

            isBugReportThreadRunning.store(false);
        }).detach();
    }
}
