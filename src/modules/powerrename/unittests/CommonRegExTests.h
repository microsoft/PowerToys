//#undef TESTS_PARTIAL // Uncomment temporarily to make intellisense work in this file.
#ifndef TESTS_PARTIAL
#include "CppUnitTestInclude.h"
#include "powerrename/lib/Settings.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameRegEx.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
namespace PowerRenameRegExTests
{
TEST_CLASS (SimpleTests)
{
public:
#endif

struct SearchReplaceExpected
{
    PCWSTR search;
    PCWSTR replace;
    PCWSTR test;
    PCWSTR expected;
};

TEST_METHOD (GeneralReplaceTest)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"big") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"bigbar", result);
    CoTaskMemFree(result);
}

TEST_METHOD (ReplaceNoMatch)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"notfound") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"big") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar", result);
    CoTaskMemFree(result);
}

TEST_METHOD (ReplaceNoSearchOrReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::IsTrue(result == nullptr);
    CoTaskMemFree(result);
}

TEST_METHOD (ReplaceNoReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"bar", result);
    CoTaskMemFree(result);
}

TEST_METHOD (ReplaceEmptyStringReplaceTerm)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"foo") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"bar", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyDefaultFlags)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = 0;
    Assert::IsTrue(renameRegEx->GetFlags(&flags) == S_OK);
    Assert::IsTrue(flags == 0);
}

TEST_METHOD (VerifyCaseSensitiveSearch)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceFirstOnly)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceAll)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceAllCaseInsensitive)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceFirstOnlyUseRegEx)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceAllUseRegEx)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceAllUseRegExCaseSensitive)
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
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
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::AreEqual(sreTable[i].expected, result);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyReplaceFirstWildCardUseRegex)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), UseRegularExpressions);
}

TEST_METHOD (VerifyReplaceFirstWildCardMatchAllOccurrences)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"AAAAAA" },
        { L".*", L"Foo", L".*", L"Foo" },
        { L".*", L"Foo", L".*Bar.*", L"FooBarFoo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), MatchAllOccurrences);
}

TEST_METHOD (VerifyReplaceFirstWildNoFlags)
{
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"AAAAAA" },
        { L".*", L"Foo", L".*", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), 0);
}

TEST_METHOD (VerifyEventsFire)
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
    Assert::IsTrue(renameRegEx->PutFileTime(SYSTEMTIME{ 0 }) == S_OK);
    Assert::IsTrue(renameRegEx->ResetFileTime() == S_OK);
    Assert::IsTrue(lstrcmpi(L"FOO", mockEvents->m_searchTerm) == 0);
    Assert::IsTrue(lstrcmpi(L"BAR", mockEvents->m_replaceTerm) == 0);
    Assert::IsTrue(flags == mockEvents->m_flags);
    Assert::IsTrue(renameRegEx->UnAdvise(cookie) == S_OK);
    mockEvents->Release();
}

TEST_METHOD (VerifySimpleCounterNoRegex)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foo$1bar_0", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifySimpleCounterNoEnum)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_${}", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifySimpleCounter)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"bar_${}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_0", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyMultipleCounters)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"_${}_bar_${}") == S_OK);
    unsigned long index = {};
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foo_0_bar_0", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyCounterIncrementCustomization)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"bar_${increment=10}") == S_OK);
    unsigned long index = 1;
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_10", result);
    Assert::AreEqual<unsigned long>(index, 2);
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_20", result);
    Assert::AreEqual<unsigned long>(index, 3);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyCounterStartCustomization)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"bar_${start=1000}") == S_OK);
    unsigned long index = 5;
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_1005", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyCounterPaddingCustomization)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"bar_${padding=5}") == S_OK);
    unsigned long index = 204;
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_00204", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyCounterAllCustomizations)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = EnumerateItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"bar_${increment=7,start=993,padding=5}") == S_OK);
    unsigned long index = 12;
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_01077", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerDefaultFlags)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = 0;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalnum=9}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foo$1bar_${rstringalnum=9}", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerNoRegex)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foo$1bar_${}", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerNoRandomizerRegEx)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalnum=9}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_${rstringalnum=9}", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegEx)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalnum=9}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    std::wstring resultStr(result);
    std::wregex pattern(L"foobar_\\w{9}");
    Assert::IsTrue(std::regex_match(resultStr, pattern));
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegExZeroValue)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalnum=0}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    Assert::AreEqual(L"foobar_", result);
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegExChar)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalpha=9}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    std::wstring resultStr(result);
    std::wregex pattern(L"foobar_[A-Za-z]{9}");
    Assert::IsTrue(std::regex_match(resultStr, pattern));
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegExNum)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringdigit=9}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    std::wstring resultStr(result);
    std::wregex pattern(L"foobar_\\d{9}");
    Assert::IsTrue(std::regex_match(resultStr, pattern));
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegExUuid)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${ruuidv4}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    std::wstring resultStr(result);
    std::wregex pattern(L"foobar_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");
    Assert::IsTrue(std::regex_match(resultStr, pattern));
    CoTaskMemFree(result);
}

TEST_METHOD (VerifyRandomizerRegExAllBackToBack)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = RandomizeItems | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    PWSTR result = nullptr;
    Assert::IsTrue(renameRegEx->PutSearchTerm(L"bar") == S_OK);
    Assert::IsTrue(renameRegEx->PutReplaceTerm(L"$1bar_${rstringalnum=2}${rstringalpha=2}${rstringdigit=2}${ruuidv4}") == S_OK);
    unsigned long index = {};
    Assert::IsTrue(renameRegEx->Replace(L"foobar", &result, index) == S_OK);
    std::wstring resultStr(result);
    std::wregex pattern(L"foobar_\\w{2}[A-Za-z]{2}\\d{2}[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");
    Assert::IsTrue(std::regex_match(resultStr, pattern));
    CoTaskMemFree(result);
}

#ifndef TESTS_PARTIAL
};
}
#endif
