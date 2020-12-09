#include "pch.h"
#include "CppUnitTest.h"
#include <common/SettingsAPI/settings_objects.h>

#include <powerpreview/settings.cpp>
#include <powerpreview/trace.cpp>
#include <powerpreview/registry_wrapper.h>
#include <powerpreview/preview_handler.cpp>
#include <powerpreview/thumbnail_provider.cpp>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace FileExplorerPreviewSettingsTest
{
    struct FunctionProperties
    {
    public:
        LONG ReturnValue = ERROR_SUCCESS;
        int NumOfCalls = 0;
        HKEY Scope = NULL;
        LPCWSTR SubKey;
        LPCWSTR ValueName;
        wchar_t ValueData[255] = { 0 };
    };

    class RegistryMock : public RegistryWrapperIface
    {
    private:
        wchar_t mockData[255] = { 0 };

    public:
        FunctionProperties SetRegistryMockProperties;
        FunctionProperties DeleteRegistryMockProperties;
        FunctionProperties GetRegistryMockProperties;

        LONG SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData)
        {
            SetRegistryMockProperties.NumOfCalls += 1;
            SetRegistryMockProperties.Scope = keyScope;
            SetRegistryMockProperties.SubKey = subKey;
            SetRegistryMockProperties.ValueName = valueName;
            wcscpy_s(SetRegistryMockProperties.ValueData, cbData, (WCHAR*)data);
            return SetRegistryMockProperties.ReturnValue;
        }

        LONG DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName)
        {
            DeleteRegistryMockProperties.NumOfCalls++;
            DeleteRegistryMockProperties.Scope = keyScope;
            DeleteRegistryMockProperties.SubKey = subKey;
            DeleteRegistryMockProperties.ValueName = valueName;
            return DeleteRegistryMockProperties.ReturnValue;
        }

        LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData)
        {
            GetRegistryMockProperties.NumOfCalls++;
            GetRegistryMockProperties.Scope = keyScope;
            GetRegistryMockProperties.SubKey = subKey;
            GetRegistryMockProperties.ValueName = valueName;
            *pdwType = REG_SZ;
            wcscpy_s((LPWSTR)pvData, 255, mockData);
            return GetRegistryMockProperties.ReturnValue;
        }

        void SetMockData(std::wstring data)
        {
            wcscpy_s(mockData, data.c_str());
        }
    };

    TEST_CLASS (BaseSettingsTest)
    {
    public:
        TEST_METHOD (LoadState_ShouldLoadValidState_IfInitalStateIsPresent)
        {
            // Arrange
            bool defaultState = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(defaultState, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");

            // Act
            previewSettings.LoadState(settings);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
        }

        TEST_METHOD (LoadState_ShouldNotChangeDefaultState_IfNoInitalStateIsPresent)
        {
            // Arrange
            bool defaultState = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(defaultState, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\"}", L"FileExplorerPreviewTests");

            // Act
            previewSettings.LoadState(settings);

            // Assert
            Assert::AreEqual(previewSettings.GetToggleSettingState(), defaultState);
        }

        TEST_METHOD (PreviewHandlerSettingsUpdateState_ShouldDisableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsFalseAndPowerToysIsElevatedAndRegistryContainsThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(true);
            // Add expected data in registry
            mockRegistryWrapper->SetMockData(previewSettings.GetRegistryValueData());

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (ThumbnailProviderSettingsUpdateState_ShouldDisableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsFalseAndPowerToysIsElevatedAndRegistryContainsThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            ThumbnailProviderSettings thumbnailSettings = GetThumbnailProviderSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(thumbnailSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");
            thumbnailSettings.UpdateToggleSettingState(true);
            // Add expected data in registry
            mockRegistryWrapper->SetMockData(thumbnailSettings.GetCLSID());

            // Act
            thumbnailSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(thumbnailSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldNotDisableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsFalseAndPowerToysIsElevatedAndRegistryDoesNotContainThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(true);

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldNotDisableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsFalseAndPowerToysIsNotElevated)
        {
            // Arrange
            bool enabled = true;
            bool elevated = false;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(true);

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldEnableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsTrueAndPowerToysIsElevatedAndRegistryDoesNotContainThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"true"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(false);

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (PreviewHandlerSettingsUpdateState_ShouldNotEnableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsTrueAndPowerToysIsElevatedAndRegistryContainsThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"true"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(false);
            // Add expected data in registry
            mockRegistryWrapper->SetMockData(previewSettings.GetRegistryValueData());

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (ThumbnailProviderSettingsUpdateState_ShouldNotEnableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsTrueAndPowerToysIsElevatedAndRegistryContainsThePreview)
        {
            // Arrange
            bool enabled = true;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            ThumbnailProviderSettings thumbnailSettings = GetThumbnailProviderSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(thumbnailSettings.GetToggleSettingName(), L"true"), L"FileExplorerPreviewTests");
            thumbnailSettings.UpdateToggleSettingState(false);
            // Add expected data in registry
            mockRegistryWrapper->SetMockData(thumbnailSettings.GetCLSID());

            // Act
            thumbnailSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldNotEnableInRegistry_IfPreviewsAreEnabledAndNewSettingsStateIsTrueAndPowerToysIsNotElevated)
        {
            // Arrange
            bool enabled = true;
            bool elevated = false;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"true"), L"FileExplorerPreviewTests");
            previewSettings.UpdateToggleSettingState(false);

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->GetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldUpdateToggleSettingState_IfPreviewsAreEnabledAndPowerToysIsElevated)
        {
            // Arrange
            bool enabled = false;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
        }

        TEST_METHOD (UpdateState_ShouldUpdateToggleSettingState_IfPreviewsAreEnabledAndPowerToysIsNotElevated)
        {
            // Arrange
            bool enabled = false;
            bool elevated = false;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
        }

        TEST_METHOD (UpdateState_ShouldOnlyUpdateToggleSettingState_IfPreviewsAreDisabledAndPowerToysIsElevated)
        {
            // Arrange
            bool enabled = false;
            bool elevated = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 0);
        }

        TEST_METHOD (UpdateState_ShouldOnlyUpdateToggleSettingState_IfPreviewsAreDisabledAndPowerToysIsNotElevated)
        {
            // Arrange
            bool enabled = false;
            bool elevated = false;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"), L"FileExplorerPreviewTests");

            // Act
            previewSettings.UpdateState(settings, enabled, elevated);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 0);
        }

        TEST_METHOD (UpdateToggleSettingState_ShouldUpdateState_WhenCalled)
        {
            // Arrange
            bool updatedState = false;
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, new RegistryMock());

            // Act
            previewSettings.UpdateToggleSettingState(updatedState);

            // Assert
            Assert::AreEqual(previewSettings.GetToggleSettingState(), updatedState);
        }

        TEST_METHOD (PreviewHandlerSettingsEnable_ShouldCallSetRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);

            // Act
            previewSettings.Enable();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.SubKey, PreviewHandlerSettings::GetSubkey());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueName, previewSettings.GetCLSID());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueData, previewSettings.GetRegistryValueData().c_str());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->SetRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_LOCAL_MACHINE));
        }

        TEST_METHOD (PreviewHandlerDisable_ShouldCallDeleteRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            PreviewHandlerSettings previewSettings = GetPreviewHandlerSettingsObject(true, mockRegistryWrapper);

            // Act
            previewSettings.Disable();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.SubKey, PreviewHandlerSettings::GetSubkey());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.ValueName, previewSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->DeleteRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_LOCAL_MACHINE));
        }

        TEST_METHOD (ThumbnailProviderSettingsEnable_ShouldCallSetRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            ThumbnailProviderSettings thumbnailSettings = GetThumbnailProviderSettingsObject(true, mockRegistryWrapper);

            // Act
            thumbnailSettings.Enable();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.SubKey, thumbnailSettings.GetSubkey());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueName, nullptr);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueData, thumbnailSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->SetRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CLASSES_ROOT));
        }

        TEST_METHOD (ThumbnailProviderSettingsDisable_ShouldCallDeleteRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            ThumbnailProviderSettings thumbnailSettings = GetThumbnailProviderSettingsObject(true, mockRegistryWrapper);

            // Act
            thumbnailSettings.Disable();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.SubKey, thumbnailSettings.GetSubkey());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.ValueName, nullptr);
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->DeleteRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CLASSES_ROOT));
        }

        PreviewHandlerSettings GetPreviewHandlerSettingsObject(bool defaultState, RegistryWrapperIface* registryMock)
        {
            return PreviewHandlerSettings(
                defaultState,
                L"valid-name",
                L"valid-description",
                L"valid-guid",
                L"valid-handler",
                std::unique_ptr<RegistryWrapperIface>(registryMock));
        }

        ThumbnailProviderSettings GetThumbnailProviderSettingsObject(bool defaultState, RegistryWrapperIface* registryMock)
        {
            return ThumbnailProviderSettings(
                defaultState,
                L"valid-name",
                L"valid-description",
                L"valid-guid",
                L"valid-handler",
                std::unique_ptr<RegistryWrapperIface>(registryMock),
                L"valid-subkey");
        }

        std::wstring GetJSONSettings(const std::wstring& _settingsNameId, const std::wstring& _value) const
        {
            return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
        }
    };
}
