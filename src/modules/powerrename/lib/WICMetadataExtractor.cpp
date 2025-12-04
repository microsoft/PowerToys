// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "WICMetadataExtractor.h"
#include "MetadataFormatHelper.h"
#include <algorithm>
#include <sstream>
#include <iomanip>
#include <cwctype>
#include <comdef.h>
#include <shlwapi.h>

using namespace PowerRenameLib;

namespace
{
    // Documentation: https://learn.microsoft.com/en-us/windows/win32/wic/-wic-native-image-format-metadata-queries
    
    // ========================================================================
    // EXTENSIBLE METADATA PATH SYSTEM
    // ========================================================================
    // Different image formats store EXIF metadata under different root paths:
    // - JPEG/TIFF: /app1/ifd/... (metadata in APP1 marker)
    // - HEIC/HEIF/TIFF: /ifd/... (metadata directly in IFD)
    // - Future formats (PNG, WebP) can be added by extending the root path vectors
    //
    // The EXIF tag IDs (e.g., {ushort=36867} for DateTimeOriginal) are identical
    // across all formats - only the root path differs.
    // ========================================================================

    // Root paths for IFD-based metadata (main IFD, not EXIF sub-IFD)
    // Used for: DateTime, Make, Model, Orientation, Artist, Copyright
    const std::vector<std::wstring> IFD_ROOT_PATHS = {
        L"/app1/ifd",   // JPEG (APP1 marker)
        L"/ifd"         // HEIC/HEIF/TIFF (direct IFD)
    };

    // Root paths for EXIF sub-IFD metadata
    // Used for: DateTimeOriginal, DateTimeDigitized, ISO, Aperture, ShutterSpeed, etc.
    const std::vector<std::wstring> EXIF_ROOT_PATHS = {
        L"/app1/ifd/exif",  // JPEG (APP1 marker -> EXIF sub-IFD)
        L"/ifd/exif"        // HEIC/HEIF/TIFF (direct EXIF sub-IFD)
    };

    // Root paths for GPS sub-IFD metadata
    // Used for: GPSLatitude, GPSLongitude, GPSAltitude, etc.
    const std::vector<std::wstring> GPS_ROOT_PATHS = {
        L"/app1/ifd/gps",   // JPEG (APP1 marker -> GPS sub-IFD)
        L"/ifd/gps"         // HEIC/HEIF/TIFF (direct GPS sub-IFD)
    };

    // ========================================================================
    // EXIF TAG IDs (identical across all formats)
    // ========================================================================
    
    // Date/Time tags
    const std::wstring TAG_DATE_TAKEN = L"{ushort=36867}";       // DateTimeOriginal (EXIF sub-IFD)
    const std::wstring TAG_DATE_DIGITIZED = L"{ushort=36868}";   // DateTimeDigitized (EXIF sub-IFD)
    const std::wstring TAG_DATE_MODIFIED = L"{ushort=306}";      // DateTime (main IFD)
    
    // Camera tags (main IFD)
    const std::wstring TAG_CAMERA_MAKE = L"{ushort=271}";        // Make
    const std::wstring TAG_CAMERA_MODEL = L"{ushort=272}";       // Model
    
    // Camera tags (EXIF sub-IFD)
    const std::wstring TAG_LENS_MODEL = L"{ushort=42036}";       // LensModel
    const std::wstring TAG_ISO = L"{ushort=34855}";              // ISOSpeedRatings
    const std::wstring TAG_APERTURE = L"{ushort=33437}";         // FNumber
    const std::wstring TAG_SHUTTER_SPEED = L"{ushort=33434}";    // ExposureTime
    const std::wstring TAG_FOCAL_LENGTH = L"{ushort=37386}";     // FocalLength
    const std::wstring TAG_EXPOSURE_BIAS = L"{ushort=37380}";    // ExposureBiasValue
    const std::wstring TAG_FLASH = L"{ushort=37385}";            // Flash
    const std::wstring TAG_COLOR_SPACE = L"{ushort=40961}";      // ColorSpace
    const std::wstring TAG_WIDTH = L"{ushort=40962}";            // PixelXDimension
    const std::wstring TAG_HEIGHT = L"{ushort=40963}";           // PixelYDimension
    
    // Image properties (main IFD)
    const std::wstring TAG_ORIENTATION = L"{ushort=274}";        // Orientation
    const std::wstring TAG_ARTIST = L"{ushort=315}";             // Artist
    const std::wstring TAG_COPYRIGHT = L"{ushort=33432}";        // Copyright
    
    // GPS tags (GPS sub-IFD)
    const std::wstring TAG_GPS_LATITUDE = L"{ushort=2}";         // GPSLatitude
    const std::wstring TAG_GPS_LATITUDE_REF = L"{ushort=1}";     // GPSLatitudeRef
    const std::wstring TAG_GPS_LONGITUDE = L"{ushort=4}";        // GPSLongitude
    const std::wstring TAG_GPS_LONGITUDE_REF = L"{ushort=3}";    // GPSLongitudeRef
    const std::wstring TAG_GPS_ALTITUDE = L"{ushort=6}";         // GPSAltitude
    const std::wstring TAG_GPS_ALTITUDE_REF = L"{ushort=5}";     // GPSAltitudeRef

