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
extern int g_lastUpdatedDay = -1;
static ScheduleMode prevMode = ScheduleMode::Off;
static std::wstring prevLat, prevLon;
static int prevMinutes = -1;
static bool lastOverrideStatus = false;

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

    LightSwitchSettings::instance().InitFileWatcher();

    HANDLE hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");

    LightSwitchSettings::instance().LoadSettings();
    auto& settings = LightSwitchSettings::instance().settings();

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    // Handle initial theme application if necessary
    if (settings.scheduleMode != ScheduleMode::Off)
    {
        Logger::info(L"[LightSwitchService] Schedule mode is set to {}. Applying theme if necessary.", settings.scheduleMode);
        LightSwitchSettings::instance().ApplyThemeIfNecessary();
    }
    else
    {
        Logger::info(L"[LightSwitchService] Schedule mode is set to Off.");
    }

    g_lastUpdatedDay = st.wDay;
    Logger::info(L"[LightSwitchService] Initializing g_lastUpdatedDay to {}.", g_lastUpdatedDay);
    ULONGLONG lastSettingsReload = 0;

    // ticker loop
    for (;;)
    {
        HANDLE waits[2] = { g_ServiceStopEvent, hParent };
        DWORD count = hParent ? 2 : 1;
        bool skipRest = false;

        const auto& settings = LightSwitchSettings::instance().settings();

        // If the mode is set to Off, suspend the scheduler and avoid extra work
        if (settings.scheduleMode == ScheduleMode::Off)
        {
            Logger::info(L"[LightSwitchService] Schedule mode is OFF - suspending scheduler but keeping service alive.");

            if (!hManualOverride)
                hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");

            HANDLE waitsOff[4];
            DWORD countOff = 0;
            waitsOff[countOff++] = g_ServiceStopEvent;
            if (hParent)
                waitsOff[countOff++] = hParent;
            if (hManualOverride)
                waitsOff[countOff++] = hManualOverride;
            waitsOff[countOff++] = LightSwitchSettings::instance().GetSettingsChangedEvent();

            for (;;)
            {
                DWORD wait = WaitForMultipleObjects(countOff, waitsOff, FALSE, INFINITE);

                if (wait == WAIT_OBJECT_0)
                {
                    Logger::info(L"[LightSwitchService] Stop event triggered - exiting worker loop.");
                    goto cleanup;
                }
                if (hParent && wait == WAIT_OBJECT_0 + 1)
                {
                    Logger::info(L"[LightSwitchService] Parent exited - stopping service.");
                    goto cleanup;
                }

                if (wait == WAIT_OBJECT_0 + (hParent ? 2 : 1))
                {
                    Logger::info(L"[LightSwitchService] Manual override received while schedule OFF.");
                    ResetEvent(hManualOverride);
                    continue;
                }

                if (wait == WAIT_OBJECT_0 + (hParent ? 3 : 2))
                {
                    Logger::trace(L"[LightSwitchService] Settings change event triggered, reloading settings...");
                    ResetEvent(LightSwitchSettings::instance().GetSettingsChangedEvent());
                    const auto& newSettings = LightSwitchSettings::instance().settings();
                    lastSettingsReload = GetTickCount64();

                    if (newSettings.scheduleMode != ScheduleMode::Off)
                    {
                        Logger::info(L"[LightSwitchService] Schedule re-enabled, resuming normal loop.");
                        break;
                    }
                }
            }

            continue;
        }

        bool scheduleJustEnabled = (prevMode == ScheduleMode::Off && settings.scheduleMode != ScheduleMode::Off);
        prevMode = settings.scheduleMode;

        ULONGLONG nowTick = GetTickCount64();
        bool recentSettingsReload = (nowTick - lastSettingsReload < 2000);

        Logger::debug(L"[LightSwitchService] Current g_lastUpdatedDay value = {}.", g_lastUpdatedDay);

        // Manual Override Detection Logic
        bool manualOverrideActive = (hManualOverride && WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);

        if (manualOverrideActive != lastOverrideStatus)
        {
            Logger::debug(L"[LightSwitchService] Manual override active = {}", manualOverrideActive);
            lastOverrideStatus = manualOverrideActive;
        }

        if (settings.scheduleMode != ScheduleMode::Off && !recentSettingsReload && !scheduleJustEnabled && !manualOverrideActive)
        {
            bool currentSystemTheme = GetCurrentSystemTheme();
            bool currentAppsTheme = GetCurrentAppsTheme();

            SYSTEMTIME st;
            GetLocalTime(&st);
            int nowMinutes = st.wHour * 60 + st.wMinute;

            int lightBoundary = 0;
            int darkBoundary = 0;

            if (settings.scheduleMode == ScheduleMode::SunsetToSunrise)
            {
                lightBoundary = (settings.lightTime + settings.sunrise_offset) % 1440;
                darkBoundary = (settings.darkTime + settings.sunset_offset) % 1440;
            }
            else
            {
                lightBoundary = settings.lightTime;
                darkBoundary = settings.darkTime;
            }

            bool shouldBeLight = (lightBoundary < darkBoundary) ? (nowMinutes >= lightBoundary && nowMinutes < darkBoundary) : (nowMinutes >= lightBoundary || nowMinutes < darkBoundary);

            Logger::debug(L"[LightSwitchService] shouldBeLight = {}", shouldBeLight);

            bool systemMismatch = settings.changeSystem && (currentSystemTheme != shouldBeLight);
            bool appsMismatch = settings.changeApps && (currentAppsTheme != shouldBeLight);

            if (systemMismatch || appsMismatch)
            {
              // Make sure this is not because we crossed a boundary
              bool crossedBoundary = false;
              if (prevMinutes != -1)
              {
                  if (nowMinutes < prevMinutes)
                  {
                      // wrapped around midnight
                      crossedBoundary = (prevMinutes <= lightBoundary || nowMinutes >= lightBoundary) ||
                                        (prevMinutes <= darkBoundary || nowMinutes >= darkBoundary);
                  }
                  else
                  {
                      crossedBoundary = (prevMinutes < lightBoundary && nowMinutes >= lightBoundary) ||
                                        (prevMinutes < darkBoundary && nowMinutes >= darkBoundary);
                  }
              }

              if (crossedBoundary)
              {
                  Logger::info(L"[LightSwitchService] Missed boundary detected. Applying theme instead of triggering manual override.");
                  LightSwitchSettings::instance().ApplyThemeIfNecessary();
              }
              else
              {
                  Logger::info(L"[LightSwitchService] External {} theme change detected, enabling manual override.",
                               systemMismatch && appsMismatch ? L"system/app" :
                               systemMismatch                 ? L"system" :
                                                                L"app");
                  SetEvent(hManualOverride);
                  skipRest = true;
              }
            }
        }
        else
        {
            Logger::debug(L"[LightSwitchService] Skipping external-change detection (schedule off, recent reload, or just enabled).");
        }
        
        if (hManualOverride)
            manualOverrideActive = (WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);

        if (manualOverrideActive)
        {
            int lightBoundary = (settings.lightTime + settings.sunrise_offset) % 1440;
            int darkBoundary = (settings.darkTime + settings.sunset_offset) % 1440;

            SYSTEMTIME st;
            GetLocalTime(&st);
            nowMinutes = st.wHour * 60 + st.wMinute;

            bool crossedLight = false;
            bool crossedDark = false;

            if (prevMinutes != -1)
            {
                // this means we are in a new day cycle
                if (nowMinutes < prevMinutes)
                {
                    crossedLight = (prevMinutes <= lightBoundary || nowMinutes >= lightBoundary);
                    crossedDark = (prevMinutes <= darkBoundary || nowMinutes >= darkBoundary);
                }
                else
                {
                    crossedLight = (prevMinutes < lightBoundary && nowMinutes >= lightBoundary);
                    crossedDark = (prevMinutes < darkBoundary && nowMinutes >= darkBoundary);
                }
            }

            if (crossedLight || crossedDark)
            {
                ResetEvent(hManualOverride);
                Logger::info(L"[LightSwitchService] Manual override cleared after crossing schedule boundary.");
            }
            else
            {
                Logger::debug(L"[LightSwitchService] Skipping schedule due to manual override");
                skipRest = true;
            }
        }

        // Apply theme if nothing has made us skip
        if (!skipRest)
        {
            // Next two conditionals check for any updates necessary to the sun times.
            bool modeChangedToSunset = (prevMode != settings.scheduleMode &&
                                        settings.scheduleMode == ScheduleMode::SunsetToSunrise);
            bool coordsChanged = (prevLat != settings.latitude || prevLon != settings.longitude);

            if ((modeChangedToSunset || coordsChanged) && settings.scheduleMode == ScheduleMode::SunsetToSunrise)
            {
                SYSTEMTIME st;
                GetLocalTime(&st);

                Logger::info(L"[LightSwitchService] Mode or coordinates changed, recalculating sun times.");
                update_sun_times(settings);
                g_lastUpdatedDay = st.wDay;
                prevMode = settings.scheduleMode;
                prevLat = settings.latitude;
                prevLon = settings.longitude;
            }

            SYSTEMTIME st;
            GetLocalTime(&st);
            int nowMinutes = st.wHour * 60 + st.wMinute;

            if ((g_lastUpdatedDay != st.wDay) && (settings.scheduleMode == ScheduleMode::SunsetToSunrise))
            {
                update_sun_times(settings);
                g_lastUpdatedDay = st.wDay;
                prevMinutes = -1;
                Logger::info(L"[LightSwitchService] Recalculated sun times at new day boundary.");
            }

            // settings after any necessary updates.
            LightSwitchSettings::instance().LoadSettings();
            const auto& currentSettings = LightSwitchSettings::instance().settings();

            wchar_t msg[160];
            swprintf_s(msg,
                       L"[LightSwitchService] now=%02d:%02d | light=%02d:%02d | dark=%02d:%02d | mode=%s",
                       st.wHour,
                       st.wMinute,
                       currentSettings.lightTime / 60,
                       currentSettings.lightTime % 60,
                       currentSettings.darkTime / 60,
                       currentSettings.darkTime % 60,
                       ToString(currentSettings.scheduleMode).c_str());
            Logger::info(msg);

            LightSwitchSettings::instance().ApplyThemeIfNecessary();
        }

        // ─── Wait For Next Minute Tick Or Stop Event ────────────────────────────────
        SYSTEMTIME st;
        GetLocalTime(&st);
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 50)
            msToNextMinute = 50;

       prevMinutes = nowMinutes;

        DWORD wait = WaitForMultipleObjects(count, waits, FALSE, msToNextMinute);
        if (wait == WAIT_OBJECT_0)
        {
            Logger::info(L"[LightSwitchService] Stop event triggered - exiting worker loop.");
            break;
        }
        if (hParent && wait == WAIT_OBJECT_0 + 1)
        {
            Logger::info(L"[LightSwitchService] Parent process exited - stopping service.");
            break;
        }
    }

cleanup:
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