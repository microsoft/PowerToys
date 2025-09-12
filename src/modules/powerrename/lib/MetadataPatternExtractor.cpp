// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataPatternExtractor.h"
#include "WICMetadataExtractor.h"
#include "CachedWICMetadataExtractor.h"
#include <format>
#include <sstream>
#include <iomanip>
#include <cmath>
#include <thread>
#include <future>

using namespace PowerRenameLib;

MetadataPatternExtractor::MetadataPatternExtractor(
    std::shared_ptr<IMetadataExtractor> extractor)
    : extractor(extractor ? extractor : GetDefaultExtractor())
{
}

std::shared_ptr<IMetadataExtractor> MetadataPatternExtractor::GetDefaultExtractor()
{
    static auto defaultExtractor = std::make_shared<CachedWICMetadataExtractor>();
    return defaultExtractor;
}

MetadataPatternMap MetadataPatternExtractor::ExtractPatterns(
    const std::wstring& filePath,
    MetadataType type)
{
    switch (type)
    {
    case MetadataType::EXIF:
        return ExtractEXIFPatterns(filePath);
    case MetadataType::XMP:
        return ExtractXMPPatterns(filePath);
    default:
        return MetadataPatternMap();
    }
}

std::vector<std::pair<std::wstring, MetadataPatternMap>> MetadataPatternExtractor::ExtractPatternsFromFiles(
    const std::vector<std::wstring>& filePaths,
    MetadataType type)
{
    std::vector<std::future<std::pair<std::wstring, MetadataPatternMap>>> futures;
    
    // Launch async tasks for each file
    for (const auto& filePath : filePaths)
    {
        futures.push_back(std::async(std::launch::async, [this, filePath, type]() {
            return std::make_pair(filePath, ExtractPatterns(filePath, type));
        }));
    }
    
    // Collect results
    std::vector<std::pair<std::wstring, MetadataPatternMap>> results;
    for (auto& future : futures)
    {
        results.push_back(future.get());
    }
    
    return results;
}

bool MetadataPatternExtractor::IsSupported(const std::wstring& filePath, MetadataType type) const
{
    return extractor->IsSupported(filePath, type);
}

MetadataPatternMap MetadataPatternExtractor::ExtractEXIFPatterns(const std::wstring& filePath)
{
    MetadataPatternMap patterns;
    
    if (!extractor->IsSupported(filePath, MetadataType::EXIF))
    {
        return patterns;
    }
    
    EXIFMetadata exif;
    if (extractor->ExtractEXIFMetadata(filePath, exif) != ExtractionResult::Success)
    {
        return patterns;
    }
    
    // Camera information
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
    
    // Shooting parameters
    if (exif.iso.has_value())
    {
        patterns[MetadataPatterns::ISO] = FormatISO(exif.iso.value());
    }
    
    if (exif.aperture.has_value())
    {
        patterns[MetadataPatterns::APERTURE] = FormatAperture(exif.aperture.value());
    }
    
    if (exif.shutterSpeed.has_value())
    {
        patterns[MetadataPatterns::SHUTTER] = FormatShutterSpeed(exif.shutterSpeed.value());
    }
    
    if (exif.focalLength.has_value())
    {
        patterns[MetadataPatterns::FOCAL] = std::to_wstring(static_cast<int>(exif.focalLength.value())) + L"mm";
    }
    
    if (exif.flash.has_value())
    {
        patterns[MetadataPatterns::FLASH] = FormatFlash(exif.flash.value());
    }
    
    // Image properties
    if (exif.width.has_value())
    {
        patterns[MetadataPatterns::WIDTH] = std::to_wstring(exif.width.value());
    }
    
    if (exif.height.has_value())
    {
        patterns[MetadataPatterns::HEIGHT] = std::to_wstring(exif.height.value());
    }
    
    // Author and copyright
    if (exif.author.has_value())
    {
        patterns[MetadataPatterns::AUTHOR] = exif.author.value();
    }
    
    if (exif.copyright.has_value())
    {
        patterns[MetadataPatterns::COPYRIGHT] = exif.copyright.value();
    }
    
    // Location
    if (exif.latitude.has_value())
    {
        patterns[MetadataPatterns::LATITUDE] = FormatCoordinate(exif.latitude.value(), true);
    }
    
    if (exif.longitude.has_value())
    {
        patterns[MetadataPatterns::LONGITUDE] = FormatCoordinate(exif.longitude.value(), false);
    }
    
    // Date patterns
    if (exif.dateTaken.has_value())
    {
        AddDatePatterns(exif.dateTaken.value(), DatePatternSuffixes::DATE_TAKEN_PREFIX, patterns);
    }
    
    return patterns;
}

MetadataPatternMap MetadataPatternExtractor::ExtractXMPPatterns(const std::wstring& filePath)
{
    MetadataPatternMap patterns;
    
    if (!extractor->IsSupported(filePath, MetadataType::XMP))
    {
        return patterns;
    }
    
    XMPMetadata xmp;
    if (extractor->ExtractXMPMetadata(filePath, xmp) != ExtractionResult::Success)
    {
        return patterns;
    }
    
    // Author and copyright
    if (xmp.creator.has_value())
    {
        patterns[MetadataPatterns::AUTHOR] = xmp.creator.value();
    }
    
    if (xmp.rights.has_value())
    {
        patterns[MetadataPatterns::COPYRIGHT] = xmp.rights.value();
    }
    
    if (xmp.title.has_value())
    {
        patterns[MetadataPatterns::TITLE] = xmp.title.value();
    }
    
    if (xmp.subject.has_value())
    {
        // Join keywords with semicolons
        std::wstring keywords;
        const auto& subjectVector = xmp.subject.value();
        for (size_t i = 0; i < subjectVector.size(); ++i)
        {
            if (i > 0) keywords += L"; ";
            keywords += subjectVector[i];
        }
        patterns[MetadataPatterns::SUBJECT] = keywords;
    }
    
    // Date patterns
    if (xmp.createDate.has_value())
    {
        AddDatePatterns(xmp.createDate.value(), DatePatternSuffixes::CREATE_PREFIX, patterns);
    }
    
    if (xmp.modifyDate.has_value())
    {
        AddDatePatterns(xmp.modifyDate.value(), DatePatternSuffixes::MODIFY_PREFIX, patterns);
    }
    
    if (xmp.metadataDate.has_value())
    {
        AddDatePatterns(xmp.metadataDate.value(), DatePatternSuffixes::METADATA_PREFIX, patterns);
    }
    
    return patterns;
}

