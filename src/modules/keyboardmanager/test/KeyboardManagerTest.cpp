#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

MockedInput mockedInputHandler;

namespace KeyboardManagerTest
{
    TEST_CLASS (RemappingTests)
    {
    public:
        TEST_METHOD (TestMethod1)
        {
            UINT res = mockedInputHandler.SendVirtualInput(NULL, NULL, NULL);
            Assert::AreEqual(res, (UINT)0);
        }
    };
}
