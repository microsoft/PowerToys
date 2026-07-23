#include "pch.h"
#include "DraggingState.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>

DraggingState::DraggingState(const std::function<void()>& keyUpdateCallback, const std::function<bool(bool)>& layoutSwitchByWheelCallback) :
    m_secondaryMouseState(false),
    m_middleMouseState(false),
    m_mouseHook(std::bind(&DraggingState::OnSecondaryMouseDown, this), std::bind(&DraggingState::OnMiddleMouseDown, this), std::bind(&DraggingState::OnMouseWheel, this, std::placeholders::_1)),
    m_ctrlKeyState(keyUpdateCallback),
    m_keyUpdateCallback(keyUpdateCallback),
    m_layoutSwitchByWheelCallback(layoutSwitchByWheelCallback)
{
}

void DraggingState::Enable()
{
    if (FancyZonesSettings::settings().mouseSwitch || FancyZonesSettings::settings().mouseWheelLayoutSwitch)
    {
        m_mouseHook.enable();
    }

    m_ctrlKeyState.enable();
}

void DraggingState::Disable()
{
    m_dragging = false;
    m_secondaryMouseState = false;
    m_middleMouseState = false;
    m_shift = false;

    m_mouseHook.disable();
    m_ctrlKeyState.disable();
}

void DraggingState::UpdateDraggingState() noexcept
{
    // This updates m_dragging depending on if the shift key is being held down
    if (FancyZonesSettings::settings().shiftDrag)
    {
        m_dragging = (m_shift ^ m_secondaryMouseState);
    }
    else
    {
        m_dragging = !(m_shift ^ m_secondaryMouseState);
    }
}

void DraggingState::OnSecondaryMouseDown()
{
    m_secondaryMouseState = !m_secondaryMouseState;
    m_keyUpdateCallback();
}

bool DraggingState::OnMouseWheel(bool up)
{
    // Layout switching by wheel is active only while zones are shown, so the wheel
    // keeps scrolling normally in any other state
    if (m_dragging && FancyZonesSettings::settings().mouseWheelLayoutSwitch)
    {
        return m_layoutSwitchByWheelCallback(up);
    }

    return false;
}

void DraggingState::OnMiddleMouseDown()
{
    if (FancyZonesSettings::settings().mouseMiddleClickSpanningMultipleZones)
    {
        m_middleMouseState = !m_middleMouseState;
    }
    else
    {
        m_secondaryMouseState = !m_secondaryMouseState;
    }

    m_keyUpdateCallback();
}

bool DraggingState::IsDragging() const noexcept
{
    return m_dragging;
}

bool DraggingState::IsSelectManyZonesState() const noexcept
{
    return m_ctrlKeyState.state() || m_middleMouseState;
}

void DraggingState::SetShiftState(bool value) noexcept
{
    m_shift = value;
    m_keyUpdateCallback();
}
