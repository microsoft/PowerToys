#include "pch.h"
#include "target_state.h"
#include "start_visible.h"
#include "keyboard_state.h"
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>

TargetState::TargetState(int ms_delay) :
    // TODO: All this processing should be done w/o a separate thread etc. in pre_wnd_proc of winkey_popup to avoid
    //       multithreading. Use SetTimer for delayed events
    delay(std::chrono::milliseconds(ms_delay)),
    thread(&TargetState::thread_proc, this)
{
}

constexpr unsigned VK_S = 0x53;

bool TargetState::signal_event(unsigned vk_code, bool key_down)
{
    std::unique_lock lock(mutex);
    // Ignore repeated key presses
    if (!events.empty() && events.back().key_down == key_down && events.back().vk_code == vk_code)
    {
        return false;
    }
    // Hide the overlay when WinKey + Shift + S is pressed
    if (key_down && state == Shown && vk_code == VK_S && (GetKeyState(VK_LSHIFT) || GetKeyState(VK_RSHIFT)))
    {
        // We cannot use normal hide() here, there is stuff that needs deinitialization.
        // It can be safely done when the user releases the WinKey.
        instance->quick_hide();
    }
    const bool win_key_released = !key_down && (vk_code == VK_LWIN || vk_code == VK_RWIN);
    constexpr auto overlay_fade_in_animation_time = std::chrono::milliseconds(300);
    const auto overlay_active = state == Shown && (std::chrono::system_clock::now() - signal_timestamp > overlay_fade_in_animation_time);
    const bool suppress_win_release = win_key_released && (state == ForceShown || overlay_active) && !nonwin_key_was_pressed_during_shown;

    events.push_back({ key_down, vk_code });
    lock.unlock();
    cv.notify_one();
    if (suppress_win_release)
    {
        // Send a 0xFF VK code, which is outside of the VK code range, to prevent
        // the start menu from appearing.
        INPUT input[3] = { {}, {}, {} };
        input[0].type = INPUT_KEYBOARD;
        input[0].ki.wVk = 0xFF;
        input[0].ki.dwExtraInfo = CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG;
        input[1].type = INPUT_KEYBOARD;
        input[1].ki.wVk = 0xFF;
        input[1].ki.dwFlags = KEYEVENTF_KEYUP;
        input[1].ki.dwExtraInfo = CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG;
        input[2].type = INPUT_KEYBOARD;
        input[2].ki.wVk = vk_code;
        input[2].ki.dwFlags = KEYEVENTF_KEYUP;
        input[2].ki.dwExtraInfo = CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG;
        SendInput(3, input, sizeof(INPUT));
    }
    return suppress_win_release;
}

void TargetState::was_hidden()
{
    std::unique_lock<std::recursive_mutex> lock(mutex);
    // Ignore callbacks from the D2DOverlayWindow
    if (state == ForceShown)
    {
        return;
    }
    state = Hidden;
    events.clear();
    lock.unlock();
    cv.notify_one();
}

void TargetState::exit()
{
    std::unique_lock lock(mutex);
    events.clear();
    state = Exiting;
    lock.unlock();
    cv.notify_one();
    thread.join();
}

KeyEvent TargetState::next()
{
    auto e = events.front();
    events.pop_front();
    return e;
}

void TargetState::handle_hidden()
{
    std::unique_lock lock(mutex);
    if (events.empty())
        cv.wait(lock);
    if (events.empty() || state == Exiting)
        return;
    auto event = next();
    if (event.key_down && (event.vk_code == VK_LWIN || event.vk_code == VK_RWIN))
    {
        state = Timeout;
        winkey_timestamp = std::chrono::system_clock::now();
    }
}

void TargetState::handle_shown(const bool forced)
{
    std::unique_lock lock(mutex);
    if (events.empty())
    {
        cv.wait(lock);
    }
    if (events.empty() || state == Exiting)
    {
        return;
    }
    auto event = next();
    if (event.vk_code == VK_LWIN || event.vk_code == VK_RWIN)
    {
        if (!forced && (!event.key_down || !winkey_held()))
        {
            state = Hidden;
        }
        return;
    }

    if (event.key_down)
    {
        nonwin_key_was_pressed_during_shown = true;
        lock.unlock();
        instance->on_held_press(event.vk_code);
    }
}

void TargetState::thread_proc()
{
    while (true)
    {
        switch (state)
        {
        case Hidden:
            handle_hidden();
            break;
        case Timeout:
            try
            {
                handle_timeout();
            }
            catch (...)
            {
                Logger::critical("Timeout, handle_timeout failed.");
            }
            break;
        case Shown:
            try
            {
                handle_shown(false);
            }
            catch (...)
            {
                Logger::critical("Shown, handle_shown failed.");
            }

            break;
        case ForceShown:
            try
            {
                handle_shown(true);
            }
            catch (...)
            {
                Logger::critical("ForceShown, handle_shown failed.");
            }

            break;
        case Exiting:
        default:
            return;
        }
    }
}

void TargetState::handle_timeout()
{
    std::unique_lock lock(mutex);
    auto wait_time = delay - (std::chrono::system_clock::now() - winkey_timestamp);
    if (events.empty())
    {
        cv.wait_for(lock, wait_time);
    }
    if (state == Exiting)
    {
        return;
    }

    // Skip all VK_*WIN-down events
    while (!events.empty())
    {
        auto event = events.front();
        if (event.key_down && (event.vk_code == VK_LWIN || event.vk_code == VK_RWIN))
            events.pop_front();
        else
            break;
    }
    // If we've detected that a user is holding anything other than VK_*WIN or start menu is visible, we should hide
    if (!events.empty() || !only_winkey_key_held() || is_start_visible())
    {
        state = Hidden;
        return;
    }

    if (std::chrono::system_clock::now() - winkey_timestamp < delay)
    {
        return;
    }

    signal_timestamp = std::chrono::system_clock::now();
    nonwin_key_was_pressed_during_shown = false;
    state = Shown;
    lock.unlock();
    instance->on_held();
}

void TargetState::set_delay(int ms_delay)
{
    std::unique_lock lock(mutex);
    delay = std::chrono::milliseconds(ms_delay);
}

void TargetState::toggle_force_shown()
{
    std::unique_lock lock(mutex);
    events.clear();
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