    // Helper function to build full metadata paths by combining root paths with tag IDs
    std::vector<std::wstring> BuildMetadataPaths(const std::vector<std::wstring>& rootPaths, const std::wstring& tagId)
    {
        std::vector<std::wstring> paths;
        paths.reserve(rootPaths.size());
        for (const auto& root : rootPaths)
        {
            paths.push_back(root + L"/" + tagId);
        }
        return paths;
    }
    
    
    // Documentation: https://developer.adobe.com/xmp/docs/XMPNamespaces/xmp/
    // Based on actual WIC path format discovered through enumeration
    // XMP Basic schema - xmp: namespace
    const std::wstring XMP_CREATE_DATE = L"/xmp/xmp:CreateDate";        // XMP Create Date
    const std::wstring XMP_MODIFY_DATE = L"/xmp/xmp:ModifyDate";        // XMP Modify Date
    const std::wstring XMP_METADATA_DATE = L"/xmp/xmp:MetadataDate";    // XMP Metadata Date
    const std::wstring XMP_CREATOR_TOOL = L"/xmp/xmp:CreatorTool";      // XMP Creator Tool
    
    // Dublin Core schema - dc: namespace
    // Note: For language alternatives like title/description, we need to append /x-default
    const std::wstring XMP_DC_TITLE = L"/xmp/dc:title/x-default";             // Title (default language)
    const std::wstring XMP_DC_DESCRIPTION = L"/xmp/dc:description/x-default"; // Description (default language)
    const std::wstring XMP_DC_CREATOR = L"/xmp/dc:creator";                   // Creator/Author
    const std::wstring XMP_DC_SUBJECT = L"/xmp/dc:subject";                   // Subject/Keywords (array)
    
    // XMP Rights Management schema - xmpRights: namespace
    const std::wstring XMP_RIGHTS = L"/xmp/xmpRights:WebStatement";           // Copyright/Rights
    
    // XMP Media Management schema - xmpMM: namespace
    const std::wstring XMP_MM_DOCUMENT_ID = L"/xmp/xmpMM:DocumentID";                  // Document ID
    const std::wstring XMP_MM_INSTANCE_ID = L"/xmp/xmpMM:InstanceID";                  // Instance ID  
    const std::wstring XMP_MM_ORIGINAL_DOCUMENT_ID = L"/xmp/xmpMM:OriginalDocumentID"; // Original Document ID
    const std::wstring XMP_MM_VERSION_ID = L"/xmp/xmpMM:VersionID";                    // Version ID
    
    
    std::wstring TrimWhitespace(const std::wstring& value)
    {
        const auto first = value.find_first_not_of(L" \t\r\n");
        if (first == std::wstring::npos)
        {
            return {};
        }

        const auto last = value.find_last_not_of(L" \t\r\n");
        return value.substr(first, last - first + 1);
    }

    bool TryParseFixedWidthInt(const std::wstring& source, size_t start, size_t length, int& value)
    {
        if (start + length > source.size())
        {
            return false;
        }

        int result = 0;
        for (size_t i = 0; i < length; ++i)
        {
            const wchar_t ch = source[start + i];
            if (ch < L'0' || ch > L'9')
            {
                return false;
            }

            result = result * 10 + static_cast<int>(ch - L'0');
        }

        value = result;
        return true;
    }

    bool ValidateAndBuildSystemTime(int year, int month, int day, int hour, int minute, int second, int milliseconds, SYSTEMTIME& outTime)
    {
        if (year < 1601 || year > 9999 ||
            month < 1 || month > 12 ||
            day < 1 || day > 31 ||
            hour < 0 || hour > 23 ||
            minute < 0 || minute > 59 ||
            second < 0 || second > 59 ||
            milliseconds < 0 || milliseconds > 999)
        {
            return false;
        }

        SYSTEMTIME candidate{};
        candidate.wYear = static_cast<WORD>(year);
        candidate.wMonth = static_cast<WORD>(month);
        candidate.wDay = static_cast<WORD>(day);
        candidate.wHour = static_cast<WORD>(hour);
        candidate.wMinute = static_cast<WORD>(minute);
        candidate.wSecond = static_cast<WORD>(second);
        candidate.wMilliseconds = static_cast<WORD>(milliseconds);

        FILETIME fileTime{};
        if (!SystemTimeToFileTime(&candidate, &fileTime))
        {
            return false;
        }

        outTime = candidate;
        return true;
    }

