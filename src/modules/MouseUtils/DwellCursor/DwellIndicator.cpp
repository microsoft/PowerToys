#include "pch.h"
#include "DwellIndicator.h"
#include <gdiplus.h>
#include <cmath>

#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "dwmapi.lib")

using namespace Gdiplus;

/**
 * @brief Implementation class for the dwell indicator using the Pimpl idiom
 * 
 * This class handles all the visual indicator functionality:
 * - Creates a transparent, topmost window at cursor position
 * - Draws a circular progress arc using GDI+
 * - Updates progress smoothly during countdown
 * - Uses system accent color for theming
 */
class DwellIndicatorImpl
{
public:
    DwellIndicatorImpl() = default;
    ~DwellIndicatorImpl() = default;

    // Public interface methods
    bool Initialize();
    void Show(int x, int y);
    void UpdateProgress(float progress);
    void Hide();
    void Cleanup();

private:
    // Window management
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    bool CreateIndicatorWindow();
    void DrawIndicator(HDC hdc);
    float GetDpiScale() const;

    // Window class and visual constants
    static constexpr auto m_className = L"DwellCursorIndicator";
    static constexpr auto m_windowTitle = L"PowerToys Dwell Cursor Indicator";
    static constexpr float kIndicatorRadius = 20.0f;     // Circle radius in pixels
    static constexpr float kStrokeWidth = 3.0f;          // Arc stroke width in pixels

    // Window and positioning state
    HWND m_hwnd = NULL;                 // Handle to the indicator window
    HINSTANCE m_hinstance = NULL;       // Module instance handle
    bool m_isVisible = false;           // Current visibility state
    int m_currentX = 0;                 // Last shown X position
    int m_currentY = 0;                 // Last shown Y position
    float m_progress = 0.0f;            // Current progress (0.0 to 1.0)
    
    // GDI+ resources
    ULONG_PTR m_gdiplusToken = 0;       // GDI+ initialization token

    friend class DwellIndicator;
};

/**
 * @brief Window procedure for the indicator window
 * 
 * Handles window messages for the transparent indicator overlay:
 * - WM_PAINT: Triggers redraw of the progress arc
 * - WM_NCHITTEST: Returns HTTRANSPARENT to allow mouse events to pass through
 * - WM_DESTROY: Standard cleanup
 * 
 * @param hWnd Window handle
 * @param message Windows message ID
 * @param wParam Message parameter
 * @param lParam Message parameter
 * @return Message handling result
 */
LRESULT CALLBACK DwellIndicatorImpl::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    DwellIndicatorImpl* pThis = nullptr;
    
    // Retrieve the instance pointer stored during window creation
    if (message == WM_NCCREATE)
    {
        // During window creation, extract the 'this' pointer from creation params
        CREATESTRUCT* pcs = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = static_cast<DwellIndicatorImpl*>(pcs->lpCreateParams);
        SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
    }
    else
    {
        // For all other messages, retrieve the stored 'this' pointer
        pThis = reinterpret_cast<DwellIndicatorImpl*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
    }

    switch (message)
    {
    case WM_PAINT:
        // Redraw the indicator - this is where our visual progress arc gets drawn
        if (pThis)
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hWnd, &ps);
            pThis->DrawIndicator(hdc);  // Draw the circular progress indicator
            EndPaint(hWnd, &ps);
        }
        return 0;
        
    case WM_NCHITTEST:
        // Restore transparent mouse behavior - allow clicks to pass through
        return HTTRANSPARENT;
        
    case WM_DESTROY:
        // DO NOT call PostQuitMessage(0) for overlay windows!
        // This was interfering with PowerToys main message loop and causing settings menu issues
        // Just let the window be destroyed normally
        break;
        
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

/**
 * @brief Initialize the indicator system
 * 
 * Sets up GDI+ graphics system and creates the indicator window.
 * This must be called before any Show/Update operations.
 * 
 * @return true if initialization successful, false on failure
 */
