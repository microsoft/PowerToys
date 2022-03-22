#include "pch.h"
#include "resource.h"
#pragma comment(lib, "d2d1")
#include "FileTypeUtils.h"
#include "ViewerApp.h"

// The main window class name.
static TCHAR szWindowClass[] = _T("Spacebar");


int CALLBACK WinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPSTR lpCmdLine,
    _In_ int nCmdShow
) {
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);
    UNREFERENCED_PARAMETER(nCmdShow);

    App app;

    // uncomment to be able to attach to process for debugging
    //while (!::IsDebuggerPresent())
    //    ::Sleep(100); // to avoid 100% CPU load

    if (SUCCEEDED(app.Initialize(hInstance)))
    {
        // Main message loop:
        MSG msg;
        while (GetMessage(&msg, NULL, 0, 0))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    return 0;
}

// This should probably live in another file
App::App()
    :
    m_pIPreviewHandler(nullptr),
    m_pIWICFactory(nullptr),
    m_pD2DFactory(nullptr),
    m_pIWICBitmap(nullptr),
    m_pRenderTarget(nullptr),
    m_pD2DBitmap(nullptr),
    m_pImageFactory(nullptr),
    m_hWndPreview(0),
    m_singleInstanceMutex(0),
    m_bitmapWidth(0),
    m_bitmapHeight(0),
    m_currentItem(0)
{
    // COINIT_DISABLE_OLE1DDE: Setting this flag avoids some overhead associated with Object Linking and Embedding (OLE) 1.0, an obsolete technology
    CoInitializeEx(NULL, COINIT_MULTITHREADED | COINIT_DISABLE_OLE1DDE);
}

App::~App()
{
    CleanUp();
    if (m_pIWICFactory)
    {
        m_pIWICFactory.Release();
        m_pIWICFactory = nullptr;
    }
    if (m_pD2DFactory)
    {
        m_pD2DFactory.Release();
        m_pD2DFactory = nullptr;
    }
    if (m_pRenderTarget)
    {
        m_pRenderTarget.Release();
        m_pRenderTarget = nullptr;
    }
    if (m_pIPreviewHandler)
    {
        m_pIPreviewHandler->Unload();
        m_pIPreviewHandler.Release();
        m_pIPreviewHandler = nullptr;
    }

    // Uninitialize COM
    CoUninitialize();
}

void App::CleanUp()
{
    m_bitmapWidth = 0;
    m_bitmapHeight = 0;

    if (m_pD2DBitmap)
    {
        m_pD2DBitmap.Release();
        m_pD2DBitmap = nullptr;
    }
    if (m_pIWICBitmap)
    {
        m_pIWICBitmap.Release();
        m_pIWICBitmap = nullptr;
    }
    if (m_pImageFactory)
    {
        m_pImageFactory.Release();
        m_pImageFactory = nullptr;
    }
}

HRESULT App::CreateAppWindow(HINSTANCE hInstance)
{
    if (FAILED(InitializeTheme()))
    {
        m_isDarkModeSupported = false;
    }

    WNDCLASSEX wcex;

    HICON icon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_ICON1));

    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = App::s_WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = sizeof(LONG_PTR);
    wcex.hInstance = hInstance;
    wcex.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_ICON1));
    wcex.hCursor = LoadCursor(NULL, IDC_ARROW);
    wcex.hbrBackground = NULL;
    wcex.lpszMenuName = NULL;
    wcex.lpszClassName = szWindowClass;
    wcex.hIconSm = NULL;
    
    HRESULT hr = RegisterClassEx(&wcex) ? S_OK : E_FAIL;

    DestroyIcon(icon);

    if (SUCCEEDED(hr))
    {
        m_hWndPreview = CreateWindow(
            szWindowClass,                                                                              // Name of the application
            NULL,                                                                                       // Title bar text
            WindowStyle,                                                                                // Type of window to create
            m_windowRect.left, m_windowRect.top,                                                        // Initial position
            m_windowRect.right - m_windowRect.left, m_windowRect.bottom - m_windowRect.top,             // initial size
            NULL,                                                                                       // Parent of this window
            NULL,                                                                                       // App does not have a menu bar
            hInstance,                                                                                  // First parameter from WinMain
            this
        );

        hr = m_hWndPreview ? S_OK : E_FAIL;
    }

    return hr;
}

HRESULT App::InitializeResources()
{
    // Create WIC factory
    HRESULT hr = CoCreateInstance(
        CLSID_WICImagingFactory,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&m_pIWICFactory)
    );

    if (SUCCEEDED(hr))
    {
        // Create D2D factory
        hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &m_pD2DFactory);
    }

    return hr;
}