    std::optional<SYSTEMTIME> ParseExifDateTime(const std::wstring& date)
    {
        if (date.size() < 19)
        {
            return std::nullopt;
        }

        if (date[4] != L':' || date[7] != L':' ||
            (date[10] != L' ' && date[10] != L'T') ||
            date[13] != L':' || date[16] != L':')
        {
            return std::nullopt;
        }

        int year = 0;
        int month = 0;
        int day = 0;
        int hour = 0;
        int minute = 0;
        int second = 0;

        if (!TryParseFixedWidthInt(date, 0, 4, year) ||
            !TryParseFixedWidthInt(date, 5, 2, month) ||
            !TryParseFixedWidthInt(date, 8, 2, day) ||
            !TryParseFixedWidthInt(date, 11, 2, hour) ||
            !TryParseFixedWidthInt(date, 14, 2, minute) ||
            !TryParseFixedWidthInt(date, 17, 2, second))
        {
            return std::nullopt;
        }

        int milliseconds = 0;
        size_t pos = 19;
        if (pos < date.size() && (date[pos] == L'.' || date[pos] == L','))
        {
            ++pos;
            int digits = 0;
            while (pos < date.size() && std::iswdigit(date[pos]) && digits < 3)
            {
                milliseconds = milliseconds * 10 + static_cast<int>(date[pos] - L'0');
                ++pos;
                ++digits;
            }

            while (digits > 0 && digits < 3)
            {
                milliseconds *= 10;
                ++digits;
            }
        }

        SYSTEMTIME result{};
        if (!ValidateAndBuildSystemTime(year, month, day, hour, minute, second, milliseconds, result))
        {
            return std::nullopt;
        }

        return result;
    }

    std::optional<SYSTEMTIME> ParseIso8601DateTime(const std::wstring& date)
    {
        if (date.size() < 19)
        {
            return std::nullopt;
        }

        size_t separator = date.find(L'T');
        if (separator == std::wstring::npos)
        {
            separator = date.find(L' ');
        }

        if (separator == std::wstring::npos)
        {
            return std::nullopt;
        }

        int year = 0;
        int month = 0;
        int day = 0;
        if (!TryParseFixedWidthInt(date, 0, 4, year) ||
            date[4] != L'-' ||
            !TryParseFixedWidthInt(date, 5, 2, month) ||
            date[7] != L'-' ||
            !TryParseFixedWidthInt(date, 8, 2, day))
        {
            return std::nullopt;
        }

        size_t timePos = separator + 1;
        if (timePos + 7 >= date.size())
        {
            return std::nullopt;
        }

        int hour = 0;
        int minute = 0;
        int second = 0;
        if (!TryParseFixedWidthInt(date, timePos, 2, hour) ||
            date[timePos + 2] != L':' ||
            !TryParseFixedWidthInt(date, timePos + 3, 2, minute) ||
            date[timePos + 5] != L':' ||
            !TryParseFixedWidthInt(date, timePos + 6, 2, second))
        {
            return std::nullopt;
        }

        size_t pos = timePos + 8;
        int milliseconds = 0;
        if (pos < date.size() && (date[pos] == L'.' || date[pos] == L','))
        {
            ++pos;
            int digits = 0;
            while (pos < date.size() && std::iswdigit(date[pos]) && digits < 3)
            {
                milliseconds = milliseconds * 10 + static_cast<int>(date[pos] - L'0');
                ++pos;
                ++digits;
            }

            while (pos < date.size() && std::iswdigit(date[pos]))
            {
                ++pos;
            }

            while (digits > 0 && digits < 3)
            {
                milliseconds *= 10;
                ++digits;
            }
        }

        bool hasOffset = false;
        int offsetMinutes = 0;
        if (pos < date.size())
        {
            const wchar_t tzIndicator = date[pos];
            if (tzIndicator == L'Z' || tzIndicator == L'z')
            {
                hasOffset = true;
                offsetMinutes = 0;
                ++pos;
            }
            else if (tzIndicator == L'+' || tzIndicator == L'-')
            {
                hasOffset = true;
                const int sign = (tzIndicator == L'-') ? -1 : 1;
                ++pos;

                int offsetHours = 0;
                int offsetMins = 0;
                if (!TryParseFixedWidthInt(date, pos, 2, offsetHours))
                {
                    return std::nullopt;
                }
                pos += 2;

                if (pos < date.size() && date[pos] == L':')
                {
                    ++pos;
                }

                if (pos + 1 < date.size() && std::iswdigit(date[pos]) && std::iswdigit(date[pos + 1]))
                {
                    if (!TryParseFixedWidthInt(date, pos, 2, offsetMins))
                    {
                        return std::nullopt;
                    }
                    pos += 2;
                }

                if (offsetHours < 0 || offsetHours > 23 || offsetMins < 0 || offsetMins > 59)
                {
                    return std::nullopt;
                }

                offsetMinutes = sign * (offsetHours * 60 + offsetMins);
            }

            while (pos < date.size() && std::iswspace(date[pos]))
            {
                ++pos;
            }

            if (pos != date.size())
            {
                return std::nullopt;
            }
        }

        SYSTEMTIME baseTime{};
        if (!ValidateAndBuildSystemTime(year, month, day, hour, minute, second, milliseconds, baseTime))
        {
            return std::nullopt;
        }

        if (!hasOffset)
        {
            return baseTime;
        }

        FILETIME utcFileTime{};
        if (!SystemTimeToFileTime(&baseTime, &utcFileTime))
        {
            return std::nullopt;
        }

        ULARGE_INTEGER timeValue{};
        timeValue.LowPart = utcFileTime.dwLowDateTime;
        timeValue.HighPart = utcFileTime.dwHighDateTime;

        constexpr long long TicksPerMinute = 60LL * 10000000LL;
        timeValue.QuadPart -= static_cast<long long>(offsetMinutes) * TicksPerMinute;

        FILETIME adjustedUtc{};
        adjustedUtc.dwLowDateTime = timeValue.LowPart;
        adjustedUtc.dwHighDateTime = timeValue.HighPart;

        FILETIME localFileTime{};
        if (!FileTimeToLocalFileTime(&adjustedUtc, &localFileTime))
        {
            return std::nullopt;
        }

        SYSTEMTIME localTime{};
        if (!FileTimeToSystemTime(&localFileTime, &localTime))
        {
            return std::nullopt;
        }

        return localTime;
    }
// Global WIC factory management with thread-safe access
    CComPtr<IWICImagingFactory> g_wicFactory;
    std::once_flag g_wicInitFlag;
    std::mutex g_wicFactoryMutex;  // Protect access to g_wicFactory
}

