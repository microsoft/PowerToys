#include "pch.h"
#include "CppUnitTest.h"
#include <powerpreview/settings.cpp>
#include <powerpreview/trace.cpp>
#include <settings_objects.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace PreviewHandlerSettingsTest
{
	TEST_CLASS(MarkDownPreviewSettingsClassTest)
	{
	public:
		TEST_METHOD(EnableRender_ShouldUpdateStateToTrue_WhenSuccessful)
		{
			// Arrange
			PrevPaneMDRendrSettings tempSettings = PrevPaneMDRendrSettings();
			tempSettings.SetState(false); //preview handler initially disabled

			// Act
			tempSettings.EnablePreview();

			// Assert
			Assert::IsTrue(tempSettings.GetState());
		}

		TEST_METHOD(DisableRender_ShouldUpdateStateToFalse_WhenSuccessful)
		{
			// Arrange
			PrevPaneMDRendrSettings tempSettings = PrevPaneMDRendrSettings();
			tempSettings.SetState(true); //preview handler initially enabled

			// Act
			tempSettings.DisablePreview();

			// Assert
			Assert::IsFalse(tempSettings.GetState());
		}

		std::wstring GetJSONSettings(const std::wstring &_settingsNameId,const std::wstring &_value) const
		{
			return L"{\"name\":\"Module Name\",\"properties\" : {\"" + _settingsNameId + L"\":{\"value\":" + _value + L"}},\"version\" : \"1.0\" }";
		}
	};
}