bool DwellIndicatorImpl::Initialize()
{
    m_hinstance = GetModuleHandle(NULL);
    
    // Initialize GDI+ graphics system for smooth drawing
    GdiplusStartupInput gdiplusStartupInput;
    Gdiplus::Status status = GdiplusStartup(&m_gdiplusToken, &gdiplusStartupInput, NULL);
    if (status != Gdiplus::Ok)
    {
        // GDI+ initialization failed - no visual indicator will work
        return false;
    }
    
    // Create the transparent overlay window
    bool windowCreated = CreateIndicatorWindow();
    if (!windowCreated)
    {
        // Clean up GDI+ if window creation failed
        if (m_gdiplusToken != 0)
        {
            GdiplusShutdown(m_gdiplusToken);
            m_gdiplusToken = 0;
        }
    }
    
    return windowCreated;
}

/**
 * @brief Create the transparent indicator window
 * 
 * Creates a layered, transparent, topmost window that:
 * - Appears above all other windows
 * - Allows mouse events to pass through
 * - Has no border, title bar, or decorations
 * - Is positioned and sized later when shown
 * 
 * @return true if window created successfully, false on failure
 */
bool DwellIndicatorImpl::CreateIndicatorWindow()
{
    WNDCLASS wc{};
    
    // Set DPI awareness for proper scaling on high-DPI displays
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    
    // Register window class only if not already registered
    if (!GetClassInfoW(m_hinstance, m_className, &wc))
    {
        wc.lpfnWndProc = WndProc;                           // Our window procedure
        wc.hInstance = m_hinstance;                         // Module instance
        wc.hIcon = LoadIcon(m_hinstance, IDI_APPLICATION);  // Default icon
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);        // Default cursor
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH)); // Transparent background
        wc.lpszClassName = m_className;                     // Class name for window

        if (!RegisterClassW(&wc))
        {
            // Failed to register window class
            DWORD error = GetLastError();
            // Note: Can't use Logger here as it might not be available in all contexts
            return false;
        }
    }

    // Create window with transparency and mouse pass-through restored
    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
    
    m_hwnd = CreateWindowExW(
        exStyle,                    // Extended window styles with transparency restored
        m_className,                // Window class name
        m_windowTitle,              // Window title (not visible)
        WS_POPUP,                   // Window style - popup with no decorations
        0, 0, 100, 100,            // Initial position and size (will be adjusted in Show())
        nullptr,                    // No parent window
        nullptr,                    // No menu
        m_hinstance,                // Module instance
        this);                      // Pass 'this' pointer for WndProc to access

    if (!m_hwnd)
    {
        DWORD error = GetLastError();
        // Note: Can't use Logger here as it might not be available in all contexts
    }
    else
    {
        OutputDebugStringA("DwellIndicator: Created transparent layered window with mouse pass-through\n");
    }

    return m_hwnd != nullptr;
}

/**
 * @brief Show the indicator at specified cursor position
 * 
 * Positions the window centered on the cursor location and makes it visible.
 * The window size is calculated based on indicator radius and DPI scaling.
 * 
 * @param x Cursor X coordinate in screen pixels
 * @param y Cursor Y coordinate in screen pixels
 */
