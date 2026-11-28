#include "pch.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameManager.h>
#include <PowerRenameItem.h>
#include "MockPowerRenameItem.h"
#include "MockPowerRenameManagerEvents.h"
#include "TestFileHelper.h"
#include "Helpers.h"
#include "Renaming.h"
#include <icu.h>

#define DEFAULT_FLAGS 0

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

#define HINST_THISCOMPONENT ((HINSTANCE)&__ImageBase)

HINSTANCE g_hostHInst = HINST_THISCOMPONENT;

namespace PowerRenameManagerTests
{
    TEST_CLASS (SimpleTests)
    {
    public:
        struct rename_pairs
        {
            std::wstring originalName;
            std::wstring newName;
            bool isFile;
            bool shouldRename;
            int depth;
        };

        void RenameHelper(_In_ rename_pairs * renamePairs, _In_ int numPairs, _In_ std::wstring searchTerm, _In_ std::wstring replaceTerm, SYSTEMTIME fileTime, _In_ DWORD flags)
        {
            // Create a single item (in a temp directory) and verify rename works as expected
            CTestFileHelper testFileHelper;
            for (int i = 0; i < numPairs; i++)
            {
                if (renamePairs[i].isFile)
                {
                    Assert::IsTrue(testFileHelper.AddFile(renamePairs[i].originalName));
                }
                else
                {
                    Assert::IsTrue(testFileHelper.AddFolder(renamePairs[i].originalName));
                }
            }

            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            CMockPowerRenameManagerEvents* mockMgrEvents = new CMockPowerRenameManagerEvents();
            CComPtr<IPowerRenameManagerEvents> mgrEvents;
            Assert::IsTrue(mockMgrEvents->QueryInterface(IID_PPV_ARGS(&mgrEvents)) == S_OK);
            DWORD cookie = 0;
            Assert::IsTrue(mgr->Advise(mgrEvents, &cookie) == S_OK);

            for (int i = 0; i < numPairs; i++)
            {
                CComPtr<IPowerRenameItem> item;
                CMockPowerRenameItem::CreateInstance(testFileHelper.GetFullPath(renamePairs[i].originalName).c_str(),
                                                     renamePairs[i].originalName.c_str(),
                                                     renamePairs[i].depth,
                                                     !renamePairs[i].isFile,
                                                     fileTime,
                                                     &item);

                int itemId = 0;
                Assert::IsTrue(item->GetId(&itemId) == S_OK);
                mgr->AddItem(item);
            }

            // TODO: Setup match and replace parameters
            CComPtr<IPowerRenameRegEx> renRegEx;
            Assert::IsTrue(mgr->GetRenameRegEx(&renRegEx) == S_OK);
            renRegEx->PutFlags(flags);
            renRegEx->PutSearchTerm(searchTerm.c_str());
            renRegEx->PutReplaceTerm(replaceTerm.c_str());

            // Perform the rename
            bool replaceSuccess = false;
            for (int step = 0; step < 20; step++)
            {
                replaceSuccess = mgr->Rename(0, true) == S_OK;
                if (replaceSuccess)
                {
                    break;
                }
                Sleep(10);
            }

            Assert::IsTrue(replaceSuccess);

            std::vector<std::wstring> shouldRename = { L"not ", L"" };

            // Verify the rename occurred
            for (int i = 0; i < numPairs; i++)
            {
                Assert::IsTrue(testFileHelper.PathExistsCaseSensitive(renamePairs[i].originalName) == !renamePairs[i].shouldRename, 
                               (std::wstring(L"The path: [" +  renamePairs[i].originalName + L"] should ") + shouldRename[!renamePairs[i].shouldRename] + L"exist.").c_str());
                Assert::IsTrue(testFileHelper.PathExistsCaseSensitive(renamePairs[i].newName) == renamePairs[i].shouldRename,
                               (std::wstring(L"The path: [" + renamePairs[i].newName + L"] should ") + shouldRename[renamePairs[i].shouldRename] + L"exist.").c_str());
            }

            Assert::IsTrue(mgr->Shutdown() == S_OK);

            mockMgrEvents->Release();
        }
        TEST_METHOD (CreateTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
        }

        TEST_METHOD (CreateAndShutdownTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            Assert::IsTrue(mgr->Shutdown() == S_OK);
        }

        TEST_METHOD (AddItemTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            CComPtr<IPowerRenameItem> item;
            CMockPowerRenameItem::CreateInstance(L"foo", L"foo", 0, false, SYSTEMTIME{ 0 }, &item);
            mgr->AddItem(item);
            Assert::IsTrue(mgr->Shutdown() == S_OK);
        }

        TEST_METHOD (VerifyRenameManagerEvents)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            CMockPowerRenameManagerEvents* mockMgrEvents = new CMockPowerRenameManagerEvents();
            CComPtr<IPowerRenameManagerEvents> mgrEvents;
            Assert::IsTrue(mockMgrEvents->QueryInterface(IID_PPV_ARGS(&mgrEvents)) == S_OK);
            DWORD cookie = 0;
            Assert::IsTrue(mgr->Advise(mgrEvents, &cookie) == S_OK);
            CComPtr<IPowerRenameItem> item;
            CMockPowerRenameItem::CreateInstance(L"foo", L"foo", 0, false, SYSTEMTIME{ 0 }, &item);
            int itemId = 0;
            Assert::IsTrue(item->GetId(&itemId) == S_OK);
            mgr->AddItem(item);

            Assert::IsTrue(mgr->Shutdown() == S_OK);

            mockMgrEvents->Release();
        }

