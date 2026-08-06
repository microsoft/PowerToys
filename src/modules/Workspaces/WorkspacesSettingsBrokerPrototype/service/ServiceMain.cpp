// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "BrokerServer.h"
#include "../common/Protocol.h"

#include <windows.h>

namespace
{
    SERVICE_STATUS g_status{};
    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    HANDLE g_stopEvent = nullptr;

    void ReportStatus(DWORD state, DWORD error = ERROR_SUCCESS, DWORD waitHint = 0)
    {
        static DWORD checkpoint = 1;
        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = error;
        g_status.dwWaitHint = waitHint;
        g_status.dwControlsAccepted =
            state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        g_status.dwCheckPoint =
            state == SERVICE_RUNNING || state == SERVICE_STOPPED ? 0 : checkpoint++;
        if (g_statusHandle)
        {
            SetServiceStatus(g_statusHandle, &g_status);
        }
    }

    void WINAPI ServiceControl(DWORD control)
    {
        if (control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN)
        {
            ReportStatus(SERVICE_STOP_PENDING, ERROR_SUCCESS, 6000);
            if (g_stopEvent)
            {
                SetEvent(g_stopEvent);
            }
        }
    }

    void WINAPI ServiceEntry(DWORD, LPWSTR*)
    {
        g_statusHandle =
            RegisterServiceCtrlHandlerW(SettingsBrokerPrototype::kServiceName, ServiceControl);
        if (!g_statusHandle)
        {
            return;
        }

        ReportStatus(SERVICE_START_PENDING, ERROR_SUCCESS, 5000);
        g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!g_stopEvent)
        {
            ReportStatus(SERVICE_STOPPED, GetLastError());
            return;
        }

        ReportStatus(SERVICE_RUNNING);
        const DWORD result = SettingsBrokerPrototype::RunBrokerServer(g_stopEvent);
        CloseHandle(g_stopEvent);
        g_stopEvent = nullptr;
        ReportStatus(SERVICE_STOPPED, result);
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc == 2 && _wcsicmp(argv[1], L"--console") == 0)
    {
        g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!g_stopEvent)
        {
            return static_cast<int>(GetLastError());
        }
        SetConsoleCtrlHandler(
            [](DWORD) -> BOOL {
                if (g_stopEvent)
                {
                    SetEvent(g_stopEvent);
                }
                return TRUE;
            },
            TRUE);
        const DWORD result = SettingsBrokerPrototype::RunBrokerServer(g_stopEvent);
        CloseHandle(g_stopEvent);
        return static_cast<int>(result);
    }

    wchar_t serviceName[] = L"PTSettingsBrokerPrototype";
    SERVICE_TABLE_ENTRYW table[] = {
        { serviceName, ServiceEntry },
        { nullptr, nullptr },
    };
    if (!StartServiceCtrlDispatcherW(table))
    {
        return static_cast<int>(GetLastError());
    }
    return 0;
}
