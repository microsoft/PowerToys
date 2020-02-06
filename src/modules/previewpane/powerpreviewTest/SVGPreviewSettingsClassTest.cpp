#include "pch.h"
#include "CppUnitTest.h"
#include <settings_objects.h>
#include <powerpreviewTest/BaseSettingsClassTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace BaseSettingsTest
{
    TEST_CLASS(SVGPreviewSettingsClassTest)
	{
		public:
			TEST_METHOD(EnableRender_ShouldUpdateStateToTrue_WhenSucessfull)
			{
				// Arrange
                		PrevPaneSVGRendrSettings tempSettings = PrevPaneSVGRendrSettings();
				PowerToyValues values = PowerToyValues::from_json_string(GetJSONSettings(tempSettings.GetName(), L"false"));
				tempSettings.UpdateState(values);

				// Act
				tempSettings.EnablePreview();
				
				// Assert
				Assert::IsTrue(tempSettings.GetState());
			}

			TEST_METHOD(DisableRender_ShouldUpdateStateToFalse_WhenSucessfull)
			{
				// Arrange
                		PrevPaneSVGRendrSettings tempSettings = PrevPaneSVGRendrSettings();
				bool valueExists = tempSettings.GetRegistryValue(); // check if key-value exists.

				// Act
				tempSettings.DisabledPreview(); // should set state to false if Value exists.
				bool previewState = tempSettings.GetState();
				
				// Assert
				if(valueExists)
				{
					Assert::IsFalse(previewState);
				}
				else
				{
					Assert::IsTrue(previewState);
				}
			}

			std::wstring GetJSONSettings(std::wstring _settingsNameId, std::wstring _value) const
			{
				return L"{\"name\":\"Module Name\",\"properties\" : {\""+_settingsNameId+L"\":{\"value\":"+_value+L"}},\"version\" : \"1.0\" }";
			}
	};
}
