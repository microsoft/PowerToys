// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace PowerDisplay.Common.Helpers;

/// <summary>
/// Helper class for mapping PnP (Plug and Play) manufacturer IDs to display names.
/// PnP IDs are 3-character codes assigned by Microsoft to hardware manufacturers.
/// See: https://uefi.org/pnp_id_list
/// </summary>
public static class PnpIdHelper
{
    /// <summary>
    /// Map of common laptop/monitor manufacturer PnP IDs to display names.
    /// Only includes manufacturers known to produce laptops with internal displays.
    /// </summary>
    private static readonly FrozenDictionary<string, string> ManufacturerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Major laptop manufacturers
        { "ACR", "Acer" },
        { "AUO", "AU Optronics" },
        { "BOE", "BOE" },
        { "CMN", "Chi Mei Innolux" },
        { "DEL", "Dell" },
        { "HWP", "HP" },
        { "IVO", "InfoVision" },
        { "LEN", "Lenovo" },
        { "LGD", "LG Display" },
        { "NCP", "Najing CEC Panda" },
        { "SAM", "Samsung" },
        { "SDC", "Samsung Display" },
        { "SEC", "Samsung Electronics" },
        { "SHP", "Sharp" },
        { "AUS", "ASUS" },
        { "MSI", "MSI" },
        { "APP", "Apple" },
        { "SNY", "Sony" },
        { "PHL", "Philips" },
        { "HSD", "HannStar" },
        { "CPT", "Chunghwa Picture Tubes" },
        { "QDS", "Quanta Display" },
        { "TMX", "Tianma Microelectronics" },
        { "CSO", "CSOT" },

        // Microsoft Surface
        { "MSF", "Microsoft" },
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extract the 3-character PnP manufacturer ID from a hardware ID.
    /// </summary>
    /// <param name="hardwareId">Hardware ID like "LEN4038" or "BOE0900".</param>
    /// <returns>The 3-character PnP ID (e.g., "LEN"), or null if invalid.</returns>
    public static string? ExtractPnpId(string? hardwareId)
    {
        if (string.IsNullOrEmpty(hardwareId) || hardwareId.Length < 3)
        {
            return null;
        }

        // PnP ID is the first 3 characters
        return hardwareId.Substring(0, 3).ToUpperInvariant();
    }

    /// <summary>
    /// Get a user-friendly display name for an internal display based on its hardware ID.
    /// </summary>
    /// <param name="hardwareId">Hardware ID like "LEN4038" or "BOE0900".</param>
    /// <returns>Display name like "Lenovo Built-in Display" or "Built-in Display" as fallback.</returns>
    public static string GetBuiltInDisplayName(string? hardwareId)
    {
        var pnpId = ExtractPnpId(hardwareId);

        if (pnpId != null && ManufacturerNames.TryGetValue(pnpId, out var manufacturer))
        {
            return $"{manufacturer} Built-in Display";
        }

        return "Built-in Display";
    }
}