void DwellIndicatorImpl::Show(int x, int y)
{
    // Check if window handle is valid before proceeding
    if (!m_hwnd) 
    {
        OutputDebugStringA("DwellIndicator: ERROR - Window handle is NULL, cannot show indicator\n");
        return;
    }

    // **CRITICAL FIX: Reset progress state immediately when showing at new position**
    float oldProgress = m_progress;
    m_progress = 0.0f;

    // Store current position for reference
    m_currentX = x;
    m_currentY = y;
    
    // Calculate window size based on indicator radius and DPI scaling
    const float dpiScale = GetDpiScale();
    const int windowSize = static_cast<int>((kIndicatorRadius * 2 + kStrokeWidth * 2 + 10) * dpiScale);
    
    // Calculate final window position (centered on cursor)
    const int windowX = x - windowSize / 2;
    const int windowY = y - windowSize / 2;
    
    // Log detailed positioning information for debugging
    char debugMsg[512];
    sprintf_s(debugMsg, sizeof(debugMsg), 
        "DwellIndicator: SHOW - Cursor:(%d,%d) Window:(%d,%d) Size:%dx%d DPI:%.2f Progress: %.3f->0.0\n",
        x, y, windowX, windowY, windowSize, windowSize, dpiScale, oldProgress);
    OutputDebugStringA(debugMsg);
    
    // **GDI+ EXPERT FIX: Use UpdateLayeredWindow for proper transparency reset**
    // This is the correct way to handle layered windows with transparency
    
    // First hide the window to ensure clean state
    if (m_isVisible)
    {
        ShowWindow(m_hwnd, SW_HIDE);
        m_isVisible = false;
    }
    
    // Position window (while hidden for clean transition)
    BOOL setWindowPosResult = SetWindowPos(m_hwnd, HWND_TOPMOST, 
        windowX, windowY,           // Calculated position
        windowSize, windowSize,     // Square window to contain circle
        SWP_NOACTIVATE | SWP_HIDEWINDOW);  // Position but keep hidden for now
    
    if (!setWindowPosResult)
    {
        DWORD error = GetLastError();
        sprintf_s(debugMsg, sizeof(debugMsg), 
            "DwellIndicator: ERROR - SetWindowPos failed with error %lu\n", error);
        OutputDebugStringA(debugMsg);
    }
    
    // **GDI+ EXPERT FIX: Create clean bitmap and use UpdateLayeredWindow**
    // This completely clears any previous drawing artifacts
    HDC screenDC = GetDC(NULL);
    HDC memoryDC = CreateCompatibleDC(screenDC);
    
    // Create 32-bit bitmap with alpha channel for proper transparency
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = windowSize;
    bmi.bmiHeader.biHeight = -windowSize;  // Top-down DIB
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;         // 32-bit with alpha
    bmi.bmiHeader.biCompression = BI_RGB;
    
    void* pvBits = nullptr;
    HBITMAP hBitmap = CreateDIBSection(screenDC, &bmi, DIB_RGB_COLORS, &pvBits, NULL, 0);
    
    if (hBitmap && memoryDC)
    {
        HBITMAP oldBitmap = static_cast<HBITMAP>(SelectObject(memoryDC, hBitmap));
        
        // **CRITICAL: Clear the entire bitmap with transparent pixels**
        // This ensures no artifacts from previous drawings
        // Fix for C26451: Use safe arithmetic to prevent overflow
        const size_t bitmapSizeBytes = static_cast<size_t>(windowSize) * static_cast<size_t>(windowSize) * 4ULL;
        memset(pvBits, 0, bitmapSizeBytes);  // Clear to transparent
        
        // Create GDI+ Graphics object from memory DC
        Graphics graphics(memoryDC);
        graphics.SetSmoothingMode(SmoothingModeAntiAlias);
        graphics.SetCompositingMode(CompositingModeSourceOver);
        graphics.SetCompositingQuality(CompositingQualityHighQuality);
        
        // **GDI+ EXPERT: Use Graphics::Clear with transparent color**
        // This properly clears the alpha channel
        graphics.Clear(Color(0, 0, 0, 0));  // Fully transparent
        
        // Draw only the background circle (no progress arc yet since progress = 0.0)
        const float centerX = windowSize / 2.0f;
        const float centerY = windowSize / 2.0f;
        const float radius = kIndicatorRadius * dpiScale;
        const float strokeWidth = kStrokeWidth * dpiScale;
        
        // Get system accent color
        DWORD accentColor = 0;
        BOOL isOpaque = FALSE;
        Color backgroundCircleColor;
        
        if (SUCCEEDED(DwmGetColorizationColor(&accentColor, &isOpaque)))
        {
            const BYTE r = (accentColor >> 16) & 0xFF;
            const BYTE g = (accentColor >> 8) & 0xFF;
            const BYTE b = accentColor & 0xFF;
            const BYTE bgR = static_cast<BYTE>((r + 128) / 2);
            const BYTE bgG = static_cast<BYTE>((g + 128) / 2);
            const BYTE bgB = static_cast<BYTE>((b + 128) / 2);
            backgroundCircleColor = Color(80, bgR, bgG, bgB);
        }
        else
        {
            backgroundCircleColor = Color(80, 160, 160, 160);
        }
        
        // Draw background circle
        RectF ellipseRect(centerX - radius, centerY - radius, radius * 2, radius * 2);
        Pen bgPen(backgroundCircleColor, strokeWidth * 0.6f);
        graphics.DrawEllipse(&bgPen, ellipseRect);
        
        // **GDI+ EXPERT: Use UpdateLayeredWindow for artifact-free display**
        POINT ptSrc = {0, 0};
        POINT ptDst = {windowX, windowY};
        SIZE size = {windowSize, windowSize};
        BLENDFUNCTION blend = {};
        blend.BlendOp = AC_SRC_OVER;
        blend.SourceConstantAlpha = 255;
        blend.AlphaFormat = AC_SRC_ALPHA;  // Use per-pixel alpha
        
        BOOL updateResult = UpdateLayeredWindow(m_hwnd, screenDC, &ptDst, &size, 
                                              memoryDC, &ptSrc, 0, &blend, ULW_ALPHA);
        
        // Cleanup
        SelectObject(memoryDC, oldBitmap);
        DeleteObject(hBitmap);
        
        sprintf_s(debugMsg, sizeof(debugMsg), 
            "DwellIndicator: UpdateLayeredWindow result: %s\n",
            updateResult ? "SUCCESS" : "FAILED");
        OutputDebugStringA(debugMsg);
    }
    
    DeleteDC(memoryDC);
    ReleaseDC(NULL, screenDC);
    
    // Now show the window with clean, artifact-free display
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    m_isVisible = true;
    
    OutputDebugStringA("DwellIndicator: SHOW Complete - Clean display with no artifacts\n");
}

