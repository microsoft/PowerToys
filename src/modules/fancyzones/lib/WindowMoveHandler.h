#pragma once

interface IFancyZonesSettings;
interface IZoneWindow;
class SecondaryMouseButtonsHook;
class ShiftKeyHook;

class WindowMoveHandler
{
public:
    WindowMoveHandler(const winrt::com_ptr<IFancyZonesSettings>& settings, SecondaryMouseButtonsHook* mouseHook, ShiftKeyHook* shiftHook);
    ~WindowMoveHandler();

    bool InMoveSize() const noexcept;
    bool IsDragEnabled() const noexcept;

    void OnMouseDown() noexcept;
    void OnShiftChangeState(bool state) noexcept;  //True for shift down event false for shift up

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<int>& indexSet, winrt::com_ptr<IZoneWindow> zoneWindow) noexcept;
    bool MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IZoneWindow> zoneWindow);

private:
    class WindowMoveHandlerPrivate* pimpl;
};