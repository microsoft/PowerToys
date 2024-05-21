#include "pch.h"
#include "WindowUtils.h"

#include <ShellScalingApi.h>

namespace Common
{
    namespace Display
    {
        namespace DPIAware
        {
            constexpr inline int DEFAULT_DPI = 96;
        
            void InverseConvert(HMONITOR monitor_handle, float& width, float& height)
            {
                if (monitor_handle == NULL)
                {
                    const POINT ptZero = { 0, 0 };
                    monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
                }

                UINT dpi_x, dpi_y;
                if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK)
                {
                    width = width * DPIAware::DEFAULT_DPI / dpi_x;
                    height = height * DPIAware::DEFAULT_DPI / dpi_y;
                }
            }
        }
    }
}

namespace WindowUtils
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const wchar_t SystemAppsFolder[] = L"SYSTEMAPPS";
        const wchar_t System[] = L"WINDOWS/SYSTEM";
        const wchar_t System32[] = L"SYSTEM32";
        const wchar_t SystemWOW64[] = L"SYSTEMWOW64";
        const char SplashClassName[] = "MsoSplash";
        const wchar_t CoreWindow[] = L"WINDOWS.UI.CORE.COREWINDOW";
        const wchar_t SearchUI[] = L"SEARCHUI.EXE";
        const wchar_t ProjectsSnapshotTool[] = L"PROJECTSSNAPSHOTTOOL";
        const wchar_t ProjectsEditor[] = L"PROJECTSEDITOR";
        const wchar_t ProjectsLauncher[] = L"PROJECTSLAUNCHER";
    }

    bool IsRoot(HWND window) noexcept
    {
        return GetAncestor(window, GA_ROOT) == window;
    }

    bool IsMaximized(HWND window) noexcept
    {
        WINDOWPLACEMENT placement{};
        if (GetWindowPlacement(window, &placement) &&
            placement.showCmd == SW_SHOWMAXIMIZED)
        {
            return true;
        }
        return false;
    }

    bool IsExcludedByDefault(HWND window, const std::wstring& processPath, const std::wstring& title)
    {
        std::wstring processPathUpper = processPath;
        CharUpperBuffW(processPathUpper.data(), static_cast<DWORD>(processPathUpper.length()));

        static std::vector<std::wstring> defaultExcludedFolders = { NonLocalizable::SystemAppsFolder, NonLocalizable::System, NonLocalizable::System32, NonLocalizable::SystemWOW64 };
        if (Common::Utils::ExcludedApps::find_folder_in_path(processPathUpper, defaultExcludedFolders))
        {
            return true;
        }

        std::array<char, 256> className;
        GetClassNameA(window, className.data(), static_cast<int>(className.size()));
        if (Common::Utils::Window::is_system_window(window, className.data()))
        {
            return true;
        }

        if (strcmp(NonLocalizable::SplashClassName, className.data()) == 0)
        {
            return true;
        }

        static std::vector<std::wstring> defaultExcludedApps = { NonLocalizable::CoreWindow, NonLocalizable::SearchUI, NonLocalizable::ProjectsEditor, NonLocalizable::ProjectsLauncher, NonLocalizable::ProjectsSnapshotTool };
        return (Common::Utils::ExcludedApps::check_excluded_app(processPathUpper, title, defaultExcludedApps));
    }

    RECT GetWindowRect(HWND window)
    {
        RECT rect;
        if (GetWindowRect(window, &rect))
        {
            float width = static_cast<float>(rect.right - rect.left);
            float height = static_cast<float>(rect.bottom - rect.top);
            float originX = static_cast<float>(rect.left);
            float originY = static_cast<float>(rect.top);

            Common::Display::DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), width, height);
            Common::Display::DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), originX, originY);

            return RECT(static_cast<LONG>(std::roundf(originX)),
                static_cast<LONG>(std::roundf(originY)), 
                static_cast<LONG>(std::roundf(originX + width)), 
                static_cast<LONG>(std::roundf(originY + height)));
        }

        return rect;
    }
}