/**
 * @brief Update the progress of the countdown indicator
 * 
 * Updates the internal progress value and triggers a redraw.
 * Progress is clamped to [0.0, 1.0] range.
 * 
 * @param progress Progress value from 0.0 (start) to 1.0 (complete)
 */
void DwellIndicatorImpl::UpdateProgress(float progress)
{
    // Clamp progress to valid range [0.0, 1.0]
    if (progress < 0.0f) progress = 0.0f;
    if (progress > 1.0f) progress = 1.0f;
    
    // Log progress updates for debugging
    char debugMsg[256];
    sprintf_s(debugMsg, sizeof(debugMsg), 
        "DwellIndicator: UPDATE Progress %.3f -> %.3f - Window: %s, Visible: %s\n",
        m_progress, progress,
        m_hwnd ? "VALID" : "NULL",
        m_isVisible ? "TRUE" : "FALSE");
    OutputDebugStringA(debugMsg);
    
    float oldProgress = m_progress;
    m_progress = progress;
    
    // **GDI+ EXPERT FIX: Use UpdateLayeredWindow for artifact-free updates**
    if (m_hwnd && m_isVisible)
    {
        // Get window dimensions
        RECT rect;
        GetClientRect(m_hwnd, &rect);
        int windowSize = rect.right - rect.left;
        
        // Create memory DC and bitmap for off-screen rendering
        HDC screenDC = GetDC(NULL);
        HDC memoryDC = CreateCompatibleDC(screenDC);
        
        // Create 32-bit bitmap with alpha channel
        BITMAPINFO bmi = {};
        bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bmi.bmiHeader.biWidth = windowSize;
        bmi.bmiHeader.biHeight = -windowSize;  // Top-down DIB
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;         // 32-bit with alpha
        bmi.bmiHeader.biCompression = BI_RGB;
        
        void* pvBits = nullptr;
        HBITMAP hBitmap = CreateDIBSection(screenDC, &bmi, DIB_RGB_COLORS, &pvBits, NULL, 0);
        
        if (hBitmap && memoryDC)
        {
            HBITMAP oldBitmap = static_cast<HBITMAP>(SelectObject(memoryDC, hBitmap));
            
            // **CRITICAL: Clear entire bitmap to transparent**
            const size_t bitmapSizeBytes = static_cast<size_t>(windowSize) * static_cast<size_t>(windowSize) * 4ULL;
            memset(pvBits, 0, bitmapSizeBytes);
            
            // Create GDI+ Graphics object
            Graphics graphics(memoryDC);
            graphics.SetSmoothingMode(SmoothingModeAntiAlias);
            graphics.SetCompositingMode(CompositingModeSourceOver);
            graphics.SetCompositingQuality(CompositingQualityHighQuality);
            
            // **GDI+ EXPERT: Proper alpha channel clearing**
            graphics.Clear(Color(0, 0, 0, 0));
            
            // Calculate drawing parameters
            const float dpiScale = GetDpiScale();
            const float centerX = windowSize / 2.0f;
            const float centerY = windowSize / 2.0f;
            const float radius = kIndicatorRadius * dpiScale;
            const float strokeWidth = kStrokeWidth * dpiScale;
            
            // Get system colors
            DWORD accentColor = 0;
            BOOL isOpaque = FALSE;
            Color progressColor;
            Color backgroundCircleColor;
            
            if (SUCCEEDED(DwmGetColorizationColor(&accentColor, &isOpaque)))
            {
                const BYTE a = 255;
                const BYTE r = (accentColor >> 16) & 0xFF;
                const BYTE g = (accentColor >> 8) & 0xFF;
                const BYTE b = accentColor & 0xFF;
                progressColor = Color(a, r, g, b);
                
                const BYTE bgR = static_cast<BYTE>((r + 128) / 2);
                const BYTE bgG = static_cast<BYTE>((g + 128) / 2);
                const BYTE bgB = static_cast<BYTE>((b + 128) / 2);
                backgroundCircleColor = Color(80, bgR, bgG, bgB);
            }
            else
            {
                progressColor = Color(255, 0, 120, 215);
                backgroundCircleColor = Color(80, 160, 160, 160);
            }
            
            // Create bounding rectangle
            RectF ellipseRect(centerX - radius, centerY - radius, radius * 2, radius * 2);
            
            // Draw background circle
            Pen bgPen(backgroundCircleColor, strokeWidth * 0.6f);
            graphics.DrawEllipse(&bgPen, ellipseRect);
            
            // Draw progress arc if we have progress
            if (m_progress > 0.0f)
            {
                Pen progressPen(progressColor, strokeWidth);
                progressPen.SetStartCap(LineCapRound);
                progressPen.SetEndCap(LineCapRound);
                
                const float startAngle = -90.0f;  // 12 o'clock
                const float sweepAngle = m_progress * 360.0f;
                
                graphics.DrawArc(&progressPen, ellipseRect, startAngle, sweepAngle);
                
                sprintf_s(debugMsg, sizeof(debugMsg), 
                    "DwellIndicator: Drew arc - Progress %.3f, SweepAngle %.1f degrees\n",
                    m_progress, sweepAngle);
                OutputDebugStringA(debugMsg);
            }
            
            // **GDI+ EXPERT: Update layered window with new content**
            POINT ptSrc = {0, 0};
            POINT ptDst = {m_currentX - windowSize/2, m_currentY - windowSize/2};
            SIZE size = {windowSize, windowSize};
            BLENDFUNCTION blend = {};
            blend.BlendOp = AC_SRC_OVER;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = AC_SRC_ALPHA;
            
            BOOL updateResult = UpdateLayeredWindow(m_hwnd, screenDC, &ptDst, &size, 
                                                  memoryDC, &ptSrc, 0, &blend, ULW_ALPHA);
            
            // Cleanup
            SelectObject(memoryDC, oldBitmap);
            DeleteObject(hBitmap);
            
            sprintf_s(debugMsg, sizeof(debugMsg), 
                "DwellIndicator: UPDATE Complete - UpdateLayeredWindow: %s (%.3f->%.3f)\n",
                updateResult ? "SUCCESS" : "FAILED", oldProgress, progress);
            OutputDebugStringA(debugMsg);
        }
        
        DeleteDC(memoryDC);
        ReleaseDC(NULL, screenDC);
    }
    else
    {
        OutputDebugStringA("DwellIndicator: UPDATE Skipped - window not ready or not visible\n");
    }
}

