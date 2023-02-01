#pragma once

#include <FancyZonesLib/Zone.h>

class WorkArea;

class WindowKeyboardSnap
{
    struct ExtendWindowModeData
    {
        HWND window{ nullptr };
        ZoneIndexSet windowInitialIndexSet{};
        ZoneIndex windowFinalIndex{ -1 };

        bool IsExtended(HWND wnd) const  
        {
            return window == wnd && windowFinalIndex != -1;
        }

        void Set(HWND w)
        {
            window = w;
            windowFinalIndex = -1;
            windowInitialIndexSet.clear();
        }

        void Reset()
        {
            window = nullptr;
            windowFinalIndex = -1;
            windowInitialIndexSet.clear();
        }
    };


public:
    WindowKeyboardSnap() = default;
    ~WindowKeyboardSnap() = default;

    bool SnapForegroundWindow(DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
	
private:
    bool SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
    bool SnapHotkeyBasedOnPosition(HWND window, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
    bool ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea);

    bool MoveByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea);
    bool MoveByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea);
    bool Extend(HWND window, DWORD vkCode, WorkArea* const workArea);

    ExtendWindowModeData m_extendData{}; // Needed for ExtendWindowByDirectionAndPosition
};
