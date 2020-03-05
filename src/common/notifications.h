#pragma once

#include <string_view>
#include <vector>
#include <variant>

namespace notifications
{
    constexpr inline const wchar_t TOAST_ACTIVATED_LAUNCH_ARG[] = L"-ToastActivated";

    void register_background_toast_handler();

    void run_desktop_app_activator_loop();

    struct link_button
    {
        std::wstring_view label;
        std::wstring_view url;
    };

    struct background_activated_button
    {
        std::wstring_view label;
    };

    using button_t = std::variant<link_button, background_activated_button>;

    void show_toast(std::wstring_view plaintext_message);
    void show_toast_with_activations(std::wstring_view plaintext_message, std::wstring_view background_handler_id, std::vector<button_t> buttons);
}