HRESULT App::ParseFileNames()
{
    LPWSTR* szArglist;
    int nArgs;

    // First argument is SpacebarPreview.exe
    szArglist = CommandLineToArgvW(GetCommandLine(), &nArgs);

    if (NULL == szArglist || nArgs == 1)
    {
        wprintf(L"CommandLineToArgv failed or no file name specificed\n");

        return E_FAIL;
    }

    HRESULT hr = S_OK;

    LPWSTR filenames = szArglist[1];
    std::wstringstream stream(filenames);
    std::wstring line;

    std::vector<std::wstring> files;

    while (std::getline(stream, line))
    {
        auto file = line.substr(0, std::wstring::npos);
        if (std::filesystem::exists(file))
        {
            files.emplace_back(file);
        }
    }

    if (files.size() == 1) // Only one file is opened. Get all files in folder for navigation.
    {
        WCHAR drive[_MAX_DRIVE] = {};
        WCHAR dir[_MAX_DIR] = {};
        WCHAR name[_MAX_FNAME] = {};
        WCHAR ext[_MAX_EXT] = {};
        
        WCHAR path[_MAX_DIR] = {};

        if (_wsplitpath_s(files[0].c_str(), drive, _MAX_DRIVE, dir, _MAX_DIR, name, _MAX_FNAME, ext, _MAX_EXT) == 0)
        {
            // Construct the path of the folder
            wcscpy_s(path, drive);
            wcscat_s(path, dir);

            int index = 0;
                
            // TODO: this doesn't get us the order the user has the files sorted in. Need a different solution.
            // Also does not handle navigating files in icon view in File Explorer
            for (const auto& entry : std::filesystem::directory_iterator(path))
            {
                m_fileList.emplace_back(ParseFileInfo(entry));
                    
                // found the file the user is trying to access
                if (std::filesystem::equivalent(entry.path(), files[0]))
                {
                    m_currentItem = index;
                }
                index++;
            }
        }
        else
        {
            hr = E_FAIL;
        }
    }
    else if (files.size() > 1)
    {
        for (std::wstring file : files)
        {
            auto entry = std::filesystem::directory_entry(file.c_str());
            m_fileList.emplace_back(ParseFileInfo(entry));
        }
    }

    if (SUCCEEDED(hr) && !m_fileList.empty())
    {
        hr = SHCreateItemFromParsingName(GetFilePathAt(m_currentItem), NULL, IID_PPV_ARGS(&m_pImageFactory));
    }

    return hr;
}

FileInfo App::ParseFileInfo(std::filesystem::directory_entry entry)
{
    FileInfo fileInfo = { 0 };
    wcscpy_s(fileInfo.filename, entry.path().filename().c_str());
    wcscpy_s(fileInfo.fullPath, entry.path().c_str());
    wcscpy_s(fileInfo.extension, entry.path().extension().c_str());

    fileInfo.size = entry.file_size();

    auto sctp = std::chrono::time_point_cast<std::chrono::system_clock::duration>(entry.last_write_time()
        - std::filesystem::file_time_type::clock::now()
        + std::chrono::system_clock::now());
    fileInfo.lastModified = std::chrono::system_clock::to_time_t(sctp);

    SHFILEINFO sfi;
    auto hr = SHGetFileInfo(entry.path().c_str(),
        -1,
        &sfi,
        sizeof(sfi),
        SHGFI_TYPENAME);

    if (SUCCEEDED(hr))
    {
        wcscpy_s(fileInfo.fileTypeName, sfi.szTypeName);
    }
    else
    {
        // TODO: failed to get file name.
        wcscpy_s(fileInfo.fileTypeName, L"Error getting filename");
    }

    fileInfo.isFolder = entry.is_directory();

    if (entry.is_directory())
    {
        fileInfo.fileCount = 0;
        for (const auto& entry : std::filesystem::directory_iterator(entry.path()))
        {
            if (entry.is_regular_file())
            {
                fileInfo.size += entry.file_size();
                fileInfo.fileCount++;
            }
        }
    }
    else
    {
        fileInfo.fileCount = 0;
    }

    return fileInfo;
}