WICMetadataExtractor::WICMetadataExtractor()
{
    InitializeWIC();
}

WICMetadataExtractor::~WICMetadataExtractor()
{
    // WIC cleanup handled statically
}

void WICMetadataExtractor::InitializeWIC()
{
    std::call_once(g_wicInitFlag, []() {
        // Don't initialize COM in library code - assume caller has done it
        // Just create the WIC factory
        HRESULT hr = CoCreateInstance(
            CLSID_WICImagingFactory,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_IWICImagingFactory,
            reinterpret_cast<LPVOID*>(&g_wicFactory)
        );

        if (FAILED(hr))
        {
            g_wicFactory = nullptr;
        }
    });
}

CComPtr<IWICImagingFactory> WICMetadataExtractor::GetWICFactory()
{
    std::lock_guard<std::mutex> lock(g_wicFactoryMutex);
    return g_wicFactory;
}

bool WICMetadataExtractor::ExtractEXIFMetadata(
    const std::wstring& filePath,
    EXIFMetadata& outMetadata)
{
    return cache.GetOrLoadEXIF(filePath, outMetadata, [this, &filePath](EXIFMetadata& metadata) {
        return LoadEXIFMetadata(filePath, metadata);
    });
}

bool WICMetadataExtractor::LoadEXIFMetadata(
    const std::wstring& filePath,
    EXIFMetadata& outMetadata)
{
    CComPtr<IWICMetadataQueryReader> reader;

    if (!PathFileExistsW(filePath.c_str()))
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] EXIF metadata extraction failed: File not found - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    auto decoder = CreateDecoder(filePath);
    if (!decoder)
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] EXIF metadata extraction: Unsupported format or unable to create decoder - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    CComPtr<IWICBitmapFrameDecode> frame;
    if (FAILED(decoder->GetFrame(0, &frame)))
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] EXIF metadata extraction failed: WIC decoder error - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    reader = GetMetadataReader(decoder);
    if (!reader)
    {
        // No metadata is not necessarily an error - just means the file has no EXIF data
        return false;
    }

    ExtractAllEXIFFields(reader, outMetadata);
    ExtractGPSData(reader, outMetadata);

    return true;
}

void WICMetadataExtractor::ClearCache()
{
    cache.ClearAll();
}

CComPtr<IWICBitmapDecoder> WICMetadataExtractor::CreateDecoder(const std::wstring& filePath)
{
    auto factory = GetWICFactory();
    if (!factory)
    {
        return nullptr;
    }
    
    CComPtr<IWICBitmapDecoder> decoder;
    HRESULT hr = factory->CreateDecoderFromFilename(
        filePath.c_str(),
        nullptr,
        GENERIC_READ,
        WICDecodeMetadataCacheOnLoad,
        &decoder
    );
    
    if (FAILED(hr))
    {
        return nullptr;
    }
    
    return decoder;
}

CComPtr<IWICMetadataQueryReader> WICMetadataExtractor::GetMetadataReader(IWICBitmapDecoder* decoder)
{
    if (!decoder)
    {
        return nullptr;
    }
    
    CComPtr<IWICBitmapFrameDecode> frame;
    if (FAILED(decoder->GetFrame(0, &frame)))
    {
        return nullptr;
    }
    
    CComPtr<IWICMetadataQueryReader> reader;
    frame->GetMetadataQueryReader(&reader);
    
    return reader;
}

