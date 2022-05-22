#include "pch.h"
#include "VirtualDesktop.h"

#include <common/logger/logger.h>
#include "trace.h"

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RegCurrentVirtualDesktop[] = L"CurrentVirtualDesktop";
    const wchar_t RegVirtualDesktopIds[] = L"VirtualDesktopIDs";
    const wchar_t RegKeyVirtualDesktops[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";
    const wchar_t RegKeyVirtualDesktopsFromSession[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops";
}

std::optional<GUID> NewGetCurrentDesktopId()
{
    wil::unique_hkey key{};
    if (RegOpenKeyExW(HKEY_CURRENT_USER, NonLocalizable::RegKeyVirtualDesktops, 0, KEY_ALL_ACCESS, &key) == ERROR_SUCCESS)
    {
        GUID value{};
        DWORD size = sizeof(GUID);
        if (RegQueryValueExW(key.get(), NonLocalizable::RegCurrentVirtualDesktop, 0, nullptr, reinterpret_cast<BYTE*>(&value), &size) == ERROR_SUCCESS)
        {
            return value;
        }
    }

    return std::nullopt;
}

std::optional<GUID> GetDesktopIdFromCurrentSession()
{
    DWORD sessionId;
    if (!ProcessIdToSessionId(GetCurrentProcessId(), &sessionId))
    {
        return std::nullopt;
    }

    wchar_t sessionKeyPath[256]{};
    if (FAILED(StringCchPrintfW(sessionKeyPath, ARRAYSIZE(sessionKeyPath), NonLocalizable::RegKeyVirtualDesktopsFromSession, sessionId)))
    {
        return std::nullopt;
    }

    wil::unique_hkey key{};
    if (RegOpenKeyExW(HKEY_CURRENT_USER, sessionKeyPath, 0, KEY_ALL_ACCESS, &key) == ERROR_SUCCESS)
    {
        GUID value{};
        DWORD size = sizeof(GUID);
        if (RegQueryValueExW(key.get(), NonLocalizable::RegCurrentVirtualDesktop, 0, nullptr, reinterpret_cast<BYTE*>(&value), &size) == ERROR_SUCCESS)
        {
            return value;
        }
    }

    return std::nullopt;
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

VirtualDesktop::VirtualDesktop()
{
    auto res = CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&m_vdManager));
    if (FAILED(res))
    {
        Logger::error("Failed to create VirtualDesktopManager instance");
    }
}

VirtualDesktop::~VirtualDesktop()
{
    if (m_vdManager)
    {
        m_vdManager->Release();
    }
}

VirtualDesktop& VirtualDesktop::instance()
{
    static VirtualDesktop self;
    return self;
}

std::optional<GUID> VirtualDesktop::GetCurrentVirtualDesktopIdFromRegistry() const
{
    // On newer Windows builds, the current virtual desktop is persisted to
    // a totally different reg key. Look there first.
    std::optional<GUID> desktopId = NewGetCurrentDesktopId();
    if (desktopId.has_value())
    {
        return desktopId;
    }

    // Explorer persists current virtual desktop identifier to registry on a per session basis, but only
    // after first virtual desktop switch happens. If the user hasn't switched virtual desktops in this
    // session, value in registry will be empty.
    desktopId = GetDesktopIdFromCurrentSession();
    if (desktopId.has_value())
    {
        return desktopId;
    }

    // Fallback scenario is to get array of virtual desktops stored in registry, but not kept per session.
    // Note that we are taking first element from virtual desktop array, which is primary desktop.
    // If user has more than one virtual desktop, previous function should return correct value, as desktop
    // switch occurred in current session.
    else
    {
        auto ids = GetVirtualDesktopIdsFromRegistry();
        if (ids.has_value() && ids->size() > 0)
        {
            return ids->at(0);
        }
    }

    return std::nullopt;
}

