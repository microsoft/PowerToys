#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>

// Get a string from the resource file
inline std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback)
{
    wchar_t* text_ptr;
    auto length = LoadStringW(instance, resource_id, reinterpret_cast<wchar_t*>(&text_ptr), 0);
    if (length == 0)
    {
        return fallback;
    }
    else
    {
        return { text_ptr, static_cast<std::size_t>(length) };
    }
}

extern "C" IMAGE_DOS_HEADER __ImageBase;
// Wrapper for getting a string from the resource file. Returns the resource id text when fails.
#define GET_RESOURCE_STRING(resource_id) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), L#resource_id)
