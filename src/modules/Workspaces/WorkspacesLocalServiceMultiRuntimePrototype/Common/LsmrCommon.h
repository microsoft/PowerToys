#pragma once

#include <windows.h>

#include <filesystem>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

#ifndef PT_UPDATER_VERSION_MAJOR
#define PT_UPDATER_VERSION_MAJOR 5
#endif

namespace ptlsmr
{
    inline constexpr wchar_t UpdaterPackageName[] =
        L"Microsoft.PowerToys.WsPuvr.RawUpdater";
    inline constexpr wchar_t RuntimePackageNameTrack1[] =
        L"Microsoft.PowerToys.WsPuvr.Runtime1";
    inline constexpr wchar_t RuntimePackageNameTrack2[] =
        L"Microsoft.PowerToys.WsPuvr.Runtime2";
    inline constexpr wchar_t PackagePublisher[] =
        L"CN=PowerToys Workspaces Packaged Payload Updater Virtual Runtime Prototype Test";
    inline constexpr wchar_t RuntimeExe[] = L"PtPuvrRuntime.exe";
    inline constexpr wchar_t UpdaterExe[] = L"PtPuvrUpdater.exe";
    inline constexpr wchar_t UpdaterServiceName[] = L"PtPuvrUpdater";
    inline constexpr wchar_t UpdaterPipeName[] = L"\\\\.\\pipe\\PtPuvrUpdater";
    inline constexpr wchar_t StoreRelativeRoot[] =
        L"Microsoft\\PowerToys\\WorkspacesPackagedPayloadUpdaterVirtualRuntimePrototype";
#if PT_UPDATER_VERSION_MAJOR == 5
    inline constexpr wchar_t UpdaterVersion[] = L"5.0.0.0";
#elif PT_UPDATER_VERSION_MAJOR == 6
    inline constexpr wchar_t UpdaterVersion[] = L"6.0.0.0";
#else
#error Unsupported PT_UPDATER_VERSION_MAJOR
#endif
    inline constexpr uint32_t ProtocolMagic = 0x52565550; // PUVR
    inline constexpr uint16_t ProtocolVersion = 2;
    inline constexpr size_t MaxOwnerSidChars = 192;
    inline constexpr size_t MaxPackagePathChars = 1024;

    class win32_error : public std::runtime_error
    {
    public:
        win32_error(const char* operation, DWORD error);
        [[nodiscard]] DWORD code() const noexcept;

    private:
        DWORD m_code;
    };

    class unique_handle
    {
    public:
        unique_handle() noexcept = default;
        explicit unique_handle(HANDLE value) noexcept;
        ~unique_handle();
        unique_handle(const unique_handle&) = delete;
        unique_handle& operator=(const unique_handle&) = delete;
        unique_handle(unique_handle&& other) noexcept;
        unique_handle& operator=(unique_handle&& other) noexcept;
        [[nodiscard]] HANDLE get() const noexcept;
        [[nodiscard]] HANDLE release() noexcept;
        void reset(HANDLE value = nullptr) noexcept;
        explicit operator bool() const noexcept;

    private:
        HANDLE m_value{};
    };

    class local_memory
    {
    public:
        local_memory() noexcept = default;
        explicit local_memory(void* value) noexcept;
        ~local_memory();
        local_memory(const local_memory&) = delete;
        local_memory& operator=(const local_memory&) = delete;
        local_memory(local_memory&& other) noexcept;
        local_memory& operator=(local_memory&& other) noexcept;
        [[nodiscard]] void* get() const noexcept;

    private:
        void* m_value{};
    };

    struct InstanceNames
    {
        std::wstring ownerSid;
        std::wstring suffix;
        std::wstring serviceName;
        std::filesystem::path storeDirectory;
        std::filesystem::path evidencePath;
    };

    void check_bool(BOOL result, const char* operation);
    [[nodiscard]] std::wstring current_token_user_sid(HANDLE token = nullptr);
    [[nodiscard]] bool token_contains_sid(HANDLE token, std::wstring_view sid);
    [[nodiscard]] bool token_is_administrator(HANDLE token);
    [[nodiscard]] std::wstring canonical_owner_sid(std::wstring_view value);
    [[nodiscard]] InstanceNames instance_names(std::wstring_view ownerSid);
    [[nodiscard]] std::wstring service_sid(std::wstring_view serviceName);
    [[nodiscard]] std::filesystem::path program_data_root();
    [[nodiscard]] std::wstring expected_updater_package_full_name(uint16_t major);
    [[nodiscard]] std::wstring expected_updater_package_family_name();
    [[nodiscard]] bool is_allowed_updater_package_full_name(std::wstring_view value);
    [[nodiscard]] std::filesystem::path updater_package_directory(uint16_t major);
    [[nodiscard]] std::filesystem::path staged_package_directory(
        std::wstring_view packageFullName);
    [[nodiscard]] std::wstring file_uri(const std::filesystem::path& path);
    [[nodiscard]] std::wstring runtime_package_name(uint16_t track);
    [[nodiscard]] std::wstring expected_runtime_package_full_name(uint16_t track);
    [[nodiscard]] std::wstring expected_runtime_package_family_name(uint16_t track);
    [[nodiscard]] bool is_allowed_runtime_package_full_name(std::wstring_view value);
    [[nodiscard]] uint16_t runtime_track_from_package_full_name(std::wstring_view fullName);
    [[nodiscard]] std::wstring package_version_string(std::wstring_view fullName);
    [[nodiscard]] std::wstring quote_argument(std::wstring_view value);
    [[nodiscard]] std::vector<std::wstring> command_line_arguments();
    [[nodiscard]] std::wstring argument_value(
        const std::vector<std::wstring>& arguments,
        std::wstring_view name);
    void protect_directory_for_service(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid);
    void protect_system_directory(const std::filesystem::path& directory);
    void write_utf8_file_atomic(const std::filesystem::path& path, std::wstring_view value);
    [[nodiscard]] std::wstring read_utf8_file(const std::filesystem::path& path, size_t maximumBytes);
}
