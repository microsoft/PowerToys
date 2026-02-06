#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>

// Implementations in resources.cpp
std::wstring get_english_fallback_string(UINT resource_id, HINSTANCE instance);
std::wstring get_resource_string_language_override(UINT resource_id, HINSTANCE instance);
std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback);

extern "C" IMAGE_DOS_HEADER __ImageBase;
// Wrapper for getting a string from the resource file. Returns the resource id text when fails.
#define GET_RESOURCE_STRING(resource_id) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), L#resource_id)
#define GET_RESOURCE_STRING_FALLBACK(resource_id, fallback) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), fallback)
