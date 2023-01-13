// PowerRenameTest.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "PowerRenameTest.h"
#include <PowerRenameInterfaces.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include <Shobjidl.h>
#include <atlfile.h>
#include <atlstr.h>

#include <common/utils/process_path.h>

#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

HINSTANCE g_hostHInst;
void ModuleAddRef() {}
void ModuleRelease() {}

// {0440049F-D1DC-4E46-B27B-98393D79486B}
//DEFINE_GUID(CLSID_PowerRenameMenu, 0x0440049F, 0xD1DC, 0x4E46, 0xB2, 0x7B, 0x98, 0x39, 0x3D, 0x79, 0x48, 0x6B);

class __declspec(uuid("{0440049F-D1DC-4E46-B27B-98393D79486B}")) Foo;
static const CLSID CLSID_PowerRenameMenu = __uuidof(Foo);

DEFINE_GUID(BHID_DataObject, 0xb8c0bd9f, 0xed24, 0x455c, 0x83, 0xe6, 0xd5, 0x39, 0xc, 0x4f, 0xe8, 0xc4);

int APIENTRY wWinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE /*hPrevInstance*/,
    _In_ PWSTR /*lpCmdLine*/,
    _In_ int /*nCmdShow*/)
{
    g_hostHInst = hInstance;
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    if (SUCCEEDED(hr))
    {
        // Set the application path based on the location of the dll
        std::wstring path = get_module_folderpath(g_hostHInst);
        path = path + L"\\PowerToys.PowerRename.exe";
        LPTSTR lpApplicationName = (LPTSTR)path.c_str();

        CString commandLine;
        commandLine.Format(_T("\"%s\""), lpApplicationName);

        int nSize = commandLine.GetLength() + 1;
        LPTSTR lpszCommandLine = new TCHAR[nSize];
        _tcscpy_s(lpszCommandLine, nSize, commandLine);

        STARTUPINFO startupInfo;
        ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
        startupInfo.cb = sizeof(STARTUPINFO);
        startupInfo.dwFlags = STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES;
        startupInfo.wShowWindow = SW_SHOWNORMAL;

        PROCESS_INFORMATION processInformation;

        // Start the resizer
        CreateProcess(
            NULL,
            lpszCommandLine,
            NULL,
            NULL,
            TRUE,
            0,
            NULL,
            NULL,
            &startupInfo,
            &processInformation);
        delete[] lpszCommandLine;
        if (!CloseHandle(processInformation.hProcess))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }
        if (!CloseHandle(processInformation.hThread))
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            return hr;
        }
        CoUninitialize();
    }

    return 0;
}
