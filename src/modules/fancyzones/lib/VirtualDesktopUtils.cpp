#include "pch.h"

#include "VirtualDesktopUtils.h"

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RegCurrentVirtualDesktop[] = L"CurrentVirtualDesktop";
    const wchar_t RegVirtualDesktopIds[] = L"VirtualDesktopIDs";
    const wchar_t RegKeyVirtualDesktops[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";
    const wchar_t RegKeyVirtualDesktopsFromSession[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops";
}

namespace VirtualDesktopUtils
{
    const CLSID CLSID_ImmersiveShell = { 0xC2F03A33, 0x21F5, 0x47FA, 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39 };

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
        return SUCCEEDED(CLSIDFromString(virtualDesktopId.c_str(), desktopId));
    }

    bool GetDesktopIdFromCurrentSession(GUID* desktopId)
    {
        DWORD sessionId;
        if (!ProcessIdToSessionId(GetCurrentProcessId(), &sessionId))
        {
            return false;
        }

        wchar_t sessionKeyPath[256]{};
        if (FAILED(StringCchPrintfW(sessionKeyPath, ARRAYSIZE(sessionKeyPath), NonLocalizable::RegKeyVirtualDesktopsFromSession, sessionId)))
        {
            return false;
        }

        wil::unique_hkey key{};
        if (RegOpenKeyExW(HKEY_CURRENT_USER, sessionKeyPath, 0, KEY_ALL_ACCESS, &key) == ERROR_SUCCESS)
        {
            GUID value{};
            DWORD size = sizeof(GUID);
            if (RegQueryValueExW(key.get(), NonLocalizable::RegCurrentVirtualDesktop, 0, nullptr, reinterpret_cast<BYTE*>(&value), &size) == ERROR_SUCCESS)
            {
                *desktopId = value;
                return true;
            }
        }
        return false;
    }

    bool GetCurrentVirtualDesktopId(GUID* desktopId)
    {
        // Explorer persists current virtual desktop identifier to registry on a per session basis, but only
        // after first virtual desktop switch happens. If the user hasn't switched virtual desktops in this
        // session, value in registry will be empty.
        if (GetDesktopIdFromCurrentSession(desktopId))
        {
            return true;
        }
        // Fallback scenario is to get array of virtual desktops stored in registry, but not kept per session.
        // Note that we are taking first element from virtual desktop array, which is primary desktop.
        // If user has more than one virtual desktop, previous function should return correct value, as desktop
        // switch occurred in current session.
        else
        {
            std::vector<GUID> ids{};
            if (GetVirtualDesktopIds(ids) && ids.size() > 0)
            {
                *desktopId = ids[0];
                return true;
            }
        }
        return false;
    }

    bool GetVirtualDesktopIds(HKEY hKey, std::vector<GUID>& ids)
    {
        if (!hKey)
        {
            return false;
        }
        DWORD bufferCapacity;
        // request regkey binary buffer capacity only
        if (RegQueryValueExW(hKey, NonLocalizable::RegVirtualDesktopIds, 0, nullptr, nullptr, &bufferCapacity) != ERROR_SUCCESS)
        {
            return false;
        }
        std::unique_ptr<BYTE[]> buffer = std::make_unique<BYTE[]>(bufferCapacity);
        // request regkey binary content
        if (RegQueryValueExW(hKey, NonLocalizable::RegVirtualDesktopIds, 0, nullptr, buffer.get(), &bufferCapacity) != ERROR_SUCCESS)
        {
            return false;
        }
        const size_t guidSize = sizeof(GUID);
        std::vector<GUID> temp;
        temp.reserve(bufferCapacity / guidSize);
        for (size_t i = 0; i < bufferCapacity; i += guidSize)
        {
            GUID* guid = reinterpret_cast<GUID*>(buffer.get() + i);
            temp.push_back(*guid);
        }
        ids = std::move(temp);
        return true;
    }

    bool GetVirtualDesktopIds(std::vector<GUID>& ids)
    {
        return GetVirtualDesktopIds(GetVirtualDesktopsRegKey(), ids);
    }

    bool GetVirtualDesktopIds(std::vector<std::wstring>& ids)
    {
        std::vector<GUID> guids{};
        if (GetVirtualDesktopIds(guids))
        {
            for (auto& guid : guids)
            {
                wil::unique_cotaskmem_string guidString;
                if (SUCCEEDED(StringFromCLSID(guid, &guidString)))
                {
                    ids.push_back(guidString.get());
                }
            }
            return true;
        }
        return false;
    }

    HKEY OpenVirtualDesktopsRegKey()
    {
        HKEY hKey{ nullptr };
        if (RegOpenKeyEx(HKEY_CURRENT_USER, NonLocalizable::RegKeyVirtualDesktops, 0, KEY_ALL_ACCESS, &hKey) == ERROR_SUCCESS)
        {
            return hKey;
        }
        return nullptr;
    }

    HKEY GetVirtualDesktopsRegKey()
    {
        static wil::unique_hkey virtualDesktopsKey{ OpenVirtualDesktopsRegKey() };
        return virtualDesktopsKey.get();
    }

    void HandleVirtualDesktopUpdates(HWND window, UINT message, HANDLE terminateEvent)
    {
        HKEY virtualDesktopsRegKey = GetVirtualDesktopsRegKey();
        if (!virtualDesktopsRegKey)
        {
            return;
        }
        HANDLE regKeyEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
        HANDLE events[2] = { regKeyEvent, terminateEvent };
        while (1)
        {
            if (RegNotifyChangeKeyValue(virtualDesktopsRegKey, TRUE, REG_NOTIFY_CHANGE_LAST_SET, regKeyEvent, TRUE) != ERROR_SUCCESS)
            {
                return;
            }
            if (WaitForMultipleObjects(2, events, FALSE, INFINITE) != (WAIT_OBJECT_0 + 0))
            {
                // if terminateEvent is signalized or WaitForMultipleObjects failed, terminate thread execution
                return;
            }
            PostMessage(window, message, 0, 0);
        }
    }
}
