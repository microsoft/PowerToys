#pragma once

#include <functional>

class KeyState
{
public:
    KeyState() :
        m_mouseState(false),
        m_shiftState(false),
        m_ctrlState(false)
    {};

    inline bool mouseState() const noexcept { return m_mouseState; }
    inline bool shiftState() const noexcept { return m_shiftState; }
    inline bool ctrlState() const noexcept { return m_ctrlState; }

    inline void setCallback(const std::function<void()>& updateCallback) noexcept
    {
        m_updateCallback = updateCallback;
    }
    
    inline void setMouseState(bool state) noexcept
    {
        if (m_mouseState != state)
        {
            m_mouseState = state;
            if (m_updateCallback)
            {
                m_updateCallback();
            }
        }
    }

    inline void setShiftState(bool state) noexcept
    {
        if (m_shiftState != state)
        {
            m_shiftState = state;
            if (m_updateCallback)
            {
                m_updateCallback();
            }
        }
    }
    
    inline void setCtrlState(bool state) noexcept
    {
        if (m_ctrlState != state)
        {
            m_ctrlState = state;
            if (m_updateCallback)
            {
                m_updateCallback();
            }
        }
    }

private:
    std::atomic<bool> m_mouseState;
    std::atomic<bool> m_shiftState;
    std::atomic<bool> m_ctrlState;

    std::function<void()> m_updateCallback;
};