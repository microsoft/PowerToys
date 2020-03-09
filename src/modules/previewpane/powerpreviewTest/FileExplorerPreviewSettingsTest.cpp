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

    class RegistryMock : public RegistryWrapperIface
    {
    public:
        RegistryMock(){};

        LONG SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData)
        {
            return ERROR_SUCCESS;
        }

        LONG DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName)
        {
            return ERROR_SUCCESS;
        }

        LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData)
        {
            return ERROR_SUCCESS;
        }
    };

	TEST_CLASS(BaseSettingsTest)
	{
	public:
		TEST_METHOD(LoadState_ShouldLoadNewState_WhenSucessfull)
		{
			// Arrange
			FileExplorerPreviewSettings tempSettings = GetSttingsObjects();
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
			FileExplorerPreviewSettings tempSettings = GetSttingsObjects();
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
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects();
            tempSettings.SetState(false); //preview handler initially disabled

            // Act
            tempSettings.EnablePreview();

            // Assert
            Assert::IsTrue(tempSettings.GetState());
        }

        TEST_METHOD(DisableRender_ShouldUpdateStateToFalse_WhenSuccessful)
        {
            // Arrange
            FileExplorerPreviewSettings tempSettings = GetSttingsObjects();
            tempSettings.SetState(true); //preview handler initially enabled

            // Act
            tempSettings.DisablePreview();

            // Assert
            Assert::IsFalse(tempSettings.GetState());
        }

		FileExplorerPreviewSettings GetSttingsObjects()
		{
            return FileExplorerPreviewSettings(
                false,
                GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL),
                GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                L"{test-guid}",
                TEXT("Test Handler\0"),
                new RegistryMock());
		}

		std::wstring GetJSONSettings(const std::wstring &_settingsNameId, const std::wstring &_value) const
		{
			return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
		}
	};
}
