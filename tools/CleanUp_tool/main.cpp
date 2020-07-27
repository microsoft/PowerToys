#include <windows.h>
#include <stdlib.h>
#include <string.h>
#include <tchar.h>
#include <shlwapi.h>
#pragma comment(lib,"shlwapi.lib")
#include "shlobj.h"

static TCHAR szWindowClass[] = _T("CleanUp tool");
static TCHAR szTitle[] = _T("Tool to clean up FancyZones installation");

HINSTANCE hInst;

LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);
void CleanUp();
void RemoveSettingsFolder();
void ClearRegistry();

int CALLBACK WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPSTR lpCmdLine, _In_ int nCmdShow)
{
    WNDCLASSEX wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = 0;
    wcex.hInstance = hInstance;
    wcex.hIcon = LoadIcon(hInstance, IDI_APPLICATION);
    wcex.hCursor = LoadCursor(NULL, IDC_ARROW);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszMenuName = NULL;
    wcex.lpszClassName = szWindowClass;
    wcex.hIconSm = LoadIcon(wcex.hInstance, IDI_APPLICATION);

    if (!RegisterClassEx(&wcex))
    {
        MessageBox(NULL, _T("Call to RegisterClassEx failed!"), szTitle, NULL);
        return 1;
    }

    hInst = hInstance;

    HWND hWnd = CreateWindow(
        szWindowClass,
        szTitle,
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT,
        200, 200,
        NULL,
        NULL,
        hInstance,
        NULL
    );

    if (!hWnd)
    {
        MessageBox(NULL, _T("Call to CreateWindow failed!"), szTitle, NULL);
        return 1;
    }

    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

    HWND hwndButton = CreateWindow(
        L"BUTTON",  // Predefined class; Unicode assumed 
        L"Clear",      // Button text 
        WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,  // Styles 
        50,         // x position 
        50,         // y position 
        100,        // Button width
        100,        // Button height
        hWnd,     // Parent window
        (HMENU) 1,       // No menu.
        (HINSTANCE)GetWindowLongPtr(hWnd, GWLP_HINSTANCE),
        NULL);      // Pointer not needed.

    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return (int)msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    PAINTSTRUCT ps;
    HDC hdc;

    switch (message)
    {
    case WM_PAINT:
        hdc = BeginPaint(hWnd, &ps);
        EndPaint(hWnd, &ps);
        break;
    case WM_COMMAND:
        if (wParam == 1)
        {
            CleanUp();
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
        break;
    }

    return 0;
}

void CleanUp()
{
    RemoveSettingsFolder();
    ClearRegistry();
}

void RemoveSettingsFolder()
{
    wchar_t settingsPath[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPath(NULL, ssfLOCALAPPDATA, NULL, 0, settingsPath)))
    {
        PathAppend(settingsPath, L"\\Microsoft\\PowerToys");
    }

    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    if (SUCCEEDED(hr))
    {
        IFileOperation* pfo;
        hr = CoCreateInstance(CLSID_FileOperation, NULL, CLSCTX_ALL, IID_PPV_ARGS(&pfo));
        if (SUCCEEDED(hr))
        {
            hr = pfo->SetOperationFlags(FOF_NO_UI);
            if (SUCCEEDED(hr))
            {
                IShellItem* psiFrom = NULL;
                hr = SHCreateItemFromParsingName(settingsPath, NULL, IID_PPV_ARGS(&psiFrom));
                if (SUCCEEDED(hr))
                {
                    if (SUCCEEDED(hr))
                    {
                        hr = pfo->DeleteItem(psiFrom, NULL);
                    }
                    psiFrom->Release();
                }

                if (SUCCEEDED(hr))
                {
                    hr = pfo->PerformOperations();
                }
            }
            pfo->Release();
        }
        CoUninitialize();
    }
}

void ClearRegistry()
{
    RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\SuperFancyZones");
    RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerRename");
    RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{e16ea82f-6d94-4f30-bb02-d6d911588afd}");
    RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\Microsoft\\ImageResizer");
    //RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers");
    //RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops");
    //RegDeleteKeyW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops");
}
