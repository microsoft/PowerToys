#include "pch.h"

#include <filesystem>
#include <fstream>
#include <objbase.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        struct ChromeProfiles
        {
            std::filesystem::path root;
            const std::wstring appId = L"abcdefghijklmnopabcdefghijklmnop";

            ChromeProfiles()
            {
                GUID guid{};
                Assert::IsTrue(SUCCEEDED(CoCreateGuid(&guid)));
                wchar_t id[40]{};
                Assert::IsTrue(StringFromGUID2(guid, id, ARRAYSIZE(id)) != 0);
                root = std::filesystem::temp_directory_path() / (std::wstring(L"WorkspacesChromeProfiles-") + id);
                Assert::IsTrue(std::filesystem::create_directory(root));
            }

            ~ChromeProfiles()
            {
                std::error_code ec;
                std::filesystem::remove_all(root, ec);
            }

            std::filesystem::path Apps(const std::wstring& profile) const
            {
                return root / L"Google\\Chrome\\User Data" / profile / L"Web Applications";
            }

            void InstallShortcut(const std::wstring& profile, const std::wstring& id, const std::wstring& name) const
            {
                auto folder = Apps(profile) / (L"_crx_" + id);
                std::filesystem::create_directories(folder);
                std::ofstream icon(folder / (name + L".ico"));
                Assert::IsTrue(icon.good());
            }
        };
    }

    TEST_CLASS (PwaHelperTests)
    {
    public:
        TEST_METHOD (ChromeQualifiedAndDefaultWindowsResolveTheirOwnInstallations)
        {
            ChromeProfiles data;
            Utils::PwaHelper helper(data.root);
            const std::wstring standard = L"Chrome._crx_abcdefghijabcdefghijklmnop";
            const std::wstring qualified = L"Chrome._crx_abcdeghijklmnop.UserData.Profile1";
            Assert::IsFalse(helper.GetChromeAppId(L"Chrome").has_value());
            Assert::IsFalse(helper.GetChromeAppId(qualified).has_value());

            data.InstallShortcut(L"Profile 1", data.appId, L"Work");
            std::filesystem::create_directories(data.Apps(L"Profile 1") / L"Manifest Resources" / data.appId / L"Icons");
            Assert::IsFalse(helper.GetChromeAppId(standard).has_value());
            Assert::AreEqual(data.appId, helper.GetChromeAppId(qualified).value());
            Assert::AreEqual(std::wstring(L"Work"), helper.SearchPwaName(data.appId, qualified));
            Assert::AreEqual(data.appId, helper.GetChromeAppId(L"Chrome._crx_" + data.appId + L".UserData.Profile1").value());

            data.InstallShortcut(L"Default", data.appId, L"Home");
            Assert::AreEqual(data.appId, helper.GetChromeAppId(standard).value());
            Assert::AreEqual(std::wstring(L"Home"), helper.SearchPwaName(data.appId, standard));
            Assert::AreEqual(std::wstring(L"Work"), helper.SearchPwaName(data.appId, qualified));
        }

        TEST_METHOD (ChromeManifestIdsAndShortenedComponentsRequireAnUnambiguousMatch)
        {
            ChromeProfiles data;
            const std::wstring longProfile = L"LongProfileNameWithSharedTail1234567890";
            const std::wstring aumid = L"Chrome._crx_abcdeghijklmnop.UserData.L1234567890";
            std::filesystem::create_directories(data.Apps(longProfile) / L"Manifest Resources" / data.appId / L"Icons");
            // Another app shares the leading characters, but has a different retained tail.
            auto different = data.appId;
            different.back() = L'a';
            data.InstallShortcut(longProfile, different, L"Another app");
            Utils::PwaHelper first(data.root);
            Assert::AreEqual(data.appId, first.GetChromeAppId(aumid).value());
            Assert::AreEqual(aumid, first.SearchPwaName(data.appId, aumid));

            // A different character in Chromium's omitted middle is genuinely ambiguous.
            auto ambiguous = data.appId;
            ambiguous[10] = L'a';
            data.InstallShortcut(longProfile, ambiguous, L"Ambiguous app");
            Assert::IsFalse(Utils::PwaHelper(data.root).GetChromeAppId(aumid).has_value());
            std::filesystem::remove_all(data.Apps(longProfile) / (L"_crx_" + ambiguous));

            data.InstallShortcut(L"LongProfileNameZZZZSharedTail1234567890", data.appId, L"Other profile");
            Assert::IsFalse(Utils::PwaHelper(data.root).GetChromeAppId(aumid).has_value());
        }

        TEST_METHOD (PwaHelper_Constructor_DoesNotThrow)
        {
            // Act & Assert - Constructor should not crash when called
            try
            {
                Utils::PwaHelper helper;
                // If we get here, the constructor didn't throw
                Assert::IsTrue(true);
            }
            catch (...)
            {
                Assert::Fail(L"PwaHelper constructor should not throw exceptions");
            }
        }

        TEST_METHOD (PwaHelper_GetEdgeAppId_EmptyAumid_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyAumid = L"";

            // Act
            auto result = helper.GetEdgeAppId(emptyAumid);

            // Assert
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD (PwaHelper_GetChromeAppId_EmptyAumid_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyAumid = L"";

            // Act
            auto result = helper.GetChromeAppId(emptyAumid);

            // Assert
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD (PwaHelper_SearchPwaName_EmptyParameters_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyPwaAppId = L"";
            std::wstring emptyWindowAumid = L"";

            // Act
            std::wstring result = helper.SearchPwaName(emptyPwaAppId, emptyWindowAumid);

            // Assert
            Assert::IsTrue(result.empty());
        }

        TEST_METHOD (PwaHelper_SearchPwaName_NonExistentIds_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring nonExistentPwaAppId = L"nonexistent_app_id";
            std::wstring nonExistentWindowAumid = L"nonexistent_aumid";

            // Act
            std::wstring result = helper.SearchPwaName(nonExistentPwaAppId, nonExistentWindowAumid);

            // TODO: is it really expected?
            Assert::IsTrue(result == nonExistentWindowAumid);
        }

        TEST_METHOD (PwaHelper_GetEdgeAppId_ValidConstruction_DoesNotCrash)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring testAumid = L"Microsoft.MicrosoftEdge_8wekyb3d8bbwe!App";

            // Act & Assert - Should not crash
            auto result = helper.GetEdgeAppId(testAumid);
            // Result can be empty or have value, but should not crash
        }

        TEST_METHOD (PwaHelper_GetChromeAppId_ValidConstruction_DoesNotCrash)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring testAumid = L"Chrome.App.TestId";

            // Act & Assert - Should not crash
            auto result = helper.GetChromeAppId(testAumid);
            // Result can be empty or have value, but should not crash
        }
    };
}
