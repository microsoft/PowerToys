#include "pch.h"
#include "RestartManagement.h"

#include <RestartManager.h>
#include <Psapi.h>

#include <common/utils/processApi.h>

void RestartProcess(const std::wstring& processName)
{
    DWORD sessionHandle{};
    WCHAR sessionKey[CCH_RM_SESSION_KEY + 1];
    if (RmStartSession(&sessionHandle, 0, sessionKey) != ERROR_SUCCESS)
    {
        return;
    }
    auto processHandles = getProcessHandlesByName(processName, PROCESS_QUERY_INFORMATION);
    std::vector<RM_UNIQUE_PROCESS> pInfo;
    for (const auto& hProcess : processHandles)
    {
        FILETIME creationTime{};
        FILETIME _{};
        if (GetProcessTimes(hProcess.get(), &creationTime, &_, &_, &_))
        {
            pInfo.emplace_back(RM_UNIQUE_PROCESS{ GetProcessId(hProcess.get()), creationTime });
        }
    }

    if (pInfo.empty() ||
        RmRegisterResources(sessionHandle, 0, nullptr, sizeof(pInfo), pInfo.data(), 0, nullptr) != ERROR_SUCCESS)
    {
        return;
    }
    RmShutdown(sessionHandle, RmForceShutdown, nullptr);
    RmRestart(sessionHandle, 0, nullptr);
    RmEndSession(sessionHandle);
}
