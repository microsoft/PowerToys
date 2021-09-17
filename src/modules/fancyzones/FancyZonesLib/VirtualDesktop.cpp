#include "pch.h"
#include "VirtualDesktop.h"

#include <common/logger/logger.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RegCurrentVirtualDesktop[] = L"CurrentVirtualDesktop";
    const wchar_t RegVirtualDesktopIds[] = L"VirtualDesktopIDs";
    const wchar_t RegKeyVirtualDesktops[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";
    const wchar_t RegKeyVirtualDesktopsFromSession[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops";
}

const CLSID CLSID_ImmersiveShell = { 0xC2F03A33, 0x21F5, 0x47FA, 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39 };

IServiceProvider* GetServiceProvider()
{
    IServiceProvider* provider{ nullptr };
    if (FAILED(CoCreateInstance(CLSID_ImmersiveShell, nullptr, CLSCTX_LOCAL_SERVER, __uuidof(provider), (PVOID*)&provider)))
    {
        Logger::error("Failed to get ServiceProvider for VirtualDesktopManager");
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
        Logger::error("Failed to get VirtualDesktopManager");
        return nullptr;
    }
    return manager;
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

VirtualDesktop::VirtualDesktop(const std::function<void()>& vdInitCallback, const std::function<void()>& vdUpdatedCallback) :
    m_vdInitCallback(vdInitCallback),
    m_vdUpdatedCallback(vdUpdatedCallback),
    m_vdManager(GetVirtualDesktopManager())
{
}

void VirtualDesktop::Init()
{
    m_vdInitCallback();

    m_terminateVirtualDesktopTrackerEvent.reset(CreateEvent(nullptr, FALSE, FALSE, nullptr));
    m_virtualDesktopTrackerThread.submit(OnThreadExecutor::task_t{ [&] { HandleVirtualDesktopUpdates(); } });
}

void VirtualDesktop::UnInit()
{
    if (m_terminateVirtualDesktopTrackerEvent)
    {
        SetEvent(m_terminateVirtualDesktopTrackerEvent.get());
    }
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

bool VirtualDesktop::IsWindowOnCurrentDesktop(HWND window) const
{
    std::optional<GUID> id = GetDesktopId(window);
    return id.has_value();
}

std::optional<GUID> VirtualDesktop::GetDesktopId(HWND window) const
{
    GUID id;
    BOOL isWindowOnCurrentDesktop = false;
    if (m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop) == S_OK && isWindowOnCurrentDesktop)
    {
        // Filter windows such as Windows Start Menu, Task Switcher, etc.
        if (m_vdManager->GetWindowDesktopId(window, &id) == S_OK && id != GUID_NULL)
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

void VirtualDesktop::HandleVirtualDesktopUpdates()
{
    HKEY virtualDesktopsRegKey = GetVirtualDesktopsRegKey();
    if (!virtualDesktopsRegKey)
    {
        return;
    }
    HANDLE regKeyEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    HANDLE events[2] = { regKeyEvent, m_terminateVirtualDesktopTrackerEvent.get() };
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

        m_vdUpdatedCallback();
    }
}