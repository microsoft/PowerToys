//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Panoramic screenshot capture and stitching
//
//==============================================================================
#pragma once

#include <windows.h>
#include <vector>
#include <atomic>
#include <memory>

// WIL for unique handles
#include <wil/resource.h>

// Message to signal panorama capture stop (must match ZoomIt.h)
#define WM_USER_PANORAMA_STOP   WM_USER+500

// Forward declarations
struct ID3D11Device;
struct ID3D11DeviceContext;
struct ID3D11Texture2D;

//----------------------------------------------------------------------------
// Structure to hold a captured frame with its position
//----------------------------------------------------------------------------
struct PanoramaFrame
{
    std::vector<BYTE> pixels;       // BGRA pixel data
    int width;                      // Frame width
    int height;                     // Frame height
    int relativeX;                  // X position relative to first frame
    int relativeY;                  // Y position relative to first frame
    LONGLONG timestamp;             // Capture timestamp
};

//----------------------------------------------------------------------------
// Structure for scroll offset detection result
//----------------------------------------------------------------------------
struct ScrollOffset
{
    int dx;             // Horizontal scroll amount
    int dy;             // Vertical scroll amount
    double confidence;  // Match confidence (0.0 - 1.0)
    bool valid;         // Whether a valid match was found
};

//----------------------------------------------------------------------------
// PanoramaCapture class - handles continuous capture and stitching
//----------------------------------------------------------------------------
class PanoramaCapture
{
public:
    PanoramaCapture();
    ~PanoramaCapture();

    // Start panorama capture mode with a selected rectangle
    // Returns true if capture started successfully
    bool Start(HWND ownerWindow, const RECT& captureRect);

    // Stop capture and return stitched result as HBITMAP
    // Caller is responsible for deleting the returned bitmap
    HBITMAP Stop();

    // Cancel capture without producing result
    void Cancel();

    // Check if capture is currently active
    bool IsCapturing() const { return m_capturing; }

    // Get the overlay window handle (for message routing)
    HWND GetOverlayWindow() const { return m_overlayWindow.get(); }

    // Get current frame count
    size_t GetFrameCount() const { return m_frames.size(); }

    // Force a frame capture (called by timer or manually)
    void CaptureFrame();

private:
    // Detect scroll direction and amount between two frames
    // Uses normalized cross-correlation for accurate matching
    ScrollOffset DetectScrollOffset(
        const std::vector<BYTE>& frame1,
        const std::vector<BYTE>& frame2,
        int width, int height);

    // Compute normalized cross-correlation between two image regions
    double ComputeNCC(
        const BYTE* img1, const BYTE* img2,
        int width, int height, int stride,
        int img1OffsetX, int img1OffsetY,
        int img2OffsetX, int img2OffsetY,
        int compareWidth, int compareHeight);

    // Check if two frames are significantly different
    bool FramesAreDifferent(
        const std::vector<BYTE>& frame1,
        const std::vector<BYTE>& frame2,
        int width, int height);

    // Stitch all captured frames into final panorama
    HBITMAP StitchFrames();

    // Blend a frame onto the canvas - only copy new (non-overlapping) content
    void BlendFrameOntoCanvas(
        BYTE* canvas, int canvasWidth, int canvasHeight,
        const BYTE* frame, int frameWidth, int frameHeight,
        int destX, int destY, int previousFrameBottom);

    // Capture screen region to byte array
    std::vector<BYTE> CaptureScreenRegion(const RECT& rect, int& outWidth, int& outHeight);

    // Create and manage the overlay window
    bool CreateOverlayWindow(HWND ownerWindow);
    static LRESULT CALLBACK OverlayWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleOverlayMessage(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    // Timer callback for periodic frame capture
    static void CALLBACK TimerCallback(HWND hwnd, UINT msg, UINT_PTR idTimer, DWORD dwTime);

    // Keyboard hook for ESC detection
    static LRESULT CALLBACK KeyboardHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    bool InstallKeyboardHook();
    void RemoveKeyboardHook();

private:
    // Capture state
    std::atomic<bool> m_capturing;
    RECT m_captureRect;
    HWND m_ownerWindow;
    wil::unique_hwnd m_overlayWindow;

    // Captured frames
    std::vector<PanoramaFrame> m_frames;
    std::vector<BYTE> m_previousFrame;
    int m_previousWidth;
    int m_previousHeight;

    // Accumulated scroll offsets
    int m_totalOffsetX;
    int m_totalOffsetY;

    // Timer for periodic capture
    UINT_PTR m_timerID;
    static const UINT CAPTURE_INTERVAL_MS = 100;  // Capture every 100ms

    // Detection parameters - reduced for performance
    static const int SEARCH_RANGE_Y = 150;        // Max vertical search range
    static const int SEARCH_RANGE_X = 30;         // Max horizontal search range
    static const int SEARCH_STEP = 8;             // Step size for search (larger = faster)
    static const int STRIP_HEIGHT = 40;           // Height of comparison strip
    static const int MIN_SCROLL_THRESHOLD = 5;    // Minimum pixels to consider as scroll
    static const double MATCH_THRESHOLD;          // NCC threshold for valid match
    static const double DIFFERENCE_THRESHOLD;     // Threshold for frame difference

    // Window class name
    static const wchar_t* OVERLAY_CLASS_NAME;

    // Instance pointer for static callbacks
    static PanoramaCapture* s_instance;

    // Keyboard hook handle
    HHOOK m_keyboardHook;
};

