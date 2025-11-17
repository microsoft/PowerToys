#include "pch.h"
#include "KeyDelay.h"

// NOTE: The destructor should never be called on the DelayThread, i.e. from any of shortPress, longPress or longPressReleased, as it will re-enter the mutex. Even if the mutex is removed it will deadlock because of the join statement
KeyDelay::~KeyDelay()
{
    std::unique_lock<std::mutex> l(_queueMutex);
    _quit = true;
    _cv.notify_all();
    l.unlock();
    _delayThread.join();
}

void KeyDelay::KeyEvent(LowlevelKeyboardEvent* ev)
{
    std::lock_guard guard(_queueMutex);
    _queue.push({ ev->lParam->time, ev->wParam });
    _cv.notify_all();
}

KeyTimedEvent KeyDelay::NextEvent()
{
    auto ev = _queue.front();
    _queue.pop();
    return ev;
}

bool KeyDelay::CheckIfMillisHaveElapsed(DWORD64 first, DWORD64 last, DWORD64 duration)
{
    if (first < last && first <= first + duration)
    {
        return first + duration < last;
    }
    else
    {
        first += ULLONG_MAX / 2;
        last += ULLONG_MAX / 2;
        return first + duration < last;
    }
}

bool KeyDelay::HasNextEvent()
{
    return !_queue.empty();
}

bool KeyDelay::HandleRelease()
{
    while (HasNextEvent())
    {
        auto ev = NextEvent();
        switch (ev.message)
        {
        case WM_KEYDOWN:
        case WM_SYSKEYDOWN:
            _state = KeyDelayState::ON_HOLD;
            _initialHoldKeyDown = ev.time;
            return false;
        case WM_KEYUP:
        case WM_SYSKEYUP:
            break;
        }
    }

    return true;
}

bool KeyDelay::HandleOnHold(std::unique_lock<std::mutex>& cvLock)
{
    while (HasNextEvent())
    {
        auto ev = NextEvent();
        switch (ev.message)
        {
        case WM_KEYDOWN:
        case WM_SYSKEYDOWN:
            break;
        case WM_KEYUP:
        case WM_SYSKEYUP:
            if (CheckIfMillisHaveElapsed(_initialHoldKeyDown, ev.time, LONG_PRESS_DELAY_MILLIS))
            {
                if (_onLongPressDetected != nullptr)
                {
                    _onLongPressDetected(_key);
                }
                if (_onLongPressReleased != nullptr)
                {
                    _onLongPressReleased(_key);
                }
            }
            else
            {
                if (_onShortPress != nullptr)
                {
                    _onShortPress(_key);
                }
            }
            _state = KeyDelayState::RELEASED;
            return false;
        }
    }

    if (CheckIfMillisHaveElapsed(_initialHoldKeyDown, GetTickCount64(), LONG_PRESS_DELAY_MILLIS))
    {
        if (_onLongPressDetected != nullptr)
        {
            _onLongPressDetected(_key);
        }
        _state = KeyDelayState::ON_HOLD_TIMEOUT;
    }
    else
    {
        _cv.wait_for(cvLock, std::chrono::milliseconds(ON_HOLD_WAIT_TIMEOUT_MILLIS));
    }
    return false;
}

bool KeyDelay::HandleOnHoldTimeout()
{
    while (HasNextEvent())
    {
        auto ev = NextEvent();
        switch (ev.message)
        {
        case WM_KEYDOWN:
        case WM_SYSKEYDOWN:
            break;
        case WM_KEYUP:
        case WM_SYSKEYUP:
            if (_onLongPressReleased != nullptr)
            {
                _onLongPressReleased(_key);
            }
            _state = KeyDelayState::RELEASED;
            return false;
        }
    }

    return true;
}

void KeyDelay::DelayThread()
{
    std::unique_lock<std::mutex> qLock(_queueMutex);
    bool shouldWait = true;
    while (!_quit)
    {
        if (shouldWait)
        {
            _cv.wait(qLock);
        }

        switch (_state)
        {
        case KeyDelayState::RELEASED:
            shouldWait = HandleRelease();
            break;
        case KeyDelayState::ON_HOLD:
            shouldWait = HandleOnHold(qLock);
            break;
        case KeyDelayState::ON_HOLD_TIMEOUT:
            shouldWait = HandleOnHoldTimeout();
            break;
        }
    }
}