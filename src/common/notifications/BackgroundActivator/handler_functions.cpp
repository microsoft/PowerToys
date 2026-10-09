#include "pch.h"

#include "handler_functions.h"

#include <winrt/Windows.System.h>

using handler_function_t = void (*)(const size_t button_id);

namespace
{
    const std::unordered_map<std::wstring_view, handler_function_t> handlers_map;
}

void dispatch_to_background_handler(std::wstring_view argument)
{
    winrt::Windows::Foundation::WwwFormUrlDecoder decoder{ argument };

    const size_t button_id = std::stoi(decoder.GetFirstValueByName(L"button_id").c_str());
    auto handler = decoder.GetFirstValueByName(L"handler");

    const auto found_handler = handlers_map.find(handler);
    if (found_handler == end(handlers_map))
    {
        return;
    }
    found_handler->second(button_id);
}
