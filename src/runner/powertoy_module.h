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

// The catalog of every module Runner can load. Defined in main.cpp.
std::span<const ModuleRegistration> known_modules();
const ModuleRegistration* find_known_module(std::wstring_view key);

// Loaded modules. Only the main thread modifies the map (insert on load, clear at shutdown), and it
// holds modules_mutex() while doing so; other threads must hold it to read. The main thread may read
// without it. Never call a function that takes the lock while holding it. Entries are not removed
// before shutdown, so returned pointers stay valid.
std::mutex& modules_mutex();
std::map<std::wstring, PowertoyModule>& modules();

// Loading stays off until Runner has made its elevation decision.
void set_module_loading_allowed(bool allowed);
PowertoyModule* find_loaded_module(const std::wstring& key);
// Main thread only. Loads (but does not enable) a known module if needed.
PowertoyModule* load_known_module(const std::wstring& key);
// Main thread only. Applies GPO to `enabled`; loads the module only if it ends up enabled.
PowertoyModule* get_module_for_enabled_state(const std::wstring& key, bool& enabled);
