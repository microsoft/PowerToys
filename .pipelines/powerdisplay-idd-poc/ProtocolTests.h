// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#pragma once
#include "DdcEmulator.h"
#include <stdexcept>
#include <vector>
#include <initializer_list>

inline void RunProtocolTests()
{
    const auto check = [](bool condition, const char* message) {
        if (!condition)
        {
            throw std::runtime_error(message);
        }
    };
    const auto command = [](std::initializer_list<BYTE> payload) {
        std::vector<BYTE> frame{ 0x51, static_cast<BYTE>(0x80 | payload.size()) };
        frame.insert(frame.end(), payload);
        BYTE checksum = 0x6e;
        for (const BYTE byte : frame)
        {
            checksum ^= byte;
        }
        frame.push_back(checksum);
        return frame;
    };
    const auto transmit = [&](DdcEmulator& state, std::initializer_list<BYTE> payload) {
        const auto frame = command(payload);
        return state.Transmit(0x6e, frame.data(), frame.size());
    };
    DdcEmulator first{}, second{};
    first.Snapshot.Brightness = 40;
    second.Snapshot.Brightness = 60;
    BYTE reply[64]{};
    check(!first.Receive(0x6f, reply, sizeof(reply)), "Receive without request");
    check(transmit(first, { 0x01, 0x10 }), "Get brightness request");
    check(first.Receive(0x6f, reply, sizeof(reply)), "Get brightness reply");
    check(reply[0] == 0x6e && reply[1] == 0x88 && reply[2] == 0x02 && reply[3] == 0 &&
              reply[4] == 0x10 && reply[7] == 100 && reply[9] == 40,
          "Get brightness payload");
    BYTE checksum = 0x50;
    for (size_t i = 0; i < first.ReplyLength; ++i)
    {
        checksum ^= reply[i];
    }
    check(checksum == 0, "Reply checksum");
    check(transmit(first, { 0x03, 0x10, 0, 31 }) && transmit(second, { 0x03, 0x10, 0, 73 }), "Set brightness");
    check(first.Snapshot.Brightness == 31 && second.Snapshot.Brightness == 73 &&
              first.Snapshot.Sets == 1 && second.Snapshot.Sets == 1,
          "Independent state/counters");
    check(transmit(first, { 0x01, 0x10 }) && first.Receive(0x6f, reply, sizeof(reply)) && reply[9] == 31, "Stateful readback");
    check(!transmit(first, { 0x03, 0x10, 0, 101 }) && first.Snapshot.Brightness == 31, "Reject brightness >100");
    check(transmit(first, { 0x01, 0x12 }) && first.Receive(0x6f, reply, sizeof(reply)) && reply[3] == 1, "Unsupported VCP result");

    std::string capabilities;
    for (size_t offset = 0;;)
    {
        check(transmit(first, { 0xf3, static_cast<BYTE>(offset >> 8), static_cast<BYTE>(offset & 0xff) }), "Capabilities request");
        check(first.Receive(0x6f, reply, sizeof(reply)) && reply[2] == 0xe3, "Capabilities response");
        const size_t bytes = (reply[1] & 0x7f) - 3;
        if (bytes == 0)
        {
            break;
        }
        capabilities.append(reinterpret_cast<const char*>(reply + 5), bytes);
        offset += bytes;
        check(offset < 256, "Bound capabilities pagination");
    }
    check(capabilities == DdcEmulator::Capabilities, "Capabilities complete round trip");
    check(!transmit(first, { 0xf3, 0xff, 0xff }), "Reject capabilities offset");
    auto invalid = command({ 0x03, 0x10, 0, 70 });
    invalid.back() ^= 1;
    check(!first.Transmit(0x6e, invalid.data(), invalid.size()) && first.Snapshot.Brightness == 31, "Reject bad checksum");
    check(!first.Transmit(0x6e, nullptr, 64) && !first.Transmit(0x37, invalid.data(), invalid.size()) &&
              !first.Transmit(0x6e, invalid.data(), 3) && !first.Transmit(0x6e, invalid.data(), 65),
          "Reject invalid transport");
    check(!first.Receive(0x6f, reply, sizeof(reply)), "Invalid request clears pending response");
    check(transmit(first, { 0x01, 0x10 }) && !first.Receive(0x37, reply, sizeof(reply)) &&
              !first.Receive(0x6f, nullptr, sizeof(reply)) && !first.Receive(0x6f, reply, 65),
          "Reject invalid receive");
    std::cout << "{\"ProtocolTestsPassed\":true,\"Cases\":6}\n";
}
