#pragma once

const float MEDIA_FILE_DISPLAY_PERCENTAGE = 0.75f;      // Media file preview can only allocate 75% of the screen space
const float NON_MEDIA_FILE_DISPLAY_PERCENTAGE = 0.50f;  // non media file preview can only allocate 50% of the screen space
const float DEFAULT_DPI = 96.f;                         // Default DPI that maps image resolution directly to screen resolution

const float ICON_DISPLAY_PERCENTAGE_W = 0.40f;  // icon preview can only allocate 40% of the screen width
const float ICON_DISPLAY_PERCENTAGE_H = 0.50f;  // icon preview can only allocate 50% of the screen height

const SIZE MIN_WINDOW_SIZE = { 500, 500 };

const SIZE SMALL = { 32, 32 };
const SIZE MEDIUM = { 96, 96 };
const SIZE LARGE = { 256, 256 };
const SIZE EXTRALARGE = { 1024, 1024 };

const DWORD WindowStyle = (WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SIZEBOX);

const LPCWSTR IPREVIEWHANDLER_CLSID = L"{8895b1c6-b41f-4c1c-a562-0d564250836f}";

// From OS Repo
typedef enum {
    WCA_UNDEFINED = 0,
    WCA_NCRENDERING_ENABLED = 1,
    WCA_NCRENDERING_POLICY = 2,
    WCA_TRANSITIONS_FORCEDISABLED = 3,
    WCA_ALLOW_NCPAINT = 4,
    WCA_CAPTION_BUTTON_BOUNDS = 5,
    WCA_NONCLIENT_RTL_LAYOUT = 6,
    WCA_FORCE_ICONIC_REPRESENTATION = 7,
    WCA_EXTENDED_FRAME_BOUNDS = 8,
    WCA_HAS_ICONIC_BITMAP = 9,
    WCA_THEME_ATTRIBUTES = 10,
    WCA_NCRENDERING_EXILED = 11,
    WCA_NCADORNMENTINFO = 12,
    WCA_EXCLUDED_FROM_LIVEPREVIEW = 13,
    WCA_VIDEO_OVERLAY_ACTIVE = 14,
    WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 15,
    WCA_DISALLOW_PEEK = 16,
    WCA_CLOAK = 17,
    WCA_CLOAKED = 18,
    WCA_ACCENT_POLICY = 19,
    WCA_FREEZE_REPRESENTATION = 20,
    WCA_EVER_UNCLOAKED = 21,
    WCA_VISUAL_OWNER = 22,
    WCA_HOLOGRAPHIC = 23,
    WCA_EXCLUDED_FROM_DDA = 24,
    WCA_PASSIVEUPDATEMODE = 25,
    WCA_USEDARKMODECOLORS = 26,
    WCA_LAST,
} WINDOWCOMPOSITIONATTRIB;

typedef struct
{
    WCHAR filename[MAX_PATH];
    WCHAR fullPath[MAX_PATH];
    WCHAR extension[MAX_PATH];
    WCHAR fileTypeName[80];
    uintmax_t size; // in bytes
    time_t lastModified;
    bool isFolder;
    int fileCount;
} FileInfo;

typedef struct
{
    WINDOWCOMPOSITIONATTRIB Attrib;
    PVOID pvData;
    SIZE_T cbData;
} WINDOWCOMPOSITIONATTRIBDATA;

using fnSetWindowCompositionAttribute = BOOL (WINAPI*)(HWND hWnd, WINDOWCOMPOSITIONATTRIBDATA*);
fnSetWindowCompositionAttribute _SetWindowCompositionAttribute = nullptr;

using fnShouldAppsUseDarkMode = bool (WINAPI*)();
fnShouldAppsUseDarkMode _ShouldAppsUseDarkMode = nullptr;

using fnRefreshImmersiveColorPolicyState = void (WINAPI*)();
fnRefreshImmersiveColorPolicyState _RefreshImmersiveColorPolicyState = nullptr;

class App
{
    public:
        App();
        ~App();

        HRESULT Initialize(HINSTANCE hInstance);

        BOOL inline IsLoaded() { return IsThumbnailLoaded() || IsFullImageLoaded(); }
        BOOL inline IsThumbnailLoaded() { return m_pIWICBitmap != nullptr; }        
        BOOL inline IsFullImageLoaded() { return m_pIWICBitmap != nullptr; }

        UINT inline GetBitmapWidth() { return m_bitmapWidth; }
        UINT inline GetBitmapHeight() { return m_bitmapHeight; }

        HWND inline GetWindowHandle() { return m_hWndPreview; }

        LPCWSTR inline GetFilePathAt(UINT i) { return m_fileList[i].fullPath; }
        FileInfo inline GetFileInfoAt(UINT i) { return m_fileList[i]; }

        void RefreshTitleBarThemeColor(HWND hWnd);
        
    private:
        HRESULT InitializeTheme();
        HRESULT CreateAppWindow(HINSTANCE hInstance);
        HRESULT CreateDeviceResources();
        HRESULT InitializeResources();
        HRESULT LoadThumbnail(__RPC__deref_out_opt HBITMAP* hBitmap);
        HRESULT OpenFile(LPCWSTR filename);
        HRESULT ParseFileNames();
        HRESULT PrepareImage();
        HRESULT RedrawWindow();
        HRESULT UpdateWindowProperties();
        void UpdateWindowSize();
        HRESULT TryPreviewLoad(LPCWSTR filename);
        HRESULT TryLaunchDefaultHandler();

        RECT CalculateLetterboxRect(RECT const& client, RECT const& content, bool bCenter, bool scaleContent,
            int contentOffsetX, int contentOffsetY);
        RECT ReduceRect(RECT const& rect, float percentage);

        LRESULT OnPaint();
        void PaintFileInfo(HDC hdc, RECT const& client);

        void NavigateToFile(UINT i);

        void CleanUp();
      
        LRESULT WndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
        static LRESULT CALLBACK s_WndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

        FileInfo ParseFileInfo(std::filesystem::directory_entry entry);
        std::wstring FormatSize(uintmax_t size);

    private:
        // handle to the main/preview window
        HWND                            m_hWndPreview;

        CComPtr<IPreviewHandler>        m_pIPreviewHandler;

        CComPtr<IWICImagingFactory>     m_pIWICFactory;
        CComPtr<IWICBitmap>             m_pIWICBitmap;
        CComPtr<IShellItemImageFactory> m_pImageFactory;

        CComPtr<ID2D1Factory>           m_pD2DFactory;
        CComPtr<ID2D1HwndRenderTarget>  m_pRenderTarget;
        CComPtr<ID2D1Bitmap>            m_pD2DBitmap;

        std::vector<FileInfo>           m_fileList;

        RECT                            m_monitorRect;  // The full monitor rect
        RECT                            m_windowRect;   // The maximum window rect
        RECT                            m_contentRect;  // The rect that the content will be fit into

        UINT                            m_bitmapWidth;
        UINT                            m_bitmapHeight;

        UINT                            m_currentItem;

        HANDLE                          m_singleInstanceMutex;

        BOOL                            m_isIcon = FALSE;
        BOOL                            m_isPreviewLoaded = FALSE;
        BOOL                            m_isDarkModeSupported = TRUE;
};