void WICMetadataExtractor::ExtractAllEXIFFields(IWICMetadataQueryReader* reader, EXIFMetadata& metadata)
{
    if (!reader)
        return;
    
    // Extract date/time fields using extensible path system
    // DateTimeOriginal and DateTimeDigitized are in EXIF sub-IFD
    metadata.dateTaken = ReadDateTimeFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_DATE_TAKEN));
    metadata.dateDigitized = ReadDateTimeFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_DATE_DIGITIZED));
    // DateTime is in main IFD
    metadata.dateModified = ReadDateTimeFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_DATE_MODIFIED));
    
    // Extract camera information
    // Make and Model are in main IFD
    metadata.cameraMake = ReadStringFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_CAMERA_MAKE));
    metadata.cameraModel = ReadStringFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_CAMERA_MODEL));
    // LensModel is in EXIF sub-IFD
    metadata.lensModel = ReadStringFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_LENS_MODEL));
    
    // Extract shooting parameters (all in EXIF sub-IFD)
    metadata.iso = ReadIntegerFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_ISO));
    metadata.aperture = ReadDoubleFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_APERTURE));
    metadata.shutterSpeed = ReadDoubleFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_SHUTTER_SPEED));
    metadata.focalLength = ReadDoubleFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_FOCAL_LENGTH));
    metadata.exposureBias = ReadDoubleFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_EXPOSURE_BIAS));
    metadata.flash = ReadIntegerFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_FLASH));
    
    // Extract image properties
    // Width, Height, ColorSpace are in EXIF sub-IFD
    metadata.width = ReadIntegerFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_WIDTH));
    metadata.height = ReadIntegerFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_HEIGHT));
    metadata.colorSpace = ReadIntegerFromPaths(reader, BuildMetadataPaths(EXIF_ROOT_PATHS, TAG_COLOR_SPACE));
    // Orientation is in main IFD
    metadata.orientation = ReadIntegerFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_ORIENTATION));
    
    // Extract author information (main IFD)
    metadata.author = ReadStringFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_ARTIST));
    metadata.copyright = ReadStringFromPaths(reader, BuildMetadataPaths(IFD_ROOT_PATHS, TAG_COPYRIGHT));
}

void WICMetadataExtractor::ExtractGPSData(IWICMetadataQueryReader* reader, EXIFMetadata& metadata)
{
    if (!reader)
    {
        return;
    }

    // Use extensible path system for GPS data extraction
    auto lat = ReadMetadataFromPaths(reader, BuildMetadataPaths(GPS_ROOT_PATHS, TAG_GPS_LATITUDE));
    auto lon = ReadMetadataFromPaths(reader, BuildMetadataPaths(GPS_ROOT_PATHS, TAG_GPS_LONGITUDE));
    auto latRef = ReadMetadataFromPaths(reader, BuildMetadataPaths(GPS_ROOT_PATHS, TAG_GPS_LATITUDE_REF));
    auto lonRef = ReadMetadataFromPaths(reader, BuildMetadataPaths(GPS_ROOT_PATHS, TAG_GPS_LONGITUDE_REF));

    if (lat && lon)
    {
        PropVariantValue emptyLatRef;
        PropVariantValue emptyLonRef;

        const PROPVARIANT& latRefVar = latRef ? latRef->Get() : emptyLatRef.Get();
        const PROPVARIANT& lonRefVar = lonRef ? lonRef->Get() : emptyLonRef.Get();

        auto coords = MetadataFormatHelper::ParseGPSCoordinates(
            lat->Get(),
            lon->Get(),
            latRefVar,
            lonRefVar);

        metadata.latitude = coords.first;
        metadata.longitude = coords.second;
    }

    auto alt = ReadMetadataFromPaths(reader, BuildMetadataPaths(GPS_ROOT_PATHS, TAG_GPS_ALTITUDE));
    if (alt)
    {
        metadata.altitude = MetadataFormatHelper::ParseGPSRational(alt->Get());
    }
}


std::optional<SYSTEMTIME> WICMetadataExtractor::ReadDateTime(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar)
    {
        return std::nullopt;
    }

    std::wstring rawValue;
    const PROPVARIANT& variant = propVar->Get();

    switch (variant.vt)
    {
    case VT_LPWSTR:
        if (variant.pwszVal)
        {
            rawValue = variant.pwszVal;
        }
        break;
    case VT_BSTR:
        if (variant.bstrVal)
        {
            rawValue = variant.bstrVal;
        }
        break;
    case VT_LPSTR:
        if (variant.pszVal)
        {
            const int size = MultiByteToWideChar(CP_UTF8, 0, variant.pszVal, -1, nullptr, 0);
            if (size > 1)
            {
                rawValue.resize(static_cast<size_t>(size) - 1);
                MultiByteToWideChar(CP_UTF8, 0, variant.pszVal, -1, &rawValue[0], size);
            }
        }
        break;
    default:
        break;
    }

    if (rawValue.empty())
    {
        return std::nullopt;
    }

    const std::wstring normalized = TrimWhitespace(rawValue);
    if (normalized.empty())
    {
        return std::nullopt;
    }

    if (auto exifDate = ParseExifDateTime(normalized))
    {
        return exifDate;
    }

    if (auto isoDate = ParseIso8601DateTime(normalized))
    {
        return isoDate;
    }

    return std::nullopt;
}

