#pragma once

#include <cstddef>
#include <cstdint>

namespace powertoys::protected_runtime::protocol
{
    inline constexpr wchar_t host_service_name[] = L"PtPuvrHost";
    inline constexpr wchar_t host_pipe_prefix[] = L"\\\\.\\pipe\\PtPuvrHost-";
    inline constexpr wchar_t control_plane_registry_key[] =
        L"SOFTWARE\\Microsoft\\PowerToys\\WorkspacesProtectedRuntimeControlPlanePrototype";
    inline constexpr wchar_t host_endpoint_registry_value[] = L"HostEndpoint";

    inline constexpr uint32_t magic = 0x52565550; // PUVR
    inline constexpr uint32_t authentication_magic = 0x48545541; // AUTH
    inline constexpr uint16_t version = 5;
    inline constexpr size_t max_release_id_chars = 80;

    enum class control_command : uint16_t
    {
        acquire = 1,
        status = 2,
        release = 3,
    };

#pragma pack(push, 1)
    struct authentication_preface
    {
        uint32_t magic{ authentication_magic };
        uint16_t version{ protocol::version };
        uint16_t reserved{};
    };

    struct control_request
    {
        uint32_t magic{ protocol::magic };
        uint16_t version{ protocol::version };
        uint16_t command{};
        uint16_t reserved{};
        wchar_t releaseId[max_release_id_chars]{};
    };

    struct control_reply
    {
        uint32_t magic{ protocol::magic };
        uint16_t version{ protocol::version };
        uint16_t command{};
        uint32_t win32Status{};
        uint32_t scmState{};
        uint32_t processId{};
        uint32_t leaseCount{};
        wchar_t runtimeVersion[64]{};
        wchar_t activeEngineVersion[64]{};
        wchar_t detail[2048]{};
    };
#pragma pack(pop)
}
