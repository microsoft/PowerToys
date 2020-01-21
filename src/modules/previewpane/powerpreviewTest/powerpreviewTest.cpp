#include "pch.h"
#include "CppUnitTest.h"
#include <powerpreview/settings.cpp>
#include <powerpreview/trace.cpp>
#include <settings_objects.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;
using namespace PowerPreviewSettings;

namespace powerpreviewTest
{
	TEST_CLASS(powerpreviewTest)
	{
		public:
			TEST_METHOD(LoadState_ShouldLoadNewState_WhenSucessfull)
			{
				// Arrange
				ExplrSVGSttngs tempSettings = ExplrSVGSttngs();
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
				ExplrSVGSttngs tempSettings = ExplrSVGSttngs();
				PowerToyValues values = PowerToyValues::from_json_string(GetJSONSettings(tempSettings.GetName(), L"true"));
				tempSettings.SetState(false);
				bool expectedState = true; 

				// Act
				tempSettings.UpdateState(values);
				bool actualState = tempSettings.GetState(); 
				
				// Assert
				Assert::AreEqual(actualState, expectedState);
			}

			std::wstring GetJSONSettings(std::wstring _settingsNameId, std::wstring _value)
			{
				return L"{\"name\":\"Module Name\",\"properties\" : {\""+_settingsNameId+L"\":{\"value\":"+_value+L"}},\"version\" : \"1.0\" }";
			}
	};
}
