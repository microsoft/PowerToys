#pragma once

#include <windows.h>
#include <appmodel.h>

#include <array>
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace ptap
{
    inline constexpr wchar_t PackageName[] = L"Microsoft.PowerToys.PtAliasProto";
    inline constexpr wchar_t PackagePublisher[] = L"CN=PowerToys PtAliasProto Test";
    inline constexpr wchar_t AliasName[] = L"PtAliasProtoWorker.exe";
    inline constexpr wchar_t StoreRootName[] = L"Microsoft\\PowerToys\\PtAliasProto";
    inline constexpr uint32_t StateMagic = 0x50544150;
    inline constexpr uint32_t ProtocolMagic = 0x50415450;
    inline constexpr uint16_t ProtocolVersion = 1;
    inline constexpr DWORD MaxProtocolPayload = 1024;
    inline constexpr DWORD WorkerReadyTimeoutMs = 20000;
    inline constexpr DWORD WorkerStopTimeoutMs = 10000;

    class win32_error : public std::runtime_error
    {
    public:
        win32_error(const char* operation, DWORD error);
        [[nodiscard]] DWORD code() const noexcept;

    private:
        DWORD m_code;
    };

    void check_bool(BOOL result, const char* operation);
    void check_lstatus(LSTATUS result, const char* operation);

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
        [[nodiscard]] void* release() noexcept;

    private:
        void* m_value{};
    };

    class secret_buffer
    {
    public:
        secret_buffer() = default;
        explicit secret_buffer(size_t characters);
        ~secret_buffer();
        secret_buffer(const secret_buffer&) = delete;
        secret_buffer& operator=(const secret_buffer&) = delete;
        secret_buffer(secret_buffer&& other) noexcept;
        secret_buffer& operator=(secret_buffer&& other) noexcept;
        [[nodiscard]] wchar_t* data() noexcept;
        [[nodiscard]] const wchar_t* data() const noexcept;
        [[nodiscard]] size_t size() const noexcept;

    private:
        std::vector<wchar_t> m_value;
    };

    struct PackageVersion
    {
        uint16_t major{};
        uint16_t minor{};
        uint16_t build{};
        uint16_t revision{};
    };

    struct PackageIdentity
    {
        std::wstring fullName;
        std::wstring familyName;
        std::wstring publisherId;
        PackageVersion version;
        UINT32 architecture{};
    };

#pragma pack(push, 1)
    struct PrototypeState
    {
        uint32_t magic{ StateMagic };
        uint32_t formatVersion{ 1 };
        wchar_t ownerSid[192]{};
        wchar_t accountName[32]{};
        wchar_t accountSid[192]{};
        wchar_t serviceSid[192]{};
        wchar_t serviceName[64]{};
        wchar_t desiredPackageFullName[256]{};
        wchar_t lastGoodPackageFullName[256]{};
        uint64_t stateGeneration{};
        uint32_t lastWorkerPid{};
        uint32_t lastWin32Error{};
    };

    struct RequestHeader
    {
        uint32_t magic{ ProtocolMagic };
        uint16_t version{ ProtocolVersion };
        uint16_t command{};
        uint32_t requestId{};
        uint32_t payloadBytes{};
    };

    struct ReplyHeader
    {
        uint32_t magic{ ProtocolMagic };
        uint16_t version{ ProtocolVersion };
        uint16_t command{};
        uint32_t requestId{};
        uint32_t win32Status{};
        uint32_t payloadBytes{};
    };

    struct StatusPayload
    {
        uint32_t scmState{};
        uint32_t workerPid{};
        uint32_t lastWin32Error{};
        uint32_t desiredVersion{};
        uint32_t lastGoodVersion{};
        wchar_t packageFullName[256]{};
    };

    struct EvidenceRecord
    {
        uint32_t magic{ StateMagic };
        uint32_t formatVersion{ 2 };
        uint64_t launchCount{};
        uint32_t processId{};
        uint32_t sessionId{};
        uint32_t hasExpectedServiceSid{};
        wchar_t packageFullName[256]{};
        wchar_t packageFamilyName[128]{};
        wchar_t userSid[192]{};
        wchar_t serviceSid[192]{};
    };
#pragma pack(pop)

    enum class Command : uint16_t
    {
        Status = 1,
        EnsurePackage = 2,
        StopWorker = 3,
        CleanupRegistration = 4,
    };

    struct InstanceNames
    {
        std::wstring suffix;
        std::wstring accountName;
        std::wstring serviceName;
        std::wstring pipeName;
        std::filesystem::path storeDirectory;
        std::filesystem::path statePath;
        std::filesystem::path evidencePath;
        std::filesystem::path launcherDirectory;
        std::filesystem::path launcherPath;
    };

    [[nodiscard]] std::wstring format_error(DWORD error);
    [[nodiscard]] std::wstring current_token_user_sid();
    [[nodiscard]] std::wstring token_user_sid(HANDLE token);
    [[nodiscard]] bool token_contains_sid(HANDLE token, std::wstring_view sid);
    [[nodiscard]] bool token_is_administrator(HANDLE token);
    [[nodiscard]] std::wstring sid_for_account(std::wstring_view account);
    [[nodiscard]] std::wstring service_sid(std::wstring_view serviceName);
    [[nodiscard]] std::wstring owner_hash(std::wstring_view ownerSid);
    [[nodiscard]] InstanceNames instance_names(std::wstring_view ownerSid);
    [[nodiscard]] std::wstring expected_package_family_name();
    [[nodiscard]] PackageIdentity validate_package_full_name(std::wstring_view fullName);
    [[nodiscard]] uint64_t version_value(const PackageVersion& version) noexcept;
    [[nodiscard]] uint32_t compact_version(const PackageVersion& version) noexcept;
    [[nodiscard]] bool is_allowed_version(const PackageVersion& version) noexcept;
    [[nodiscard]] bool is_package_staged(std::wstring_view fullName);
    [[nodiscard]] std::filesystem::path current_local_app_data();
    [[nodiscard]] std::filesystem::path alias_path();
    [[nodiscard]] PrototypeState read_state(const std::filesystem::path& path);
    void write_state_atomic(const std::filesystem::path& path, PrototypeState state);
    void write_evidence_atomic(const std::filesystem::path& path, const EvidenceRecord& evidence);
    [[nodiscard]] EvidenceRecord read_evidence(const std::filesystem::path& path);
    void append_log(const std::filesystem::path& storeDirectory, std::wstring_view component, std::wstring_view message) noexcept;
    void set_protected_directory_acl(
        const std::filesystem::path& path,
        std::wstring_view serviceAccountSid,
        std::wstring_view ownerSid,
        bool ownerReadOnly,
        bool serviceAccountFullControl);
    void set_protected_root_acl(const std::filesystem::path& path);
    [[nodiscard]] local_memory security_descriptor_from_sddl(const std::wstring& sddl);
    [[nodiscard]] std::wstring quote_argument(std::wstring_view value);
    [[nodiscard]] std::vector<std::wstring> command_line_arguments();
    [[nodiscard]] std::wstring argument_value(const std::vector<std::wstring>& args, std::wstring_view name);
    [[nodiscard]] bool has_argument(const std::vector<std::wstring>& args, std::wstring_view name);
    void copy_bounded(wchar_t* destination, size_t destinationCount, std::wstring_view source);
    [[nodiscard]] std::wstring bounded_string(const wchar_t* source, size_t sourceCount);
    [[nodiscard]] uint64_t increment_launch_count(const std::filesystem::path& storeDirectory);
    [[nodiscard]] std::wstring make_nonce();
}
