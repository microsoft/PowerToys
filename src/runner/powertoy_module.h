#pragma once
#include <interface/powertoy_module_interface.h>
#include <string>
#include <memory>
#include <mutex>
#include <vector>
#include <functional>
#include <span>
#include "hotkey_conflict_detector.h"
#include "module_registration.h"

#include <common/utils/json.h>

struct PowertoyModuleDeleter
{
    void operator()(PowertoyModuleIface* pt_module) const
    {
        if (pt_module)
        {
            pt_module->destroy();
        }
    }
};

struct PowertoyModuleDLLDeleter
{
    using pointer = HMODULE;
    void operator()(HMODULE handle) const
    {
        FreeLibrary(handle);
    }
};

class PowertoyModule
{
public:
    PowertoyModule(PowertoyModuleIface* pt_module, HMODULE handle);

    inline PowertoyModuleIface* operator->()
    {
        return pt_module.get();
    }

    json::JsonObject json_config() const;

    void update_hotkeys();

    void UpdateHotkeyEx();

    inline void remove_hotkey_records()
    {
        hkmng.RemoveHotkeyByModule(pt_module->get_key());
    }

private:
    HotkeyConflictDetector::HotkeyConflictManager& hkmng;
    std::unique_ptr<HMODULE, PowertoyModuleDLLDeleter> handle;
    std::unique_ptr<PowertoyModuleIface, PowertoyModuleDeleter> pt_module;

    
};

PowertoyModule load_powertoy(const std::wstring_view filename);
// Hold this lock while accessing either registry, including calls through its entries.
std::recursive_mutex& modules_mutex();
std::map<std::wstring, PowertoyModule>& modules();
const std::map<std::wstring, ModuleRegistration>& registered_modules();
void register_modules(std::span<const ModuleRegistration> registrations);
PowertoyModule* load_registered_module(const std::wstring& key);
PowertoyModule* get_module_for_enabled_state(const std::wstring& key, bool& enabled);
