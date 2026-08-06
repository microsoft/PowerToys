// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <cstdint>

namespace SettingsBrokerPrototype
{
    constexpr wchar_t kServiceName[] = L"PTSettingsBrokerPrototype";
    constexpr wchar_t kServiceAccount[] = L"NT SERVICE\\PTSettingsBrokerPrototype";
    constexpr wchar_t kPipeName[] = L"\\\\.\\pipe\\PTSettingsBrokerPrototype.v1";

    constexpr uint32_t kRequestMagic = 0x42545050;  // PPTB
    constexpr uint32_t kResponseMagic = 0x52545050; // PPTR
    constexpr uint16_t kProtocolMajor = 1;
    constexpr uint16_t kMinProtocolMinor = 0;
    constexpr uint16_t kMaxProtocolMinor = 1;
    constexpr uint32_t kMaxPayloadBytes = 1024u * 1024u;
    constexpr uint32_t kIoTimeoutMs = 5000;
    constexpr uint32_t kCancelCompletionTimeoutMs = 1000;
    constexpr uint32_t kWorkerCount = 8;
    constexpr uint32_t kPerSidConnectionLimit = 2;
    constexpr uint8_t kResponseConsumedAck = 0xa5;

    constexpr uint32_t CapabilityMultiTarget = 0x00000001;
    constexpr uint32_t CapabilityPerUserQuota = 0x00000002;
    constexpr uint32_t kCapabilities = CapabilityMultiTarget | CapabilityPerUserQuota;

    enum class Opcode : uint16_t
    {
        Ping = 1,
        Get = 2,
        Put = 3,
    };

    enum class Status : uint16_t
    {
        Ok = 0,
        BadRequest = 1,
        UnsupportedMajor = 2,
        UnsupportedMinor = 3,
        UnknownOpcode = 4,
        PayloadTooLarge = 5,
        AuthRejected = 6,
        TargetDenied = 7,
        Busy = 8,
        NotFound = 9,
        IoError = 10,
        Timeout = 11,
    };

#pragma pack(push, 1)
    struct RequestHeader
    {
        uint32_t magic;
        uint16_t headerBytes;
        uint16_t major;
        uint16_t minor;
        uint16_t opcode;
        uint32_t targetId;
        uint32_t payloadBytes;
    };

    struct ResponseHeader
    {
        uint32_t magic;
        uint16_t headerBytes;
        uint16_t major;
        uint16_t minor;
        uint16_t status;
        uint16_t reserved;
        uint32_t capabilities;
        uint32_t payloadBytes;
    };
#pragma pack(pop)

    static_assert(sizeof(RequestHeader) == 20);
    static_assert(sizeof(ResponseHeader) == 22);
}
