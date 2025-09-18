// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "IMetadataExtractor.h"
#include "MetadataTypes.h"
#include <wincodec.h>
#include <atlbase.h>

namespace PowerRenameLib
{
    /// <summary>
    /// Windows Imaging Component (WIC) implementation for metadata extraction
    /// Provides efficient batch extraction of all metadata types
    /// </summary>
    class WICMetadataExtractor : public IMetadataExtractor
    {
    public:
        WICMetadataExtractor();
        ~WICMetadataExtractor() override;

        // IMetadataExtractor implementation
        ExtractionResult ExtractEXIFMetadata(
            const std::wstring& filePath,
            EXIFMetadata& outMetadata) override;

        ExtractionResult ExtractXMPMetadata(
            const std::wstring& filePath,
            XMPMetadata& outMetadata) override;

        bool IsSupported(const std::wstring& filePath, MetadataType metadataType) override;

    private:
        // WIC factory management
        static CComPtr<IWICImagingFactory> GetWICFactory();
        static void InitializeWIC();
        static void CleanupWIC();

        // WIC operations
        CComPtr<IWICBitmapDecoder> CreateDecoder(const std::wstring& filePath);
        CComPtr<IWICMetadataQueryReader> GetMetadataReader(IWICBitmapDecoder* decoder);

        // Batch extraction methods
        void ExtractAllEXIFFields(IWICMetadataQueryReader* reader, EXIFMetadata& metadata);
        void ExtractGPSData(IWICMetadataQueryReader* reader, EXIFMetadata& metadata);
        void ExtractAllXMPFields(IWICMetadataQueryReader* reader, XMPMetadata& metadata);

        // Field reading helpers
        std::optional<SYSTEMTIME> ReadDateTime(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<std::wstring> ReadString(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<int64_t> ReadInteger(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<double> ReadDouble(IWICMetadataQueryReader* reader, const std::wstring& path);
        std::optional<std::vector<std::wstring>> ReadStringArray(IWICMetadataQueryReader* reader, const std::wstring& path);

        // GPS utilities
        static double ParseGPSRational(const PROPVARIANT& pv);
        static double ParseSingleRational(const uint8_t* bytes, size_t offset);
        static double ParseSingleSRational(const uint8_t* bytes, size_t offset);
        static std::pair<double, double> ParseGPSCoordinates(
            const PROPVARIANT& latitude,
            const PROPVARIANT& longitude,
            const PROPVARIANT& latRef,
            const PROPVARIANT& lonRef);

        // Helper methods
        std::optional<PROPVARIANT> ReadMetadata(IWICMetadataQueryReader* reader, const std::wstring& path);

    public:
        // Public helper for testing and external use
        static std::wstring SanitizeForFileName(const std::wstring& str);
    };
}