#include "pch.h"
#include "common.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommon
{
    TEST_CLASS (CommonUtils)
    {
        std::vector<std::wstring> what_global{
            L"TELEGRAM",
            L"SUBLIME TEXT",
            L"PROGRAM",
            L"TEXT",
        };

        TEST_METHOD (FindAppNameInPathTest1)
        {
            std::wstring where(L"C:\\USERS\\GUEST\\APPDATA\\ROAMING\\TELEGRAM DESKTOP\\TELEGRAM.EXE");
            bool ans = find_app_name_in_path(where, what_global);
            Assert::IsTrue(ans);
        }
        TEST_METHOD (FindAppNameInPathTest2)
        {
            std::vector<std::wstring> what{
                L"NOTEPAD",
            };
            std::wstring where(L"C:\\PROGRAM FILES\\NOTEPAD++\\NOTEPAD++.EXE");
            bool ans = find_app_name_in_path(where, what);
            Assert::IsTrue(ans);
        }
        TEST_METHOD (FindAppNameInPathTest3)
        {
            std::vector<std::wstring> what{
                L"NOTEPAD++.EXE",
            };
            std::wstring where(L"C:\\PROGRAM FILES\\NOTEPAD++\\NOTEPAD++.EXE");
            bool ans = find_app_name_in_path(where, what);
            Assert::IsTrue(ans);
        }
        TEST_METHOD (FindAppNameInPathTest4)
        {
            std::wstring where(L"C:\\PROGRAM FILES\\SUBLIME TEXT 3\\SUBLIME_TEXT.EXE");
            bool ans = find_app_name_in_path(where, what_global);
            Assert::IsFalse(ans);
        }
        TEST_METHOD (FindAppNameInPathTest5)
        {
            std::vector<std::wstring> what{
                L"NOTEPAD.EXE",
            };
            std::wstring where(L"C:\\PROGRAM FILES\\NOTEPAD++\\NOTEPAD++.EXE");
            bool ans = find_app_name_in_path(where, what);
            Assert::IsFalse(ans);
        }
    };
}
