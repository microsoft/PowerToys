// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <string>
#include <optional>
#include <vector>
#include <windows.h>

namespace PowerRenameLib
{
    /// <summary>
    /// Supported metadata format types
    /// </summary>
    enum class MetadataType
    {
        EXIF,    // EXIF metadata (camera settings, date taken, etc.)
        XMP      // XMP metadata (Dublin Core, Photoshop, etc.)
    };

    /// <summary>
    /// Complete EXIF metadata structure
    /// Contains all commonly used EXIF fields with optional values
    /// </summary>
    struct EXIFMetadata
    {
        // Date and time information
        std::optional<SYSTEMTIME> dateTaken;      // DateTimeOriginal
        std::optional<SYSTEMTIME> dateDigitized; // DateTimeDigitized
        std::optional<SYSTEMTIME> dateModified;  // DateTime
        
        // Camera information
        std::optional<std::wstring> cameraMake;   // Make
        std::optional<std::wstring> cameraModel;  // Model
        std::optional<std::wstring> lensModel;    // LensModel
        
        // Shooting parameters
        std::optional<int64_t> iso;               // ISO speed
        std::optional<double> aperture;           // F-number
        std::optional<double> shutterSpeed;       // Exposure time
        std::optional<double> focalLength;        // Focal length in mm
        std::optional<double> exposureBias;       // Exposure bias value
        std::optional<int64_t> flash;             // Flash status
        
        // Image properties
        std::optional<int64_t> width;             // Image width in pixels
        std::optional<int64_t> height;            // Image height in pixels
        std::optional<int64_t> orientation;       // Image orientation
        std::optional<int64_t> colorSpace;        // Color space
        
        // Author and copyright
        std::optional<std::wstring> author;       // Artist
        std::optional<std::wstring> copyright;    // Copyright notice
        
        // GPS information
        std::optional<double> latitude;           // GPS latitude in decimal degrees
        std::optional<double> longitude;          // GPS longitude in decimal degrees
        std::optional<double> altitude;           // GPS altitude in meters
    };

    /// <summary>
    /// XMP (Extensible Metadata Platform) metadata structure
    /// Contains XMP Basic, Dublin Core, Rights and Media Management schema fields
    /// </summary>
    struct XMPMetadata
    {
        // XMP Basic schema - https://ns.adobe.com/xap/1.0/
        std::optional<SYSTEMTIME> createDate;     // xmp:CreateDate
        std::optional<SYSTEMTIME> modifyDate;     // xmp:ModifyDate  
        std::optional<SYSTEMTIME> metadataDate;   // xmp:MetadataDate
        std::optional<std::wstring> creatorTool;  // xmp:CreatorTool
        
        // Dublin Core schema - http://purl.org/dc/elements/1.1/
        std::optional<std::wstring> title;        // dc:title
        std::optional<std::wstring> description;  // dc:description
        std::optional<std::wstring> creator;      // dc:creator (author)
        std::optional<std::vector<std::wstring>> subject; // dc:subject (keywords)
        
        // XMP Rights Management schema - http://ns.adobe.com/xap/1.0/rights/
        std::optional<std::wstring> rights;       // xmpRights:WebStatement (copyright)
        
        // XMP Media Management schema - http://ns.adobe.com/xap/1.0/mm/
        std::optional<std::wstring> documentID;       // xmpMM:DocumentID
        std::optional<std::wstring> instanceID;       // xmpMM:InstanceID
        std::optional<std::wstring> originalDocumentID; // xmpMM:OriginalDocumentID
        std::optional<std::wstring> versionID;        // xmpMM:VersionID
    };




    /// <summary>
    /// Constants for metadata pattern names
    /// </summary>
    namespace MetadataPatterns
    {
        // EXIF patterns
        constexpr wchar_t CAMERA_MAKE[] = L"CAMERA_MAKE";
        constexpr wchar_t CAMERA_MODEL[] = L"CAMERA_MODEL";
        constexpr wchar_t LENS[] = L"LENS";
        constexpr wchar_t ISO[] = L"ISO";
        constexpr wchar_t APERTURE[] = L"APERTURE";
        constexpr wchar_t SHUTTER[] = L"SHUTTER";
        constexpr wchar_t FOCAL[] = L"FOCAL";
        constexpr wchar_t FLASH[] = L"FLASH";
        constexpr wchar_t WIDTH[] = L"WIDTH";
        constexpr wchar_t HEIGHT[] = L"HEIGHT";
        constexpr wchar_t AUTHOR[] = L"AUTHOR";
        constexpr wchar_t COPYRIGHT[] = L"COPYRIGHT";
        constexpr wchar_t LATITUDE[] = L"LATITUDE";
        constexpr wchar_t LONGITUDE[] = L"LONGITUDE";
        
        // Date components from EXIF DateTimeOriginal (when photo was taken)
        constexpr wchar_t DATE_TAKEN_YYYY[] = L"DATE_TAKEN_YYYY";
        constexpr wchar_t DATE_TAKEN_YY[] = L"DATE_TAKEN_YY";
        constexpr wchar_t DATE_TAKEN_MM[] = L"DATE_TAKEN_MM";
        constexpr wchar_t DATE_TAKEN_DD[] = L"DATE_TAKEN_DD";
        constexpr wchar_t DATE_TAKEN_HH[] = L"DATE_TAKEN_HH";
        constexpr wchar_t DATE_TAKEN_mm[] = L"DATE_TAKEN_mm";
        constexpr wchar_t DATE_TAKEN_SS[] = L"DATE_TAKEN_SS";

        // Additional EXIF patterns
        constexpr wchar_t EXPOSURE_BIAS[] = L"EXPOSURE_BIAS";
        constexpr wchar_t ORIENTATION[] = L"ORIENTATION";
        constexpr wchar_t COLOR_SPACE[] = L"COLOR_SPACE";
        constexpr wchar_t ALTITUDE[] = L"ALTITUDE";
        
        // XMP patterns
        constexpr wchar_t CREATOR_TOOL[] = L"CREATOR_TOOL";

        // Date components from XMP CreateDate
        constexpr wchar_t CREATE_DATE_YYYY[] = L"CREATE_DATE_YYYY";
        constexpr wchar_t CREATE_DATE_YY[] = L"CREATE_DATE_YY";
        constexpr wchar_t CREATE_DATE_MM[] = L"CREATE_DATE_MM";
        constexpr wchar_t CREATE_DATE_DD[] = L"CREATE_DATE_DD";
        constexpr wchar_t CREATE_DATE_HH[] = L"CREATE_DATE_HH";
        constexpr wchar_t CREATE_DATE_mm[] = L"CREATE_DATE_mm";
        constexpr wchar_t CREATE_DATE_SS[] = L"CREATE_DATE_SS";

        // Dublin Core patterns
        constexpr wchar_t TITLE[] = L"TITLE";
        constexpr wchar_t DESCRIPTION[] = L"DESCRIPTION";
        constexpr wchar_t CREATOR[] = L"CREATOR";
        constexpr wchar_t SUBJECT[] = L"SUBJECT";  // Keywords
        
        // XMP Rights pattern
        constexpr wchar_t RIGHTS[] = L"RIGHTS";  // Copyright
        
        // XMP Media Management patterns
        constexpr wchar_t DOCUMENT_ID[] = L"DOCUMENT_ID";
        constexpr wchar_t INSTANCE_ID[] = L"INSTANCE_ID";
        constexpr wchar_t ORIGINAL_DOCUMENT_ID[] = L"ORIGINAL_DOCUMENT_ID";
        constexpr wchar_t VERSION_ID[] = L"VERSION_ID";
    }
}