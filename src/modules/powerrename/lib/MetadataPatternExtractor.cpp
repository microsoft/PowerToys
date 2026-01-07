// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataPatternExtractor.h"
#include "MetadataFormatHelper.h"
#include "WICMetadataExtractor.h"
#include <algorithm>
#include <format>
#include <sstream>
#include <iomanip>
#include <cmath>
#include <utility>

using namespace PowerRenameLib;

MetadataPatternExtractor::MetadataPatternExtractor()
    : extractor(std::make_unique<WICMetadataExtractor>())
{
}

MetadataPatternExtractor::~MetadataPatternExtractor() = default;

MetadataPatternMap MetadataPatternExtractor::ExtractPatterns(
    const std::wstring& filePath,
    MetadataType type)
{
    MetadataPatternMap patterns;
    
    switch (type)
    {
    case MetadataType::EXIF:
        patterns = ExtractEXIFPatterns(filePath);
        break;
    case MetadataType::XMP:
        patterns = ExtractXMPPatterns(filePath);
        break;
    default:
        return MetadataPatternMap();
    }

    // Sanitize all pattern values for filename safety before returning
    // This ensures all metadata values are safe to use in filenames (removes illegal chars like <>:"/\|?*)
    // IMPORTANT: Only call SanitizeForFileName here to avoid performance waste
    for (auto& [key, value] : patterns)
    {
        value = MetadataFormatHelper::SanitizeForFileName(value);
    }

    return patterns;
}

void MetadataPatternExtractor::ClearCache()
{
    if (extractor)
    {
        extractor->ClearCache();
    }
}

MetadataPatternMap MetadataPatternExtractor::ExtractEXIFPatterns(const std::wstring& filePath)
{
    MetadataPatternMap patterns;

    EXIFMetadata exif;
    if (!extractor->ExtractEXIFMetadata(filePath, exif))
    {
        return patterns;
    }

    if (exif.cameraMake.has_value())
    {
        patterns[MetadataPatterns::CAMERA_MAKE] = exif.cameraMake.value();
    }

    if (exif.cameraModel.has_value())
    {
        patterns[MetadataPatterns::CAMERA_MODEL] = exif.cameraModel.value();
    }

    if (exif.lensModel.has_value())
    {
        patterns[MetadataPatterns::LENS] = exif.lensModel.value();
    }

    if (exif.iso.has_value())
    {
        patterns[MetadataPatterns::ISO] = MetadataFormatHelper::FormatISO(exif.iso.value());
    }

    if (exif.aperture.has_value())
    {
        patterns[MetadataPatterns::APERTURE] = MetadataFormatHelper::FormatAperture(exif.aperture.value());
    }

    if (exif.shutterSpeed.has_value())
    {
        patterns[MetadataPatterns::SHUTTER] = MetadataFormatHelper::FormatShutterSpeed(exif.shutterSpeed.value());
    }

    if (exif.focalLength.has_value())
    {
        patterns[MetadataPatterns::FOCAL] = std::to_wstring(static_cast<int>(exif.focalLength.value())) + L"mm";
    }

    if (exif.flash.has_value())
    {
        patterns[MetadataPatterns::FLASH] = MetadataFormatHelper::FormatFlash(exif.flash.value());
    }

    if (exif.width.has_value())
    {
        patterns[MetadataPatterns::WIDTH] = std::to_wstring(exif.width.value());
    }

    if (exif.height.has_value())
    {
        patterns[MetadataPatterns::HEIGHT] = std::to_wstring(exif.height.value());
    }

    if (exif.author.has_value())
    {
        patterns[MetadataPatterns::AUTHOR] = exif.author.value();
    }

    if (exif.copyright.has_value())
    {
        patterns[MetadataPatterns::COPYRIGHT] = exif.copyright.value();
    }

    if (exif.latitude.has_value())
    {
        patterns[MetadataPatterns::LATITUDE] = MetadataFormatHelper::FormatCoordinate(exif.latitude.value(), true);
    }

    if (exif.longitude.has_value())
    {
        patterns[MetadataPatterns::LONGITUDE] = MetadataFormatHelper::FormatCoordinate(exif.longitude.value(), false);
    }

    // Only extract DATE_TAKEN patterns (most commonly used)
    if (exif.dateTaken.has_value())
    {
        const SYSTEMTIME& date = exif.dateTaken.value();
        patterns[MetadataPatterns::DATE_TAKEN_YYYY] = std::format(L"{:04d}", date.wYear);
        patterns[MetadataPatterns::DATE_TAKEN_YY] = std::format(L"{:02d}", date.wYear % 100);
        patterns[MetadataPatterns::DATE_TAKEN_MM] = std::format(L"{:02d}", date.wMonth);
        patterns[MetadataPatterns::DATE_TAKEN_DD] = std::format(L"{:02d}", date.wDay);
        patterns[MetadataPatterns::DATE_TAKEN_HH] = std::format(L"{:02d}", date.wHour);
        patterns[MetadataPatterns::DATE_TAKEN_mm] = std::format(L"{:02d}", date.wMinute);
        patterns[MetadataPatterns::DATE_TAKEN_SS] = std::format(L"{:02d}", date.wSecond);
    }
    // Note: dateDigitized and dateModified are still extracted but not exposed as patterns

    if (exif.exposureBias.has_value())
    {
        patterns[MetadataPatterns::EXPOSURE_BIAS] = std::format(L"{:.2f}", exif.exposureBias.value());
    }

    if (exif.orientation.has_value())
    {
        patterns[MetadataPatterns::ORIENTATION] = std::to_wstring(exif.orientation.value());
    }

    if (exif.colorSpace.has_value())
    {
        patterns[MetadataPatterns::COLOR_SPACE] = std::to_wstring(exif.colorSpace.value());
    }

    if (exif.altitude.has_value())
    {
        patterns[MetadataPatterns::ALTITUDE] = std::format(L"{:.2f} m", exif.altitude.value());
    }

    return patterns;
}

