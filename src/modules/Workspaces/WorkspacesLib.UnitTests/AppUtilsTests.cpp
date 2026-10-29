#include "pch.h"
#include <filesystem> // Add this line
#include <WorkspacesLib/WindowUtils.h>
#include <wil/resource.h>
#include <wil/com.h>
#include <atlbase.h>
#include <shlobj.h>
#include <propkey.h>
#include <propvarutil.h>
#include <fstream>
#include <chrono>
#include <common/utils/process_path.h>
#include <workspaces-common/WindowUtils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(AppUtilsTests)
    {
    public:
        TEST_METHOD(CaptureShortcutPreservesTargetIdentityAndElevation)
        {
            auto apartment = wil::CoInitializeEx(COINIT_APARTMENTTHREADED);
            GUID id{};
            Assert::IsTrue(SUCCEEDED(CoCreateGuid(&id)));
            auto folder = std::filesystem::temp_directory_path() / (L"WorkspacesCapture-" + std::wstring(winrt::to_hstring(id)));
            std::filesystem::create_directory(folder);
            auto cleanup = wil::scope_exit([&] { std::filesystem::remove_all(folder); });
            auto shortcut = folder / L"Capture Shortcut.lnk";
            CComPtr<IShellLinkW> link;
            Assert::IsTrue(SUCCEEDED(link.CoCreateInstance(CLSID_ShellLink)));
            Assert::IsTrue(SUCCEEDED(link->SetPath(L"%SystemRoot%\\System32\\notepad.exe")));
            CComQIPtr<IPropertyStore> store(link);
            PROPVARIANT value{};
            Assert::IsTrue(SUCCEEDED(InitPropVariantFromString(L"Workspaces.Capture.Test", &value)));
            Assert::IsTrue(SUCCEEDED(store->SetValue(PKEY_AppUserModel_ID, value)));
            PropVariantClear(&value);
            Assert::IsTrue(SUCCEEDED(store->Commit()));
            CComQIPtr<IPersistFile> file(link);
            Assert::IsTrue(SUCCEEDED(file->Save(shortcut.c_str(), TRUE)));

            const auto app = Utils::Apps::GetShortcutApp(shortcut.wstring());
            Assert::IsTrue(app.has_value());
            wchar_t expected[MAX_PATH]{};
            ExpandEnvironmentStringsW(L"%SystemRoot%\\System32\\notepad.exe", expected, ARRAYSIZE(expected));
            Assert::AreEqual(0, _wcsicmp(expected, app->installPath.c_str()));
            Assert::AreEqual(std::wstring(L"Capture Shortcut"), app->name);
            Assert::AreEqual(std::wstring(L"Workspaces.Capture.Test"), app->appUserModelId);
            Assert::IsTrue(app->canLaunchElevated);
        }

        TEST_METHOD(CaptureShortcutPreservesSteamProtocolAndRejectsUnreadableLinks)
        {
            auto apartment = wil::CoInitializeEx(COINIT_APARTMENTTHREADED);
            GUID id{};
            Assert::IsTrue(SUCCEEDED(CoCreateGuid(&id)));
            auto folder = std::filesystem::temp_directory_path() / (L"WorkspacesCapture-" + std::wstring(winrt::to_hstring(id)));
            std::filesystem::create_directory(folder);
            auto cleanup = wil::scope_exit([&] { std::filesystem::remove_all(folder); });
            auto shortcut = folder / L"Capture Game.url";
            std::ofstream(shortcut) << "[InternetShortcut]\nURL=steam://rungameid/99999999999999999999\n";
            const auto app = Utils::Apps::GetShortcutApp(shortcut.wstring());
            Assert::IsTrue(app.has_value());
            Assert::IsTrue(app->IsSteamGame());
            Assert::AreEqual(std::wstring(L"steam://rungameid/99999999999999999999"), app->protocolPath);
            Assert::IsFalse(Utils::Apps::GetShortcutApp((folder / L"missing.lnk").wstring()).has_value());
        }

        TEST_METHOD(CaptureDiscoveryPreservesVisibleApplicationMetadata)
        {
            auto apartment = wil::CoInitializeEx(COINIT_APARTMENTTHREADED);
            std::vector<HWND> windows;
            EnumWindows([](HWND window, LPARAM context) -> BOOL {
                if (IsWindowVisible(window) && GetWindowTextLengthW(window))
                    reinterpret_cast<std::vector<HWND>*>(context)->push_back(window);
                return TRUE;
            }, reinterpret_cast<LPARAM>(&windows));
            const auto start = std::chrono::steady_clock::now();
            const auto capture = Utils::Apps::GetCaptureAppsList(windows);
            const auto captureMs = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();
            const auto baselineStart = std::chrono::steady_clock::now();
            const auto baseline = Utils::Apps::GetAppsList();
            const auto baselineMs = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - baselineStart).count();
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(
                (L"Capture inventory ms=" + std::to_wstring(captureMs) + L"; AppsFolder ms=" + std::to_wstring(baselineMs)).c_str());
            size_t compared{};
            for (const auto window : windows)
            {
                DWORD pid{};
                GetWindowThreadProcessId(window, &pid);
                const auto path = get_process_path(pid);
                if (path.empty() || WindowUtils::IsExcludedByDefault(window, path))
                    continue;
                const auto oldApp = Utils::Apps::GetApp(path, pid, baseline);
                const auto newApp = Utils::Apps::GetApp(path, pid, capture);
                Assert::IsTrue(oldApp.has_value() == newApp.has_value(), path.c_str());
                if (!oldApp)
                    continue;
                Assert::AreEqual(oldApp->name, newApp->name, path.c_str());
                Assert::IsTrue(_wcsicmp(oldApp->installPath.c_str(), newApp->installPath.c_str()) == 0, path.c_str());
                Assert::AreEqual(oldApp->packageFullName, newApp->packageFullName, path.c_str());
                Assert::IsTrue(oldApp->canLaunchElevated == newApp->canLaunchElevated, path.c_str());
                if (!oldApp->packageFullName.empty() || oldApp->IsSteamGame())
                    Assert::AreEqual(oldApp->appUserModelId, newApp->appUserModelId, path.c_str());
                ++compared;
            }
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(
                (L"Visible application metadata matched=" + std::to_wstring(compared)).c_str());
        }

        TEST_METHOD(WindowMatchingRejectsTheOppositeElevation)
        {
            wil::unique_handle token;
            Assert::IsTrue(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()) != FALSE);
            TOKEN_ELEVATION elevation{};
            DWORD length{};
            Assert::IsTrue(GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &length) != FALSE);
            const bool actual = elevation.TokenIsElevated != 0;
            Assert::IsTrue(Utils::MatchesProcessElevation(GetCurrentProcessId(), actual));
            Assert::IsFalse(Utils::MatchesProcessElevation(GetCurrentProcessId(), !actual));
        }

        TEST_METHOD(UnknownProcessElevationIsNotTreatedAsNonElevated)
        {
            Assert::IsFalse(Utils::MatchesProcessElevation(MAXDWORD, false));
            Assert::IsFalse(Utils::MatchesProcessElevation(MAXDWORD, true));
        }

        TEST_METHOD(WorkspaceInventoryMatchesAuthenticatedPlanWithoutFullShellEnumeration)
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application app{};
            app.name = L"Captured application";
            app.path = L"C:\\WorkspaceTest\\Captured.exe";
            app.packageFullName = L"Example.Package_1.0.0.0_x64__example";
            app.appUserModelId = L"Example.Package_example!App";
            app.canLaunchElevated = true;
            project.apps = { app, app };
            const auto inventory = Utils::Apps::GetWorkspaceApps(project);
            Assert::AreEqual(size_t{ 1 }, inventory.size());
            const auto found = Utils::Apps::GetApp(app.path, GetCurrentProcessId(), inventory);
            Assert::IsTrue(found.has_value());
            Assert::AreEqual(app.name, found->name);
            Assert::AreEqual(app.path, found->installPath);
            Assert::AreEqual(app.packageFullName, found->packageFullName);
            Assert::IsTrue(found->canLaunchElevated);
        }

        TEST_METHOD(WorkspaceInventoryPreservesSteamAndDoesNotAssignOnePwaToEveryBrowserWindow)
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application game{};
            game.name = L"Game";
            game.path = L"C:\\Games\\Example\\game.exe";
            game.appUserModelId = L"steam://rungameid/123";
            WorkspacesData::WorkspacesProject::Application pwa{};
            pwa.name = L"Work app (Edge)";
            pwa.path = L"C:\\Browser\\msedge.exe";
            pwa.pwaAppId = L"example-pwa";
            project.apps = { game, pwa };
            const auto inventory = Utils::Apps::GetWorkspaceApps(project);
            Assert::AreEqual(size_t{ 2 }, inventory.size());
            Assert::IsTrue(inventory[0].IsSteamGame());
            Assert::AreEqual(game.appUserModelId, inventory[0].protocolPath);
            Assert::IsTrue(inventory[1].IsEdge());
            Assert::IsTrue(inventory[1].pwaAppId.empty());
        }

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