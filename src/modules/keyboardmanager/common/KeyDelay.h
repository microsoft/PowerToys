#pragma once
#include <interface/lowlevel_keyboard_event_data.h>
#include <functional>
#include <thread>
#include <queue>
#include <mutex>
#include <chrono>

enum class KeyDelayState 
{
    RELEASED,
    ON_HOLD,
    ON_HOLD_TIMEOUT,
};

struct KeyTimedEvent
{
    DWORD time;
    WPARAM message;
};

class KeyDelay
{
public:
    KeyDelay(
        DWORD key,
        std::function<void(DWORD)> onShortPress,
        std::function<void(DWORD)> onLongPressDetected,
        std::function<void(DWORD)> onLongPressReleased
    ) :
        _quit(false), 
        _state(KeyDelayState::RELEASED),
        _initialHoldKeyDown(0),
        _key(key),
        _onShortPress(onShortPress),
        _onLongPressDetected(onLongPressDetected),
        _onLongPressReleased(onLongPressReleased),
        _delayThread(&KeyDelay::DelayThread, this)
    {};

    void KeyEvent(LowlevelKeyboardEvent* ev);
    ~KeyDelay();

private:
    void DelayThread();
    bool HandleRelease();
    bool HandleOnHold(std::unique_lock<std::mutex>& cvLock);
    bool HandleOnHoldTimeout();
    KeyTimedEvent NextEvent();
    bool HasNextEvent();
    bool CheckIfMillisHaveElapsed(DWORD first, DWORD last, DWORD duration);

    std::thread _delayThread;
    bool _quit;
    KeyDelayState _state;
    std::function<void(DWORD)> _onLongPressDetected;
    std::function<void(DWORD)> _onLongPressReleased;
    std::function<void(DWORD)> _onShortPress;
    std::queue<KeyTimedEvent> _queue;
    std::mutex _queueMutex;
    std::condition_variable _cv;
    DWORD _initialHoldKeyDown;
    DWORD _key;
};