MetadataPatternMap MetadataPatternExtractor::ExtractXMPPatterns(const std::wstring& filePath)
{
    MetadataPatternMap patterns;

    XMPMetadata xmp;
    if (!extractor->ExtractXMPMetadata(filePath, xmp))
    {
        return patterns;
    }

    if (xmp.creator.has_value())
    {
        const auto& creator = xmp.creator.value();
        patterns[MetadataPatterns::AUTHOR] = creator;
        patterns[MetadataPatterns::CREATOR] = creator;
    }

    if (xmp.rights.has_value())
    {
        const auto& rights = xmp.rights.value();
        patterns[MetadataPatterns::RIGHTS] = rights;
        patterns[MetadataPatterns::COPYRIGHT] = rights;
    }

    if (xmp.title.has_value())
    {
        patterns[MetadataPatterns::TITLE] = xmp.title.value();
    }

    if (xmp.description.has_value())
    {
        patterns[MetadataPatterns::DESCRIPTION] = xmp.description.value();
    }

    if (xmp.subject.has_value())
    {
        std::wstring joined;
        for (const auto& entry : xmp.subject.value())
        {
            if (!joined.empty())
            {
                joined.append(L"; ");
            }
            joined.append(entry);
        }
        if (!joined.empty())
        {
            patterns[MetadataPatterns::SUBJECT] = joined;
        }
    }

    if (xmp.creatorTool.has_value())
    {
        patterns[MetadataPatterns::CREATOR_TOOL] = xmp.creatorTool.value();
    }

    if (xmp.documentID.has_value())
    {
        patterns[MetadataPatterns::DOCUMENT_ID] = xmp.documentID.value();
    }

    if (xmp.instanceID.has_value())
    {
        patterns[MetadataPatterns::INSTANCE_ID] = xmp.instanceID.value();
    }

    if (xmp.originalDocumentID.has_value())
    {
        patterns[MetadataPatterns::ORIGINAL_DOCUMENT_ID] = xmp.originalDocumentID.value();
    }

    if (xmp.versionID.has_value())
    {
        patterns[MetadataPatterns::VERSION_ID] = xmp.versionID.value();
    }

    // Only extract CREATE_DATE patterns (primary creation time)
    if (xmp.createDate.has_value())
    {
        const SYSTEMTIME& date = xmp.createDate.value();
        patterns[MetadataPatterns::CREATE_DATE_YYYY] = std::format(L"{:04d}", date.wYear);
        patterns[MetadataPatterns::CREATE_DATE_YY] = std::format(L"{:02d}", date.wYear % 100);
        patterns[MetadataPatterns::CREATE_DATE_MM] = std::format(L"{:02d}", date.wMonth);
        patterns[MetadataPatterns::CREATE_DATE_DD] = std::format(L"{:02d}", date.wDay);
        patterns[MetadataPatterns::CREATE_DATE_HH] = std::format(L"{:02d}", date.wHour);
        patterns[MetadataPatterns::CREATE_DATE_mm] = std::format(L"{:02d}", date.wMinute);
        patterns[MetadataPatterns::CREATE_DATE_SS] = std::format(L"{:02d}", date.wSecond);
    }
    // Note: modifyDate and metadataDate are still extracted but not exposed as patterns

    return patterns;
}

