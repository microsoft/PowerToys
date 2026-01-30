#include "pch.h"
#include "TestHelpers.h"
#include <serialized.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(SerializedTests)
    {
    public:
        // Basic Read tests
        TEST_METHOD(Read_DefaultState_ReturnsDefaultValue)
        {
            Serialized<int> s;
            int value = -1;
            s.Read([&value](const int& v) {
                value = v;
            });
            Assert::AreEqual(0, value); // Default constructed int is 0
        }

        TEST_METHOD(Read_StringType_ReturnsEmpty)
        {
            Serialized<std::string> s;
            std::string value = "initial";
            s.Read([&value](const std::string& v) {
                value = v;
            });
            Assert::AreEqual(std::string(""), value);
        }

        // Basic Access tests
        TEST_METHOD(Access_ModifyValue_ValueIsModified)
        {
            Serialized<int> s;
            s.Access([](int& v) {
                v = 42;
            });

            int value = 0;
            s.Read([&value](const int& v) {
                value = v;
            });
            Assert::AreEqual(42, value);
        }

        TEST_METHOD(Access_ModifyString_StringIsModified)
        {
            Serialized<std::string> s;
            s.Access([](std::string& v) {
                v = "hello";
            });

            std::string value;
            s.Read([&value](const std::string& v) {
                value = v;
            });
            Assert::AreEqual(std::string("hello"), value);
        }

        TEST_METHOD(Access_MultipleModifications_LastValuePersists)
        {
            Serialized<int> s;
            s.Access([](int& v) { v = 1; });
            s.Access([](int& v) { v = 2; });
            s.Access([](int& v) { v = 3; });

            int value = 0;
            s.Read([&value](const int& v) {
                value = v;
            });
            Assert::AreEqual(3, value);
        }

        // Reset tests
        TEST_METHOD(Reset_AfterModification_ReturnsDefault)
        {
            Serialized<int> s;
            s.Access([](int& v) { v = 42; });
            s.Reset();

            int value = -1;
            s.Read([&value](const int& v) {
                value = v;
            });
            Assert::AreEqual(0, value);
        }

        TEST_METHOD(Reset_String_ReturnsEmpty)
        {
            Serialized<std::string> s;
            s.Access([](std::string& v) { v = "hello"; });
            s.Reset();

            std::string value = "initial";
            s.Read([&value](const std::string& v) {
                value = v;
            });
            Assert::AreEqual(std::string(""), value);
        }

        // Complex type tests
        TEST_METHOD(Serialized_VectorType_Works)
        {
            Serialized<std::vector<int>> s;
            s.Access([](std::vector<int>& v) {
                v.push_back(1);
                v.push_back(2);
                v.push_back(3);
            });

            size_t size = 0;
            int sum = 0;
            s.Read([&size, &sum](const std::vector<int>& v) {
                size = v.size();
                for (int i : v) sum += i;
            });

            Assert::AreEqual(static_cast<size_t>(3), size);
            Assert::AreEqual(6, sum);
        }

        TEST_METHOD(Serialized_MapType_Works)
        {
            Serialized<std::map<std::string, int>> s;
            s.Access([](std::map<std::string, int>& v) {
                v["one"] = 1;
                v["two"] = 2;
            });

            int value = 0;
            s.Read([&value](const std::map<std::string, int>& v) {
                auto it = v.find("two");
                if (it != v.end()) {
                    value = it->second;
                }
            });

            Assert::AreEqual(2, value);
        }

        // Thread safety tests
        TEST_METHOD(ThreadSafety_ConcurrentReads_NoDataRace)
        {
            Serialized<int> s;
            s.Access([](int& v) { v = 42; });

            std::atomic<int> readCount{ 0 };
            std::vector<std::thread> threads;

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&s, &readCount]() {
                    for (int j = 0; j < 100; ++j)
                    {
                        s.Read([&readCount](const int& v) {
                            if (v == 42) {
                                readCount++;
                            }
                        });
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(1000, readCount.load());
        }

        TEST_METHOD(ThreadSafety_ConcurrentAccessAndRead_NoDataRace)
        {
            Serialized<int> s;
            std::atomic<bool> done{ false };
            std::atomic<int> accessCount{ 0 };

            // Writer thread
            std::thread writer([&s, &done, &accessCount]() {
                for (int i = 0; i < 100; ++i)
                {
                    s.Access([i](int& v) {
                        v = i;
                    });
                    accessCount++;
                }
                done = true;
            });

            // Reader threads
            std::vector<std::thread> readers;
            std::atomic<int> readAttempts{ 0 };

            for (int i = 0; i < 5; ++i)
            {
                readers.emplace_back([&s, &done, &readAttempts]() {
                    while (!done)
                    {
                        s.Read([](const int& v) {
                            // Just read the value
                            (void)v;
                        });
                        readAttempts++;
                    }
                });
            }

            writer.join();
            for (auto& t : readers)
            {
                t.join();
            }

            // Verify all access calls completed
            Assert::AreEqual(100, accessCount.load());
            // Verify reads happened
            Assert::IsTrue(readAttempts > 0);
        }

        // Struct type test
        TEST_METHOD(Serialized_StructType_Works)
        {
            struct TestStruct
            {
                int x = 0;
                std::string name;
            };

            Serialized<TestStruct> s;
            s.Access([](TestStruct& v) {
                v.x = 10;
                v.name = "test";
            });

            int x = 0;
            std::string name;
            s.Read([&x, &name](const TestStruct& v) {
                x = v.x;
                name = v.name;
            });

            Assert::AreEqual(10, x);
            Assert::AreEqual(std::string("test"), name);
        }

        TEST_METHOD(Reset_StructType_ResetsToDefault)
        {
            struct TestStruct
            {
                int x = 0;
                std::string name;
            };

            Serialized<TestStruct> s;
            s.Access([](TestStruct& v) {
                v.x = 10;
                v.name = "test";
            });
            s.Reset();

            int x = -1;
            std::string name = "not empty";
            s.Read([&x, &name](const TestStruct& v) {
                x = v.x;
                name = v.name;
            });

            Assert::AreEqual(0, x);
            Assert::AreEqual(std::string(""), name);
        }
    };
}
