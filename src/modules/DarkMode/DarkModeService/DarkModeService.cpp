#include <windows.h>
#include <tchar.h>
#include "ThemeScheduler.h"
#include "ThemeHelper.h"
#include <stdio.h>
#include <string>
#include <DarkModeSettings.h>
#include <common/utils/gpo.h>

// Global service variables
SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;

// Forward declarations of service functions (we’ll define them later)
VOID WINAPI ServiceMain(DWORD argc, LPTSTR* argv);
VOID WINAPI ServiceCtrlHandler(DWORD dwCtrl);
DWORD WINAPI ServiceWorkerThread(LPVOID lpParam);

// Entry point for the executable
int _tmain(int argc, TCHAR* argv[])
{
    // Parse args
    DWORD parentPid = 0;
    bool debug = false;
    for (int i = 1; i < argc; ++i)
    {
        if (_tcscmp(argv[i], _T("--debug")) == 0)
            debug = true;
        else if (_tcscmp(argv[i], _T("--pid")) == 0 && i + 1 < argc)
            parentPid = _tstoi(argv[++i]);
    }

    if (debug)
    {
        // Create a console window for debug output
        AllocConsole();
        FILE* f;
        freopen_s(&f, "CONOUT$", "w", stdout);
        freopen_s(&f, "CONOUT$", "w", stderr);

        // Optional: set a title so you can find it easily
        SetConsoleTitle(L"DarkModeService Debug");

        // Console mode (debug)
        g_ServiceStopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
        ServiceWorkerThread(reinterpret_cast<void*>(static_cast<ULONG_PTR>(parentPid)));
        CloseHandle(g_ServiceStopEvent);

        // Keep window open until a key is pressed (optional)
        // system("pause");

        FreeConsole();
        return 0;
    }

    // Try to connect to SCM
    wchar_t serviceName[] = L"DarkModeService";
    SERVICE_TABLE_ENTRYW table[] = { { serviceName, ServiceMain }, { nullptr, nullptr } };

    if (!StartServiceCtrlDispatcherW(table))
    {
        DWORD err = GetLastError();
        if (err == ERROR_FAILED_SERVICE_CONTROLLER_CONNECT) // not launched by SCM
        {
            g_ServiceStopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
            HANDLE hThread = CreateThread(
                nullptr, 0, ServiceWorkerThread, reinterpret_cast<void*>(static_cast<ULONG_PTR>(parentPid)), 0, nullptr);

            // Wait so the process stays alive
            WaitForSingleObject(hThread, INFINITE);
            CloseHandle(hThread);
            CloseHandle(g_ServiceStopEvent);
            return 0;
        }
        return static_cast<int>(err);
    }

    return 0;
}

// Called when the service is launched by Windows
VOID WINAPI ServiceMain(DWORD, LPTSTR*)
{
    g_StatusHandle = RegisterServiceCtrlHandler(_T("DarkModeService"), ServiceCtrlHandler);
    if (!g_StatusHandle)
        return;

    g_ServiceStatus.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
    g_ServiceStatus.dwControlsAccepted = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN;
    g_ServiceStatus.dwCurrentState = SERVICE_START_PENDING;
    SetServiceStatus(g_StatusHandle, &g_ServiceStatus);

    g_ServiceStopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    if (!g_ServiceStopEvent)
    {
        g_ServiceStatus.dwCurrentState = SERVICE_STOPPED;
        g_ServiceStatus.dwWin32ExitCode = GetLastError();
        SetServiceStatus(g_StatusHandle, &g_ServiceStatus);
        return;
    }

    g_ServiceStatus.dwCurrentState = SERVICE_RUNNING;
    SetServiceStatus(g_StatusHandle, &g_ServiceStatus);

    HANDLE hThread = CreateThread(nullptr, 0, ServiceWorkerThread, nullptr, 0, nullptr);
    WaitForSingleObject(hThread, INFINITE);
    CloseHandle(hThread);

    CloseHandle(g_ServiceStopEvent);
    g_ServiceStatus.dwCurrentState = SERVICE_STOPPED;
    g_ServiceStatus.dwWin32ExitCode = 0;
    SetServiceStatus(g_StatusHandle, &g_ServiceStatus);
}

