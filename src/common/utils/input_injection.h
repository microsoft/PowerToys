#pragma once

#include <Windows.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <functional>
#include <thread>
#include <vector>

// Audited helper for injecting a keyboard chord (for example Ctrl+V) with SendInput.
//
// Rules it enforces:
//  - Modifier key-downs are never injected to "restore" an earlier keyboard state. A key-down built from a
//    stale GetAsyncKeyState snapshot can land after the user's physical key-up and leave the modifier
//    logically held system-wide until it is pressed again.
//  - It can wait (bounded) for the user to physically release the modifiers of the hotkey that triggered it,
//    then injects key-ups for any modifier that is still held, so the target sees exactly the requested chord.
//  - The whole chord (key-downs and key-ups) goes out in one SendInput batch, every event tagged with the
//    caller's dwExtraInfo. If SendInput sends fewer events than requested, key-ups are sent for every
//    chord key-down that went out without its key-up.
//
// Must not be called on a low-level keyboard hook thread: the physical key-ups it waits for are queued behind
// that hook and would never be observed.
namespace input_injection
{
    // Key-up for an unassigned virtual key. Sent while Win is held, it keeps the Win key-up from opening Start.
    inline constexpr WORD start_menu_suppression_key = 0xFF;

    inline constexpr std::array<WORD, 8> modifier_keys = { VK_LCONTROL, VK_RCONTROL, VK_LWIN, VK_RWIN, VK_LSHIFT, VK_RSHIFT, VK_LMENU, VK_RMENU };

    // The Win32 calls the helper makes. Tests replace them with fakes.
    struct system_api
    {
        std::function<UINT(UINT, INPUT*)> send_input = [](UINT count, INPUT* inputs) { return SendInput(count, inputs, sizeof(INPUT)); };
        std::function<bool(WORD)> is_key_down = [](WORD vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; };
        std::function<DWORD()> get_last_error = [] { return GetLastError(); };
        std::function<void(std::chrono::milliseconds)> sleep = [](std::chrono::milliseconds duration) { std::this_thread::sleep_for(duration); };
    };

    struct chord_options
    {
        // Set on every injected event so hooks (e.g. the runner's centralized keyboard hook) can recognize it.
        ULONG_PTR extra_info = 0;

        // How long to wait for the user to release the modifier keys before forcing key-ups. Zero doesn't wait.
        std::chrono::milliseconds modifier_release_timeout{ 0 };
        std::chrono::milliseconds poll_interval{ 10 };

        // Inject key-ups for modifiers that are still held, so the target doesn't see e.g. Win+Shift+Ctrl+V.
        bool release_held_modifiers = true;

        // Send the Start menu suppression key before releasing modifiers.
        bool suppress_start_menu = true;
    };

    struct chord_result
    {
        UINT requested = 0;
        UINT sent = 0;

        // Key-ups sent to undo the key-downs of a partially sent batch.
        UINT cleanup_requested = 0;
        UINT cleanup_sent = 0;

        // GetLastError() after SendInput sent fewer events than requested.
        DWORD error = ERROR_SUCCESS;

        // Modifiers were still held when the wait timed out; key-ups were injected for them.
        bool modifier_wait_timed_out = false;

        bool succeeded() const noexcept
        {
            return sent == requested;
        }
    };

    inline INPUT make_key_input(WORD vk, bool key_up, ULONG_PTR extra_info) noexcept
    {
        INPUT input = {};
        input.type = INPUT_KEYBOARD;
        input.ki.wVk = vk;
        input.ki.dwFlags = key_up ? KEYEVENTF_KEYUP : 0;
        input.ki.dwExtraInfo = extra_info;
        return input;
    }

    inline bool any_modifier_down(const system_api& api)
    {
        return std::any_of(modifier_keys.begin(), modifier_keys.end(), [&api](WORD vk) { return api.is_key_down(vk); });
    }

    // Polls until no modifier key is down. Returns false if one is still down after the timeout.
    inline bool wait_for_modifiers_release(std::chrono::milliseconds timeout, std::chrono::milliseconds poll_interval, const system_api& api = {})
    {
        const auto interval = (std::max)(poll_interval, std::chrono::milliseconds{ 1 });
        std::chrono::milliseconds waited{ 0 };
        while (any_modifier_down(api))
        {
            if (waited >= timeout)
            {
                return false;
            }

            api.sleep(interval);
            waited += interval;
        }

        return true;
    }

    // Presses keys in order and releases them in reverse order, e.g. { VK_CONTROL, 'V' } sends Ctrl+V.
    inline chord_result send_chord(const std::vector<WORD>& keys, const chord_options& options = {}, const system_api& api = {})
    {
        chord_result result;

        if (options.modifier_release_timeout.count() > 0)
        {
            result.modifier_wait_timed_out = !wait_for_modifiers_release(options.modifier_release_timeout, options.poll_interval, api);
        }

        std::vector<INPUT> inputs;
        inputs.reserve(1 + modifier_keys.size() + keys.size() * 2);

        if (options.suppress_start_menu)
        {
            inputs.push_back(make_key_input(start_menu_suppression_key, true, options.extra_info));
        }

        if (options.release_held_modifiers)
        {
            for (const auto vk : modifier_keys)
            {
                if (api.is_key_down(vk))
                {
                    inputs.push_back(make_key_input(vk, true, options.extra_info));
                }
            }
        }

        const size_t first_key_down = inputs.size();
        for (const auto vk : keys)
        {
            inputs.push_back(make_key_input(vk, false, options.extra_info));
        }

        for (auto it = keys.rbegin(); it != keys.rend(); ++it)
        {
            inputs.push_back(make_key_input(*it, true, options.extra_info));
        }

        result.requested = static_cast<UINT>(inputs.size());
        if (inputs.empty())
        {
            return result;
        }

        result.sent = api.send_input(result.requested, inputs.data());
        if (result.sent >= result.requested)
        {
            return result;
        }

        result.error = api.get_last_error();

        // Release, last pressed first, every key whose key-down went out but whose key-up didn't.
        const size_t sent = result.sent;
        const size_t key_count = keys.size();
        std::vector<INPUT> cleanup;
        for (size_t i = key_count; i-- > 0;)
        {
            const size_t key_down_index = first_key_down + i;
            const size_t key_up_index = first_key_down + (2 * key_count) - 1 - i;
            if (key_down_index < sent && key_up_index >= sent)
            {
                cleanup.push_back(make_key_input(keys[i], true, options.extra_info));
            }
        }

        if (!cleanup.empty())
        {
            result.cleanup_requested = static_cast<UINT>(cleanup.size());
            result.cleanup_sent = api.send_input(result.cleanup_requested, cleanup.data());
        }

        return result;
    }
}
