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
}
