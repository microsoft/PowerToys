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
#include <NightLightRegistryObserver.h>
#include <trace.h>
#include "CliPipeServer.h"
#include "CliProtocol.h"
#include "ScheduleCommandQueue.h"
#include <array>
#include <algorithm>
#include <roapi.h>
#include <wil/resource.h>

SERVICE_STATUS g_ServiceStatus = {};
SERVICE_STATUS_HANDLE g_StatusHandle = nullptr;
HANDLE g_ServiceStopEvent = nullptr;

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

namespace
{
    using winrt::Windows::Data::Json::JsonObject;
    using winrt::Windows::Data::Json::JsonValue;

    JsonObject SerializeStatus(const StatusSnapshot& status)
    {
        JsonObject state;
        const auto themeName = [](const std::optional<bool>& value) {
            return value ? (*value ? L"light" : L"dark") : L"unknown";
        };
        state.SetNamedValue(L"systemTheme", JsonValue::CreateStringValue(themeName(status.systemLight)));
        state.SetNamedValue(L"appsTheme", JsonValue::CreateStringValue(themeName(status.appsLight)));
        state.SetNamedValue(L"changeSystem", JsonValue::CreateBooleanValue(status.config.changeSystem));
        state.SetNamedValue(L"changeApps", JsonValue::CreateBooleanValue(status.config.changeApps));
        state.SetNamedValue(L"scheduleMode", JsonValue::CreateStringValue(ToString(status.config.scheduleMode)));
        state.SetNamedValue(L"manualOverride", JsonValue::CreateBooleanValue(status.manualOverride));
        return state;
    }

    JsonObject SerializeResult(const ThemeCommandResult& result)
    {
        auto state = SerializeStatus(result.status);
        const wchar_t* code = result.errorCode.c_str();
        if (result.errorCode == L"SETTINGS_READ_FAILED")
            code = L"INVALID_CONFIGURATION";
        else if (result.errorCode == L"THEME_READ_FAILED" || result.errorCode == L"THEME_WRITE_FAILED")
            code = L"EXECUTION_FAILED";
        return result.success ? light_switch_cli::MakeSuccess(state) : light_switch_cli::MakeError(code, result.message, state);
    }
}