VOID WINAPI ServiceCtrlHandler(DWORD dwCtrl)
{
    switch (dwCtrl)
    {
    case SERVICE_CONTROL_STOP:
        if (g_ServiceStatus.dwCurrentState != SERVICE_RUNNING)
            break;

        g_ServiceStatus.dwCurrentState = SERVICE_STOP_PENDING;
        SetServiceStatus(g_StatusHandle, &g_ServiceStatus);

        // Signal the service to stop
        SetEvent(g_ServiceStopEvent);
        break;

    default:
        break;
    }
}

DWORD WINAPI ServiceWorkerThread(LPVOID lpParam)
{
    DWORD parentPid = static_cast<DWORD>(reinterpret_cast<ULONG_PTR>(lpParam));
    HANDLE hParent = nullptr;
    if (parentPid)
        hParent = OpenProcess(SYNCHRONIZE, FALSE, parentPid);

    OutputDebugString(L"[DarkModeService] Worker thread starting...\n");

    // Initialize settings system
    DarkModeSettings::instance().InitFileWatcher();

    for (;;)
    {
        HANDLE waits[2] = { g_ServiceStopEvent, hParent };
        DWORD count = hParent ? 2 : 1;

        SYSTEMTIME st;
        GetLocalTime(&st);
        int nowMinutes = st.wHour * 60 + st.wMinute;

        DarkModeSettings::instance().LoadSettings();
        const auto& settings = DarkModeSettings::instance().settings();

        int lightMinutes = 0;
        int darkMinutes = 0;

        if (settings.useLocation)
        {
            SunTimes sun = CalculateSunriseSunset(
                std::stod(settings.latitude),
                std::stod(settings.longitude),
                st.wYear,
                st.wMonth,
                st.wDay);

            lightMinutes = sun.sunriseHour * 60 + sun.sunriseMinute;
            darkMinutes = sun.sunsetHour * 60 + sun.sunsetMinute;
        }
        else
        {
            // already stored in minutes since midnight
            lightMinutes = settings.lightTime;
            darkMinutes = settings.darkTime;
        }

        // Debug print
        wchar_t msg[160];
        swprintf_s(
            msg,
            L"[DarkModeService] now=%02d:%02d | light=%02d:%02d | dark=%02d:%02d\n",
            st.wHour,
            st.wMinute,
            lightMinutes / 60,
            lightMinutes % 60,
            darkMinutes / 60,
            darkMinutes % 60);
        OutputDebugString(msg);

#ifdef _DEBUG
        wprintf(L"%ls", msg);
#endif

        // Respect overrides first
        if (settings.forceLight)
        {
            SetSystemTheme(true);
            SetAppsTheme(true);
        }
        else if (settings.forceDark)
        {
            SetSystemTheme(false);
            SetAppsTheme(false);
        }
        else
        {
            if (nowMinutes == lightMinutes)
            {
                if (settings.changeSystem)
                    SetSystemTheme(true);
                if (settings.changeApps)
                    SetAppsTheme(true);
            }
            else if (nowMinutes == darkMinutes)
            {
                if (settings.changeSystem)
                    SetSystemTheme(false);
                if (settings.changeApps)
                    SetAppsTheme(false);
            }
        }

        // Sleep until next minute, wake if stop/parent dies
        GetLocalTime(&st);
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 50)
            msToNextMinute = 50;

        DWORD wait = WaitForMultipleObjects(count, waits, FALSE, msToNextMinute);
        if (wait == WAIT_OBJECT_0) // stop event
            break;
        if (hParent && wait == WAIT_OBJECT_0 + 1) // parent exited
            break;
    }

    if (hParent)
        CloseHandle(hParent);

    return 0;
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    winrt::init_apartment();

    if (powertoys_gpo::getConfiguredDarkModeEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        wchar_t msg[160];
        swprintf_s(
            msg,
            L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.\n");
        OutputDebugString(msg);
        return 0;
    }

    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    int rc = _tmain(argc, argv); // reuse your existing logic
    LocalFree(argv);
    return rc;
}