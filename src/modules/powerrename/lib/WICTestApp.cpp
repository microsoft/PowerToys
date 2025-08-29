#include "pch.h"
#include "MediaMetadataExtractor.h"
#include <iostream>
#include <iomanip>

using namespace PowerRenameLib;

void PrintMetadataValue(const WICMetadataExtractor::MetadataValue& value)
{
    std::visit([](const auto& v) {
        using T = std::decay_t<decltype(v)>;
        if constexpr (std::is_same_v<T, std::wstring>) {
            std::wcout << v;
        } else if constexpr (std::is_same_v<T, int32_t>) {
            std::wcout << v;
        } else if constexpr (std::is_same_v<T, uint32_t>) {
            std::wcout << v;
        } else if constexpr (std::is_same_v<T, double>) {
            std::wcout << std::fixed << std::setprecision(2) << v;
        } else if constexpr (std::is_same_v<T, bool>) {
            std::wcout << (v ? L"true" : L"false");
        } else if constexpr (std::is_same_v<T, std::vector<uint8_t>>) {
            std::wcout << L"[Binary data: " << v.size() << L" bytes]";
        }
    }, value);
}

void PrintImageInfo(const WICMetadataExtractor::ImageInfo& info)
{
    std::wcout << L"Image Information:\n";
    std::wcout << L"  Dimensions: " << info.width << L" x " << info.height << L"\n";
    std::wcout << L"  Format: " << info.containerFormat << L"\n";
    std::wcout << L"  Pixel Format: " << info.pixelFormat << L"\n";
    std::wcout << L"  Bits per Pixel: " << info.bitsPerPixel << L"\n\n";

    if (!info.exifData.empty()) {
        std::wcout << L"EXIF Data:\n";
        for (const auto& [key, value] : info.exifData) {
            std::wcout << L"  " << key << L": ";
            PrintMetadataValue(value);
            std::wcout << L"\n";
        }
        std::wcout << L"\n";
    }

    if (!info.gpsData.empty()) {
        std::wcout << L"GPS Data:\n";
        for (const auto& [key, value] : info.gpsData) {
            std::wcout << L"  " << key << L": ";
            PrintMetadataValue(value);
            std::wcout << L"\n";
        }
        std::wcout << L"\n";
    }

    if (!info.iptcData.empty()) {
        std::wcout << L"IPTC Data:\n";
        for (const auto& [key, value] : info.iptcData) {
            std::wcout << L"  " << key << L": ";
            PrintMetadataValue(value);
            std::wcout << L"\n";
        }
        std::wcout << L"\n";
    }
}

int wmain(int argc, wchar_t* argv[])
{
    if (argc != 2) {
        std::wcout << L"Usage: WICTestApp <image_file_path>\n";
        return 1;
    }

    const std::wstring filePath = argv[1];
    
    std::wcout << L"Testing WIC Metadata Extractor with: " << filePath << L"\n\n";

    try {
        WICMetadataExtractor extractor;
        
        // Test format info
        auto formatInfo = extractor.GetFormatInfo(filePath);
        if (formatInfo.has_value()) {
            std::wcout << L"Format Information:\n";
            std::wcout << L"  Format Name: " << formatInfo->formatName << L"\n";
            std::wcout << L"  File Extensions: " << formatInfo->fileExtensions << L"\n";
            std::wcout << L"  Supports Metadata: " << (formatInfo->supportsMetadata ? L"Yes" : L"No") << L"\n";
            std::wcout << L"  Supports Multi-Frame: " << (formatInfo->supportsMultiFrame ? L"Yes" : L"No") << L"\n\n";
        }

        // Test image info extraction
        WICMetadataExtractor::ExtractionOptions options;
        options.includeExif = true;
        options.includeGps = true;
        options.includeIptc = true;
        options.includeXmp = true;
        options.cacheMetadata = true;

        auto imageInfo = extractor.ExtractImageInfo(filePath, options);
        if (imageInfo.has_value()) {
            PrintImageInfo(imageInfo.value());
        } else {
            std::wcout << L"Failed to extract image information.\n";
        }

        // Test metadata type detection
        std::wcout << L"Metadata Type Detection:\n";
        std::wcout << L"  Has EXIF: " << (extractor.HasMetadataType(filePath, L"exif") ? L"Yes" : L"No") << L"\n";
        std::wcout << L"  Has GPS: " << (extractor.HasMetadataType(filePath, L"gps") ? L"Yes" : L"No") << L"\n";
        std::wcout << L"  Has IPTC: " << (extractor.HasMetadataType(filePath, L"iptc") ? L"Yes" : L"No") << L"\n";
        std::wcout << L"  Has XMP: " << (extractor.HasMetadataType(filePath, L"xmp") ? L"Yes" : L"No") << L"\n\n";

        // Test supported formats
        auto formats = extractor.GetSupportedFormats();
        std::wcout << L"Supported Formats (" << formats.size() << L" total):\n";
        for (size_t i = 0; i < std::min(formats.size(), 5ul); ++i) {
            std::wcout << L"  " << formats[i].formatName << L" (" << formats[i].fileExtensions << L")\n";
        }
        if (formats.size() > 5) {
            std::wcout << L"  ... and " << (formats.size() - 5) << L" more\n";
        }

    } catch (const std::exception& e) {
        std::cout << "Exception: " << e.what() << std::endl;
        return 1;
    } catch (...) {
        std::wcout << L"Unknown exception occurred.\n";
        return 1;
    }

    return 0;
}
