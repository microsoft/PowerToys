#include <windows.h>
#include <tchar.h>
#include "ThemeScheduler.h"
#include "ThemeHelper.h"

// Forward declarations of service functions (we’ll define them later)
VOID WINAPI ServiceMain(DWORD argc, LPTSTR* argv);
VOID WINAPI ServiceCtrlHandler(DWORD dwCtrl);
DWORD WINAPI ServiceWorkerThread(LPVOID lpParam);

// Entry point for the executable
int _tmain(int argc, TCHAR* argv[])
{
    // OPTIONAL: Run as a console for debugging
    if (argc > 1 && _tcscmp(argv[1], _T("--debug")) == 0)
    {
        ServiceWorkerThread(nullptr);
        return 0;
    }

    wchar_t serviceName[] = L"DarkModeService";

    SERVICE_TABLE_ENTRYW ServiceTable[] = {
        { serviceName, ServiceMain },
        { nullptr, nullptr }
    };

    if (!StartServiceCtrlDispatcher(ServiceTable))
    {
        return GetLastError();
    }

    return 0;
}

// Global service variables
SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;

// Called when the service is launched by Windows
VOID WINAPI ServiceMain(DWORD argc, LPTSTR* argv)
{
    g_StatusHandle = RegisterServiceCtrlHandler(_T("DarkModeService"), ServiceCtrlHandler);
    if (!g_StatusHandle)
        return;

    g_ServiceStatus.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
    g_ServiceStatus.dwCurrentState = SERVICE_START_PENDING;
    g_ServiceStatus.dwControlsAccepted = SERVICE_ACCEPT_STOP;
    SetServiceStatus(g_StatusHandle, &g_ServiceStatus);

    // Create an event used to signal when the service should stop
    g_ServiceStopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    if (!g_ServiceStopEvent)
    {
        g_ServiceStatus.dwCurrentState = SERVICE_STOPPED;
        g_ServiceStatus.dwWin32ExitCode = GetLastError();
        SetServiceStatus(g_StatusHandle, &g_ServiceStatus);
        return;
    }

    // Service is now running
    g_ServiceStatus.dwCurrentState = SERVICE_RUNNING;
    SetServiceStatus(g_StatusHandle, &g_ServiceStatus);

    // Start service work on a background thread
    HANDLE hThread = CreateThread(nullptr, 0, ServiceWorkerThread, nullptr, 0, nullptr);
    WaitForSingleObject(hThread, INFINITE);

    // Cleanup when the worker thread exits
    CloseHandle(g_ServiceStopEvent);
    g_ServiceStatus.dwCurrentState = SERVICE_STOPPED;
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
    OutputDebugString(L"[DarkModeService] Worker thread starting...\n");

    while (WaitForSingleObject(g_ServiceStopEvent, 0) != WAIT_OBJECT_0)
    {
        SYSTEMTIME st;
        GetLocalTime(&st);

        // Hardcoded location for now (Philadelphia)
        double latitude = 39.9526;
        double longitude = -75.1652;

        SunTimes sun = CalculateSunriseSunset(latitude, longitude, st.wYear, st.wMonth, st.wDay);

        // Convert current time to minutes since midnight
        int nowMinutes = st.wHour * 60 + st.wMinute;
        int sunriseMinutes = sun.sunriseHour * 60 + sun.sunriseMinute;
        int sunsetMinutes = sun.sunsetHour * 60 + sun.sunsetMinute;

        // Switch to light theme at sunrise
        if (nowMinutes == sunriseMinutes)
        {
            SetSystemTheme(true); // light
            SetAppsTheme(true);
            OutputDebugString(L"[DarkModeService] Switched to LIGHT theme\n");
        }

        // Switch to dark theme at sunset
        if (nowMinutes == sunsetMinutes)
        {
            SetSystemTheme(false); // dark
            SetAppsTheme(false);
            OutputDebugString(L"[DarkModeService] Switched to DARK theme\n");
        }

        // Sleep until next minute
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 0)
            msToNextMinute = 0;
        Sleep(msToNextMinute);
    }

    OutputDebugString(L"[DarkModeService] Worker thread is stopping...\n");
    return 0;
}
