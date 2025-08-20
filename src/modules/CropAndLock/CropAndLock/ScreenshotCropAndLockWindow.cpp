#include "pch.h"
#include "ScreenshotCropAndLockWindow.h"

const std::wstring ScreenshotCropAndLockWindow::ClassName = L"CropAndLock.ScreenshotCropAndLockWindow";
std::once_flag ScreenshotCropAndLockWindowClassRegistration;

void ScreenshotCropAndLockWindow::RegisterWindowClass()
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

ScreenshotCropAndLockWindow::ScreenshotCropAndLockWindow(std::wstring const& titleString, int width, int height)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(ScreenshotCropAndLockWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = 0;
    auto style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;

    RECT rect = { 0, 0, width, height };
    winrt::check_bool(AdjustWindowRectEx(&rect, style, false, exStyle));
    auto adjustedWidth = rect.right - rect.left;
    auto adjustedHeight = rect.bottom - rect.top;

    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), titleString.c_str(), style, CW_USEDEFAULT, CW_USEDEFAULT, adjustedWidth, adjustedHeight, nullptr, nullptr, instance, this));
    WINRT_ASSERT(m_window);
}

ScreenshotCropAndLockWindow::~ScreenshotCropAndLockWindow()
{
    DestroyWindow(m_window);
}

LRESULT ScreenshotCropAndLockWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
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
    case WM_PAINT:
        if (m_captured && m_bitmap)
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(m_window, &ps);
            HDC memDC = CreateCompatibleDC(hdc);
            SelectObject(memDC, m_bitmap.get());

            RECT clientRect = {};
            GetClientRect(m_window, &clientRect);
            int clientWidth = clientRect.right - clientRect.left;
            int clientHeight = clientRect.bottom - clientRect.top;

            int srcWidth = m_destRect.right - m_destRect.left;
            int srcHeight = m_destRect.bottom - m_destRect.top;

            float srcAspect = static_cast<float>(srcWidth) / srcHeight;
            float dstAspect = static_cast<float>(clientWidth) / clientHeight;

            int drawWidth = clientWidth;
            int drawHeight = static_cast<int>(clientWidth / srcAspect);
            if (dstAspect > srcAspect)
            {
                drawHeight = clientHeight;
                drawWidth = static_cast<int>(clientHeight * srcAspect);
            }

            int offsetX = (clientWidth - drawWidth) / 2;
            int offsetY = (clientHeight - drawHeight) / 2;

            SetStretchBltMode(hdc, HALFTONE);
            StretchBlt(hdc, offsetX, offsetY, drawWidth, drawHeight, memDC, 0, 0, srcWidth, srcHeight, SRCCOPY);
            DeleteDC(memDC);
            EndPaint(m_window, &ps);
        }
        break;
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}

void ScreenshotCropAndLockWindow::CropAndLock(HWND windowToCrop, RECT cropRect)
{
    if (m_captured)
    {
        return;
    }

    // Get full window bounds
    RECT windowRect{};
    winrt::check_hresult(DwmGetWindowAttribute(
        windowToCrop,
        DWMWA_EXTENDED_FRAME_BOUNDS,
        &windowRect,
        sizeof(windowRect)));

    RECT clientRect = ClientAreaInScreenSpace(windowToCrop);
    auto offsetX = clientRect.left - windowRect.left;
    auto offsetY = clientRect.top - windowRect.top;

    m_sourceRect = {
        cropRect.left + offsetX,
        cropRect.top + offsetY,
        cropRect.right + offsetX,
        cropRect.bottom + offsetY
    };

    int fullWidth = windowRect.right - windowRect.left;
    int fullHeight = windowRect.bottom - windowRect.top;

    HDC fullDC = CreateCompatibleDC(nullptr);
    HDC screenDC = GetDC(nullptr);
    HBITMAP fullBitmap = CreateCompatibleBitmap(screenDC, fullWidth, fullHeight);
    HGDIOBJ oldFullBitmap = SelectObject(fullDC, fullBitmap);

    // Capture full window
    winrt::check_bool(PrintWindow(windowToCrop, fullDC, PW_RENDERFULLCONTENT));


    // Crop
    int cropWidth = m_sourceRect.right - m_sourceRect.left;
    int cropHeight = m_sourceRect.bottom - m_sourceRect.top;

    HDC cropDC = CreateCompatibleDC(nullptr);
    HBITMAP cropBitmap = CreateCompatibleBitmap(screenDC, cropWidth, cropHeight);
    HGDIOBJ oldCropBitmap = SelectObject(cropDC, cropBitmap);
    ReleaseDC(nullptr, screenDC);

    BitBlt(
        cropDC,
        0,
        0,
        cropWidth,
        cropHeight,
        fullDC,
        m_sourceRect.left,
        m_sourceRect.top,
        SRCCOPY);

    SelectObject(fullDC, oldFullBitmap);
    DeleteObject(fullBitmap);
    DeleteDC(fullDC);

    SelectObject(cropDC, oldCropBitmap);
    DeleteDC(cropDC);
    m_bitmap.reset(cropBitmap);

    // Resize our window
    RECT dest{ 0, 0, cropWidth, cropHeight };
    LONG_PTR exStyle = GetWindowLongPtrW(m_window, GWL_EXSTYLE);
    LONG_PTR style = GetWindowLongPtrW(m_window, GWL_STYLE);

    winrt::check_bool(AdjustWindowRectEx(&dest, static_cast<DWORD>(style), FALSE, static_cast<DWORD>(exStyle)));

    winrt::check_bool(SetWindowPos(
        m_window, HWND_TOPMOST, 0, 0, dest.right - dest.left, dest.bottom - dest.top, SWP_NOMOVE | SWP_SHOWWINDOW));

    m_destRect = { 0, 0, cropWidth, cropHeight };
    m_captured = true;
    InvalidateRect(m_window, nullptr, FALSE);
}