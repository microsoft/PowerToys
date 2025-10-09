#include <windows.h>
#include <tchar.h>
#include "ThemeScheduler.h"
#include "ThemeHelper.h"
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <stdio.h>
#include <string>
#include <LightSwitchSettings.h>
#include <common/utils/gpo.h>
#include <logger/logger_settings.h>
#include <logger/logger.h>
#include <utils/logger_helper.h>

SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;
static int g_lastUpdatedDay = -1;

VOID WINAPI ServiceMain(DWORD argc, LPTSTR* argv);
VOID WINAPI ServiceCtrlHandler(DWORD dwCtrl);
DWORD WINAPI ServiceWorkerThread(LPVOID lpParam);

// Entry point for the executable
int _tmain(int argc, TCHAR* argv[])
{
    DWORD parentPid = 0;
    bool debug = false;
    for (int i = 1; i < argc; ++i)
    {
        if (_tcscmp(argv[i], _T("--debug")) == 0)
            debug = true;
        else if (_tcscmp(argv[i], _T("--pid")) == 0 && i + 1 < argc)
            parentPid = _tstoi(argv[++i]);
    }

    // Try to connect to SCM
    wchar_t serviceName[] = L"LightSwitchService";
    SERVICE_TABLE_ENTRYW table[] = { { serviceName, ServiceMain }, { nullptr, nullptr } };

    LoggerHelpers::init_logger(L"LightSwitch", L"Service", LogSettings::lightSwitchLoggerName);

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
    g_StatusHandle = RegisterServiceCtrlHandler(_T("LightSwitchService"), ServiceCtrlHandler);
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

    SECURITY_ATTRIBUTES sa{ sizeof(sa) };
    sa.bInheritHandle = FALSE;
    sa.lpSecurityDescriptor = nullptr;

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
        Logger::info(L"[LightSwitchService] Stop requested, signaling worker thread to exit.");
        SetEvent(g_ServiceStopEvent);
        break;

    default:
        break;
    }
}

static void update_sun_times(auto& settings)
{
    double latitude = std::stod(settings.latitude);
    double longitude = std::stod(settings.longitude);

    SYSTEMTIME st;
    GetLocalTime(&st);

    SunTimes newTimes = CalculateSunriseSunset(latitude, longitude, st.wYear, st.wMonth, st.wDay);

    int newLightTime = newTimes.sunriseHour * 60 + newTimes.sunriseMinute;
    int newDarkTime = newTimes.sunsetHour * 60 + newTimes.sunsetMinute;
    try
    {
        auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"LightSwitch");
        values.add_property(L"lightTime", newLightTime);
        values.add_property(L"darkTime", newDarkTime);
        values.save_to_settings_file();

        Logger::info(L"[LightSwitchService] Updated sun times and saved to config.");
    }
    catch (const std::exception& e)
    {
        std::wstring wmsg(e.what(), e.what() + strlen(e.what()));
        Logger::error(L"[LightSwitchService] Exception during sun time update: {}", wmsg);
    }
    
}

