#include "pch.h"
#include "RestartManagement.h"

#include <RestartManager.h>
#include <Psapi.h>

std::vector<RM_UNIQUE_PROCESS> GetProcessInfoByName(const std::wstring& processName)
{
    DWORD bytesReturned{};
    std::vector<DWORD> processIds{};
    processIds.resize(1024);
    DWORD processIdSize{ (DWORD)processIds.size() * sizeof(DWORD) };
    EnumProcesses(processIds.data(), processIdSize, &bytesReturned);
    while (bytesReturned == processIdSize)
    {
        processIdSize *= 2;
        processIds.resize(processIdSize / sizeof(DWORD));
        EnumProcesses(processIds.data(), processIdSize, &bytesReturned);
    }
    std::vector<RM_UNIQUE_PROCESS> pInfos{};
    for (const DWORD& processId : processIds)
    {
        HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
        if (hProcess)
        {
            wchar_t name[MAX_PATH];
            if (GetProcessImageFileName(hProcess, name, MAX_PATH) > 0)
            {
                if (processName == PathFindFileName(name))
                {
                    FILETIME creationTime{};
                    FILETIME exitTime{};
                    FILETIME kernelTime{};
                    FILETIME userTime{};
                    if (GetProcessTimes(hProcess, &creationTime, &exitTime, &kernelTime, &userTime))
                    {
                        pInfos.push_back({ processId, creationTime });
                    }
                }
            }
            CloseHandle(hProcess);
        }
    }
    return pInfos;
}

void RestartProcess(const std::wstring& processName)
{
    DWORD sessionHandle{};
    WCHAR sessionKey[CCH_RM_SESSION_KEY + 1];
    if (RmStartSession(&sessionHandle, 0, sessionKey) != ERROR_SUCCESS)
    {
        return;
    }
    std::vector<RM_UNIQUE_PROCESS> pInfo = GetProcessInfoByName(processName);
    if (pInfo.empty() ||
        RmRegisterResources(sessionHandle, 0, nullptr, sizeof(pInfo), pInfo.data(), 0, nullptr) != ERROR_SUCCESS)
    {
        return;
    }
    RmShutdown(sessionHandle, RmForceShutdown, nullptr);
    RmRestart(sessionHandle, 0, nullptr);
    RmEndSession(sessionHandle);
}
