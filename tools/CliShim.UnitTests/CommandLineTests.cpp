// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <string>

#include <CppUnitTest.h>

#include "CommandLine.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CliShimUnitTests
{
    TEST_CLASS(CommandLineTests)
    {
    public:
        TEST_METHOD(StripArgumentZero_ReturnsForwardedTail)
        {
            struct Case
            {
                const wchar_t* commandLine;
                const wchar_t* expected;
            };

            const Case cases[] = {
                { L"fancyzonescli arg", L"arg" },
                { L"fancyzonescli a b c", L"a b c" },
                { L"fancyzonescli", L"" },
                { L"filelocksmithcli", L"" },

                { LR"("C:\Program Files\PowerToys\cli\fancyzonescli.exe" arg)", L"arg" },
                { LR"("C:\Program Files\PowerToys\cli\fancyzonescli.exe")", L"" },
                { LR"("C:\Program Files"\PowerToys\cli\fancyzonescli.exe arg)", L"arg" },
                { LR"(C:\Program" Files"\PowerToys\cli\fancyzonescli.exe arg)", L"arg" },
                { LR"("C:\Program Files"\PowerToys\cli\fancyzonescli.exe)", L"" },

                { LR"("C:\cli\fancyzonescli.exe" "a b")", LR"("a b")" },
                { LR"(fancyzonescli --path "C:\a b\c.png")", LR"(--path "C:\a b\c.png")" },

                { L"fancyzonescli\targ", L"arg" },
                { L"fancyzonescli \t arg", L"arg" },

                // Non-shell CreateProcessW callers can prepend whitespace; argv[0] must not leak.
                { L"  fancyzonescli arg", L"arg" },
                { L" fancyzonescli", L"" },
                { LR"(  "C:\cli\fancyzonescli.exe" arg)", L"arg" },

                { L"", L"" },

                // An unterminated argv[0] quote consumes the remaining command line.
                { LR"("C:\Program Files\app)", L"" },
            };

            for (const Case& testCase : cases)
            {
                const std::wstring actual = CommandLine::StripArgumentZero(testCase.commandLine);
                const std::wstring message = std::wstring(L"input: <") + testCase.commandLine + L">";
                Assert::AreEqual(std::wstring(testCase.expected), actual, message.c_str());
            }
        }
    };
}
