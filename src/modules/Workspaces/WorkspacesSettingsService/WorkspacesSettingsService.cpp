// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// PTSettingsSvc — PowerToys Settings Service.
//
// Design context — see Design-v6-Final.md in the Workspaces-EoP-Fix folder.
//
// The service runs as a virtual service account (NT SERVICE\PTSettingsSvc),
// owns the DACL on %ProgramData%\Microsoft\PowerToys\SettingsSvc\<ns>\<sid>\
// and is the only writer to the blob.bin file inside it.  Callers (Editor,
// SnapshotTool, runner, etc. — see Bindings.cpp) connect over a named pipe
// to GetBlob / PutBlob.

#include <windows.h>
#include <tchar.h>
#include <atomic>

#include "PipeServer.h"
#include "protocol/Protocol.h"

namespace
{
    SERVICE_STATUS g_status{};
    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    HANDLE g_stopEvent = nullptr;
    HANDLE g_workerThread = nullptr;

    void ReportStatus(DWORD state, DWORD waitHintMs = 0, DWORD exitCode = 0)
    {
        static DWORD checkPoint = 1;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = exitCode;
        g_status.dwWaitHint = waitHintMs;
        g_status.dwControlsAccepted =
            (state == SERVICE_START_PENDING) ? 0 : (SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN);
        g_status.dwCheckPoint = (state == SERVICE_RUNNING || state == SERVICE_STOPPED)
                                    ? 0
                                    : checkPoint++;
        if (g_statusHandle)
        {
            SetServiceStatus(g_statusHandle, &g_status);
        }
    }

    DWORD WINAPI WorkerThread(LPVOID)
    {
        return PTSettingsSvc::RunPipeServer(g_stopEvent);
    }

    VOID WINAPI ServiceCtrlHandler(DWORD ctrl)
    {
        switch (ctrl)
        {
        case SERVICE_CONTROL_STOP:
        case SERVICE_CONTROL_SHUTDOWN:
            ReportStatus(SERVICE_STOP_PENDING, 5000);
            if (g_stopEvent)
            {
                SetEvent(g_stopEvent);
            }
            break;
        default:
            break;
        }
    }

    VOID WINAPI ServiceMain(DWORD, LPTSTR*)
    {
        g_statusHandle = RegisterServiceCtrlHandlerW(
            PTSettingsSvc::kServiceName, ServiceCtrlHandler);
        if (!g_statusHandle)
        {
            return;
        }

        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        ReportStatus(SERVICE_START_PENDING, 3000);

        g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!g_stopEvent)
        {
            ReportStatus(SERVICE_STOPPED, 0, GetLastError());
            return;
        }

        g_workerThread = CreateThread(nullptr, 0, WorkerThread, nullptr, 0, nullptr);
        if (!g_workerThread)
        {
            DWORD err = GetLastError();
            CloseHandle(g_stopEvent);
            g_stopEvent = nullptr;
            ReportStatus(SERVICE_STOPPED, 0, err);
            return;
        }

        ReportStatus(SERVICE_RUNNING);

        WaitForSingleObject(g_workerThread, INFINITE);
        DWORD workerRc = 0;
        GetExitCodeThread(g_workerThread, &workerRc);
        CloseHandle(g_workerThread);
        g_workerThread = nullptr;

        CloseHandle(g_stopEvent);
        g_stopEvent = nullptr;

        ReportStatus(SERVICE_STOPPED, 0, workerRc);
    }
}

int wmain(int argc, wchar_t* argv[])
{
    // `--console` runs the pipe server in the foreground for local debugging
    // and prototype testing without going through SCM.  Production launch
    // always goes through StartServiceCtrlDispatcher.
    bool console = false;
    for (int i = 1; i < argc; ++i)
    {
        if (wcscmp(argv[i], L"--console") == 0)
        {
            console = true;
        }
    }

    if (console)
    {
        g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        SetConsoleCtrlHandler([](DWORD) -> BOOL {
            if (g_stopEvent) { SetEvent(g_stopEvent); }
            return TRUE;
        }, TRUE);
        DWORD rc = PTSettingsSvc::RunPipeServer(g_stopEvent);
        CloseHandle(g_stopEvent);
        return static_cast<int>(rc);
    }

    wchar_t name[] = L"PTSettingsSvc";
    SERVICE_TABLE_ENTRYW table[] = {
        { name, ServiceMain },
        { nullptr, nullptr },
    };

    if (!StartServiceCtrlDispatcherW(table))
    {
        return static_cast<int>(GetLastError());
    }
    return 0;
}
