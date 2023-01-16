#pragma once

class WorkArea;

class WindowKeyboardSnap
{
public:
    WindowKeyboardSnap() = default;
    ~WindowKeyboardSnap() = default;

    static bool SnapForegroundWindow(DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
	
private:
    static bool SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
    static bool SnapHotkeyBasedOnPosition(HWND window, DWORD vkCode, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
    static bool ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea);
};
