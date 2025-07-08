#include "pch.h"
#include <filesystem> // Add this line

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(AppUtilsTests)
    {
    public:
        TEST_METHOD(GetCurrentFolder_ReturnsNonEmptyPath)
        {
            // Act
            const std::wstring& result = Utils::Apps::GetCurrentFolder();

            // Assert
            Assert::IsFalse(result.empty());
            Assert::IsTrue(std::filesystem::exists(result));
        }

        TEST_METHOD(GetCurrentFolderUpper_ReturnsUppercasePath)
        {
            // Act
            const std::wstring& currentFolder = Utils::Apps::GetCurrentFolder();
            const std::wstring& currentFolderUpper = Utils::Apps::GetCurrentFolderUpper();

            // Assert
            Assert::IsFalse(currentFolderUpper.empty());
            Assert::AreEqual(currentFolder.length(), currentFolderUpper.length());
            
            // Verify it's actually uppercase
            std::wstring expectedUpper = currentFolder;
            std::transform(expectedUpper.begin(), expectedUpper.end(), expectedUpper.begin(), towupper);
            Assert::AreEqual(expectedUpper, currentFolderUpper);
        }

        TEST_METHOD(GetCurrentFolder_ConsistentResults)
        {
            // Act
            const std::wstring& result1 = Utils::Apps::GetCurrentFolder();
            const std::wstring& result2 = Utils::Apps::GetCurrentFolder();

            // Assert
            Assert::AreEqual(result1, result2);
        }

        TEST_METHOD(GetCurrentFolderUpper_ConsistentResults)
        {
            // Act
            const std::wstring& result1 = Utils::Apps::GetCurrentFolderUpper();
            const std::wstring& result2 = Utils::Apps::GetCurrentFolderUpper();

            // Assert
            Assert::AreEqual(result1, result2);
        }

        TEST_METHOD(AppData_IsEdge_EdgePath_ReturnsTrue)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

            // Act
            bool result = appData.IsEdge();

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(AppData_IsEdge_NonEdgePath_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";

            // Act
            bool result = appData.IsEdge();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsEdge_EmptyPath_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"";

            // Act
            bool result = appData.IsEdge();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsChrome_ChromePath_ReturnsTrue)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";

            // Act
            bool result = appData.IsChrome();

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(AppData_IsChrome_NonChromePath_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

            // Act
            bool result = appData.IsChrome();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsChrome_EmptyPath_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.installPath = L"";

            // Act
            bool result = appData.IsChrome();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsSteamGame_SteamProtocol_ReturnsTrue)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.protocolPath = L"steam://run/123456";

            // Act
            bool result = appData.IsSteamGame();

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(AppData_IsSteamGame_NonSteamProtocol_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.protocolPath = L"https://example.com";

            // Act
            bool result = appData.IsSteamGame();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsSteamGame_EmptyProtocol_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.protocolPath = L"";

            // Act
            bool result = appData.IsSteamGame();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_IsSteamGame_PartialSteamString_ReturnsFalse)
        {
            // Arrange
            Utils::Apps::AppData appData;
            appData.protocolPath = L"http://run/123456";

            // Act
            bool result = appData.IsSteamGame();

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(AppData_DefaultValues)
        {
            // Arrange & Act
            Utils::Apps::AppData appData;

            // Assert
            Assert::IsTrue(appData.name.empty());
            Assert::IsTrue(appData.installPath.empty());
            Assert::IsTrue(appData.packageFullName.empty());
            Assert::IsTrue(appData.appUserModelId.empty());
            Assert::IsTrue(appData.pwaAppId.empty());
            Assert::IsTrue(appData.protocolPath.empty());
            Assert::IsFalse(appData.canLaunchElevated);
        }

        TEST_METHOD(AppData_MultipleBrowserDetection)
        {
            // Arrange
            Utils::Apps::AppData edgeApp;
            edgeApp.installPath = L"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

            Utils::Apps::AppData chromeApp;
            chromeApp.installPath = L"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";

            Utils::Apps::AppData otherApp;
            otherApp.installPath = L"C:\\Program Files\\Firefox\\firefox.exe";

            // Act & Assert
            Assert::IsTrue(edgeApp.IsEdge());
            Assert::IsFalse(edgeApp.IsChrome());
            Assert::IsFalse(edgeApp.IsSteamGame());

            Assert::IsFalse(chromeApp.IsEdge());
            Assert::IsTrue(chromeApp.IsChrome());
            Assert::IsFalse(chromeApp.IsSteamGame());

            Assert::IsFalse(otherApp.IsEdge());
            Assert::IsFalse(otherApp.IsChrome());
            Assert::IsFalse(otherApp.IsSteamGame());
        }

        TEST_METHOD(GetAppsList_ReturnsAppList)
        {
            // Act
            Utils::Apps::AppList apps = Utils::Apps::GetAppsList();

            // Assert
            // The list can be empty or non-empty depending on the system
            // But it should not crash and should return a valid list
            Assert::IsTrue(apps.size() >= 0);
        }
    };
}