#pragma once

#include <windows.h>

#include <filesystem>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace ptlsmr
{
    inline constexpr wchar_t PrototypeCompanyName[] = L"Microsoft Corporation";
    inline constexpr wchar_t UpdaterProductName[] =
        L"PowerToys Workspaces protected runtime updater prototype";
    inline constexpr wchar_t RuntimeProductName[] =
        L"PowerToys Workspaces protected runtime prototype";
    inline constexpr wchar_t RuntimeExe[] = L"PtPuvrRuntime.exe";
    inline constexpr wchar_t UpdaterExe[] = L"PtPuvrUpdater.exe";
    inline constexpr wchar_t UpdaterServiceName[] = L"PtPuvrUpdater";
    inline constexpr wchar_t UpdaterPipeName[] = L"\\\\.\\pipe\\PtPuvrUpdater";
    inline constexpr wchar_t TrustedSignerPinFile[] = L"trusted-signer-sha256.txt";
    inline constexpr wchar_t StoreRelativeRoot[] =
        L"Microsoft\\PowerToys\\WorkspacesProtectedRuntimeUpdaterPrototype";
    inline constexpr wchar_t InstallRelativeRoot[] =
        L"PowerToys\\WorkspacesProtectedRuntimeUpdaterPrototype";
    inline constexpr wchar_t UpdaterVersion[] = L"5.0.0.0";
    inline constexpr uint32_t ProtocolMagic = 0x52565550; // PUVR
    inline constexpr uint16_t ProtocolVersion = 3;
    inline constexpr size_t MaxOwnerSidChars = 192;
    inline constexpr size_t MaxCandidatePathChars = 1024;
    inline constexpr size_t MaxCrashPhaseChars = 48;

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

    struct file_version
    {
        uint16_t major{};
        uint16_t minor{};
        uint16_t build{};
        uint16_t revision{};
    };

    [[nodiscard]] bool operator==(const file_version& left, const file_version& right) noexcept;
    [[nodiscard]] bool operator<(const file_version& left, const file_version& right) noexcept;
    [[nodiscard]] std::wstring format_version(const file_version& value);
    [[nodiscard]] file_version parse_version(std::wstring_view value);

    struct InstanceNames
    {
        std::wstring ownerSid;
        std::wstring suffix;
        std::wstring serviceName;
        std::filesystem::path storeDirectory;
        std::filesystem::path evidencePath;
    };

    enum class command : uint16_t
    {
        provision = 1,
        status = 2,
        cleanup = 3,
    };

#pragma pack(push, 1)
    struct request
    {
        uint32_t magic{};
        uint16_t version{};
        uint16_t command{};
        uint16_t runtimeTrack{};
        uint16_t reserved{};
        wchar_t ownerSid[MaxOwnerSidChars]{};
        wchar_t candidatePath[MaxCandidatePathChars]{};
        wchar_t crashPhase[MaxCrashPhaseChars]{};
    };

    struct reply
    {
        uint32_t magic{ ProtocolMagic };
        uint16_t version{ ProtocolVersion };
        uint16_t command{};
        uint32_t win32Status{};
        uint32_t scmState{};
        uint32_t processId{};
        uint32_t serviceExit{};
        wchar_t runtimeVersion[64]{};
        wchar_t detail[2048]{};
    };
#pragma pack(pop)

    void check_bool(BOOL result, const char* operation);
    [[nodiscard]] std::wstring current_token_user_sid(HANDLE token = nullptr);
    [[nodiscard]] bool token_contains_sid(HANDLE token, std::wstring_view sid);
    [[nodiscard]] bool token_is_administrator(HANDLE token);
    [[nodiscard]] std::wstring canonical_owner_sid(std::wstring_view value);
    [[nodiscard]] InstanceNames instance_names(std::wstring_view ownerSid);
    [[nodiscard]] std::wstring service_sid(std::wstring_view serviceName);
    [[nodiscard]] std::filesystem::path program_data_root();
    [[nodiscard]] std::filesystem::path installation_root();
    [[nodiscard]] std::filesystem::path updater_install_directory(
        const file_version& version);
    [[nodiscard]] std::filesystem::path runtime_root();
    [[nodiscard]] std::filesystem::path runtime_install_directory(
        uint16_t track,
        const file_version& version);
    [[nodiscard]] std::filesystem::path runtime_executable_path(
        uint16_t track,
        const file_version& version);
    [[nodiscard]] std::filesystem::path trusted_signer_pin_path();
    [[nodiscard]] bool path_is_within(
        const std::filesystem::path& child,
        const std::filesystem::path& parent);
    [[nodiscard]] std::wstring quote_argument(std::wstring_view value);
    [[nodiscard]] std::vector<std::wstring> command_line_arguments();
    [[nodiscard]] std::wstring argument_value(
        const std::vector<std::wstring>& arguments,
        std::wstring_view name);
    [[nodiscard]] bool has_argument(
        const std::vector<std::wstring>& arguments,
        std::wstring_view name);
    [[nodiscard]] std::wstring canonical_signer_sha256(std::wstring_view value);
    [[nodiscard]] std::wstring read_trusted_signer_pin();
    void write_trusted_signer_pin(std::wstring_view value);
    [[nodiscard]] DWORD require_no_package_identity();

    void protect_system_directory(const std::filesystem::path& directory);
    void protect_runtime_directory(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid = L"");
    void protect_directory_for_service(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid);
    [[nodiscard]] std::filesystem::path create_protected_staging_directory(
        const std::filesystem::path& parent,
        std::wstring_view prefix);
    void copy_file_to_protected_stage(
        const std::filesystem::path& source,
        const std::filesystem::path& stagedFile);
    void move_file_atomically(
        const std::filesystem::path& source,
        const std::filesystem::path& destination);
    [[nodiscard]] bool files_are_identical(
        const std::filesystem::path& first,
        const std::filesystem::path& second);
    void write_utf8_file_atomic(const std::filesystem::path& path, std::wstring_view value);
    [[nodiscard]] std::wstring read_utf8_file(const std::filesystem::path& path, size_t maximumBytes);

    [[nodiscard]] file_version validate_updater_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_runtime_candidate(
        const std::filesystem::path& path,
        uint16_t expectedTrack,
        std::wstring_view expectedSignerPin);
}
