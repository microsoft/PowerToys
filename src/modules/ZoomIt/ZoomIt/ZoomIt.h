//============================================================================
//
// Zoomit
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// Screen zoom and annotation tool.
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================
#pragma once

// Ignore getversion deprecation warning
#pragma warning( disable: 4996 )

typedef HRESULT (__stdcall * type_pEnableThemeDialogTexture)(
                HWND hwnd,
                DWORD dwFlags
                );
type_pEnableThemeDialogTexture    pEnableThemeDialogTexture;

// For testing anti-aliased bitmap stretching
#define SCALE_GDIPLUS		0
#define SCALE_HALFTONE		0

// sent in mouse message when coming from tablet pen
#define MI_WP_SIGNATURE		0xFF515700

#define ZOOM_LEVEL_MIN		1
#define ZOOM_LEVEL_INIT		2
#define ZOOM_LEVEL_STEP_IN	((float) 1.1)
#define ZOOM_LEVEL_STEP_OUT	((float) 0.8)
#define ZOOM_LEVEL_MAX		32
#define ZOOM_LEVEL_STEP_TIME	20

#define LIVEZOOM_MOVE_REGIONS	8

#define WIN7_VERSION		0x106
#define WIN10_VERSION		0x206

// Time that we'll cache live zoom window to avoid flicker
// of live zooming on Vista/ws2k8
#define LIVEZOOM_WINDOW_TIMEOUT	2*3600*1000

#define MAX_UNDO_HISTORY	32

#define PEN_WIDTH			5
#define MIN_PEN_WIDTH        2
#define MAX_PEN_WIDTH		40
#define MAX_LIVE_PEN_WIDTH    600

#define APPNAME		L"ZoomIt"
#define WM_USER_TRAY_ACTIVATE	WM_USER+100
#define WM_USER_TYPING_OFF		WM_USER+101
#define WM_USER_GET_ZOOM_LEVEL	WM_USER+102
#define WM_USER_GET_SOURCE_RECT	WM_USER+103
#define WM_USER_SET_ZOOM		WM_USER+104
#define WM_USER_STOP_RECORDING	WM_USER+105
#define WM_USER_SAVE_CURSOR		WM_USER+106
#define WM_USER_RESTORE_CURSOR	WM_USER+107
#define WM_USER_MAGNIFY_CURSOR	WM_USER+108
#define WM_USER_EXIT_MODE		WM_USER+109
#define WM_USER_RELOAD_SETTINGS	WM_USER+110

typedef struct _TYPED_KEY {
    RECT		rc;
    struct _TYPED_KEY *Next;	
} TYPED_KEY, *P_TYPED_KEY;

typedef struct _DRAW_UNDO {
    HDC			hDc;
    HBITMAP		hBitmap;
    struct _DRAW_UNDO *Next;
} DRAW_UNDO, *P_DRAW_UNDO;

typedef struct {
    TCHAR		TabTitle[64];
    HWND		hPage;
} OPTION_TABS, *P_OPTIONS_TABS;

#define COLOR_RED		RGB(255, 0, 0)
#define COLOR_GREEN		RGB(0, 255, 0)
#define COLOR_BLUE		RGB(0, 0, 255)
#define COLOR_ORANGE	RGB(255,128,0)
#define COLOR_YELLOW	RGB(255, 255, 0 )
#define COLOR_PINK		RGB(255,128,255)
#define COLOR_BLUR		RGB(112,112,112)

#define DRAW_RECTANGLE	1
#define DRAW_ELLIPSE	2
#define DRAW_LINE		3
#define DRAW_ARROW		4

#define SHALLOW_ZOOM    1
#define SHALLOW_DESTROY 2
#define LIVE_DRAW_ZOOM   3

#define PEN_COLOR_HIGHLIGHT(Pencolor)	((Pencolor >> 24) != 0xFF)
#define PEN_COLOR_BLUR(Pencolor)        ((Pencolor & 0x00FFFFFF) == COLOR_BLUR)

#define CURSOR_SAVE_MARGIN  4


typedef BOOL (__stdcall *type_pGetMonitorInfo)(
  HMONITOR hMonitor,  // handle to display monitor
  LPMONITORINFO lpmi  // display monitor information 
);

typedef HMONITOR (__stdcall *type_MonitorFromPoint)(
  POINT pt,      // point 
  DWORD dwFlags  // determine return value
);