std::optional<std::wstring> WICMetadataExtractor::ReadString(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;

    std::wstring result;
    switch (propVar->Get().vt)
    {
    case VT_LPWSTR:
        if (propVar->Get().pwszVal)
            result = propVar->Get().pwszVal;
        break;
    case VT_BSTR:
        if (propVar->Get().bstrVal)
            result = propVar->Get().bstrVal;
        break;
    case VT_LPSTR:
        if (propVar->Get().pszVal)
        {
            int size = MultiByteToWideChar(CP_UTF8, 0, propVar->Get().pszVal, -1, nullptr, 0);
            if (size > 1)
            {
                result.resize(static_cast<size_t>(size) - 1);
                MultiByteToWideChar(CP_UTF8, 0, propVar->Get().pszVal, -1, &result[0], size);
            }
        }
        break;
    }
    
    
    // Trim whitespace from both ends
    if (!result.empty())
    {
        size_t start = result.find_first_not_of(L" \t\r\n");
        size_t end = result.find_last_not_of(L" \t\r\n");
        if (start != std::wstring::npos && end != std::wstring::npos)
        {
            result = result.substr(start, end - start + 1);
        }
        else if (start == std::wstring::npos)
        {
            result.clear();
        }
    }
    
    return result.empty() ? std::nullopt : std::make_optional(result);
}

std::optional<int64_t> WICMetadataExtractor::ReadInteger(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;

    int64_t result = 0;
    switch (propVar->Get().vt)
    {
        case VT_I1: result = propVar->Get().cVal; break;
        case VT_I2: result = propVar->Get().iVal; break;
        case VT_I4: result = propVar->Get().lVal; break;
        case VT_I8: result = propVar->Get().hVal.QuadPart; break;
        case VT_UI1: result = propVar->Get().bVal; break;
        case VT_UI2: result = propVar->Get().uiVal; break;
        case VT_UI4: result = propVar->Get().ulVal; break;
        case VT_UI8: result = static_cast<int64_t>(propVar->Get().uhVal.QuadPart); break;
        default:
            return std::nullopt;
    }
    
    return result;
}

