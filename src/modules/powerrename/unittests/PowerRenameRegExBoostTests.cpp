#include "pch.h"
#include "powerrename/lib/Settings.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameRegEx.h>
#include "MockPowerRenameRegExEvents.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameRegExBoostTests
{
    struct SearchReplaceExpected
    {
        PCWSTR search;
        PCWSTR replace;
        PCWSTR test;
        PCWSTR expected;
    };

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

TEST_METHOD(GeneralReplaceTest)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"big") == S_OK);
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result) == S_OK);
    Assert::IsTrue(wcscmp(result, L"bigbar") == 0);
    CoTaskMemFree(result);
}

TEST_METHOD(ReplaceNoMatch)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"notfound") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"big") == S_OK);
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result) == S_OK);
    Assert::IsTrue(wcscmp(result, L"foobar") == 0);
    CoTaskMemFree(result);
}

TEST_METHOD(ReplaceNoSearchOrReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result) == S_OK);
    Assert::IsTrue(result == nullptr);
    CoTaskMemFree(result);
}

TEST_METHOD(ReplaceNoReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result) == S_OK);
    Assert::IsTrue(wcscmp(result, L"bar") == 0);
    CoTaskMemFree(result);
}

TEST_METHOD(ReplaceEmptyStringReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"") == S_OK);
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result) == S_OK);
    Assert::IsTrue(wcscmp(result, L"bar") == 0);
    CoTaskMemFree(result);
}

TEST_METHOD(VerifyDefaultFlags)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = 0;
    Assert::IsTrue(renameRegEx->GetFlags(&flags) == S_OK);
    Assert::IsTrue(flags == 0);
}

TEST_METHOD(VerifyCaseSensitiveSearch)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"Foo", L"Foo", L"FooBar", L"FooBar" },
        { L"Foo", L"boo", L"FooBar", L"booBar" },
        { L"Foo", L"boo", L"foobar", L"foobar" },
        { L"123", L"654", L"123456", L"654456" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceFirstOnly)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = 0;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AABBA" },
        { L"B", L"BBB", L"ABABAB", L"ABBBABAB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceAll)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AAAAA" },
        { L"B", L"BBB", L"ABABAB", L"ABBBABBBABBB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceAllCaseInsensitive)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AAAAA" },
        { L"B", L"BBB", L"ABABAB", L"ABBBABBBABBB" },
        { L"b", L"BBB", L"AbABAb", L"ABBBABABBB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceFirstOnlyUseRegEx)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AABBA" },
        { L"B", L"BBB", L"ABABAB", L"ABBBABAB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceAllUseRegEx)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AAAAA" },
        { L"B", L"BBB", L"ABABAB", L"ABBBABBBABBB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceAllUseRegExCaseSensitive)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions | CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        { L"B", L"BB", L"ABA", L"ABBA" },
        { L"B", L"A", L"ABBBA", L"AAAAA" },
        { L"b", L"BBB", L"AbABAb", L"ABBBABABBB" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

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
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

void VerifyReplaceFirstWildcard(SearchReplaceExpected sreTable[], int tableSize, DWORD flags)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    for (int i = 0; i < tableSize; i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::AreEqual(sreTable[i].expected, result);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyReplaceFirstWildCardUseRegex)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), UseRegularExpressions);
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

TEST_METHOD(VerifyReplaceFirstWildCardMatchAllOccurrences)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"AAAAAA" },
        { L".*", L"Foo", L".*", L"Foo" },
        { L".*", L"Foo", L".*Bar.*", L"FooBarFoo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), MatchAllOccurrences);
}

TEST_METHOD(VerifyReplaceFirstWildNoFlags)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"AAAAAA" },
        { L".*", L"Foo", L".*", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), 0);
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
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
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
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyEventsFire)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    CMockPowerRenameRegExEvents* mockEvents = new CMockPowerRenameRegExEvents();
    CComPtr<IPowerRenameRegExEvents> regExEvents;
    Assert::IsTrue(mockEvents->QueryInterface(IID_PPV_ARGS(&regExEvents)) == S_OK);
    DWORD cookie = 0;
    Assert::IsTrue(renameRegEx->Advise(regExEvents, &cookie) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions | CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"FOO") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"BAR") == S_OK);
    Assert::IsTrue(renameRegEx->PutFileTime(SYSTEMTIME{0}) == S_OK);
    Assert::IsTrue(renameRegEx->ResetFileTime() == S_OK);
    Assert::IsTrue(lstrcmpi(L"FOO", mockEvents->m_searchTerm) == 0);
    Assert::IsTrue(lstrcmpi(L"BAR", mockEvents->m_replaceTerm) == 0);
    Assert::IsTrue(flags == mockEvents->m_flags);
    Assert::IsTrue(renameRegEx->UnAdvise(cookie) == S_OK);
    mockEvents->Release();
}
};
}
