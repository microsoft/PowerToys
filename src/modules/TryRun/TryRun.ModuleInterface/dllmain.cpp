// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/utils/elevation.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <modules/interface/powertoy_module_interface.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr wchar_t ModuleKey[] = L"TryRun";
    constexpr wchar_t MenuProperty[] = L"show_in_context_menu";
    constexpr size_t MaximumSettingsCharacters = 32768;
    constexpr DWORD RegistrationTimeoutMs = 15000;

    bool CanManageRegistration()
    {
        wil::unique_handle token;
        TOKEN_ELEVATION elevation{};
        DWORD returned = 0;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()) ||
            !GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &returned))
        {
            Logger::error(L"Try Run cannot verify the Runner's elevation. Explorer registration was not changed.");
            return false;
        }

        if (elevation.TokenIsElevated != 0)
        {
            Logger::warn(L"Try Run integration requires PowerToys to run without administrator privileges. Restart PowerToys normally to enable Try Run and manage its Explorer menu.");
            return false;
        }

        return true;
    }

    bool ReadMenuSetting(const json::JsonObject& settings)
    {
        if (settings.GetNamedString(L"name") != ModuleKey || settings.GetNamedString(L"version") != L"1")
        {
            throw winrt::hresult_invalid_argument(L"Unsupported Try Run settings identity or version.");
        }

        return settings.GetNamedObject(L"properties").GetNamedObject(MenuProperty).GetNamedBoolean(L"value");
    }

    json::JsonObject SettingsSnapshot(bool showMenu)
    {
        json::JsonObject toggle;
        toggle.SetNamedValue(L"value", json::value(showMenu));
        json::JsonObject properties;
        properties.SetNamedValue(MenuProperty, toggle);
        json::JsonObject settings;
        settings.SetNamedValue(L"name", json::value(ModuleKey));
        settings.SetNamedValue(L"version", json::value(L"1"));
        settings.SetNamedValue(L"properties", properties);
        return settings;
    }
}

class TryRunModuleInterface final : public PowertoyModuleIface
{
public:
    TryRunModuleInterface() :
        m_root(get_module_folderpath(reinterpret_cast<HMODULE>(&__ImageBase)) + L"\\TryRun")
    {
        LoggerHelpers::init_logger(ModuleKey, L"ModuleInterface", "TryRun");
        try
        {
            m_showMenu = ReadMenuSetting(PTSettingsHelper::load_module_settings(ModuleKey));
        }
        catch (...)
        {
            Logger::info(L"Try Run uses its default context-menu setting because no valid saved configuration was found.");
        }

        m_canManageRegistration = CanManageRegistration();
        if (m_canManageRegistration)
        {
            // Reconcile stale registration even when this opt-in module starts
            // disabled. The helper removes only this build's owned registration.
            m_pendingRegistration = true;
            m_worker = std::thread([this] {
                try
                {
                    ProcessPendingOperations();
                }
                catch (...)
                {
                    m_canManageRegistration = false;
                    Logger::error(L"Try Run's integration worker stopped after an unexpected failure. Restart PowerToys before enabling Try Run again.");
                }
            });
        }
    }

    ~TryRunModuleInterface()
    {
        {
            std::lock_guard lock(m_mutex);
            m_stopping = true;
            m_launchPending = false;
        }

        m_changed.notify_one();
        if (m_worker.joinable())
        {
            m_worker.join();
        }
    }

    const wchar_t* get_name() override
    {
        return L"Try Run";
    }

    const wchar_t* get_key() override
    {
        return ModuleKey;
    }

    bool is_enabled_by_default() const override
    {
        return false;
    }

    powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredValue(powertoys_gpo::POLICY_CONFIGURE_ENABLED_GLOBAL_ALL_UTILITIES);
    }

    bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        if (!buffer_size)
        {
            return false;
        }

        const auto serialized = SettingsSnapshot(m_showMenu.load()).Stringify();
        const auto required = static_cast<int>(serialized.size() + 1);
        if (!buffer || *buffer_size < required)
        {
            *buffer_size = required;
            return false;
        }

        wcscpy_s(buffer, static_cast<size_t>(*buffer_size), serialized.c_str());
        *buffer_size = required;
        return true;
    }

    void set_config(const wchar_t* config) override
    {
        try
        {
            if (!config || wcsnlen_s(config, MaximumSettingsCharacters + 1) > MaximumSettingsCharacters)
            {
                throw winrt::hresult_invalid_argument(L"Try Run settings are missing or oversized.");
            }

            const bool showMenu = ReadMenuSetting(json::JsonObject::Parse(config));
            auto snapshot = SettingsSnapshot(showMenu);
            PTSettingsHelper::save_module_settings(ModuleKey, snapshot);
            m_showMenu = showMenu;
            QueueRegistration();
        }
        catch (...)
        {
            Logger::error(L"Try Run settings were not changed because the configuration is invalid or could not be saved.");
        }
    }

    void call_custom_action(const wchar_t* action) override
    {
        try
        {
            if (!action || wcsnlen_s(action, MaximumSettingsCharacters + 1) > MaximumSettingsCharacters)
            {
                throw winrt::hresult_invalid_argument(L"Try Run action is missing or oversized.");
            }

            auto requested = PowerToysSettings::CustomActionObject::from_json_string(action);
            if (requested.get_name() == L"Launch" && requested.get_value().empty())
            {
                std::lock_guard lock(m_mutex);
                if (m_enabled && m_canManageRegistration && !m_stopping)
                {
                    m_launchPending = true;
                    m_changed.notify_one();
                }
            }
        }
        catch (...)
        {
            Logger::error(L"Try Run ignored an invalid custom action.");
        }
    }

    void enable() override
    {
        // Runner persists is_enabled(). Preserve the user's requested state even
        // when this process cannot perform the non-elevated integration work.
        m_enabled = true;
        if (!m_canManageRegistration)
        {
            Logger::warn(L"Try Run's enabled preference is preserved, but integration actions are unavailable in this process. Restart PowerToys normally to use Try Run.");
            return;
        }

        QueueRegistration();
    }

    void disable() override
    {
        m_enabled = false;
        QueueRegistration();
    }

    bool is_enabled() override
    {
        return m_enabled.load();
    }

    void destroy() override
    {
        // Preserve the user's enabled-menu setting across ordinary Runner exits.
        // No Try Run windows or sandbox workloads are owned by this module DLL.
        delete this;
    }

