#include "pch.h"
#include "ThumbnailCropAndLockWindow.h"

const std::wstring ThumbnailCropAndLockWindow::ClassName = L"CropAndLock.ThumbnailCropAndLockWindow";
std::once_flag ThumbnailCropAndLockWindowClassRegistration;

float ComputeScaleFactor(RECT const& windowRect, RECT const& contentRect)
{
    auto windowWidth = static_cast<float>(windowRect.right - windowRect.left);
    auto windowHeight = static_cast<float>(windowRect.bottom - windowRect.top);
    auto contentWidth = static_cast<float>(contentRect.right - contentRect.left);
    auto contentHeight = static_cast<float>(contentRect.bottom - contentRect.top);

    auto windowRatio = windowWidth / windowHeight;
    auto contentRatio = contentWidth / contentHeight;

    auto scaleFactor = windowWidth / contentWidth;
    if (windowRatio > contentRatio)
    {
        scaleFactor = windowHeight / contentHeight;
    }

    return scaleFactor;
}

RECT ComputeDestRect(RECT const& windowRect, RECT const& contentRect)
{
    auto scaleFactor = ComputeScaleFactor(windowRect, contentRect);

    auto windowWidth = static_cast<float>(windowRect.right - windowRect.left);
    auto windowHeight = static_cast<float>(windowRect.bottom - windowRect.top);
    auto contentWidth = static_cast<float>(contentRect.right - contentRect.left) * scaleFactor;
    auto contentHeight = static_cast<float>(contentRect.bottom - contentRect.top) * scaleFactor;

    auto remainingWidth = windowWidth - contentWidth;
    auto remainingHeight = windowHeight - contentHeight;

    auto left = static_cast<LONG>(remainingWidth / 2.0f);
    auto top = static_cast<LONG>(remainingHeight / 2.0f);
    auto right = left + static_cast<LONG>(contentWidth);
    auto bottom = top + static_cast<LONG>(contentHeight);

    return RECT{ left, top, right, bottom };
}

void ThumbnailCropAndLockWindow::RegisterWindowClass()
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    WNDCLASSEXW wcex = {};
    wcex.cbSize = sizeof(wcex);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = instance;
    wcex.hIcon = LoadIconW(instance, IDI_APPLICATION);
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.hbrBackground = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
    wcex.lpszClassName = ClassName.c_str();
    wcex.hIconSm = LoadIconW(wcex.hInstance, IDI_APPLICATION);
    winrt::check_bool(RegisterClassExW(&wcex));
}

ThumbnailCropAndLockWindow::ThumbnailCropAndLockWindow(std::wstring const& titleString, int width, int height)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(ThumbnailCropAndLockWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = 0;
    auto style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;

    RECT rect = { 0, 0, width, height};
    winrt::check_bool(AdjustWindowRectEx(&rect, style, false, exStyle));
    auto adjustedWidth = rect.right - rect.left;
    auto adjustedHeight = rect.bottom - rect.top;

    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), titleString.c_str(), style,
        CW_USEDEFAULT, CW_USEDEFAULT, adjustedWidth, adjustedHeight, nullptr, nullptr, instance, this));
    WINRT_ASSERT(m_window);
}

ThumbnailCropAndLockWindow::~ThumbnailCropAndLockWindow()
{
    DisconnectTarget();
    DestroyWindow(m_window);
}

LRESULT ThumbnailCropAndLockWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
{
    switch (message)
    {
    case WM_DESTROY:
        if (m_closedCallback != nullptr && !m_destroyed)
        {
            m_destroyed = true;
            m_closedCallback(m_window);
        }
        break;
    case WM_SIZE:
    case WM_SIZING:
    {
        if (m_thumbnail != nullptr)
        {
            RECT clientRect = {};
            winrt::check_bool(GetClientRect(m_window, &clientRect));

            m_destRect = ComputeDestRect(clientRect, m_sourceRect);

            DWM_THUMBNAIL_PROPERTIES properties = {};
            properties.dwFlags = DWM_TNP_RECTDESTINATION;
            properties.rcDestination = m_destRect;
            winrt::check_hresult(DwmUpdateThumbnailProperties(m_thumbnail.get(), &properties));
        }
    }
    break;
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}

void ThumbnailCropAndLockWindow::CropAndLock(HWND windowToCrop, RECT cropRect)
{
    DisconnectTarget();
    m_currentTarget = windowToCrop;

    // Adjust the crop rect to be in the window space as reported by the DWM
    RECT windowRect = {};
    winrt::check_hresult(DwmGetWindowAttribute(m_currentTarget, DWMWA_EXTENDED_FRAME_BOUNDS, reinterpret_cast<void*>(&windowRect), sizeof(windowRect)));
    auto clientRect = ClientAreaInScreenSpace(m_currentTarget);
    auto diffX = clientRect.left - windowRect.left;
    auto diffY = clientRect.top - windowRect.top;
    auto adjustedCropRect = cropRect;
    adjustedCropRect.left += diffX;
    adjustedCropRect.top += diffY;
    adjustedCropRect.right += diffX;
    adjustedCropRect.bottom += diffY;
    cropRect = adjustedCropRect;

    // Resize our window
    auto width = cropRect.right - cropRect.left;
    auto height = cropRect.bottom - cropRect.top;
    windowRect = { 0, 0, width, height };
    auto exStyle = static_cast<DWORD>(GetWindowLongPtrW(m_window, GWL_EXSTYLE));
    auto style = static_cast<DWORD>(GetWindowLongPtrW(m_window, GWL_STYLE));
    winrt::check_bool(AdjustWindowRectEx(&windowRect, style, false, exStyle));
    auto adjustedWidth = windowRect.right - windowRect.left;
    auto adjustedHeight = windowRect.bottom - windowRect.top;
    winrt::check_bool(SetWindowPos(m_window, HWND_TOPMOST, 0, 0, adjustedWidth, adjustedHeight, SWP_NOMOVE | SWP_SHOWWINDOW));

    // Setup the thumbnail
    winrt::check_hresult(DwmRegisterThumbnail(m_window, m_currentTarget, m_thumbnail.addressof()));

    clientRect = {};
    winrt::check_bool(GetClientRect(m_window, &clientRect));
    m_destRect = clientRect;
    m_sourceRect = cropRect;

    DWM_THUMBNAIL_PROPERTIES properties = {};
    properties.dwFlags = DWM_TNP_SOURCECLIENTAREAONLY | DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE;
    properties.fSourceClientAreaOnly = false;
    properties.fVisible = true;
    properties.opacity = 255;
    properties.rcDestination = m_destRect;
    properties.rcSource = m_sourceRect;
    winrt::check_hresult(DwmUpdateThumbnailProperties(m_thumbnail.get(), &properties));
}

void ThumbnailCropAndLockWindow::Hide()
{
    DisconnectTarget();
    ShowWindow(m_window, SW_HIDE);
}

void ThumbnailCropAndLockWindow::DisconnectTarget()
{
    if (m_currentTarget != nullptr)
    {
        m_thumbnail.reset();
        m_currentTarget = nullptr;
    }
}
