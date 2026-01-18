//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Panoramic screenshot capture and stitching implementation
//
//==============================================================================
#include "pch.h"
#include "PanoramaCapture.h"
#include "Utility.h"
#include <algorithm>
#include <cmath>

// Static member initialization
const double PanoramaCapture::MATCH_THRESHOLD = 0.30;  // Low threshold to detect more scrolls
const double PanoramaCapture::DIFFERENCE_THRESHOLD = 0.02;
const wchar_t* PanoramaCapture::OVERLAY_CLASS_NAME = L"ZoomItPanoramaOverlay";
PanoramaCapture* PanoramaCapture::s_instance = nullptr;

//----------------------------------------------------------------------------
// Constructor
//----------------------------------------------------------------------------
PanoramaCapture::PanoramaCapture()
    : m_capturing(false)
    , m_captureRect{}
    , m_ownerWindow(nullptr)
    , m_previousWidth(0)
    , m_previousHeight(0)
    , m_totalOffsetX(0)
    , m_totalOffsetY(0)
    , m_timerID(0)
    , m_keyboardHook(nullptr)
{
    s_instance = this;
}

//----------------------------------------------------------------------------
// Destructor
//----------------------------------------------------------------------------
PanoramaCapture::~PanoramaCapture()
{
    Cancel();
    if (s_instance == this)
    {
        s_instance = nullptr;
    }
}

//----------------------------------------------------------------------------
// Start panorama capture
//----------------------------------------------------------------------------
bool PanoramaCapture::Start(HWND ownerWindow, const RECT& captureRect)
{
    if (m_capturing)
    {
        return false;
    }

    m_ownerWindow = ownerWindow;
    m_captureRect = captureRect;
    m_frames.clear();
    m_previousFrame.clear();
    m_totalOffsetX = 0;
    m_totalOffsetY = 0;

    // Create the overlay window to show capture region
    if (!CreateOverlayWindow(ownerWindow))
    {
        return false;
    }

    // Install keyboard hook to detect ESC key
    if (!InstallKeyboardHook())
    {
        m_overlayWindow.reset();
        return false;
    }

    m_capturing = true;

    // Capture the initial frame
    CaptureFrame();

    // Start periodic capture timer
    m_timerID = SetTimer(m_overlayWindow.get(), 1, CAPTURE_INTERVAL_MS, TimerCallback);

    return true;
}

//----------------------------------------------------------------------------
// Stop capture and return stitched result
//----------------------------------------------------------------------------
HBITMAP PanoramaCapture::Stop()
{
    OutputDebugStringW(L"[PanoramaCapture] Stop() called\n");

    if (!m_capturing)
    {
        OutputDebugStringW(L"[PanoramaCapture] Stop(): not capturing, returning null\n");
        return nullptr;
    }

    // Stop the timer
    if (m_timerID != 0)
    {
        KillTimer(m_overlayWindow.get(), m_timerID);
        m_timerID = 0;
    }

    // Remove keyboard hook
    RemoveKeyboardHook();

    m_capturing = false;

    // Capture final frame
    CaptureFrame();

    // Destroy overlay window
    m_overlayWindow.reset();

    WCHAR msg[256];
    swprintf_s(msg, L"[PanoramaCapture] Stop(): %zu frames captured, stitching...\n", m_frames.size());
    OutputDebugStringW(msg);

    // Stitch all frames together
    HBITMAP result = StitchFrames();

    if (result)
    {
        OutputDebugStringW(L"[PanoramaCapture] Stop(): StitchFrames succeeded\n");
    }
    else
    {
        OutputDebugStringW(L"[PanoramaCapture] Stop(): StitchFrames returned NULL\n");
    }

    // Clean up
    m_frames.clear();
    m_previousFrame.clear();

    return result;
}

//----------------------------------------------------------------------------
// Cancel capture
//----------------------------------------------------------------------------
void PanoramaCapture::Cancel()
{
    if (m_timerID != 0)
    {
        KillTimer(m_overlayWindow.get(), m_timerID);
        m_timerID = 0;
    }

    // Remove keyboard hook
    RemoveKeyboardHook();

    m_capturing = false;
    m_overlayWindow.reset();
    m_frames.clear();
    m_previousFrame.clear();
}

