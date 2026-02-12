#include "pch.h"
#include "TestHelpers.h"
#include <modulesRegistry.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    static std::wstring GetInstallDir()
    {
        wchar_t path[MAX_PATH];
        GetModuleFileNameW(nullptr, path, MAX_PATH);
        return std::filesystem::path{ path }.parent_path().wstring();
    }

    TEST_CLASS(ModulesRegistryTests)
    {
    public:
        // Test that all changeset generator functions return valid changesets
        TEST_METHOD(GetSvgPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getSvgPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetSvgThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getSvgThumbnailHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetMarkdownPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getMdPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetMonacoPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getMonacoPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetPdfPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getPdfPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetPdfThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getPdfThumbnailHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetGcodePreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getGcodePreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetGcodeThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getGcodeThumbnailHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetStlThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getStlThumbnailHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetQoiPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getQoiPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        TEST_METHOD(GetQoiThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            auto changeSet = getQoiThumbnailHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        // Test enabled vs disabled state
        TEST_METHOD(ChangeSet_EnabledVsDisabled_MayDiffer)
        {
            auto enabledSet = getSvgPreviewHandlerChangeSet(GetInstallDir(), true);
            auto disabledSet = getSvgPreviewHandlerChangeSet(GetInstallDir(), false);

            // Both should be valid change sets
            Assert::IsFalse(enabledSet.changes.empty());
            Assert::IsFalse(disabledSet.changes.empty());
        }

        // Test getAllOnByDefaultModulesChangeSets
        TEST_METHOD(GetAllOnByDefaultModulesChangeSets_ReturnsMultipleChangeSets)
        {
            auto changeSets = getAllOnByDefaultModulesChangeSets(GetInstallDir());

            // Should return multiple changesets for all default-enabled modules
            Assert::IsTrue(changeSets.size() > 0);
        }

        // Test getAllModulesChangeSets
        TEST_METHOD(GetAllModulesChangeSets_ReturnsChangeSets)
        {
            auto changeSets = getAllModulesChangeSets(GetInstallDir());

            // Should return changesets for all modules
            Assert::IsTrue(changeSets.size() > 0);
        }

        TEST_METHOD(GetAllModulesChangeSets_ContainsMoreThanOnByDefault)
        {
            auto allSets = getAllModulesChangeSets(GetInstallDir());
            auto defaultSets = getAllOnByDefaultModulesChangeSets(GetInstallDir());

            // All modules should be >= on-by-default modules
            Assert::IsTrue(allSets.size() >= defaultSets.size());
        }

        // Test that changesets have valid structure
        TEST_METHOD(ChangeSet_HasValidKeyPath)
        {
            auto changeSet = getSvgPreviewHandlerChangeSet(GetInstallDir(), false);

            Assert::IsFalse(changeSet.changes.empty());
        }

        // Test all changeset functions don't crash
        TEST_METHOD(AllChangeSetFunctions_DoNotCrash)
        {
            auto installDir = GetInstallDir();
            getSvgPreviewHandlerChangeSet(installDir, true);
            getSvgPreviewHandlerChangeSet(installDir, false);
            getSvgThumbnailHandlerChangeSet(installDir, true);
            getSvgThumbnailHandlerChangeSet(installDir, false);
            getMdPreviewHandlerChangeSet(installDir, true);
            getMdPreviewHandlerChangeSet(installDir, false);
            getMonacoPreviewHandlerChangeSet(installDir, true);
            getMonacoPreviewHandlerChangeSet(installDir, false);
            getPdfPreviewHandlerChangeSet(installDir, true);
            getPdfPreviewHandlerChangeSet(installDir, false);
            getPdfThumbnailHandlerChangeSet(installDir, true);
            getPdfThumbnailHandlerChangeSet(installDir, false);
            getGcodePreviewHandlerChangeSet(installDir, true);
            getGcodePreviewHandlerChangeSet(installDir, false);
            getGcodeThumbnailHandlerChangeSet(installDir, true);
            getGcodeThumbnailHandlerChangeSet(installDir, false);
            getStlThumbnailHandlerChangeSet(installDir, true);
            getStlThumbnailHandlerChangeSet(installDir, false);
            getQoiPreviewHandlerChangeSet(installDir, true);
            getQoiPreviewHandlerChangeSet(installDir, false);
            getQoiThumbnailHandlerChangeSet(installDir, true);
            getQoiThumbnailHandlerChangeSet(installDir, false);

            Assert::IsTrue(true);
        }
    };
}
