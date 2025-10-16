#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>

#include <string>
#include <thread>

// Get the executable path or module name for modern apps
std::wstring get_process_path(DWORD pid) noexcept;

// Get the executable path or module name for modern apps
std::wstring get_process_path(HWND window) noexcept;

std::wstring get_process_path_waiting_uwp(HWND window);

std::wstring get_module_filename(HMODULE mod = nullptr);

std::wstring get_module_folderpath(HMODULE mod = nullptr, bool removeFilename = true);
