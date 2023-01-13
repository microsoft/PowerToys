#include "pch.h"
#include "target_state.h"
#include "start_visible.h"
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>

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

void TargetState::toggle_force_shown()
{
    std::unique_lock lock(mutex);
    if (state != ForceShown)
    {
        state = ForceShown;
        overlay_window_instance->on_held();
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