        TEST_METHOD (VerifySingleRename)
        {
            // Create a single item and verify rename works as expected
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"bar.txt", true, true }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }

        TEST_METHOD (VerifyMultiRename)
        {
            // Create a single item and verify rename works as expected
            rename_pairs renamePairs[] = {
                { L"foo1.txt", L"bar1.txt", true, true, 0 },
                { L"foo2.txt", L"bar2.txt", true, true, 0 },
                { L"foo3.txt", L"bar3.txt", true, true, 0 },
                { L"foo4.txt", L"bar4.txt", true, true, 0 },
                { L"foo5.txt", L"bar5.txt", true, true, 0 },
                { L"baa.txt", L"baa_norename.txt", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }

        TEST_METHOD (VerifyLinguisticallyEqualNameRename)
        {
            // "ß" and "ss" compare equal linguistically but are different names. A name that is only
            // canonically equivalent to the original (NFD vs. NFC) must still not be renamed.
            rename_pairs renamePairs[] = {
                { L"Ma\u00DF.txt", L"Mass.txt", true, true, 0 },
                { L"Test\u0438\u0306.txt", L"Test\u0439.txt", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"\u00DF", L"ss", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }

        TEST_METHOD (VerifyFilesOnlyRename)
        {
            // Verify only files are renamed when folders match too
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"bar.txt", true, true, 0 },
                { L"foo", L"foo_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | ExcludeFolders);
        }

        TEST_METHOD (VerifyFoldersOnlyRename)
        {
            // Verify only folders are renamed when files match too
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"foo_norename.txt", true, false, 0 },
                { L"foo", L"bar", false, true, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | ExcludeFiles);
        }

        TEST_METHOD (VerifyFileNameOnlyRename)
        {
            // Verify only file name is renamed, not extension
            rename_pairs renamePairs[] = {
                { L"foo.foo", L"bar.foo", true, true, 0 },
                { L"test.foo", L"test.foo_norename", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | NameOnly);
        }

        TEST_METHOD (VerifyFileExtensionOnlyRename)
        {
            // Verify only file extension is renamed, not name
            rename_pairs renamePairs[] = {
                { L"foo.foo", L"foo.bar", true, true, 0 },
                { L"test.foo", L"test.bar", true, true, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | ExtensionOnly);
        }

        TEST_METHOD (VerifySubFoldersRename)
        {
            // Verify subfolders do not get renamed
            rename_pairs renamePairs[] = {
                { L"foo1", L"bar1", false, true, 0 },
                { L"foo2", L"foo2_norename", false, false, 1 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | ExcludeSubfolders);
        }

        TEST_METHOD (VerifyUppercaseTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo", L"BAR", true, true, 0 },
                { L"foo.test", L"BAR.TEST", true, true, 0 },
                { L"TEST", L"TEST_norename", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Uppercase);
        }

        TEST_METHOD (VerifyLowercaseTransform)
        {
            rename_pairs renamePairs[] = {
                { L"Foo", L"bar", false, true, 0 },
                { L"Foo.teST", L"bar.test", false, true, 0 },
                { L"test", L"test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Lowercase);
        }

        TEST_METHOD (VerifyTitlecaseTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo And The To", L"Bar and the To", false, true, 0 },
                { L"foo And The To.txt", L"Bar and the To.txt", true, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | TitleCase);
        }      

        TEST_METHOD (VerifyTitlecaseWithApostropheTransform)
        {
            rename_pairs renamePairs[] = {
                { L"the foo i'll and i've you're dogs' the i'd it's i'm don't to y'all", L"The Bar I'll and I've You're Dogs' the I'd It's I'm Don't to Y'all", false, true, 0 },
                { L"'the 'foo' 'i'll' and i've you're dogs' the 'i'd' it's i'm don't to y'all.txt", L"'The 'Bar' 'I'll' and I've You're Dogs' the 'I'd' It's I'm Don't to Y'all.txt", true, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | TitleCase);
        }

        TEST_METHOD (VerifyCapitalizedTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo and the to", L"Bar And The To", false, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Capitalized);
        }

        TEST_METHOD (VerifyCapitalizedWithApostropheTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo i'll and i've you're dogs' the i'd it's i'm don't to y'all", L"Bar I'll And I've You're Dogs' The I'd It's I'm Don't To Y'all", false, true, 0 },
                { L"'foo i'll 'and' i've you're dogs' the i'd it's i'm don't to y'all.txt", L"'Bar I'll 'And' I've You're Dogs' The I'd It's I'm Don't To Y'all.txt", true, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Capitalized);
        }

        TEST_METHOD (VerifySentenceCaseTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo and the to", L"Bar and the to", false, true, 0 },
                { L"foo And The To.txt", L"Bar and the to.txt", true, true, 0 },
                { L"01 - foo test.txt", L"01 - Bar test.txt", true, true, 0 },
                { L"--foo test--.txt", L"--Bar test--.txt", true, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | SentenceCase);
        }

        TEST_METHOD (VerifySentenceCaseDirectTransform)
        {
            wchar_t result[MAX_PATH] = { 0 };

            // Basic sentence case with file extension preserved.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"just a test.txt", SentenceCase, false)));
            Assert::AreEqual(L"Just a test.txt", result);

            // Already capitalized.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"Just a test.txt", SentenceCase, false)));
            Assert::AreEqual(L"Just a test.txt", result);

            // All uppercase stem with extension case preserved.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"JUST A TEST.TXT", SentenceCase, false)));
            Assert::AreEqual(L"Just a test.TXT", result);

            // Leading numbers and symbols.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"01 - just a test.txt", SentenceCase, false)));
            Assert::AreEqual(L"01 - Just a test.txt", result);

            // Leading punctuation.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"--hello world--.txt", SentenceCase, false)));
            Assert::AreEqual(L"--Hello world--.txt", result);

            // Folder.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"just a test folder", SentenceCase, true)));
            Assert::AreEqual(L"Just a test folder", result);

            // File without extension.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"just a test", SentenceCase, false)));
            Assert::AreEqual(L"Just a test", result);

            // Single letter.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"a.txt", SentenceCase, false)));
            Assert::AreEqual(L"A.txt", result);

            // No letters.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"123_456.txt", SentenceCase, false)));
            Assert::AreEqual(L"123_456.txt", result);

            // Empty string.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"", SentenceCase, false)));
            Assert::AreEqual(L"", result);

            // ExtensionOnly flag should leave name untouched.
            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"just a test.txt", SentenceCase | ExtensionOnly, false)));
            Assert::AreEqual(L"just a test.txt", result);
        }

        TEST_METHOD (VerifySentenceCaseUnicodeTransform)
        {
            wchar_t result[MAX_PATH] = { 0 };

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\u03A6\u03A9\u03A3.txt", SentenceCase, false)));
            Assert::AreEqual(L"\u03A6\u03C9\u03C2.txt", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\u03A6\u03A9\u03A3\u0301 TEST.TXT", SentenceCase, false)));
            Assert::AreEqual(L"\u03A6\u03C9\u03C2\u0301 test.TXT", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\U00010428 TEST.txt", SentenceCase, false)));
            Assert::AreEqual(L"\U00010400 test.txt", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"01 - \U00010428 \U00010400 TEST.TXT", SentenceCase, false)));
            Assert::AreEqual(L"01 - \U00010400 \U00010428 test.TXT", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\U00010428 TEST.folder", SentenceCase, true)));
            Assert::AreEqual(L"\U00010400 test.folder", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\u00DF TEST.txt", SentenceCase, false)));
            Assert::AreEqual(L"Ss test.txt", result);

            Assert::IsTrue(SUCCEEDED(GetTransformedFileName(result, ARRAYSIZE(result), L"\U00010428 TEST.TXT", SentenceCase | ExtensionOnly, false)));
            Assert::AreEqual(L"\U00010428 TEST.TXT", result);

            wchar_t smallResult[4] = { 0 };
            Assert::AreEqual(STRSAFE_E_INSUFFICIENT_BUFFER, GetTransformedFileName(smallResult, ARRAYSIZE(smallResult), L"\u00DF AB", SentenceCase, false));
        }

        TEST_METHOD (VerifySentenceCaseRepeatedTransform)
        {
            // ICU's default locale is process-wide; restore it even if an assertion throws.
            struct LocaleRestorer
            {
                std::string original = uloc_getDefault();

                ~LocaleRestorer()
                {
                    UErrorCode status = U_ZERO_ERROR;
                    uloc_setDefault(original.c_str(), &status);
                }
            } restoreLocale;

            // Lithuanian lowercasing inserts a dot before accents on I; contextual
            // titlecasing must preserve the original sequence without accumulating dots.
            for (const char* locale : { "en-US", "lt-LT" })
            {
                UErrorCode status = U_ZERO_ERROR;
                uloc_setDefault(locale, &status);
                Assert::IsTrue(U_SUCCESS(status));
                for (const auto& names : {
                         std::pair{ L"\u00DF TEST.txt", L"Ss test.txt" },
                         std::pair{ L"I\u0301 TEST.txt", L"I\u0301 test.txt" },
                         std::pair{ L"01 - I\u0301 TEST.TXT", L"01 - I\u0301 test.TXT" },
                         std::pair{ L"\u4E2D TEST.txt", L"\u4E2D test.txt" } })
                {
                    wchar_t first[MAX_PATH] = { 0 };
                    wchar_t second[MAX_PATH] = { 0 };
                    Assert::IsTrue(SUCCEEDED(GetTransformedFileName(first, ARRAYSIZE(first), names.first, SentenceCase, false)));
                    Assert::AreEqual(names.second, first);
                    Assert::IsTrue(SUCCEEDED(GetTransformedFileName(second, ARRAYSIZE(second), first, SentenceCase, false)));
                    Assert::IsTrue(wcscmp(first, second) == 0, L"Sentence Case must be stable on the second transformation.");
                }
            }
        }

        TEST_METHOD (VerifyCaseExpansionComponentLength)
        {
            wchar_t currentDirectory[MAX_PATH] = { 0 };
            Assert::IsTrue(GetCurrentDirectoryW(ARRAYSIZE(currentDirectory), currentDirectory) != 0);
            wchar_t volumePath[MAX_PATH] = { 0 };
            Assert::IsTrue(GetVolumePathNameW(currentDirectory, volumePath, ARRAYSIZE(volumePath)) != 0);
            DWORD maximumComponentLength = 0;
            Assert::IsTrue(GetVolumeInformationW(volumePath, nullptr, 0, nullptr, &maximumComponentLength, nullptr, nullptr, 0) != 0);
            Assert::IsTrue(maximumComponentLength >= 5 && maximumComponentLength < MAX_PATH);

            // Sharp s expands by one UTF-16 unit. Exercise exactly the component limit
            // and one unit beyond it, using root-level mock paths so MAX_PATH cannot mask it.
            // No files are created at the volume root.
            for (const size_t originalLength : { static_cast<size_t>(maximumComponentLength) - 1, static_cast<size_t>(maximumComponentLength) })
            {
                const std::wstring original = L"\u00DF" + std::wstring(originalLength - 5, L'A') + L".txt";
                const std::wstring path = std::wstring(volumePath) + original;
                Assert::IsTrue(path.size() + 1 <= MAX_PATH, L"The expanded target path must fit so component validation is exercised.");

                CComPtr<IPowerRenameManager> manager;
                Assert::IsTrue(SUCCEEDED(CPowerRenameManager::s_CreateInstance(&manager)));
                CComPtr<IPowerRenameRegEx> regex;
                Assert::IsTrue(SUCCEEDED(manager->GetRenameRegEx(&regex)));
                Assert::IsTrue(SUCCEEDED(regex->PutFlags(SentenceCase)));
                Assert::IsTrue(SUCCEEDED(regex->PutSearchTerm(L"")));
                Assert::IsTrue(SUCCEEDED(regex->PutReplaceTerm(L"")));

                CComPtr<IPowerRenameItem> item;
                Assert::IsTrue(SUCCEEDED(CMockPowerRenameItem::CreateInstance(path.c_str(), original.c_str(), 0, false, SYSTEMTIME{}, &item)));
                unsigned long index = 0;
                const bool expectedRename = originalLength < maximumComponentLength;
                Assert::AreEqual(expectedRename, DoRename(regex, index, item));
                PowerRenameItemRenameStatus status;
                Assert::IsTrue(SUCCEEDED(item->GetStatus(&status)));
                Assert::IsTrue(status == (expectedRename ? PowerRenameItemRenameStatus::ShouldRename : PowerRenameItemRenameStatus::ItemNameTooLong));
                bool shouldRename = false;
                Assert::IsTrue(SUCCEEDED(item->ShouldRenameItem(SentenceCase, &shouldRename)));
                Assert::AreEqual(expectedRename, shouldRename);
                PWSTR proposed = nullptr;
                Assert::IsTrue(SUCCEEDED(item->GetNewName(&proposed)));
                const std::wstring proposedName(proposed);
                CoTaskMemFree(proposed);
                Assert::IsTrue(proposedName.size() == originalLength + 1);
                Assert::IsTrue(proposedName == L"Ss" + std::wstring(originalLength - 5, L'a') + L".txt");
                Assert::IsTrue(SUCCEEDED(manager->Shutdown()));
            }
        }

        TEST_METHOD (VerifyNameOnlyTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo.foo", L"BAR.foo", true, true, 0 },
                { L"foo.txt", L"BAR.TXT", false, true, 0 },
                { L"TEST", L"TEST_norename", false, false, 1 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Uppercase | NameOnly);
        }

        TEST_METHOD (VerifyExtensionOnlyTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo.FOO", L"foo.bar", true, true, 0 },
                { L"bar.FOO", L"bar.FOO_norename", false, false, 0 },
                { L"foo.bar", L"foo.bar_norename", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Lowercase | ExtensionOnly);
        }

        TEST_METHOD (VerifyFileAttributesNoPadding)
        {
            rename_pairs renamePairs[] = {
                { L"foo", L"bar20-7-22-15-6-42-4", true, true, 0 },
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar$YY-$M-$D-$h-$m-$s-$f", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }

        TEST_METHOD (VerifyFileAttributesPadding)
        {
            rename_pairs renamePairs[] = {
                { L"foo", L"bar2020-07-22-15-06-42-453", true, true, 0 },
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar$YYYY-$MM-$DD-$hh-$mm-$ss-$fff", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }

        TEST_METHOD (VerifyFileAttributesMonthAndDayNames)
        {
            std::locale::global(std::locale(""));
            SYSTEMTIME fileTime = { 2020, 1, 3, 1, 15, 6, 42, 453 };
            wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
            wchar_t result[MAX_PATH] = L"bar";
            wchar_t formattedDate[MAX_PATH];
            if (GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH) == 0)
                StringCchCopy(localeName, LOCALE_NAME_MAX_LENGTH, L"en_US");

            GetDateFormatEx(localeName, NULL, &fileTime, L"MMM", formattedDate, MAX_PATH, NULL);
            formattedDate[0] = towupper(formattedDate[0]);
            StringCchPrintf(result, MAX_PATH, TEXT("%s%s"), result, formattedDate);

            GetDateFormatEx(localeName, NULL, &fileTime, L"MMMM", formattedDate, MAX_PATH, NULL);
            formattedDate[0] = towupper(formattedDate[0]);
            StringCchPrintf(result, MAX_PATH, TEXT("%s-%s"), result, formattedDate);

            GetDateFormatEx(localeName, NULL, &fileTime, L"ddd", formattedDate, MAX_PATH, NULL);
            formattedDate[0] = towupper(formattedDate[0]);
            StringCchPrintf(result, MAX_PATH, TEXT("%s-%s"), result, formattedDate);

            GetDateFormatEx(localeName, NULL, &fileTime, L"dddd", formattedDate, MAX_PATH, NULL);
            formattedDate[0] = towupper(formattedDate[0]);
            StringCchPrintf(result, MAX_PATH, TEXT("%s-%s"), result, formattedDate);

            rename_pairs renamePairs[] = {
                { L"foo", result, true, true, 0 },
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar$MMM-$MMMM-$DDD-$DDDD", SYSTEMTIME{ 2020, 1, 3, 1, 15, 6, 42, 453 }, DEFAULT_FLAGS);
        }
    };
}
