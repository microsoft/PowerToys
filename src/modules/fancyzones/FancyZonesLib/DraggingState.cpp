#include "pch.h"
#include "DraggingState.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>

DraggingState::DraggingState(const std::function<void()>& keyUpdateCallback) :
    m_secondaryMouseState(false),
    m_middleMouseState(false),
    m_mouseHook(std::bind(&DraggingState::OnSecondaryMouseDown, this), std::bind(&DraggingState::OnMiddleMouseDown, this)),
    m_keyUpdateCallback(keyUpdateCallback)
{
}

void DraggingState::Enable()
{
    if (FancyZonesSettings::settings().mouseSwitch)
    {
        m_mouseHook.enable();
    }
}

void DraggingState::Disable()
{
    m_dragging = false;
    m_secondaryMouseState = false;
    m_middleMouseState = false;
    m_shift = false;
    m_ctrl = false;

    m_mouseHook.disable();
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
    return m_ctrl || m_middleMouseState;
}

void DraggingState::SetShiftState(bool value) noexcept
{
    m_shift = value;
    m_keyUpdateCallback();
}

void DraggingState::SetCtrlState(bool value) noexcept
{
    m_ctrl = value;
    m_keyUpdateCallback();
}
