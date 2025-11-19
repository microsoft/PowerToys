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
#include "LightSwitchStateManager.h"
#include <LightSwitchUtils.h>

SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;

VOID WINAPI ServiceMain(DWORD argc, LPTSTR* argv);
VOID WINAPI ServiceCtrlHandler(DWORD dwCtrl);
DWORD WINAPI ServiceWorkerThread(LPVOID lpParam);
void ApplyTheme(bool shouldBeLight);

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

void ApplyTheme(bool shouldBeLight)
{
    const auto& s = LightSwitchSettings::settings();

    if (s.changeSystem)
    {
        bool isSystemCurrentlyLight = GetCurrentSystemTheme();
        if (shouldBeLight != isSystemCurrentlyLight)
        {
            SetSystemTheme(shouldBeLight);
            Logger::info(L"[LightSwitchService] Changed system theme to {}.", shouldBeLight ? L"light" : L"dark");
        }
    }

    if (s.changeApps)
    {
        bool isAppsCurrentlyLight = GetCurrentAppsTheme();
        if (shouldBeLight != isAppsCurrentlyLight)
        {
            SetAppsTheme(shouldBeLight);
            Logger::info(L"[LightSwitchService] Changed apps theme to {}.", shouldBeLight ? L"light" : L"dark");
        }
    }
}

static void DetectAndHandleExternalThemeChange(LightSwitchStateManager& stateManager)
{
    const auto& s = LightSwitchSettings::settings();
    if (s.scheduleMode == ScheduleMode::Off)
        return;

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    // Compute effective boundaries (with offsets if needed)
    int effectiveLight = s.lightTime;
    int effectiveDark = s.darkTime;

    if (s.scheduleMode == ScheduleMode::SunsetToSunrise)
    {
        effectiveLight = (s.lightTime + s.sunrise_offset) % 1440;
        effectiveDark = (s.darkTime + s.sunset_offset) % 1440;
    }

    // Use shared helper (handles wraparound logic)
    bool shouldBeLight = ShouldBeLight(nowMinutes, effectiveLight, effectiveDark);

    // Compare current system/apps theme
    bool currentSystemLight = GetCurrentSystemTheme();
    bool currentAppsLight = GetCurrentAppsTheme();

    bool systemMismatch = s.changeSystem && (currentSystemLight != shouldBeLight);
    bool appsMismatch = s.changeApps && (currentAppsLight != shouldBeLight);

    // Trigger manual override only if mismatch and not already active
    if ((systemMismatch || appsMismatch) && !stateManager.GetState().isManualOverride)
    {
        Logger::info(L"[LightSwitchService] External theme change detected (Windows Settings). Entering manual override mode.");
        stateManager.OnManualOverride();
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

    // ────────────────────────────────────────────────────────────────
    // Initialization
    // ────────────────────────────────────────────────────────────────
    static LightSwitchStateManager stateManager;

    LightSwitchSettings::instance().InitFileWatcher();

    HANDLE hManualOverride = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");
    HANDLE hSettingsChanged = LightSwitchSettings::instance().GetSettingsChangedEvent();

    LightSwitchSettings::instance().LoadSettings();
    const auto& settings = LightSwitchSettings::instance().settings();

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    Logger::info(L"[LightSwitchService] Initialized at {:02d}:{:02d}.", st.wHour, st.wMinute);
    stateManager.SyncInitialThemeState();
    stateManager.OnTick(nowMinutes);

    // ────────────────────────────────────────────────────────────────
    // Worker Loop
    // ────────────────────────────────────────────────────────────────
    for (;;)
    {
        HANDLE waits[4];
        DWORD count = 0;
        waits[count++] = g_ServiceStopEvent;
        if (hParent)
            waits[count++] = hParent;
        if (hManualOverride)
            waits[count++] = hManualOverride;
        waits[count++] = hSettingsChanged;

        // Wait for one of these to trigger or for a new minute tick
        SYSTEMTIME st;
        GetLocalTime(&st);
        int msToNextMinute = (60 - st.wSecond) * 1000 - st.wMilliseconds;
        if (msToNextMinute < 50)
            msToNextMinute = 50;

        DWORD wait = WaitForMultipleObjects(count, waits, FALSE, msToNextMinute);

        if (wait == WAIT_TIMEOUT)
        {
            // regular minute tick
            GetLocalTime(&st);
            nowMinutes = st.wHour * 60 + st.wMinute;
            DetectAndHandleExternalThemeChange(stateManager);
            stateManager.OnTick(nowMinutes);
            continue;
        }

        if (wait == WAIT_OBJECT_0)
        {
            Logger::info(L"[LightSwitchService] Stop event triggered — exiting.");
            break;
        }

        if (hParent && wait == WAIT_OBJECT_0 + 1)
        {
            Logger::info(L"[LightSwitchService] Parent process exited — stopping service.");
            break;
        }

        if (hManualOverride && wait == WAIT_OBJECT_0 + (hParent ? 2 : 1))
        {
            Logger::info(L"[LightSwitchService] Manual override event detected.");
            stateManager.OnManualOverride();
            ResetEvent(hManualOverride);
            continue;
        }

        if (wait == WAIT_OBJECT_0 + (hParent ? (hManualOverride ? 3 : 2) : 2))
        {
            ResetEvent(hSettingsChanged);
            LightSwitchSettings::instance().LoadSettings();
            stateManager.OnSettingsChanged();
            continue;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Cleanup
    // ────────────────────────────────────────────────────────────────
    if (hManualOverride)
        CloseHandle(hManualOverride);
    if (hParent)
        CloseHandle(hParent);

    Logger::info(L"[LightSwitchService] Worker thread exiting cleanly.");
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