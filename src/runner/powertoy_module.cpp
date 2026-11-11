#include "pch.h"
#include "powertoy_module.h"
#include "centralized_kb_hook.h"
#include "centralized_hotkeys.h"
#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

namespace
{
    std::map<std::wstring, ModuleRegistration>& registrations()
    {
        static std::map<std::wstring, ModuleRegistration> value;
        return value;
    }
}

std::recursive_mutex& modules_mutex()
{
    static std::recursive_mutex mutex;
    return mutex;
}

std::map<std::wstring, PowertoyModule>& modules()
{
    static std::map<std::wstring, PowertoyModule> modules;
    return modules;
}

const std::map<std::wstring, ModuleRegistration>& registered_modules()
{
    return registrations();
}

void register_modules(std::span<const ModuleRegistration> knownModules)
{
    std::lock_guard lock(modules_mutex());
    registrations().clear();
    for (const auto& registration : knownModules)
    {
        registrations().emplace(registration.key, registration);
    }
}

PowertoyModule* load_registered_module(const std::wstring& key)
{
    std::lock_guard lock(modules_mutex());
    if (const auto loaded = modules().find(key); loaded != modules().end())
    {
        return &loaded->second;
    }
    const auto registration = registered_modules().find(key);
    if (registration == registered_modules().end())
    {
        // The catalog is deliberately empty during pre-elevation settings initialization.
        return nullptr;
    }

    const auto path = registration->second.dllPath;
    try
    {
        auto module = load_powertoy(path);
        const auto loaded = modules().emplace(key, std::move(module));
        return &loaded.first->second;
    }
    catch (const winrt::hresult_error& error)
    {
        Logger::error(L"Failed to load {}: {}", path, error.message());
    }
    catch (const std::exception& error)
    {
        Logger::error("Failed to load {}: {}", winrt::to_string(path), error.what());
    }

#ifndef _DEBUG
    const std::wstring message = L"Failed to load " + std::wstring(path);
    MessageBoxW(nullptr, message.c_str(), L"PowerToys", MB_OK | MB_ICONERROR);
#endif
    return nullptr;
}

PowertoyModule* get_module_for_enabled_state(const std::wstring& key, bool& enabled)
{
    std::lock_guard lock(modules_mutex());
    const auto registration = registered_modules().find(key);
    if (registration == registered_modules().end())
    {
        return nullptr;
    }
    enabled = registration->second.apply_policy(enabled);
    if (const auto loaded = modules().find(key); loaded != modules().end())
    {
        return &loaded->second;
    }
    return enabled ? load_registered_module(key) : nullptr;
}

PowertoyModule load_powertoy(const std::wstring_view filename)
{
    auto handle = winrt::check_pointer(LoadLibraryW(filename.data()));
    auto create = reinterpret_cast<powertoy_create_func>(GetProcAddress(handle, "powertoy_create"));
    if (!create)
    {
        FreeLibrary(handle);
        winrt::throw_last_error();
    }
    auto pt_module = create();
    if (!pt_module)
    {
        FreeLibrary(handle);
        winrt::throw_hresult(winrt::hresult(E_POINTER));
    }
    return PowertoyModule(pt_module, handle);
}

json::JsonObject PowertoyModule::json_config() const
{
    int size = 0;
    pt_module->get_config(nullptr, &size);
    std::wstring result;
    result.resize(static_cast<size_t>(size) - 1);
    pt_module->get_config(result.data(), &size);
    return json::JsonObject::Parse(result);
}

PowertoyModule::PowertoyModule(PowertoyModuleIface* pt_module, HMODULE handle) :
    handle(handle), pt_module(pt_module), hkmng(HotkeyConflictDetector::HotkeyConflictManager::GetInstance())
{
    if (!pt_module)
    {
        throw std::runtime_error("Module not initialized");
    }

    remove_hotkey_records();
    update_hotkeys();
    UpdateHotkeyEx();
}

void PowertoyModule::update_hotkeys()
{
    CentralizedKeyboardHook::ClearModuleHotkeys(pt_module->get_key());

    size_t hotkeyCount = pt_module->get_hotkeys(nullptr, 0);
    std::vector<PowertoyModuleIface::Hotkey> hotkeys(hotkeyCount);
    pt_module->get_hotkeys(hotkeys.data(), hotkeyCount);

    auto modulePtr = pt_module.get();

    for (size_t i = 0; i < hotkeyCount; i++)
    {
        if (hotkeys[i].isShown)
        {
            hkmng.AddHotkey(hotkeys[i], pt_module->get_key(), static_cast<int>(i), pt_module->is_enabled());

            CentralizedKeyboardHook::SetHotkeyAction(pt_module->get_key(), hotkeys[i], [modulePtr, i] {
                Logger::trace(L"{} hotkey is invoked from Centralized keyboard hook", modulePtr->get_key());
                return modulePtr->on_hotkey(i);
            });
        }
    }
}

void PowertoyModule::UpdateHotkeyEx()
{
    CentralizedHotkeys::UnregisterHotkeysForModule(pt_module->get_key());
    CentralizedKeyboardHook::ClearPressedKeyActions(pt_module->get_key());

    auto container = pt_module->GetHotkeyEx();
    if (container.has_value() && pt_module->is_enabled())
    {
        hkmng.RemoveHotkeyByModule(pt_module->get_key());

        auto hotkey = container.value();
        auto modulePtr = pt_module.get();
        auto action = [modulePtr](WORD /*modifiersMask*/, WORD /*vkCode*/) {
            Logger::trace(L"{} hotkey Ex is invoked from Centralized keyboard hook", modulePtr->get_key());
            modulePtr->OnHotkeyEx();
        };

        HotkeyConflictDetector::Hotkey _hotkey = HotkeyConflictDetector::ShortcutToHotkey({ hotkey.modifiersMask, hotkey.vkCode });
        hkmng.AddHotkey(_hotkey, pt_module->get_key(), 0, pt_module->is_enabled()); // This is the only one activation hotkey, so we use "0" as the name.

        CentralizedHotkeys::AddHotkeyAction({ hotkey.modifiersMask, hotkey.vkCode }, { pt_module->get_key(), action });
    }

    // HACK:
    // Just for enabling the shortcut guide legacy behavior of pressing the Windows Key.
    // This is not the sort of behavior we'd like to have generalized on other modules.
    // But this was a way to bring back the long windows key behavior that the community wanted back while maintaining the separate process.
    if (pt_module->is_enabled() && pt_module->keep_track_of_pressed_win_key())
    {
        auto modulePtr = pt_module.get();
        auto action = [modulePtr] {
            if (modulePtr->is_enabled())
            {
                return modulePtr->on_hotkey(PowertoyModuleIface::WIN_KEY_HOLD_HOTKEY_ID);
            }

            return false;
        };
        CentralizedKeyboardHook::AddPressedKeyAction(pt_module->get_key(), VK_LWIN, pt_module->milliseconds_win_key_must_be_pressed(), action);
        CentralizedKeyboardHook::AddPressedKeyAction(pt_module->get_key(), VK_RWIN, pt_module->milliseconds_win_key_must_be_pressed(), action);
    }
}
