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
        HKEY Scope;
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
		TEST_METHOD(LoadState_ShouldLoadNewState_WhenSucessfull)
		{
			// Arrange
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(new RegistryMock());
			PowerToyValues values = PowerToyValues::from_json_string(GetJSONSettings(tempSettings.GetName(), L"true"));
			tempSettings.SetState(false);
			bool expectedState = true;

			// Act
			tempSettings.LoadState(values);
			bool actualState = tempSettings.GetState();

			// Assert
			Assert::AreEqual(actualState, expectedState);
		}

		TEST_METHOD(UpdateState_ShouldChangeState_WhenSucessfull)
		{
			// Arrange
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(new RegistryMock());
			PowerToyValues values = PowerToyValues::from_json_string(GetJSONSettings(tempSettings.GetName(), L"true"));
			tempSettings.SetState(false);
			bool expectedState = true;

			// Act
			tempSettings.UpdateState(values);
			bool actualState = tempSettings.GetState();

			// Assert
			Assert::AreEqual(actualState, expectedState);
		}

		TEST_METHOD(EnableRender_ShouldUpdateStateToTrue_WhenSuccessful)
        {
            // Arrange
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(new RegistryMock());
            tempSettings.SetState(false); //preview handler initially disabled

            // Act
            tempSettings.EnablePreview();

            // Assert
            Assert::IsTrue(tempSettings.GetState());
        }

        TEST_METHOD(DisableRender_ShouldUpdateStateToFalse_WhenSuccessful)
        {
            // Arrange
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(new RegistryMock());
            tempSettings.SetState(true); //preview handler initially enabled

            // Act
            tempSettings.DisablePreview();

            // Assert
            Assert::IsFalse(tempSettings.GetState());
        }

        TEST_METHOD(EnablePreview_ShouldCallSetRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);

            // Act
            tempSettings.EnablePreview();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.SubKey, tempSettings.GetSubKey());
            Assert::AreEqual(mockRegistryWrapper->SetRegistryMockProperties.ValueName, tempSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->SetRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CURRENT_USER));
        }

        TEST_METHOD(EnablePreview_ShouldNotSetStateToTrue_IfSetRegistryValueFailed)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            mockRegistryWrapper->SetRegistryMockProperties.ReturnValue = ERROR_OUTOFMEMORY;
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);
            tempSettings.SetState(false);

            // Act
            tempSettings.EnablePreview();

            // Assert
            Assert::IsFalse(tempSettings.GetState());
        }

        TEST_METHOD(EnablePreview_ShouldSetStateToTrue_IfSetRegistryValueReturnSuccessErrorCode)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);
            tempSettings.SetState(false);

            // Act
            tempSettings.EnablePreview();

            // Assert
            Assert::IsTrue(tempSettings.GetState());
        }

        TEST_METHOD(DisablePreview_ShouldCallDeleteRegistryValueWithValidArguments_WhenCalled)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);

            // Act
            tempSettings.DisablePreview();

            // Assert
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.NumOfCalls, 1);
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.SubKey, tempSettings.GetSubKey());
            Assert::AreEqual(mockRegistryWrapper->DeleteRegistryMockProperties.ValueName, tempSettings.GetCLSID());
            Assert::AreEqual((ULONG_PTR)(mockRegistryWrapper->DeleteRegistryMockProperties.Scope), (ULONG_PTR)(HKEY_CURRENT_USER));
        }

        TEST_METHOD(DisablePreview_ShouldNotSetStateToFalse_IfDeleteRegistryValueFailed)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            mockRegistryWrapper->DeleteRegistryMockProperties.ReturnValue = ERROR_OUTOFMEMORY;
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);
            tempSettings.SetState(true);

            // Act
            tempSettings.DisablePreview();

            // Assert
            Assert::IsTrue(tempSettings.GetState());
        }

        TEST_METHOD(DisablePreview_ShouldSetStateToFalse_IfDeleteRegistryValueReturnSuccessErrorCode)
        {
            // Arrange
            RegistryMock* mockRegistryWrapper = new RegistryMock();
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects(mockRegistryWrapper);
            tempSettings.SetState(true);

            // Act
            tempSettings.DisablePreview();

            // Assert
            Assert::IsFalse(tempSettings.GetState());
        }

		FileExplorerPreviewSettings GetSttingsObjects(RegistryMock * registryMock)
		{
            return FileExplorerPreviewSettings(
                false,
                GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL),
                GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                L"{test-guid}",
                TEXT("Test Handler\0"),
                registryMock);
		}

		std::wstring GetJSONSettings(const std::wstring &_settingsNameId, const std::wstring &_value) const
		{
			return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
		}
	};
}
