#include "pch.h"
#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

TCHAR g_MicrophoneDeviceId[1024]{};
void OutputDebug( const TCHAR*, ... ) {}

#include "..\ZoomIt\WhisperRecognizer.cpp"

namespace WhisperLanguageTests
{
    TEST_CLASS(WindowsDisplayLanguageTests)
    {
    public:
        TEST_METHOD(UsesWindowsDisplayLanguageWithoutRegionalSuffix)
        {
            const struct { LANGID language; const char* expected; } cases[] = {
                { 0x0409, "en" }, { 0x0407, "de" }, { 0x040c, "fr" },
                { 0x080a, "es" }, { 0x0416, "pt" }, { 0x0804, "zh" },
                { 0x0404, "zh" }, { 0x0411, "ja" }, { 0x0401, "ar" },
            };
            for( const auto& test : cases )
            {
                Assert::AreEqual( std::string( test.expected ), ResolveWhisperLanguage( test.language ) );
            }
        }

        TEST_METHOD(UnsupportedLanguageUsesAutomaticDetection)
        {
            Assert::AreEqual( std::string( "auto" ), ResolveWhisperLanguage( 0x0435 ) );
        }

        TEST_METHOD(InvalidWindowsLanguageUsesAutomaticDetection)
        {
            Assert::AreEqual( std::string( "auto" ), ResolveWhisperLanguage( 0xffff ) );
        }
    };
}
