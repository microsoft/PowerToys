#pragma once

#include <string_view>
#include <vector>

namespace notifications
{
    void register_background_toast_handler();

    // Make sure your plaintext_message argument is properly XML-escaped
    void show_toast(std::wstring_view plaintext_message);
    void show_toast_background_activated(std::wstring_view plaintext_message, std::wstring_view background_handler_id, std::vector<std::wstring_view> plaintext_button_labels);
}
