// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <string>
#include <unordered_map>
#include <vector>
#include <memory>
#include "MetadataTypes.h"
#include "IMetadataExtractor.h"

namespace PowerRenameLib
{
    // Pattern-Value mapping for metadata replacement
    using MetadataPatternMap = std::unordered_map<std::wstring, std::wstring>;

    /// <summary>
    /// Enhanced metadata pattern extractor with dependency injection support
    /// </summary>
    class MetadataPatternExtractor
    {
    public:
        /// <summary>
        /// Constructor with optional dependency injection
        /// </summary>
        /// <param name="extractor">Optional metadata extractor implementation</param>
        explicit MetadataPatternExtractor(
            std::shared_ptr<IMetadataExtractor> extractor = nullptr);
        
        /// <summary>
        /// Extract all patterns for the specified metadata type from the file
        /// </summary>
        MetadataPatternMap ExtractPatterns(
            const std::wstring& filePath,
            MetadataType type);
        
        /// <summary>
        /// Extract patterns from multiple files in parallel
        /// </summary>
        std::vector<std::pair<std::wstring, MetadataPatternMap>> ExtractPatternsFromFiles(
            const std::vector<std::wstring>& filePaths,
            MetadataType type);
        
        /// <summary>
        /// Check if the file supports the specified metadata type
        /// </summary>
        bool IsSupported(const std::wstring& filePath, MetadataType type) const;
        
        /// <summary>
        /// Get patterns supported by specific metadata type
        /// </summary>
        static std::vector<std::wstring> GetSupportedPatterns(MetadataType type);
        
        /// <summary>
        /// Get all possible metadata patterns
        /// </summary>
        static std::vector<std::wstring> GetAllPossiblePatterns();
        
        // Static methods for backward compatibility
        static MetadataPatternMap ExtractPatternsStatic(
            const std::wstring& filePath,
            MetadataType type);
            
        static bool IsSupportedStatic(
            const std::wstring& filePath, 
            MetadataType type);
        
    private:
        // Metadata extractor instance
        std::shared_ptr<IMetadataExtractor> extractor;
        
        // Default static extractor for backward compatibility
        static std::shared_ptr<IMetadataExtractor> GetDefaultExtractor();
        
        // Extract patterns for each metadata type
        MetadataPatternMap ExtractEXIFPatterns(const std::wstring& filePath);
        MetadataPatternMap ExtractXMPPatterns(const std::wstring& filePath);
        
        // Extract date patterns from SYSTEMTIME
        void AddDatePatterns(
            const SYSTEMTIME& date,
            const std::wstring& prefix,
            MetadataPatternMap& patterns);
        
        // Formatting helpers (static as they don't need instance data)
        static std::wstring FormatAperture(double aperture);
        static std::wstring FormatShutterSpeed(double speed);
        static std::wstring FormatISO(int64_t iso);
        static std::wstring FormatFlash(int64_t flashValue);
        static std::wstring FormatCoordinate(double coord, bool isLatitude);
        static std::wstring FormatSystemTime(const SYSTEMTIME& st);
    };
    
}