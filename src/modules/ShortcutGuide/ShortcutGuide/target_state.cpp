#include "pch.h"
#include "target_state.h"
#include "start_visible.h"
#include "keyboard_state.h"
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>

TargetState::TargetState(int ms_delay) :
    // TODO: All this processing should be done w/o a separate thread etc. in pre_wnd_proc of winkey_popup to avoid
    //       multithreading. Use SetTimer for delayed events
    delay(std::chrono::milliseconds(ms_delay))
{
}

constexpr unsigned VK_S = 0x53;

void TargetState::was_hidden()
{
    std::unique_lock<std::recursive_mutex> lock(mutex);
    // Ignore callbacks from the D2DOverlayWindow
    if (state == ForceShown)
    {
        return;
    }
    state = Hidden;
    lock.unlock();
    cv.notify_one();
}

void TargetState::exit()
{
    std::unique_lock lock(mutex);
    state = Exiting;
    lock.unlock();
    cv.notify_one();
}

void TargetState::set_delay(int ms_delay)
{
    std::unique_lock lock(mutex);
    delay = std::chrono::milliseconds(ms_delay);
}

void TargetState::toggle_force_shown()
{
    std::unique_lock lock(mutex);
    if (state != ForceShown)
    {
        state = ForceShown;
        instance->on_held();
    }
    else
    {
        state = Hidden;
    }
}

bool TargetState::active() const
{
    return state == ForceShown || state == Shown;
}