HRESULT App::Initialize(HINSTANCE hInstance)
{
    m_singleInstanceMutex = CreateMutex(NULL, TRUE, L"SpacebarPreview");
    if (m_singleInstanceMutex == NULL || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        HWND existingApp = FindWindow(szWindowClass, NULL);
        if (existingApp)
        {
            SetForegroundWindow(existingApp);
        }
        return E_FAIL;
    }

    auto initAsync = std::async([this] { return this->InitializeResources(); });

    HRESULT hr = ParseFileNames();
    auto fileName = GetFilePathAt(m_currentItem);

    if (SUCCEEDED(hr))
    {
        hr = CreateAppWindow(hInstance);
    }

    if (SUCCEEDED(initAsync.get()) && SUCCEEDED(hr))
    {
        auto createAsync = std::async([this] { return this->CreateDeviceResources(); });
        if (!m_fileList.empty() && SUCCEEDED(hr))
        {
            hr = OpenFile(fileName);

            if (SUCCEEDED(hr))
            {
                UpdateWindowSize();
            }

            if (SUCCEEDED(hr) && SUCCEEDED(createAsync.get()))
            {
                auto prepImg = std::async([this] { return this->PrepareImage(); });

                hr = UpdateWindowProperties();

                // The calling thread must own this window.
                bool animate = AnimateWindow(GetWindowHandle(), 120, AW_ACTIVATE | AW_BLEND);
                
                if (SUCCEEDED(hr) && SUCCEEDED(prepImg.get()) && animate)
                {
                    hr = RedrawWindow();
                }
            }
            else
            {
                hr = E_FAIL;
            }
        }
    }
    else
    {
        hr = E_FAIL;
    }


    return hr;
}


HRESULT App::InitializeTheme()
{
    HMODULE hUxtheme = LoadLibraryExW(L"uxtheme.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    HMODULE user32 = GetModuleHandleW(L"user32.dll");
    if (hUxtheme && user32)
    {
        _SetWindowCompositionAttribute = reinterpret_cast<fnSetWindowCompositionAttribute>(GetProcAddress(user32, "SetWindowCompositionAttribute"));
        _ShouldAppsUseDarkMode = reinterpret_cast<fnShouldAppsUseDarkMode>(GetProcAddress(hUxtheme, MAKEINTRESOURCEA(132)));
        _RefreshImmersiveColorPolicyState = reinterpret_cast<fnRefreshImmersiveColorPolicyState>(GetProcAddress(hUxtheme, MAKEINTRESOURCEA(104)));
    }
    else
    {
        return E_FAIL;
    }
    return S_OK;
}

LRESULT CALLBACK App::s_WndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    App* pThis;
    LRESULT lRet = 0;

    if (uMsg == WM_NCCREATE)
    {
        auto pcs = reinterpret_cast<LPCREATESTRUCT>(lParam);
        pThis = reinterpret_cast<App*>(pcs->lpCreateParams);

        SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        lRet = DefWindowProc(hWnd, uMsg, wParam, lParam);
    }
    else
    {
        pThis = reinterpret_cast<App*> (GetWindowLongPtr(hWnd, GWLP_USERDATA));
        if (pThis)
        {
            lRet = pThis->WndProc(hWnd, uMsg, wParam, lParam);
        }
        else
        {
            lRet = DefWindowProc(hWnd, uMsg, wParam, lParam);
        }
    }
    return lRet;
}

void App::RefreshTitleBarThemeColor(HWND hWnd)
{
    if (m_isDarkModeSupported)
    {
        BOOL darkMode = _ShouldAppsUseDarkMode();

        WINDOWCOMPOSITIONATTRIBDATA data = { WCA_USEDARKMODECOLORS, &darkMode, sizeof(darkMode) };
        _SetWindowCompositionAttribute(hWnd, &data);
    }
}

