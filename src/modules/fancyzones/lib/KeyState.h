#pragma once

#include "GenericKeyHook.h"

template<int... keys>
class KeyState
{
public:
    KeyState(const std::function<void()>& callback) :
        m_state(false),
        m_hook(std::bind(&KeyState::onChangeState, this, std::placeholders::_1)),
        m_updateCallback(callback)
    {
    
    }

    inline bool state() const noexcept { return m_state; }

    inline void enable()
    {
        m_hook.enable();
        m_state = (((GetAsyncKeyState(keys) & 0x8000) || ...));
    }

    inline void disable()
    {
        m_hook.disable();
    }

    inline void setState(bool state) noexcept
    {
        if (m_state != state)
        {
            m_state = state;
            if (m_updateCallback)
            {
                m_updateCallback();
            }
        }
    }

private:
    inline void onChangeState(bool state) noexcept
    {
        setState(state);
    }

private:
    std::atomic<bool> m_state;
    GenericKeyHook<keys...> m_hook;
    std::function<void()> m_updateCallback;
};