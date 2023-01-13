#pragma once
#include <functional>
#include <thread>
#include <queue>
#include <mutex>

#include <common/hooks/LowlevelKeyboardEvent.h>
// Available states for the KeyDelay state machine.
enum class KeyDelayState
{
    RELEASED,
    ON_HOLD,
    ON_HOLD_TIMEOUT,
};

// Virtual key + timestamp (in millis since Windows startup)
struct KeyTimedEvent
{
    DWORD64 time;
    WPARAM message;
};

// Handles delayed key inputs.
// Implemented as a state machine running on its own thread.
// Thread stops on destruction.
class KeyDelay
{
public:
    KeyDelay(
        DWORD key,
        std::function<void(DWORD)> onShortPress,
        std::function<void(DWORD)> onLongPressDetected,
        std::function<void(DWORD)> onLongPressReleased) :
        _quit(false),
        _state(KeyDelayState::RELEASED),
        _initialHoldKeyDown(0),
        _key(key),
        _onShortPress(onShortPress),
        _onLongPressDetected(onLongPressDetected),
        _onLongPressReleased(onLongPressReleased),
        _delayThread(&KeyDelay::DelayThread, this){};

    // Enqueue new KeyTimedEvent and notify the condition variable.
    void KeyEvent(LowlevelKeyboardEvent* ev);
    ~KeyDelay();

private:
    // Runs the state machine, waits if there is no events to process.
    // Checks for _quit condition.
    void DelayThread();

    // Manage state transitions and trigger callbacks on certain events.
    // Returns whether or not the thread should wait on new events.
    bool HandleRelease();
    bool HandleOnHold(std::unique_lock<std::mutex>& cvLock);
    bool HandleOnHoldTimeout();

    // Get next key event in queue.
    KeyTimedEvent NextEvent();
    bool HasNextEvent();

    // Check if <duration> milliseconds passed since <first> millisecond.
    // Also checks for overflow conditions.
    bool CheckIfMillisHaveElapsed(DWORD64 first, DWORD64 last, DWORD64 duration);

    bool _quit;
    KeyDelayState _state;

    // Callback functions, the key provided in the constructor is passed as an argument.
    std::function<void(DWORD)> _onLongPressDetected;
    std::function<void(DWORD)> _onLongPressReleased;
    std::function<void(DWORD)> _onShortPress;

    // Queue holding key events that are not processed yet. Should be kept synchronized
    // using _queueMutex
    std::queue<KeyTimedEvent> _queue;
    std::mutex _queueMutex;

    // DelayThread waits on this condition variable when there is no events to process.
    std::condition_variable _cv;

    // Keeps track of the time at which the initial KEY_DOWN event happened.
    DWORD64 _initialHoldKeyDown;

    // Virtual Key provided in the constructor. Passed to callback functions.
    DWORD _key;

    // Declare _delayThread after all other members so that it is the last to be initialized by the constructor
    std::thread _delayThread;

    static const DWORD64 LONG_PRESS_DELAY_MILLIS = 900;
    static const DWORD64 ON_HOLD_WAIT_TIMEOUT_MILLIS = 50;
};
