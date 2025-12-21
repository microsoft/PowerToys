#include "pch.h"
#include "BgcodePreviewHandler.h"

#include <shellapi.h>
#include <Shlwapi.h>
#include <string>

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/process_path.h>
#include <common/Themes/windows_colors.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

BgcodePreviewHandler::BgcodePreviewHandler() :
    m_cRef(1), m_hwndParent(NULL), m_rcParent(), m_punkSite(NULL), m_process(NULL)
{
    m_resizeEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::BGCODE_PREVIEW_RESIZE_EVENT);

    std::filesystem::path logFilePath(PTSettingsHelper::get_local_low_folder_location());
    logFilePath.append(LogSettings::bgcodePrevLogPath);
    Logger::init(LogSettings::bgcodePrevLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    InterlockedIncrement(&g_cDllRef);
}

BgcodePreviewHandler::~BgcodePreviewHandler()
{
    InterlockedDecrement(&g_cDllRef);
}

#pragma region IUnknown

IFACEMETHODIMP BgcodePreviewHandler::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(BgcodePreviewHandler, IPreviewHandler),
        QITABENT(BgcodePreviewHandler, IInitializeWithFile),
        QITABENT(BgcodePreviewHandler, IPreviewHandlerVisuals),
        QITABENT(BgcodePreviewHandler, IOleWindow),
        QITABENT(BgcodePreviewHandler, IObjectWithSite),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
BgcodePreviewHandler::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG)
BgcodePreviewHandler::Release()
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

IFACEMETHODIMP BgcodePreviewHandler::Initialize(LPCWSTR pszFilePath, DWORD grfMode)
{
    m_filePath = pszFilePath;
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandler

IFACEMETHODIMP BgcodePreviewHandler::SetWindow(HWND hwnd, const RECT* prc)
{
    if (hwnd && prc)
    {
        m_hwndParent = hwnd;
        m_rcParent = *prc;
    }
    return S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::SetFocus()
{
    return S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::QueryFocus(HWND* phwnd)
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

IFACEMETHODIMP BgcodePreviewHandler::TranslateAccelerator(MSG* pmsg)
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

IFACEMETHODIMP BgcodePreviewHandler::SetRect(const RECT* prc)
{
    HRESULT hr = E_INVALIDARG;
    if (prc != NULL)
    {
        if (m_rcParent.left == 0 && m_rcParent.top == 0 && m_rcParent.right == 0 && m_rcParent.bottom == 0 && (prc->left != 0 || prc->top != 0 || prc->right != 0 || prc->bottom != 0))
        {
            // BgcodePreviewHandler position first initialisation, do the first preview
            m_rcParent = *prc;
            DoPreview();
        }
        if (!m_resizeEvent)
        {
            Logger::error(L"Failed to create resize event for BgcodePreviewHandler");
        }
        else
        {
            if (m_rcParent.right != prc->right || m_rcParent.left != prc->left || m_rcParent.top != prc->top || m_rcParent.bottom != prc->bottom)
            {
                if (!SetEvent(m_resizeEvent))
                {
                    Logger::error(L"Failed to signal resize event for BgcodePreviewHandler");
                }
            }
        }
        m_rcParent = *prc;
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP BgcodePreviewHandler::DoPreview()
{
    try
    {
        if (m_hwndParent == NULL || (m_rcParent.left == 0 && m_rcParent.top == 0 && m_rcParent.right == 0 && m_rcParent.bottom == 0))
        {
            // Postponing Start BgcodePreviewHandler.exe, parent and position not yet initialized. Preview will be done after initialisation.
            return S_OK;
        }
        Logger::info(L"Starting BgcodePreviewHandler.exe");

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
        std::wstring appPath = get_module_folderpath(g_hInst) + L"\\PowerToys.BgcodePreviewHandler.exe";

        SHELLEXECUTEINFO sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = appPath.c_str();
        sei.lpParameters = cmdLine.c_str();
        sei.nShow = SW_SHOWDEFAULT;
        ShellExecuteEx(&sei);

        // Prevent to leak processes: preview is called multiple times when minimizing and restoring Explorer window
        if (m_process)
        {
            TerminateProcess(m_process, 0);
        }

        m_process = sei.hProcess;
    }
    catch (std::exception& e)
    {
        std::wstring errorMessage = std::wstring{ winrt::to_hstring(e.what()) };
        Logger::error(L"Failed to start BgcodePreviewHandler.exe. Error: {}", errorMessage);
    }

    return S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::Unload()
{
    Logger::info(L"Unload and terminate .exe");

    m_hwndParent = NULL;
    TerminateProcess(m_process, 0);
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandlerVisuals

IFACEMETHODIMP BgcodePreviewHandler::SetBackgroundColor(COLORREF color)
{
    HBRUSH brush = CreateSolidBrush(WindowsColors::is_dark_mode() ? RGB(0x1e, 0x1e, 0x1e) : RGB(0xff, 0xff, 0xff));
    SetClassLongPtr(m_hwndParent, GCLP_HBRBACKGROUND, reinterpret_cast<LONG_PTR>(brush));
    return S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::SetFont(const LOGFONTW* plf)
{
    return S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::SetTextColor(COLORREF color)
{
    return S_OK;
}

#pragma endregion

#pragma region IOleWindow

IFACEMETHODIMP BgcodePreviewHandler::GetWindow(HWND* phwnd)
{
    HRESULT hr = E_INVALIDARG;
    if (phwnd)
    {
        *phwnd = m_hwndParent;
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP BgcodePreviewHandler::ContextSensitiveHelp(BOOL fEnterMode)
{
    return E_NOTIMPL;
}

#pragma endregion

#pragma region IObjectWithSite

IFACEMETHODIMP BgcodePreviewHandler::SetSite(IUnknown* punkSite)
{
    if (m_punkSite)
    {
        m_punkSite->Release();
        m_punkSite = NULL;
    }
    return punkSite ? punkSite->QueryInterface(&m_punkSite) : S_OK;
}

IFACEMETHODIMP BgcodePreviewHandler::GetSite(REFIID riid, void** ppv)
{
    *ppv = NULL;
    return m_punkSite ? m_punkSite->QueryInterface(riid, ppv) : E_FAIL;
}

#pragma endregion

#pragma region Helper Functions

#pragma endregion
