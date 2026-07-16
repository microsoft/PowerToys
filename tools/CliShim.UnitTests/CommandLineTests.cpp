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
                // Normal shell launches: unquoted program name, ends at first whitespace.
                { L"fancyzones arg", L"arg" },
                { L"fancyzones a b c", L"a b c" },
                { L"fancyzones", L"" },
                { L"filelocksmith", L"" },

                // Quotes group whitespace within argv[0]; backslashes have no special meaning.
                { LR"("C:\Program Files\PowerToys\cli\fancyzones.exe" arg)", L"arg" },
                { LR"("C:\Program Files\PowerToys\cli\fancyzones.exe")", L"" },
                { LR"("C:\Program Files"\PowerToys\cli\fancyzones.exe arg)", L"arg" },
                { LR"(C:\Program" Files"\PowerToys\cli\fancyzones.exe arg)", L"arg" },
                { LR"("C:\Program Files"\PowerToys\cli\fancyzones.exe)", L"" },

                // The user's exact quoting in the tail is preserved verbatim (the whole point of the shim).
                { LR"("C:\cli\fancyzones.exe" "a b")", LR"("a b")" },
                { LR"(fancyzones --path "C:\a b\c.png")", LR"(--path "C:\a b\c.png")" },

                // Tabs count as whitespace for both the argv[0] terminator and the trim.
                { L"fancyzones\targ", L"arg" },
                { L"fancyzones \t arg", L"arg" },

                // Regression: a command line padded with leading whitespace must NOT leak the program
                // name (a non-shell parent can pass this via CreateProcessW; the OS loader never does).
                { L"  fancyzones arg", L"arg" },
                { L" fancyzones", L"" },
                { LR"(  "C:\cli\fancyzones.exe" arg)", L"arg" },

                // Degenerate input.
                { L"", L"" },

                // Unterminated argv[0] quote: ends at end-of-string (CRT-faithful), so no arguments remain.
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
