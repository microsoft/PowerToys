#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <dwmapi.h>

#include <array>
#include <optional>
#include <functional>
#include <unordered_map>

// Initializes and runs windows message loop
int run_message_loop(bool until_idle = false,
                     std::optional<uint32_t> timeout_ms = {},
                     std::unordered_map<DWORD, std::function<void()>> wm_app_msg_callbacks = {});

// Check if window is part of the shell or the taskbar.
bool is_system_window(HWND hwnd, const char* class_name);

template<typename T>
inline T GetWindowCreateParam(LPARAM lparam)
{
    static_assert(sizeof(T) <= sizeof(void*));
    T data{ static_cast<T>(reinterpret_cast<CREATESTRUCT*>(lparam)->lpCreateParams) };
    return data;
}

template<typename T>
inline void StoreWindowParam(HWND window, T data)
{
    static_assert(sizeof(T) <= sizeof(void*));
    SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(data));
}

template<typename T>
inline T GetWindowParam(HWND window)
{
    return reinterpret_cast<T>(GetWindowLongPtrW(window, GWLP_USERDATA));
}