private:
    void QueueRegistration()
    {
        std::lock_guard lock(m_mutex);
        if (m_canManageRegistration && !m_stopping)
        {
            m_pendingRegistration = true;
            m_changed.notify_one();
        }
    }

    void ProcessPendingOperations()
    {
        std::optional<bool> registered;
        for (;;)
        {
            bool desiredRegistration = false;
            bool launch = false;
            {
                std::unique_lock lock(m_mutex);
                m_changed.wait(lock, [this] { return m_stopping || m_pendingRegistration || m_launchPending; });
                if (m_stopping && !m_pendingRegistration)
                {
                    return;
                }

                desiredRegistration = m_enabled && m_showMenu;
                launch = m_launchPending && m_enabled && !m_stopping;
                m_pendingRegistration = false;
                m_launchPending = false;
            }

            if (!registered || *registered != desiredRegistration)
            {
                if (ChangeRegistration(desiredRegistration))
                {
                    registered = desiredRegistration;
                }
                else
                {
                    // Registration can fail after writing some keys (for example,
                    // during COM activation verification). Do not reuse an older
                    // cached state and skip the next requested cleanup.
                    registered.reset();
                }
            }

            if (launch)
            {
                bool stillEnabled = false;
                {
                    std::lock_guard lock(m_mutex);
                    stillEnabled = m_enabled && m_canManageRegistration && !m_stopping;
                }

                if (stillEnabled && !RunNonElevatedEx(m_root + L"\\PowerToys.TryRun.exe", L"", m_root))
                {
                    Logger::error(L"Try Run could not open its configuration window from the desktop shell.");
                }
            }
        }
    }

    bool ChangeRegistration(bool enable)
    {
        if (m_registrationBlocked)
        {
            Logger::error(L"Try Run Explorer registration is blocked after a helper could not be stopped. Restart PowerToys after inspecting the helper.");
            return false;
        }

        const std::wstring helper = m_root + L"\\Explorer\\PowerToys.TryRun.Explorer.exe";
        const std::wstring operation = enable ? L"--register" : L"--unregister";
        std::wstring command = L"\"" + helper + L"\" " + operation;
        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        startup.dwFlags = STARTF_USESHOWWINDOW;
        startup.wShowWindow = SW_HIDE;
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(helper.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, m_root.c_str(), &startup, &process))
        {
            Logger::error(L"Try Run Explorer {} could not start (Windows error {}). Check the adjacent Explorer helper.", operation, GetLastError());
            return false;
        }

        // Only this fixed registration process is ours to stop. Never enumerate
        // processes by executable name or terminate the user's Try Run workload.
        wil::unique_handle helperProcess(process.hProcess);
        wil::unique_handle helperThread(process.hThread);
        const DWORD waited = WaitForSingleObject(helperProcess.get(), RegistrationTimeoutMs);
        if (waited != WAIT_OBJECT_0)
        {
            Logger::error(L"Try Run Explorer {} did not complete within its bounded wait (wait status {}).", operation, waited);
            if (!TerminateProcess(helperProcess.get(), ERROR_TIMEOUT))
            {
                Logger::error(L"Try Run could not stop its registration helper (Windows error {}).", GetLastError());
            }
            m_registrationBlocked = WaitForSingleObject(helperProcess.get(), 5000) != WAIT_OBJECT_0;
            return false;
        }

        DWORD exitCode = 1;
        if (!GetExitCodeProcess(helperProcess.get(), &exitCode) || exitCode != 0)
        {
            Logger::error(L"Try Run Explorer {} failed (exit {}). The menu may not match the requested setting.", operation, exitCode);
            return false;
        }

        Logger::info(L"Try Run Explorer {} completed.", operation);
        return true;
    }

    const std::wstring m_root;
    std::atomic<bool> m_enabled{ false };
    std::atomic<bool> m_showMenu{ true };
    std::atomic<bool> m_canManageRegistration{ false };
    bool m_registrationBlocked = false;
    std::mutex m_mutex;
    std::condition_variable m_changed;
    bool m_stopping = false;
    bool m_pendingRegistration = false;
    bool m_launchPending = false;
    std::thread m_worker;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    try
    {
        return new TryRunModuleInterface();
    }
    catch (...)
    {
        return nullptr;
    }
}
