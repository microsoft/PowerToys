// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <string>
#include <unordered_map>
#include <vector>
#include <memory>
#include "MetadataTypes.h"

namespace PowerRenameLib
{
    // Pattern-Value mapping for metadata replacement
    using MetadataPatternMap = std::unordered_map<std::wstring, std::wstring>;

    /// <summary>
    /// Metadata pattern extractor that converts metadata into replaceable patterns
    /// </summary>
    class MetadataPatternExtractor
    {
    public:
        MetadataPatternExtractor();
        ~MetadataPatternExtractor();

        MetadataPatternMap ExtractPatterns(const std::wstring& filePath, MetadataType type);

        void ClearCache();

        static std::vector<std::wstring> GetSupportedPatterns(MetadataType type);
        static std::vector<std::wstring> GetAllPossiblePatterns();

    private:
        std::unique_ptr<class WICMetadataExtractor> extractor;

        MetadataPatternMap ExtractEXIFPatterns(const std::wstring& filePath);
        MetadataPatternMap ExtractXMPPatterns(const std::wstring& filePath);

        static std::wstring FormatAperture(double aperture);
        static std::wstring FormatShutterSpeed(double speed);
        static std::wstring FormatISO(int64_t iso);
        static std::wstring FormatFlash(int64_t flashValue);
        static std::wstring FormatCoordinate(double coord, bool isLatitude);
        static std::wstring FormatSystemTime(const SYSTEMTIME& st);
    };
}
