// Modern WIC-based MediaMetadataExtractor Example
// This demonstrates the full capabilities of the new WIC-based implementation

#include "MediaMetadataExtractor.h"
#include <iostream>
#include <iomanip>

using namespace PowerRenameLib;

void PrintImageInfo(const WICMetadataExtractor::ImageInfo& imageInfo)
{
    std::wcout << L"=== WIC Image Information ===" << std::endl;
    
    // Basic image properties
    std::wcout << L"Dimensions: " << imageInfo.width << L"x" << imageInfo.height << std::endl;
    std::wcout << L"Bits per Pixel: " << imageInfo.bitsPerPixel << std::endl;
    std::wcout << L"Pixel Format: " << imageInfo.pixelFormat << std::endl;
    std::wcout << L"Container Format: " << imageInfo.containerFormat << std::endl;
    
    // EXIF metadata
    if (!imageInfo.exifData.empty())
    {
        std::wcout << L"\n--- EXIF Data ---" << std::endl;
        for (const auto& [key, value] : imageInfo.exifData)
        {
            std::wcout << key << L": " << WICMetadataExtractor::MetadataFormatter::FormatValue(value) << std::endl;
        }
    }
    
    // GPS metadata
    if (!imageInfo.gpsData.empty())
    {
        std::wcout << L"\n--- GPS Data ---" << std::endl;
        for (const auto& [key, value] : imageInfo.gpsData)
        {
            std::wcout << key << L": " << WICMetadataExtractor::MetadataFormatter::FormatValue(value) << std::endl;
        }
    }
    
    // IPTC metadata
    if (!imageInfo.iptcData.empty())
    {
        std::wcout << L"\n--- IPTC Data ---" << std::endl;
        for (const auto& [key, value] : imageInfo.iptcData)
        {
            std::wcout << key << L": " << WICMetadataExtractor::MetadataFormatter::FormatValue(value) << std::endl;
        }
    }
    
    // XMP metadata
    if (!imageInfo.xmpData.empty())
    {
        std::wcout << L"\n--- XMP Data ---" << std::endl;
        for (const auto& [key, value] : imageInfo.xmpData)
        {
            std::wcout << key << L": " << WICMetadataExtractor::MetadataFormatter::FormatValue(value) << std::endl;
        }
    }
    
    // Custom metadata
    if (!imageInfo.customData.empty())
    {
        std::wcout << L"\n--- Additional Metadata ---" << std::endl;
        for (const auto& [key, value] : imageInfo.customData)
        {
            std::wcout << key << L": " << WICMetadataExtractor::MetadataFormatter::FormatValue(value) << std::endl;
        }
    }
}

void DemonstrateBasicUsage()
{
    std::wcout << L"=== Basic Usage Demo ===" << std::endl;
    
    WICMetadataExtractor extractor;
    
    // Example file path - replace with actual image file
    std::wstring filePath = L"C:\\example\\photo.jpg";
    
    // Check format support
    auto formatInfo = extractor.GetFormatInfo(filePath);
    if (formatInfo.has_value())
    {
        std::wcout << L"Format: " << formatInfo->formatName << std::endl;
        std::wcout << L"Extensions: " << formatInfo->fileExtensions << std::endl;
        std::wcout << L"Supports Metadata: " << (formatInfo->supportsMetadata ? L"Yes" : L"No") << std::endl;
        std::wcout << L"Multi-frame: " << (formatInfo->supportsMultiFrame ? L"Yes" : L"No") << std::endl;
    }
    
    // Extract comprehensive metadata
    WICMetadataExtractor::ExtractionOptions options;
    options.includeExif = true;
    options.includeIptc = true;
    options.includeXmp = true;
    options.includeGps = true;
    options.includeCustom = true;
    
    auto imageInfo = extractor.ExtractImageInfo(filePath, options);
    if (imageInfo.has_value())
    {
        PrintImageInfo(imageInfo.value());
    }
    else
    {
        std::wcout << L"Failed to extract metadata" << std::endl;
    }
}