static DWORD RunServiceWorker(LPVOID lpParam)
{
    using light_switch_cli::Command;
    using light_switch_cli::Request;

    const DWORD parentPid = static_cast<DWORD>(reinterpret_cast<ULONG_PTR>(lpParam));
    wil::unique_handle parent(parentPid ? OpenProcess(SYNCHRONIZE, FALSE, parentPid) : nullptr);
    wil::unique_handle toggleRequests(OpenSemaphoreW(SYNCHRONIZE, FALSE, LIGHT_SWITCH_TOGGLE_REQUEST_SEMAPHORE));
    LightSwitchStateManager stateManager;
    auto& settingsStore = LightSwitchSettings::instance();
    ScheduleCommandQueue scheduleCommands;
    std::unique_ptr<NightLightRegistryObserver> nightLightWatcher;

    Logger::info(L"[LightSwitchService] Worker thread starting, parent PID: {}", parentPid);
    settingsStore.InitFileWatcher();
    HANDLE settingsChanged = settingsStore.GetSettingsChangedEvent();

    // Observer lifetime stays on the existing worker. In particular, Stop() joins
    // its callback thread and must never run while holding the state-manager lock.
    const auto updateNightLightWatcher = [&]() {
        const bool needed = LightSwitchSettings::settings().scheduleMode == ScheduleMode::FollowNightLight;
        if (needed && !nightLightWatcher)
        {
            nightLightWatcher = std::make_unique<NightLightRegistryObserver>(
                HKEY_CURRENT_USER, NIGHT_LIGHT_REGISTRY_PATH, [&stateManager]() { stateManager.OnNightLightChange(); });
        }
        else if (!needed && nightLightWatcher)
        {
            nightLightWatcher->Stop();
            nightLightWatcher.reset();
        }
    };

    settingsStore.LoadSettings();
    updateNightLightWatcher();
    stateManager.SyncInitialThemeState();

    const auto applyScheduleRequest = [&](const Request& request) -> JsonObject {
        LightSwitchConfig current;
        std::wstring error;
        if (!settingsStore.TryLoadSettings(current, error))
        {
            return light_switch_cli::MakeError(L"INVALID_CONFIGURATION", error);
        }

        ScheduleMode target = current.scheduleMode;
        if (request.command == Command::ScheduleDisable)
        {
            target = ScheduleMode::Off;
        }
        else if (request.mode)
        {
            // The protocol parser only accepts the three explicit enabled modes.
            target = FromString(*request.mode);
        }
        else if (target == ScheduleMode::Off)
        {
            return light_switch_cli::MakeError(
                L"INVALID_ARGUMENT", L"The schedule is off. Specify --mode fixed-hours, sunset-to-sunrise, or follow-night-light.");
        }

        if (target != current.scheduleMode && !settingsStore.TrySetScheduleMode(target, current, error))
        {
            return light_switch_cli::MakeError(L"EXECUTION_FAILED", error);
        }

        // The state manager ignores unchanged configurations, including the late
        // file-watcher echo of this same save. A no-op enable must not clear a
        // manual theme override.
        updateNightLightWatcher();
        const auto applied = stateManager.OnSettingsChanged();
        if (!applied.success)
        {
            return SerializeResult(applied);
        }
        const auto status = stateManager.GetStatusSnapshot();
        if (status.config.scheduleMode != target)
        {
            return light_switch_cli::MakeError(
                L"EXECUTION_FAILED", L"The schedule was changed by another settings update. Query status and try again.", SerializeStatus(status));
        }
        return light_switch_cli::MakeSuccess(SerializeStatus(status));
    };

    light_switch_cli::CliPipeServer cliServer([&](const Request& request) -> JsonObject {
        if (WaitForSingleObject(g_ServiceStopEvent, 0) == WAIT_OBJECT_0)
        {
            return light_switch_cli::MakeError(L"SERVICE_UNAVAILABLE", L"Light Switch is stopping.");
        }
        switch (request.command)
        {
        case Command::Status:
        {
            const auto status = stateManager.GetStatusSnapshot();
            if (!status.configurationAvailable)
            {
                return light_switch_cli::MakeError(L"INVALID_CONFIGURATION", L"Light Switch settings have not been loaded successfully.");
            }
            return status.systemLight && status.appsLight ? light_switch_cli::MakeSuccess(SerializeStatus(status)) : light_switch_cli::MakeError(L"EXECUTION_FAILED", L"Windows theme state could not be read.", SerializeStatus(status));
        }
        case Command::Light:
            return SerializeResult(stateManager.SetTheme(true));
        case Command::Dark:
            return SerializeResult(stateManager.SetTheme(false));
        case Command::Toggle:
            return SerializeResult(stateManager.ToggleTheme());
        case Command::ScheduleEnable:
        case Command::ScheduleDisable:
            return scheduleCommands.Submit(request);
        default:
            return light_switch_cli::MakeError(L"INVALID_ARGUMENT", L"Unknown Light Switch command.");
        }
    });
    const auto stopCli = wil::scope_exit([&]() {
        scheduleCommands.Stop();
        cliServer.Stop();
    });
    if (!cliServer.Start())
    {
        Logger::error(L"[LightSwitchService] Could not start the CLI pipe (error: {}).", GetLastError());
    }

    std::array<HANDLE, 5> waits{};
    DWORD count = 0;
    const DWORD stopIndex = count;
    waits[count++] = g_ServiceStopEvent;
    DWORD parentIndex = MAXDWORD;
    DWORD toggleIndex = MAXDWORD;
    if (parent)
    {
        parentIndex = count;
        waits[count++] = parent.get();
    }
    if (toggleRequests)
    {
        toggleIndex = count;
        waits[count++] = toggleRequests.get();
    }
    const DWORD settingsIndex = count;
    waits[count++] = settingsChanged;
    const DWORD scheduleIndex = count;
    waits[count++] = scheduleCommands.Event();

    for (;;)
    {
        SYSTEMTIME now;
        GetLocalTime(&now);
        const DWORD untilNextMinute = static_cast<DWORD>((std::max)(50, (60 - now.wSecond) * 1000 - now.wMilliseconds));
        const DWORD wait = WaitForMultipleObjects(count, waits.data(), FALSE, untilNextMinute);
        if (wait == WAIT_TIMEOUT)
        {
            stateManager.DetectExternalThemeChange();
            stateManager.OnTick();
        }
        else if (wait == WAIT_FAILED)
        {
            Logger::error(L"[LightSwitchService] Worker wait failed (error: {}).", GetLastError());
            break;
        }
        else
        {
            const DWORD index = wait - WAIT_OBJECT_0;
            if (index == stopIndex || index == parentIndex)
            {
                break;
            }
            if (index == toggleIndex)
            {
                const auto result = stateManager.ToggleTheme();
                if (!result.success)
                {
                    Logger::warn(L"[LightSwitchService] Toggle failed: {}", result.message);
                }
            }
            else if (index == settingsIndex)
            {
                ResetEvent(settingsChanged);
                settingsStore.LoadSettings();
                updateNightLightWatcher();
                stateManager.OnSettingsChanged();
            }
            else if (index == scheduleIndex)
            {
                scheduleCommands.ProcessPending(applyScheduleRequest);
            }
        }
    }

    // Release pipe callbacks waiting for this worker before joining the server.
    // In-flight theme operations retain access to stateManager until Stop returns.
    SetEvent(g_ServiceStopEvent);
    scheduleCommands.Stop();
    cliServer.Stop();
    if (nightLightWatcher)
    {
        nightLightWatcher->Stop();
        nightLightWatcher.reset();
    }
    Logger::info(L"[LightSwitchService] Worker thread exiting cleanly.");
    return 0;
}
DWORD WINAPI ServiceWorkerThread(LPVOID lpParam)
{
    try
    {
        winrt::check_hresult(RoInitialize(RO_INIT_MULTITHREADED));
        const auto apartmentCleanup = wil::scope_exit([]() { RoUninitialize(); });
        return RunServiceWorker(lpParam);
    }
    catch (const winrt::hresult_error& error)
    {
        Logger::error(L"[LightSwitchService] Worker failed: {}", error.message().c_str());
        return static_cast<DWORD>(error.code());
    }
    catch (...)
    {
        Logger::error(L"[LightSwitchService] Worker failed unexpectedly.");
        return ERROR_GEN_FAILURE;
    }
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    Trace::LightSwitch::RegisterProvider();

    if (powertoys_gpo::getConfiguredLightSwitchEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        wchar_t msg[160];
        swprintf_s(
            msg,
            L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        Logger::info(msg);
        Trace::LightSwitch::UnregisterProvider();
        return 0;
    }
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    int rc = _tmain(argc, argv); // reuse your existing logic
    LocalFree(argv);

    Trace::LightSwitch::UnregisterProvider();
    return rc;
}
