#pragma once
#include <interface/powertoy_module_interface.h>
#include <string>
#include <memory>
#include <mutex>
#include <vector>
#include <functional>
#include "hotkey_conflict_detector.h"

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
std::map<std::wstring, PowertoyModule>& modules();
