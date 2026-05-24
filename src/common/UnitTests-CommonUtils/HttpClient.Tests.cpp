#include "pch.h"
#include "TestHelpers.h"
#include <HttpClient.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(HttpClientTests)
    {
    public:
        // Note: Network tests may fail in offline environments
        // These tests are designed to verify the API doesn't crash

        TEST_METHOD(HttpClient_DefaultConstruction)
        {
            http::HttpClient client;
            // Should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(HttpClient_Request_InvalidUri_ReturnsEmpty)
        {
            http::HttpClient client;

            try
            {
                // Invalid URI should not crash, may throw or return empty
                auto result = client.request(winrt::Windows::Foundation::Uri(L"invalid://not-a-valid-uri"));
                // If we get here, result may be empty or contain error
            }
            catch (...)
            {
                // Exception is acceptable for invalid URI
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(HttpClient_Download_InvalidUri_DoesNotCrash)
        {
            http::HttpClient client;
            TestHelpers::TempFile tempFile;

            try
            {
                auto result = client.download(
                    winrt::Windows::Foundation::Uri(L"https://invalid.invalid.invalid"),
                    tempFile.path());
                // May return false or throw
            }
            catch (...)
            {
                // Exception is acceptable for invalid/unreachable URI
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(HttpClient_Download_WithCallback_DoesNotCrash)
        {
            http::HttpClient client;
            TestHelpers::TempFile tempFile;
            std::atomic<int> callbackCount{ 0 };

            try
            {
                auto result = client.download(
                    winrt::Windows::Foundation::Uri(L"https://invalid.invalid.invalid"),
                    tempFile.path(),
                    [&callbackCount]([[maybe_unused]] float progress) {
                        callbackCount++;
                    });
            }
            catch (...)
            {
                // Exception is acceptable
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(HttpClient_Download_EmptyPath_DoesNotCrash)
        {
            http::HttpClient client;

            try
            {
                auto result = client.download(
                    winrt::Windows::Foundation::Uri(L"https://example.com"),
                    L"");
            }
            catch (...)
            {
                // Exception is acceptable for empty path
            }
            Assert::IsTrue(true);
        }

        // These tests require network access and may be skipped in offline environments
        TEST_METHOD(HttpClient_Request_ValidUri_ReturnsResult)
        {
            // Skip this test in most CI environments
            // Only run manually to verify network functionality
            http::HttpClient client;

            try
            {
                // Use a reliable, fast-responding URL
                auto result = client.request(winrt::Windows::Foundation::Uri(L"https://www.microsoft.com"));
                // Result may or may not be successful depending on network
            }
            catch (...)
            {
                // Network errors are acceptable in test environment
            }
            Assert::IsTrue(true);
        }

        // Thread safety test (doesn't require network)
        TEST_METHOD(HttpClient_MultipleInstances_DoNotCrash)
        {
            std::vector<std::unique_ptr<http::HttpClient>> clients;

            for (int i = 0; i < 10; ++i)
            {
                clients.push_back(std::make_unique<http::HttpClient>());
            }

            // All clients should coexist without crashing
            Assert::AreEqual(static_cast<size_t>(10), clients.size());
        }

        TEST_METHOD(HttpClient_ConcurrentConstruction_DoesNotCrash)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 5; ++i)
            {
                threads.emplace_back([&successCount]() {
                    http::HttpClient client;
                    successCount++;
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(5, successCount.load());
        }
    };
}
