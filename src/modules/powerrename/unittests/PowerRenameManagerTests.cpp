#include "stdafx.h"
#include "CppUnitTest.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameManager.h>
#include <PowerRenameItem.h>
#include "MockPowerRenameItem.h"
#include "MockPowerRenameManagerEvents.h"
#include "TestFileHelper.h"

#define DEFAULT_FLAGS MatchAllOccurences

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

#define HINST_THISCOMPONENT ((HINSTANCE)&__ImageBase)

HINSTANCE g_hInst = HINST_THISCOMPONENT;

namespace PowerRenameManagerTests
{
    TEST_CLASS(SimpleTests)
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

        void RenameHelper(_In_ rename_pairs * renamePairs, _In_ int numPairs, _In_ std::wstring searchTerm, _In_ std::wstring replaceTerm, _In_ DWORD flags)
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
                CMockPowerRenameItem::CreateInstance(testFileHelper.GetFullPath(
                                                                       renamePairs[i].originalName)
                                                         .c_str(),
                                                     renamePairs[i].originalName.c_str(),
                                                     renamePairs[i].depth,
                                                     !renamePairs[i].isFile,
                                                     &item);

                int itemId = 0;
                Assert::IsTrue(item->get_id(&itemId) == S_OK);
                mgr->AddItem(item);

                // Verify the item we added is the same from the event
                Assert::IsTrue(mockMgrEvents->m_itemAdded != nullptr && mockMgrEvents->m_itemAdded == item);
                int eventItemId = 0;
                Assert::IsTrue(mockMgrEvents->m_itemAdded->get_id(&eventItemId) == S_OK);
                Assert::IsTrue(itemId == eventItemId);
            }

            // TODO: Setup match and replace parameters
            CComPtr<IPowerRenameRegEx> renRegEx;
            Assert::IsTrue(mgr->get_renameRegEx(&renRegEx) == S_OK);
            renRegEx->put_flags(flags);
            renRegEx->put_searchTerm(searchTerm.c_str());
            renRegEx->put_replaceTerm(replaceTerm.c_str());

            Sleep(1000);

            // Perform the rename
            Assert::IsTrue(mgr->Rename(0) == S_OK);

            Sleep(1000);

            // Verify the rename occurred
            for (int i = 0; i < numPairs; i++)
            {
                Assert::IsTrue(testFileHelper.PathExists(renamePairs[i].originalName) == !renamePairs[i].shouldRename);
                Assert::IsTrue(testFileHelper.PathExists(renamePairs[i].newName) == renamePairs[i].shouldRename);
            }

            Assert::IsTrue(mgr->Shutdown() == S_OK);

            mockMgrEvents->Release();
        }
        TEST_METHOD(CreateTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
        }

        TEST_METHOD(CreateAndShutdownTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            Assert::IsTrue(mgr->Shutdown() == S_OK);
        }

        TEST_METHOD(AddItemTest)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            CComPtr<IPowerRenameItem> item;
            CMockPowerRenameItem::CreateInstance(L"foo", L"foo", 0, false, &item);
            mgr->AddItem(item);
            Assert::IsTrue(mgr->Shutdown() == S_OK);
        }

        TEST_METHOD(VerifyRenameManagerEvents)
        {
            CComPtr<IPowerRenameManager> mgr;
            Assert::IsTrue(CPowerRenameManager::s_CreateInstance(&mgr) == S_OK);
            CMockPowerRenameManagerEvents* mockMgrEvents = new CMockPowerRenameManagerEvents();
            CComPtr<IPowerRenameManagerEvents> mgrEvents;
            Assert::IsTrue(mockMgrEvents->QueryInterface(IID_PPV_ARGS(&mgrEvents)) == S_OK);
            DWORD cookie = 0;
            Assert::IsTrue(mgr->Advise(mgrEvents, &cookie) == S_OK);
            CComPtr<IPowerRenameItem> item;
            CMockPowerRenameItem::CreateInstance(L"foo", L"foo", 0, false, &item);
            int itemId = 0;
            Assert::IsTrue(item->get_id(&itemId) == S_OK);
            mgr->AddItem(item);

            // Verify the item we added is the same from the event
            Assert::IsTrue(mockMgrEvents->m_itemAdded != nullptr && mockMgrEvents->m_itemAdded == item);
            int eventItemId = 0;
            Assert::IsTrue(mockMgrEvents->m_itemAdded->get_id(&eventItemId) == S_OK);
            Assert::IsTrue(itemId == eventItemId);
            Assert::IsTrue(mgr->Shutdown() == S_OK);

            mockMgrEvents->Release();
        }

        TEST_METHOD(VerifySingleRename)
        {
            // Create a single item and verify rename works as expected
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"bar.txt", true, true }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS);
        }

        TEST_METHOD(VerifyMultiRename)
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

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS);
        }

        TEST_METHOD(VerifyFilesOnlyRename)
        {
            // Verify only files are renamed when folders match too
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"bar.txt", true, true, 0 },
                { L"foo", L"foo_norename", false, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS | ExcludeFolders);
        }

        TEST_METHOD(VerifyFoldersOnlyRename)
        {
            // Verify only folders are renamed when files match too
            rename_pairs renamePairs[] = {
                { L"foo.txt", L"foo_norename.txt", true, false, 0 },
                { L"foo", L"bar", false, true, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS | ExcludeFiles);
        }

        TEST_METHOD(VerifyFileNameOnlyRename)
        {
            // Verify only file name is renamed, not extension
            rename_pairs renamePairs[] = {
                { L"foo.foo", L"bar.foo", true, true, 0 },
                { L"test.foo", L"test.foo_norename", true, false, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS | NameOnly);
        }

        TEST_METHOD(VerifyFileExtensionOnlyRename)
        {
            // Verify only file extension is renamed, not name
            rename_pairs renamePairs[] = {
                { L"foo.foo", L"foo.bar", true, true, 0 },
                { L"test.foo", L"test.bar", true, true, 0 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS | ExtensionOnly);
        }

        TEST_METHOD(VerifySubFoldersRename)
        {
            // Verify subfolders do not get renamed
            rename_pairs renamePairs[] = {
                { L"foo1", L"bar1", false, true, 0 },
                { L"foo2", L"foo2_norename", false, false, 1 }
            };

            RenameHelper(renamePairs, ARRAYSIZE(renamePairs), L"foo", L"bar", DEFAULT_FLAGS | ExcludeSubfolders);
        }
    };
}