void MetadataPatternExtractor::AddDatePatterns(
    const SYSTEMTIME& date,
    const std::wstring& prefix,
    MetadataPatternMap& patterns)
{
    // Full date-time format
    patterns[prefix + DatePatternSuffixes::DATE_TIME] = FormatSystemTime(date);
    
    // Date only
    patterns[prefix + DatePatternSuffixes::DATE] = std::format(L"{:04d}-{:02d}-{:02d}",
        date.wYear, date.wMonth, date.wDay);
    
    // Individual components
    patterns[prefix + DatePatternSuffixes::YEAR] = std::to_wstring(date.wYear);
    patterns[prefix + DatePatternSuffixes::MONTH] = std::format(L"{:02d}", date.wMonth);
    patterns[prefix + DatePatternSuffixes::DAY] = std::format(L"{:02d}", date.wDay);
    patterns[prefix + DatePatternSuffixes::HOUR] = std::format(L"{:02d}", date.wHour);
    patterns[prefix + DatePatternSuffixes::MINUTE] = std::format(L"{:02d}", date.wMinute);
    patterns[prefix + DatePatternSuffixes::SECOND] = std::format(L"{:02d}", date.wSecond);
    
    // Month names
    static const std::wstring monthNames[] = {
        L"Jan", L"Feb", L"Mar", L"Apr", L"May", L"Jun",
        L"Jul", L"Aug", L"Sep", L"Oct", L"Nov", L"Dec"
    };
    
    if (date.wMonth >= 1 && date.wMonth <= 12)
    {
        patterns[prefix + DatePatternSuffixes::MONTH_NAME] = monthNames[date.wMonth - 1];
    }
}

std::wstring MetadataPatternExtractor::FormatAperture(double aperture)
{
    return std::format(L"f_{:.1f}", aperture);
}

std::wstring MetadataPatternExtractor::FormatShutterSpeed(double speed)
{
    if (speed < 1.0)
    {
        int denominator = static_cast<int>(std::round(1.0 / speed));
        return std::format(L"1_{}", denominator);
    }
    else
    {
        return std::format(L"{:.1f}s", speed);
    }
}

std::wstring MetadataPatternExtractor::FormatISO(int64_t iso)
{
    return DatePatternSuffixes::ISO_PREFIX + std::to_wstring(iso);
}

std::wstring MetadataPatternExtractor::FormatFlash(int64_t flashValue)
{
    return (flashValue & 1) ? L"Flash" : L"NoFlash";
}

std::wstring MetadataPatternExtractor::FormatCoordinate(double coord, bool isLatitude)
{
    wchar_t direction = isLatitude ? (coord >= 0 ? L'N' : L'S') : (coord >= 0 ? L'E' : L'W');
    double absCoord = std::abs(coord);
    int degrees = static_cast<int>(absCoord);
    double minutes = (absCoord - degrees) * 60.0;
    
    return std::format(L"{:d}°{:.2f}'{}", degrees, minutes, direction);
}

std::wstring MetadataPatternExtractor::FormatSystemTime(const SYSTEMTIME& st)
{
    return std::format(L"{:04d}-{:02d}-{:02d} {:02d}:{:02d}:{:02d}",
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
}

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
            MetadataPatterns::ALTITUDE,
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
            MetadataPatterns::CREATE_DATE_SS,
            MetadataPatterns::MODIFY_DATE_YYYY,
            MetadataPatterns::MODIFY_DATE_YY,
            MetadataPatterns::MODIFY_DATE_MM,
            MetadataPatterns::MODIFY_DATE_DD,
            MetadataPatterns::MODIFY_DATE_HH,
            MetadataPatterns::MODIFY_DATE_mm,
            MetadataPatterns::MODIFY_DATE_SS,
            MetadataPatterns::METADATA_DATE_YYYY,
            MetadataPatterns::METADATA_DATE_YY,
            MetadataPatterns::METADATA_DATE_MM,
            MetadataPatterns::METADATA_DATE_DD,
            MetadataPatterns::METADATA_DATE_HH,
            MetadataPatterns::METADATA_DATE_mm,
            MetadataPatterns::METADATA_DATE_SS
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
    
    // Remove duplicates
    std::sort(allPatterns.begin(), allPatterns.end());
    allPatterns.erase(std::unique(allPatterns.begin(), allPatterns.end()), allPatterns.end());
    
    return allPatterns;
}

// Static methods for backward compatibility
MetadataPatternMap MetadataPatternExtractor::ExtractPatternsStatic(
    const std::wstring& filePath,
    MetadataType type)
{
    static auto extractor = GetDefaultExtractor();
    MetadataPatternExtractor instance(extractor);
    return instance.ExtractPatterns(filePath, type);
}

bool MetadataPatternExtractor::IsSupportedStatic(
    const std::wstring& filePath,
    MetadataType type)
{
    static auto extractor = GetDefaultExtractor();
    MetadataPatternExtractor instance(extractor);
    return instance.IsSupported(filePath, type);
}