DWORD WINAPI ServiceWorkerThread(LPVOID lpParam)
{
    DWORD parentPid = static_cast<DWORD>(reinterpret_cast<ULONG_PTR>(lpParam));
    HANDLE hParent = nullptr;
    if (parentPid)
        hParent = OpenProcess(SYNCHRONIZE, FALSE, parentPid);

    Logger::info(L"[LightSwitchService] Worker thread starting...");
    Logger::info(L"[LightSwitchService] Parent PID: {}", parentPid);

    // Initialize settings system
    LightSwitchSettings::instance().InitFileWatcher();

    // Open the manual override event created by the module interface
    HANDLE hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");

    auto applyTheme = [](int nowMinutes, int lightMinutes, int darkMinutes, const auto& settings) {
        bool isLightActive = false;

        if (lightMinutes < darkMinutes)
        {
            // Normal case: sunrise < sunset
            isLightActive = (nowMinutes >= lightMinutes && nowMinutes < darkMinutes);
        }
        else
        {
            // Wraparound case: e.g. light at 21:00, dark at 06:00
            isLightActive = (nowMinutes >= lightMinutes || nowMinutes < darkMinutes);
        }

        bool isSystemCurrentlyLight = GetCurrentSystemTheme();
        bool isAppsCurrentlyLight = GetCurrentAppsTheme();

        if (isLightActive)
        {
            if (settings.changeSystem && !isSystemCurrentlyLight)
                SetSystemTheme(true);
            if (settings.changeApps && !isAppsCurrentlyLight)
                SetAppsTheme(true);
        }
        else
        {
            if (settings.changeSystem && isSystemCurrentlyLight)
                SetSystemTheme(false);
            if (settings.changeApps && isAppsCurrentlyLight)
                SetAppsTheme(false);
        }
    };

    // --- At service start: immediately honor the schedule ---
    {
        SYSTEMTIME st;
        GetLocalTime(&st);
        int nowMinutes = st.wHour * 60 + st.wMinute;

        LightSwitchSettings::instance().LoadSettings();
        const auto& settings = LightSwitchSettings::instance().settings();

        applyTheme(nowMinutes, settings.lightTime + settings.sunrise_offset, settings.darkTime + settings.sunset_offset, settings);
    }

    // --- Main loop: wakes once per minute or stop/parent death ---
    for (;;)
    {
        HANDLE waits[2] = { g_ServiceStopEvent, hParent };
        DWORD count = hParent ? 2 : 1;

        SYSTEMTIME st;
        GetLocalTime(&st);
        int nowMinutes = st.wHour * 60 + st.wMinute;

        LightSwitchSettings::instance().LoadSettings();
        const auto& settings = LightSwitchSettings::instance().settings();

        // Refresh suntimes at day boundary
        if (g_lastUpdatedDay != st.wDay)
        {
            update_sun_times(settings);
            g_lastUpdatedDay = st.wDay;

            Logger::info(L"[LightSwitchService] Recalculated sun times at new day boundary.");
        }

        wchar_t msg[160];
        swprintf_s(msg,
                   L"[LightSwitchService] now=%02d:%02d | light=%02d:%02d | dark=%02d:%02d",
                   st.wHour,
                   st.wMinute,
                   settings.lightTime / 60,
                   settings.lightTime % 60,
                   settings.darkTime / 60,
                   settings.darkTime % 60);
        Logger::info(msg);

        // --- Manual override check ---
        bool manualOverrideActive = false;
        if (hManualOverride)
        {
            manualOverrideActive = (WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);
        }

        if (manualOverrideActive)
        {
            // Did we hit a scheduled boundary? (reset override at boundary)
            if (nowMinutes == (settings.lightTime + settings.sunrise_offset) % 1440 ||
                nowMinutes == (settings.darkTime + settings.sunset_offset) % 1440)
            {
                ResetEvent(hManualOverride);
                Logger::info(L"[LightSwitchService] Manual override cleared at boundary\n");
            }
            else
            {
                Logger::info(L"[LightSwitchService] Skipping schedule due to manual override\n");
                goto sleep_until_next_minute;
            }
        }

        // Apply theme logic (only runs if no manual override or override just cleared)
        applyTheme(nowMinutes, settings.lightTime + settings.sunrise_offset, settings.darkTime + settings.sunset_offset, settings);

    sleep_until_next_minute:
        GetLocalTime(&st);
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 50)
            msToNextMinute = 50;

        DWORD wait = WaitForMultipleObjects(count, waits, FALSE, msToNextMinute);
        if (wait == WAIT_OBJECT_0)
        {
            Logger::info(L"[LightSwitchService] Stop event triggered — exiting worker loop.");
            break;
        }
        if (hParent && wait == WAIT_OBJECT_0 + 1) // parent process exited
        {
            Logger::info(L"[LightSwitchService] Parent process exited — stopping service.");
            break;
        }

    }

    if (hManualOverride)
        CloseHandle(hManualOverride);
    if (hParent)
        CloseHandle(hParent);

    return 0;
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    if (powertoys_gpo::getConfiguredLightSwitchEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        wchar_t msg[160];
        swprintf_s(
            msg,
            L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        Logger::info(msg);
        return 0;
    }

    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    int rc = _tmain(argc, argv); // reuse your existing logic
    LocalFree(argv);
    return rc;
}