#pragma once

#include "input_injection.h"

#include <Windows.h>

#include <algorithm>
#include <chrono>
#include <functional>
#include <vector>

// Copies the foreground app's current selection to the clipboard: tries WM_COPY on the focused control, falls back
// to Ctrl+C sent through input_injection::send_chord, and watches the clipboard sequence number for a bounded time
// to tell whether anything was copied.
//
// Never logs; callers inspect the returned copy_result. Blocks for up to roughly
// attempts * (wm_copy_timeout + modifier_release_timeout + 2 * clipboard_timeout) + (attempts - 1) * retry_delay,
// so it must not be called on a low-level keyboard hook thread (the modifier key-ups it waits for are queued behind
// that hook) or on a UI thread.
namespace copy_selection
{
    // The Win32 calls the helper makes. Tests replace them with fakes.
    struct system_api
    {
        std::function<DWORD()> get_clipboard_sequence_number = [] { return GetClipboardSequenceNumber(); };

        // Fills in GUI info for the foreground thread. Returns false on failure; the error comes from input.get_last_error.
        std::function<bool(GUITHREADINFO&)> get_gui_thread_info = [](GUITHREADINFO& info) {
            info.cbSize = sizeof(GUITHREADINFO);
            return GetGUIThreadInfo(0, &info) != FALSE;
        };

        // Sends WM_COPY without hanging on an unresponsive target. Returns false if the message wasn't delivered.
        std::function<bool(HWND, std::chrono::milliseconds)> send_copy_message = [](HWND target, std::chrono::milliseconds timeout) {
            DWORD_PTR result = 0;
            return SendMessageTimeout(target, WM_COPY, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, static_cast<UINT>(timeout.count()), &result) != 0;
        };

        // Used for SendInput, the keyboard state, GetLastError and every sleep.
        input_injection::system_api input;
    };

    struct copy_options
    {
        // Each attempt tries WM_COPY (when enabled) and then, if the clipboard didn't change, Ctrl+C.
        int attempts = 1;
        std::chrono::milliseconds retry_delay{ 100 };

        bool try_wm_copy = true;
        std::chrono::milliseconds wm_copy_timeout{ 50 };

        // How long to watch the clipboard sequence number after each WM_COPY or Ctrl+C, and how often to check it.
        std::chrono::milliseconds clipboard_timeout{ 150 };
        std::chrono::milliseconds clipboard_poll_interval{ 30 };

        // Used for Ctrl+C, including dwExtraInfo and the bounded wait for the user's modifiers to be released.
        input_injection::chord_options chord;
    };

    enum class wm_copy_outcome
    {
        not_attempted,
        gui_thread_info_failed,
        no_target_window,
        send_failed,
        sent,
    };

    struct attempt_result
    {
        wm_copy_outcome wm_copy = wm_copy_outcome::not_attempted;

        // GetLastError() after GetGUIThreadInfo failed.
        DWORD wm_copy_error = ERROR_SUCCESS;

        bool ctrl_c_attempted = false;
        input_injection::chord_result ctrl_c;
    };

    enum class copy_status
    {
        // The clipboard sequence number changed after WM_COPY or Ctrl+C.
        copied,

        // Nothing changed the clipboard within the timeout, e.g. there was no selection.
        timed_out,

        // Nothing changed the clipboard and the last Ctrl+C couldn't be fully injected.
        injection_failed,
    };

    enum class copy_method
    {
        none,
        wm_copy,
        ctrl_c,
    };

    struct copy_result
    {
        copy_status status = copy_status::timed_out;
        copy_method method = copy_method::none;
        std::vector<attempt_result> attempts;

        bool copied() const noexcept
        {
            return status == copy_status::copied;
        }
    };

    inline const std::vector<WORD>& ctrl_c_chord()
    {
        static const std::vector<WORD> keys = { VK_CONTROL, 'C' };
        return keys;
    }

    // Returns true as soon as the sequence number differs from initial_sequence, false once the timeout has elapsed.
    inline bool wait_for_clipboard_change(DWORD initial_sequence, std::chrono::milliseconds timeout, std::chrono::milliseconds poll_interval, const system_api& api = {})
    {
        const auto interval = (std::max)(poll_interval, std::chrono::milliseconds{ 1 });
        std::chrono::milliseconds waited{ 0 };
        while (api.get_clipboard_sequence_number() == initial_sequence)
        {
            if (waited >= timeout)
            {
                return false;
            }

            api.input.sleep(interval);
            waited += interval;
        }

        return true;
    }

    inline bool try_wm_copy(const copy_options& options, attempt_result& attempt, const system_api& api)
    {
        GUITHREADINFO info = {};
        info.cbSize = sizeof(GUITHREADINFO);
        if (!api.get_gui_thread_info(info))
        {
            attempt.wm_copy = wm_copy_outcome::gui_thread_info_failed;
            attempt.wm_copy_error = api.input.get_last_error();
            return false;
        }

        const HWND target = info.hwndFocus ? info.hwndFocus : info.hwndActive;
        if (!target)
        {
            attempt.wm_copy = wm_copy_outcome::no_target_window;
            return false;
        }

        if (!api.send_copy_message(target, options.wm_copy_timeout))
        {
            attempt.wm_copy = wm_copy_outcome::send_failed;
            return false;
        }

        attempt.wm_copy = wm_copy_outcome::sent;
        return true;
    }

    inline copy_result copy(const copy_options& options = {}, const system_api& api = {})
    {
        copy_result result;
        const int attempts = (std::max)(options.attempts, 1);
        for (int i = 0; i < attempts; ++i)
        {
            if (i > 0)
            {
                api.input.sleep(options.retry_delay);
            }

            attempt_result& attempt = result.attempts.emplace_back();

            if (options.try_wm_copy)
            {
                const DWORD sequence = api.get_clipboard_sequence_number();
                if (try_wm_copy(options, attempt, api) &&
                    wait_for_clipboard_change(sequence, options.clipboard_timeout, options.clipboard_poll_interval, api))
                {
                    result.status = copy_status::copied;
                    result.method = copy_method::wm_copy;
                    return result;
                }
            }

            const DWORD sequence = api.get_clipboard_sequence_number();
            attempt.ctrl_c_attempted = true;
            attempt.ctrl_c = input_injection::send_chord(ctrl_c_chord(), options.chord, api.input);
            if (attempt.ctrl_c.succeeded() &&
                wait_for_clipboard_change(sequence, options.clipboard_timeout, options.clipboard_poll_interval, api))
            {
                result.status = copy_status::copied;
                result.method = copy_method::ctrl_c;
                return result;
            }
        }

        result.status = result.attempts.back().ctrl_c.succeeded() ? copy_status::timed_out : copy_status::injection_failed;
        return result;
    }
}
