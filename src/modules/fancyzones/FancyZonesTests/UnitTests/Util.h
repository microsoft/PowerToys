#pragma once
// disable warning 4505 -'function' : unreferenced local function has been removed
// as not all functions from Util.h are used in this test
#pragma warning(push)
#pragma warning(disable : 4505)
#include "FancyZonesLib/FancyZonesDataTypes.h"

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

    static void AreEqual(FancyZonesDataTypes::ZoneSetLayoutType t1, FancyZonesDataTypes::ZoneSetLayoutType t2)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(t1 == t2);
    }

    static void AreEqual(const std::vector<std::pair<HMONITOR, RECT>>& a1, const std::vector<std::pair<HMONITOR, RECT>>& a2)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(a1.size() == a2.size());
        for (size_t i = 0; i < a1.size(); i++)
        {
            Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(a1[i].first == a2[i].first);
        }
    }

    static std::pair<bool, std::wstring> CompareJsonObjects(const json::JsonObject& expected, const json::JsonObject& actual, bool recursive = true)
    {
        auto iter = expected.First();
        while (iter.HasCurrent())
        {
            const auto key = iter.Current().Key();
            if (!actual.HasKey(key))
            {
                return std::make_pair(false, key.c_str());
            }

            const std::wstring expectedStringified = iter.Current().Value().Stringify().c_str();
            const std::wstring actualStringified = actual.GetNamedValue(key).Stringify().c_str();

            if (recursive)
            {
                json::JsonObject expectedJson;
                if (json::JsonObject::TryParse(expectedStringified, expectedJson))
                {
                    json::JsonObject actualJson;
                    if (json::JsonObject::TryParse(actualStringified, actualJson))
                    {
                        CompareJsonObjects(expectedJson, actualJson, true);
                    }
                    else
                    {
                        return std::make_pair(false, key.c_str());
                    }
                }
                else
                {
                    if (expectedStringified != actualStringified)
                    {
                        return std::make_pair(false, key.c_str());
                    }
                }
            }
            else
            {
                if (expectedStringified != actualStringified)
                {
                    return std::make_pair(false, key.c_str());
                }
            }

            iter.MoveNext();
        }

        return std::make_pair(true, L"");
    }

    static std::pair<bool, std::wstring> CompareJsonArrays(const json::JsonArray& expected, const json::JsonArray& actual)
    {
        if (expected.Size() != actual.Size())
        {
            return std::make_pair(false, L"Array sizes don't match");
        }

        for (uint32_t i = 0; i < expected.Size(); i++)
        {
            auto res = CustomAssert::CompareJsonObjects(expected.GetObjectAt(i), actual.GetObjectAt(i));
            if (!res.first)
            {
                return res;
            }
        }

        return std::make_pair(true, L"");
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
    std::optional<GUID> StringToGuid(const std::wstring& str);
}

template<>
std::wstring Microsoft::VisualStudio::CppUnitTestFramework::ToString(const std::vector<int>& vec)
{
    std::wstring str = L"{";
    for (size_t i = 0; i < vec.size(); i++)
    {
        str += std::to_wstring(vec[i]);
        if (i != vec.size() - 1)
        {
            str += L",";
        }
    }
    str += L"}";
    return str;
}

#pragma warning(pop)