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
/// <remarks>
/// Speed, slots, and form factor come from the firmware's SMBIOS table, which Windows returns
/// as-is from GetSystemFirmwareTable('RSMB'). The layout is defined by the DMTF System
/// Management BIOS (SMBIOS) Reference Specification, DSP0134:
/// https://www.dmtf.org/standards/smbios. Section references below are to DSP0134 3.x, where
/// section 6.1 describes the structure layout and section 7.18 describes the Memory Device
/// (type 17) structure. All multi-byte fields are little-endian.
/// </remarks>
internal static class MemoryHardware
{
    // GetSystemFirmwareTable('RSMB') returns a RawSMBIOSData header before the table:
    // Used20CallingMethod, SMBIOSMajorVersion, SMBIOSMinorVersion, and DmiRevision (one byte
    // each), then the table length as a DWORD at offset 4. The table data starts at offset 8.
    // https://learn.microsoft.com/windows/win32/api/sysinfoapi/nf-sysinfoapi-getsystemfirmwaretable
    private const int RawSmbiosLengthOffset = 4;
    private const int RawSmbiosHeaderLength = 8;

    // Every structure starts with a 4-byte header (section 6.1.2): Type (BYTE), Length (BYTE,
    // the length of the formatted area only), and Handle (WORD).
    private const int StructureHeaderLength = 4;
    private const int StructureTypeOffset = 0x00;
    private const int StructureLengthOffset = 0x01;

    // Structure types (section 7): 17 is Memory Device, 127 is End-of-Table.
    private const byte MemoryDeviceType = 17;
    private const byte EndOfTableType = 127;

    // Memory Device (type 17) fields, section 7.18. Each field exists only when the
    // structure's Length covers it, since older SMBIOS versions have shorter structures.
    internal const int SizeOffset = 0x0C;                     // WORD, 2.1+. 0 = no module in the slot; 0xFFFF = unknown.
    internal const int FormFactorOffset = 0x0E;               // BYTE, 2.1+. See section 7.18.1 and GetFormFactorName.
    internal const int SpeedOffset = 0x15;                    // WORD, 2.3+. Rated speed in MT/s; 0 = unknown.
    internal const int ConfiguredSpeedOffset = 0x20;          // WORD, 2.7+. Speed the memory runs at, in MT/s; 0 = unknown.
    internal const int ExtendedSpeedOffset = 0x54;            // DWORD, 3.3+. Rated speed when Speed is 0xFFFF.
    internal const int ExtendedConfiguredSpeedOffset = 0x58;  // DWORD, 3.3+. Configured speed when Configured Speed is 0xFFFF.

    // The shortest Memory Device structure (SMBIOS 2.1) is 0x15 bytes; it ends after Type Detail.
    private const int MinimumMemoryDeviceLength = 0x15;

    // A 16-bit Speed or Size of 0xFFFF means the value doesn't fit (speed) or is unknown (size).
    private const ushort WordValueNotPresent = 0xFFFF;

    // Memory Device Form Factor values, section 7.18.1.
    private const byte FormFactorDimm = 0x09;
    private const byte FormFactorRowOfChips = 0x0B;           // Memory soldered to the board.
    private const byte FormFactorSodimm = 0x0D;
    private const byte FormFactorFbDimm = 0x0F;
    private const byte FormFactorCamm = 0x11;

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
        while (offset + StructureHeaderLength <= table.Length)
        {
            var type = table[offset + StructureTypeOffset];
            var length = table[offset + StructureLengthOffset];
            if (type == EndOfTableType || length < StructureHeaderLength || offset + length > table.Length)
            {
                break;
            }

            if (type == MemoryDeviceType && length >= MinimumMemoryDeviceLength)
            {
                var device = table.Slice(offset, length);
                slots++;
                var size = BinaryPrimitives.ReadUInt16LittleEndian(device[SizeOffset..]);
                if (size is not 0 and not WordValueNotPresent)
                {
                    used++;
                    if (formFactor == 0)
                    {
                        formFactor = device[FormFactorOffset];
                    }

                    speed = Math.Max(speed, ReadSpeed(device));
                }
            }

            // The formatted area is followed by its strings (section 6.1.3). Each string ends
            // with a zero byte and the set ends with an extra zero, so a structure always ends
            // with two zero bytes, even when it has no strings.
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
        // Prefer the configured speed, which is what the memory runs at, and fall back to the
        // rated speed. When a 16-bit speed is 0xFFFF, the real value is in its 32-bit extended field.
        var configured = ReadWordWithExtension(device, ConfiguredSpeedOffset, ExtendedConfiguredSpeedOffset);
        return configured != 0 ? configured : ReadWordWithExtension(device, SpeedOffset, ExtendedSpeedOffset);
    }

    /// <summary>
    /// Reads a 16-bit field, or its 32-bit extended field when the 16-bit value is 0xFFFF.
    /// Returns zero when the structure is too short to contain the field.
    /// </summary>
    private static uint ReadWordWithExtension(ReadOnlySpan<byte> device, int wordOffset, int extendedOffset)
    {
        if (device.Length < wordOffset + sizeof(ushort))
        {
            return 0;
        }

        uint value = BinaryPrimitives.ReadUInt16LittleEndian(device[wordOffset..]);
        if (value == WordValueNotPresent)
        {
            value = device.Length >= extendedOffset + sizeof(uint) ? BinaryPrimitives.ReadUInt32LittleEndian(device[extendedOffset..]) : 0;
        }

        return value;
    }

    /// <summary>Returns the name of an SMBIOS memory form factor, or null to hide it.</summary>
    public static string? GetFormFactorName(byte formFactor) => formFactor switch
    {
        FormFactorDimm => "DIMM",
        FormFactorRowOfChips => CoreWidgetProvider.Helpers.Resources.GetResource("Memory_Widget_Template/Form_Factor_Row_Of_Chips"),
        FormFactorSodimm => "SODIMM",
        FormFactorFbDimm => "FB-DIMM",
        FormFactorCamm => "CAMM",
        _ => null,
    };

    private static unsafe MemoryModules? ReadModules()
    {
        try
        {
            // The first call with no buffer returns the size needed.
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

            // Trust the header's table length only as far as the buffer goes.
            var tableLength = (int)Math.Min(
                BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(RawSmbiosLengthOffset)),
                (uint)(buffer.Length - RawSmbiosHeaderLength));
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
