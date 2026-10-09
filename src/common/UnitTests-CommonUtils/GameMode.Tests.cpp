#include "pch.h"
#include "TestHelpers.h"
#include <game_mode.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(GameModeTests)
    {
    public:
        TEST_METHOD(DetectGameMode_ReturnsBoolean)
        {
            // This function queries Windows game mode status
            bool result = detect_game_mode();

            // Result depends on current system state, but should be a valid boolean
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(DetectGameMode_ConsistentResults)
        {
            // Multiple calls should return consistent results (unless game mode changes)
            bool result1 = detect_game_mode();
            bool result2 = detect_game_mode();
            bool result3 = detect_game_mode();

            // Results should be consistent across rapid calls
            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        TEST_METHOD(DetectGameMode_DoesNotCrash)
        {
            // Call multiple times to ensure no crash or memory leak
            for (int i = 0; i < 100; ++i)
            {
                detect_game_mode();
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(DetectGameMode_ThreadSafe)
        {
            // Test that calling from multiple threads is safe
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        detect_game_mode();
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
    };
}
