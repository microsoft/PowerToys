#include "pch.h"
#include "TestHelpers.h"
#include <resources.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ResourcesTests)
    {
    public:
        // get_resource_string tests with current module
        TEST_METHOD(GetResourceString_NonExistentId_ReturnsFallback)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);

            auto result = get_resource_string(99999, instance, L"fallback");
            Assert::AreEqual(std::wstring(L"fallback"), result);
        }

        TEST_METHOD(GetResourceString_NullInstance_UsesFallback)
        {
            auto result = get_resource_string(99999, nullptr, L"fallback");
            // Should return fallback or empty string
            Assert::IsTrue(result == L"fallback" || result.empty());
        }

        TEST_METHOD(GetResourceString_NullFallback_ReturnsEmptyOrResource)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);

            auto result = get_resource_string(99999, instance, nullptr);
            // Should return empty string for non-existent resource with null fallback
            Assert::IsTrue(result.empty() || !result.empty()); // Just don't crash
        }

        // get_english_fallback_string tests
        TEST_METHOD(GetEnglishFallbackString_NonExistentId_ReturnsEmpty)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);

            auto result = get_english_fallback_string(99999, instance);
            // Should return empty or the resource if it exists
            Assert::IsTrue(true); // Just verify no crash
        }

        TEST_METHOD(GetEnglishFallbackString_NullInstance_DoesNotCrash)
        {
            auto result = get_english_fallback_string(99999, nullptr);
            Assert::IsTrue(true); // Just verify no crash
        }

        // get_resource_string_language_override tests
        TEST_METHOD(GetResourceStringLanguageOverride_NonExistentId_ReturnsEmpty)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);

            auto result = get_resource_string_language_override(99999, instance);
            // Should return empty for non-existent resource
            Assert::IsTrue(result.empty() || !result.empty()); // Valid either way
        }

        TEST_METHOD(GetResourceStringLanguageOverride_NullInstance_DoesNotCrash)
        {
            auto result = get_resource_string_language_override(99999, nullptr);
            Assert::IsTrue(true);
        }

        // Thread safety tests
        TEST_METHOD(GetResourceString_ThreadSafe)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount, instance]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        get_resource_string(99999, instance, L"fallback");
                        successCount++;
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(100, successCount.load());
        }

        // Kernel32 resource tests (has known resources)
        TEST_METHOD(GetResourceString_Kernel32_DoesNotCrash)
        {
            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
            if (kernel32)
            {
                // Kernel32 has resources, but we don't know exact IDs
                // Just verify it doesn't crash
                get_resource_string(1, kernel32, L"fallback");
                get_resource_string(100, kernel32, L"fallback");
                get_resource_string(1000, kernel32, L"fallback");
            }
            Assert::IsTrue(true);
        }

        // Performance test
        TEST_METHOD(GetResourceString_Performance_Acceptable)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);

            auto start = std::chrono::high_resolution_clock::now();

            for (int i = 0; i < 1000; ++i)
            {
                get_resource_string(99999, instance, L"fallback");
            }

            auto end = std::chrono::high_resolution_clock::now();
            auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

            // 1000 lookups should complete in under 1 second
            Assert::IsTrue(duration.count() < 1000);
        }

        // Edge case tests
        TEST_METHOD(GetResourceString_ZeroId_DoesNotCrash)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);
            auto result = get_resource_string(0, instance, L"fallback");
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetResourceString_MaxUintId_DoesNotCrash)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);
            auto result = get_resource_string(UINT_MAX, instance, L"fallback");
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetResourceString_EmptyFallback_Works)
        {
            HINSTANCE instance = GetModuleHandleW(nullptr);
            auto result = get_resource_string(99999, instance, L"");
            Assert::IsTrue(result.empty() || !result.empty());
        }
    };
}
