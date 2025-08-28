#pragma once
#include <string>
#include <memory>
#include <map>

namespace PowerRenameLib
{
    /// <summary>
    /// Media metadata extractor class that uses TinyEXIF for EXIF data extraction
    /// to extract metadata from image files for use in PowerRename
    /// </summary>
    class MediaMetadataExtractor
    {
    public:
        struct ImageMetadata
        {
            std::wstring cameraModel;
            std::wstring dateTaken;
            std::wstring gpsLocation;
            std::wstring artist;
            std::wstring copyright;
            int width = 0;
            int height = 0;
            int iso = 0;
            double aperture = 0.0;
            double shutterSpeed = 0.0;
            double focalLength = 0.0;
        };

        MediaMetadataExtractor();
        ~MediaMetadataExtractor();

        /// <summary>
        /// Extract metadata from an image file using TinyEXIF
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <returns>ImageMetadata structure with extracted data</returns>
        ImageMetadata ExtractEXIFMetadata(const std::wstring& filePath);

        /// <summary>
        /// Format metadata for use in rename patterns
        /// </summary>
        /// <param name="metadata">Metadata structure</param>
        /// <param name="pattern">Pattern key (e.g., "camera", "date", "location")</param>
        /// <returns>Formatted string for the pattern</returns>
        std::wstring FormatMetadataForRename(const ImageMetadata& metadata, const std::wstring& pattern);

    private:
        class Impl;
        std::unique_ptr<Impl> m_pImpl;
    };
}