LRESULT App::WndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    switch (uMsg)
    {
    case WM_CREATE:
        RefreshTitleBarThemeColor(hWnd);
        break;
    case WM_SETTINGCHANGE:
        if (m_isDarkModeSupported &&
            lParam && CompareStringOrdinal(reinterpret_cast<LPCWCH>(lParam), -1, L"ImmersiveColorSet", -1, TRUE) == CSTR_EQUAL)
        {
            _RefreshImmersiveColorPolicyState();
            SendMessageW(hWnd, WM_THEMECHANGED, 0, 0);
        }
        break;
    case WM_THEMECHANGED:
        RefreshTitleBarThemeColor(hWnd);
        break;
    case WM_PAINT:
        return OnPaint();
    case WM_KEYDOWN:
        switch (wParam)
        {
        case VK_LEFT:
            if (IsLoaded() && m_currentItem > 0)
            {
                NavigateToFile(--m_currentItem);
            }
            break;
        case VK_RIGHT:
            if (IsLoaded() && m_currentItem < m_fileList.size() - 1)
            {
                NavigateToFile(++m_currentItem);
            }
            break;
        // Display preview when pressing P key
        case 0x50:
            if (!m_isPreviewLoaded && IsLoaded())
            {
                TryPreviewLoad(GetFilePathAt(m_currentItem));
            }
            break;
        case VK_RETURN:
            if (FAILED(TryLaunchDefaultHandler()))
            {
                // Handle failed app launch
            }
            break;
        default:
            break;
        }
        break;
    case WM_KEYUP:
        switch (wParam)
        {
        case VK_SPACE:
            DestroyWindow(hWnd);
            break;
        default:
            break;
        }
        break;
    case WM_GETMINMAXINFO:
    {
        LPMINMAXINFO lpMMI = (LPMINMAXINFO)lParam;
        lpMMI->ptMinTrackSize.x = MIN_WINDOW_SIZE.cx;
        lpMMI->ptMinTrackSize.y = MIN_WINDOW_SIZE.cy;

        break;
    }
    case WM_SIZE:
    {
        auto size = D2D1::SizeU(LOWORD(lParam), HIWORD(lParam));

        if (m_pRenderTarget)
        {
            // If we couldn't resize, release the device and we'll recreate it during the next render pass.
            if (FAILED(m_pRenderTarget->Resize(size)))
            {
                if (nullptr != m_pRenderTarget)
                {
                    m_pRenderTarget.Release();
                    m_pRenderTarget = nullptr;
                }

                if (nullptr != m_pD2DBitmap)
                {
                    m_pD2DBitmap.Release();
                    m_pD2DBitmap = nullptr;
                }
            }
        }
        break;
    }
    case WM_KILLFOCUS:
        // DestroyWindow(hWnd);
        break;
    case WM_CLOSE:
        DestroyWindow(hWnd);
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, uMsg, wParam, lParam);
        break;
    }

    return 0;
}

void App::NavigateToFile(UINT i)
{
    OpenFile(GetFilePathAt(i));
    UpdateWindowSize();
    PrepareImage();
    UpdateWindowProperties();
    RedrawWindow();
}

HRESULT App::OpenFile(LPCWSTR filename)
{
    HBITMAP hBitmap = nullptr;

    HRESULT hr = S_OK;

    // This part shouldn't be called on first launch

    if (IsLoaded())
    {
        CleanUp();
        // This is already done asynchronously on launch for perf reasons.
        hr = SHCreateItemFromParsingName(GetFilePathAt(m_currentItem), NULL, IID_PPV_ARGS(&m_pImageFactory));
    }
    if (SUCCEEDED(hr))
    {
        hr = LoadThumbnail(&hBitmap);
    }

    if (SUCCEEDED(hr))
    {
        hr = m_pIWICFactory->CreateBitmapFromHBITMAP(hBitmap, 0, WICBitmapUseAlpha, &m_pIWICBitmap);
    }

    if (SUCCEEDED(hr))
    {
        hr = m_pIWICBitmap->GetSize(&m_bitmapWidth, &m_bitmapHeight);
    }

    if (hBitmap)
    {
        DeleteObject(hBitmap);
    }

    return hr;
}


HRESULT App::LoadThumbnail(__RPC__deref_out_opt HBITMAP* hBitmap)
{
    HRESULT hr = S_OK;

    auto extension = GetFileInfoAt(m_currentItem).extension;
    bool isMedia = FileTypeUtils::IsMedia(extension);
    bool isDocument = FileTypeUtils::IsDocument(extension);

    bool shouldLoadImageThumbnail = isMedia || isDocument;
    if (shouldLoadImageThumbnail)
    {
        auto imageFlags = SIIGBF_BIGGERSIZEOK | SIIGBF_THUMBNAILONLY | SIIGBF_SCALEUP | SIIGBF_RESIZETOFIT;
        hr = m_pImageFactory->GetImage(EXTRALARGE, imageFlags, hBitmap);

        if (FAILED(hr))
        {
            hr = m_pImageFactory->GetImage(LARGE, imageFlags, hBitmap);

        }

        if (FAILED(hr))
        {
            hr = m_pImageFactory->GetImage(MEDIUM, imageFlags, hBitmap);
        }

        if (FAILED(hr))
        {
            hr = m_pImageFactory->GetImage(SMALL, imageFlags, hBitmap);
        }

        if (SUCCEEDED(hr))
        {
            m_isIcon = false;
        }
    }

    if (!shouldLoadImageThumbnail || FAILED(hr))
    {
        auto iconFlags = SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY;
        hr = m_pImageFactory->GetImage(LARGE, iconFlags, hBitmap);

        if (SUCCEEDED(hr))
        {
            m_isIcon = true;
        }
    }

    return hr;
}

