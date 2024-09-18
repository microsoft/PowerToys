//============================================================================
//
// Zoomit
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// Screen zoom and annotation tool.
//
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

#define ZOOMLEVEL_MIN		1
#define ZOOMLEVEL_INIT		2
#define ZOOMLEVEL_STEPIN	((float) 1.1)
#define ZOOMLEVEL_STEPOUT	((float) 0.8)
#define ZOOMLEVEL_MAX		32
#define ZOOMLEVEL_STEPTIME	20

#define LIVEZOOM_MOVEREGIONS	8

#define WIN7_VERSION		0x106
#define WIN10_VERSION		0x206

// Time that we'll cache live zoom window to avoid flicker
// of live zooming on Vista/ws2k8
#define LIVEZOOM_WINDOW_TIMEOUT	2*3600*1000

#define MAX_UNDO_HISTORY	32

#define PEN_WIDTH			5
#define MIN_PENWIDTH        2
#define MAX_PENWIDTH		40
#define MAX_LIVEPENWIDTH    600

#define APPNAME		L"ZoomIt"
#define WM_USER_TRAYACTIVATE	WM_USER+100
#define WM_USER_TYPINGOFF		WM_USER+101
#define WM_USER_GETZOOMLEVEL	WM_USER+102
#define WM_USER_GETSOURCERECT	WM_USER+103
#define WM_USER_SETZOOM			WM_USER+104
#define WM_USER_STOPRECORDING	WM_USER+105
#define WM_USER_SAVECURSOR		WM_USER+106
#define WM_USER_RESTORECURSOR   WM_USER+107
#define WM_USER_MAGNIFYCURSOR	WM_USER+108
#define WM_USER_EXITMODE		WM_USER+109
#define WM_USER_RELOADSETTINGS	WM_USER+110

typedef struct _TYPED_KEY {
	RECT		rc;
	struct _TYPED_KEY *Next;	
} TYPED_KEY, *PTYPED_KEY;

typedef struct _DRAW_UNDO {
	HDC			hDc;
	HBITMAP		hBitmap;
	struct _DRAW_UNDO *Next;
} DRAW_UNDO, *PDRAW_UNDO;

typedef struct {
	TCHAR		TabTitle[64];
	HWND		hPage;
} OPTION_TABS, *POPTIONS_TABS;

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

#define PEN_COLOR_HIGHLIGHT(Pencolor)	(Pencolor >> 24) != 0xFF


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
typedef BOOL (__stdcall *type_pMagInitialize)(VOID);

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
	QUERY_USER_NOTIFICATION_STATE *pquns
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

class CGraphicsInit
{
	ULONG_PTR	m_Token;
public:
	CGraphicsInit()
	{
		Gdiplus::GdiplusStartupOutput	startupOut;
		Gdiplus::GdiplusStartupInput	startupIn;
		Gdiplus::GdiplusStartup( &m_Token, &startupIn, &startupOut );
	}
	~CGraphicsInit()
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
	IDXGISurface	*dgxiSurface,
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
