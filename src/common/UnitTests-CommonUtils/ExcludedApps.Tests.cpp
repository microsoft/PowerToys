#include "pch.h"
#include "TestHelpers.h"
#include <excluded_apps.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ExcludedAppsTests)
    {
    public:
        // find_app_name_in_path tests
        TEST_METHOD(FindAppNameInPath_ExactMatch_ReturnsTrue)
        {
            std::wstring path = L"C:\\Program Files\\App\\notepad.exe";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_NoMatch_ReturnsFalse)
        {
            std::wstring path = L"C:\\Program Files\\App\\notepad.exe";
            std::vector<std::wstring> apps = { L"calc.exe" };
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_MultipleApps_FindsMatch)
        {
            std::wstring path = L"C:\\Program Files\\App\\notepad.exe";
            std::vector<std::wstring> apps = { L"calc.exe", L"notepad.exe", L"word.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_EmptyPath_ReturnsFalse)
        {
            std::wstring path = L"";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_EmptyApps_ReturnsFalse)
        {
            std::wstring path = L"C:\\Program Files\\App\\notepad.exe";
            std::vector<std::wstring> apps = {};
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_PartialMatchInFolder_ReturnsFalse)
        {
            // "notepad" appears in folder name but not as the exe name
            std::wstring path = L"C:\\notepad\\other.exe";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_CaseSensitive_ReturnsFalse)
        {
            std::wstring path = L"C:\\Program Files\\App\\NOTEPAD.EXE";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            // The function does rfind which is case-sensitive
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_MatchWithDifferentExtension_ReturnsFalse)
        {
            std::wstring path = L"C:\\Program Files\\App\\notepad.com";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_MatchAtEndOfPath_ReturnsTrue)
        {
            std::wstring path = L"C:\\Windows\\System32\\notepad.exe";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_UNCPath_Works)
        {
            std::wstring path = L"\\\\server\\share\\folder\\app.exe";
            std::vector<std::wstring> apps = { L"app.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        // find_folder_in_path tests
        TEST_METHOD(FindFolderInPath_FolderExists_ReturnsTrue)
        {
            std::wstring path = L"C:\\Program Files\\MyApp\\app.exe";
            std::vector<std::wstring> folders = { L"Program Files" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_FolderNotExists_ReturnsFalse)
        {
            std::wstring path = L"C:\\Windows\\System32\\app.exe";
            std::vector<std::wstring> folders = { L"Program Files" };
            Assert::IsFalse(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_MultipleFolders_FindsMatch)
        {
            std::wstring path = L"C:\\Windows\\System32\\app.exe";
            std::vector<std::wstring> folders = { L"Program Files", L"System32", L"Users" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_EmptyPath_ReturnsFalse)
        {
            std::wstring path = L"";
            std::vector<std::wstring> folders = { L"Windows" };
            Assert::IsFalse(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_EmptyFolders_ReturnsFalse)
        {
            std::wstring path = L"C:\\Windows\\app.exe";
            std::vector<std::wstring> folders = {};
            Assert::IsFalse(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_PartialMatch_ReturnsTrue)
        {
            // find_folder_in_path uses rfind which finds substrings
            std::wstring path = L"C:\\Windows\\System32\\app.exe";
            std::vector<std::wstring> folders = { L"System" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_NestedFolder_ReturnsTrue)
        {
            std::wstring path = L"C:\\Program Files\\Company\\Product\\bin\\app.exe";
            std::vector<std::wstring> folders = { L"Product" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_RootDrive_ReturnsTrue)
        {
            std::wstring path = L"C:\\folder\\app.exe";
            std::vector<std::wstring> folders = { L"C:\\" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_UNCPath_Works)
        {
            std::wstring path = L"\\\\server\\share\\folder\\app.exe";
            std::vector<std::wstring> folders = { L"share" };
            Assert::IsTrue(find_folder_in_path(path, folders));
        }

        TEST_METHOD(FindFolderInPath_CaseSensitive_ReturnsFalse)
        {
            std::wstring path = L"C:\\WINDOWS\\app.exe";
            std::vector<std::wstring> folders = { L"windows" };
            // rfind is case-sensitive
            Assert::IsFalse(find_folder_in_path(path, folders));
        }

        // Edge case tests
        TEST_METHOD(FindAppNameInPath_AppNameInMiddleOfPath_HandlesCorrectly)
        {
            // The app name appears both in folder and as filename
            std::wstring path = L"C:\\notepad\\bin\\notepad.exe";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindAppNameInPath_JustFilename_ReturnsTrue)
        {
            std::wstring path = L"notepad.exe";
            std::vector<std::wstring> apps = { L"notepad.exe" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        TEST_METHOD(FindFolderInPath_JustFilename_ReturnsFalse)
        {
            std::wstring path = L"app.exe";
            std::vector<std::wstring> folders = { L"Windows" };
            Assert::IsFalse(find_folder_in_path(path, folders));
        }
    };
}