/**
 * @brief Draw the circular progress indicator
 * 
 * **NOTE: This method is now primarily for fallback WM_PAINT handling**
 * The main rendering is done through UpdateLayeredWindow in Show() and UpdateProgress()
 * for artifact-free display on layered windows.
 * 
 * @param hdc Device context to draw into
 */
void DwellIndicatorImpl::DrawIndicator(HDC hdc)
{
    // Log drawing calls for debugging
    char debugMsg[256];
    sprintf_s(debugMsg, sizeof(debugMsg), 
        "DwellIndicator: DRAW (Fallback WM_PAINT) - Progress %.3f, Visible: %s\n",
        m_progress, m_isVisible ? "TRUE" : "FALSE");
    OutputDebugStringA(debugMsg);
    
    // **GDI+ EXPERT: For WM_PAINT on layered windows, we need special handling**
    // Generally, UpdateLayeredWindow bypasses WM_PAINT, but this provides fallback
    
    // Set up GDI+ graphics object with optimal settings for layered windows
    Graphics graphics(hdc);
    graphics.SetSmoothingMode(SmoothingModeAntiAlias);
    graphics.SetCompositingMode(CompositingModeSourceOver);
    graphics.SetCompositingQuality(CompositingQualityHighQuality);
    graphics.SetPixelOffsetMode(PixelOffsetModeHighQuality);

    // Get window client area dimensions
    RECT rect;
    GetClientRect(m_hwnd, &rect);
    const float centerX = (rect.right - rect.left) / 2.0f;
    const float centerY = (rect.bottom - rect.top) / 2.0f;

    // **GDI+ EXPERT: Proper clearing for layered windows**
    // Use Graphics::Clear instead of FillRectangle for proper alpha handling
    graphics.Clear(Color(0, 0, 0, 0));  // Fully transparent background

    // Apply DPI scaling for high-resolution displays
    const float dpiScale = GetDpiScale();
    const float radius = kIndicatorRadius * dpiScale;
    const float strokeWidth = kStrokeWidth * dpiScale;

    // Get system accent color for theming consistency
    DWORD accentColor = 0;
    BOOL isOpaque = FALSE;
    Color progressColor;
    Color backgroundCircleColor;
    
    if (SUCCEEDED(DwmGetColorizationColor(&accentColor, &isOpaque)))
    {
        // Extract RGB components from system accent color
        const BYTE a = 255;
        const BYTE r = (accentColor >> 16) & 0xFF;
        const BYTE g = (accentColor >> 8) & 0xFF;
        const BYTE b = accentColor & 0xFF;
        progressColor = Color(a, r, g, b);
        
        // Create subtle background color
        const BYTE bgR = static_cast<BYTE>((r + 128) / 2);
        const BYTE bgG = static_cast<BYTE>((g + 128) / 2);
        const BYTE bgB = static_cast<BYTE>((b + 128) / 2);
        backgroundCircleColor = Color(80, bgR, bgG, bgB);
    }
    else
    {
        // Fallback colors
        progressColor = Color(255, 0, 120, 215);
        backgroundCircleColor = Color(80, 160, 160, 160);
    }

    // Create bounding rectangle for the circle
    RectF ellipseRect(
        centerX - radius,
        centerY - radius,
        radius * 2,
        radius * 2
    );

    // Draw background circle
    Pen bgPen(backgroundCircleColor, strokeWidth * 0.6f);
    graphics.DrawEllipse(&bgPen, ellipseRect);

    // Draw progress arc only if we have measurable progress
    if (m_progress > 0.0f)
    {
        Pen progressPen(progressColor, strokeWidth);
        progressPen.SetStartCap(LineCapRound);
        progressPen.SetEndCap(LineCapRound);

        const float startAngle = -90.0f;  // 12 o'clock position
        const float sweepAngle = m_progress * 360.0f;

        graphics.DrawArc(&progressPen, ellipseRect, startAngle, sweepAngle);
        
        sprintf_s(debugMsg, sizeof(debugMsg), 
            "DwellIndicator: DRAW Arc (Fallback) - Progress %.3f, SweepAngle %.1f degrees\n",
            m_progress, sweepAngle);
        OutputDebugStringA(debugMsg);
    }
    else
    {
        OutputDebugStringA("DwellIndicator: DRAW (Fallback) - No arc (progress 0.0)\n");
    }
}

