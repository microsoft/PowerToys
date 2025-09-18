// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "WICMetadataExtractor.h"
#include <algorithm>
#include <sstream>
#include <iomanip>
#include <comdef.h>
#include <shlwapi.h>

using namespace PowerRenameLib;

namespace
{
    // Documentation: https://learn.microsoft.com/en-us/windows/win32/wic/-wic-native-image-format-metadata-queries

    // WIC metadata property paths
    const std::wstring EXIF_DATE_TAKEN = L"/app1/ifd/exif/{ushort=36867}";      // DateTimeOriginal
    const std::wstring EXIF_DATE_DIGITIZED = L"/app1/ifd/exif/{ushort=36868}";  // DateTimeDigitized  
    const std::wstring EXIF_DATE_MODIFIED = L"/app1/ifd/{ushort=306}";           // DateTime
    const std::wstring EXIF_CAMERA_MAKE = L"/app1/ifd/{ushort=271}";            // Make
    const std::wstring EXIF_CAMERA_MODEL = L"/app1/ifd/{ushort=272}";           // Model
    const std::wstring EXIF_LENS_MODEL = L"/app1/ifd/exif/{ushort=42036}";      // LensModel
    const std::wstring EXIF_ISO = L"/app1/ifd/exif/{ushort=34855}";             // ISOSpeedRatings
    const std::wstring EXIF_APERTURE = L"/app1/ifd/exif/{ushort=33437}";        // FNumber
    const std::wstring EXIF_SHUTTER_SPEED = L"/app1/ifd/exif/{ushort=33434}";   // ExposureTime
    const std::wstring EXIF_FOCAL_LENGTH = L"/app1/ifd/exif/{ushort=37386}";    // FocalLength
    const std::wstring EXIF_EXPOSURE_BIAS = L"/app1/ifd/exif/{ushort=37380}";   // ExposureBiasValue
    const std::wstring EXIF_FLASH = L"/app1/ifd/exif/{ushort=37385}";           // Flash
    const std::wstring EXIF_ORIENTATION = L"/app1/ifd/{ushort=274}";            // Orientation
    const std::wstring EXIF_COLOR_SPACE = L"/app1/ifd/exif/{ushort=40961}";     // ColorSpace
    const std::wstring EXIF_WIDTH = L"/app1/ifd/exif/{ushort=40962}";           // PixelXDimension - actual image width
    const std::wstring EXIF_HEIGHT = L"/app1/ifd/exif/{ushort=40963}";          // PixelYDimension - actual image height
    const std::wstring EXIF_ARTIST = L"/app1/ifd/{ushort=315}";                 // Artist
    const std::wstring EXIF_COPYRIGHT = L"/app1/ifd/{ushort=33432}";            // Copyright
    
