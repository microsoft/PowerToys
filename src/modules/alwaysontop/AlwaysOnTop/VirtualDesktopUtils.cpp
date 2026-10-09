#include "pch.h"
#include "VirtualDesktopUtils.h"

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RegKeyVirtualDesktops[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";
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

VirtualDesktopUtils::VirtualDesktopUtils()
{
    auto res = CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&m_vdManager));
    if (FAILED(res))
    {
        Logger::error("Failed to create VirtualDesktopManager instance");
    }
}

VirtualDesktopUtils::~VirtualDesktopUtils()
{
    if (m_vdManager)
    {
        m_vdManager->Release();
    }
}

bool VirtualDesktopUtils::IsWindowOnCurrentDesktop(HWND window) const
{
    std::optional<GUID> id = GetDesktopId(window);
    return id.has_value();
}

std::optional<GUID> VirtualDesktopUtils::GetDesktopId(HWND window) const
{
    GUID id;
    BOOL isWindowOnCurrentDesktop = false;
    if (m_vdManager && m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop) == S_OK && isWindowOnCurrentDesktop)
    {
        // Filter windows such as Windows Start Menu, Task Switcher, etc.
        if (m_vdManager->GetWindowDesktopId(window, &id) == S_OK && id != GUID_NULL)
        {
            return id;
        }
    }

    return std::nullopt;
}
