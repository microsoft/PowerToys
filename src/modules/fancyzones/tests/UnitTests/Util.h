#pragma once

#include "lib/JsonHelpers.h"

namespace CustomAssert
{
    static void AreEqual(const RECT& r1, const RECT& r2)
    {
        const bool equal = ((r1.left == r2.left) && (r1.right == r2.right) && (r1.top == r2.top) && (r1.bottom == r2.bottom));
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(equal);
    }

    static void AreEqual(GUID g1, GUID g2)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(g1 == g2);
    }

    static void AreEqual(JSONHelpers::ZoneSetLayoutType t1, JSONHelpers::ZoneSetLayoutType t2)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(t1 == t2);
    }
}

namespace Mocks
{
    static HWND Window()
    {
        static UINT_PTR s_nextWindow = 0;
        return reinterpret_cast<HWND>(++s_nextWindow);
    }

    static HMONITOR Monitor()
    {
        static UINT_PTR s_nextMonitor = 0;
        return reinterpret_cast<HMONITOR>(++s_nextMonitor);
    }

    static HINSTANCE Instance()
    {
        static UINT_PTR s_nextInstance = 0;
        return reinterpret_cast<HINSTANCE>(++s_nextInstance);
    }

    HWND WindowCreate(HINSTANCE hInst);
}

namespace Helpers
{
    std::wstring GuidToString(const GUID& guid);
    std::wstring CreateGuidString();
}