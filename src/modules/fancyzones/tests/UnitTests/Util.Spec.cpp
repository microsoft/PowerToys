#include "pch.h"
#include "lib\util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(UtilUnitTests){
        public:
            TEST_METHOD(TestParseDeviceId){
                // We're interested in the unique part between the first and last #'s
                // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
                // Example output: DELA026#5&10a58c63&0&UID16777488
                PCWSTR input = L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    wchar_t output[256]{};
    ParseDeviceId(input, output, ARRAYSIZE(output));
    Assert::AreEqual(0, wcscmp(output, L"DELA026#5&10a58c63&0&UID16777488"));
}

TEST_METHOD(TestParseInvalidDeviceId)
{
    // We're interested in the unique part between the first and last #'s
    // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
    // Example output: DELA026#5&10a58c63&0&UID16777488
    PCWSTR input = L"AnInvalidDeviceId";
    wchar_t output[256]{};
    ParseDeviceId(input, output, ARRAYSIZE(output));
    Assert::AreEqual(0, wcscmp(output, L"FallbackDevice"));
}
}
;
}