std::optional<double> WICMetadataExtractor::ReadDouble(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;

    double result = 0.0;
    
    // Check if this is an ExposureBias field by looking for the tag ID in the path
    // ExposureBias (EXIF tag 37380) uses SRATIONAL type (signed rational)
    bool isExposureBias = (path.find(TAG_EXPOSURE_BIAS) != std::wstring::npos);
    
    switch (propVar->Get().vt)
    {
        case VT_R4: 
            result = static_cast<double>(propVar->Get().fltVal); 
            break;
        case VT_R8: 
            result = propVar->Get().dblVal; 
            break;
        case VT_UI1 | VT_VECTOR:
        case VT_UI4 | VT_VECTOR:
            // Handle rational number (common for EXIF values)
            // Rational data is stored as 8 bytes: 4-byte numerator + 4-byte denominator
            if (propVar->Get().caub.cElems >= 8)
            {
                // ExposureBias uses SRATIONAL type (signed rational)
                // which can represent negative values like -0.33 EV for exposure compensation.
                // Most other EXIF fields use RATIONAL type (unsigned) for values like aperture, shutter speed.
                if (isExposureBias)
                {
                    // Parse as signed rational: int32_t / int32_t
                    result = MetadataFormatHelper::ParseSingleSRational(propVar->Get().caub.pElems, 0);
                    break;
                }
                else
                {
                    // Parse as unsigned rational: uint32_t / uint32_t
                    // First check if denominator is valid (non-zero) to avoid division by zero
                    const uint8_t* bytes = propVar->Get().caub.pElems;
                    uint32_t denominator = static_cast<uint32_t>(bytes[4]) |
                                         (static_cast<uint32_t>(bytes[5]) << 8) |
                                         (static_cast<uint32_t>(bytes[6]) << 16) |
                                         (static_cast<uint32_t>(bytes[7]) << 24);
                
                    if (denominator != 0)
                    {
                        result = MetadataFormatHelper::ParseSingleRational(propVar->Get().caub.pElems, 0);
                        break;
                    }
                }
            }
            return std::nullopt;
        default:
            // Try integer conversion
            switch (propVar->Get().vt)
            {
            case VT_I1: result = static_cast<double>(propVar->Get().cVal); break;
            case VT_I2: result = static_cast<double>(propVar->Get().iVal); break;
            case VT_I4: result = static_cast<double>(propVar->Get().lVal); break;
            case VT_I8: 
                {
                    // ExposureBias may be stored as VT_I8 in some WIC implementations
                    // It represents a signed rational (SRATIONAL) packed into a 64-bit integer
                    if (isExposureBias)
                    {
                        // Parse signed rational from int64: low 32 bits = numerator, high 32 bits = denominator
                        // Some implementations may reverse the order, so we try both
                        int32_t numerator = static_cast<int32_t>(propVar->Get().hVal.QuadPart & 0xFFFFFFFF);
                        int32_t denominator = static_cast<int32_t>(propVar->Get().hVal.QuadPart >> 32);
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            // Try reversed order: high 32 bits = numerator, low 32 bits = denominator
                            numerator = static_cast<int32_t>(propVar->Get().hVal.QuadPart >> 32);
                            denominator = static_cast<int32_t>(propVar->Get().hVal.QuadPart & 0xFFFFFFFF);
                            if (denominator != 0)
                            {
                                result = static_cast<double>(numerator) / static_cast<double>(denominator);
                            }
                            else
                            {
                                result = 0.0; // Default to 0 if both attempts fail
                            }
                        }
                    }
                    else
                    {
                        // For other fields, treat VT_I8 as a simple 64-bit integer
                        result = static_cast<double>(propVar->Get().hVal.QuadPart);
                    }
                }
                break;
            case VT_UI1: result = static_cast<double>(propVar->Get().bVal); break;
            case VT_UI2: result = static_cast<double>(propVar->Get().uiVal); break;
            case VT_UI4: result = static_cast<double>(propVar->Get().ulVal); break;
            case VT_UI8: 
                {
                    // ExposureBias may be stored as VT_UI8 in some WIC implementations
                    // Even though it's unsigned, we need to reinterpret it as signed for SRATIONAL
                    if (isExposureBias)
                    {
                        // Parse signed rational from uint64 (reinterpret as signed)
                        // Low 32 bits = numerator, high 32 bits = denominator
                        int32_t numerator = static_cast<int32_t>(propVar->Get().uhVal.QuadPart & 0xFFFFFFFF);
                        int32_t denominator = static_cast<int32_t>(propVar->Get().uhVal.QuadPart >> 32);
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            // Try reversed order: high 32 bits = numerator, low 32 bits = denominator
                            numerator = static_cast<int32_t>(propVar->Get().uhVal.QuadPart >> 32);
                            denominator = static_cast<int32_t>(propVar->Get().uhVal.QuadPart & 0xFFFFFFFF);
                            if (denominator != 0)
                            {
                                result = static_cast<double>(numerator) / static_cast<double>(denominator);
                            }
                            else
                            {
                                result = 0.0; // Default to 0 if both attempts fail
                            }
                        }
                    }
                    else
                    {
                        // For other EXIF rational fields (unsigned), try both byte orders to handle different encodings
                        // First try: low 32 bits = numerator, high 32 bits = denominator
                        uint32_t numerator = static_cast<uint32_t>(propVar->Get().uhVal.QuadPart & 0xFFFFFFFF);
                        uint32_t denominator = static_cast<uint32_t>(propVar->Get().uhVal.QuadPart >> 32);
                    
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            // Second try: high 32 bits = numerator, low 32 bits = denominator
                            numerator = static_cast<uint32_t>(propVar->Get().uhVal.QuadPart >> 32);
                            denominator = static_cast<uint32_t>(propVar->Get().uhVal.QuadPart & 0xFFFFFFFF);
                            if (denominator != 0)
                            {
                                result = static_cast<double>(numerator) / static_cast<double>(denominator);
                            }
                            else
                            {
                                // Fall back to treating as regular integer if denominator is 0
                                result = static_cast<double>(propVar->Get().uhVal.QuadPart);
                            }
                        }
                    }
                }
                break;
            default:
                return std::nullopt;
            }
    }
    
    return result;
}

std::optional<PropVariantValue> WICMetadataExtractor::ReadMetadata(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    if (!reader)
        return std::nullopt;
    
    PropVariantValue value;

    HRESULT hr = reader->GetMetadataByName(path.c_str(), value.GetAddressOf());
    if (SUCCEEDED(hr))
    {
        return std::optional<PropVariantValue>(std::move(value));
    }

    return std::nullopt;
}

// Multi-path reading methods for cross-format compatibility
// These methods try each path in order until metadata is found

std::optional<SYSTEMTIME> WICMetadataExtractor::ReadDateTimeFromPaths(
    IWICMetadataQueryReader* reader,
    const std::vector<std::wstring>& paths)
{
    for (const auto& path : paths)
    {
        auto result = ReadDateTime(reader, path);
        if (result.has_value())
        {
            return result;
        }
    }
    return std::nullopt;
}

std::optional<std::wstring> WICMetadataExtractor::ReadStringFromPaths(
    IWICMetadataQueryReader* reader,
    const std::vector<std::wstring>& paths)
{
    for (const auto& path : paths)
    {
        auto result = ReadString(reader, path);
        if (result.has_value())
        {
            return result;
        }
    }
    return std::nullopt;
}

