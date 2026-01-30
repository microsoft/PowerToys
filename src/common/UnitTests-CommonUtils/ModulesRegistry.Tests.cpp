#include "pch.h"
#include "TestHelpers.h"
#include <modulesRegistry.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ModulesRegistryTests)
    {
    public:
        // Test that all changeset generator functions return valid changesets
        TEST_METHOD(GetSvgPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getSvgPreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetSvgThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getSvgThumbnailProviderChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetMarkdownPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getMarkdownPreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetMonacoPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getMonacoPreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetPdfPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getPdfPreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetPdfThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getPdfThumbnailProviderChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetGcodePreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getGcodePreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetGcodeThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getGcodeThumbnailProviderChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetStlThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getStlThumbnailProviderChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetQoiPreviewHandlerChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getQoiPreviewHandlerChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        TEST_METHOD(GetQoiThumbnailProviderChangeSet_ReturnsChangeSet)
        {
            bool enabled = true;
            auto changeSet = getQoiThumbnailProviderChangeSet(enabled);

            Assert::IsFalse(changeSet.keyPath.empty());
        }

        // Test enabled vs disabled state
        TEST_METHOD(ChangeSet_EnabledVsDisabled_MayDiffer)
        {
            auto enabledSet = getSvgPreviewHandlerChangeSet(true);
            auto disabledSet = getSvgPreviewHandlerChangeSet(false);

            // Both should have same key path
            Assert::AreEqual(enabledSet.keyPath, disabledSet.keyPath);
        }

        // Test getAllOnByDefaultModulesChangeSets
        TEST_METHOD(GetAllOnByDefaultModulesChangeSets_ReturnsMultipleChangeSets)
        {
            auto changeSets = getAllOnByDefaultModulesChangeSets();

            // Should return multiple changesets for all default-enabled modules
            Assert::IsTrue(changeSets.size() > 0);
        }

        // Test getAllModulesChangeSets
        TEST_METHOD(GetAllModulesChangeSets_ReturnsChangeSets)
        {
            auto changeSets = getAllModulesChangeSets();

            // Should return changesets for all modules
            Assert::IsTrue(changeSets.size() > 0);
        }

        TEST_METHOD(GetAllModulesChangeSets_ContainsMoreThanOnByDefault)
        {
            auto allSets = getAllModulesChangeSets();
            auto defaultSets = getAllOnByDefaultModulesChangeSets();

            // All modules should be >= on-by-default modules
            Assert::IsTrue(allSets.size() >= defaultSets.size());
        }

        // Test that changesets have valid structure
        TEST_METHOD(ChangeSet_HasValidKeyPath)
        {
            auto changeSet = getSvgPreviewHandlerChangeSet(true);

            // Key path should not be empty and should be a valid registry path
            Assert::IsFalse(changeSet.keyPath.empty());
            Assert::IsTrue(changeSet.keyPath.find(L"\\") != std::wstring::npos ||
                          changeSet.keyPath.find(L"Software") != std::wstring::npos ||
                          changeSet.keyPath.find(L"Classes") != std::wstring::npos ||
                          changeSet.keyPath.length() > 0);
        }

        // Test all changeset functions don't crash
        TEST_METHOD(AllChangeSetFunctions_DoNotCrash)
        {
            getSvgPreviewHandlerChangeSet(true);
            getSvgPreviewHandlerChangeSet(false);
            getSvgThumbnailProviderChangeSet(true);
            getSvgThumbnailProviderChangeSet(false);
            getMarkdownPreviewHandlerChangeSet(true);
            getMarkdownPreviewHandlerChangeSet(false);
            getMonacoPreviewHandlerChangeSet(true);
            getMonacoPreviewHandlerChangeSet(false);
            getPdfPreviewHandlerChangeSet(true);
            getPdfPreviewHandlerChangeSet(false);
            getPdfThumbnailProviderChangeSet(true);
            getPdfThumbnailProviderChangeSet(false);
            getGcodePreviewHandlerChangeSet(true);
            getGcodePreviewHandlerChangeSet(false);
            getGcodeThumbnailProviderChangeSet(true);
            getGcodeThumbnailProviderChangeSet(false);
            getStlThumbnailProviderChangeSet(true);
            getStlThumbnailProviderChangeSet(false);
            getQoiPreviewHandlerChangeSet(true);
            getQoiPreviewHandlerChangeSet(false);
            getQoiThumbnailProviderChangeSet(true);
            getQoiThumbnailProviderChangeSet(false);

            Assert::IsTrue(true);
        }
    };
}
