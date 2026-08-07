// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// PTSettingsSvc — PowerToys Settings Service.
//
// The service runs as a virtual service account (NT SERVICE\PTSettingsSvc),
// owns the DACL on %ProgramData%\Microsoft\PowerToys\SettingsSvc\<ns>\<sid>\
// and is the only writer to the blob.bin file inside it.  Callers (Editor,
// SnapshotTool, runner, etc. — see Bindings.cpp) connect over a named pipe
// to GetBlob / PutBlob.

#include <windows.h>
#include <tchar.h>
#include <atomic>
#include <string>

#include "PipeServer.h"
#include "CallerAuth.h"
#include "SelfRegister.h"
#include "protocol/Protocol.h"
#include "protocol/PipeName.h"

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
    // Verbs:
    //   --console          run the pipe server in the foreground (local debug).
    //   --register  <SID>  elevated: create/re-point the per-user service +
    //                      provision the protected store + start.
    //   --repoint   <SID>  elevated: re-point binPath to this exe + restart.
    //   --unregister <SID> elevated: stop + delete the per-user service.
    // The first positional argument is the owner SID.  When running AS the
    // service (no verb), it flows in from the registered binPath as argv[1] and
    // is used as the pipe/owner SID; console/dev falls back to the current user.
    bool console = false;
    const wchar_t* verb = nullptr;
    std::wstring ownerSid;
    for (int i = 1; i < argc; ++i)
    {
        if (wcscmp(argv[i], L"--console") == 0)
        {
            console = true;
        }
        else if (wcscmp(argv[i], L"--register") == 0 ||
                 wcscmp(argv[i], L"--repoint") == 0 ||
                 wcscmp(argv[i], L"--unregister") == 0)
        {
            verb = argv[i];
        }
        else if (argv[i][0] != L'-' && ownerSid.empty())
        {
            ownerSid = argv[i];
        }
    }

    // Self-management verbs run elevated and exit; they never start the pipe.
    if (verb)
    {
        if (ownerSid.empty())
        {
            return static_cast<int>(E_INVALIDARG);
        }
        if (wcscmp(verb, L"--register") == 0)
        {
            return PTSettingsSvc::RunRegister(ownerSid);
        }
        if (wcscmp(verb, L"--repoint") == 0)
        {
            return PTSettingsSvc::RunRepoint(ownerSid);
        }
        return PTSettingsSvc::RunUnregister(ownerSid);
    }

    if (ownerSid.empty())
    {
        ownerSid = PTSettingsSvc::CurrentProcessUserSidString();
    }
    PTSettingsSvc::SetServiceOwnerSid(ownerSid);

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
