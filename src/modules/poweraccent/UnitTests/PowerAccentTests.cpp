// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include <common/utils/string_utils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerAccentUnitTests
{
    TEST_CLASS(StringUtilsTrimTests)
    {
    public:
        TEST_METHOD(Trim_RemovesLeadingAndTrailingWhitespace)
        {
            const auto result = trim<wchar_t>(std::wstring_view(L"  hello  "));
            Assert::IsTrue(result == L"hello");
        }
    };
}