//----------------------------------------------------------------------------
// Install low-level keyboard hook for ESC detection
//----------------------------------------------------------------------------
bool PanoramaCapture::InstallKeyboardHook()
{
    if (m_keyboardHook != nullptr)
    {
        OutputDebugStringW(L"[PanoramaCapture] Keyboard hook already installed\n");
        return true; // Already installed
    }

    m_keyboardHook = SetWindowsHookEx(
        WH_KEYBOARD_LL,
        KeyboardHookProc,
        GetModuleHandle(nullptr),
        0
    );

    if (m_keyboardHook != nullptr)
    {
        OutputDebugStringW(L"[PanoramaCapture] Keyboard hook installed successfully\n");
    }
    else
    {
        WCHAR msg[256];
        swprintf_s(msg, L"[PanoramaCapture] Failed to install keyboard hook, error=%d\n", GetLastError());
        OutputDebugStringW(msg);
    }

    return (m_keyboardHook != nullptr);
}

//----------------------------------------------------------------------------
// Remove keyboard hook
//----------------------------------------------------------------------------
void PanoramaCapture::RemoveKeyboardHook()
{
    if (m_keyboardHook != nullptr)
    {
        UnhookWindowsHookEx(m_keyboardHook);
        m_keyboardHook = nullptr;
    }
}

