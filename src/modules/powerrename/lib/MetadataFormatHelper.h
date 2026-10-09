// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <string>
#include <utility>
#include <windows.h>
#include <propvarutil.h>

namespace PowerRenameLib
{
    /// <summary>
    /// Helper class for formatting and parsing metadata values
    /// Provides static utility functions for converting metadata to human-readable strings
    /// and parsing raw metadata values
    /// </summary>
    class MetadataFormatHelper
    {
    public:
        // Formatting functions - Convert metadata values to display strings
        
        /// <summary>
        /// Format aperture value (f-number)
        /// </summary>
        /// <param name="aperture">Aperture value (e.g., 2.8)</param>
        /// <returns>Formatted string (e.g., "f/2.8")</returns>
        static std::wstring FormatAperture(double aperture);

        /// <summary>
        /// Format shutter speed
        /// </summary>
        /// <param name="speed">Shutter speed in seconds</param>
        /// <returns>Formatted string (e.g., "1/100s" or "2.5s")</returns>
        static std::wstring FormatShutterSpeed(double speed);

        /// <summary>
        /// Format ISO value
        /// </summary>
        /// <param name="iso">ISO speed value</param>
        /// <returns>Formatted string (e.g., "ISO 400")</returns>
        static std::wstring FormatISO(int64_t iso);

        /// <summary>
        /// Format flash status
        /// </summary>
        /// <param name="flashValue">Flash value from EXIF</param>
        /// <returns>Formatted string (e.g., "Flash On" or "Flash Off")</returns>
        static std::wstring FormatFlash(int64_t flashValue);

        /// <summary>
        /// Format GPS coordinate
        /// </summary>
        /// <param name="coord">Coordinate value in decimal degrees</param>
        /// <param name="isLatitude">true for latitude, false for longitude</param>
        /// <returns>Formatted string (e.g., "40°26.76'N")</returns>
        static std::wstring FormatCoordinate(double coord, bool isLatitude);

        /// <summary>
        /// Format SYSTEMTIME to string
        /// </summary>
        /// <param name="st">SYSTEMTIME structure</param>
        /// <returns>Formatted string (e.g., "2024-03-15 14:30:45")</returns>
        static std::wstring FormatSystemTime(const SYSTEMTIME& st);

        // Parsing functions - Convert raw metadata to usable values

        /// <summary>
        /// Parse GPS rational value from PROPVARIANT
        /// </summary>
        /// <param name="pv">PROPVARIANT containing GPS rational data</param>
        /// <returns>Parsed double value</returns>
        static double ParseGPSRational(const PROPVARIANT& pv);

        /// <summary>
        /// Parse single rational value from byte array
        /// </summary>
        /// <param name="bytes">Byte array containing rational data</param>
        /// <param name="offset">Offset in the byte array</param>
        /// <returns>Parsed double value (numerator / denominator)</returns>
        static double ParseSingleRational(const uint8_t* bytes, size_t offset);

        /// <summary>
        /// Parse single signed rational value from byte array
        /// </summary>
        /// <param name="bytes">Byte array containing signed rational data</param>
        /// <param name="offset">Offset in the byte array</param>
        /// <returns>Parsed double value (signed numerator / signed denominator)</returns>
        static double ParseSingleSRational(const uint8_t* bytes, size_t offset);

        /// <summary>
        /// Parse GPS coordinates from PROPVARIANT values
        /// </summary>
        /// <param name="latitude">PROPVARIANT containing latitude</param>
        /// <param name="longitude">PROPVARIANT containing longitude</param>
        /// <param name="latRef">PROPVARIANT containing latitude reference (N/S)</param>
        /// <param name="lonRef">PROPVARIANT containing longitude reference (E/W)</param>
        /// <returns>Pair of (latitude, longitude) in decimal degrees</returns>
        static std::pair<double, double> ParseGPSCoordinates(
            const PROPVARIANT& latitude,
            const PROPVARIANT& longitude,
            const PROPVARIANT& latRef,
            const PROPVARIANT& lonRef);

        /// <summary>
        /// Sanitize a string to make it safe for use in filenames
        /// Replaces illegal filename characters (< > : " / \ | ? * and control chars) with underscore
        /// Also removes trailing dots and spaces which Windows doesn't allow at end of filename
        /// 
        /// IMPORTANT: This should ONLY be called in ExtractPatterns to avoid performance waste.
        /// Do NOT call this function when reading raw metadata values.
        /// </summary>
        /// <param name="str">String to sanitize</param>
        /// <returns>Sanitized string safe for use in filename</returns>
        static std::wstring SanitizeForFileName(const std::wstring& str);
    };
}