/**
 * @brief Hide the indicator window
 * 
 * Makes the window invisible but keeps it alive for potential re-showing.
 * Also resets the progress state to ensure clean restart on next show.
 */
void DwellIndicatorImpl::Hide()
{
    if (m_hwnd && m_isVisible)
    {
        char debugMsg[256];
        sprintf_s(debugMsg, sizeof(debugMsg), 
            "DwellIndicator: HIDE - Progress %.3f->0.0, Visible: %s->FALSE\n",
            m_progress, m_isVisible ? "TRUE" : "FALSE");
        OutputDebugStringA(debugMsg);
        
        // **GDI+ EXPERT FIX: Proper layered window hiding**
        // Clear the layered window content before hiding to prevent artifacts
        RECT rect;
        GetClientRect(m_hwnd, &rect);
        int windowSize = rect.right - rect.left;
        
        if (windowSize > 0)
        {
            HDC screenDC = GetDC(NULL);
            HDC memoryDC = CreateCompatibleDC(screenDC);
            
            // Create transparent bitmap
            BITMAPINFO bmi = {};
            bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = windowSize;
            bmi.bmiHeader.biHeight = -windowSize;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;
            
            void* pvBits = nullptr;
            HBITMAP hBitmap = CreateDIBSection(screenDC, &bmi, DIB_RGB_COLORS, &pvBits, NULL, 0);
            
            if (hBitmap && memoryDC)
            {
                HBITMAP oldBitmap = static_cast<HBITMAP>(SelectObject(memoryDC, hBitmap));
                
                // Clear to fully transparent
                // Fix for C26451: Use safe arithmetic to prevent overflow
                const size_t bitmapSizeBytes = static_cast<size_t>(windowSize) * static_cast<size_t>(windowSize) * 4ULL;
                memset(pvBits, 0, bitmapSizeBytes);
                
                // Update layered window with transparent content
                POINT ptSrc = {0, 0};
                POINT ptDst = {m_currentX - windowSize/2, m_currentY - windowSize/2};
                SIZE size = {windowSize, windowSize};
                BLENDFUNCTION blend = {};
                blend.BlendOp = AC_SRC_OVER;
                blend.SourceConstantAlpha = 0;  // Make completely transparent
                blend.AlphaFormat = AC_SRC_ALPHA;
                
                UpdateLayeredWindow(m_hwnd, screenDC, &ptDst, &size, 
                                  memoryDC, &ptSrc, 0, &blend, ULW_ALPHA);
                
                SelectObject(memoryDC, oldBitmap);
                DeleteObject(hBitmap);
            }
            
            DeleteDC(memoryDC);
            ReleaseDC(NULL, screenDC);
        }
        
        // Now hide the window
        ShowWindow(m_hwnd, SW_HIDE);
        m_isVisible = false;
        
        // **CRITICAL: Reset progress when hiding to ensure clean state for next show**
        m_progress = 0.0f;
        
        OutputDebugStringA("DwellIndicator: HIDE Complete - Layered window cleared and hidden\n");
    }
    else
    {
        OutputDebugStringA("DwellIndicator: HIDE Skipped - already hidden or invalid window\n");
    }
}