HRESULT App::PrepareImage()
{
    CComPtr<IWICFormatConverter> spFormatConverter;

    HRESULT hr = m_pIWICFactory->CreateFormatConverter(&spFormatConverter);

    if (SUCCEEDED(hr))
    {
        hr = spFormatConverter->Initialize(
            m_pIWICBitmap,
            GUID_WICPixelFormat32bppPBGRA,
            WICBitmapDitherTypeNone,
            NULL,
            0.f,
            WICBitmapPaletteTypeMedianCut);

        m_pRenderTarget->CreateBitmapFromWicBitmap(spFormatConverter, 0, &m_pD2DBitmap);
    }

    return hr;
}

HRESULT App::CreateDeviceResources()
{
    HRESULT hr = S_OK;

    if (!m_pRenderTarget)
    {
        auto renderTargetProperties = D2D1::RenderTargetProperties();

        D2D1_PIXEL_FORMAT pixelFormat = D2D1::PixelFormat(
            DXGI_FORMAT_B8G8R8A8_UNORM,
            D2D1_ALPHA_MODE_IGNORE
        );

        renderTargetProperties.pixelFormat = pixelFormat;

        // Set the DPI to be the default system DPI to allow direct mapping
        // between image pixels and desktop pixels in different system DPI settings
        renderTargetProperties.dpiX = DEFAULT_DPI;
        renderTargetProperties.dpiY = DEFAULT_DPI;

        RECT client;
        GetClientRect(GetWindowHandle(), &client);
        auto size = D2D1::SizeU(client.right - client.left, client.bottom - client.top);

        hr = m_pD2DFactory->CreateHwndRenderTarget(
            renderTargetProperties,
            D2D1::HwndRenderTargetProperties(GetWindowHandle(), size),
            &m_pRenderTarget
        );
    }

    return hr;
}

HRESULT App::UpdateWindowProperties()
{
    HRESULT hr = S_OK;

    if (IsLoaded())
    {
        RECT content;
        RECT resultingSize;

        // Scale content to fit window
        content = { 0, 0, (LONG)GetBitmapWidth(), (LONG)GetBitmapHeight() };
        if (m_isIcon)
        {
            resultingSize = m_windowRect;
        }
        else
        {
            resultingSize = CalculateLetterboxRect(m_windowRect, content, true, true, 0, 0);
        }
        

        // TODO: This should probably be moved to UpdateWindowSize. It makes more sense there.
        // Calculate window size based on content size
        AdjustWindowRect(&resultingSize, WindowStyle, false);
        auto windowWidth = resultingSize.right - resultingSize.left;
        auto windowHeight = resultingSize.bottom - resultingSize.top;

        // Center the position of the window relative to the monitor
        int windowX = (GetSystemMetrics(SM_CXSCREEN) - windowWidth) / 2;
        int windowY = (GetSystemMetrics(SM_CYSCREEN) - windowHeight) / 2;

        SetWindowPos(GetWindowHandle(), HWND_NOTOPMOST, windowX, windowY, windowWidth, windowHeight, SWP_NOACTIVATE) ? S_OK : E_FAIL;

        static const unsigned strMax = 8 + MAX_PATH;
        WCHAR appName[strMax] = L"Peek - "; // TODO: Localize this

        wcscat_s(appName, GetFileInfoAt(m_currentItem).filename);
        SetWindowTextW(GetWindowHandle(), appName) ? S_OK : E_FAIL;
    }

    return hr;
}

HRESULT App::RedrawWindow()
{
    return InvalidateRect(GetWindowHandle(), NULL, FALSE) ? S_OK : E_FAIL;
}

