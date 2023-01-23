#include "pch.h"
#include "PdfPreviewHandler.h"

#include <shellapi.h>
#include <Shlwapi.h>
#include <string>

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/process_path.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

PdfPreviewHandler::PdfPreviewHandler() :
    m_cRef(1), m_hwndParent(NULL), m_rcParent(), m_punkSite(NULL), m_process(NULL)
{
    m_resizeEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::PDF_PREVIEW_RESIZE_EVENT);

    std::filesystem::path logFilePath(PTSettingsHelper::get_local_low_folder_location());
    logFilePath.append(LogSettings::pdfPrevLogPath);
    Logger::init(LogSettings::pdfPrevLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

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
        if (!m_resizeEvent)
        {
            Logger::error(L"Failed to create resize event for PdfPreviewHandler");
        }
        else
        {
            if (m_rcParent.right != prc->right || m_rcParent.left != prc->left || m_rcParent.top != prc->top || m_rcParent.bottom != prc->bottom)
            {
                if (!SetEvent(m_resizeEvent))
                {
                    Logger::error(L"Failed to signal resize event for PdfPreviewHandler");
                }
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
        Logger::info(L"Starting PdfPreviewHandler.exe");

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
    catch (std::exception& e)
    {
        std::wstring errorMessage = std::wstring{ winrt::to_hstring(e.what()) };
        Logger::error(L"Failed to start PdfPreviewHandler.exe. Error: {}", errorMessage);
    }

    return S_OK;
}

IFACEMETHODIMP PdfPreviewHandler::Unload()
{
    Logger::info(L"Unload and terminate .exe");

    m_hwndParent = NULL;
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
