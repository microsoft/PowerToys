// Unit tests for WIC-based MediaMetadataExtractor
// This file provides basic tests for the new WIC implementation

#include "pch.h"
#include "MediaMetadataExtractor.h"
#include <gtest/gtest.h>
#include <filesystem>

using namespace PowerRenameLib;

class MediaMetadataExtractorTest : public ::testing::Test
{
protected:
    void SetUp() override
    {
        extractor = std::make_unique<MediaMetadataExtractor>();
    }

    void TearDown() override
    {
        extractor.reset();
    }

    std::unique_ptr<MediaMetadataExtractor> extractor;
};

TEST_F(MediaMetadataExtractorTest, InitializationTest)
{
    ASSERT_NE(extractor, nullptr);
}

TEST_F(MediaMetadataExtractorTest, FormatSupportTest)
{
    // Test common image formats
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.jpg"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.jpeg"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.png"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.gif"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.bmp"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.tiff"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.tif"));
    
    // Test case insensitive
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.JPG"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.PNG"));
    
    // Test RAW formats
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.cr2"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.nef"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.arw"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.dng"));
    
    // Test modern formats
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.heic"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.heif"));
    EXPECT_TRUE(extractor->IsFormatSupported(L"test.webp"));
    
    // Test unsupported formats
    EXPECT_FALSE(extractor->IsFormatSupported(L"test.txt"));
    EXPECT_FALSE(extractor->IsFormatSupported(L"test.pdf"));
    EXPECT_FALSE(extractor->IsFormatSupported(L"test.mp4"));
    
    // Test files without extension
    EXPECT_FALSE(extractor->IsFormatSupported(L"test"));
    EXPECT_FALSE(extractor->IsFormatSupported(L""));
}

TEST_F(MediaMetadataExtractorTest, NonExistentFileTest)
{
    // Test with non-existent file
    auto metadata = extractor->ExtractMetadata(L"non_existent_file.jpg");
    
    // Should return empty metadata without crashing
    EXPECT_EQ(metadata.width, 0);
    EXPECT_EQ(metadata.height, 0);
    EXPECT_TRUE(metadata.cameraModel.empty());
    EXPECT_TRUE(metadata.dateTaken.empty());
}

TEST_F(MediaMetadataExtractorTest, PatternFormattingTest)
{
    MediaMetadataExtractor::ImageMetadata metadata;
    
    // Set up test data
    metadata.width = 1920;
    metadata.height = 1080;
    metadata.cameraModel = L"Canon EOS R5";
    metadata.dateTaken = L"2024-03-15 14:30:25";
    metadata.iso = 400;
    metadata.aperture = 2.8;
    metadata.shutterSpeed = 0.008; // 1/125s
    metadata.focalLength = 85.0;
    metadata.gpsLocation = L"40.748817, -73.985428";
    metadata.artist = L"John Doe";
    metadata.title = L"Test Image";
    metadata.rating = L"5";
    metadata.orientation = L"Normal";
    metadata.colorSpace = L"sRGB";
    metadata.software = L"Adobe Lightroom";
    metadata.bitDepth = 24;
    metadata.flashUsed = true;
    
    // Test basic patterns
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"camera"), L"Canon EOS R5");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"date"), L"2024-03-15");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"dimensions"), L"1920x1080");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"width"), L"1920");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"height"), L"1080");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"iso"), L"ISO400");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"aperture"), L"f2.8");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"shutter"), L"1/125s");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"focal"), L"85mm");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"location"), L"40.748817, -73.985428");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"artist"), L"John Doe");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"title"), L"Test Image");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"rating"), L"5");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"orientation"), L"Normal");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"colorspace"), L"sRGB");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"software"), L"Adobe Lightroom");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"bitdepth"), L"24bit");
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"flash"), L"Flash");
    
    // Test datetime pattern
    std::wstring datetime = extractor->FormatMetadataForRename(metadata, L"datetime");
    EXPECT_EQ(datetime, L"2024-03-15_14-30-25");
    
    // Test unknown pattern
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"unknown"), L"Unknown");
    
    // Test empty metadata
    MediaMetadataExtractor::ImageMetadata emptyMetadata;
    EXPECT_EQ(extractor->FormatMetadataForRename(emptyMetadata, L"camera"), L"Unknown");
    EXPECT_EQ(extractor->FormatMetadataForRename(emptyMetadata, L"date"), L"Unknown");
    EXPECT_EQ(extractor->FormatMetadataForRename(emptyMetadata, L"iso"), L"Unknown");
    
    // Test flash when not used
    emptyMetadata.flashUsed = false;
    EXPECT_EQ(extractor->FormatMetadataForRename(emptyMetadata, L"flash"), L"NoFlash");
}

TEST_F(MediaMetadataExtractorTest, BackwardCompatibilityTest)
{
    // Test that ExtractEXIFMetadata still works (calls ExtractMetadata internally)
    auto metadata = extractor->ExtractEXIFMetadata(L"non_existent_file.jpg");
    
    // Should return empty metadata without crashing
    EXPECT_EQ(metadata.width, 0);
    EXPECT_EQ(metadata.height, 0);
    EXPECT_TRUE(metadata.cameraModel.empty());
}

TEST_F(MediaMetadataExtractorTest, ShutterSpeedFormattingTest)
{
    MediaMetadataExtractor::ImageMetadata metadata;
    
    // Test fast shutter speed (fraction)
    metadata.shutterSpeed = 0.008; // 1/125s
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"shutter"), L"1/125s");
    
    // Test very fast shutter speed
    metadata.shutterSpeed = 0.001; // 1/1000s
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"shutter"), L"1/1000s");
    
    // Test slow shutter speed (seconds)
    metadata.shutterSpeed = 2.5; // 2.5 seconds
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"shutter"), L"2.5s");
    
    // Test 1 second
    metadata.shutterSpeed = 1.0;
    EXPECT_EQ(extractor->FormatMetadataForRename(metadata, L"shutter"), L"1.0s");
}

// Performance test (optional - can be disabled for regular runs)
TEST_F(MediaMetadataExtractorTest, DISABLED_PerformanceTest)
{
    const int iterations = 1000;
    auto start = std::chrono::high_resolution_clock::now();
    
    for (int i = 0; i < iterations; ++i)
    {
        extractor->IsFormatSupported(L"test.jpg");
    }
    
    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    
    // Should be very fast (less than 1ms per call)
    EXPECT_LT(duration.count() / iterations, 1000); // Less than 1000 microseconds per call
}
