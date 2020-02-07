#include "pch.h"
#include "CppUnitTest.h"
#include <settings_objects.h>
#include <powerpreviewTest/BaseSettingsClassTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace BaseSettingsTest
{
	TEST_CLASS(FileExplorerPreviewSettingsTest)
	{
	public:
		TEST_METHOD(LoadState_ShouldLoadNewState_WhenSucessfull)
		{
			// Arrange
			BaseSettingsClassTest tempSettings = BaseSettingsClassTest();
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
			BaseSettingsClassTest tempSettings = BaseSettingsClassTest();
			PowerToyValues values = PowerToyValues::from_json_string(GetJSONSettings(tempSettings.GetName(), L"true"));
			tempSettings.SetState(false);
			bool expectedState = true;

			// Act
			tempSettings.UpdateState(values);
			bool actualState = tempSettings.GetState();

			// Assert
			Assert::AreEqual(actualState, expectedState);
		}

		TEST_METHOD(SetRegistryValue_ShouldCreateAValueInRegistry_WhenSucessfull)
		{
			// Arrange
			BaseSettingsClassTest tempSettings = BaseSettingsClassTest();

			// Act
			tempSettings.SetRegistryValue();
			bool results = tempSettings.GetRegistryValue();

			// Assert
			Assert::IsTrue(results);
		}

		TEST_METHOD(RemoveRegistryValue_ShouldDeleteAValueInRegistry_WhenSucessfull)
		{
			// Arrange
			BaseSettingsClassTest tempSettings = BaseSettingsClassTest();

			// Act
			tempSettings.SetRegistryValue();
			bool results = tempSettings.RemvRegistryValue();

			// Assert
			Assert::IsFalse(results);
		}

		std::wstring GetJSONSettings(std::wstring _settingsNameId, std::wstring _value) const
		{
			return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
		}
	};
}
