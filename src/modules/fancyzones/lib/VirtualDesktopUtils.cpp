#include "pch.h"

#include "VirtualDesktopUtils.h"

namespace VirtualDesktopUtils
{
    const CLSID CLSID_ImmersiveShell = { 0xC2F03A33, 0x21F5, 0x47FA, 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39 };
    const wchar_t GUID_EmptyGUID[] = L"{00000000-0000-0000-0000-000000000000}";

    IServiceProvider* GetServiceProvider()
    {
        IServiceProvider* provider{ nullptr };
        if (FAILED(CoCreateInstance(CLSID_ImmersiveShell, nullptr, CLSCTX_LOCAL_SERVER, __uuidof(provider), (PVOID*)&provider)))
        {
            return nullptr;
        }
        return provider;
    }

    IVirtualDesktopManager* GetVirtualDesktopManager()
    {
        IVirtualDesktopManager* manager{ nullptr };
        IServiceProvider* serviceProvider = GetServiceProvider();
        if (serviceProvider == nullptr || FAILED(serviceProvider->QueryService(__uuidof(manager), &manager)))
        {
            return nullptr;
        }
        return manager;
    }

    bool GetWindowDesktopId(HWND topLevelWindow, GUID* desktopId)
    {
        static IVirtualDesktopManager* virtualDesktopManager = GetVirtualDesktopManager();
        return (virtualDesktopManager != nullptr) &&
               SUCCEEDED(virtualDesktopManager->GetWindowDesktopId(topLevelWindow, desktopId));
    }

    bool GetZoneWindowDesktopId(IZoneWindow* zoneWindow, GUID* desktopId)
    {
        // Format: <device-id>_<resolution>_<virtual-desktop-id>
        std::wstring uniqueId = zoneWindow->UniqueId();
        std::wstring virtualDesktopId = uniqueId.substr(uniqueId.rfind('_') + 1);
        if (virtualDesktopId == GUID_EmptyGUID)
        {
            return false;
        }
        return SUCCEEDED(CLSIDFromString(virtualDesktopId.c_str(), desktopId));
    }
}
