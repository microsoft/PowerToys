#include "pch.h"
#include "system_menu_helper.h"

#include <interface/powertoy_module_interface.h>

namespace
{
    constexpr int KSeparatorPos = 1;
    constexpr int KNewItemPos = 2;

    unsigned int GenerateItemId()
    {
        static unsigned int generator = 0x70777479;
        return ++generator;
    }
}

SystemMenuHelper& SystemMenuHelperInstace()
{
    static SystemMenuHelper instance;
    return instance;
}

void SystemMenuHelper::SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config)
{
    Reset(module);
    Configurations[module] = config;
    for (auto& [window, modules] : ProcessedModules)
    {
        // Unregister module. After system menu is opened again, new configuration will be applied.
        modules.erase(std::remove(std::begin(modules), std::end(modules), module), std::end(modules));
    }
}

void SystemMenuHelper::ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName)
{
    for (const auto& item : Configurations[module])
    {
        if (itemName == item.name && item.checkBox)
        {
            // Handle check/uncheck action only if specified by module configuration.
            for (const auto& [id, data] : IdMappings)
            {
                if (data.second == itemName)
                {
                    HMENU systemMenu = GetSystemMenu(window, false);
                    int state = (GetMenuState(systemMenu, id, MF_BYCOMMAND) == MF_CHECKED) ? MF_UNCHECKED : MF_CHECKED;
                    CheckMenuItem(systemMenu, id, MF_BYCOMMAND | state);
                    break;
                }
            }
            break;
        }
    }
}

bool SystemMenuHelper::Customize(PowertoyModuleIface* module, HWND window)
{
    auto& modules = ProcessedModules[window];
    for (const auto& m : modules)
    {
        if (module == m)
        {
            return false;
        }
    }
    AddSeparator(module, window);
    for (const auto& info : Configurations[module])
    {
        AddItem(module, window, info.name, info.enable);
    }
    modules.push_back(module);
    return true;
}

void SystemMenuHelper::Reset(PowertoyModuleIface* module)
{
    for (auto& [window, modules] : ProcessedModules)
    {
        if (HMENU systemMenu{ GetSystemMenu(window, false) })
        {
            for (auto& [id, data] : IdMappings)
            {
                if (data.first == module)
                {
                    DeleteMenu(systemMenu, id, MF_BYCOMMAND);
                }
            }
        }
    }
}

bool SystemMenuHelper::HasCustomConfig(PowertoyModuleIface* module)
{
    return Configurations.find(module) != Configurations.end();
}

bool SystemMenuHelper::AddItem(PowertoyModuleIface* module, HWND window, const std::wstring& name, const bool enable)
{
    if (HMENU systemMenu{ GetSystemMenu(window, false) })
    {
        MENUITEMINFO item;
        item.cbSize = sizeof(item);
        item.fMask = MIIM_ID | MIIM_STRING | MIIM_STATE;
        item.fState = MF_UNCHECKED | MF_DISABLED; // Item is disabled by default.
        item.wID = GenerateItemId();
        item.dwTypeData = const_cast<WCHAR*>(name.c_str());
        item.cch = (UINT)name.size() + 1;

        if (InsertMenuItem(systemMenu, GetMenuItemCount(systemMenu) - KNewItemPos, true, &item))
        {
            IdMappings[item.wID] = { module, name };
            if (enable)
            {
                EnableMenuItem(systemMenu, item.wID, MF_BYCOMMAND | MF_ENABLED);
            }
            return true;
        }
    }
    return false;
}

bool SystemMenuHelper::AddSeparator(PowertoyModuleIface* module, HWND window)
{
    if (HMENU systemMenu{ GetSystemMenu(window, false) })
    {
        MENUITEMINFO separator;
        separator.cbSize = sizeof(separator);
        separator.fMask = MIIM_ID | MIIM_FTYPE;
        separator.fType = MFT_SEPARATOR;
        separator.wID = GenerateItemId();

        if (InsertMenuItem(systemMenu, GetMenuItemCount(systemMenu) - KSeparatorPos, true, &separator))
        {
            IdMappings[separator.wID] = { module, L"sepparator_dummy_name" };
            return true;
        }
    }
    return false;
}

PowertoyModuleIface* SystemMenuHelper::ModuleFromItemId(const int& id)
{
    auto it = IdMappings.find(id);
    if (it != IdMappings.end())
    {
        return it->second.first;
    }
    return nullptr;
}

const std::wstring SystemMenuHelper::ItemNameFromItemId(const int& id)
{
    auto itemIt = IdMappings.find(id);
    if (itemIt != IdMappings.end())
    {
        return itemIt->second.second;
    }
    return std::wstring{};
}
