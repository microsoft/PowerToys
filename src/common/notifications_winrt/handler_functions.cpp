#include "pch.h"

#include "handler_functions.h"

using handler_function_t = void (*)(IBackgroundTaskInstance, const size_t button_id);

namespace
{
    const std::unordered_map<std::wstring_view, handler_function_t> handlers_map;
}

void dispatch_to_backround_handler(std::wstring_view background_handler_id, IBackgroundTaskInstance bti, const size_t button_id)
{
    const auto found_handler = handlers_map.find(background_handler_id);
    if (found_handler == end(handlers_map))
    {
        return;
    }
    found_handler->second(std::move(bti), button_id);
}