/**
 * @brief Clean up all indicator resources
 * 
 * Hides and destroys the window, shuts down GDI+.
 * Called during module shutdown or when indicator is no longer needed.
 */
void DwellIndicatorImpl::Cleanup()
{
    Hide();  // Hide window first
    
    // Destroy the window and clean up Windows resources
    if (m_hwnd)
    {
        DestroyWindow(m_hwnd);
        m_hwnd = NULL;
    }
    
    // Shutdown GDI+ graphics system
    if (m_gdiplusToken != 0)
    {
        GdiplusShutdown(m_gdiplusToken);
        m_gdiplusToken = 0;
    }
}

/**
 * @brief Get DPI scaling factor for the current display
 * 
 * @return DPI scale factor (1.0 = 96 DPI, 1.25 = 120 DPI, etc.)
 */
float DwellIndicatorImpl::GetDpiScale() const
{
    if (!m_hwnd) return 1.0f;  // Default scale if no window
    return static_cast<float>(GetDpiForWindow(m_hwnd)) / 96.0f;
}

// ============================================================================
// DwellIndicator Public Interface Implementation
// ============================================================================

/**
 * @brief Constructor - creates the implementation instance
 */
DwellIndicator::DwellIndicator() : m_impl(std::make_unique<DwellIndicatorImpl>())
{
}

/**
 * @brief Destructor - ensures cleanup of resources
 */
DwellIndicator::~DwellIndicator()
{
    if (m_impl)
    {
        m_impl->Cleanup();
    }
}

/**
 * @brief Initialize the indicator system
 * @return true if successful, false on failure
 */
bool DwellIndicator::Initialize()
{
    return m_impl ? m_impl->Initialize() : false;
}

/**
 * @brief Show indicator at cursor position
 * @param x Cursor X coordinate
 * @param y Cursor Y coordinate
 */
void DwellIndicator::Show(int x, int y)
{
    if (m_impl) m_impl->Show(x, y);
}

/**
 * @brief Update countdown progress
 * @param progress Progress from 0.0 to 1.0
 */
void DwellIndicator::UpdateProgress(float progress)
{
    if (m_impl) m_impl->UpdateProgress(progress);
}

/**
 * @brief Hide the indicator
 */
void DwellIndicator::Hide()
{
    if (m_impl) m_impl->Hide();
}

/**
 * @brief Clean up all resources
 */
void DwellIndicator::Cleanup()
{
    if (m_impl) m_impl->Cleanup();
}