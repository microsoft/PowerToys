#include "pch.h"
#include "AppLaunch.h"

HRESULT LaunchUI(HMODULE module)
{
    // Compute exe path
    std::wstring exe_path = get_module_folderpath(module);
    exe_path += L'\\';
    exe_path += constants::nonlocalizable::FileNameUIExe;

    STARTUPINFO startupInfo;
    ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
    startupInfo.cb = sizeof(STARTUPINFO);
    startupInfo.dwFlags = STARTF_USESHOWWINDOW;
    startupInfo.wShowWindow = SW_SHOWNORMAL;

    PROCESS_INFORMATION processInformation;
    std::wstring command_line = L"\"";
    command_line += exe_path;
    command_line += L"\"\0";

    CreateProcessW(
        NULL,
        command_line.data(),
        NULL,
        NULL,
        TRUE,
        0,
        NULL,
        NULL,
        &startupInfo,
        &processInformation);

    // Discard handles
    CloseHandle(processInformation.hProcess);
    CloseHandle(processInformation.hThread);

    return S_OK;
}