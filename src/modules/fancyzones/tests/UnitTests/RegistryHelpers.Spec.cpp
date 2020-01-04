#include "pch.h"
#include "lib\RegistryHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(RegistryHelpersUnitTests){
        public:
            TEST_METHOD(GetDefaultKey){
                // Test the path to the key is the same string.
                wchar_t key[256];
    Assert::AreEqual(0, wcscmp(RegistryHelpers::GetKey(nullptr, key, ARRAYSIZE(key)), L"Software\\SuperFancyZones"));
}

TEST_METHOD(GetKeyWithMonitor)
{
    // Test the path to the key is the same string.
    wchar_t key[256];
    Assert::AreEqual(0, wcscmp(RegistryHelpers::GetKey(L"Monitor1", key, ARRAYSIZE(key)), L"Software\\SuperFancyZones\\Monitor1"));
}

TEST_METHOD(OpenKey)
{
    // The default key should exist.
    wil::unique_hkey key{ RegistryHelpers::OpenKey({}) };
    Assert::IsNotNull(key.get());

    // The Monitor1 key shouldn't exist.
    wil::unique_hkey key2{ RegistryHelpers::OpenKey(L"Monitor1") };
    Assert::IsNull(key2.get());
}
}
;
}
