#include "pch.h"
#include "VirtualDesktop.h"

#include "FancyZonesWinHookEventIDs.h"
#include "util.h"

#include <common/logger/logger.h>
#include <exception>
#include <utility>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RegCurrentVirtualDesktop[] = L"CurrentVirtualDesktop";
    const wchar_t RegVirtualDesktopIds[] = L"VirtualDesktopIDs";
    const wchar_t RegKeyVirtualDesktops[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";
    const wchar_t RegKeyVirtualDesktopsFromSession[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops";
}

namespace
{
    std::optional<GUID> ReadCurrentDesktopId(HKEY key) noexcept
    {
        GUID value{};
        DWORD size = sizeof(value);
        if (RegGetValueW(key, nullptr, NonLocalizable::RegCurrentVirtualDesktop, RRF_RT_REG_BINARY, nullptr, &value, &size) == ERROR_SUCCESS && size == sizeof(value))
        {
            return value;
        }

        return std::nullopt;
    }
}

std::optional<GUID> NewGetCurrentDesktopId()
{
    wil::unique_hkey key{};
    if (RegOpenKeyExW(HKEY_CURRENT_USER, NonLocalizable::RegKeyVirtualDesktops, 0, KEY_QUERY_VALUE, &key) == ERROR_SUCCESS)
    {
        return ReadCurrentDesktopId(key.get());
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

GUID VirtualDesktop::GetCurrentVirtualDesktopIdFromRegistry() const
{
    // On newer Windows builds, the current virtual desktop is persisted to
    // a totally different reg key. Look there first.
    std::optional<GUID> desktopId = NewGetCurrentDesktopId();
    if (desktopId.has_value())
    {
        return desktopId.value();
    }

    // Explorer persists current virtual desktop identifier to registry on a per session basis, but only
    // after first virtual desktop switch happens. If the user hasn't switched virtual desktops in this
    // session, value in registry will be empty.
    desktopId = GetDesktopIdFromCurrentSession();
    if (desktopId.has_value())
    {
        return desktopId.value();
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

    return GUID_NULL;
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
    BOOL isWindowOnCurrentDesktop = false;
    if (m_vdManager)
    {
        m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop);
    }

    return isWindowOnCurrentDesktop;
}

std::vector<HWND> VirtualDesktop::GetWindowsFromCurrentDesktop() const
{
    using result_t = std::vector<HWND>;
    result_t windows;

    auto callback = [](HWND window, LPARAM data) -> BOOL {
        result_t& result = *reinterpret_cast<result_t*>(data);
        result.push_back(window);
        return TRUE;
    };
    EnumWindows(callback, reinterpret_cast<LPARAM>(&windows));

    std::vector<HWND> result;
    for (auto window : windows)
    {
        BOOL isOnCurrentVD{};
        if (m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isOnCurrentVD) == S_OK && isOnCurrentVD)
        {
            result.push_back(window);
        }
    }

    return result;
}

VirtualDesktopWatcher::~VirtualDesktopWatcher()
{
    Stop();
}

void VirtualDesktopWatcher::Start(HWND window) noexcept
{
    if (m_thread.joinable())
    {
        return;
    }

    wil::unique_hkey key;
    const auto openResult = RegOpenKeyExW(HKEY_CURRENT_USER, NonLocalizable::RegKeyVirtualDesktops, 0, KEY_NOTIFY | KEY_QUERY_VALUE, &key);
    if (openResult != ERROR_SUCCESS)
    {
        Logger::warn(L"Failed to open the virtual desktop registry key for notifications. Error: {}", openResult);
        return;
    }

    // Take the baseline before FancyZones synchronizes its initial desktop.
    const auto initialDesktopId = ReadCurrentDesktopId(key.get());
    m_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
    if (!m_stopEvent)
    {
        Logger::warn(L"Failed to create the virtual desktop watcher stop event. Error: {}", GetLastError());
        return;
    }

    try
    {
        m_thread = std::thread([window, stopEvent = m_stopEvent.get(), key = std::move(key), lastDesktopId = initialDesktopId]() mutable noexcept {
            wil::unique_event changeEvent;
            changeEvent.reset(CreateEventW(nullptr, FALSE, FALSE, nullptr));
            if (!changeEvent)
            {
                Logger::warn(L"Failed to create the virtual desktop registry change event. Error: {}", GetLastError());
                return;
            }

            // Close the watched key before the event so any pending notification is canceled first.
            const auto watchedKey = std::move(key);
            const HANDLE events[] = { stopEvent, changeEvent.get() };
            for (;;)
            {
                // Arm before reading so writes during the read are signaled on the next wait.
                const auto notifyResult = RegNotifyChangeKeyValue(watchedKey.get(), FALSE, REG_NOTIFY_CHANGE_LAST_SET, changeEvent.get(), TRUE);
                if (notifyResult != ERROR_SUCCESS)
                {
                    Logger::warn(L"Failed to watch the virtual desktop registry key. Error: {}", notifyResult);
                    return;
                }

                const auto desktopId = ReadCurrentDesktopId(watchedKey.get());
                if (desktopId && desktopId != lastDesktopId)
                {
                    lastDesktopId = desktopId;
                    Logger::info(L"Virtual desktop switch detected in the registry: {}", FancyZonesUtils::GuidToString(*desktopId).value_or(L"<unknown>"));
                    PostMessageW(window, WM_PRIV_VD_SWITCH, 0, 0);
                }

                const auto waitResult = WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, INFINITE);
                if (waitResult != WAIT_OBJECT_0 + 1)
                {
                    if (waitResult == WAIT_FAILED)
                    {
                        Logger::warn(L"Failed to wait for virtual desktop registry changes. Error: {}", GetLastError());
                    }
                    return;
                }
            }
        });
    }
    catch (const std::exception& error)
    {
        Logger::warn("Failed to start the virtual desktop registry watcher. {}", error.what());
        m_stopEvent.reset();
    }
}

void VirtualDesktopWatcher::Stop() noexcept
{
    if (m_thread.joinable())
    {
        m_stopEvent.SetEvent();
        m_thread.join();
    }
    m_stopEvent.reset();
}