    // GPS paths
    const std::wstring GPS_LATITUDE = L"/app1/ifd/gps/{ushort=2}";              // GPSLatitude
    const std::wstring GPS_LATITUDE_REF = L"/app1/ifd/gps/{ushort=1}";          // GPSLatitudeRef
    const std::wstring GPS_LONGITUDE = L"/app1/ifd/gps/{ushort=4}";             // GPSLongitude
    const std::wstring GPS_LONGITUDE_REF = L"/app1/ifd/gps/{ushort=3}";         // GPSLongitudeRef
    const std::wstring GPS_ALTITUDE = L"/app1/ifd/gps/{ushort=6}";              // GPSAltitude
    const std::wstring GPS_ALTITUDE_REF = L"/app1/ifd/gps/{ushort=5}";          // GPSAltitudeRef
    
    
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
    
    
    // Global WIC factory management
    CComPtr<IWICImagingFactory> g_wicFactory;
    std::once_flag g_wicInitFlag;
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

void WICMetadataExtractor::CleanupWIC()
{
    g_wicFactory = nullptr;
    // Don't call CoUninitialize - caller is responsible for COM lifecycle
}

CComPtr<IWICImagingFactory> WICMetadataExtractor::GetWICFactory()
{
    return g_wicFactory;
}

ExtractionResult WICMetadataExtractor::ExtractEXIFMetadata(
    const std::wstring& filePath,
    EXIFMetadata& outMetadata)
{
    CComPtr<IWICMetadataQueryReader> reader;

     // Check if file exists
    if (!PathFileExistsW(filePath.c_str()))
    {
        return ExtractionResult::FileNotFound;
    }

    auto decoder = CreateDecoder(filePath);
    if (!decoder)
    {
        return ExtractionResult::UnsupportedFormat;
    }

    // Get first frame
    CComPtr<IWICBitmapFrameDecode> frame;
    if (FAILED(decoder->GetFrame(0, &frame)))
    {
        return ExtractionResult::DecoderError;
    }

    // Get metadata reader
    reader = GetMetadataReader(decoder);
    if (!reader)
    {
        return ExtractionResult::MetadataNotFound;
    }
    
    // Extract all EXIF fields in batch
    ExtractAllEXIFFields(reader, outMetadata);
    
    // Extract GPS data
    ExtractGPSData(reader, outMetadata);
    
    return ExtractionResult::Success;
}

bool WICMetadataExtractor::IsSupported(const std::wstring& filePath, MetadataType metadataType)
{
    // First check if WIC can decode the file at all
    auto decoder = CreateDecoder(filePath);
    if (!decoder)
    {
        return false;
    }

    // Get metadata reader to check if specific metadata type is present
    auto reader = GetMetadataReader(decoder);
    if (!reader)
    {
        return false;
    }

    // Check for presence of specific metadata type based on known paths
    std::wstring testPath;
    switch (metadataType)
    {
    case MetadataType::EXIF:
        // Test for common EXIF paths
        testPath = L"/app1/ifd/exif/";
        break;
    case MetadataType::XMP:
        // Test for XMP namespace
        testPath = L"/xmp/";
        break;
    default:
        return false;
    }

    // Try to query for the metadata type - if it exists, we support it
    PROPVARIANT propValue;
    PropVariantInit(&propValue);
    HRESULT hr = reader->GetMetadataByName(testPath.c_str(), &propValue);
    PropVariantClear(&propValue);

    // For both XMP and EXIF, we should return true if the file can be decoded
    // since extraction might work even if the test path doesn't exist
    // (different image formats may use different metadata paths)
    if (metadataType == MetadataType::XMP || metadataType == MetadataType::EXIF)
    {
        return true; // If we can decode the file, we can try extraction
    }
    
    // For other metadata types, check if the query succeeded
    return SUCCEEDED(hr);
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
    
    // Extract date/time fields
    metadata.dateTaken = ReadDateTime(reader, EXIF_DATE_TAKEN);
    metadata.dateDigitized = ReadDateTime(reader, EXIF_DATE_DIGITIZED);
    metadata.dateModified = ReadDateTime(reader, EXIF_DATE_MODIFIED);
    
    // Extract camera information
    metadata.cameraMake = ReadString(reader, EXIF_CAMERA_MAKE);
    metadata.cameraModel = ReadString(reader, EXIF_CAMERA_MODEL);
    metadata.lensModel = ReadString(reader, EXIF_LENS_MODEL);
    
    // Extract shooting parameters
    metadata.iso = ReadInteger(reader, EXIF_ISO);
    metadata.aperture = ReadDouble(reader, EXIF_APERTURE);
    metadata.shutterSpeed = ReadDouble(reader, EXIF_SHUTTER_SPEED);
    metadata.focalLength = ReadDouble(reader, EXIF_FOCAL_LENGTH);
    metadata.exposureBias = ReadDouble(reader, EXIF_EXPOSURE_BIAS);
    metadata.flash = ReadInteger(reader, EXIF_FLASH);
    
    // Extract image properties
    metadata.width = ReadInteger(reader, EXIF_WIDTH);
    metadata.height = ReadInteger(reader, EXIF_HEIGHT);
    metadata.orientation = ReadInteger(reader, EXIF_ORIENTATION);
    metadata.colorSpace = ReadInteger(reader, EXIF_COLOR_SPACE);
    
    // Extract author information
    metadata.author = ReadString(reader, EXIF_ARTIST);
    metadata.copyright = ReadString(reader, EXIF_COPYRIGHT);
}

void WICMetadataExtractor::ExtractGPSData(IWICMetadataQueryReader* reader, EXIFMetadata& metadata)
{
    if (!reader)
        return;
    
    // Extract GPS coordinates
    auto lat = ReadMetadata(reader, GPS_LATITUDE);
    auto lon = ReadMetadata(reader, GPS_LONGITUDE);
    auto latRef = ReadMetadata(reader, GPS_LATITUDE_REF);
    auto lonRef = ReadMetadata(reader, GPS_LONGITUDE_REF);
    
    if (lat.has_value() && lon.has_value())
    {
        auto coords = ParseGPSCoordinates(lat.value(), lon.value(),
                                         latRef.value_or(PROPVARIANT{}),
                                         lonRef.value_or(PROPVARIANT{}));
        
        metadata.latitude = coords.first;
        metadata.longitude = coords.second;
        
        PropVariantClear(&lat.value());
        PropVariantClear(&lon.value());
        if (latRef.has_value()) PropVariantClear(&latRef.value());
        if (lonRef.has_value()) PropVariantClear(&lonRef.value());
    }
    
    // Extract altitude
    auto alt = ReadMetadata(reader, GPS_ALTITUDE);
    if (alt.has_value())
    {
        metadata.altitude = ParseGPSRational(alt.value());
        PropVariantClear(&alt.value());
    }
}

std::optional<SYSTEMTIME> WICMetadataExtractor::ReadDateTime(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;
    
    // Convert PROPVARIANT to string first
    std::wstring dateStr;
    switch (propVar->vt)
    {
    case VT_LPWSTR:
        if (propVar->pwszVal)
            dateStr = propVar->pwszVal;
        break;
    case VT_BSTR:
        if (propVar->bstrVal)
            dateStr = propVar->bstrVal;
        break;
    case VT_LPSTR:
        if (propVar->pszVal)
        {
            int size = MultiByteToWideChar(CP_UTF8, 0, propVar->pszVal, -1, nullptr, 0);
            if (size > 1)
            {
                dateStr.resize(static_cast<size_t>(size) - 1);
                MultiByteToWideChar(CP_UTF8, 0, propVar->pszVal, -1, &dateStr[0], size);
            }
        }
        break;
    }
    
    PropVariantClear(&propVar.value());
    
    if (dateStr.empty())
        return std::nullopt;
    
    // Parse date formats
    SYSTEMTIME st = {0};
    
    // Try EXIF date format first: "YYYY:MM:DD HH:MM:SS"
    if (dateStr.length() >= 19)
    {
        if (swscanf_s(dateStr.c_str(), L"%hd:%hd:%hd %hd:%hd:%hd",
                     &st.wYear, &st.wMonth, &st.wDay,
                     &st.wHour, &st.wMinute, &st.wSecond) == 6)
        {
            if (st.wYear > 0 && st.wMonth > 0 && st.wMonth <= 12 && 
                st.wDay > 0 && st.wDay <= 31)
            {
                return st;
            }
        }
    }
    
    // Try XMP ISO 8601 format: "YYYY-MM-DDTHH:MM:SS" or with timezone
    if (dateStr.length() >= 19)
    {
        // Try basic ISO format without milliseconds
        if (swscanf_s(dateStr.c_str(), L"%hd-%hd-%hdT%hd:%hd:%hd",
                     &st.wYear, &st.wMonth, &st.wDay,
                     &st.wHour, &st.wMinute, &st.wSecond) == 6)
        {
            if (st.wYear > 0 && st.wMonth > 0 && st.wMonth <= 12 && 
                st.wDay > 0 && st.wDay <= 31)
            {
                return st;
            }
        }
    }
    
    // Try alternative ISO format with space instead of T
    if (dateStr.length() >= 19)
    {
        if (swscanf_s(dateStr.c_str(), L"%hd-%hd-%hd %hd:%hd:%hd",
                     &st.wYear, &st.wMonth, &st.wDay,
                     &st.wHour, &st.wMinute, &st.wSecond) == 6)
        {
            if (st.wYear > 0 && st.wMonth > 0 && st.wMonth <= 12 && 
                st.wDay > 0 && st.wDay <= 31)
            {
                return st;
            }
        }
    }
    
    return std::nullopt;
}

std::optional<std::wstring> WICMetadataExtractor::ReadString(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;
    
    std::wstring result;
    switch (propVar->vt)
    {
    case VT_LPWSTR:
        if (propVar->pwszVal)
            result = propVar->pwszVal;
        break;
    case VT_BSTR:
        if (propVar->bstrVal)
            result = propVar->bstrVal;
        break;
    case VT_LPSTR:
        if (propVar->pszVal)
        {
            int size = MultiByteToWideChar(CP_UTF8, 0, propVar->pszVal, -1, nullptr, 0);
            if (size > 1)
            {
                result.resize(static_cast<size_t>(size) - 1);
                MultiByteToWideChar(CP_UTF8, 0, propVar->pszVal, -1, &result[0], size);
            }
        }
        break;
    }
    
    PropVariantClear(&propVar.value());
    
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
    
    // For XMP strings, also sanitize for file names
    if (!result.empty())
    {
        result = SanitizeForFileName(result);
    }
    
    return result.empty() ? std::nullopt : std::make_optional(result);
}

std::wstring WICMetadataExtractor::SanitizeForFileName(const std::wstring& str)
{
    // Windows illegal filename characters: < > : " / \ | ? *
    // Also control characters (0-31) and some others
    std::wstring sanitized = str;
    
    // Replace illegal characters with underscore
    for (auto& ch : sanitized)
    {
        // Check for illegal characters
        if (ch == L'<' || ch == L'>' || ch == L':' || ch == L'"' ||
            ch == L'/' || ch == L'\\' || ch == L'|' || ch == L'?' || ch == L'*' ||
            ch < 32)  // Control characters
        {
            ch = L'_';
        }
    }
    
    // Also remove trailing dots and spaces (Windows doesn't like them at end of filename)
    while (!sanitized.empty() && (sanitized.back() == L'.' || sanitized.back() == L' '))
    {
        sanitized.pop_back();
    }
    
    return sanitized;
}

std::optional<int64_t> WICMetadataExtractor::ReadInteger(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;
    
    int64_t result = 0;
    switch (propVar->vt)
    {
    case VT_I1: result = propVar->cVal; break;
    case VT_I2: result = propVar->iVal; break;
    case VT_I4: result = propVar->lVal; break;
    case VT_I8: result = propVar->hVal.QuadPart; break;
    case VT_UI1: result = propVar->bVal; break;
    case VT_UI2: result = propVar->uiVal; break;
    case VT_UI4: result = propVar->ulVal; break;
    case VT_UI8: result = static_cast<int64_t>(propVar->uhVal.QuadPart); break;
    default:
        PropVariantClear(&propVar.value());
        return std::nullopt;
    }
    
    PropVariantClear(&propVar.value());
    return result;
}

std::optional<double> WICMetadataExtractor::ReadDouble(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;
    
    double result = 0.0;
    switch (propVar->vt)
    {
    case VT_R4: 
        result = static_cast<double>(propVar->fltVal); 
        break;
    case VT_R8: 
        result = propVar->dblVal; 
        break;
    case VT_UI1 | VT_VECTOR:
    case VT_UI4 | VT_VECTOR:
        // Handle rational number (common for EXIF values)
        // Check if this is signed rational (SRATIONAL) for ExposureBias
        if (propVar->caub.cElems >= 8)
        {
            // For ExposureBias and similar fields, we need signed rational
            // The path contains "37380" which is ExposureBiasValue tag
            if (path.find(L"37380") != std::wstring::npos)
            {
                result = ParseSingleSRational(propVar->caub.pElems, 0);
                break;
            }
            else
            {
                // Extract denominator to check if the rational is valid
                const uint8_t* bytes = propVar->caub.pElems;
                uint32_t denominator = static_cast<uint32_t>(bytes[4]) |
                                     (static_cast<uint32_t>(bytes[5]) << 8) |
                                     (static_cast<uint32_t>(bytes[6]) << 16) |
                                     (static_cast<uint32_t>(bytes[7]) << 24);
                
                if (denominator != 0)
                {
                    result = ParseSingleRational(propVar->caub.pElems, 0);
                    break;
                }
            }
        }
        PropVariantClear(&propVar.value());
        return std::nullopt;
    default:
        // Try integer conversion
        switch (propVar->vt)
        {
        case VT_I1: result = static_cast<double>(propVar->cVal); break;
        case VT_I2: result = static_cast<double>(propVar->iVal); break;
        case VT_I4: result = static_cast<double>(propVar->lVal); break;
        case VT_I8: 
            {
                // Check if this is ExposureBias (SRATIONAL stored as VT_I8)
                if (path.find(L"37380") != std::wstring::npos)
                {
                    // ExposureBias: signed rational stored as int64
                    // For EXIF SRATIONAL in WIC: low 32 bits = numerator, high 32 bits = denominator
                    int32_t numerator = static_cast<int32_t>(propVar->hVal.QuadPart & 0xFFFFFFFF);
                    int32_t denominator = static_cast<int32_t>(propVar->hVal.QuadPart >> 32);
                    if (denominator != 0)
                    {
                        result = static_cast<double>(numerator) / static_cast<double>(denominator);
                    }
                    else
                    {
                        // If denominator is 0, try the other way around
                        numerator = static_cast<int32_t>(propVar->hVal.QuadPart >> 32);
                        denominator = static_cast<int32_t>(propVar->hVal.QuadPart & 0xFFFFFFFF);
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            result = 0.0; // Default to 0 for ExposureBias if can't parse
                        }
                    }
                }
                else
                {
                    result = static_cast<double>(propVar->hVal.QuadPart);
                }
            }
            break;
        case VT_UI1: result = static_cast<double>(propVar->bVal); break;
        case VT_UI2: result = static_cast<double>(propVar->uiVal); break;
        case VT_UI4: result = static_cast<double>(propVar->ulVal); break;
        case VT_UI8: 
            {
                // Check if this is ExposureBias (SRATIONAL stored as VT_UI8)
                if (path.find(L"37380") != std::wstring::npos)
                {
                    // ExposureBias: signed rational stored as uint64 but should be interpreted as signed
                    // For EXIF SRATIONAL in WIC: low 32 bits = numerator, high 32 bits = denominator
                    int32_t numerator = static_cast<int32_t>(propVar->uhVal.QuadPart & 0xFFFFFFFF);
                    int32_t denominator = static_cast<int32_t>(propVar->uhVal.QuadPart >> 32);
                    if (denominator != 0)
                    {
                        result = static_cast<double>(numerator) / static_cast<double>(denominator);
                    }
                    else
                    {
                        // If denominator is 0, try the other way around
                        numerator = static_cast<int32_t>(propVar->uhVal.QuadPart >> 32);
                        denominator = static_cast<int32_t>(propVar->uhVal.QuadPart & 0xFFFFFFFF);
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            result = 0.0; // Default to 0 for ExposureBias if can't parse
                        }
                    }
                }
                else
                {
                    // VT_UI8 for EXIF rational: Try both orders to handle different encodings
                    // First try: low 32 bits = numerator, high 32 bits = denominator
                    uint32_t numerator = static_cast<uint32_t>(propVar->uhVal.QuadPart & 0xFFFFFFFF);
                    uint32_t denominator = static_cast<uint32_t>(propVar->uhVal.QuadPart >> 32);
                    
                    if (denominator != 0)
                    {
                        result = static_cast<double>(numerator) / static_cast<double>(denominator);
                    }
                    else
                    {
                        // Second try: high 32 bits = numerator, low 32 bits = denominator
                        numerator = static_cast<uint32_t>(propVar->uhVal.QuadPart >> 32);
                        denominator = static_cast<uint32_t>(propVar->uhVal.QuadPart & 0xFFFFFFFF);
                        if (denominator != 0)
                        {
                            result = static_cast<double>(numerator) / static_cast<double>(denominator);
                        }
                        else
                        {
                            // Fall back to treating as regular integer if denominator is 0
                            result = static_cast<double>(propVar->uhVal.QuadPart);
                        }
                    }
                }
            }
            break;
        default:
            PropVariantClear(&propVar.value());
            return std::nullopt;
        }
    }
    
    PropVariantClear(&propVar.value());
    return result;
}

