#include "pch.h"
#include "WICMetadataExtractor.h"
#include <filesystem>
#include <sstream>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerRenameLib;

namespace WICMetadataExtractorTests
{
    // Helper function to get the test data directory path
    std::wstring GetTestDataPath()
    {
        // Get the directory where the test DLL is located
        // When running with vstest, we need to get the DLL module handle
        HMODULE hModule = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                          reinterpret_cast<LPCWSTR>(&GetTestDataPath),
                          &hModule);
        
        wchar_t modulePath[MAX_PATH];
        GetModuleFileNameW(hModule, modulePath, MAX_PATH);
        std::filesystem::path dllPath(modulePath);
        
        // Navigate to the test data directory
        // The test data is in the output directory alongside the DLL
        std::filesystem::path testDataPath = dllPath.parent_path() / L"testdata";
        
        return testDataPath.wstring();
    }

    TEST_CLASS(ExtractEXIFMetadataTests)
    {
    public:
        TEST_METHOD(ExtractEXIF_InvalidFile_ReturnsFalse)
        {
            // Test that EXIF extraction fails for nonexistent file
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\nonexistent.jpg";
            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            Assert::IsFalse(result, L"EXIF extraction should fail for nonexistent file");
        }

        TEST_METHOD(ExtractEXIF_ExifTest_AllFields)
        {
            // Test exif_test.jpg which contains comprehensive EXIF data
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\exif_test.jpg";
            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            Assert::IsTrue(result, L"EXIF extraction should succeed");
            
            // Verify all the fields that are in exif_test.jpg
            Assert::IsTrue(metadata.cameraMake.has_value(), L"Camera make should be present");
            Assert::AreEqual(L"samsung", metadata.cameraMake.value().c_str(), L"Camera make should be samsung");
            
            Assert::IsTrue(metadata.cameraModel.has_value(), L"Camera model should be present");
            Assert::AreEqual(L"SM-G930P", metadata.cameraModel.value().c_str(), L"Camera model should be SM-G930P");
            
            Assert::IsTrue(metadata.lensModel.has_value(), L"Lens model should be present");
            Assert::AreEqual(L"Samsung Galaxy S7 Rear Camera", metadata.lensModel.value().c_str(), L"Lens model should match");
            
            Assert::IsTrue(metadata.iso.has_value(), L"ISO should be present");
            Assert::AreEqual(40, static_cast<int>(metadata.iso.value()), L"ISO should be 40");
            
            Assert::IsTrue(metadata.aperture.has_value(), L"Aperture should be present");
            Assert::AreEqual(1.7, metadata.aperture.value(), 0.01, L"Aperture should be f/1.7");
            
            Assert::IsTrue(metadata.shutterSpeed.has_value(), L"Shutter speed should be present");
            Assert::AreEqual(0.000625, metadata.shutterSpeed.value(), 0.000001, L"Shutter speed should be 0.000625s");
            
            Assert::IsTrue(metadata.focalLength.has_value(), L"Focal length should be present");
            Assert::AreEqual(4.2, metadata.focalLength.value(), 0.1, L"Focal length should be 4.2mm");
            
            Assert::IsTrue(metadata.flash.has_value(), L"Flash should be present");
            Assert::AreEqual(0u, static_cast<unsigned int>(metadata.flash.value()), L"Flash should be 0x0");
            
            Assert::IsTrue(metadata.exposureBias.has_value(), L"Exposure bias should be present");
            Assert::AreEqual(0.0, metadata.exposureBias.value(), 0.01, L"Exposure bias should be 0 EV");
            
            Assert::IsTrue(metadata.author.has_value(), L"Author should be present");
            Assert::AreEqual(L"Carl Seibert (Exif)", metadata.author.value().c_str(), L"Author should match");
            
            Assert::IsTrue(metadata.copyright.has_value(), L"Copyright should be present");
            Assert::IsTrue(metadata.copyright.value().find(L"Carl Seibert") != std::wstring::npos, L"Copyright should contain Carl Seibert");
        }

        TEST_METHOD(ExtractEXIF_ExifTest2_WidthHeight)
        {
            // Test exif_test_2.jpg which only contains width and height
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\exif_test_2.jpg";
            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            Assert::IsTrue(result, L"EXIF extraction should succeed");
            
            // exif_test_2.jpg only has width and height
            Assert::IsTrue(metadata.width.has_value(), L"Width should be present");
            Assert::AreEqual(1080u, static_cast<unsigned int>(metadata.width.value()), L"Width should be 1080px");
            
            Assert::IsTrue(metadata.height.has_value(), L"Height should be present");
            Assert::AreEqual(810u, static_cast<unsigned int>(metadata.height.value()), L"Height should be 810px");
            
            // Other fields should not be present
            Assert::IsFalse(metadata.cameraMake.has_value(), L"Camera make should not be present in exif_test_2.jpg");
            Assert::IsFalse(metadata.cameraModel.has_value(), L"Camera model should not be present in exif_test_2.jpg");
            Assert::IsFalse(metadata.iso.has_value(), L"ISO should not be present in exif_test_2.jpg");
        }

        TEST_METHOD(ExtractEXIF_ClearCache)
        {
            // Test cache clearing works
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\exif_test.jpg";
            
            bool result1 = extractor.ExtractEXIFMetadata(testFile, metadata);
            Assert::IsTrue(result1);
            
            extractor.ClearCache();
            
            EXIFMetadata metadata2;
            bool result2 = extractor.ExtractEXIFMetadata(testFile, metadata2);
            Assert::IsTrue(result2);
            
            // Both calls should succeed
            Assert::AreEqual(metadata.cameraMake.value().c_str(), metadata2.cameraMake.value().c_str());
        }
    };

    TEST_CLASS(ExtractXMPMetadataTests)
    {
    public:
        TEST_METHOD(ExtractXMP_InvalidFile_ReturnsFalse)
        {
            // Test that XMP extraction fails for nonexistent file
            WICMetadataExtractor extractor;
            XMPMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\nonexistent.jpg";
            bool result = extractor.ExtractXMPMetadata(testFile, metadata);

            Assert::IsFalse(result, L"XMP extraction should fail for nonexistent file");
        }

        TEST_METHOD(ExtractXMP_XmpTest_AllFields)
        {
            // Test xmp_test.jpg which contains comprehensive XMP data
            WICMetadataExtractor extractor;
            XMPMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\xmp_test.jpg";
            bool result = extractor.ExtractXMPMetadata(testFile, metadata);

            Assert::IsTrue(result, L"XMP extraction should succeed");
            
            // Verify all the fields that are in xmp_test.jpg
            Assert::IsTrue(metadata.title.has_value(), L"Title should be present");
            Assert::AreEqual(L"object name here", metadata.title.value().c_str(), L"Title should match");
            
            Assert::IsTrue(metadata.description.has_value(), L"Description should be present");
            Assert::IsTrue(metadata.description.value().find(L"This is a metadata test file") != std::wstring::npos, 
                L"Description should contain expected text");
            
            Assert::IsTrue(metadata.rights.has_value(), L"Rights should be present");
            Assert::AreEqual(L"metadatamatters.blog", metadata.rights.value().c_str(), L"Rights should match");
            
            Assert::IsTrue(metadata.creatorTool.has_value(), L"Creator tool should be present");
            Assert::IsTrue(metadata.creatorTool.value().find(L"Adobe Photoshop Lightroom") != std::wstring::npos,
                L"Creator tool should contain Lightroom");
            
            Assert::IsTrue(metadata.documentID.has_value(), L"Document ID should be present");
            Assert::IsTrue(metadata.documentID.value().find(L"xmp.did:") != std::wstring::npos, 
                L"Document ID should start with xmp.did:");
            
            Assert::IsTrue(metadata.instanceID.has_value(), L"Instance ID should be present");
            Assert::IsTrue(metadata.instanceID.value().find(L"xmp.iid:") != std::wstring::npos,
                L"Instance ID should start with xmp.iid:");
            
            Assert::IsTrue(metadata.subject.has_value(), L"Subject keywords should be present");
            Assert::IsTrue(metadata.subject.value().size() > 0, L"Should have at least one keyword");
        }

        TEST_METHOD(ExtractXMP_XmpTest2_BasicFields)
        {
            // Test xmp_test_2.jpg which only contains basic XMP fields
            WICMetadataExtractor extractor;
            XMPMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\xmp_test_2.jpg";
            bool result = extractor.ExtractXMPMetadata(testFile, metadata);

            Assert::IsTrue(result, L"XMP extraction should succeed");
            
            // xmp_test_2.jpg only has CreatorTool, DocumentID, and InstanceID
            Assert::IsTrue(metadata.creatorTool.has_value(), L"Creator tool should be present");
            Assert::IsTrue(metadata.creatorTool.value().find(L"Adobe Photoshop CS6") != std::wstring::npos,
                L"Creator tool should be Photoshop CS6");
            
            Assert::IsTrue(metadata.documentID.has_value(), L"Document ID should be present");
            Assert::IsTrue(metadata.documentID.value().find(L"xmp.did:") != std::wstring::npos, 
                L"Document ID should start with xmp.did:");
            
            Assert::IsTrue(metadata.instanceID.has_value(), L"Instance ID should be present");
            Assert::IsTrue(metadata.instanceID.value().find(L"xmp.iid:") != std::wstring::npos,
                L"Instance ID should start with xmp.iid:");
            
            // Other fields should not be present
            Assert::IsFalse(metadata.title.has_value(), L"Title should not be present in xmp_test_2.jpg");
            Assert::IsFalse(metadata.description.has_value(), L"Description should not be present in xmp_test_2.jpg");
            Assert::IsFalse(metadata.rights.has_value(), L"Rights should not be present in xmp_test_2.jpg");
            Assert::IsFalse(metadata.creator.has_value(), L"Creator should not be present in xmp_test_2.jpg");
        }

        TEST_METHOD(ExtractXMP_ClearCache)
        {
            // Test cache clearing works
            WICMetadataExtractor extractor;
            XMPMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\xmp_test.jpg";

            bool result1 = extractor.ExtractXMPMetadata(testFile, metadata);
            Assert::IsTrue(result1);

            extractor.ClearCache();

            XMPMetadata metadata2;
            bool result2 = extractor.ExtractXMPMetadata(testFile, metadata2);
            Assert::IsTrue(result2);

            // Both calls should succeed
            Assert::AreEqual(metadata.title.value().c_str(), metadata2.title.value().c_str());
        }
    };

    TEST_CLASS(ExtractHEIFMetadataTests)
    {
    public:
        TEST_METHOD(ExtractHEIF_EXIF_CameraInfo)
        {
            // Test HEIF EXIF extraction - camera information
            // This test requires HEIF Image Extensions to be installed from Microsoft Store
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            // Check if file exists first
            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            // If HEIF extension is not installed, extraction may fail
            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify camera information from iPhone
            Assert::IsTrue(metadata.cameraMake.has_value(), L"Camera make should be present");
            Assert::AreEqual(L"Apple", metadata.cameraMake.value().c_str(), L"Camera make should be Apple");

            Assert::IsTrue(metadata.cameraModel.has_value(), L"Camera model should be present");
            // Model should contain "iPhone"
            Assert::IsTrue(metadata.cameraModel.value().find(L"iPhone") != std::wstring::npos,
                L"Camera model should contain iPhone");
        }

        TEST_METHOD(ExtractHEIF_EXIF_DateTaken)
        {
            // Test HEIF EXIF extraction - date taken
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify date taken is present
            Assert::IsTrue(metadata.dateTaken.has_value(), L"Date taken should be present");

            // Verify the date is a reasonable year (2020-2030 range)
            SYSTEMTIME dt = metadata.dateTaken.value();
            Assert::IsTrue(dt.wYear >= 2020 && dt.wYear <= 2030, L"Date taken year should be reasonable");
        }

        TEST_METHOD(ExtractHEIF_EXIF_ShootingParameters)
        {
            // Test HEIF EXIF extraction - shooting parameters
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify shooting parameters are present
            Assert::IsTrue(metadata.iso.has_value(), L"ISO should be present");
            Assert::IsTrue(metadata.iso.value() > 0, L"ISO should be positive");

            Assert::IsTrue(metadata.aperture.has_value(), L"Aperture should be present");
            Assert::IsTrue(metadata.aperture.value() > 0, L"Aperture should be positive");

            Assert::IsTrue(metadata.shutterSpeed.has_value(), L"Shutter speed should be present");
            Assert::IsTrue(metadata.shutterSpeed.value() > 0, L"Shutter speed should be positive");

            Assert::IsTrue(metadata.focalLength.has_value(), L"Focal length should be present");
            Assert::IsTrue(metadata.focalLength.value() > 0, L"Focal length should be positive");
        }

        TEST_METHOD(ExtractHEIF_EXIF_GPS)
        {
            // Test HEIF EXIF extraction - GPS coordinates
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify GPS coordinates are present (if the test file has GPS data)
            if (metadata.latitude.has_value() && metadata.longitude.has_value())
            {
                // Latitude should be between -90 and 90
                Assert::IsTrue(metadata.latitude.value() >= -90.0 && metadata.latitude.value() <= 90.0,
                    L"Latitude should be valid");

                // Longitude should be between -180 and 180
                Assert::IsTrue(metadata.longitude.value() >= -180.0 && metadata.longitude.value() <= 180.0,
                    L"Longitude should be valid");
            }
            else
            {
                Logger::WriteMessage(L"GPS data not present in test file");
            }
        }

        TEST_METHOD(ExtractHEIF_EXIF_ImageDimensions)
        {
            // Test HEIF EXIF extraction - image dimensions
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify image dimensions are present
            Assert::IsTrue(metadata.width.has_value(), L"Width should be present");
            Assert::IsTrue(metadata.width.value() > 0, L"Width should be positive");

            Assert::IsTrue(metadata.height.has_value(), L"Height should be present");
            Assert::IsTrue(metadata.height.value() > 0, L"Height should be positive");
        }

        TEST_METHOD(ExtractHEIF_EXIF_LensModel)
        {
            // Test HEIF EXIF extraction - lens model
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\heif_test.heic";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"HEIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"HEIF extraction failed - HEIF Image Extensions may not be installed");
                return;
            }

            Assert::IsTrue(result, L"HEIF EXIF extraction should succeed");

            // Verify lens model is present (iPhone photos typically have this)
            if (metadata.lensModel.has_value())
            {
                Assert::IsFalse(metadata.lensModel.value().empty(), L"Lens model should not be empty");
            }
            else
            {
                Logger::WriteMessage(L"Lens model not present in test file");
            }
        }
    };

    TEST_CLASS(ExtractAVIFMetadataTests)
    {
    public:
        TEST_METHOD(ExtractAVIF_EXIF_CameraInfo)
        {
            // Test AVIF EXIF extraction - camera information
            // This test requires AV1 Video Extension to be installed from Microsoft Store
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\avif_test.avif";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"AVIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"AVIF extraction failed - AV1 Video Extension may not be installed");
                return;
            }

            Assert::IsTrue(result, L"AVIF EXIF extraction should succeed");

            // Verify camera information
            if (metadata.cameraMake.has_value())
            {
                Assert::IsFalse(metadata.cameraMake.value().empty(), L"Camera make should not be empty");
            }

            if (metadata.cameraModel.has_value())
            {
                Assert::IsFalse(metadata.cameraModel.value().empty(), L"Camera model should not be empty");
            }
        }

        TEST_METHOD(ExtractAVIF_EXIF_DateTaken)
        {
            // Test AVIF EXIF extraction - date taken
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\avif_test.avif";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"AVIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"AVIF extraction failed - AV1 Video Extension may not be installed");
                return;
            }

            Assert::IsTrue(result, L"AVIF EXIF extraction should succeed");

            // Verify date taken is present
            if (metadata.dateTaken.has_value())
            {
                SYSTEMTIME dt = metadata.dateTaken.value();
                Assert::IsTrue(dt.wYear >= 2000 && dt.wYear <= 2100, L"Date taken year should be reasonable");
            }
            else
            {
                Logger::WriteMessage(L"Date taken not present in AVIF test file");
            }
        }

        TEST_METHOD(ExtractAVIF_EXIF_ImageDimensions)
        {
            // Test AVIF EXIF extraction - image dimensions
            WICMetadataExtractor extractor;
            EXIFMetadata metadata;

            std::wstring testFile = GetTestDataPath() + L"\\avif_test.avif";

            if (!std::filesystem::exists(testFile))
            {
                Logger::WriteMessage(L"AVIF test file not found, skipping test");
                return;
            }

            bool result = extractor.ExtractEXIFMetadata(testFile, metadata);

            if (!result)
            {
                Logger::WriteMessage(L"AVIF extraction failed - AV1 Video Extension may not be installed");
                return;
            }

            Assert::IsTrue(result, L"AVIF EXIF extraction should succeed");

            // Verify image dimensions are present
            if (metadata.width.has_value())
            {
                Assert::IsTrue(metadata.width.value() > 0, L"Width should be positive");
            }

            if (metadata.height.has_value())
            {
                Assert::IsTrue(metadata.height.value() > 0, L"Height should be positive");
            }
        }
    };
}
