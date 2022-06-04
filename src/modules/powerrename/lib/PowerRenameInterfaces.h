#pragma once
#include "pch.h"
#include <string>
#include <vector>

enum PowerRenameFlags
{
    CaseSensitive = 0x1,
    MatchAllOccurences = 0x2,
    UseRegularExpressions = 0x4,
    EnumerateItems = 0x8,
    ExcludeFiles = 0x10,
    ExcludeFolders = 0x20,
    ExcludeSubfolders = 0x40,
    NameOnly = 0x80,
    ExtensionOnly = 0x100,
    Uppercase = 0x200,
    Lowercase = 0x400,
    Titlecase = 0x800,
    Capitalized = 0x1000
};

enum PowerRenameFilters
{
    None = 1,
    Selected = 2,
    FlagsApplicable = 3,
    ShouldRename = 4,
};

interface __declspec(uuid("3ECBA62B-E0F0-4472-AA2E-DEE7A1AA46B9")) IPowerRenameRegExEvents : public IUnknown
{
public:
    IFACEMETHOD(OnSearchTermChanged)(_In_ PCWSTR searchTerm) = 0;
    IFACEMETHOD(OnReplaceTermChanged)(_In_ PCWSTR replaceTerm) = 0;
    IFACEMETHOD(OnFlagsChanged)(_In_ DWORD flags) = 0;
    IFACEMETHOD(OnFileTimeChanged)(_In_ SYSTEMTIME fileTime) = 0;
};

interface __declspec(uuid("E3ED45B5-9CE0-47E2-A595-67EB950B9B72")) IPowerRenameRegEx : public IUnknown
{
public:
    IFACEMETHOD(Advise)(_In_ IPowerRenameRegExEvents* regExEvents, _Out_ DWORD* cookie) = 0;
    IFACEMETHOD(UnAdvise)(_In_ DWORD cookie) = 0;
    IFACEMETHOD(GetSearchTerm)(_Outptr_ PWSTR* searchTerm) = 0;
    IFACEMETHOD(PutSearchTerm)(_In_ PCWSTR searchTerm, bool forceRenaming = false) = 0;
    IFACEMETHOD(GetReplaceTerm)(_Outptr_ PWSTR* replaceTerm) = 0;
    IFACEMETHOD(PutReplaceTerm)(_In_ PCWSTR replaceTerm, bool forceRenaming = false) = 0;
    IFACEMETHOD(GetFlags)(_Out_ DWORD* flags) = 0;
    IFACEMETHOD(PutFlags)(_In_ DWORD flags) = 0;
    IFACEMETHOD(PutFileTime)(_In_ SYSTEMTIME fileTime) = 0;
    IFACEMETHOD(ResetFileTime)() = 0;
    IFACEMETHOD(Replace)(_In_ PCWSTR source, _Outptr_ PWSTR* result) = 0;
};

interface __declspec(uuid("C7F59201-4DE1-4855-A3A2-26FC3279C8A5")) IPowerRenameItem : public IUnknown
{
public:
    IFACEMETHOD(PutPath)(_In_opt_ PCWSTR newPath) = 0;
    IFACEMETHOD(GetPath)(_Outptr_ PWSTR * path) = 0;
    IFACEMETHOD(GetTime)(_Outptr_ SYSTEMTIME* time) = 0;
    IFACEMETHOD(GetShellItem)(_Outptr_ IShellItem** ppsi) = 0;
    IFACEMETHOD(GetOriginalName)(_Outptr_ PWSTR * originalName) = 0;
    IFACEMETHOD(PutOriginalName)(_In_opt_ PCWSTR originalName) = 0;
    IFACEMETHOD(GetNewName)(_Outptr_ PWSTR * newName) = 0;
    IFACEMETHOD(PutNewName)(_In_opt_ PCWSTR newName) = 0;
    IFACEMETHOD(GetIsFolder)(_Out_ bool* isFolder) = 0;
    IFACEMETHOD(GetIsSubFolderContent)(_Out_ bool* isSubFolderContent) = 0;
    IFACEMETHOD(GetSelected)(_Out_ bool* selected) = 0;
    IFACEMETHOD(PutSelected)(_In_ bool selected) = 0;
    IFACEMETHOD(GetId)(_Out_ int *id) = 0;
    IFACEMETHOD(GetDepth)(_Out_ UINT* depth) = 0;
    IFACEMETHOD(PutDepth)(_In_ int depth) = 0;
    IFACEMETHOD(ShouldRenameItem)(_In_ DWORD flags, _Out_ bool* shouldRename) = 0;
    IFACEMETHOD(IsItemVisible)(_In_ DWORD filter, _In_ DWORD flags, _Out_ bool* isItemVisible) = 0;
    IFACEMETHOD(Reset)() = 0;
};

