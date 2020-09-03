#pragma once
#include <deque>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include "shortcut_guide.h"

struct KeyEvent
{
    bool key_down;
    unsigned vk_code;
};

class TargetState
{
public:
    TargetState(int ms_delay);
    bool signal_event(unsigned vk_code, bool key_down);
    void was_hidden();
    void exit();
    void set_delay(int ms_delay);

    void toggle_force_shown();
    bool active() const;

private:
    KeyEvent next();
    void handle_hidden();
    void handle_timeout();
    void handle_shown(const bool forced);
    void thread_proc();
    std::recursive_mutex mutex;
    std::condition_variable_any cv;
    std::chrono::system_clock::time_point winkey_timestamp, signal_timestamp;
    std::chrono::milliseconds delay;
    std::deque<KeyEvent> events;
    enum State
    {
        Hidden,
        Timeout,
        Shown,
        ForceShown,
        Exiting
    };
    std::atomic<State> state = Hidden;

    bool nonwin_key_was_pressed_during_shown = false;
    std::thread thread;
};
