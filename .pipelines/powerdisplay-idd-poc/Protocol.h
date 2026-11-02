// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#pragma once

inline constexpr GUID FixtureInterface = { 0x51800002, 0x10ab, 0x4abc, { 0x90, 0x10, 0x50, 0x54, 0x49, 0x44, 0x44, 0x01 } };
inline constexpr ULONG SnapshotIoctl = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_READ_DATA);

struct MonitorSnapshot
{
    ULONG Connector;
    ULONG Brightness;
    ULONG Transmits;
    ULONG Receives;
    ULONG Gets;
    ULONG Sets;
    ULONG Capabilities;
    ULONG LastTransmitAddress;
    ULONG LastTransmitLength;
    BYTE LastTransmit[16];
    ULONG LastReceiveAddress;
    ULONG LastReceiveLength;
};

struct FixtureSnapshot
{
    ULONG Version;
    MonitorSnapshot Monitors[2];
};
