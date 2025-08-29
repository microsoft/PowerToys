#pragma once
#include <string>
#include <memory>
#include <map>
#include <vector>
#include <optional>
#include <variant>

namespace PowerRenameLib
{
    /// <summary>
    /// Windows Imaging Component (WIC) based metadata extractor
    /// Designed to leverage WIC's full capabilities for comprehensive image metadata extraction
    /// </summary>
    class WICMetadataExtractor
    {
    public:
        /// <summary>
        /// Metadata value type - can hold different data types returned by WIC
        /// </summary>
        using MetadataValue = std::variant<
            std::wstring,           // String values
            int32_t,               // Integer values  
            uint32_t,              // Unsigned integer values
            double,                // Floating point values
            bool,                  // Boolean values
            std::vector<uint8_t>   // Binary data
        >;

        /// <summary>
        /// Comprehensive metadata container with WIC-native organization
        /// </summary>
        struct ImageInfo
        {
            // Basic image properties
            uint32_t width = 0;
            uint32_t height = 0;
            uint32_t bitsPerPixel = 0;
            std::wstring pixelFormat;
            std::wstring containerFormat;
            
            // All metadata organized by source
            std::map<std::wstring, MetadataValue> exifData;      // EXIF metadata
            std::map<std::wstring, MetadataValue> iptcData;      // IPTC metadata  
            std::map<std::wstring, MetadataValue> xmpData;       // XMP metadata
            std::map<std::wstring, MetadataValue> ifdData;       // IFD metadata
            std::map<std::wstring, MetadataValue> gpsData;       // GPS metadata
            std::map<std::wstring, MetadataValue> customData;    // Custom/other metadata
        };

        /// <summary>
        /// Supported image format information
        /// </summary>
        struct FormatInfo
        {
            std::wstring formatName;
            std::wstring fileExtensions;  // Comma-separated
            std::vector<std::wstring> mimeTypes;
            bool supportsMetadata = false;
            bool supportsMultiFrame = false;
        };

        /// <summary>
        /// Metadata extraction options
        /// </summary>
        struct ExtractionOptions
        {
            bool includeExif = true;
            bool includeIptc = true;
            bool includeXmp = true;
            bool includeGps = true;
            bool includeCustom = true;
            bool includeThumbnails = false;
            bool cacheMetadata = true;
            uint32_t maxBinaryDataSize = 1024 * 1024; // 1MB limit for binary data
        };

        WICMetadataExtractor();
        ~WICMetadataExtractor();

        /// <summary>
        /// Extract comprehensive metadata using WIC's native capabilities
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="options">Extraction options</param>
        /// <returns>Complete image information including all available metadata</returns>
        std::optional<ImageInfo> ExtractImageInfo(const std::wstring& filePath, 
                                                  const ExtractionOptions& options = {});

        /// <summary>
        /// Get detailed format information for a file
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <returns>Format information if supported</returns>
        std::optional<FormatInfo> GetFormatInfo(const std::wstring& filePath);

        /// <summary>
        /// Get all supported formats by the current WIC installation
        /// </summary>
        /// <returns>List of all supported image formats</returns>
        std::vector<FormatInfo> GetSupportedFormats();

        /// <summary>
        /// Check if specific metadata type is available in the file
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="metadataType">Type to check (e.g., L"exif", L"iptc", L"xmp")</param>
        /// <returns>True if the metadata type exists</returns>
        bool HasMetadataType(const std::wstring& filePath, const std::wstring& metadataType);

        /// <summary>
        /// Extract specific metadata value by WIC property path
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="propertyPath">WIC metadata path (e.g., L"/app1/ifd/exif/{ushort=272}")</param>
        /// <returns>Metadata value if found</returns>
        std::optional<MetadataValue> GetMetadataByPath(const std::wstring& filePath, 
                                                      const std::wstring& propertyPath);

        /// <summary>
        /// Smart metadata formatter with WIC-aware formatting
        /// </summary>
        class MetadataFormatter
        {
        public:
            /// <summary>
            /// Format any metadata value to string with appropriate formatting
            /// </summary>
            static std::wstring FormatValue(const MetadataValue& value);

            /// <summary>
            /// Format GPS coordinates to human-readable string
            /// </summary>
            static std::wstring FormatGpsCoordinates(const MetadataValue& latitude, 
                                                   const MetadataValue& longitude,
                                                   const MetadataValue& latRef = {},
                                                   const MetadataValue& lonRef = {});

            /// <summary>
            /// Format camera settings to readable string
            /// </summary>
            static std::wstring FormatCameraSettings(const std::map<std::wstring, MetadataValue>& exifData);

            /// <summary>
            /// Format date/time with various output formats
            /// </summary>
            static std::wstring FormatDateTime(const MetadataValue& dateTime, 
                                             const std::wstring& format = L"yyyy-MM-dd");

            /// <summary>
            /// Format exposure settings (aperture, shutter, ISO)
            /// </summary>
            static std::wstring FormatExposureSettings(const MetadataValue& aperture,
                                                     const MetadataValue& shutter,
                                                     const MetadataValue& iso);
        };

        /// <summary>
        /// PowerRename integration helper - provides common rename patterns
        /// </summary>
        class RenamePatternProvider
        {
        public:
            /// <summary>
            /// Get available rename patterns based on available metadata
            /// </summary>
            static std::vector<std::wstring> GetAvailablePatterns(const ImageInfo& imageInfo);

            /// <summary>
            /// Resolve pattern to actual value
            /// </summary>
            static std::wstring ResolvePattern(const std::wstring& pattern, const ImageInfo& imageInfo);

            /// <summary>
            /// Get smart suggestions for rename patterns based on file content
            /// </summary>
            static std::vector<std::wstring> GetSmartSuggestions(const ImageInfo& imageInfo);

            /// <summary>
            /// Batch pattern resolution for multiple patterns
            /// </summary>
            static std::map<std::wstring, std::wstring> ResolvePatterns(
                const std::vector<std::wstring>& patterns, 
                const ImageInfo& imageInfo);
        };



        /// <summary>
        /// Metadata cache management
        /// </summary>
        void ClearCache();
        void SetCacheEnabled(bool enabled);
        size_t GetCacheSize() const;

    private:
        class Impl;
        std::unique_ptr<Impl> m_pImpl;
    };

    /// <summary>
    /// Convenient aliases for backward compatibility and easier usage
    /// </summary>
    using MediaMetadataExtractor = WICMetadataExtractor;  // Alias for existing code
    using ImageMetadata = WICMetadataExtractor::ImageInfo; // Alias for existing code
}