LRESULT App::OnPaint()
{
    HRESULT hr = S_OK;
    PAINTSTRUCT ps;
    HDC hdc;

    if (hdc = BeginPaint(GetWindowHandle(), &ps))
    {
        if (IsLoaded())
        {
            if (!(m_pRenderTarget->CheckWindowState() & D2D1_WINDOW_STATE_OCCLUDED))
            {
                m_pRenderTarget->BeginDraw();

                m_pRenderTarget->SetTransform(D2D1::Matrix3x2F::Identity());

                // Clear the background
                m_pRenderTarget->Clear();

                // Create a rectangle with size of current window
                RECT client;
                GetClientRect(GetWindowHandle(), &client);

                RECT bitmapRect = { 0, 0, (LONG)GetBitmapWidth(), (LONG)GetBitmapHeight() };

                if (m_isIcon)
                {
                    m_contentRect = CalculateLetterboxRect(client, bitmapRect, true, false, 0, -70);
                }
                else
                {
                    m_contentRect = CalculateLetterboxRect(client, bitmapRect, true, true, 0, 0);
                }
                
                auto destinationRect = D2D1::RectF((FLOAT)m_contentRect.left, (FLOAT)m_contentRect.top, (FLOAT)m_contentRect.right, (FLOAT)m_contentRect.bottom);

                // D2DBitmap may have been released due to device loss. 
                // If so, re-create it from the source bitmap
                if (m_pIWICBitmap && !m_pD2DBitmap)
                {
                    m_pRenderTarget->CreateBitmapFromWicBitmap(m_pIWICBitmap, nullptr, &m_pD2DBitmap);
                }

                // Draws an image and scales it to the current window size
                if (m_pD2DBitmap)
                {
                    m_pRenderTarget->DrawBitmap(m_pD2DBitmap, destinationRect);
                }

                hr = m_pRenderTarget->EndDraw();

                // In case of device loss, discard D2D render target and D2DBitmap
                // They will be re-created in the next rendering pass
                if (hr == D2DERR_RECREATE_TARGET)
                {
                    if (m_pD2DBitmap)
                    {
                        m_pD2DBitmap.Release();
                        m_pD2DBitmap = nullptr;
                    }
                    if (m_pD2DBitmap)
                    {
                        m_pRenderTarget.Release();
                        m_pRenderTarget = nullptr;
                    }

                    // Force a re-render
                    hr = InvalidateRect(GetWindowHandle(), nullptr, TRUE) ? S_OK : E_FAIL;
                }

                if (m_isIcon)
                {
                    PaintFileInfo(hdc, client);
                }
            }
        }

        EndPaint(GetWindowHandle(), &ps);
    }

    return SUCCEEDED(hr) ? 0 : 1;
}

void App::PaintFileInfo(HDC hdc, RECT const& client)
{
    // Write info
    HFONT hFont;
    RECT fileTypeRect;
    RECT fileInfoRect;

    SetRect(&fileTypeRect, client.left, m_contentRect.bottom + 40, client.right, m_contentRect.bottom + 70);
    SetRect(&fileInfoRect, client.left, fileTypeRect.bottom, client.right, fileTypeRect.bottom + 200);

    hFont = CreateFont(
        30, 0, 0, 0,                // cHeight, cWidth, cEscapement, cOrientation
        FW_HEAVY,                   // cWeight
        FALSE,                      // bItalic
        FALSE,                      // bUnderline
        FALSE,                      // bStrikeOut
        DEFAULT_CHARSET,            // iCharSet
        OUT_OUTLINE_PRECIS,         // iOutPrecision
        CLIP_DEFAULT_PRECIS,        // iClipPrecision
        CLEARTYPE_QUALITY,          // iQuality
        VARIABLE_PITCH,             // iPitchAndFamily
        TEXT("Segoe UI regular"));  // pszFaceName

    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, RGB(255, 255, 255));

    //Sets the coordinates for the rectangle in which the text is to be formatted.
    SelectObject(hdc, hFont);
    DrawText(hdc, GetFileInfoAt(m_currentItem).fileTypeName, -1, &fileTypeRect, DT_CENTER);
    DeleteObject(hFont);

    hFont = CreateFont(
        22, 0, 0, 0,                // cHeight, cWidth, cEscapement, cOrientation
        FW_DONTCARE,                // cWeight
        FALSE,                      // bItalic
        FALSE,                      // bUnderline
        FALSE,                      // bStrikeOut
        DEFAULT_CHARSET,            // iCharSet
        OUT_OUTLINE_PRECIS,         // iOutPrecision
        CLIP_DEFAULT_PRECIS,        // iClipPrecision
        CLEARTYPE_QUALITY,          // iQuality
        VARIABLE_PITCH,             // iPitchAndFamily
        TEXT("Segoe UI regular"));  // pszFaceName

    SelectObject(hdc, hFont);

    std::wstringstream buffer;

    auto size = FormatSize(GetFileInfoAt(m_currentItem).size);
    buffer << size;

    if (GetFileInfoAt(m_currentItem).isFolder)
    {
        buffer << L" (" << GetFileInfoAt(m_currentItem).fileCount;
        buffer << (GetFileInfoAt(m_currentItem).fileCount == 1 ? L" file)" : L" files)"); // TODO: Localize this
    }
    buffer << L"\n";

    auto time = GetFileInfoAt(m_currentItem).lastModified;
    std::tm localTime;
    localtime_s(&localTime, &time);
    buffer << std::put_time(&localTime, L"Last modified %B %d, %Y at %I:%M%p"); // TODO: Localize this

    std::wstring formattedInfo = buffer.str();

    DrawText(hdc, formattedInfo.c_str(), -1, &fileInfoRect, DT_CENTER);

    DeleteObject(hFont);
}