interface __declspec(uuid("{26CBFFD9-13B3-424E-BAC9-D12B0539149C}")) IPowerRenameItemFactory : public IUnknown
{
public:
    IFACEMETHOD(Create)(_In_ IShellItem* psi, _COM_Outptr_ IPowerRenameItem** ppItem) = 0;
};

interface __declspec(uuid("87FC43F9-7634-43D9-99A5-20876AFCE4AD")) IPowerRenameManagerEvents : public IUnknown
{
public:
    IFACEMETHOD(OnItemAdded)(_In_ IPowerRenameItem* renameItem) = 0;
    IFACEMETHOD(OnUpdate)(_In_ IPowerRenameItem * renameItem) = 0;
    IFACEMETHOD(OnRename)(_In_ IPowerRenameItem * renameItem) = 0;
    IFACEMETHOD(OnError)(_In_ IPowerRenameItem * renameItem) = 0;
    IFACEMETHOD(OnRegExStarted)(_In_ DWORD threadId) = 0;
    IFACEMETHOD(OnRegExCanceled)(_In_ DWORD threadId) = 0;
    IFACEMETHOD(OnRegExCompleted)(_In_ DWORD threadId) = 0;
    IFACEMETHOD(OnRenameStarted)() = 0;
    IFACEMETHOD(OnRenameCompleted)(_In_ bool closeUIWindowAfterRenaming) = 0;
};

interface __declspec(uuid("001BBD88-53D2-4FA6-95D2-F9A9FA4F9F70")) IPowerRenameManager : public IUnknown
{
public:
    IFACEMETHOD(Advise)(_In_ IPowerRenameManagerEvents* renameManagerEvent, _Out_ DWORD* cookie) = 0;
    IFACEMETHOD(UnAdvise)(_In_ DWORD cookie) = 0;
    IFACEMETHOD(Start)() = 0;
    IFACEMETHOD(Stop)() = 0;
    IFACEMETHOD(Reset)() = 0;
    IFACEMETHOD(Shutdown)() = 0;
    IFACEMETHOD(Rename)(_In_ HWND hwndParent, _In_ bool closeWindow) = 0;
    IFACEMETHOD(UpdateChildrenPath)(_In_ int parentId, _In_ size_t oldParentPathSize) = 0;
    IFACEMETHOD(GetCloseUIWindowAfterRenaming)(_Out_ bool* closeUIWindowAfterRenaming) = 0;
    IFACEMETHOD(AddItem)(_In_ IPowerRenameItem * pItem) = 0;
    IFACEMETHOD(GetItemByIndex)(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem) = 0;
    IFACEMETHOD(GetVisibleItemByIndex)(_In_ UINT index, _COM_Outptr_ IPowerRenameItem ** ppItem) = 0;
    IFACEMETHOD(SetVisible)() = 0;
    IFACEMETHOD(GetItemById)(_In_ int id, _COM_Outptr_ IPowerRenameItem** ppItem) = 0;
    IFACEMETHOD(GetItemCount)(_Out_ UINT* count) = 0;
    IFACEMETHOD(GetVisibleItemCount)(_Out_ UINT* count) = 0;
    IFACEMETHOD(GetSelectedItemCount)(_Out_ UINT* count) = 0;
    IFACEMETHOD(GetRenameItemCount)(_Out_ UINT* count) = 0;
    IFACEMETHOD(GetFlags)(_Out_ DWORD* flags) = 0;
    IFACEMETHOD(PutFlags)(_In_ DWORD flags) = 0;
    IFACEMETHOD(GetFilter)(_Out_ DWORD* filter) = 0;
    IFACEMETHOD(SwitchFilter)(_In_ int columnNumber) = 0;
    IFACEMETHOD(GetRenameRegEx)(_COM_Outptr_ IPowerRenameRegEx** ppRegEx) = 0;
    IFACEMETHOD(PutRenameRegEx)(_In_ IPowerRenameRegEx* pRegEx) = 0;
    IFACEMETHOD(GetRenameItemFactory)(_COM_Outptr_ IPowerRenameItemFactory** ppItemFactory) = 0;
    IFACEMETHOD(PutRenameItemFactory)(_In_ IPowerRenameItemFactory* pItemFactory) = 0;
};

interface __declspec(uuid("04AAFABE-B76E-4E13-993A-B5941F52B139")) IPowerRenameMRU : public IUnknown
{
public:
    IFACEMETHOD(AddMRUString)(_In_ PCWSTR entry) = 0;
    IFACEMETHOD_(const std::vector<std::wstring>&, GetMRUStrings)() = 0;
};

interface __declspec(uuid("CE8C8616-C1A8-457A-9601-10570F5B9F1F")) IPowerRenameEnum : public IUnknown
{
public:
    IFACEMETHOD(Start)
    (_In_ IEnumShellItems * enumShellItems) = 0;
    IFACEMETHOD(Cancel)() = 0;
};
