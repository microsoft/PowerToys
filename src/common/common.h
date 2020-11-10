#pragma once
#include <optional>
#include <string>
#include <Windows.h>
#include <string>
#include <memory>
#include <vector>


// Gets position of given window.
std::optional<RECT> get_window_pos(HWND hwnd);

// Check if window is part of the shell or the taskbar.
bool is_system_window(HWND hwnd, const char* class_name);

// Initializes and runs windows message loop
int run_message_loop(const bool until_idle = false, const std::optional<uint32_t> timeout_seconds = {});

std::optional<std::wstring> get_last_error_message(const DWORD dw);
void show_last_error_message(LPCWSTR lpszFunction, DWORD dw, LPCWSTR errorTitle);

enum WindowState
{
    UNKNOWN,
    MINIMIZED,
    MAXIMIZED,
    SNAPED_TOP_LEFT,
    SNAPED_LEFT,
    SNAPED_BOTTOM_LEFT,
    SNAPED_TOP_RIGHT,
    SNAPED_RIGHT,
    SNAPED_BOTTOM_RIGHT,
    RESTORED
};
WindowState get_window_state(HWND hwnd);

// Returns true if the current process is running with elevated privileges
bool is_process_elevated(const bool use_cached_value = true);

// Drops the elevated privileges if present
bool drop_elevated_privileges();

// Run command as elevated user, returns true if succeeded
HANDLE run_elevated(const std::wstring& file, const std::wstring& params);

// Run command as non-elevated user, returns true if succeeded, puts the process id into returnPid if returnPid != NULL
bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid);

// Run command with the same elevation, returns true if succeeded
bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid);

// Returns true if the current process is running from administrator account
bool check_user_is_admin();

// Returns true when one or more strings from vector found in string
bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what);

// Get the executable path or module name for modern apps
std::wstring get_process_path(DWORD pid) noexcept;
// Get the executable path or module name for modern apps
std::wstring get_process_path(HWND hwnd) noexcept;

std::wstring get_product_version();

std::wstring get_module_filename(HMODULE mod = nullptr);
std::wstring get_module_folderpath(HMODULE mod = nullptr, const bool removeFilename = true);

// Get a string from the resource file
std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback);
// Wrapper for getting a string from the resource file. Returns the resource id text when fails.
// Requires that
//  extern "C" IMAGE_DOS_HEADER __ImageBase;
// is added to the .cpp file.
#define GET_RESOURCE_STRING(resource_id) get_resource_string(resource_id, reinterpret_cast<HINSTANCE>(&__ImageBase), L#resource_id)

std::optional<std::string> exec_and_read_output(const std::wstring_view command, DWORD timeout_ms = 30000);

// Helper class for various COM-related APIs, e.g working with security descriptors
template<typename T>
struct typed_storage
{
    std::unique_ptr<char[]> _buffer;
    inline explicit typed_storage(const DWORD size) :
        _buffer{ std::make_unique<char[]>(size) }
    {
    }
    inline operator T*()
    {
        return reinterpret_cast<T*>(_buffer.get());
    }
};

template<class... Ts>
struct overloaded : Ts...
{
    using Ts::operator()...;
};
template<class... Ts>
overloaded(Ts...) -> overloaded<Ts...>;
