#include "pch.h"
#include "DraggingState.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>

DraggingState::DraggingState(const std::function<void()>& keyUpdateCallback) :
    m_mouseState(false),
    m_middleMouseState(false),
    m_mouseHook(std::bind(&DraggingState::OnMouseDown, this)),
    m_middleMouseHook(std::bind(&DraggingState::OnMiddleMouseDown, this)),
    m_leftShiftKeyState(keyUpdateCallback),
    m_rightShiftKeyState(keyUpdateCallback),
    m_ctrlKeyState(keyUpdateCallback),
    m_keyUpdateCallback(keyUpdateCallback)
{
}

void DraggingState::Enable()
{
    if (FancyZonesSettings::settings().mouseSwitch)
    {
        m_mouseHook.enable();
        m_middleMouseHook.enable();
    }

    m_leftShiftKeyState.enable();
    m_rightShiftKeyState.enable();
    m_ctrlKeyState.enable();
}

void DraggingState::Disable()
{
    const bool leftShiftPressed = m_leftShiftKeyState.state();
    const bool rightShiftPressed = m_rightShiftKeyState.state();

    if (FancyZonesSettings::settings().shiftDrag)
    {
        if (leftShiftPressed)
        {
            FancyZonesUtils::SwallowKey(VK_LSHIFT);
        }

        if (rightShiftPressed)
        {
            FancyZonesUtils::SwallowKey(VK_RSHIFT);
        }
    }

    m_dragging = false;
    m_mouseState = false;
    m_middleMouseState = false;

    m_mouseHook.disable();
    m_middleMouseHook.disable();
    m_leftShiftKeyState.disable();
    m_rightShiftKeyState.disable();
    m_ctrlKeyState.disable();
}

void DraggingState::UpdateDraggingState() noexcept
{
    // This updates m_dragEnabled depending on if the shift key is being held down
    if (FancyZonesSettings::settings().shiftDrag)
    {
        m_dragging = ((m_leftShiftKeyState.state() || m_rightShiftKeyState.state()) ^ m_mouseState);
    }
    else
    {
        m_dragging = !((m_leftShiftKeyState.state() || m_rightShiftKeyState.state()) ^ m_mouseState);
    }
}

void DraggingState::OnMouseDown()
{
    m_mouseState = !m_mouseState;
    m_keyUpdateCallback();
}

void DraggingState::OnMiddleMouseDown()
{
    m_middleMouseState = !m_middleMouseState;
    m_keyUpdateCallback();
}

bool DraggingState::IsDragging() const noexcept
{
    return m_dragging;
}

bool DraggingState::IsSelectManyZonesState() const noexcept
{
    return m_ctrlKeyState.state() ^ m_middleMouseState;
}