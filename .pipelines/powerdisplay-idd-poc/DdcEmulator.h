// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#pragma once
#include "Protocol.h"
#include <array>
#include <cstring>

struct DdcEmulator
{
    MonitorSnapshot Snapshot{};
    std::array<BYTE, 64> Reply{};
    size_t ReplyLength = 0;
    static constexpr char Capabilities[] = "(prot(monitor)type(lcd)model(PT-IDD-POC)cmds(01 03 F3)vcp(10)mccs_ver(2.2))";

    void SetReply(const BYTE* payload, size_t count)
    {
        Reply.fill(0);
        Reply[0] = 0x6e;
        Reply[1] = static_cast<BYTE>(0x80 | count);
        memcpy(Reply.data() + 2, payload, count);
        BYTE checksum = 0x50;
        for (size_t i = 0; i < count + 2; ++i)
        {
            checksum ^= Reply[i];
        }
        Reply[count + 2] = checksum;
        ReplyLength = count + 3;
    }

    bool Transmit(UINT address, const BYTE* data, size_t length)
    {
        ++Snapshot.Transmits;
        Snapshot.LastTransmitAddress = address;
        Snapshot.LastTransmitLength = static_cast<ULONG>(length);
        memset(Snapshot.LastTransmit, 0, sizeof(Snapshot.LastTransmit));
        if (data && length >= 4 && length <= 64)
        {
            memcpy(Snapshot.LastTransmit, data, length < sizeof(Snapshot.LastTransmit) ? length : sizeof(Snapshot.LastTransmit));
        }
        ReplyLength = 0;
        // The native callbacks use wire addresses despite the SevenBitI2CAddress field name.
        if (address != 0x6e || !data || length < 4 || length > 64)
        {
            return false;
        }
        const size_t count = data[1] & 0x7f;
        if (data[0] != 0x51 || (data[1] & 0x80) == 0 || count + 3 != length)
        {
            return false;
        }
        BYTE checksum = 0x6e;
        for (size_t i = 0; i < length; ++i)
        {
            checksum ^= data[i];
        }
        if (checksum != 0)
        {
            return false;
        }
        if (data[2] == 0x01 && count == 2)
        {
            ++Snapshot.Gets;
            const BYTE payload[] = { 0x02, static_cast<BYTE>(data[3] == 0x10 ? 0 : 1), data[3], 0, 0, 100, 0, static_cast<BYTE>(Snapshot.Brightness) };
            SetReply(payload, sizeof(payload));
            return true;
        }
        if (data[2] == 0x03 && count == 4 && data[3] == 0x10 && data[4] == 0 && data[5] <= 100)
        {
            ++Snapshot.Sets;
            Snapshot.Brightness = data[5];
            return true;
        }
        if (data[2] == 0xf3 && count == 3)
        {
            const size_t offset = (static_cast<size_t>(data[3]) << 8) | data[4];
            if (offset > sizeof(Capabilities) - 1)
            {
                return false;
            }
            ++Snapshot.Capabilities;
            BYTE payload[35] = { 0xe3, data[3], data[4] };
            const size_t remaining = sizeof(Capabilities) - 1 - offset;
            const size_t bytes = (remaining < 32) ? remaining : 32;
            memcpy(payload + 3, Capabilities + offset, bytes);
            SetReply(payload, bytes + 3);
            return true;
        }
        return false;
    }

    bool Receive(UINT address, BYTE* data, size_t length)
    {
        ++Snapshot.Receives;
        Snapshot.LastReceiveAddress = address;
        Snapshot.LastReceiveLength = static_cast<ULONG>(length);
        if (address != 0x6f || !data || length == 0 || length > 64 || ReplyLength == 0)
        {
            return false;
        }
        memset(data, 0, length);
        memcpy(data, Reply.data(), (length < ReplyLength) ? length : ReplyLength);
        return true;
    }
};