void DemonstrateRenamePatterns()
{
    std::wcout << L"\n=== Rename Pattern Demo ===" << std::endl;
    
    WICMetadataExtractor extractor;
    std::wstring filePath = L"C:\\example\\photo.jpg";
    
    auto imageInfo = extractor.ExtractImageInfo(filePath);
    if (!imageInfo.has_value())
    {
        std::wcout << L"No image data available for pattern demo" << std::endl;
        return;
    }
    
    // Get available patterns
    auto patterns = WICMetadataExtractor::RenamePatternProvider::GetAvailablePatterns(imageInfo.value());
    
    std::wcout << L"Available Patterns:" << std::endl;
    for (const auto& pattern : patterns)
    {
        std::wstring value = WICMetadataExtractor::RenamePatternProvider::ResolvePattern(pattern, imageInfo.value());
        std::wcout << L"{" << pattern << L"} -> " << value << std::endl;
    }
    
    // Get smart suggestions
    auto suggestions = WICMetadataExtractor::RenamePatternProvider::GetSmartSuggestions(imageInfo.value());
    
    std::wcout << L"\nSmart Rename Suggestions:" << std::endl;
    for (const auto& suggestion : suggestions)
    {
        std::wcout << L"  " << suggestion << std::endl;
    }
}

void DemonstrateAdvancedFeatures()
{
    std::wcout << L"\n=== Advanced Features Demo ===" << std::endl;
    
    WICMetadataExtractor extractor;
    
    // List all supported formats
    auto supportedFormats = extractor.GetSupportedFormats();
    std::wcout << L"Supported formats (" << supportedFormats.size() << L"):" << std::endl;
    for (const auto& format : supportedFormats)
    {
        std::wcout << L"  " << format.formatName << L" (" << format.fileExtensions << L")" << std::endl;
    }
    
    // Test specific metadata types
    std::wstring filePath = L"C:\\example\\photo.jpg";
    
    std::wcout << L"\nMetadata type availability:" << std::endl;
    std::wcout << L"  EXIF: " << (extractor.HasMetadataType(filePath, L"exif") ? L"Yes" : L"No") << std::endl;
    std::wcout << L"  IPTC: " << (extractor.HasMetadataType(filePath, L"iptc") ? L"Yes" : L"No") << std::endl;
    std::wcout << L"  XMP: " << (extractor.HasMetadataType(filePath, L"xmp") ? L"Yes" : L"No") << std::endl;
    std::wcout << L"  GPS: " << (extractor.HasMetadataType(filePath, L"gps") ? L"Yes" : L"No") << std::endl;
    
    // Extract specific metadata by path
    auto cameraModel = extractor.GetMetadataByPath(filePath, L"/app1/ifd/{ushort=272}");
    if (cameraModel.has_value())
    {
        std::wcout << L"Camera Model (direct path): " << 
            WICMetadataExtractor::MetadataFormatter::FormatValue(cameraModel.value()) << std::endl;
    }
    
    // Cache management
    std::wcout << L"Cache size: " << extractor.GetCacheSize() << L" items" << std::endl;
}

void DemonstrateFormatterFeatures()
{
    std::wcout << L"\n=== Formatter Features Demo ===" << std::endl;
    
    // Create sample metadata values for demonstration
    WICMetadataExtractor::MetadataValue sampleDouble = 2.8;
    WICMetadataExtractor::MetadataValue sampleInt = 400;
    WICMetadataExtractor::MetadataValue sampleString = std::wstring(L"Canon EOS R5");
    WICMetadataExtractor::MetadataValue sampleBool = true;
    
    std::wcout << L"Formatted values:" << std::endl;
    std::wcout << L"  Double: " << WICMetadataExtractor::MetadataFormatter::FormatValue(sampleDouble) << std::endl;
    std::wcout << L"  Integer: " << WICMetadataExtractor::MetadataFormatter::FormatValue(sampleInt) << std::endl;
    std::wcout << L"  String: " << WICMetadataExtractor::MetadataFormatter::FormatValue(sampleString) << std::endl;
    std::wcout << L"  Boolean: " << WICMetadataExtractor::MetadataFormatter::FormatValue(sampleBool) << std::endl;
    
    // Demonstrate exposure settings formatting
    std::wcout << L"Exposure Settings: " << 
        WICMetadataExtractor::MetadataFormatter::FormatExposureSettings(sampleDouble, 0.008, sampleInt) << std::endl;
}

// Main example function
void RunCompleteExample()
{
    std::wcout << L"WIC-based MediaMetadataExtractor Complete Example" << std::endl;
    std::wcout << L"=================================================" << std::endl;
    
    try
    {
        DemonstrateBasicUsage();
        DemonstrateRenamePatterns();
        DemonstrateAdvancedFeatures();
        DemonstrateFormatterFeatures();
        
        std::wcout << L"\nExample completed successfully!" << std::endl;
    }
    catch (const std::exception& e)
    {
        std::wcout << L"Error: " << e.what() << std::endl;
    }
    catch (...)
    {
        std::wcout << L"Unknown error occurred" << std::endl;
    }
}