RECT App::CalculateLetterboxRect(RECT const& client, RECT const& content, bool bCenter, bool scaleContent, int contentOffsetX, int contentOffsetY)
{
    int clientWidth = client.right - client.left;
    int clientHeight = client.bottom - client.top;

    int contentWidth = content.right - content.left;
    int contentHeight = content.bottom - content.top;

    // Calculate new content size
    int resultingWidth = contentWidth;
    int resultingHeight = contentHeight;

    // Only scale if it's not an icon
    if (scaleContent)
    {
        resultingWidth = ::MulDiv(clientHeight, contentWidth, contentHeight);
        resultingHeight = ::MulDiv(clientWidth, contentHeight, contentWidth);

        // Adjust dimensions to fit inside client area
        if (resultingWidth > clientWidth)
        {
            resultingWidth = clientWidth;
            resultingHeight = ::MulDiv(resultingWidth, contentHeight, contentWidth);
        }
        else
        {
            resultingHeight = clientHeight;
            resultingWidth = ::MulDiv(resultingHeight, contentWidth, contentHeight);
        }
    }

    RECT rect = { 0 };
    ::SetRect(&rect, 0, 0, resultingWidth, resultingHeight);

    int offsetX = contentOffsetX;
    int offsetY = contentOffsetY;
    if (bCenter)
    {
        // Calculate offsets to center content
        offsetX += ((clientWidth - resultingWidth) / 2);
        offsetY += ((clientHeight - resultingHeight) / 2);
    }

    ::OffsetRect(&rect, offsetX, offsetY);

    return rect;

}

void App::UpdateWindowSize()
{
    // Get monitor info
    POINT cursorPos;
    GetCursorPos(&cursorPos);
    HMONITOR hmonPrimary = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
    MONITORINFO monitorinfo = { 0 };
    monitorinfo.cbSize = sizeof(monitorinfo);
    GetMonitorInfo(hmonPrimary, &monitorinfo);

    // A RECT structure that specifies the work area rectangle of the display monitor
    m_monitorRect = monitorinfo.rcWork;

    float monitorPercentageW = 1.0f;
    float monitorPercentageH = 1.0f;

    // Set the maximum working area for the preview
    if (m_isIcon)
    {
        monitorPercentageW = ICON_DISPLAY_PERCENTAGE_W;
        monitorPercentageH = ICON_DISPLAY_PERCENTAGE_H;
    }
    else
    {
        auto extension = GetFileInfoAt(m_currentItem).extension;
        bool isMedia = FileTypeUtils::IsMedia(extension);
        bool isDocument = FileTypeUtils::IsDocument(extension);

        if (isMedia)
        {
            monitorPercentageW = MEDIA_FILE_DISPLAY_PERCENTAGE;
            monitorPercentageH = MEDIA_FILE_DISPLAY_PERCENTAGE;
        }
        else if (isDocument)
        {
            monitorPercentageW = NON_MEDIA_FILE_DISPLAY_PERCENTAGE;
            monitorPercentageH = NON_MEDIA_FILE_DISPLAY_PERCENTAGE;
        }
    }

    // the maximum working area for the preview
    auto windowWidth = (INT)(m_monitorRect.right * monitorPercentageW);
    auto windowHeight = (INT)(m_monitorRect.bottom * monitorPercentageH);

    m_windowRect.left = m_monitorRect.left + (m_monitorRect.right - m_monitorRect.left - windowWidth) / 2;
    m_windowRect.top = m_monitorRect.top + (m_monitorRect.bottom - m_monitorRect.top - windowHeight) / 2;
    m_windowRect.right = m_windowRect.left + windowWidth;
    m_windowRect.bottom = m_windowRect.top + windowHeight;
}

RECT App::ReduceRect(RECT const& rect, float percentage)
{
    RECT result;

    auto width = (INT)((rect.right - rect.left) * percentage);
    auto height = (INT)((rect.bottom - rect.top)* percentage);

    result.left = rect.left + (rect.right - rect.left - width) / 2;
    result.top = rect.top + (rect.bottom - rect.top - height) / 2;
    result.right = result.left + width;
    result.bottom = result.top + height;

    return result;
}

