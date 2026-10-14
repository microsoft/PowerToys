// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Reads the memory hardware details that Task Manager shows: speed, slots, form factor, and
/// hardware reserved memory. They're read once, since they don't change while Windows runs.
/// </summary>
internal static class MemoryHardware
{
    private const byte MemoryDeviceType = 17;
    private const byte EndOfTableType = 127;

    // The table data follows an 8-byte RawSMBIOSData header.
    private const int RawSmbiosHeaderLength = 8;

    private static readonly Lazy<MemoryModules?> CachedModules = new(ReadModules);
    private static readonly Lazy<ulong> CachedInstalledBytes = new(ReadInstalledBytes);

    /// <summary>Gets the memory modules, or null when the firmware doesn't describe them.</summary>
    public static MemoryModules? Modules => CachedModules.Value;

    /// <summary>Gets the installed memory in bytes, or zero when unknown.</summary>
    public static ulong InstalledBytes => CachedInstalledBytes.Value;

    /// <summary>
    /// Parses SMBIOS structures for memory devices (type 17). Each device is a slot; a device
    /// with a size is a used slot. Speed is the configured speed in MT/s, or the rated speed.
    /// </summary>
    internal static MemoryModules? Parse(ReadOnlySpan<byte> table)
    {
        var slots = 0;
        var used = 0;
        uint speed = 0;
        byte formFactor = 0;
        var offset = 0;
        while (offset + 4 <= table.Length)
        {
            var type = table[offset];
            var length = table[offset + 1];
            if (type == EndOfTableType || length < 4 || offset + length > table.Length)
            {
                break;
            }

            if (type == MemoryDeviceType && length >= 0x15)
            {
                var device = table.Slice(offset, length);
                slots++;
                var size = BinaryPrimitives.ReadUInt16LittleEndian(device[0x0C..]);
                if (size is not 0 and not 0xFFFF)
                {
                    used++;
                    if (formFactor == 0)
                    {
                        formFactor = device[0x0E];
                    }

                    speed = Math.Max(speed, ReadSpeed(device));
                }
            }

            // Strings follow the formatted area and end with two zero bytes.
            var next = offset + length;
            while (next + 1 < table.Length && (table[next] != 0 || table[next + 1] != 0))
            {
                next++;
            }

            offset = next + 2;
        }

        return slots == 0 ? null : new MemoryModules(used, slots, speed, formFactor);
    }

    private static uint ReadSpeed(ReadOnlySpan<byte> device)
    {
        // Configured speed (SMBIOS 2.7+) is what the memory runs at; fall back to the rated speed.
        // 0xFFFF means the value is in the matching 32-bit extended field (SMBIOS 3.3+).
        uint configured = device.Length >= 0x22 ? BinaryPrimitives.ReadUInt16LittleEndian(device[0x20..]) : 0u;
        if (configured == 0xFFFF)
        {
            configured = device.Length >= 0x5C ? BinaryPrimitives.ReadUInt32LittleEndian(device[0x58..]) : 0;
        }

        if (configured != 0)
        {
            return configured;
        }

        uint rated = device.Length >= 0x17 ? BinaryPrimitives.ReadUInt16LittleEndian(device[0x15..]) : 0u;
        if (rated == 0xFFFF)
        {
            rated = device.Length >= 0x58 ? BinaryPrimitives.ReadUInt32LittleEndian(device[0x54..]) : 0;
        }

        return rated;
    }

    /// <summary>Returns the name of an SMBIOS memory form factor, or null to hide it.</summary>
    public static string? GetFormFactorName(byte formFactor) => formFactor switch
    {
        0x09 => "DIMM",
        0x0B => CoreWidgetProvider.Helpers.Resources.GetResource("Memory_Widget_Template/Form_Factor_Row_Of_Chips"),
        0x0D => "SODIMM",
        0x0F => "FB-DIMM",
        0x11 => "CAMM",
        _ => null,
    };

    private static unsafe MemoryModules? ReadModules()
    {
        try
        {
            var size = PInvoke.GetSystemFirmwareTable(FIRMWARE_TABLE_PROVIDER.RSMB, 0, null, 0);
            if (size <= RawSmbiosHeaderLength)
            {
                return null;
            }

            var buffer = new byte[size];
            fixed (byte* pointer = buffer)
            {
                if (PInvoke.GetSystemFirmwareTable(FIRMWARE_TABLE_PROVIDER.RSMB, 0, pointer, size) != size)
                {
                    return null;
                }
            }

            var tableLength = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4)), (uint)(buffer.Length - RawSmbiosHeaderLength));
            return Parse(buffer.AsSpan(RawSmbiosHeaderLength, tableLength));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ulong ReadInstalledBytes() =>
        PInvoke.GetPhysicallyInstalledSystemMemory(out var kilobytes) ? kilobytes * 1024 : 0;
}

/// <summary>Memory modules as the firmware describes them.</summary>
internal readonly record struct MemoryModules(int SlotsUsed, int SlotCount, uint SpeedMegaTransfers, byte FormFactor);
