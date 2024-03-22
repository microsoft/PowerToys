#include "pch.h"
#include "MonacoPreviewHandler.h"
#include "../powerpreview/powerpreviewConstants.h"

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

MonacoPreviewHandler::MonacoPreviewHandler() :
    m_cRef(1), m_hwndParent(NULL), m_rcParent(), m_punkSite(NULL), m_process(NULL)
{
    m_resizeEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT);

    std::filesystem::path logFilePath(PTSettingsHelper::get_local_low_folder_location());
    logFilePath.append(LogSettings::monacoPrevLogPath);
    Logger::init(LogSettings::monacoPrevLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    InterlockedIncrement(&g_cDllRef);
}

MonacoPreviewHandler::~MonacoPreviewHandler()
{
    InterlockedDecrement(&g_cDllRef);
}

#pragma region IUnknown

IFACEMETHODIMP MonacoPreviewHandler::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(MonacoPreviewHandler, IPreviewHandler),
        QITABENT(MonacoPreviewHandler, IInitializeWithFile),
        QITABENT(MonacoPreviewHandler, IPreviewHandlerVisuals),
        QITABENT(MonacoPreviewHandler, IOleWindow),
        QITABENT(MonacoPreviewHandler, IObjectWithSite),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
MonacoPreviewHandler::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG)
MonacoPreviewHandler::Release()
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

IFACEMETHODIMP MonacoPreviewHandler::Initialize(LPCWSTR pszFilePath, DWORD grfMode)
{
    m_filePath = pszFilePath;
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandler

IFACEMETHODIMP MonacoPreviewHandler::SetWindow(HWND hwnd, const RECT* prc)
{
    if (hwnd && prc)
    {
        m_hwndParent = hwnd;
        m_rcParent = *prc;
    }
    return S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::SetFocus()
{
    return S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::QueryFocus(HWND* phwnd)
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

IFACEMETHODIMP MonacoPreviewHandler::TranslateAccelerator(MSG* pmsg)
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

IFACEMETHODIMP MonacoPreviewHandler::SetRect(const RECT* prc)
{
    HRESULT hr = E_INVALIDARG;
    if (prc != NULL)
    {
        if (m_rcParent.left == 0 && m_rcParent.top == 0 && m_rcParent.right == 0 && m_rcParent.bottom == 0 && (prc->left != 0 || prc->top != 0 || prc->right != 0 || prc->bottom != 0))
        {
            // MonacoPreviewHandler position first initialisation, do the first preview
            m_rcParent = *prc;
            DoPreview();
        }
        if (!m_resizeEvent)
        {
            Logger::error(L"Failed to create resize event for MonacoPreviewHandler");
        }
        else
        {
            if (m_rcParent.right != prc->right || m_rcParent.left != prc->left || m_rcParent.top != prc->top || m_rcParent.bottom != prc->bottom)
            {
                if (!SetEvent(m_resizeEvent))
                {
                    Logger::error(L"Failed to signal resize event for MonacoPreviewHandler");
                }
            }
        }
        m_rcParent = *prc;
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP MonacoPreviewHandler::DoPreview()
{
    try
    {
        if (m_rcParent.left == 0 && m_rcParent.top == 0 && m_rcParent.right == 0 && m_rcParent.bottom == 0)
        {
            // Postponing Start MonacoPreviewHandler.exe, position not yet initialized. preview will be done after initialisation
            return S_OK;
        }

        Logger::info(L"Starting MonacoPreviewHandler.exe");
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
        std::wstring appPath = get_module_folderpath(g_hInst) + L"\\PowerToys.MonacoPreviewHandler.exe";

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
        Logger::error(L"Failed to start MonacoPreviewHandler.exe. Error: {}", errorMessage);
    }

    return S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::Unload()
{
    Logger::info(L"Unload and terminate .exe");

    m_hwndParent = NULL;
    TerminateProcess(m_process, 0);
    return S_OK;
}

#pragma endregion

#pragma region IPreviewHandlerVisuals

IFACEMETHODIMP MonacoPreviewHandler::SetBackgroundColor(COLORREF color)
{
    HBRUSH brush = CreateSolidBrush(WindowsColors::is_dark_mode() ? powerpreviewConstants::DARK_THEME_COLOR : powerpreviewConstants::LIGHT_THEME_COLOR);
    SetClassLongPtr(m_hwndParent, GCLP_HBRBACKGROUND, reinterpret_cast<LONG_PTR>(brush));
    return S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::SetFont(const LOGFONTW* plf)
{
    return S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::SetTextColor(COLORREF color)
{
    return S_OK;
}

#pragma endregion

#pragma region IOleWindow

IFACEMETHODIMP MonacoPreviewHandler::GetWindow(HWND* phwnd)
{
    HRESULT hr = E_INVALIDARG;
    if (phwnd)
    {
        *phwnd = m_hwndParent;
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP MonacoPreviewHandler::ContextSensitiveHelp(BOOL fEnterMode)
{
    return E_NOTIMPL;
}

#pragma endregion

#pragma region IObjectWithSite

IFACEMETHODIMP MonacoPreviewHandler::SetSite(IUnknown* punkSite)
{
    if (m_punkSite)
    {
        m_punkSite->Release();
        m_punkSite = NULL;
    }
    return punkSite ? punkSite->QueryInterface(&m_punkSite) : S_OK;
}

IFACEMETHODIMP MonacoPreviewHandler::GetSite(REFIID riid, void** ppv)
{
    *ppv = NULL;
    return m_punkSite ? m_punkSite->QueryInterface(riid, ppv) : E_FAIL;
}

#pragma endregion

#pragma region Helper Functions

#pragma endregion
