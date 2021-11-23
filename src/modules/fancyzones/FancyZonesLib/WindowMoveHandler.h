#pragma once

#include "FancyZonesWindowProperties.h"
#include "KeyState.h"
#include "SecondaryMouseButtonsHook.h"

#include <functional>

interface IFancyZonesSettings;
interface IWorkArea;

class WindowMoveHandler
{
public:
    WindowMoveHandler(const winrt::com_ptr<IFancyZonesSettings>& settings, const std::function<void()>& keyUpdateCallback);

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IWorkArea>>& workAreaMap) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IWorkArea>>& workAreaMap) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IWorkArea>>& workAreaMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, winrt::com_ptr<IWorkArea> workArea, bool suppressMove = false) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IWorkArea> workArea) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IWorkArea> workArea) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode, winrt::com_ptr<IWorkArea> workArea) noexcept;

    inline void OnMouseDown() noexcept
    {
        m_mouseState = !m_mouseState;
        m_keyUpdateCallback();
    }

    inline bool IsDragEnabled() const noexcept
    {
        return m_dragEnabled;
    }

    inline bool InDragging() const noexcept
    {
        return m_inDragging;
    }

private:
    struct WindowTransparencyProperties
    {
        HWND draggedWindow = nullptr;
        long draggedWindowExstyle = 0;
        COLORREF draggedWindowCrKey = RGB(0, 0, 0);
        DWORD draggedWindowDwFlags = 0;
        BYTE draggedWindowInitialAlpha = 0;
    };

    // MoveSize related window properties
    struct MoveSizeWindowInfo
    {
        // True if from the styles the window looks like a standard window
        bool isStandardWindow = false;
        // True if the window is a top-level window that does not have a visible owner
        bool hasNoVisibleOwner = false;
    };

    void WarnIfElevationIsRequired(HWND window) noexcept;
    void UpdateDragState() noexcept;

    void SetWindowTransparency(HWND window) noexcept;
    void ResetWindowTransparency() noexcept;

    winrt::com_ptr<IFancyZonesSettings> m_settings{};

    bool m_inDragging{}; // Whether or not a move/size operation is currently active
    HWND m_draggedWindow{}; // The window that is being moved/sized
    MoveSizeWindowInfo m_draggedWindowInfo; // MoveSizeWindowInfo of the window at the moment when dragging started
    winrt::com_ptr<IWorkArea> m_draggedWindowWorkArea; // "Active" WorkArea, where the move/size is happening. Will update as drag moves between monitors.
    bool m_dragEnabled{}; // True if we should be showing zone hints while dragging

    WindowTransparencyProperties m_windowTransparencyProperties;

    std::atomic<bool> m_mouseState;
    SecondaryMouseButtonsHook m_mouseHook;
    KeyState<VK_LSHIFT, VK_RSHIFT> m_shiftKeyState;
    KeyState<VK_LCONTROL, VK_RCONTROL> m_ctrlKeyState;
    std::function<void()> m_keyUpdateCallback;
};
