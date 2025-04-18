#include "pch.h"
#include "powerrename/lib/Settings.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameRegEx.h>
#include "MockPowerRenameRegExEvents.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameRegExBoostTests
{
    TEST_CLASS(SimpleTests)
    {
    public:
TEST_CLASS_INITIALIZE(ClassInitialize)
{
    CSettingsInstance().SetUseBoostLib(true);
}

TEST_CLASS_CLEANUP(ClassCleanup)
{
    CSettingsInstance().SetUseBoostLib(false);
}

#define TESTS_PARTIAL
#include "CommonRegExTests.h"

TEST_METHOD(VerifyMatchAllWildcardUseRegEx)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    // This differs from the Standard Library: .* has two matches (all and nothing).
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"FooFoo" },
        { L".+", L"Foo", L"AAAAAA", L"Foo" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceFirstWildCardUseRegexMatchAllOccurrences)
{
    // This differs from the Standard Library: .* has two matches (all and nothing).
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"FooFoo" },
        { L".+", L"Foo", L"AAAAAA", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), UseRegularExpressions | MatchAllOccurrences);
}

TEST_METHOD(VerifyHandleCapturingGroups)
{
    // This differs from the Standard Library: Boost does not recognize $123 as $1 and "23".
    // To use a capturing group followed by numbers as replacement curly braces are needed.
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions | CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"(foo)(bar)", L"$1_$002_$223_$001021_$00001", L"foobar", L"foo_$002__$001021_$00001" },
        { L"(foo)(bar)", L"$1_$002_${2}23_$001021_$00001", L"foobar", L"foo_$002_bar23_$001021_$00001" },
        { L"(foo)(bar)", L"_$1$2_$123$040", L"foobar", L"_foobar_$040" },
        { L"(foo)(bar)", L"_$1$2_${1}23$040", L"foobar", L"_foobar_foo23$040" },
        { L"(foo)(bar)", L"$$$1", L"foobar", L"$foo" },
        { L"(foo)(bar)", L"$$1", L"foobar", L"$1" },
        { L"(foo)(bar)", L"$12", L"foobar", L"" },
        { L"(foo)(bar)", L"${1}2", L"foobar", L"foo2" },
        { L"(foo)(bar)", L"$10", L"foobar", L"" },
        { L"(foo)(bar)", L"${1}0", L"foobar", L"foo0" },
        { L"(foo)(bar)", L"$01", L"foobar", L"$01" },
        { L"(foo)(bar)", L"$$$11", L"foobar", L"$" },
        { L"(foo)(bar)", L"$$${1}1", L"foobar", L"$foo1" },
        { L"(foo)(bar)", L"$$$$113a", L"foobar", L"$$113a" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyLookbehind)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"(?<=E12).*", L"Foo", L"AAE12BBB", L"AAE12Foo" },
        { L"(?<=E12).+", L"Foo", L"AAE12BBB", L"AAE12Foo" },
        { L"(?<=E\\d\\d).+", L"Foo", L"AAE12BBB", L"AAE12Foo" },
        { L"(?<!E12).*", L"Foo", L"AAE12BBB", L"Foo" },
        { L"(?<!E12).+", L"Foo", L"AAE12BBB", L"Foo" },
        { L"(?<!E\\d\\d).+", L"Foo", L"AAE12BBB", L"Foo" },
    };

    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    Assert::IsTrue(renameRegEx->PutFlags(UseRegularExpressions) == S_OK);

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (Verify12and24HourTimeFormats)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    struct TimeTestCase {
        SYSTEMTIME time;        // Input time
        PCWSTR formatString;    // Format pattern
        PCWSTR expectedResult;  // Expected output
        PCWSTR description;     // Description of what we're testing
    };

    struct TimeTestCase testCases[] = {
        // Midnight (00:00 / 12:00 AM)
        { { 2025, 4, 4, 10, 0, 0, 0, 0 }, L"[$hh:$mm] [$H:$mm $tt]", L"[00:00] [12:00 am]", L"Midnight formatting" },

        // Noon (12:00 / 12:00 PM)
        { { 2025, 4, 4, 10, 12, 0, 0, 0 }, L"[$hh:$mm] [$H:$mm $tt]", L"[12:00] [12:00 pm]", L"Noon formatting" },

        // 1:05 AM
        { { 2025, 4, 4, 10, 1, 5, 0, 0 }, L"[$h:$m] [$H:$m $tt] [$hh:$mm] [$HH:$mm $TT]", 
          L"[1:5] [1:5 am] [01:05] [01:05 AM]", L"1 AM with various formats" },

        // 11 PM
        { { 2025, 4, 4, 10, 23, 45, 0, 0 }, L"[$h:$m] [$H:$m $tt] [$hh:$mm] [$HH:$mm $TT]", 
          L"[23:45] [11:45 pm] [23:45] [11:45 PM]", L"11 PM with various formats" },

        // Mixed formats in complex pattern
        { { 2025, 4, 4, 10, 14, 30, 0, 0 }, L"Date: $YYYY-$MM-$DD Time: $hh:$mm (24h) / $H:$mm $tt (12h)", 
          L"Date: 2025-04-10 Time: 14:30 (24h) / 2:30 pm (12h)", L"Complex combined format" },
    };

    for (int i = 0; i < ARRAYSIZE(testCases); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(L"test") == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(testCases[i].formatString) == S_OK);
        Assert::IsTrue(renameRegEx->PutFileTime(testCases[i].time) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(L"test", &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, testCases[i].expectedResult) == 0, 
                      (std::wstring(L"Failed test case: ") + testCases[i].description).c_str());
        CoTaskMemFree(result);
    }
}
};
}