//----------------------------------------------------------------------------
// Keyboard hook callback - detect ESC to stop capture
//----------------------------------------------------------------------------
LRESULT CALLBACK PanoramaCapture::KeyboardHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    OutputDebugStringW(L"[PanoramaCapture] KeyboardHookProc called\n");

    if (nCode >= 0 && s_instance != nullptr && s_instance->m_capturing)
    {
        KBDLLHOOKSTRUCT* pKbStruct = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        WCHAR msg[256];
        swprintf_s(msg, L"[PanoramaCapture] Key event: wParam=0x%X, vkCode=0x%X\n", (UINT)wParam, pKbStruct->vkCode);
        OutputDebugStringW(msg);

        if (wParam == WM_KEYDOWN && pKbStruct->vkCode == VK_ESCAPE)
        {
            OutputDebugStringW(L"[PanoramaCapture] ESC detected, posting stop message\n");
            // Post message to owner window to stop capture
            if (s_instance->m_ownerWindow != nullptr)
            {
                PostMessage(s_instance->m_ownerWindow, WM_USER_PANORAMA_STOP, 0, 0);
            }
            return 1; // Consume the ESC key
        }
    }
    else
    {
        WCHAR msg[256];
        swprintf_s(msg, L"[PanoramaCapture] Hook skipped: nCode=%d, s_instance=%p, capturing=%d\n",
            nCode, s_instance, s_instance ? (int)s_instance->m_capturing : -1);
        OutputDebugStringW(msg);
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

//----------------------------------------------------------------------------
// Capture a single frame
//----------------------------------------------------------------------------
void PanoramaCapture::CaptureFrame()
{
    if (!m_capturing)
    {
        return;
    }

    int width = 0, height = 0;
    auto frameData = CaptureScreenRegion(m_captureRect, width, height);

    if (frameData.empty())
    {
        return;
    }

    // If this is the first frame, just store it
    if (m_previousFrame.empty())
    {
        OutputDebugStringW(L"[PanoramaCapture] First frame captured\n");
        PanoramaFrame frame;
        frame.pixels = std::move(frameData);
        frame.width = width;
        frame.height = height;
        frame.relativeX = 0;
        frame.relativeY = 0;
        frame.timestamp = GetTickCount64();

        m_previousFrame = frame.pixels;
        m_previousWidth = width;
        m_previousHeight = height;
        m_frames.push_back(std::move(frame));
        return;
    }

    // Check if the frame has changed enough to warrant analysis
    if (!FramesAreDifferent(m_previousFrame, frameData, width, height))
    {
        return;  // Frame hasn't changed significantly
    }

    // Detect scroll offset between previous and current frame
    ScrollOffset offset = DetectScrollOffset(m_previousFrame, frameData, width, height);

    WCHAR msg[256];
    swprintf_s(msg, L"[PanoramaCapture] DetectScrollOffset: dx=%d, dy=%d, confidence=%.3f, valid=%d\n",
        offset.dx, offset.dy, offset.confidence, offset.valid ? 1 : 0);
    OutputDebugStringW(msg);

    if (offset.valid && (abs(offset.dy) >= MIN_SCROLL_THRESHOLD || abs(offset.dx) >= MIN_SCROLL_THRESHOLD))
    {
        swprintf_s(msg, L"[PanoramaCapture] SCROLL DETECTED! Adding frame #%zu, totalOffset=(%d, %d)\n",
            m_frames.size() + 1, m_totalOffsetX + offset.dx, m_totalOffsetY + offset.dy);
        OutputDebugStringW(msg);

        // Update accumulated offset
        m_totalOffsetX += offset.dx;
        m_totalOffsetY += offset.dy;

        // Create new frame entry
        PanoramaFrame frame;
        frame.pixels = frameData;  // Copy the frame data
        frame.width = width;
        frame.height = height;
        frame.relativeX = m_totalOffsetX;
        frame.relativeY = m_totalOffsetY;
        frame.timestamp = GetTickCount64();

        m_frames.push_back(std::move(frame));

        // Update previous frame for next comparison
        m_previousFrame = frameData;  // Copy, don't move
        m_previousWidth = width;
        m_previousHeight = height;
    }
    else
    {
        // Even if not a valid scroll, update previous frame to track content changes
        m_previousFrame = frameData;
        m_previousWidth = width;
        m_previousHeight = height;
    }
}

//----------------------------------------------------------------------------
// Capture screen region to byte array (BGRA format)
//----------------------------------------------------------------------------
std::vector<BYTE> PanoramaCapture::CaptureScreenRegion(const RECT& rect, int& outWidth, int& outHeight)
{
    outWidth = rect.right - rect.left;
    outHeight = rect.bottom - rect.top;

    if (outWidth <= 0 || outHeight <= 0)
    {
        return {};
    }

    HDC hdcScreen = GetDC(nullptr);
    if (!hdcScreen)
    {
        return {};
    }

    HDC hdcMem = CreateCompatibleDC(hdcScreen);
    if (!hdcMem)
    {
        ReleaseDC(nullptr, hdcScreen);
        return {};
    }

    // Create DIB section for direct pixel access
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(bmi.bmiHeader);
    bmi.bmiHeader.biWidth = outWidth;
    bmi.bmiHeader.biHeight = -outHeight;  // Top-down DIB
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* pBits = nullptr;
    HBITMAP hBitmap = CreateDIBSection(hdcMem, &bmi, DIB_RGB_COLORS, &pBits, nullptr, 0);

    if (!hBitmap || !pBits)
    {
        DeleteDC(hdcMem);
        ReleaseDC(nullptr, hdcScreen);
        return {};
    }

    HGDIOBJ hOldBitmap = SelectObject(hdcMem, hBitmap);

    // Capture the screen region
    BitBlt(hdcMem, 0, 0, outWidth, outHeight,
           hdcScreen, rect.left, rect.top, SRCCOPY | CAPTUREBLT);

    // Copy pixels to vector
    size_t dataSize = static_cast<size_t>(outWidth) * outHeight * 4;
    std::vector<BYTE> result(dataSize);
    memcpy(result.data(), pBits, dataSize);

    // Cleanup
    SelectObject(hdcMem, hOldBitmap);
    DeleteObject(hBitmap);
    DeleteDC(hdcMem);
    ReleaseDC(nullptr, hdcScreen);

    return result;
}

//----------------------------------------------------------------------------
// Check if two frames are significantly different
//----------------------------------------------------------------------------
bool PanoramaCapture::FramesAreDifferent(
    const std::vector<BYTE>& frame1,
    const std::vector<BYTE>& frame2,
    int width, int height)
{
    if (frame1.size() != frame2.size())
    {
        return true;
    }

    // Sample pixels to check for differences (checking every pixel is too slow)
    const int SAMPLE_STEP = 10;
    int differentPixels = 0;
    int totalSamples = 0;

    for (int y = 0; y < height; y += SAMPLE_STEP)
    {
        for (int x = 0; x < width; x += SAMPLE_STEP)
        {
            size_t idx = (static_cast<size_t>(y) * width + x) * 4;
            if (idx + 3 < frame1.size())
            {
                // Compare RGB values (ignore alpha)
                int diffB = abs(static_cast<int>(frame1[idx]) - static_cast<int>(frame2[idx]));
                int diffG = abs(static_cast<int>(frame1[idx + 1]) - static_cast<int>(frame2[idx + 1]));
                int diffR = abs(static_cast<int>(frame1[idx + 2]) - static_cast<int>(frame2[idx + 2]));

                if (diffB > 10 || diffG > 10 || diffR > 10)
                {
                    differentPixels++;
                }
                totalSamples++;
            }
        }
    }

    if (totalSamples == 0)
    {
        return false;
    }

    double diffRatio = static_cast<double>(differentPixels) / totalSamples;
    return diffRatio > DIFFERENCE_THRESHOLD;
}

//----------------------------------------------------------------------------
// Detect scroll offset using large block matching from center of frame
//----------------------------------------------------------------------------
ScrollOffset PanoramaCapture::DetectScrollOffset(
    const std::vector<BYTE>& frame1,
    const std::vector<BYTE>& frame2,
    int width, int height)
{
    ScrollOffset result = { 0, 0, 0.0, false };

    if (frame1.empty() || frame2.empty() || width <= 0 || height <= 0)
    {
        return result;
    }

    // SIMPLE AND ROBUST SCROLL DETECTION:
    // Take a large block from the CENTER of frame1
    // Search for it in frame2 at different Y offsets
    // The Y displacement tells us scroll amount
    //
    // Key insight: Use LARGE block to avoid false matches
    // Only search in CENTER X region (avoid scrollbars on sides)

    // Scale parameters based on window size
    // For small windows, use smaller margins and block size
    const int marginX = max(10, min(60, width / 10));   // 10% of width, clamped 10-60
    const int marginY = max(10, min(50, height / 10));  // 10% of height, clamped 10-50
    const int blockHeight = max(20, min(120, height / 4));  // 25% of height, clamped 20-120
    const int searchStep = 2;  // Fine search step

    // Block position in frame1 - center of frame
    int block1Y = (height - blockHeight) / 2;
    int blockStartX = marginX;
    int blockEndX = width - marginX;
    int blockWidth = blockEndX - blockStartX;

    // Check if we have enough space to work with
    if (blockWidth < 20 || blockHeight < 20)
    {
        OutputDebugStringA("[PanoramaCapture] Window too small for scroll detection\n");
        return result;
    }

    // Check if there's room to search
    int searchStart = marginY;
    int searchEnd = height - blockHeight - marginY;
    if (searchEnd <= searchStart)
    {
        OutputDebugStringA("[PanoramaCapture] Not enough vertical space to search\n");
        return result;
    }

    // Search for this block in frame2 at different Y positions
    double bestSad = 1e30;
    double secondBestSad = 1e30;
    int bestMatchY = block1Y;
    int searchPositions = 0;

    for (int searchY = searchStart; searchY < searchEnd; searchY += searchStep)
    {
        double sad = 0.0;
        int pixelCount = 0;

        // Compare the blocks - sample every 2nd or 3rd pixel for speed
        int sampleStep = max(2, min(3, blockWidth / 50));
        for (int y = 0; y < blockHeight; y += sampleStep)
        {
            for (int x = blockStartX; x < blockEndX; x += sampleStep)
            {
                size_t idx1 = (static_cast<size_t>(block1Y + y) * width + x) * 4;
                size_t idx2 = (static_cast<size_t>(searchY + y) * width + x) * 4;

                if (idx1 + 2 < frame1.size() && idx2 + 2 < frame2.size())
                {
                    int diffR = abs(static_cast<int>(frame1[idx1 + 2]) - static_cast<int>(frame2[idx2 + 2]));
                    int diffG = abs(static_cast<int>(frame1[idx1 + 1]) - static_cast<int>(frame2[idx2 + 1]));
                    int diffB = abs(static_cast<int>(frame1[idx1]) - static_cast<int>(frame2[idx2]));

                    sad += diffR + diffG + diffB;
                    pixelCount++;
                }
            }
        }

        if (pixelCount > 0)
        {
            double avgSad = sad / pixelCount;
            searchPositions++;

            if (avgSad < bestSad)
            {
                secondBestSad = bestSad;
                bestSad = avgSad;
                bestMatchY = searchY;
            }
            else if (avgSad < secondBestSad)
            {
                secondBestSad = avgSad;
            }
        }
    }

    // Need at least 2 search positions to compare
    if (searchPositions < 2)
    {
        OutputDebugStringA("[PanoramaCapture] Not enough search positions\n");
        return result;
    }

    // Calculate scroll amount
    int dy = block1Y - bestMatchY;

    // Compute confidence based on how much better best match is than second best
    double confidence = 0.0;
    if (secondBestSad > 0.001 && bestSad < 1e29)
    {
        double ratio = secondBestSad / (bestSad + 0.001);
        confidence = min(1.0, (ratio - 1.0) * 2.0);
    }

    // Validation:
    // 1. Best SAD must be low (good match quality)
    // 2. Either: second best is significantly worse (ratio test)
    //    OR: best SAD is very low (< 5) indicating excellent match
    // 3. Must have scrolled at least MIN_SCROLL_THRESHOLD pixels
    bool goodMatch = (bestSad < 15.0);
    bool clearWinner = (secondBestSad > bestSad * 1.15) || (bestSad < 5.0);
    bool significantScroll = (abs(dy) >= MIN_SCROLL_THRESHOLD);

    bool isValid = goodMatch && clearWinner && significantScroll;

    result.dx = 0;
    result.dy = dy;
    result.confidence = confidence;
    result.valid = isValid;

    // Debug output
    OutputDebugStringA(("[PanoramaCapture] Block match: bestSad=" +
                       std::to_string(static_cast<int>(bestSad)) +
                       ", 2ndSad=" + std::to_string(static_cast<int>(secondBestSad)) +
                       ", dy=" + std::to_string(dy) +
                       ", valid=" + std::to_string(isValid ? 1 : 0) + "\n").c_str());

    return result;
}

//----------------------------------------------------------------------------
// Compute normalized cross-correlation between two image regions
//----------------------------------------------------------------------------
double PanoramaCapture::ComputeNCC(
    const BYTE* img1, const BYTE* img2,
    int width, int height, int stride,
    int img1OffsetX, int img1OffsetY,
    int img2OffsetX, int img2OffsetY,
    int compareWidth, int compareHeight)
{
    // NCC formula:
    // NCC = sum((I1 - mean1) * (I2 - mean2)) / (n * std1 * std2)
    // OPTIMIZATION: Sample every 4th pixel to speed up

    if (compareWidth <= 0 || compareHeight <= 0)
    {
        return -1.0;
    }

    const int SAMPLE_STEP = 4;  // Sample every 4th pixel for speed

    // First pass: compute means
    double sum1 = 0.0, sum2 = 0.0;
    int count = 0;

    for (int y = 0; y < compareHeight; y += SAMPLE_STEP)
    {
        for (int x = 0; x < compareWidth; x += SAMPLE_STEP)
        {
            int idx1 = ((img1OffsetY + y) * width + (img1OffsetX + x)) * 4;
            int idx2 = ((img2OffsetY + y) * width + (img2OffsetX + x)) * 4;

            // Use grayscale intensity (weighted RGB)
            double val1 = 0.299 * img1[idx1 + 2] + 0.587 * img1[idx1 + 1] + 0.114 * img1[idx1];
            double val2 = 0.299 * img2[idx2 + 2] + 0.587 * img2[idx2 + 1] + 0.114 * img2[idx2];

            sum1 += val1;
            sum2 += val2;
            count++;
        }
    }

    if (count == 0)
    {
        return -1.0;
    }

    double mean1 = sum1 / count;
    double mean2 = sum2 / count;

    // Second pass: compute NCC
    double sumProduct = 0.0;
    double sumSq1 = 0.0;
    double sumSq2 = 0.0;

    for (int y = 0; y < compareHeight; y += SAMPLE_STEP)
    {
        for (int x = 0; x < compareWidth; x += SAMPLE_STEP)
        {
            int idx1 = ((img1OffsetY + y) * width + (img1OffsetX + x)) * 4;
            int idx2 = ((img2OffsetY + y) * width + (img2OffsetX + x)) * 4;

            double val1 = 0.299 * img1[idx1 + 2] + 0.587 * img1[idx1 + 1] + 0.114 * img1[idx1];
            double val2 = 0.299 * img2[idx2 + 2] + 0.587 * img2[idx2 + 1] + 0.114 * img2[idx2];

            double diff1 = val1 - mean1;
            double diff2 = val2 - mean2;

            sumProduct += diff1 * diff2;
            sumSq1 += diff1 * diff1;
            sumSq2 += diff2 * diff2;
        }
    }

    double std1 = sqrt(sumSq1 / count);
    double std2 = sqrt(sumSq2 / count);

    if (std1 < 1.0 || std2 < 1.0)
    {
        // Very low variance - likely uniform region, not reliable
        return -1.0;
    }

    double ncc = sumProduct / (count * std1 * std2);
    return ncc;
}

//----------------------------------------------------------------------------
// Stitch all captured frames into final panorama
//----------------------------------------------------------------------------
HBITMAP PanoramaCapture::StitchFrames()
{
    if (m_frames.empty())
    {
        return nullptr;
    }

    if (m_frames.size() == 1)
    {
        // Only one frame - just return it as bitmap
        const auto& frame = m_frames[0];

        // Create a device-compatible bitmap for proper clipboard support
        HDC hdcScreen = GetDC(nullptr);
        HDC hdcMem = CreateCompatibleDC(hdcScreen);
        HBITMAP hBitmap = CreateCompatibleBitmap(hdcScreen, frame.width, frame.height);
        HBITMAP hOldBitmap = (HBITMAP)SelectObject(hdcMem, hBitmap);

        // Create temp DIB to copy our pixel data
        BITMAPINFO bmi = {};
        bmi.bmiHeader.biSize = sizeof(bmi.bmiHeader);
        bmi.bmiHeader.biWidth = frame.width;
        bmi.bmiHeader.biHeight = -frame.height;  // Top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = BI_RGB;

        SetDIBitsToDevice(hdcMem, 0, 0, frame.width, frame.height,
            0, 0, 0, frame.height, frame.pixels.data(), &bmi, DIB_RGB_COLORS);

        SelectObject(hdcMem, hOldBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(nullptr, hdcScreen);

        return hBitmap;
    }

    // Calculate total canvas size from all frame offsets
    int minX = 0, maxX = 0, minY = 0, maxY = 0;

    for (const auto& frame : m_frames)
    {
        minX = min(minX, frame.relativeX);
        maxX = max(maxX, frame.relativeX + frame.width);
        minY = min(minY, frame.relativeY);
        maxY = max(maxY, frame.relativeY + frame.height);
    }

    int canvasWidth = maxX - minX;
    int canvasHeight = maxY - minY;

    // Sanity check - don't create unreasonably large bitmaps
    const int MAX_DIMENSION = 32000;
    if (canvasWidth > MAX_DIMENSION || canvasHeight > MAX_DIMENSION ||
        canvasWidth <= 0 || canvasHeight <= 0)
    {
        // Fall back to just returning the last frame
        const auto& frame = m_frames.back();

        BITMAPINFO bmi = {};
        bmi.bmiHeader.biSize = sizeof(bmi.bmiHeader);
        bmi.bmiHeader.biWidth = frame.width;
        bmi.bmiHeader.biHeight = -frame.height;
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;

        void* pBits = nullptr;
        HBITMAP hBitmap = CreateDIBSection(nullptr, &bmi, DIB_RGB_COLORS, &pBits, nullptr, 0);
        if (hBitmap && pBits)
        {
            memcpy(pBits, frame.pixels.data(), frame.pixels.size());
        }
        return hBitmap;
    }

    // Create canvas bitmap
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(bmi.bmiHeader);
    bmi.bmiHeader.biWidth = canvasWidth;
    bmi.bmiHeader.biHeight = -canvasHeight;  // Top-down
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* canvasBits = nullptr;
    HBITMAP hCanvas = CreateDIBSection(nullptr, &bmi, DIB_RGB_COLORS, &canvasBits, nullptr, 0);

    if (!hCanvas || !canvasBits)
    {
        return nullptr;
    }

    // Initialize canvas to black
    memset(canvasBits, 0, static_cast<size_t>(canvasWidth) * canvasHeight * 4);

    // Blend frames onto canvas (process in order for correct layering)
    // Track the bottom edge of content already placed
    int previousFrameBottom = 0;

    for (const auto& frame : m_frames)
    {
        int destX = frame.relativeX - minX;
        int destY = frame.relativeY - minY;

        BlendFrameOntoCanvas(
            static_cast<BYTE*>(canvasBits), canvasWidth, canvasHeight,
            frame.pixels.data(), frame.width, frame.height,
            destX, destY, previousFrameBottom);

        // Update the bottom edge after placing this frame
        previousFrameBottom = max(previousFrameBottom, destY + frame.height);
    }

    WCHAR msg[256];
    swprintf_s(msg, L"[PanoramaCapture] StitchFrames: canvas=%dx%d, frames=%zu\n",
        canvasWidth, canvasHeight, m_frames.size());
    OutputDebugStringW(msg);

    // Convert DIB to device-compatible bitmap for clipboard support
    HDC hdcScreen = GetDC(nullptr);
    HDC hdcMem = CreateCompatibleDC(hdcScreen);
    HBITMAP hResult = CreateCompatibleBitmap(hdcScreen, canvasWidth, canvasHeight);
    HBITMAP hOldBitmap = (HBITMAP)SelectObject(hdcMem, hResult);

    // Copy from DIB section to compatible bitmap
    HDC hdcCanvas = CreateCompatibleDC(hdcScreen);
    SelectObject(hdcCanvas, hCanvas);
    BitBlt(hdcMem, 0, 0, canvasWidth, canvasHeight, hdcCanvas, 0, 0, SRCCOPY);

    SelectObject(hdcMem, hOldBitmap);
    DeleteDC(hdcCanvas);
    DeleteDC(hdcMem);
    ReleaseDC(nullptr, hdcScreen);

    // Delete the intermediate DIB section
    DeleteObject(hCanvas);

    return hResult;
}

//----------------------------------------------------------------------------
// Blend a frame onto the canvas - only copy new (non-overlapping) content
//----------------------------------------------------------------------------
void PanoramaCapture::BlendFrameOntoCanvas(
    BYTE* canvas, int canvasWidth, int canvasHeight,
    const BYTE* frame, int frameWidth, int frameHeight,
    int destX, int destY, int previousFrameBottom)
{
    // Calculate the visible region
    int srcStartX = max(0, -destX);
    int srcEndX = min(frameWidth, canvasWidth - destX);

    // For Y, we only want to copy the NEW content
    // If this frame overlaps with previous content, skip the overlapping part
    int srcStartY = 0;
    int srcEndY = frameHeight;

    // If destY < previousFrameBottom, there's overlap at the top of this frame
    // We should only copy from where the new content starts
    if (destY < previousFrameBottom && destY >= 0)
    {
        // The overlap extends from destY to previousFrameBottom
        // In frame coordinates, that's from 0 to (previousFrameBottom - destY)
        int overlapHeight = previousFrameBottom - destY;
        srcStartY = overlapHeight;  // Start copying from after the overlap
    }

    if (srcStartX >= srcEndX || srcStartY >= srcEndY)
    {
        return;  // Nothing to copy
    }

    // Copy only the new (non-overlapping) content
    for (int y = srcStartY; y < srcEndY; y++)
    {
        int canvasY = destY + y;
        if (canvasY < 0 || canvasY >= canvasHeight) continue;

        for (int x = srcStartX; x < srcEndX; x++)
        {
            int canvasX = destX + x;
            if (canvasX < 0 || canvasX >= canvasWidth) continue;

            size_t srcIdx = (static_cast<size_t>(y) * frameWidth + x) * 4;
            size_t dstIdx = (static_cast<size_t>(canvasY) * canvasWidth + canvasX) * 4;

            // Copy BGRA pixels
            canvas[dstIdx] = frame[srcIdx];
            canvas[dstIdx + 1] = frame[srcIdx + 1];
            canvas[dstIdx + 2] = frame[srcIdx + 2];
            canvas[dstIdx + 3] = frame[srcIdx + 3];
        }
    }
}

//----------------------------------------------------------------------------
// Create overlay window to show capture region
//----------------------------------------------------------------------------
bool PanoramaCapture::CreateOverlayWindow(HWND ownerWindow)
{
    // Register window class
    WNDCLASSW wc = {};
    wc.lpfnWndProc = OverlayWndProc;
    wc.hInstance = GetModuleHandle(nullptr);
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.lpszClassName = OVERLAY_CLASS_NAME;

    if (RegisterClassW(&wc) == 0)
    {
        if (GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        {
            return false;
        }
    }

    // Calculate window size (slightly larger than capture rect for border)
    int borderWidth = 3;
    int windowX = m_captureRect.left - borderWidth;
    int windowY = m_captureRect.top - borderWidth;
    int windowWidth = (m_captureRect.right - m_captureRect.left) + borderWidth * 2;
    int windowHeight = (m_captureRect.bottom - m_captureRect.top) + borderWidth * 2;

    // Create layered, topmost, transparent window
    m_overlayWindow.reset(CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW,
        OVERLAY_CLASS_NAME,
        L"Panorama Capture",
        WS_POPUP,
        windowX, windowY, windowWidth, windowHeight,
        ownerWindow,
        nullptr,
        GetModuleHandle(nullptr),
        this));

    if (!m_overlayWindow)
    {
        return false;
    }

    // Set window to be click-through
    SetLayeredWindowAttributes(m_overlayWindow.get(), 0, 200, LWA_ALPHA);

    // Exclude from capture
    SetWindowDisplayAffinity(m_overlayWindow.get(), WDA_EXCLUDEFROMCAPTURE);

    // Show the window
    ShowWindow(m_overlayWindow.get(), SW_SHOWNA);

    return true;
}

//----------------------------------------------------------------------------
// Overlay window procedure (static)
//----------------------------------------------------------------------------
LRESULT CALLBACK PanoramaCapture::OverlayWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == WM_NCCREATE)
    {
        auto cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(cs->lpCreateParams));
        return TRUE;
    }

    auto self = reinterpret_cast<PanoramaCapture*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    if (self)
    {
        return self->HandleOverlayMessage(hwnd, msg, wParam, lParam);
    }

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

