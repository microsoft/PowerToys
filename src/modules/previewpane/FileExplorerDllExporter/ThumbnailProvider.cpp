#include "pch.h"
#include "ThumbnailProvider.h"

#include <filesystem>
#include <fstream>
#include <shellapi.h>
#include <Shlwapi.h>
#include <string>

#include <wil/com.h>

#include <common/utils/process_path.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

ThumbnailProvider::ThumbnailProvider(std::string name, std::wstring logFilePath, std::wstring exeName, std::wstring tempFolderName, std::wstring extension) :
    m_cRef(1), m_pStream(NULL), m_process(NULL), m_exeName(exeName), m_tempFolderName(tempFolderName), m_extension(extension)
{
    Logger::init(name, logFilePath, PTSettingsHelper::get_log_settings_file_location());

    InterlockedIncrement(&g_cDllRef);
}

ThumbnailProvider::~ThumbnailProvider()
{
    InterlockedDecrement(&g_cDllRef);
}


IFACEMETHODIMP ThumbnailProvider::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(ThumbnailProvider, IThumbnailProvider),
        QITABENT(ThumbnailProvider, IInitializeWithStream),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
ThumbnailProvider::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG)
ThumbnailProvider::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (0 == cRef)
    {
        delete this;
    }
    return cRef;
}

IFACEMETHODIMP ThumbnailProvider::Initialize(IStream* pStream, DWORD grfMode)
{
    HRESULT hr = E_INVALIDARG;
    if (pStream)
    {
        // Initialize can be called more than once, so release existing valid
        // m_pStream.
        if (m_pStream)
        {
            m_pStream->Release();
            m_pStream = NULL;
        }

        m_pStream = pStream;
        m_pStream->AddRef();
        hr = S_OK;
    }
    return hr;
}


IFACEMETHODIMP ThumbnailProvider::GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha)
{
    // Read stream into the buffer
    char buffer[4096];
    ULONG cbRead;

    Logger::trace(L"Begin");

    GUID guid;
    if (CoCreateGuid(&guid) == S_OK)
    {
        wil::unique_cotaskmem_string guidString;
        if (SUCCEEDED(StringFromCLSID(guid, &guidString)))
        {
            Logger::info(L"Read stream and save to tmp file.");

            // {CLSID} -> CLSID
            std::wstring guid = std::wstring(guidString.get()).substr(1, std::wstring(guidString.get()).size() - 2);
            std::wstring filePath = PTSettingsHelper::get_local_low_folder_location() + L"\\" + m_tempFolderName + L"\\";
            if (!std::filesystem::exists(filePath))
            {
                std::filesystem::create_directories(filePath);
            }

            std::wstring fileName = filePath + guid + m_extension;
            
            // Write data to tmp file
            std::fstream file;
            file.open(fileName, std::ios_base::out | std::ios_base::binary);

            if (!file.is_open())
            {
                return 0;
            }

            while (true)
            {
                auto result = m_pStream->Read(buffer, 4096, &cbRead);

                file.write(buffer, cbRead);
                if (result == S_FALSE)
                {               
                    break;
                }
            }
            file.close();

            m_pStream->Release();
            m_pStream = NULL;

            try
            {
                Logger::info(L"Start " + m_exeName);

                STARTUPINFO info = { sizeof(info) };
                std::wstring cmdLine{ L"\"" + fileName + L"\"" };
                cmdLine += L" ";
                cmdLine += std::to_wstring(cx);

                std::wstring appPath = get_module_folderpath(g_hInst) + L"\\" + m_exeName;

                SHELLEXECUTEINFO sei{ sizeof(sei) };
                sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
                sei.lpFile = appPath.c_str();
                sei.lpParameters = cmdLine.c_str();
                sei.nShow = SW_SHOWDEFAULT;
                ShellExecuteEx(&sei);
                m_process = sei.hProcess;
                WaitForSingleObject(m_process, INFINITE);
                std::filesystem::remove(fileName);


                std::wstring fileNameBmp = filePath + guid + L".bmp";
                if (std::filesystem::exists(fileNameBmp))
                {
                    *phbmp = static_cast<HBITMAP>(LoadImage(NULL, fileNameBmp.c_str(), IMAGE_BITMAP, 0, 0, LR_LOADFROMFILE));
                    *pdwAlpha = WTS_ALPHATYPE::WTSAT_ARGB;
                    std::filesystem::remove(fileNameBmp);
                }
                else
                {
                    Logger::info(L"Bmp file not generated.");
                    return E_FAIL;
                }
            }
            catch (std::exception& e)
            {
                std::wstring errorMessage = std::wstring{ winrt::to_hstring(e.what()) };
                Logger::error(L"Failed to start {}. Error: {}", m_exeName, errorMessage);
            }
        }
    }

    // ensure releasing the stream (not all if branches contain it)
    if (m_pStream)
    {
        m_pStream->Release();
        m_pStream = NULL;
    }

    return S_OK;
}


