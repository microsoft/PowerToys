#pragma once

#include <Windows.h>
#include <ShellScalingApi.h>

#include <algorithm>

#include <common/Display/dpi_aware.h>
#include <common/utils/excluded_apps.h>
#include <common/utils/window.h>

// FancyZones WindowUtils
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
        const wchar_t ProjectsSnapshotTool[] = L"POWERTOYS.PROJECTSSNAPSHOTTOOL";
        const wchar_t ProjectsEditor[] = L"POWERTOYS.PROJECTSEDITOR";
        const wchar_t ProjectsLauncher[] = L"POWERTOYS.PROJECTSLAUNCHER";
    }

    inline bool IsRoot(HWND window) noexcept
    {
        return GetAncestor(window, GA_ROOT) == window;
    }

    inline bool IsMaximized(HWND window) noexcept
    {
        WINDOWPLACEMENT placement{};
        if (GetWindowPlacement(window, &placement) &&
            placement.showCmd == SW_SHOWMAXIMIZED)
        {
            return true;
        }
        return false;
    }

    constexpr bool HasStyle(LONG style, LONG styleToCheck) noexcept
    {
        return ((style & styleToCheck) == styleToCheck);
    }

    inline bool IsExcludedByDefault(HWND window, const std::wstring& processPath, const std::wstring& title)
    {
        std::wstring processPathUpper = processPath;
        CharUpperBuffW(processPathUpper.data(), static_cast<DWORD>(processPathUpper.length()));

        static std::vector<std::wstring> defaultExcludedFolders = { NonLocalizable::SystemAppsFolder, NonLocalizable::System, NonLocalizable::System32, NonLocalizable::SystemWOW64 };
        if (find_folder_in_path(processPathUpper, defaultExcludedFolders))
        {
            return true;
        }

        std::array<char, 256> className;
        GetClassNameA(window, className.data(), static_cast<int>(className.size()));
        if (is_system_window(window, className.data()))
        {
            return true;
        }

        if (strcmp(NonLocalizable::SplashClassName, className.data()) == 0)
        {
            return true;
        }

        static std::vector<std::wstring> defaultExcludedApps = { NonLocalizable::CoreWindow, NonLocalizable::SearchUI, NonLocalizable::ProjectsEditor, NonLocalizable::ProjectsLauncher, NonLocalizable::ProjectsSnapshotTool };
        return (check_excluded_app(window, processPathUpper, defaultExcludedApps));
    }

    inline RECT GetWindowRect(HWND window)
    {
        RECT rect;
        if (GetWindowRect(window, &rect))
        {
            float width = static_cast<float>(rect.right - rect.left);
            float height = static_cast<float>(rect.bottom - rect.top);
            float originX = static_cast<float>(rect.left);
            float originY = static_cast<float>(rect.top);

            DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), width, height);
            DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), originX, originY);

            return RECT(static_cast<LONG>(std::roundf(originX)),
                        static_cast<LONG>(std::roundf(originY)),
                        static_cast<LONG>(std::roundf(originX + width)),
                        static_cast<LONG>(std::roundf(originY + height)));
        }

        return rect;
    }
}

// addition for Projects
namespace WindowUtils
{
    inline bool IsMinimized(HWND window)
    {
        return IsIconic(window);
    }

    #define MAX_TITLE_LENGTH 255
    inline std::wstring GetWindowTitle(HWND window)
    {
        WCHAR title[MAX_TITLE_LENGTH];
        int len = GetWindowTextW(window, title, MAX_TITLE_LENGTH);
        if (len <= 0)
        {
            return {};
        }

        return std::wstring(title);
    }

    /*bool IsFullscreen(HWND window)
    {
        TODO
    }*/
}