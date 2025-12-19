// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "MetadataTypes.h"
#include "MetadataResultCache.h"
#include "PropVariantValue.h"
#include <wincodec.h>
#include <atlbase.h>

namespace PowerRenameLib
{
    /// <summary>
    /// Windows Imaging Component (WIC) implementation for metadata extraction
    /// Provides efficient batch extraction of all metadata types with built-in caching
    /// </summary>
    class WICMetadataExtractor
    {
    public:
        WICMetadataExtractor();
        ~WICMetadataExtractor();

        // Public metadata extraction methods
        bool ExtractEXIFMetadata(
            const std::wstring& filePath,
            EXIFMetadata& outMetadata);

        bool ExtractXMPMetadata(
            const std::wstring& filePath,
            XMPMetadata& outMetadata);

        void ClearCache();

    private:
        // WIC factory management
        static CComPtr<IWICImagingFactory> GetWICFactory();
        static void InitializeWIC();

        // WIC operations
        CComPtr<IWICBitmapDecoder> CreateDecoder(const std::wstring& filePath);
        CComPtr<IWICMetadataQueryReader> GetMetadataReader(IWICBitmapDecoder* decoder);

        bool LoadEXIFMetadata(const std::wstring& filePath, EXIFMetadata& outMetadata);
        bool LoadXMPMetadata(const std::wstring& filePath, XMPMetadata& outMetadata);

        // Batch extraction methods
        void ExtractAllEXIFFields(IWICMetadataQueryReader* reader, EXIFMetadata& metadata);
        void ExtractGPSData(IWICMetadataQueryReader* reader, EXIFMetadata& metadata);
        void ExtractAllXMPFields(IWICMetadataQueryReader* reader, XMPMetadata& metadata);

        // Field reading helpers (single path)
        std::optional<SYSTEMTIME> ReadDateTime(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<std::wstring> ReadString(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<int64_t> ReadInteger(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<double> ReadDouble(IWICMetadataQueryReader* reader, const std::wstring& path);

        // Field reading helpers with multiple path support (for cross-format compatibility)
        // These methods try each path in order until metadata is found
        std::optional<SYSTEMTIME> ReadDateTimeFromPaths(IWICMetadataQueryReader* reader, const std::vector<std::wstring>& paths);
        std::optional<std::wstring> ReadStringFromPaths(IWICMetadataQueryReader* reader, const std::vector<std::wstring>& paths);
        std::optional<int64_t> ReadIntegerFromPaths(IWICMetadataQueryReader* reader, const std::vector<std::wstring>& paths);
        std::optional<double> ReadDoubleFromPaths(IWICMetadataQueryReader* reader, const std::vector<std::wstring>& paths);
        std::optional<PropVariantValue> ReadMetadataFromPaths(IWICMetadataQueryReader* reader, const std::vector<std::wstring>& paths);

        // Helper methods
        std::optional<PropVariantValue> ReadMetadata(IWICMetadataQueryReader* reader, const std::wstring& path);

    private:
        MetadataResultCache cache;
    };
}