std::optional<PROPVARIANT> WICMetadataExtractor::ReadMetadata(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    if (!reader)
        return std::nullopt;
    
    PROPVARIANT value;
    PropVariantInit(&value);
    
    HRESULT hr = reader->GetMetadataByName(path.c_str(), &value);
    if (SUCCEEDED(hr))
    {
        return value;
    }
    
    return std::nullopt;
}

double WICMetadataExtractor::ParseGPSRational(const PROPVARIANT& pv)
{
    if ((pv.vt & VT_VECTOR) && pv.caub.cElems >= 8)
    {
        return ParseSingleRational(pv.caub.pElems, 0);
    }
    return 0.0;
}

double WICMetadataExtractor::ParseSingleRational(const uint8_t* bytes, size_t offset)
{
    // Parse a single rational number (8 bytes: numerator + denominator)
    if (!bytes)
        return 0.0;
        
    const uint8_t* rationalBytes = bytes + offset;
    
    // Parse as little-endian uint32_t values
    uint32_t numerator = static_cast<uint32_t>(rationalBytes[0]) |
                        (static_cast<uint32_t>(rationalBytes[1]) << 8) |
                        (static_cast<uint32_t>(rationalBytes[2]) << 16) |
                        (static_cast<uint32_t>(rationalBytes[3]) << 24);
                        
    uint32_t denominator = static_cast<uint32_t>(rationalBytes[4]) |
                          (static_cast<uint32_t>(rationalBytes[5]) << 8) |
                          (static_cast<uint32_t>(rationalBytes[6]) << 16) |
                          (static_cast<uint32_t>(rationalBytes[7]) << 24);
    
    if (denominator != 0)
    {
        return static_cast<double>(numerator) / static_cast<double>(denominator);
    }
    
    return 0.0;
}

