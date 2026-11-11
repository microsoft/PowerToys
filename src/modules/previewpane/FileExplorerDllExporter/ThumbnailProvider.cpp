#include "pch.h"
#include "ThumbnailProvider.h"

#include <filesystem>
#include <Shlwapi.h>
#include <string>

#include <wil/com.h>

#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/thumbnail_provider.h>

extern HINSTANCE g_hInst;
extern long g_cDllRef;

ThumbnailProvider::ThumbnailProvider(std::string name, std::wstring logFilePath, std::wstring exeName, std::wstring tempFolderName, std::wstring extension) :
    m_cRef(1), m_pStream(NULL), m_exeName(exeName), m_tempFolderName(tempFolderName), m_extension(extension)
{
    Logger::init(name, logFilePath, PTSettingsHelper::get_log_settings_file_location());

    InterlockedIncrement(&g_cDllRef);
}

ThumbnailProvider::~ThumbnailProvider()
{
    thumbnail_provider::release_stream(m_pStream);
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
        thumbnail_provider::release_stream(m_pStream);

        m_pStream = pStream;
        m_pStream->AddRef();
        hr = S_OK;
    }
    return hr;
}

IFACEMETHODIMP ThumbnailProvider::GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha)
{
    auto releaseStream = wil::scope_exit([&] { thumbnail_provider::release_stream(m_pStream); });
    if (!phbmp || !pdwAlpha)
    {
        return E_POINTER;
    }

    *phbmp = nullptr;
    *pdwAlpha = WTSAT_UNKNOWN;
    if (!m_pStream || cx == 0 || cx > 10000)
    {
        return E_INVALIDARG;
    }

    try
    {
        GUID guid;
        auto result = CoCreateGuid(&guid);
        if (FAILED(result))
        {
            return result;
        }

        wil::unique_cotaskmem_string guidString;
        result = StringFromCLSID(guid, &guidString);
        if (FAILED(result))
        {
            return result;
        }

        const auto directory = std::filesystem::path{ PTSettingsHelper::get_local_low_folder_location() } / m_tempFolderName;
        std::filesystem::create_directories(directory);
        const auto fileName = directory / (std::wstring{ guidString.get() } + m_extension);
        const auto bitmapName = directory / (std::wstring{ guidString.get() } + L".bmp");
        const auto svgProfile = directory / (std::wstring{ guidString.get() } + L".WebView2");
        auto removeFiles = wil::scope_exit([&] {
            for (const auto& path : { fileName, bitmapName })
            {
                std::error_code error;
                std::filesystem::remove(path, error);
                if (error)
                {
                    Logger::error(L"Failed to remove thumbnail temporary file. Error: {}", error.value());
                }
            }

            if (m_extension == L".svg")
            {
                // Browser descendants may still be releasing files after the job is closed.
                std::error_code error;
                for (int attempt = 0; attempt < 20; ++attempt)
                {
                    std::filesystem::remove_all(svgProfile, error);
                    if (!error)
                    {
                        break;
                    }

                    Sleep(50);
                }

                if (error)
                {
                    Logger::error(L"Failed to remove thumbnail browser data. Error: {}", error.value());
                }
            }
        });

        result = thumbnail_provider::copy_stream_to_file(m_pStream, fileName);
        thumbnail_provider::release_stream(m_pStream);
        if (FAILED(result))
        {
            Logger::error(L"Failed to copy thumbnail input. Error: {}", result);
            return result;
        }

        const auto application = std::filesystem::path{ get_module_folderpath(g_hInst) } / m_exeName;
        const auto arguments = L"\"" + fileName.wstring() + L"\" " + std::to_wstring(cx);
        const auto launch = thumbnail_provider::launch_in_job(application, arguments);
        if (launch.status != thumbnail_provider::launch_status::completed)
        {
            Logger::error(L"Thumbnail provider {} did not complete. Error: {}", m_exeName, launch.error);
            return HRESULT_FROM_WIN32(launch.error == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : launch.error);
        }

        if (launch.exit_code != 0)
        {
            Logger::error(L"Thumbnail provider {} exited with code {}.", m_exeName, launch.exit_code);
            return E_FAIL;
        }

        *phbmp = static_cast<HBITMAP>(LoadImageW(nullptr, bitmapName.c_str(), IMAGE_BITMAP, 0, 0, LR_LOADFROMFILE));
        if (!*phbmp)
        {
            Logger::info(L"Thumbnail bitmap was not generated or could not be loaded.");
            return E_FAIL;
        }

        *pdwAlpha = WTSAT_ARGB;
        return S_OK;
    }
    catch (const std::exception& error)
    {
        const std::wstring errorMessage{ winrt::to_hstring(error.what()) };
        Logger::error(L"Thumbnail provider {} failed. Error: {}", m_exeName, errorMessage);
        return E_FAIL;
    }
}
