#include "pch.h"
#include "powerrename/lib/Settings.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameRegEx.h>
#include "MockPowerRenameRegExEvents.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameRegExTests
{
    TEST_CLASS(SimpleTests){
        public:
TEST_CLASS_INITIALIZE(ClassInitialize)
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

    SearchReplaceExpected sreTable[] = {
        { L".*", L"Foo", L"AAAAAA", L"Foo" },
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
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L".*", L"Foo", L"AAAAAA", L"Foo" },
    };
    VerifyReplaceFirstWildcard(sreTable, ARRAYSIZE(sreTable), UseRegularExpressions | MatchAllOccurrences);
}

TEST_METHOD(VerifyHandleCapturingGroups)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions | CaseSensitive;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"(foo)(bar)", L"$1_$002_$223_$001021_$00001", L"foobar", L"foo_$002_bar23_$001021_$00001" },
        { L"(foo)(bar)", L"_$1$2_$123$040", L"foobar", L"_foobar_foo23$040" },
        { L"(foo)(bar)", L"$$$1", L"foobar", L"$foo" },
        { L"(foo)(bar)", L"$$1", L"foobar", L"$1" },
        { L"(foo)(bar)", L"$12", L"foobar", L"foo2" },
        { L"(foo)(bar)", L"$10", L"foobar", L"foo0" },
        { L"(foo)(bar)", L"$01", L"foobar", L"$01" },
        { L"(foo)(bar)", L"$$$11", L"foobar", L"$foo1" },
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

TEST_METHOD (VerifyFileAttributesNoPadding)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions ;
    SYSTEMTIME fileTime = SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 };
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"foo", L"bar$YY-$M-$D-$h-$m-$s-$f", L"foo", L"bar20-7-22-15-6-42-4" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->PutFileTime(fileTime) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyFileAttributesPadding)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);
    SYSTEMTIME fileTime = SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 };
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"foo", L"bar$YYYY-$MM-$DD-$hh-$mm-$ss-$fff", L"foo", L"bar2020-07-22-15-06-42-453" },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->PutFileTime(fileTime) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD (VerifyFileAttributesMonthAndDayNames)
{
    CComPtr<IPowerRenameRegEx> renameRegEx;
    Assert::IsTrue(CPowerRenameRegEx::s_CreateInstance(&renameRegEx) == S_OK);
    DWORD flags = MatchAllOccurrences | UseRegularExpressions;
    Assert::IsTrue(renameRegEx->PutFlags(flags) == S_OK);

    std::locale::global(std::locale(""));
    SYSTEMTIME fileTime = { 2020, 1, 3, 1, 15, 6, 42, 453 };
    wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
    wchar_t dest[MAX_PATH] = L"bar";
    wchar_t formattedDate[MAX_PATH];
    if (GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH) == 0)
        StringCchCopy(localeName, LOCALE_NAME_MAX_LENGTH, L"en_US");

    GetDateFormatEx(localeName, NULL, &fileTime, L"MMM", formattedDate, MAX_PATH, NULL);
    formattedDate[0] = towupper(formattedDate[0]);
    StringCchPrintf(dest, MAX_PATH, TEXT("%s%s"), dest, formattedDate);

    GetDateFormatEx(localeName, NULL, &fileTime, L"MMMM", formattedDate, MAX_PATH, NULL);
    formattedDate[0] = towupper(formattedDate[0]);
    StringCchPrintf(dest, MAX_PATH, TEXT("%s-%s"), dest, formattedDate);

    GetDateFormatEx(localeName, NULL, &fileTime, L"ddd", formattedDate, MAX_PATH, NULL);
    formattedDate[0] = towupper(formattedDate[0]);
    StringCchPrintf(dest, MAX_PATH, TEXT("%s-%s"), dest, formattedDate);

    GetDateFormatEx(localeName, NULL, &fileTime, L"dddd", formattedDate, MAX_PATH, NULL);
    formattedDate[0] = towupper(formattedDate[0]);
    StringCchPrintf(dest, MAX_PATH, TEXT("%s-%s"), dest, formattedDate);

    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"foo", L"bar$MMM-$MMMM-$DDD-$DDDD", L"foo", dest },
    };

    for (int i = 0; i < ARRAYSIZE(sreTable); i++)
    {
        PWSTR result = nullptr;
        Assert::IsTrue(renameRegEx->PutSearchTerm(sreTable[i].search) == S_OK);
        Assert::IsTrue(renameRegEx->PutReplaceTerm(sreTable[i].replace) == S_OK);
        Assert::IsTrue(renameRegEx->PutFileTime(fileTime) == S_OK);
        unsigned long index = {};
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == S_OK);
        Assert::IsTrue(wcscmp(result, sreTable[i].expected) == 0);
        CoTaskMemFree(result);
    }
}

TEST_METHOD(VerifyLookbehindFails)
{
    // Standard Library Regex Engine does not support lookbehind, thus test should fail.
    SearchReplaceExpected sreTable[] = {
        //search, replace, test, result
        { L"(?<=E12).*", L"Foo", L"AAAAAA", nullptr },
        { L"(?<!E12).*", L"Foo", L"AAAAAA", nullptr },
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
        Assert::IsTrue(renameRegEx->Replace(sreTable[i].test, &result, index) == E_FAIL);
        Assert::AreEqual(sreTable[i].expected, result);
        CoTaskMemFree(result);
    }
}

};
}
