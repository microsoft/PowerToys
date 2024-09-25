#pragma once

#include <common/Display/dpi_aware.h>
#include <common/utils/excluded_apps.h>
#include <common/utils/window.h>

namespace WindowUtils
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const char SplashClassName[] = "MsoSplash";
        
        const wchar_t SystemAppsFolder[] = L"SYSTEMAPPS";
        
        const wchar_t CoreWindow[] = L"WINDOWS.UI.CORE.COREWINDOW";
        const wchar_t SearchUI[] = L"SEARCHUI.EXE";
        const wchar_t HelpWindow[] = L"WINDOWS\\HH.EXE";
        const wchar_t ApplicationFrameHost[] = L"WINDOWS\\SYSTEM32\\APPLICATIONFRAMEHOST.EXE";
        
        const wchar_t WorkspacesSnapshotTool[] = L"POWERTOYS.WORKSPACESSNAPSHOTTOOL";
        const wchar_t WorkspacesEditor[] = L"POWERTOYS.WORKSPACESEDITOR";
        const wchar_t WorkspacesLauncher[] = L"POWERTOYS.WORKSPACESLAUNCHER";
        const wchar_t WorkspacesWindowArranger[] = L"POWERTOYS.WORKSPACESWINDOWARRANGER";
    }

    inline bool IsRoot(HWND window) noexcept
    {
        return GetAncestor(window, GA_ROOT) == window;
    }

    inline bool IsMinimized(HWND window)
    {
        return IsIconic(window);
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

    inline bool IsExcludedByDefault(HWND window, const std::wstring& processPath)
    {
        std::wstring processPathUpper = processPath;
        CharUpperBuffW(processPathUpper.data(), static_cast<DWORD>(processPathUpper.length()));

        static std::vector<std::wstring> defaultExcludedFolders = { 
            NonLocalizable::SystemAppsFolder,
        };
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

        static std::vector<std::wstring> defaultExcludedApps = { 
            NonLocalizable::CoreWindow, 
            NonLocalizable::SearchUI, 
            NonLocalizable::HelpWindow,
            NonLocalizable::WorkspacesEditor, 
            NonLocalizable::WorkspacesLauncher,
            NonLocalizable::WorkspacesWindowArranger,
            NonLocalizable::WorkspacesSnapshotTool, 
        };
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
}