std::optional<std::vector<GUID>> VirtualDesktop::GetVirtualDesktopIdsFromRegistry(HKEY hKey) const
{
    if (!hKey)
    {
        return std::nullopt;
    }

    DWORD bufferCapacity;
    // request regkey binary buffer capacity only
    if (RegQueryValueExW(hKey, NonLocalizable::RegVirtualDesktopIds, 0, nullptr, nullptr, &bufferCapacity) != ERROR_SUCCESS)
    {
        return std::nullopt;
    }

    std::unique_ptr<BYTE[]> buffer = std::make_unique<BYTE[]>(bufferCapacity);
    // request regkey binary content
    if (RegQueryValueExW(hKey, NonLocalizable::RegVirtualDesktopIds, 0, nullptr, buffer.get(), &bufferCapacity) != ERROR_SUCCESS)
    {
        return std::nullopt;
    }

    const size_t guidSize = sizeof(GUID);
    std::vector<GUID> temp;
    temp.reserve(bufferCapacity / guidSize);
    for (size_t i = 0; i < bufferCapacity; i += guidSize)
    {
        GUID* guid = reinterpret_cast<GUID*>(buffer.get() + i);
        temp.push_back(*guid);
    }

    return temp;
}

std::optional<std::vector<GUID>> VirtualDesktop::GetVirtualDesktopIdsFromRegistry() const
{
    return GetVirtualDesktopIdsFromRegistry(GetVirtualDesktopsRegKey());
}

bool VirtualDesktop::IsVirtualDesktopIdSavedInRegistry(GUID id) const
{
    auto ids = GetVirtualDesktopIdsFromRegistry();
    if (!ids.has_value())
    {
        return false;
    }

    for (const auto& regId : *ids)
    {
        if (regId == id)
        {
            return true;
        }
    }

    return false;
}

bool VirtualDesktop::IsWindowOnCurrentDesktop(HWND window) const
{
    std::optional<GUID> id = GetDesktopId(window);
    return id.has_value();
}

std::optional<GUID> VirtualDesktop::GetDesktopId(HWND window) const
{
    GUID id;
    BOOL isWindowOnCurrentDesktop = false;
    if (m_vdManager && m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop) == S_OK && isWindowOnCurrentDesktop)
    {
        // Filter windows such as Windows Start Menu, Task Switcher, etc.
        if (m_vdManager->GetWindowDesktopId(window, &id) == S_OK)
        {
            return id;
        }
    }

    return std::nullopt;
}

std::vector<std::pair<HWND, GUID>> VirtualDesktop::GetWindowsRelatedToDesktops() const
{
    using result_t = std::vector<HWND>;
    result_t windows;

    auto callback = [](HWND window, LPARAM data) -> BOOL {
        result_t& result = *reinterpret_cast<result_t*>(data);
        result.push_back(window);
        return TRUE;
    };
    EnumWindows(callback, reinterpret_cast<LPARAM>(&windows));

    std::vector<std::pair<HWND, GUID>> result;
    for (auto window : windows)
    {
        auto desktop = GetDesktopId(window);
        if (desktop.has_value())
        {
            result.push_back({ window, *desktop });
        }
    }

    return result;
}

GUID VirtualDesktop::GetCurrentVirtualDesktopId() const noexcept
{
    return m_currentVirtualDesktopId;
}

GUID VirtualDesktop::GetPreviousVirtualDesktopId() const noexcept
{
    return m_previousDesktopId;
}

void VirtualDesktop::UpdateVirtualDesktopId() noexcept
{
    m_previousDesktopId = m_currentVirtualDesktopId;

    auto currentVirtualDesktopId = GetCurrentVirtualDesktopIdFromRegistry();
    if (!currentVirtualDesktopId.has_value())
    {
        Logger::info("No Virtual Desktop Id found in registry");
        currentVirtualDesktopId = VirtualDesktop::instance().GetDesktopIdByTopLevelWindows();
    }

    if (currentVirtualDesktopId.has_value())
    {
        m_currentVirtualDesktopId = *currentVirtualDesktopId;

        if (m_currentVirtualDesktopId == GUID_NULL)
        {
            Logger::warn("Couldn't retrieve virtual desktop id");
        }
    }

    Trace::VirtualDesktopChanged();
}

std::optional<GUID> VirtualDesktop::GetDesktopIdByTopLevelWindows() const
{
    using result_t = std::vector<HWND>;
    result_t windows;

    auto callback = [](HWND window, LPARAM data) -> BOOL {
        result_t& result = *reinterpret_cast<result_t*>(data);
        result.push_back(window);
        return TRUE;
    };
    EnumWindows(callback, reinterpret_cast<LPARAM>(&windows));

    for (const auto window : windows)
    {
        std::optional<GUID> id = GetDesktopId(window);
        if (id.has_value())
        {
            // Otherwise keep checking other windows
            return *id;
        }
    }

    return std::nullopt;
}
