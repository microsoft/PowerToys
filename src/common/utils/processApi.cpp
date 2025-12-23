#include "pch.h"
#include "processApi.h"

#include <utility>

std::vector<wil::unique_process_handle> getProcessHandlesByName(std::wstring_view processName, DWORD handleAccess)
{
    std::vector<wil::unique_process_handle> result;
    DWORD bytesRequired = 0;
    std::vector<DWORD> processIds;
    processIds.resize(4096 / sizeof(processIds[0]));
    auto processIdSize = static_cast<DWORD>(processIds.size() * sizeof(processIds[0]));
    EnumProcesses(processIds.data(), processIdSize, &bytesRequired);
    while (bytesRequired == processIdSize)
    {
        processIdSize *= 2;
        processIds.resize(processIdSize / sizeof(processIds[0]));
        EnumProcesses(processIds.data(), processIdSize, &bytesRequired);
    }
    processIds.resize(bytesRequired / sizeof(processIds[0]));

    handleAccess |= PROCESS_QUERY_LIMITED_INFORMATION;
    for (const DWORD processId : processIds)
    {
        try
        {
            wil::unique_process_handle hProcess{ OpenProcess(handleAccess, FALSE, processId) };
            wchar_t name[MAX_PATH + 1];
            DWORD length = MAX_PATH;
            if (!hProcess || !QueryFullProcessImageNameW(hProcess.get(), 0, name, &length))
            {
                continue;
            }
            if (processName == PathFindFileNameW(name))
            {
                result.push_back(std::move(hProcess));
            }
        }
        catch (...)
        {
        }
    }
    return result;
}
