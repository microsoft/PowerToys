#include <windows.h>
#include <tchar.h>
#include "ThemeScheduler.h"
#include "ThemeHelper.h"
#include <stdio.h>

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

struct ThemeSettings
{
    bool useLocation = true;
    int lightHour = 7;
    int lightMinute = 0;
    int darkHour = 19;
    int darkMinute = 0;
    double latitude = 39.9526;
    double longitude = -75.1652;
    bool forceDark = false;
    bool forceLight = false;
};

ThemeSettings settings;

DWORD WINAPI ServiceWorkerThread(void* lpParam)
{
    DWORD parentPid = static_cast<DWORD>(reinterpret_cast<ULONG_PTR>(lpParam));
    HANDLE hParent = nullptr;
    if (parentPid)
        hParent = OpenProcess(SYNCHRONIZE, FALSE, parentPid); // may be null if not passed

    OutputDebugString(L"[DarkModeService] Worker thread starting...\n");

    for (;;)
    {
        HANDLE waits[2] = { g_ServiceStopEvent, hParent };
        DWORD count = hParent ? 2 : 1;

        // Do the work for this minute
        SYSTEMTIME st;
        GetLocalTime(&st);

        SunTimes sun = CalculateSunriseSunset(
            settings.latitude, settings.longitude, st.wYear, st.wMonth, st.wDay);

        int nowMin = st.wHour * 60 + st.wMinute;
        int lightMin = settings.useLocation ? sun.sunriseHour * 60 + sun.sunriseMinute : settings.lightHour * 60 + settings.lightMinute;
        int darkMin = settings.useLocation ? sun.sunsetHour * 60 + sun.sunsetMinute : settings.darkHour * 60 + settings.darkMinute;

        // Print light/dark minutes and HH:MM forms
        wchar_t msg[160];
        swprintf_s(
            msg,
            L"[DarkModeService] lightMin=%d (%02d:%02d) | darkMin=%d (%02d:%02d)\n",
            lightMin,
            lightMin / 60,
            lightMin % 60,
            darkMin,
            darkMin / 60,
            darkMin % 60);
        OutputDebugString(msg);

        // If you allocated a console in --debug mode, also print to it
        #ifdef _DEBUG
                wprintf(L"%ls", msg);
        #endif

        // Apply theme with a 1-minute window to avoid jitter misses
        auto within = [](int a, int b) { return std::abs(a - b) <= 0; }; // set to 0 or 1 if you want tolerance

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
            if (within(nowMin, lightMin))
            {
                SetSystemTheme(true);
                SetAppsTheme(true);
            }
            if (within(nowMin, darkMin))
            {
                SetSystemTheme(false);
                SetAppsTheme(false);
            }
        }

        // Sleep until the top of next minute, but wake early if stop or parent dies
        GetLocalTime(&st);
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 50)
            msToNextMinute = 50;

        DWORD wait = WaitForMultipleObjects(count, waits, FALSE, msToNextMinute);
        if (wait == WAIT_OBJECT_0)
            break; // stop event
        if (hParent && wait == WAIT_OBJECT_0 + 1)
            break; // parent exited
        // else timeout, loop again
    }

    if (hParent)
        CloseHandle(hParent);
    OutputDebugString(L"[DarkModeService] Worker thread stopping...\n");
    return 0;
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    int rc = _tmain(argc, argv); // reuse your existing logic
    LocalFree(argv);
    return rc;
}
