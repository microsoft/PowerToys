// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "LightSwitchSettings.h"
#include <ThemeHelper.h>
#include <modules/PowerScripts/PowerScripts.Client.Native/HostSession.h>
#include <logger.h>
#include <condition_variable>
#include <functional>
#include <stop_token>
#include <thread>

inline std::vector<std::wstring> ThemeScriptArguments(const LightSwitchScriptBinding& binding)
{
    if (binding.id.empty() || binding.id.starts_with(L"--") ||
        binding.id.find_first_not_of(L"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.") != std::wstring::npos)
        return {};
    std::vector<std::wstring> arguments{ L"run", binding.id, L"--consumer", L"light-switch", L"--no-consent" };
    if (!binding.parameters.empty())
    {
        arguments.emplace_back(L"--set");
        for (const auto& [name, value] : binding.parameters)
        {
            if (name.empty() || name.starts_with(L"--") || name.find_first_of(L"=\0", 0, 2) != std::wstring::npos ||
                value.find(L'\0') != std::wstring::npos)
                return {};
            // Each assignment is a framed JSON argument, never a shell command.
            arguments.emplace_back(name + L"=" + value);
        }
    }
    return arguments;
}

class PowerScriptThemeRunner
{
public:
    using Invocation = std::function<void(bool, const LightSwitchScriptBinding&, std::stop_token)>;

    explicit PowerScriptThemeRunner(Invocation invoke = InvokeHost) :
        _invoke(std::move(invoke))
    {
    }

    ~PowerScriptThemeRunner()
    {
        if (_worker.joinable())
        {
            _worker.request_stop();
            _changed.notify_all();
            _worker.join();
        }
    }

    void Queue(bool light, const LightSwitchScriptBinding& binding)
    {
        std::lock_guard lock(_gate);
        // Only the latest pending theme matters. An unbound theme clears stale work.
        _pending = binding.id.empty() ? std::nullopt : std::optional(Request{ light, binding });
        if (_pending && !_worker.joinable())
            _worker = std::jthread([this](std::stop_token stop) { Run(stop); });
        _changed.notify_one();
    }

private:
    struct Request
    {
        bool light;
        LightSwitchScriptBinding binding;
    };

    Invocation _invoke;
    std::mutex _gate;
    std::condition_variable_any _changed;
    std::optional<Request> _pending;
    std::jthread _worker;

    void Run(std::stop_token stop)
    {
        while (!stop.stop_requested())
        {
            std::unique_lock lock(_gate);
            if (!_changed.wait(lock, stop, [this] { return _pending.has_value(); }))
                return;
            auto request = std::move(*_pending);
            _pending.reset();
            lock.unlock();
            try
            {
                if (!stop.stop_requested())
                    _invoke(request.light, request.binding, stop);
            }
            catch (...)
            {
                if (!stop.stop_requested())
                    Logger::warn(L"[LightSwitch] PowerScripts theme action failed. Check PowerScripts Activity and approval.");
            }
        }
    }

    static void InvokeHost(bool light, const LightSwitchScriptBinding& binding, std::stop_token stop)
    {
        const auto apartment = RoInitialize(RO_INIT_MULTITHREADED);
        THROW_IF_FAILED(apartment);
        const auto cleanup = wil::scope_exit([&] {
            winrt::clear_factory_cache();
            RoUninitialize();
        });
        LightSwitchConfig current;
        std::wstring error;
        if (!LightSwitchSettings::instance().TryLoadSettings(current, error) ||
            (light ? current.lightThemeScript : current.darkThemeScript) != binding)
            return;
        bool actual = false;
        if ((!current.changeSystem && !current.changeApps) ||
            (current.changeSystem && (TryGetSystemTheme(actual) != ERROR_SUCCESS || actual != light)) ||
            (current.changeApps && (TryGetAppsTheme(actual) != ERROR_SUCCESS || actual != light)))
            return;
        auto arguments = ThemeScriptArguments(binding);
        if (arguments.empty() || stop.stop_requested())
            return;
        wchar_t executable[32768]{};
        const auto length = GetModuleFileNameW(nullptr, executable, static_cast<DWORD>(std::size(executable)));
        THROW_LAST_ERROR_IF(length == 0 || length == std::size(executable));
        const auto directory = std::filesystem::path(executable).parent_path().parent_path() / L"PowerScripts";
        PowerScripts::HostSession session((directory / L"PowerScripts.Host.exe").wstring(), directory.wstring());
        const auto result = session.Invoke(arguments, stop);
        if (result.ExitCode != 0 && !stop.stop_requested())
            Logger::warn(L"[LightSwitch] PowerScripts theme action failed (exit code {}). Check PowerScripts Activity and approval.", result.ExitCode);
    }
};
