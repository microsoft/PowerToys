#include "pch.h"
#include "PdfThumbnailProvider.h"

#include <filesystem>
#include <Shlwapi.h>
#include <string>

#include <wil/com.h>

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/thumbnail_provider.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

PdfThumbnailProvider::PdfThumbnailProvider() :
    m_cRef(1), m_pStream(NULL)
{
    std::filesystem::path logFilePath(PTSettingsHelper::get_local_low_folder_location());
    logFilePath.append(LogSettings::pdfThumbLogPath);
    Logger::init(LogSettings::pdfThumbLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    InterlockedIncrement(&g_cDllRef);
}

PdfThumbnailProvider::~PdfThumbnailProvider()
{
    thumbnail_provider::release_stream(m_pStream);
    InterlockedDecrement(&g_cDllRef);
}

#pragma region IUnknown

IFACEMETHODIMP PdfThumbnailProvider::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(PdfThumbnailProvider, IThumbnailProvider),
        QITABENT(PdfThumbnailProvider, IInitializeWithStream),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG)
PdfThumbnailProvider::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG)
PdfThumbnailProvider::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (0 == cRef)
    {
        delete this;
    }
    return cRef;
}

#pragma endregion

#pragma region IInitializationWithStream

IFACEMETHODIMP PdfThumbnailProvider::Initialize(IStream* pStream, DWORD grfMode)
{
    HRESULT hr = E_INVALIDARG;
    if (pStream)
    {
        // Initialize can be called more than once, so release existing valid
        // m_pStream.
        thumbnail_provider::release_stream(m_pStream);

        m_pStream = pStream;
        m_pStream->AddRef();
        hr = S_OK;
    }
    return hr;
}

#pragma endregion

#pragma region IThumbnailProvider

IFACEMETHODIMP PdfThumbnailProvider::GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha)
{
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
            std::wstring filePath = PTSettingsHelper::get_local_low_folder_location() + L"\\PdfThumbnail-Temp\\";
            if (!std::filesystem::exists(filePath))
            {
                std::filesystem::create_directories(filePath);
            }

            std::wstring fileName = filePath + guid + L".pdf";
            std::wstring fileNameBmp = filePath + guid + L".bmp";

            const auto copyResult = thumbnail_provider::copy_stream_to_file(m_pStream, fileName);
            thumbnail_provider::release_stream(m_pStream);

            if (FAILED(copyResult))
            {
                std::error_code error;
                std::filesystem::remove(fileName, error);
                return copyResult;
            }

            try
            {
                Logger::info(L"Start PdfThumbnailProvider.exe");

                std::wstring cmdLine{ L"\"" + fileName + L"\"" };
                cmdLine += L" ";
                cmdLine += std::to_wstring(cx);

                std::wstring appPath = get_module_folderpath(g_hInst) + L"\\PowerToys.PdfThumbnailProvider.exe";
                const auto timeoutMs = thumbnail_provider::get_timeout_ms();
                const auto launchResult = thumbnail_provider::launch_in_job(appPath, cmdLine, timeoutMs);

                std::error_code error;
                std::filesystem::remove(fileName, error);

                if (launchResult.status != thumbnail_provider::launch_status::completed)
                {
                    if (launchResult.status == thumbnail_provider::launch_status::timed_out)
                    {
                        Logger::error(L"Pdf thumbnail provider timed out after {} ms.", timeoutMs);
                    }
                    else
                    {
                        Logger::error(L"Failed to launch Pdf thumbnail provider. Error: {}", launchResult.error);
                    }

                    std::filesystem::remove(fileNameBmp, error);
                    return HRESULT_FROM_WIN32(launchResult.error == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : launchResult.error);
                }

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
                Logger::error(L"Failed to start PdfThumbnailProvider.exe. Error: {}", errorMessage);
                std::error_code error;
                std::filesystem::remove(fileName, error);
                std::filesystem::remove(fileNameBmp, error);
            }
        }
    }

    // ensure releasing the stream (not all if branches contain it)
    thumbnail_provider::release_stream(m_pStream);

    return S_OK;
}

#pragma endregion

#pragma region Helper Functions

#pragma endregion
