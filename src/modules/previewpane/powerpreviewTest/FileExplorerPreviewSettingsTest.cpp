#include "pch.h"
#include "CppUnitTest.h"
#include <settings_objects.h>
#include <powerpreview/settings.cpp>
#include <powerpreview/trace.cpp>
#include <common.h>
#include <powerpreview/registry_wrapper.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace PreviewHandlerSettingsTest
{
    extern "C" IMAGE_DOS_HEADER __ImageBase;

    struct FunctionProperties
    {
    public:
        LONG ReturnValue = ERROR_SUCCESS;
        int NumOfCalls = 0;
        HKEY Scope = NULL;
        LPCWSTR SubKey;
        LPCWSTR ValueName;
    };

    class RegistryMock : public RegistryWrapperIface
    {
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

        LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData)
        {
            GetRegistryMockProperties.NumOfCalls++;
            GetRegistryMockProperties.Scope = keyScope;
            GetRegistryMockProperties.SubKey = subKey;
            GetRegistryMockProperties.ValueName = valueName;
            return GetRegistryMockProperties.ReturnValue;
        }
    };

	TEST_CLASS(BaseSettingsTest)
	{
	public:

        TEST_METHOD (LoadState_ShouldLoadValidState_IfInitalStateIsPresent)
        {
            // Arrange
            bool defaultState = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(defaultState, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"));

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
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(defaultState, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\"}");

            // Act
            previewSettings.LoadState(settings);

            // Assert
            Assert::AreEqual(previewSettings.GetToggleSettingState(), defaultState);
        }

        TEST_METHOD (UpdateState_ShouldDisablePreview_IfPreviewsAreEnabledAndNewSettingsStateIsFalse)
        {
            // Arrange
            bool enabled = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"));
            previewSettings.UpdateToggleSettingState(true);

            // Act
            previewSettings.UpdateState(settings, enabled);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldEnablePreview_IfPreviewsAreEnabledAndNewSettingsStateIsTrue)
        {
            // Arrange
            bool enabled = true;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"true"));
            previewSettings.UpdateToggleSettingState(false);

            // Act
            previewSettings.UpdateState(settings, enabled);

            // Assert
            Assert::IsTrue(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
        }

        TEST_METHOD (UpdateState_ShouldOnlyUpdateToggleSettingState_IfPreviewsAreDisabled)
        {
            // Arrange
            bool enabled = false;
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, mockRegistryWrapper);
            auto settings = PowerToyValues::from_json_string(GetJSONSettings(previewSettings.GetToggleSettingName(), L"false"));

            // Act
            previewSettings.UpdateState(settings, enabled);

            // Assert
            Assert::IsFalse(previewSettings.GetToggleSettingState());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 0);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 0);
        }

        TEST_METHOD (UpdateToggleSettingState_ShouldUpdateState_WhenCalled)
        {
            // Arrange
            bool updatedState = false;
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, new RegistryMock());

            // Act
            previewSettings.UpdateToggleSettingState(updatedState);

            // Assert
            Assert::AreEqual(previewSettings.GetToggleSettingState(), updatedState);
        }

        TEST_METHOD(EnablePreview_ShouldCallSetRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, mockRegistryWrapper);

            // Act
            previewSettings.EnablePreview();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.SubKey, preview_handlers_subkey);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueName, previewSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->SetRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CURRENT_USER));
        }

        TEST_METHOD(DisablePreview_ShouldCallDeleteRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings previewSettings = GetSettingsObject(true, mockRegistryWrapper);

            // Act
            previewSettings.DisablePreview();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.SubKey, preview_handlers_subkey);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.ValueName, previewSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->DeleteRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CURRENT_USER));
        }

		FileExplorerPreviewSettings GetSettingsObject(bool defaultState, RegistryWrapperIface* registryMock)
		{
            return FileExplorerPreviewSettings(
                defaultState,
                L"valid-name",
                L"valid-description",
                L"valid-guid",
                L"valid-handler",
                registryMock);
		}

		std::wstring GetJSONSettings(const std::wstring &_settingsNameId, const std::wstring &_value) const
		{
			return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
		}
	};
}