HRESULT App::TryPreviewLoad(LPCWSTR filename)
{
    HRESULT hr = S_OK;

    IQueryAssociations* pqa;
    hr = AssocCreate(CLSID_QueryAssociations, IID_IQueryAssociations, (LPVOID*)&pqa);
    if (SUCCEEDED(hr))
    {
        if (SUCCEEDED(hr))
        {
            hr = pqa->Init(ASSOCF_INIT_DEFAULTTOSTAR, GetFileInfoAt(m_currentItem).extension, NULL, NULL);
        }

        wchar_t szValue[MAX_PATH];
        DWORD cch = ARRAYSIZE(szValue);
        hr = pqa->GetString(ASSOCF_NOTRUNCATE, ASSOCSTR_SHELLEXTENSION, IPREVIEWHANDLER_CLSID, szValue, &cch);
        if (SUCCEEDED(hr) && szValue[0])
        {
            CLSID clsid;
            hr = CLSIDFromString(szValue, &clsid);
            //IUnknown* pUnk;
            IPreviewHandler* pUnk;
            hr = CoCreateInstance(clsid, NULL, CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&pUnk));
            // See https://blogs.msdn.microsoft.com/adioltean/2005/06/24/when-cocreateinstance-returns-0x80080005-co_e_server_exec_failure/
            if (hr == CO_E_SERVER_EXEC_FAILURE)
                hr = CoCreateInstance(clsid, NULL, CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&pUnk));

            if (SUCCEEDED(hr))
            {
                hr = pUnk->QueryInterface(IID_PPV_ARGS(&m_pIPreviewHandler));
                if (SUCCEEDED(hr))
                {
                    BOOL ReadyForPreview = FALSE;
                    IInitializeWithStream* piws;
                    hr = m_pIPreviewHandler->QueryInterface(IID_PPV_ARGS(&piws));
                    if (SUCCEEDED(hr))
                    {
                        IStream* pis;
                        hr = SHCreateStreamOnFileEx(filename, STGM_READ | STGM_SHARE_DENY_NONE, 0, 0, NULL, &pis);
                        if (SUCCEEDED(hr))
                        {
                            hr = piws->Initialize(pis, STGM_READ);
                            if (SUCCEEDED(hr))
                            {
                                ReadyForPreview = TRUE;
                            }
                            pis->Release();
                        }
                    }

                    //Attempting with IInitializeWithFile if IInitializeWithStream Fails
                    if (piws == NULL)
                    {
                        IInitializeWithFile* piwf;
                        hr = m_pIPreviewHandler->QueryInterface(IID_PPV_ARGS(&piwf));
                        if (SUCCEEDED(hr))
                        {
                            hr = piwf->Initialize(filename, STGM_READ);
                            if (SUCCEEDED(hr))
                            {
                                ReadyForPreview = TRUE;
                            }
                        }
                    }

                    if (ReadyForPreview == TRUE)
                    {
                        UpdateWindowSize();

                        SetWindowPos(GetWindowHandle(), HWND_NOTOPMOST,
                            m_windowRect.left,
                            m_windowRect.top,
                            m_windowRect.right - m_windowRect.left,
                            m_windowRect.bottom - m_windowRect.top,
                            SWP_SHOWWINDOW
                        );

                        RECT client;
                        GetClientRect(GetWindowHandle(), &client);
                        hr = m_pIPreviewHandler->SetWindow(GetWindowHandle(), &client);
                        hr = m_pIPreviewHandler->DoPreview();
                        hr = m_pIPreviewHandler->SetRect(&client);

                        if (SUCCEEDED(hr))
                        {
                            m_isPreviewLoaded = TRUE;
                        }
                    }
                }
                pUnk->Release();
            }
        }
        pqa->Release();
    }
    return hr;
}

HRESULT App::TryLaunchDefaultHandler()
{
    HRESULT hr = S_OK;

    // If the function succeeds, it returns a value greater than 32. 
    HINSTANCE hInstance = ShellExecuteW(GetWindowHandle(), NULL, GetFilePathAt(m_currentItem), NULL, NULL, SW_SHOW);
    if ((INT_PTR)hInstance > 32)
    {
        // Successfully launched app, close Peek
        PostQuitMessage(0);
    }
    else
    {
        // Handle error!
    }

    // In the future when we need the "Open with <app-friendly-name>", use this:
    // TCHAR defaultAppName[MAX_PATH];
    // DWORD bufSize = MAX_PATH;
    // hr = AssocQueryString(0, ASSOCSTR_FRIENDLYAPPNAME, GetFileInfoAt(m_currentItem).extension, 0, defaultAppName, &bufSize);

    return hr;
}

std::wstring App::FormatSize(uintmax_t size)
{
    int i = 0;
    double mantissa = static_cast<double>(size);
    for (; mantissa >= 1024.; ++i) {
        mantissa /= 1024.;
    }
    mantissa = std::ceil(mantissa * 10.) / 10.;
    std::wstringstream ss;
    ss << mantissa << "BKMGTPE"[i];

    if (i == 0)
    {
        return ss.str();
    }
    else
    {
        ss << "B";
        return ss.str();
    }
}
