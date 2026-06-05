// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../PowerAccentKeyboardService/ExcludedApps.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerAccentUnitTests
{
    TEST_CLASS(ExcludedAppsTests)
    {
    public:
        TEST_METHOD(ParseExcludedApps_NormalizesMultilineSettingsForActivationSuppression)
        {
            using namespace winrt::PowerToys::PowerAccentKeyboardService::implementation;

            const auto result = ParseExcludedApps(L" notepad.exe\r\n\nChrome.exe \n  C:\\Tools\\MixedCase.exe ");

            Assert::IsTrue(result.size() == 3);
            Assert::AreEqual(L"NOTEPAD.EXE", result[0].c_str());
            Assert::AreEqual(L"CHROME.EXE", result[1].c_str());
            Assert::AreEqual(L"C:\\TOOLS\\MIXEDCASE.EXE", result[2].c_str());
        }
    };
}