typedef HRESULT (__stdcall *type_pSHAutoComplete)(
    HWND hwndEdit,
    DWORD dwFlags 
);

// DPI awareness
typedef BOOL (__stdcall *type_pSetProcessDPIAware)(void);

// Live zoom
typedef BOOL (__stdcall *type_pMagSetWindowSource)(HWND hwnd,
    RECT rect
);
typedef BOOL (__stdcall *type_pMagSetWindowTransform)(HWND hwnd,
    PMAGTRANSFORM pTransform
);
typedef BOOL(__stdcall* type_pMagSetFullscreenTransform)(
    float magLevel,
    int   xOffset,
    int   yOffset
);
typedef BOOL(__stdcall* type_pMagSetInputTransform)(
    BOOL         fEnabled,
    const LPRECT pRectSource,
    const LPRECT pRectDest
);
typedef BOOL (__stdcall *type_pMagShowSystemCursor)(
    BOOL fShowCursor
);
typedef BOOL(__stdcall *type_pMagSetWindowFilterList)(
    HWND  hwnd,
    DWORD dwFilterMode,
    int   count,
    HWND* pHWND
);
typedef BOOL(__stdcall* type_pMagSetLensUseBitmapSmoothing)(
    _In_ HWND, 
    _In_ BOOL
);
typedef BOOL(__stdcall* type_MagSetFullscreenUseBitmapSmoothing)(
    BOOL fUseBitmapSmoothing
);
typedef BOOL(__stdcall* type_pMagInitialize)(VOID);

typedef BOOL(__stdcall *type_pGetPointerType)(
    _In_   UINT32 pointerId,
    _Out_  POINTER_INPUT_TYPE *pointerType
    );

typedef BOOL(__stdcall *type_pGetPointerPenInfo)(
    _In_   UINT32 pointerId,
    _Out_  POINTER_PEN_INFO *penInfo
    );

typedef HRESULT (__stdcall *type_pDwmIsCompositionEnabled)(          
    BOOL *pfEnabled
);

// opacity
typedef BOOL (__stdcall *type_pSetLayeredWindowAttributes)(
  HWND hwnd,           // handle to the layered window
  COLORREF crKey,      // specifies the color key
  BYTE bAlpha,         // value for the blend function
  DWORD dwFlags        // action
);

// Presentation mode check
typedef HRESULT (__stdcall *type_pSHQueryUserNotificationState)(          
    QUERY_USER_NOTIFICATION_STATE *pQueryUserNotificationState
);

typedef BOOL (__stdcall *type_pSystemParametersInfoForDpi)(
    UINT  uiAction,
    UINT  uiParam,
    PVOID pvParam,
    UINT  fWinIni,
    UINT  dpi
);

typedef UINT (__stdcall *type_pGetDpiForWindow)(
    HWND hwnd
);

class ComputerGraphicsInit
{
    ULONG_PTR	m_Token;
public:
    ComputerGraphicsInit()
    {
        Gdiplus::GdiplusStartupOutput	startupOut;
        Gdiplus::GdiplusStartupInput	startupIn;
        Gdiplus::GdiplusStartup( &m_Token, &startupIn, &startupOut );
    }
    ~ComputerGraphicsInit()
    {
        Gdiplus::GdiplusShutdown( m_Token );
    }
};

// Direct3D
typedef HRESULT (__stdcall *type_pCreateDirect3D11DeviceFromDXGIDevice)(
    IDXGIDevice		*dxgiDevice,
    IInspectable	**graphicsDevice
);
typedef HRESULT (__stdcall *type_pCreateDirect3D11SurfaceFromDXGISurface)(
    IDXGISurface	*dxgiSurface,
    IInspectable	**graphicsSurface
);
typedef HRESULT (__stdcall *type_pD3D11CreateDevice)(
    IDXGIAdapter			*pAdapter,
    D3D_DRIVER_TYPE			DriverType,
    HMODULE					Software,
    UINT					Flags,
    const D3D_FEATURE_LEVEL	*pFeatureLevels,
    UINT					FeatureLevels,
    UINT					SDKVersion,
    ID3D11Device			**ppDevice,
    D3D_FEATURE_LEVEL		*pFeatureLevel,
    ID3D11DeviceContext		**ppImmediateContext
);
