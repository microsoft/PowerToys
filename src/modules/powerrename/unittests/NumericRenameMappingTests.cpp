#include "pch.h"
#include "CppUnitTest.h"
#include <NumericRenameMapping.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameNumericMappingTests
{
    TEST_CLASS(NumericRenameMappingTests)
    {
    public:
        TEST_METHOD(NumericFileStemMustContainOnlyDigits)
        {
            unsigned long long number = 0;
            Assert::IsTrue(PowerRenameLib::TryGetNumericFileStem(L"001.pdf", number));
            Assert::AreEqual(1ULL, number);
            Assert::IsFalse(PowerRenameLib::TryGetNumericFileStem(L"scan-001.pdf", number));
        }

        TEST_METHOD(CsvRowsBecomeOneBasedNamesAndSupportQuotedCommas)
        {
            const wchar_t path[] = L"NumericRenameMappingTests.csv";
            std::wofstream output(path, std::ios::binary);
            output << L"\"Alice, A.\"\r\nBob\r\n";
            output.close();

            std::vector<std::wstring> names;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromCsv(path, names)));
            Assert::AreEqual<size_t>(2, names.size());
            Assert::AreEqual(std::wstring(L"Alice, A."), names[0]);
            Assert::AreEqual(std::wstring(L"Bob"), names[1]);
            DeleteFileW(path);
        }
    };
}
