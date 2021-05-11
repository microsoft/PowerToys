#include "pch.h"
#include "shortcut_guide.h"

#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

#include "trace.h"

// TODO: refactor singleton
OverlayWindow* instance = nullptr;

OverlayWindow::OverlayWindow()
{
    app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);
    app_key = L"Shortcut Guide";
    LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::shortcutGuideLoggerName);
    
    std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
    oldLogPath.append("ShortcutGuideLogs");
    LoggerHelpers::delete_old_log_folder(oldLogPath);

    shortcutEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::SHOW_SHORTCUT_GUIDE_SHARED_EVENT);
    if (!shortcutEvent)
    {
        Logger::error(L"Failed to create shortcutEvent. {}", get_last_error_or_default(GetLastError()));
    }

    Logger::info("Overlay Window is creating");
}

// Return the localized display name of the powertoy
const wchar_t* OverlayWindow::get_name()
{
    return app_name.c_str();
}

// Return the non localized key of the powertoy, this will be cached by the runner
const wchar_t* OverlayWindow::get_key()
{
    return app_key.c_str();
}

bool OverlayWindow::get_config(wchar_t* buffer, int* buffer_size)
{
    // Use events to update settings
    return true;
}

void OverlayWindow::set_config(const wchar_t* config)
{
    // Use events to update settings
}

constexpr int alternative_switch_hotkey_id = 0x2;
constexpr UINT alternative_switch_modifier_mask = MOD_WIN | MOD_SHIFT;
constexpr UINT alternative_switch_vk_code = VK_OEM_2;

bool OverlayWindow::StartProcess()
{
    unsigned long powertoys_pid = GetCurrentProcessId();
    std::wstring executable_args = L"";
    executable_args.append(std::to_wstring(powertoys_pid));

    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = L"modules\\ShortcutGuide\\ShortcutGuide\\PowerToys.ShortcutGuide.exe";
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = executable_args.data();
    if (ShellExecuteExW(&sei) == false)
    {
        Logger::error(L"Failed to start SG process. {}", get_last_error_or_default(GetLastError()));
        auto message = get_last_error_message(GetLastError());
        if (message.has_value())
        {
            Logger::error(message.value());
        }

        return false;
    }

    Logger::trace(L"Started SG process with pid={}", GetProcessId(sei.hProcess));
    m_hProcess = sei.hProcess;
    return true;
}

void OverlayWindow::TerminateProcess()
{
    if (m_hProcess)
    {
        if (WaitForSingleObject(m_hProcess, 0) != WAIT_OBJECT_0)
        {
            ::TerminateProcess(m_hProcess, 0);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }
        else
        {
            CloseHandle(m_hProcess);
            Logger::trace("SG process was already terminated");
        }
    }
    else
    {
        Logger::warn("Process handle is not initialized");
    }
}

void OverlayWindow::enable()
{
    Logger::info("Shortcut Guide is enabling");

    if (!_enabled)
    {
        Trace::EnableShortcutGuide(true);
        _enabled = true;
    }
    else
    {
        Logger::warn("Shortcut guide is already enabled");    
    }
}

void OverlayWindow::disable(bool trace_event)
{
    ResetEvent(shortcutEvent);
    Logger::info("Shortcut Guide is disabling");
    if (_enabled)
    {
        _enabled = false;
        if (trace_event)
        {
            Trace::EnableShortcutGuide(false);
        }

        TerminateProcess();
    }
    else
    {
        Logger::warn("Shortcut Guide is already disabled");
    }
}

void OverlayWindow::disable()
{
    this->disable(true);
}

bool OverlayWindow::is_enabled()
{
    return _enabled;
}

void OverlayWindow::destroy()
{
    ResetEvent(shortcutEvent);
    CloseHandle(shortcutEvent);
    this->disable(false);
    delete this;
}

size_t OverlayWindow::get_hotkeys(Hotkey* buffer, size_t buffer_size)
{
    if (buffer_size < 1)
    {
        return 1;
    }

    buffer[0].win = true;
    buffer[0].alt = false;
    buffer[0].shift = true;
    buffer[0].ctrl = false;
    buffer[0].key = VK_OEM_2;
    return 1;
}

bool OverlayWindow::on_hotkey(size_t hotkeyId)
{
    if (hotkeyId == 0)
    {
        Logger::trace("On hotkey");
        if ((m_hProcess && WaitForSingleObject(m_hProcess, 0) != WAIT_OBJECT_0) || StartProcess())
        {
            if (!SetEvent(shortcutEvent))
            {
                Logger::error(L"Failed to set shortcutEvent. {}", get_last_error_or_default(GetLastError()));
            }
        }

        return true;
    }

    return false;
}
