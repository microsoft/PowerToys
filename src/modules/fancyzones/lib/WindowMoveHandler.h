#pragma once

interface IFancyZonesSettings;
interface IZoneWindow;

class WindowMoveHandler
{
public:
    WindowMoveHandler(const winrt::com_ptr<IFancyZonesSettings>& settings);
    ~WindowMoveHandler();

    bool InMoveSize() const noexcept;
    bool IsDragEnabled() const noexcept;

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, HMONITOR monitor, const std::vector<int>& indexSet, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    bool MoveWindowIntoZoneByDirection(HMONITOR monitor, HWND window, DWORD vkCode, bool cycle, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap);

private:
    class WindowMoveHandlerPrivate* pimpl;
};