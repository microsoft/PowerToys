#include "pch.h"
#include "PdfPreviewHandler.h"

#include <Shlwapi.h>
#include <string>

#include <common/utils/process_path.h>
#include <shellapi.h>
#include <common/interop/shared_constants.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

PdfPreviewHandler::PdfPreviewHandler() :
    m_cRef(1), m_hwndParent(NULL), m_rcParent(), m_punkSite(NULL), m_process(NULL)
{
    InterlockedIncrement(&g_cDllRef);
}

PdfPreviewHandler::~PdfPreviewHandler()
{
    InterlockedDecrement(&g_cDllRef);
}

#pragma region IUnknown

IFACEMETHODIMP PdfPreviewHandler::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(PdfPreviewHandler, IPreviewHandler),
        QITABENT(PdfPreviewHandler, IInitializeWithFile),
        QITABENT(PdfPreviewHandler, IPreviewHandlerVisuals),
        QITABENT(PdfPreviewHandler, IOleWindow),
        QITABENT(PdfPreviewHandler, IObjectWithSite),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
PdfPreviewHandler::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG)
PdfPreviewHandler::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (0 == cRef)
    {
        delete this;
    }
    return cRef;
}

#pragma endregion

#pragma region IInitializationWithFile

IFACEMETHODIMP PdfPreviewHandler::Initialize(LPCWSTR pszFilePath, DWORD grfMode)
{
    m_filePath = pszFilePath;
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandler

IFACEMETHODIMP PdfPreviewHandler::SetWindow(HWND hwnd, const RECT* prc)
{
    if (hwnd && prc)
    {
        m_hwndParent = hwnd;
        m_rcParent = *prc;
    }
    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::SetFocus()
{
    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::QueryFocus(HWND* phwnd)
{
    HRESULT hr = E_INVALIDARG;
    if (phwnd)
    {
        *phwnd = ::GetFocus();
        if (*phwnd)
        {
            hr = S_OK;
        }
        else
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
        }
    }
    return hr;
}

IFACEMETHODIMP PdfPreviewHandler::TranslateAccelerator(MSG* pmsg)
{
    HRESULT hr = S_FALSE;
    IPreviewHandlerFrame* pFrame = NULL;
    if (m_punkSite && SUCCEEDED(m_punkSite->QueryInterface(&pFrame)))
    {
        hr = pFrame->TranslateAccelerator(pmsg);

        pFrame->Release();
    }
    return hr;
}

IFACEMETHODIMP PdfPreviewHandler::SetRect(const RECT* prc)
{
    HRESULT hr = E_INVALIDARG;
    if (prc != NULL)
    {
        m_rcParent = *prc;
        auto resizeEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::PDF_PREVIEW_RESIZE_EVENT);
        if (!resizeEvent)
        {
            // Logger::warn(L"Failed to create exit event for {}", get_last_error_or_default(GetLastError()));
        }
        else
        {
            if (!SetEvent(resizeEvent))
            {
                // Logger::warn(L"Failed to signal exit event for  {}", get_last_error_or_default(GetLastError()));

                // For some reason, we couldn't process the signal correctly, so we still
                // need to terminate the PowerAccent process.
                // TerminateProcess(m_process, 0);
            }
        }
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP PdfPreviewHandler::DoPreview()
{
    try
    {
        STARTUPINFO info = { sizeof(info) };
        std::wstring cmdLine{ L"\"" + m_filePath + L"\"" };
        cmdLine += L" ";
        std::wostringstream ss;
        ss << std::hex << m_hwndParent;

        cmdLine += ss.str();
        cmdLine += L" ";
        cmdLine += std::to_wstring(m_rcParent.left);
        cmdLine += L" ";
        cmdLine += std::to_wstring(m_rcParent.right);
        cmdLine += L" ";
        cmdLine += std::to_wstring(m_rcParent.top);
        cmdLine += L" ";
        cmdLine += std::to_wstring(m_rcParent.bottom);
        std::wstring appPath = get_module_folderpath(g_hInst) + L"\\PowerToys.PdfPreviewHandler.exe";

        SHELLEXECUTEINFO sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = appPath.c_str();
        sei.lpParameters = cmdLine.c_str();
        sei.nShow = SW_SHOWDEFAULT;
        ShellExecuteEx(&sei);
        m_process = sei.hProcess;
    }
    catch (std::exception&)
    {
        // PreviewError
    }

    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::Unload()
{
    TerminateProcess(m_process, 0);
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandlerVisuals

IFACEMETHODIMP PdfPreviewHandler::SetBackgroundColor(COLORREF color)
{
    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::SetFont(const LOGFONTW* plf)
{
    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::SetTextColor(COLORREF color)
{
    return S_OK;
}

#pragma endregion

#pragma region IOleWindow

IFACEMETHODIMP PdfPreviewHandler::GetWindow(HWND* phwnd)
{
    HRESULT hr = E_INVALIDARG;
    if (phwnd)
    {
        *phwnd = m_hwndParent;
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP PdfPreviewHandler::ContextSensitiveHelp(BOOL fEnterMode)
{
    return E_NOTIMPL;
}

#pragma endregion

#pragma region IObjectWithSite

IFACEMETHODIMP PdfPreviewHandler::SetSite(IUnknown* punkSite)
{
    if (m_punkSite)
    {
        m_punkSite->Release();
        m_punkSite = NULL;
    }
    return punkSite ? punkSite->QueryInterface(&m_punkSite) : S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::GetSite(REFIID riid, void** ppv)
{
    *ppv = NULL;
    return m_punkSite ? m_punkSite->QueryInterface(riid, ppv) : E_FAIL;
}

#pragma endregion

#pragma region Helper Functions

#pragma endregion
