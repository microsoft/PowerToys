#include "pch.h"
#include "CppUnitTest.h"
#include <fstream>
#include <NumericRenameMapping.h>
#include <zip.h>

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

        TEST_METHOD(NumericMappingRejectsWindowsInvalidNames)
        {
            Assert::IsTrue(PowerRenameLib::IsValidNumericRenameName(L"Alice, A."));
            Assert::IsFalse(PowerRenameLib::IsValidNumericRenameName(L"Alice/Reports"));
        }

        TEST_METHOD(CsvRowsBecomeOneBasedNamesAndSupportQuotedCommas)
        {
            const wchar_t path[] = L"NumericRenameMappingTests.csv";
            std::ofstream output(path, std::ios::binary);
            output << "\"Alice, A.\"\r\nBob\r\n";
            output.close();

            std::vector<std::wstring> names;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromCsv(path, names)));
            Assert::AreEqual<size_t>(2, names.size());
            Assert::AreEqual(std::wstring(L"Alice, A."), names[0]);
            Assert::AreEqual(std::wstring(L"Bob"), names[1]);
            DeleteFileW(path);
        }

        TEST_METHOD(XlsxRowsReadFirstWorksheetColumn)
        {
            const wchar_t path[] = L"NumericRenameMappingTests.xlsx";
            const std::string sharedStrings = R"xml(<?xml version="1.0"?><sst><si><t>Alice</t></si><si><t>Bob</t></si></sst>)xml";
            const std::string worksheet = R"xml(<?xml version="1.0"?><worksheet><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c></row><row r="2"><c r="A2" t="s"><v>1</v></c></row><row r="3"><c r="A3" t="inlineStr"><is><t>Carol</t></is></c></row></sheetData></worksheet>)xml";

            int zipError = 0;
            zip_t* archive = zip_open("NumericRenameMappingTests.xlsx", ZIP_CREATE | ZIP_TRUNCATE, &zipError);
            Assert::IsNotNull(archive);
            auto addEntry = [archive](const char* name, const std::string& content) {
                zip_source_t* source = zip_source_buffer(archive, content.data(), content.size(), 0);
                Assert::IsNotNull(source);
                Assert::IsTrue(zip_file_add(archive, name, source, ZIP_FL_OVERWRITE | ZIP_FL_ENC_UTF_8) >= 0);
            };
            addEntry("xl/sharedStrings.xml", sharedStrings);
            addEntry("xl/worksheets/sheet1.xml", worksheet);
            Assert::AreEqual(0, zip_close(archive));

            std::vector<std::wstring> names;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromXlsx(path, names)));
            Assert::AreEqual<size_t>(3, names.size());
            Assert::AreEqual(std::wstring(L"Alice"), names[0]);
            Assert::AreEqual(std::wstring(L"Bob"), names[1]);
            Assert::AreEqual(std::wstring(L"Carol"), names[2]);
            DeleteFileW(path);
        }
    };
}
