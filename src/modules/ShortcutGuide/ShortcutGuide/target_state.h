#pragma once
#include <mutex>
#include <condition_variable>
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
    void was_hidden();
    void exit();
    void set_delay(int ms_delay);

    void toggle_force_shown();
    bool active() const;

private:
    std::recursive_mutex mutex;
    std::condition_variable_any cv;
    std::chrono::milliseconds delay;
    enum State
    {
        Hidden,
        Shown,
        ForceShown,
        Exiting
    };
    std::atomic<State> state = Hidden;
};
