// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Builds a port independent identifier for a physical monitor from its EDID block.
/// The result looks like <c>DEL-41A3-ABC123</c> (PNP manufacturer, product code, serial).
/// </summary>
public static class EdidIdentity
{
    private const int BaseBlockLength = 128;
    private const byte SerialStringDescriptorTag = 0xFF;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
    private static readonly int[] DescriptorOffsets = [54, 72, 90, 108];

    /// <summary>
    /// Returns a hardware identifier for the monitor, or <c>null</c> when the EDID is invalid
    /// or has no usable serial number.
    /// </summary>
    /// <remarks>
    /// Without a serial, two identical monitors would get the same ID, and we'd rather
    /// not match at all than hand one monitor's dock to its twin.
    /// </remarks>
    public static string? TryCreate(ReadOnlySpan<byte> edid)
    {
        if (edid.Length < BaseBlockLength || !edid[..Header.Length].SequenceEqual(Header))
        {
            return null;
        }

        var manufacturer = DecodeManufacturer((ushort)((edid[8] << 8) | edid[9]));
        if (manufacturer is null)
        {
            return null;
        }

        var productCode = (ushort)(edid[10] | (edid[11] << 8));
        var serial = ReadSerialString(edid) ?? ReadNumericSerial(edid);
        if (serial is null)
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{manufacturer}-{productCode:X4}-{serial}");
    }

    private static string? DecodeManufacturer(ushort value)
    {
        Span<char> chars = stackalloc char[3];
        for (var i = 0; i < 3; i++)
        {
            var letter = (value >> (10 - (i * 5))) & 0x1F;
            if (letter is < 1 or > 26)
            {
                return null;
            }

            chars[i] = (char)('A' + letter - 1);
        }

        return new string(chars);
    }

    private static string? ReadNumericSerial(ReadOnlySpan<byte> edid)
    {
        var serial = BitConverter.ToUInt32(edid.Slice(12, 4));

        // 0x01010101 is a common placeholder serial on budget and virtual panels.
        return serial is 0 or 0x01010101 or uint.MaxValue ? null : serial.ToString(CultureInfo.InvariantCulture);
    }

    private static string? ReadSerialString(ReadOnlySpan<byte> edid)
    {
        foreach (var offset in DescriptorOffsets)
        {
            // Display descriptors start with 0x0000 and carry their tag in byte 3.
            var descriptor = edid.Slice(offset, 18);
            if (descriptor[0] != 0 || descriptor[1] != 0 || descriptor[3] != SerialStringDescriptorTag)
            {
                continue;
            }

            var text = Encoding.ASCII.GetString(descriptor[5..]);
            var newline = text.IndexOf('\n');
            if (newline >= 0)
            {
                text = text[..newline];
            }

            text = text.Trim();
            if (text.Length > 0 && text.Trim('0').Length > 0)
            {
                return text;
            }
        }

        return null;
    }
}
