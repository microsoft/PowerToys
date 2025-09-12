// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "MetadataTypes.h"
#include <string>
#include <memory>

namespace PowerRenameLib
{
    /// <summary>
    /// Interface for metadata extraction implementations
    /// Allows for dependency injection and unit testing
    /// </summary>
    class IMetadataExtractor
    {
    public:
        virtual ~IMetadataExtractor() = default;

        /// <summary>
        /// Extract EXIF metadata from an image file
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="outMetadata">Output metadata structure</param>
        /// <returns>Extraction result status</returns>
        virtual ExtractionResult ExtractEXIFMetadata(
            const std::wstring& filePath,
            EXIFMetadata& outMetadata) = 0;

        /// <summary>
        /// Extract XMP metadata from an image file
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="outMetadata">Output metadata structure</param>
        /// <returns>Extraction result status</returns>
        virtual ExtractionResult ExtractXMPMetadata(
            const std::wstring& filePath,
            XMPMetadata& outMetadata) = 0;

        /// <summary>
        /// Check if a file is supported for metadata extraction
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="metadataType">Type of metadata to check</param>
        /// <returns>True if the file format and metadata type are supported</returns>
        virtual bool IsSupported(
            const std::wstring& filePath,
            MetadataType metadataType) = 0;
    };
}