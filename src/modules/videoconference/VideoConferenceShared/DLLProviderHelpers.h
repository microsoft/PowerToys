#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <optional>
#include <functional>
#include <type_traits>

#define DECLARE_DLL_FUNCTION(NAME) \
    std::function<decltype(::NAME)> NAME = (std::add_pointer_t<decltype(::NAME)>)GetProcAddress(_library_handle, #NAME);

#define DECLARE_DLL_PROVIDER_BEGIN(DLL_NAME)                       \
    class DLL_NAME##APIProvider final                              \
    {                                                              \
        HMODULE _library_handle;                                   \
        DLL_NAME##APIProvider(HMODULE h) : _library_handle{ h } {} \
                                                                   \
    public:                                                        \
        ~DLL_NAME##APIProvider() { FreeLibrary(_library_handle); } \
        static std::optional<DLL_NAME##APIProvider> create()       \
        {                                                          \
            HMODULE h = LoadLibraryA(#DLL_NAME ".dll");            \
            std::optional<DLL_NAME##APIProvider> result;           \
            if (!h)                                                \
                return result;                                     \
            result.emplace(DLL_NAME##APIProvider{ h });            \
            return result;                                         \
        }

#define DECLARE_DLL_PROVIDER_END \
    }                           \
    ;
