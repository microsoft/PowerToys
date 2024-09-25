#pragma once

#include "pch.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void EventToggleOnOff(_In_ const bool new_enabled_state) noexcept;
    static void EventChangedTemplateLocation() noexcept;
    static void EventShowTemplateItems(_In_ const size_t number_of_templates) noexcept;
    static void EventCopyTemplate(_In_ const std::wstring template_file_extension) noexcept;
    static void EventCopyTemplateResult(_In_ const HRESULT hr) noexcept;
};
