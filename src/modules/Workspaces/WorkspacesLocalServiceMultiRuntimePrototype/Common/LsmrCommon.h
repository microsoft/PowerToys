#pragma once

#include "../../../../common/protected_runtime/ProtectedRuntimeControlProtocol.h"

#include <windows.h>

#include <filesystem>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace ptlsmr
{
    inline constexpr wchar_t PrototypeCompanyName[] = L"Microsoft Corporation";
    inline constexpr wchar_t HostProductName[] =
        L"PowerToys Workspaces protected runtime control-plane host prototype";
    inline constexpr wchar_t EngineProductName[] =
        L"PowerToys Workspaces protected runtime updater engine prototype";
    inline constexpr wchar_t RuntimeProductName[] =
        L"PowerToys Workspaces protected runtime prototype";
    inline constexpr wchar_t MetadataProductName[] =
        L"PowerToys Workspaces protected runtime release metadata prototype";
    inline constexpr wchar_t PolicyProductName[] =
        L"PowerToys Workspaces protected runtime control-plane policy prototype";
    inline constexpr wchar_t RuntimeExe[] = L"PtPuvrRuntime.exe";
    inline constexpr wchar_t EngineExe[] = L"PtPuvrUpdater.exe";
    inline constexpr wchar_t HostExe[] = L"PtPuvrHost.exe";
    inline constexpr wchar_t ReleaseManifestExe[] = L"PtPuvrReleaseManifest.exe";
    inline constexpr wchar_t CodePolicyExe[] = L"PtPuvrCodePolicy.exe";
    inline constexpr wchar_t MetadataPolicyExe[] = L"PtPuvrMetadataPolicy.exe";
    inline constexpr const wchar_t* HostServiceName =
        powertoys::protected_runtime::protocol::host_service_name;
    inline constexpr const wchar_t* HostPipePrefix =
        powertoys::protected_runtime::protocol::host_pipe_prefix;
    inline constexpr const wchar_t* ControlPlaneRegistryKey =
        powertoys::protected_runtime::protocol::control_plane_registry_key;
    inline constexpr wchar_t CleanupOutcomeRegistryKey[] =
        L"SOFTWARE\\Microsoft\\PowerToys\\WorkspacesProtectedRuntimeControlPlanePrototypeValidation";
    inline constexpr const wchar_t* HostEndpointRegistryValue =
        powertoys::protected_runtime::protocol::host_endpoint_registry_value;
    inline constexpr wchar_t StateInitializedRegistryValue[] = L"StateInitialized";
    inline constexpr wchar_t CleanupNonceRegistryValue[] = L"CleanupRunNonce";
    inline constexpr wchar_t CleanupTimestampRegistryValue[] = L"CleanupTimestampFileTimeUtc";
    inline constexpr wchar_t CleanupStatusRegistryValue[] = L"CleanupWin32Status";
    inline constexpr wchar_t CleanupStageRegistryValue[] = L"CleanupStage";
    inline constexpr wchar_t CodeSignerPinFile[] = L"code-signer-sha256.txt";
    inline constexpr wchar_t MetadataSignerPinFile[] = L"metadata-signer-sha256.txt";
    inline constexpr wchar_t ActiveEngineFile[] = L"active-engine.txt";
    inline constexpr wchar_t EngineActivationJournalFile[] =
        L"engine-activation-journal.txt";
    inline constexpr wchar_t AcceptedReleaseStateFile[] =
        L"accepted-release-state.txt";
    inline constexpr wchar_t AcquisitionJournalFile[] =
        L"acquisition-transaction.txt";
    inline constexpr wchar_t LeaseStateFile[] = L"leases.txt";
    inline constexpr wchar_t StoreRelativeRoot[] =
        L"Microsoft\\PowerToys\\WorkspacesProtectedRuntimeControlPlanePrototype";
    inline constexpr wchar_t InstallRelativeRoot[] =
        L"PowerToys\\WorkspacesProtectedRuntimeControlPlanePrototype";
    inline constexpr wchar_t HostVersion[] = L"5.0.0.0";
    inline constexpr wchar_t InitialEngineVersion[] = L"5.0.0.0";
    // Legacy controller-only names remain source-compatible for teardown diagnostics.
    inline constexpr const wchar_t* UpdaterExe = EngineExe;
    inline constexpr wchar_t UpdaterServiceName[] = L"PtPuvrLegacyEngineDiagnostics";
    inline constexpr wchar_t UpdaterPipeName[] = L"\\\\.\\pipe\\PtPuvrLegacyEngineDiagnostics";
    inline constexpr const wchar_t* UpdaterVersion = InitialEngineVersion;
    inline constexpr uint32_t ProtocolMagic =
        powertoys::protected_runtime::protocol::magic;
    inline constexpr uint32_t PipeAuthenticationMagic =
        powertoys::protected_runtime::protocol::authentication_magic;
    inline constexpr uint16_t ProtocolVersion =
        powertoys::protected_runtime::protocol::version;
    inline constexpr size_t MaxOwnerSidChars = 192;
    inline constexpr size_t MaxCandidatePathChars = 1024;
    inline constexpr size_t MaxCrashPhaseChars = 48;
    inline constexpr size_t MaxReleaseIdChars =
        powertoys::protected_runtime::protocol::max_release_id_chars;
    inline constexpr size_t TransactionIdChars = 32;
    inline constexpr size_t MaxLeases = 32;
    inline constexpr uint64_t MaxReleaseManifestBytes = 1024ull * 1024ull;
    inline constexpr uint64_t MaxRuntimeArtifactBytes = 64ull * 1024ull * 1024ull;
    inline constexpr uint64_t MaxEngineArtifactBytes = 64ull * 1024ull * 1024ull;

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

    using public_command =
        powertoys::protected_runtime::protocol::control_command;

    enum class engine_action : uint16_t
    {
        complete = 0,
        activate_engine = 1,
    };

#pragma pack(push, 1)
    using pipe_authentication_preface =
        powertoys::protected_runtime::protocol::authentication_preface;

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
        wchar_t transactionId[TransactionIdChars + 1]{};
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

    using public_request =
        powertoys::protected_runtime::protocol::control_request;
    using public_reply =
        powertoys::protected_runtime::protocol::control_reply;

    struct engine_request
    {
        uint32_t magic{};
        uint16_t version{};
        uint16_t command{};
        uint16_t reserved{};
        wchar_t ownerSid[MaxOwnerSidChars]{};
        wchar_t releaseId[MaxReleaseIdChars]{};
        wchar_t inboxPath[MaxCandidatePathChars]{};
    };

    struct engine_reply
    {
        uint32_t magic{ ProtocolMagic };
        uint16_t version{ ProtocolVersion };
        uint16_t command{};
        uint16_t action{};
        uint32_t win32Status{};
        uint32_t scmState{};
        uint32_t processId{};
        uint32_t leaseCount{};
        wchar_t runtimeVersion[64]{};
        wchar_t activeEngineVersion[64]{};
        wchar_t candidateEngineVersion[64]{};
        wchar_t candidateEnginePath[MaxCandidatePathChars]{};
        wchar_t engineCrashPhase[MaxCrashPhaseChars]{};
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
    [[nodiscard]] std::filesystem::path host_executable_path();
    [[nodiscard]] std::filesystem::path engine_root();
    [[nodiscard]] std::filesystem::path engine_install_directory(
        const file_version& version);
    [[nodiscard]] std::filesystem::path engine_executable_path(
        const file_version& version);
    [[nodiscard]] std::filesystem::path updater_install_directory(
        const file_version& version);
    [[nodiscard]] std::filesystem::path runtime_root();
    [[nodiscard]] std::filesystem::path runtime_install_directory(
        uint16_t track,
        const file_version& version);
    [[nodiscard]] std::filesystem::path runtime_executable_path(
        uint16_t track,
        const file_version& version);
    [[nodiscard]] std::filesystem::path code_signer_pin_path();
    [[nodiscard]] std::filesystem::path metadata_signer_pin_path();
    [[nodiscard]] std::filesystem::path policy_directory();
    [[nodiscard]] std::filesystem::path code_policy_path();
    [[nodiscard]] std::filesystem::path metadata_policy_path();
    [[nodiscard]] std::filesystem::path engine_state_path();
    [[nodiscard]] std::filesystem::path engine_activation_journal_path();
    [[nodiscard]] std::filesystem::path accepted_release_state_path();
    [[nodiscard]] std::filesystem::path acquisition_journal_path();
    [[nodiscard]] std::filesystem::path lease_state_path();
    [[nodiscard]] std::filesystem::path requests_root();
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
    [[nodiscard]] std::wstring read_code_signer_pin();
    [[nodiscard]] std::wstring read_metadata_signer_pin();
    [[nodiscard]] std::wstring read_trusted_signer_pin();
    void write_trusted_signer_pin(std::wstring_view value);
    [[nodiscard]] std::wstring sha256_text(std::wstring_view value);
    [[nodiscard]] std::wstring sha256_file(const std::filesystem::path& path);
    [[nodiscard]] std::wstring random_hex_identifier(size_t bytes);
    [[nodiscard]] std::wstring read_rcdata_text(
        const std::filesystem::path& path,
        std::wstring_view resourceName,
        size_t maximumBytes);
    [[nodiscard]] std::filesystem::path token_local_app_data(HANDLE token);
    [[nodiscard]] std::filesystem::path raw_process_image_path(HANDLE process);
    [[nodiscard]] std::filesystem::path raw_process_image_path(DWORD processId);
    [[nodiscard]] DWORD require_no_package_identity();

    void protect_system_directory(const std::filesystem::path& directory);
    void protect_system_file(const std::filesystem::path& file);
    void protect_runtime_directory(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid = L"");
    void protect_directory_for_service(
        const std::filesystem::path& directory,
        std::wstring_view serviceSid);
    [[nodiscard]] std::filesystem::path create_protected_staging_directory(
        const std::filesystem::path& parent,
        std::wstring_view prefix);
    [[nodiscard]] uint64_t copy_file_to_protected_stage(
        const std::filesystem::path& source,
        const std::filesystem::path& expectedSourceRoot,
        const std::filesystem::path& stagedFile,
        uint64_t maximumBytes,
        std::optional<uint64_t> expectedBytes = std::nullopt);
    void move_file_atomically(
        const std::filesystem::path& source,
        const std::filesystem::path& destination);
    [[nodiscard]] bool files_are_identical(
        const std::filesystem::path& first,
        const std::filesystem::path& second);
    void write_utf8_file_atomic(const std::filesystem::path& path, std::wstring_view value);
    [[nodiscard]] std::wstring read_utf8_file(const std::filesystem::path& path, size_t maximumBytes);

    [[nodiscard]] file_version validate_host_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_engine_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_updater_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_runtime_candidate(
        const std::filesystem::path& path,
        uint16_t expectedTrack,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_release_manifest_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedSignerPin);
    [[nodiscard]] file_version validate_policy_candidate(
        const std::filesystem::path& path,
        std::wstring_view expectedOriginalFilename,
        std::wstring_view expectedSignerPin);
}
