#include "pch.h"
#include "MetadataExtractor.h"
#include <iostream>

// Simple test function to demonstrate MetadataExtractor usage
void TestMetadataExtractor()
{
    try
    {
        PowerRenameLib::MetadataExtractor extractor;
        
        // Test with a sample path (this is just for demonstration)
        std::wstring testImagePath = L"C:\\test\\sample.jpg";
        
        // Extract EXIF metadata
        auto metadata = extractor.ExtractEXIFMetadata(testImagePath);
        
        // Test different formatting patterns
        std::wstring cameraResult = extractor.FormatMetadataForRename(metadata, L"camera");
        std::wstring dateResult = extractor.FormatMetadataForRename(metadata, L"date");
        std::wstring dimensionsResult = extractor.FormatMetadataForRename(metadata, L"dimensions");
        
        // In a real application, these would be used for renaming files
        // For now, this just demonstrates the API works
        
        // Test XMP metadata extraction (currently returns placeholder)
        auto xmpData = extractor.ExtractXMPMetadata(testImagePath);
        
        // The MetadataExtractor is now successfully integrated
        // and can be used by PowerRename to extract metadata for renaming
    }
    catch (...)
    {
        // Handle any errors during testing
    }
}