std::optional<int64_t> WICMetadataExtractor::ReadIntegerFromPaths(
    IWICMetadataQueryReader* reader,
    const std::vector<std::wstring>& paths)
{
    for (const auto& path : paths)
    {
        auto result = ReadInteger(reader, path);
        if (result.has_value())
        {
            return result;
        }
    }
    return std::nullopt;
}

std::optional<double> WICMetadataExtractor::ReadDoubleFromPaths(
    IWICMetadataQueryReader* reader,
    const std::vector<std::wstring>& paths)
{
    for (const auto& path : paths)
    {
        auto result = ReadDouble(reader, path);
        if (result.has_value())
        {
            return result;
        }
    }
    return std::nullopt;
}

std::optional<PropVariantValue> WICMetadataExtractor::ReadMetadataFromPaths(
    IWICMetadataQueryReader* reader,
    const std::vector<std::wstring>& paths)
{
    for (const auto& path : paths)
    {
        auto result = ReadMetadata(reader, path);
        if (result.has_value())
        {
            return result;
        }
    }
    return std::nullopt;
}

// GPS parsing functions have been moved to MetadataFormatHelper for better testability

bool WICMetadataExtractor::ExtractXMPMetadata(
    const std::wstring& filePath,
    XMPMetadata& outMetadata)
{
    return cache.GetOrLoadXMP(filePath, outMetadata, [this, &filePath](XMPMetadata& metadata) {
        return LoadXMPMetadata(filePath, metadata);
    });
}

bool WICMetadataExtractor::LoadXMPMetadata(
    const std::wstring& filePath,
    XMPMetadata& outMetadata)
{
    if (!PathFileExistsW(filePath.c_str()))
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] XMP metadata extraction failed: File not found - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    auto decoder = CreateDecoder(filePath);
    if (!decoder)
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] XMP metadata extraction: Unsupported format or unable to create decoder - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    CComPtr<IWICBitmapFrameDecode> frame;
    if (FAILED(decoder->GetFrame(0, &frame)))
    {
#ifdef _DEBUG
        std::wstring msg = L"[PowerRename] XMP metadata extraction failed: WIC decoder error - " + filePath + L"\n";
        OutputDebugStringW(msg.c_str());
#endif
        return false;
    }

    CComPtr<IWICMetadataQueryReader> rootReader;
    if (FAILED(frame->GetMetadataQueryReader(&rootReader)))
    {
        // No metadata is not necessarily an error - just means the file has no XMP data
        return false;
    }

    ExtractAllXMPFields(rootReader, outMetadata);

    return true;
}

// Batch extraction method implementations
void WICMetadataExtractor::ExtractAllXMPFields(IWICMetadataQueryReader* reader, XMPMetadata& metadata)
{
    if (!reader)
        return;
    
    // XMP Basic schema - xmp: namespace
    metadata.creatorTool = ReadString(reader, XMP_CREATOR_TOOL);
    metadata.createDate = ReadDateTime(reader, XMP_CREATE_DATE);
    metadata.modifyDate = ReadDateTime(reader, XMP_MODIFY_DATE);
    metadata.metadataDate = ReadDateTime(reader, XMP_METADATA_DATE);
    
    // Dublin Core schema - dc: namespace
    metadata.title = ReadString(reader, XMP_DC_TITLE);
    metadata.description = ReadString(reader, XMP_DC_DESCRIPTION);
    metadata.creator = ReadString(reader, XMP_DC_CREATOR);
    
    // For dc:subject, we need to handle the array structure
    // Try to read individual elements
    // XMP allows for large arrays, but we limit to a reasonable number to avoid performance issues
    constexpr int MAX_XMP_SUBJECTS = 50;
    std::vector<std::wstring> subjects;
    for (int i = 0; i < MAX_XMP_SUBJECTS; ++i)
    {
        std::wstring subjectPath = L"/xmp/dc:subject/{ulong=" + std::to_wstring(i) + L"}";
        auto subject = ReadString(reader, subjectPath);
        if (subject.has_value())
        {
            subjects.push_back(subject.value());
        }
        else
        {
            break;  // No more subjects
        }
    }
    if (!subjects.empty())
    {
        metadata.subject = subjects;
    }
    
    // XMP Rights Management schema
    metadata.rights = ReadString(reader, XMP_RIGHTS);
    
    // XMP Media Management schema - xmpMM: namespace
    metadata.documentID = ReadString(reader, XMP_MM_DOCUMENT_ID);
    metadata.instanceID = ReadString(reader, XMP_MM_INSTANCE_ID);
    metadata.originalDocumentID = ReadString(reader, XMP_MM_ORIGINAL_DOCUMENT_ID);
    metadata.versionID = ReadString(reader, XMP_MM_VERSION_ID);
}