// AddDatePatterns function has been removed as dynamic patterns are no longer supported.
// Date patterns are now directly added inline for DATE_TAKEN and CREATE_DATE only.
// Formatting functions have been moved to MetadataFormatHelper for better testability.

std::vector<std::wstring> MetadataPatternExtractor::GetSupportedPatterns(MetadataType type)
{
    switch (type)
    {
    case MetadataType::EXIF:
        return {
            MetadataPatterns::CAMERA_MAKE,
            MetadataPatterns::CAMERA_MODEL,
            MetadataPatterns::LENS,
            MetadataPatterns::ISO,
            MetadataPatterns::APERTURE,
            MetadataPatterns::SHUTTER,
            MetadataPatterns::FOCAL,
            MetadataPatterns::FLASH,
            MetadataPatterns::WIDTH,
            MetadataPatterns::HEIGHT,
            MetadataPatterns::AUTHOR,
            MetadataPatterns::COPYRIGHT,
            MetadataPatterns::LATITUDE,
            MetadataPatterns::LONGITUDE,
            MetadataPatterns::DATE_TAKEN_YYYY,
            MetadataPatterns::DATE_TAKEN_YY,
            MetadataPatterns::DATE_TAKEN_MM,
            MetadataPatterns::DATE_TAKEN_DD,
            MetadataPatterns::DATE_TAKEN_HH,
            MetadataPatterns::DATE_TAKEN_mm,
            MetadataPatterns::DATE_TAKEN_SS,
            MetadataPatterns::EXPOSURE_BIAS,
            MetadataPatterns::ORIENTATION,
            MetadataPatterns::COLOR_SPACE,
            MetadataPatterns::ALTITUDE
        };

    case MetadataType::XMP:
        return {
            MetadataPatterns::AUTHOR,
            MetadataPatterns::COPYRIGHT,
            MetadataPatterns::RIGHTS,
            MetadataPatterns::TITLE,
            MetadataPatterns::DESCRIPTION,
            MetadataPatterns::SUBJECT,
            MetadataPatterns::CREATOR,
            MetadataPatterns::CREATOR_TOOL,
            MetadataPatterns::DOCUMENT_ID,
            MetadataPatterns::INSTANCE_ID,
            MetadataPatterns::ORIGINAL_DOCUMENT_ID,
            MetadataPatterns::VERSION_ID,
            MetadataPatterns::CREATE_DATE_YYYY,
            MetadataPatterns::CREATE_DATE_YY,
            MetadataPatterns::CREATE_DATE_MM,
            MetadataPatterns::CREATE_DATE_DD,
            MetadataPatterns::CREATE_DATE_HH,
            MetadataPatterns::CREATE_DATE_mm,
            MetadataPatterns::CREATE_DATE_SS
        };

    default:
        return {};
    }
}

std::vector<std::wstring> MetadataPatternExtractor::GetAllPossiblePatterns()
{
    auto exifPatterns = GetSupportedPatterns(MetadataType::EXIF);
    auto xmpPatterns = GetSupportedPatterns(MetadataType::XMP);

    std::vector<std::wstring> allPatterns;
    allPatterns.reserve(exifPatterns.size() + xmpPatterns.size());

    allPatterns.insert(allPatterns.end(), exifPatterns.begin(), exifPatterns.end());
    allPatterns.insert(allPatterns.end(), xmpPatterns.begin(), xmpPatterns.end());

    std::sort(allPatterns.begin(), allPatterns.end());
    allPatterns.erase(std::unique(allPatterns.begin(), allPatterns.end()), allPatterns.end());

    return allPatterns;
}

