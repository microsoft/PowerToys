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

            PowerRenameLib::NumericRenameMapping mappings;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromCsv(path, mappings)));
            Assert::AreEqual<size_t>(2, mappings.size());
            Assert::AreEqual(std::wstring(L"Alice, A."), mappings.at(1));
            Assert::AreEqual(std::wstring(L"Bob"), mappings.at(2));
            DeleteFileW(path);
        }

        TEST_METHOD(CsvRowsUseNumericKeyAndSecondColumn)
        {
            const wchar_t path[] = L"NumericRenameMappingKeyedTests.csv";
            std::ofstream output(path, std::ios::binary);
            output << "Key,Name\r\n1,Alice\r\n2,\"Bob, Jr.\"\r\n";
            output.close();

            PowerRenameLib::NumericRenameMapping mappings;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromCsv(path, mappings)));
            Assert::AreEqual<size_t>(2, mappings.size());
            Assert::AreEqual(std::wstring(L"Alice"), mappings.at(1));
            Assert::AreEqual(std::wstring(L"Bob, Jr."), mappings.at(2));
            DeleteFileW(path);
        }

        TEST_METHOD(TextRowsBecomeOneBasedNamesAndPreserveCommas)
        {
            const wchar_t path[] = L"NumericRenameMappingTests.txt";
            std::ofstream output(path, std::ios::binary);
            output << "Alice, A.\r\n\r\nBob\r\n";
            output.close();

            PowerRenameLib::NumericRenameMapping mappings;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromText(path, mappings)));
            Assert::AreEqual<size_t>(2, mappings.size());
            Assert::AreEqual(std::wstring(L"Alice, A."), mappings.at(1));
            Assert::AreEqual(std::wstring(L"Bob"), mappings.at(2));
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

            PowerRenameLib::NumericRenameMapping mappings;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromXlsx(path, mappings)));
            Assert::AreEqual<size_t>(3, mappings.size());
            Assert::AreEqual(std::wstring(L"Alice"), mappings.at(1));
            Assert::AreEqual(std::wstring(L"Bob"), mappings.at(2));
            Assert::AreEqual(std::wstring(L"Carol"), mappings.at(3));
            DeleteFileW(path);
        }

        TEST_METHOD(XlsxRowsUseNumericKeyAndSecondColumn)
        {
            const wchar_t path[] = L"NumericRenameMappingKeyedTests.xlsx";
            const std::string sharedStrings = R"xml(<?xml version="1.0"?><sst><si><t>V1</t></si><si><t>V2</t></si><si><t>Alice</t></si><si><t>Bob</t></si></sst>)xml";
            const std::string worksheet = R"xml(<?xml version="1.0"?><worksheet><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row><row r="2"><c r="A2"><v>1</v></c><c r="B2" t="s"><v>2</v></c></row><row r="3"><c r="A3"><v>2</v></c><c r="B3" t="inlineStr"><is><t>Bob</t></is></c></row></sheetData></worksheet>)xml";

            int zipError = 0;
            zip_t* archive = zip_open("NumericRenameMappingKeyedTests.xlsx", ZIP_CREATE | ZIP_TRUNCATE, &zipError);
            Assert::IsNotNull(archive);
            auto addEntry = [archive](const char* name, const std::string& content) {
                zip_source_t* source = zip_source_buffer(archive, content.data(), content.size(), 0);
                Assert::IsNotNull(source);
                Assert::IsTrue(zip_file_add(archive, name, source, ZIP_FL_OVERWRITE | ZIP_FL_ENC_UTF_8) >= 0);
            };
            addEntry("xl/sharedStrings.xml", sharedStrings);
            addEntry("xl/worksheets/sheet1.xml", worksheet);
            Assert::AreEqual(0, zip_close(archive));

            PowerRenameLib::NumericRenameMapping mappings;
            Assert::IsTrue(SUCCEEDED(PowerRenameLib::LoadNumericRenameMappingFromXlsx(path, mappings)));
            Assert::AreEqual<size_t>(2, mappings.size());
            Assert::AreEqual(std::wstring(L"Alice"), mappings.at(1));
            Assert::AreEqual(std::wstring(L"Bob"), mappings.at(2));
            DeleteFileW(path);
        }
    };
}