double WICMetadataExtractor::ParseSingleSRational(const uint8_t* bytes, size_t offset)
{
    // Parse a single signed rational number (8 bytes: signed numerator + signed denominator)
    if (!bytes)
        return 0.0;
        
    const uint8_t* rationalBytes = bytes + offset;
    
    // Parse as little-endian int32_t values (signed)
    // First construct as unsigned, then reinterpret as signed
    uint32_t numerator_uint = static_cast<uint32_t>(rationalBytes[0]) |
                              (static_cast<uint32_t>(rationalBytes[1]) << 8) |
                              (static_cast<uint32_t>(rationalBytes[2]) << 16) |
                              (static_cast<uint32_t>(rationalBytes[3]) << 24);
                        
    uint32_t denominator_uint = static_cast<uint32_t>(rationalBytes[4]) |
                                (static_cast<uint32_t>(rationalBytes[5]) << 8) |
                                (static_cast<uint32_t>(rationalBytes[6]) << 16) |
                                (static_cast<uint32_t>(rationalBytes[7]) << 24);
    
    // Reinterpret as signed
    int32_t numerator = static_cast<int32_t>(numerator_uint);
    int32_t denominator = static_cast<int32_t>(denominator_uint);
    
    if (denominator != 0)
    {
        return static_cast<double>(numerator) / static_cast<double>(denominator);
    }
    
    return 0.0;
}

