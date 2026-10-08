// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesCLI/JsonOutput.h>
#include <WorkspacesLib/CliCommands.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliJsonOutputTests)
    {
    public:
        TEST_METHOD (NestedObjectsAndArraysUseTwoSpaceIndentation)
        {
            const auto document = json::JsonObject::Parse(LR"({"view":"summary","workspaces":[{"name":"Development","applications":[{"application":"Notepad"}]}]})");
            const std::string expected =
                "{\n"
                "  \"view\": \"summary\",\n"
                "  \"workspaces\": [\n"
                "    {\n"
                "      \"name\": \"Development\",\n"
                "      \"applications\": [\n"
                "        {\n"
                "          \"application\": \"Notepad\"\n"
                "        }\n"
                "      ]\n"
                "    }\n"
                "  ]\n"
                "}";
            Assert::AreEqual(expected, WorkspacesCli::FormatJson(document));
        }

        TEST_METHOD (EmptyListOutputUsesFlatIndentedDocument)
        {
            const auto output = WorkspacesCli::ListResult({}, false);
            const std::string expected =
                "{\n"
                "  \"view\": \"summary\",\n"
                "  \"workspaces\": []\n"
                "}";
            Assert::AreEqual(expected, WorkspacesCli::FormatJson(output));
        }

        TEST_METHOD (EmptyContainersRemainValidAndCompact)
        {
            Assert::AreEqual(std::string("{}"), WorkspacesCli::FormatJson(json::JsonObject{}));
            const auto document = json::JsonObject::Parse(LR"({"emptyArray":[],"emptyObject":{},"nested":[[],{}]})");
            const auto pretty = WorkspacesCli::FormatJson(document);
            Assert::IsTrue(pretty.find("\"emptyArray\": []") != std::string::npos);
            Assert::IsTrue(pretty.find("\"emptyObject\": {}") != std::string::npos);
            Assert::AreEqual(std::wstring(document.Stringify()),
                             std::wstring(json::JsonObject::Parse(winrt::to_hstring(pretty)).Stringify()));
        }

        TEST_METHOD (EscapedStringsNumbersAndUnicodeRetainTheirValues)
        {
            json::JsonObject document;
            const std::wstring text = L"C:\\Tools\\\"example\"\\\r\n\t{}[],: \u8bb0\u4e8b\u672c";
            document.SetNamedValue(L"escaped\\\"key", json::value(text));
            document.SetNamedValue(L"large", json::value(1.23456789e100));
            document.SetNamedValue(L"small", json::value(-1.23456789e-100));
            document.SetNamedValue(L"enabled", json::value(true));
            document.SetNamedValue(L"nothing", json::JsonValue::Parse(L"null"));
            const auto original = std::wstring(document.Stringify());
            const auto formatted = WorkspacesCli::FormatJson(document);
            const auto parsed = json::JsonObject::Parse(winrt::to_hstring(formatted));
            Assert::AreEqual(original, std::wstring(parsed.Stringify()));
            Assert::AreEqual(original, std::wstring(document.Stringify()), L"Formatting must not mutate the JSON object.");
            Assert::AreEqual(text, std::wstring(parsed.GetNamedString(L"escaped\\\"key")));
        }

        TEST_METHOD (AllShortQuoteAndBackslashCombinationsRoundTrip)
        {
            const std::wstring characters = L"\\\"{}[],:a";
            for (const auto first : characters)
            {
                for (const auto second : characters)
                {
                    for (const auto third : characters)
                    {
                        json::JsonObject document;
                        document.SetNamedValue(L"text", json::value(std::wstring{ first, second, third }));
                        const auto formatted = WorkspacesCli::FormatJson(document);
                        Assert::AreEqual(std::wstring(document.Stringify()),
                                         std::wstring(json::JsonObject::Parse(winrt::to_hstring(formatted)).Stringify()));
                    }
                }
            }
        }

        TEST_METHOD (ErrorEnvelopeRemainsOneJsonObject)
        {
            const auto error = WorkspacesCli::Failure(L"launch",
                                                      { 7, L"unsupportedContext", "Use a non-elevated terminal." });
            const auto formatted = WorkspacesCli::FormatJson(error);
            Assert::IsTrue(formatted.starts_with("{\n  \"schemaVersion\": 1,"));
            const auto parsed = json::JsonObject::Parse(winrt::to_hstring(formatted));
            Assert::AreEqual(7.0, parsed.GetNamedObject(L"error").GetNamedNumber(L"exitCode"));
            Assert::AreEqual(std::wstring(L"unsupportedContext"),
                             std::wstring(parsed.GetNamedObject(L"error").GetNamedString(L"code")));
        }
    };
}
