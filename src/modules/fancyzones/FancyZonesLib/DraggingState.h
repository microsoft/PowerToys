#pragma once

#include <FancyZonesLib/KeyState.h>
#include <FancyZonesLib/MouseButtonsHook.h>

class DraggingState
{
public:
    DraggingState(const std::function<void()>& keyUpdateCallback);
    ~DraggingState() = default;

    void Enable();
    void Disable();
    void UpdateDraggingState() noexcept;

    bool IsDragging() const noexcept;
    bool IsSelectManyZonesState() const noexcept;

    void SetShiftState(bool value) noexcept;

private:
    void OnSecondaryMouseDown();
    void OnMiddleMouseDown();

    std::atomic<bool> m_secondaryMouseState;
    std::atomic<bool> m_middleMouseState;
    MouseButtonsHook m_mouseHook;
    KeyState<VK_LCONTROL, VK_RCONTROL> m_ctrlKeyState;
    
    bool m_shift{};

    std::function<void()> m_keyUpdateCallback;

    bool m_dragging{}; // True if we should be showing zone hints while dragging
};
