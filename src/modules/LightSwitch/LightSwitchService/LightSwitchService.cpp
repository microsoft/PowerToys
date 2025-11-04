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
#include <LightSwitchServiceObserver.h>

SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;
extern int g_lastUpdatedDay = -1;
static ScheduleMode prevMode = ScheduleMode::Off;
static std::wstring prevLat, prevLon;
static int prevMinutes = -1;

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

    LightSwitchServiceObserver observer({ SettingId::LightTime,
                                          SettingId::DarkTime,
                                          SettingId::ScheduleMode,
                                          SettingId::Sunrise_Offset,
                                          SettingId::Sunset_Offset });

    HANDLE hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");

    auto applyTheme = [](int nowMinutes, int lightMinutes, int darkMinutes, const auto& settings) {
        bool isLightActive = (lightMinutes < darkMinutes) ? (nowMinutes >= lightMinutes && nowMinutes < darkMinutes) : (nowMinutes >= lightMinutes || nowMinutes < darkMinutes);

        bool isSystemCurrentlyLight = GetCurrentSystemTheme();
        bool isAppsCurrentlyLight = GetCurrentAppsTheme();

        if (isLightActive)
        {
            if (settings.changeSystem && !isSystemCurrentlyLight)
            {
                SetSystemTheme(true);
                Logger::info(L"[LightSwitchService] Changing system theme to light mode.");
            }
            if (settings.changeApps && !isAppsCurrentlyLight)
            {
                SetAppsTheme(true);
                Logger::info(L"[LightSwitchService] Changing apps theme to light mode.");
            }
        }
        else
        {
            if (settings.changeSystem && isSystemCurrentlyLight)
            {
                SetSystemTheme(false);
                Logger::info(L"[LightSwitchService] Changing system theme to dark mode.");
            }
            if (settings.changeApps && isAppsCurrentlyLight)
            {
                SetAppsTheme(false);
                Logger::info(L"[LightSwitchService] Changing apps theme to dark mode.");
            }
        }
    };

    LightSwitchSettings::instance().LoadSettings();
    auto& settings = LightSwitchSettings::instance().settings();

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    if (settings.scheduleMode != ScheduleMode::Off)
    {
        applyTheme(nowMinutes,
                   settings.lightTime + settings.sunrise_offset,
                   settings.darkTime + settings.sunset_offset,
                   settings);
        Logger::trace(L"[LightSwitchService] Initialized g_lastUpdatedDay = {}", g_lastUpdatedDay);
    }
    else
    {
        Logger::info(L"[LightSwitchService] Schedule mode is OFF - ticker suspended, waiting for manual action or mode change.");
    }

    g_lastUpdatedDay = st.wDay;
    ULONGLONG lastSettingsReload = 0;

    for (;;)
    {
        HANDLE waits[2] = { g_ServiceStopEvent, hParent };
        DWORD count = hParent ? 2 : 1;
        bool skipRest = false;

        const auto& settings = LightSwitchSettings::instance().settings();

        bool scheduleJustEnabled = (prevMode == ScheduleMode::Off && settings.scheduleMode != ScheduleMode::Off);
        prevMode = settings.scheduleMode;

        // ─── Handle "Schedule Off" Mode ─────────────────────────────────────────────
        if (settings.scheduleMode == ScheduleMode::Off)
        {
            Logger::info(L"[LightSwitchService] Schedule mode OFF - suspending scheduler but keeping service alive.");

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
                    LightSwitchSettings::instance().LoadSettings();
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

        // ─── Normal Schedule Loop ───────────────────────────────────────────────────
        ULONGLONG nowTick = GetTickCount64();
        bool recentSettingsReload = (nowTick - lastSettingsReload < 5000);

        if (g_lastUpdatedDay != -1)
        {
            bool manualOverrideActive = (hManualOverride && WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);

            if (settings.scheduleMode != ScheduleMode::Off && !recentSettingsReload && !scheduleJustEnabled)
            {
                Logger::debug(L"[LightSwitchService] Checking if manual override is active...");
                bool manualOverrideActive = (hManualOverride && WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);
                Logger::debug(L"[LightSwitchService] Manual override active = {}", manualOverrideActive);

                if (!manualOverrideActive)
                {
                    bool currentSystemTheme = GetCurrentSystemTheme();
                    bool currentAppsTheme = GetCurrentAppsTheme();

                    SYSTEMTIME st;
                    GetLocalTime(&st);
                    int nowMinutes = st.wHour * 60 + st.wMinute;

                    bool shouldBeLight = (settings.lightTime < settings.darkTime) ? (nowMinutes >= settings.lightTime && nowMinutes < settings.darkTime) : (nowMinutes >= settings.lightTime || nowMinutes < settings.darkTime);

                    Logger::debug(L"[LightSwitchService] shouldBeLight = {}", shouldBeLight);

                    if ((settings.changeSystem && (currentSystemTheme != shouldBeLight)) ||
                        (settings.changeApps && (currentAppsTheme != shouldBeLight)))
                    {
                        Logger::debug(L"[LightSwitchService] External theme change detected - enabling manual override");

                        if (!hManualOverride)
                        {
                            hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");
                            if (!hManualOverride)
                                hManualOverride = CreateEventW(nullptr, TRUE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");
                        }

                        if (hManualOverride)
                        {
                            SetEvent(hManualOverride);
                            Logger::info(L"[LightSwitchService] Detected manual theme change outside of LightSwitch. Triggering manual override.");
                            skipRest = true;
                        }
                    }
                }
            }
            else
            {
                Logger::debug(L"[LightSwitchService] Skipping external-change detection (schedule off, recent reload, or just enabled).");
            }
        }

        // ─── Apply Schedule Logic ───────────────────────────────────────────────────
        if (!skipRest)
        {
            bool modeChangedToSunset = (prevMode != settings.scheduleMode &&
                                        settings.scheduleMode == ScheduleMode::SunsetToSunrise);
            bool coordsChanged = (prevLat != settings.latitude || prevLon != settings.longitude);

            if ((modeChangedToSunset || coordsChanged) && settings.scheduleMode == ScheduleMode::SunsetToSunrise)
            {
                Logger::info(L"[LightSwitchService] Mode or coordinates changed, recalculating sun times.");
                update_sun_times(settings);
                SYSTEMTIME st;
                GetLocalTime(&st);
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

            LightSwitchSettings::instance().LoadSettings();
            const auto& currentSettings = LightSwitchSettings::instance().settings();

            wchar_t msg[160];
            swprintf_s(msg,
                       L"[LightSwitchService] now=%02d:%02d | light=%02d:%02d | dark=%02d:%02d | mode=%d",
                       st.wHour,
                       st.wMinute,
                       currentSettings.lightTime / 60,
                       currentSettings.lightTime % 60,
                       currentSettings.darkTime / 60,
                       currentSettings.darkTime % 60,
                       static_cast<int>(currentSettings.scheduleMode));
            Logger::info(msg);

            bool manualOverrideActive = false;
            if (hManualOverride)
                manualOverrideActive = (WaitForSingleObject(hManualOverride, 0) == WAIT_OBJECT_0);

            if (manualOverrideActive)
            {
                int lightBoundary = (currentSettings.lightTime + currentSettings.sunrise_offset) % 1440;
                int darkBoundary = (currentSettings.darkTime + currentSettings.sunset_offset) % 1440;

                bool crossedLight = false;
                bool crossedDark = false;

                if (prevMinutes != -1)
                {
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

                Logger::debug(L"[LightSwitchService] prevMinutes={} nowMinutes={} light={} dark={}",
                              prevMinutes,
                              nowMinutes,
                              lightBoundary,
                              darkBoundary);

                if (crossedLight || crossedDark)
                {
                    ResetEvent(hManualOverride);
                    Logger::info(L"[LightSwitchService] Manual override cleared after crossing schedule boundary.");
                }
                else
                {
                    Logger::info(L"[LightSwitchService] Skipping schedule due to manual override");
                    skipRest = true;
                }
            }

            if (!skipRest)
                applyTheme(nowMinutes,
                           currentSettings.lightTime + currentSettings.sunrise_offset,
                           currentSettings.darkTime + currentSettings.sunset_offset,
                           currentSettings);
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

void ApplyThemeNow()
{
    LightSwitchSettings::instance().LoadSettings();
    const auto& settings = LightSwitchSettings::instance().settings();

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    bool shouldBeLight = false;
    if (settings.lightTime < settings.darkTime)
        shouldBeLight = (nowMinutes >= settings.lightTime && nowMinutes < settings.darkTime);
    else
        shouldBeLight = (nowMinutes >= settings.lightTime || nowMinutes < settings.darkTime);

    bool isSystemCurrentlyLight = GetCurrentSystemTheme();
    bool isAppsCurrentlyLight = GetCurrentAppsTheme();

    Logger::info(L"[LightSwitchService] Applying (if needed) theme immediately due to schedule change.");

    if (shouldBeLight)
    {
        if (settings.changeSystem && !isSystemCurrentlyLight)
        {
            SetSystemTheme(true);
            Logger::info(L"[LightSwitchService] Changing system theme to light mode.");
        }
        if (settings.changeApps && !isAppsCurrentlyLight)
        {
            SetAppsTheme(true);
            Logger::info(L"[LightSwitchService] Changing apps theme to light mode.");
        }
    }
    else
    {
        if (settings.changeSystem && isSystemCurrentlyLight)
        {
            SetSystemTheme(false);
            Logger::info(L"[LightSwitchService] Changing system theme to dark mode.");
        }
        if (settings.changeApps && isAppsCurrentlyLight)
        {
            SetAppsTheme(false);
            Logger::info(L"[LightSwitchService] Changing apps theme to dark mode.");
        }
    }
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