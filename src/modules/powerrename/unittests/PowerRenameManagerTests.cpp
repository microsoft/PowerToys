#include "pch.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameManager.h>
#include <PowerRenameItem.h>
#include "MockPowerRenameItem.h"
#include "MockPowerRenameManagerEvents.h"
#include "TestFileHelper.h"
#include "Helpers.h"

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

                // Verify the item we added is the same from the event
                Assert::IsTrue(mockMgrEvents->m_itemAdded != nullptr && mockMgrEvents->m_itemAdded == item);
                int eventItemId = 0;
                Assert::IsTrue(mockMgrEvents->m_itemAdded->GetId(&eventItemId) == S_OK);
                Assert::IsTrue(itemId == eventItemId);
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

            // Verify the rename occurred
            for (int i = 0; i < numPairs; i++)
            {
                Assert::IsTrue(testFileHelper.PathExists(renamePairs[i].originalName) == !renamePairs[i].shouldRename);
                Assert::IsTrue(testFileHelper.PathExists(renamePairs[i].newName) == renamePairs[i].shouldRename);
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

            // Verify the item we added is the same from the event
            Assert::IsTrue(mockMgrEvents->m_itemAdded != nullptr && mockMgrEvents->m_itemAdded == item);
            int eventItemId = 0;
            Assert::IsTrue(mockMgrEvents->m_itemAdded->GetId(&eventItemId) == S_OK);
            Assert::IsTrue(itemId == eventItemId);
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
                { L"foo and the to", L"Bar and the To", false, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Titlecase);
        }

        TEST_METHOD (VerifyCapitalizedTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo and the to", L"Bar And The To", false, true, 0 },
                { L"Test", L"Test_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", SYSTEMTIME{ 2020, 7, 3, 22, 15, 6, 42, 453 }, DEFAULT_FLAGS | Capitalized);
        }

        TEST_METHOD (VerifyNameOnlyTransform)
        {
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"BAR.txt", false, true, 0 },
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

        TEST_METHOD (VerifyFileAttributesMonthandDayNames)
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