//----------------------------------------------------------------------------
// Handle overlay window messages
//----------------------------------------------------------------------------
LRESULT PanoramaCapture::HandleOverlayMessage(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);

        RECT clientRect;
        GetClientRect(hwnd, &clientRect);

        // Draw orange/yellow border to indicate recording
        HPEN hPen = CreatePen(PS_SOLID, 3, RGB(255, 165, 0));  // Orange
        HPEN hOldPen = static_cast<HPEN>(SelectObject(hdc, hPen));
        HBRUSH hOldBrush = static_cast<HBRUSH>(SelectObject(hdc, GetStockObject(NULL_BRUSH)));

        Rectangle(hdc, clientRect.left, clientRect.top, clientRect.right, clientRect.bottom);

        // Draw inner border
        HPEN hPen2 = CreatePen(PS_SOLID, 1, RGB(255, 222, 0));  // Yellow
        SelectObject(hdc, hPen2);
        InflateRect(&clientRect, -3, -3);
        Rectangle(hdc, clientRect.left, clientRect.top, clientRect.right, clientRect.bottom);

        SelectObject(hdc, hOldPen);
        SelectObject(hdc, hOldBrush);
        DeleteObject(hPen);
        DeleteObject(hPen2);

        EndPaint(hwnd, &ps);
        return 0;
    }

    case WM_NCHITTEST:
        return HTTRANSPARENT;  // Make window click-through

    case WM_TIMER:
        if (s_instance && s_instance->m_capturing)
        {
            s_instance->CaptureFrame();
            InvalidateRect(hwnd, nullptr, FALSE);  // Redraw border
        }
        return 0;
    }

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

//----------------------------------------------------------------------------
// Timer callback for periodic capture
//----------------------------------------------------------------------------
void CALLBACK PanoramaCapture::TimerCallback(HWND hwnd, UINT msg, UINT_PTR idTimer, DWORD dwTime)
{
    UNREFERENCED_PARAMETER(hwnd);
    UNREFERENCED_PARAMETER(msg);
    UNREFERENCED_PARAMETER(idTimer);
    UNREFERENCED_PARAMETER(dwTime);

    if (s_instance && s_instance->m_capturing)
    {
        // Check for ESC key using GetAsyncKeyState
        SHORT escState = GetAsyncKeyState(VK_ESCAPE);

        if (escState & 0x8000)
        {
            OutputDebugStringW(L"[PanoramaCapture] ESC PRESSED via GetAsyncKeyState! Posting stop message\n");
            if (s_instance->m_ownerWindow != nullptr)
            {
                PostMessage(s_instance->m_ownerWindow, WM_USER_PANORAMA_STOP, 0, 0);
            }
            return;
        }

        s_instance->CaptureFrame();
    }
}
