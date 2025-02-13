#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/utils/winapi_error.h>
#include <filesystem>
#include <common/interop/shared_constants.h>
#include <atlbase.h>
#include <exdisp.h>
#include <comdef.h>
#include <common/utils/elevation.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, 
                      DWORD ul_reason_for_call, 
                      LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"file-actions-menu-shortcut-setting";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"FileActionsMenu";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"A module that adds an extra menu to Explorer.";

// Implement the PowerToy Module Interface and all the required methods.
class FileActionsMenu : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    Hotkey m_hotkey;

    // If we should always try to run FileActionsMenu non-elevated.
    bool m_alwaysRunNotElevated = true;

    HANDLE m_hProcess = 0;
    DWORD m_processPid = 0;

    HANDLE m_hInvokeEvent;

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(L"FileActionsMenu");

            parse_settings(settings);
        }
        catch (std::exception&)
        {
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                parse_hotkey(jsonHotkeyObject);
            }
            catch (...)
            {
                Logger::error("Failed to initialize FileActionsMenu hotkey settings");

                set_default_key_settings();
            }
        }
        else
        {
            Logger::info("FileActionsMenu settings are empty");
            set_default_key_settings();
        }
    }

    void set_default_key_settings()
    {
        Logger::info("FileActionsMenu is going to use default key settings");
        m_hotkey.win = false;
        m_hotkey.alt = true;
        m_hotkey.shift = false;
        m_hotkey.ctrl = true;
        m_hotkey.key = 'A';
    }

    void parse_hotkey(winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize FileActionsMenu start shortcut");
        }

        if (!m_hotkey.key)
        {
            Logger::info("FileActionsMenu is going to use default shortcut");
            m_hotkey.win = false;
            m_hotkey.alt = true;
            m_hotkey.shift = false;
            m_hotkey.ctrl = true;
            m_hotkey.key = 'A';
        }
    }

    bool is_desktop_window(HWND windowHandle)
    {
        // Similar to the logic in IsDesktopWindow in FileActionsMenu UI. Keep logic synced.
        // TODO: Refactor into same C++ class consumed by both.
        wchar_t className[MAX_PATH];
        if (GetClassName(windowHandle, className, MAX_PATH) == 0)
        {
            return false;
        }
        if (wcsncmp(className, L"Progman", MAX_PATH) !=0 && wcsncmp(className, L"WorkerW", MAX_PATH) != 0)
        {
            return false;
        }
        return FindWindowEx(windowHandle, NULL, L"SHELLDLL_DefView", NULL);
    }

    inline std::wstring GetErrorString(HRESULT handle)
    {
        _com_error err(handle);
        return err.ErrorMessage();
    }

    bool is_explorer_window(HWND windowHandle)
    {
        CComPtr<IShellWindows> spShellWindows;
        auto result = spShellWindows.CoCreateInstance(CLSID_ShellWindows);
        if (result != S_OK || spShellWindows == nullptr)
        {
            Logger::warn(L"Failed to create instance. {}", GetErrorString(result));
            return true; // Might as well assume it's possible it's an explorer window.
        }

        // Enumerate all Shell Windows to compare the window handle against.
        IUnknownPtr spEnum{};
        result = spShellWindows->_NewEnum(&spEnum);
        if (result != S_OK || spEnum == nullptr)
        {
            Logger::warn(L"Failed to list explorer Windows. {}", GetErrorString(result));
            return true; // Might as well assume it's possible it's an explorer window.
        }

        IEnumVARIANTPtr spEnumVariant{};
        result = spEnum.QueryInterface(__uuidof(spEnumVariant), &spEnumVariant);
        if (result != S_OK || spEnumVariant == nullptr)
        {
            Logger::warn(L"Failed to enum explorer Windows. {}", GetErrorString(result));
            spEnum->Release();
            return true; // Might as well assume it's possible it's an explorer window.
        }

        variant_t variantElement{};
        while (spEnumVariant->Next(1, &variantElement, NULL) == S_OK)
        {
            IWebBrowserApp* spWebBrowserApp;
            result = variantElement.pdispVal->QueryInterface(IID_IWebBrowserApp, reinterpret_cast<void**>(&spWebBrowserApp));
            if (result == S_OK)
            {
                HWND hwnd;
                result = spWebBrowserApp->get_HWND(reinterpret_cast<SHANDLE_PTR*>(&hwnd));
                if (result == S_OK)
                {
                    if (hwnd == windowHandle)
                    {
                        VariantClear(&variantElement);
                        spWebBrowserApp->Release();
                        spEnumVariant->Release();
                        spEnum->Release();
                        return true;
                    }
                }
                spWebBrowserApp->Release();
            }
            VariantClear(&variantElement);
        }

        spEnumVariant->Release();
        spEnum->Release();
        return false;
    }

    bool is_FileActionsMenu_or_explorer_or_desktop_window_focused()
    {
        HWND foregroundWindowHandle = GetForegroundWindow();
        if (foregroundWindowHandle == NULL)
        {
            return false;
        }

        DWORD pid{};
        if (GetWindowThreadProcessId(foregroundWindowHandle, &pid)!=0)
        {
            // If the foreground window is the FileActionsMenu window, send activation signal.
            if (m_processPid != 0 && pid == m_processPid)
            {
                return true;
            }
        }

        if (is_desktop_window(foregroundWindowHandle))
        {
            return true;
        }

        return is_explorer_window(foregroundWindowHandle);
    }

    void launch_process()
    {
        Logger::trace(L"Starting FileActionsMenu.UI process");

        std::wstring executable_args = L"";

        const auto modulePath = get_module_folderpath();
        std::wstring runExecutablePath = modulePath;
        runExecutablePath += L"\\WinUi3Apps\\PowerToys.FileActionsMenu.Ui.exe";
        std::optional<ProcessInfo> processStartedInfo = RunNonElevatedFailsafe(runExecutablePath, executable_args, modulePath, PROCESS_QUERY_INFORMATION | SYNCHRONIZE | PROCESS_TERMINATE);
        if (processStartedInfo.has_value())
        {
            m_processPid = processStartedInfo.value().processID;
            m_hProcess = processStartedInfo.value().processHandle.release();
        }
        else
        {
            Logger::error(L"FileActionsMenuViewer failed to start not elevated.");
        }
    }

public:
    FileActionsMenu()
    {
        init_settings();

        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_FILE_ACTIONS_MENU_SHARED_EVENT);
    };

    ~FileActionsMenu()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredFileActionsMenuEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::trace("FileActionsMenu::enable()");
        ResetEvent(m_hInvokeEvent);
        m_enabled = true;
        Trace::EnableFileActionsMenu(true);
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::trace("FileActionsMenu::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            auto result = TerminateProcess(m_hProcess, 1);
            if (result == 0)
            {
                int error = GetLastError();
                Logger::trace("Couldn't terminate the process. Last error: {}", error);
            }
        }

        m_enabled = false;
        Trace::EnableFileActionsMenu(false);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }

            return 1;
        }
        else
        {
            return 0;
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"FileActionsMenu hotkey pressed");

            // Only activate and consume the shortcut if a FileActionsMenu, explorer or desktop window is the foreground application.
            if (is_FileActionsMenu_or_explorer_or_desktop_window_focused())
            {
                // TODO: fix VK_SPACE DestroyWindow in viewer app
                launch_process();

                SetEvent(m_hInvokeEvent);

                Trace::FileActionsMenuInvoked();
                return true;
            }
        }

        return false;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        Trace::SettingsTelemetry(m_hotkey);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileActionsMenu();
}