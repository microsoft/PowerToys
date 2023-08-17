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

    bool Snap(HWND window, HMONITOR activeMonitor, DWORD vkCode, 
        const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, 
        const std::vector<HMONITOR>& monitors);
    bool Snap(HWND window, RECT windowRect, HMONITOR activeMonitor, DWORD vkCode, 
        const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, 
        const std::vector<std::pair<HMONITOR, RECT>>& monitors);
    bool Extend(HWND window, RECT windowRect, HMONITOR monitor, DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
	
private:
    bool SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<HMONITOR>& monitors);
    bool SnapBasedOnPositionOnAnotherMonitor(HWND window, RECT windowRect, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<std::pair<HMONITOR, RECT>>& monitors);
    
    bool MoveByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea);
    bool MoveByDirectionAndPosition(HWND window, RECT windowRect, DWORD vkCode, bool cycle, WorkArea* const workArea);
    bool Extend(HWND window, RECT windowRect, DWORD vkCode, WorkArea* const workArea);

    ExtendWindowModeData m_extendData{}; // Needed for ExtendWindowByDirectionAndPosition
};