std::pair<double, double> WICMetadataExtractor::ParseGPSCoordinates(
    const PROPVARIANT& latitude,
    const PROPVARIANT& longitude,
    const PROPVARIANT& latRef,
    const PROPVARIANT& lonRef)
{
    double lat = 0.0, lon = 0.0;
    
    // Parse latitude - typically stored as 3 rationals (degrees, minutes, seconds)
    if ((latitude.vt & VT_VECTOR) && latitude.caub.cElems >= 24) // 3 rationals * 8 bytes each
    {
        const uint8_t* bytes = latitude.caub.pElems;
        
        // degrees, minutes, seconds (each rational is 8 bytes)
        double degrees = ParseSingleRational(bytes, 0);
        double minutes = ParseSingleRational(bytes, 8);
        double seconds = ParseSingleRational(bytes, 16);
        
        lat = degrees + minutes / 60.0 + seconds / 3600.0;
    }
    
    // Parse longitude
    if ((longitude.vt & VT_VECTOR) && longitude.caub.cElems >= 24)
    {
        const uint8_t* bytes = longitude.caub.pElems;
        
        double degrees = ParseSingleRational(bytes, 0);
        double minutes = ParseSingleRational(bytes, 8);
        double seconds = ParseSingleRational(bytes, 16);
        
        lon = degrees + minutes / 60.0 + seconds / 3600.0;
    }
    
    // Apply direction references (N/S for latitude, E/W for longitude)
    if (latRef.vt == VT_LPSTR && latRef.pszVal)
    {
        if (strcmp(latRef.pszVal, "S") == 0)
            lat = -lat;
    }
    
    if (lonRef.vt == VT_LPSTR && lonRef.pszVal)
    {
        if (strcmp(lonRef.pszVal, "W") == 0)
            lon = -lon;
    }
    
    return {lat, lon};
}


ExtractionResult WICMetadataExtractor::ExtractXMPMetadata(
    const std::wstring& filePath,
    XMPMetadata& outMetadata)
{
    // Check if file exists
    if (!PathFileExistsW(filePath.c_str()))
    {
        return ExtractionResult::FileNotFound;
    }
    
    auto decoder = CreateDecoder(filePath);
    if (!decoder)
    {
        return ExtractionResult::UnsupportedFormat;
    }
    
    // Get first frame
    CComPtr<IWICBitmapFrameDecode> frame;
    if (FAILED(decoder->GetFrame(0, &frame)))
    {
        return ExtractionResult::DecoderError;
    }
    
    // Get the root metadata reader
    CComPtr<IWICMetadataQueryReader> rootReader;
    if (FAILED(frame->GetMetadataQueryReader(&rootReader)))
    {
        return ExtractionResult::MetadataNotFound;
    }
    
    // The actual XMP data might be in a nested reader
    // Based on our path enumeration, XMP fields are directly accessible from root
    // using paths like "/xmp//xmp:CreatorTool"
    
    // Extract XMP fields using the root reader
    ExtractAllXMPFields(rootReader, outMetadata);
    
    return ExtractionResult::Success;
}


// ReadStringArray helper method
std::optional<std::vector<std::wstring>> WICMetadataExtractor::ReadStringArray(IWICMetadataQueryReader* reader, const std::wstring& path)
{
    auto propVar = ReadMetadata(reader, path);
    if (!propVar.has_value())
        return std::nullopt;
    
    std::vector<std::wstring> result;
    
    switch (propVar->vt)
    {
    case VT_VECTOR | VT_LPWSTR:
        if (propVar->calpwstr.cElems > 0 && propVar->calpwstr.pElems)
        {
            for (ULONG i = 0; i < propVar->calpwstr.cElems; ++i)
            {
                if (propVar->calpwstr.pElems[i])
                {
                    result.push_back(propVar->calpwstr.pElems[i]);
                }
            }
        }
        break;
    case VT_VECTOR | VT_BSTR:
        if (propVar->cabstr.cElems > 0 && propVar->cabstr.pElems)
        {
            for (ULONG i = 0; i < propVar->cabstr.cElems; ++i)
            {
                if (propVar->cabstr.pElems[i])
                {
                    result.push_back(propVar->cabstr.pElems[i]);
                }
            }
        }
        break;
    case VT_LPWSTR:
        if (propVar->pwszVal)
        {
            result.push_back(propVar->pwszVal);
        }
        break;
    case VT_BSTR:
        if (propVar->bstrVal)
        {
            result.push_back(propVar->bstrVal);
        }
        break;
    }
    
    PropVariantClear(&propVar.value());
    
    // Sanitize each string in the array for file names
    for (auto& str : result)
    {
        // Trim whitespace
        if (!str.empty())
        {
            size_t start = str.find_first_not_of(L" \t\r\n");
            size_t end = str.find_last_not_of(L" \t\r\n");
            if (start != std::wstring::npos && end != std::wstring::npos)
            {
                str = str.substr(start, end - start + 1);
            }
            else if (start == std::wstring::npos)
            {
                str.clear();
            }
        }
        
        // Sanitize for file names
        if (!str.empty())
        {
            str = SanitizeForFileName(str);
        }
    }
    
    // Remove any empty strings from the result
    result.erase(
        std::remove_if(result.begin(), result.end(), 
                      [](const std::wstring& s) { return s.empty(); }),
        result.end());
    
    return result.empty() ? std::nullopt : std::make_optional(result);
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
    std::vector<std::wstring> subjects;
    for (int i = 0; i < 10; ++i)  // Try up to 10 subjects
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
