######################################################################
#  This file should be kept compatible with Python 2.3, see PEP 291. #
######################################################################

# The most useful windows datatypes
from ctypes import *

BYTE = c_byte
WORD = c_ushort
DWORD = c_ulong

WCHAR = c_wchar
UINT = c_uint
INT = c_int

DOUBLE = c_double
FLOAT = c_float

BOOLEAN = BYTE
BOOL = c_long

from ctypes import _SimpleCData
class VARIANT_BOOL(_SimpleCData):
    _type_ = "v"
    def __repr__(self):
        return "%s(%r)" % (self.__class__.__name__, self.value)

ULONG = c_ulong
LONG = c_long

USHORT = c_ushort
SHORT = c_short

# in the windows header files, these are structures.
_LARGE_INTEGER = LARGE_INTEGER = c_longlong
_ULARGE_INTEGER = ULARGE_INTEGER = c_ulonglong

LPCOLESTR = LPOLESTR = OLESTR = c_wchar_p
LPCWSTR = LPWSTR = c_wchar_p
LPCSTR = LPSTR = c_char_p
LPCVOID = LPVOID = c_void_p

# WPARAM is defined as UINT_PTR (unsigned type)
# LPARAM is defined as LONG_PTR (signed type)
if sizeof(c_long) == sizeof(c_void_p):
    WPARAM = c_ulong
    LPARAM = c_long
elif sizeof(c_longlong) == sizeof(c_void_p):
    WPARAM = c_ulonglong
    LPARAM = c_longlong

ATOM = WORD
LANGID = WORD

COLORREF = DWORD
LGRPID = DWORD
LCTYPE = DWORD

LCID = DWORD

################################################################
# HANDLE types
HANDLE = c_void_p # in the header files: void *

HACCEL = HANDLE
HBITMAP = HANDLE
HBRUSH = HANDLE
HCOLORSPACE = HANDLE
HDC = HANDLE
HDESK = HANDLE
HDWP = HANDLE
HENHMETAFILE = HANDLE
HFONT = HANDLE
HGDIOBJ = HANDLE
HGLOBAL = HANDLE
HHOOK = HANDLE
HICON = HANDLE
HINSTANCE = HANDLE
HKEY = HANDLE
HKL = HANDLE
HLOCAL = HANDLE
HMENU = HANDLE
HMETAFILE = HANDLE
HMODULE = HANDLE
HMONITOR = HANDLE
HPALETTE = HANDLE
HPEN = HANDLE
HRGN = HANDLE
HRSRC = HANDLE
HSTR = HANDLE
HTASK = HANDLE
HWINSTA = HANDLE
HWND = HANDLE
SC_HANDLE = HANDLE
SERVICE_STATUS_HANDLE = HANDLE

################################################################
# Some important structure definitions

class RECT(Structure):
    _fields_ = [("left", c_long),
                ("top", c_long),
                ("right", c_long),
                ("bottom", c_long)]
tagRECT = _RECTL = RECTL = RECT

class _SMALL_RECT(Structure):
    _fields_ = [('Left', c_short),
                ('Top', c_short),
                ('Right', c_short),
                ('Bottom', c_short)]
SMALL_RECT = _SMALL_RECT

class _COORD(Structure):
    _fields_ = [('X', c_short),
                ('Y', c_short)]

class POINT(Structure):
    _fields_ = [("x", c_long),
                ("y", c_long)]
tagPOINT = _POINTL = POINTL = POINT

class SIZE(Structure):
    _fields_ = [("cx", c_long),
                ("cy", c_long)]
tagSIZE = SIZEL = SIZE

def RGB(red, green, blue):
    return red + (green << 8) + (blue << 16)

class FILETIME(Structure):
    _fields_ = [("dwLowDateTime", DWORD),
                ("dwHighDateTime", DWORD)]
_FILETIME = FILETIME

class MSG(Structure):
    _fields_ = [("hWnd", HWND),
                ("message", c_uint),
                ("wParam", WPARAM),
                ("lParam", LPARAM),
                ("time", DWORD),
                ("pt", POINT)]
tagMSG = MSG
MAX_PATH = 260

class WIN32_FIND_DATAA(Structure):
    _fields_ = [("dwFileAttributes", DWORD),
                ("ftCreationTime", FILETIME),
                ("ftLastAccessTime", FILETIME),
                ("ftLastWriteTime", FILETIME),
                ("nFileSizeHigh", DWORD),
                ("nFileSizeLow", DWORD),
                ("dwReserved0", DWORD),
                ("dwReserved1", DWORD),
                ("cFileName", c_char * MAX_PATH),
                ("cAlternateFileName", c_char * 14)]

class WIN32_FIND_DATAW(Structure):
    _fields_ = [("dwFileAttributes", DWORD),
                ("ftCreationTime", FILETIME),
                ("ftLastAccessTime", FILETIME),
                ("ftLastWriteTime", FILETIME),
                ("nFileSizeHigh", DWORD),
                ("nFileSizeLow", DWORD),
                ("dwReserved0", DWORD),
                ("dwReserved1", DWORD),
                ("cFileName", c_wchar * MAX_PATH),
                ("cAlternateFileName", c_wchar * 14)]

__all__ = ['ATOM', 'BOOL', 'BOOLEAN', 'BYTE', 'COLORREF', 'DOUBLE', 'DWORD',
           'FILETIME', 'FLOAT', 'HACCEL', 'HANDLE', 'HBITMAP', 'HBRUSH',
           'HCOLORSPACE', 'HDC', 'HDESK', 'HDWP', 'HENHMETAFILE', 'HFONT',
           'HGDIOBJ', 'HGLOBAL', 'HHOOK', 'HICON', 'HINSTANCE', 'HKEY',
           'HKL', 'HLOCAL', 'HMENU', 'HMETAFILE', 'HMODULE', 'HMONITOR',
           'HPALETTE', 'HPEN', 'HRGN', 'HRSRC', 'HSTR', 'HTASK', 'HWINSTA',
           'HWND', 'INT', 'LANGID', 'LARGE_INTEGER', 'LCID', 'LCTYPE',
           'LGRPID', 'LONG', 'LPARAM', 'LPCOLESTR', 'LPCSTR', 'LPCVOID',
           'LPCWSTR', 'LPOLESTR', 'LPSTR', 'LPVOID', 'LPWSTR', 'MAX_PATH',
           'MSG', 'OLESTR', 'POINT', 'POINTL', 'RECT', 'RECTL', 'RGB',
           'SC_HANDLE', 'SERVICE_STATUS_HANDLE', 'SHORT', 'SIZE', 'SIZEL',
           'SMALL_RECT', 'UINT', 'ULARGE_INTEGER', 'ULONG', 'USHORT',
           'VARIANT_BOOL', 'WCHAR', 'WIN32_FIND_DATAA', 'WIN32_FIND_DATAW',
           'WORD', 'WPARAM', '_COORD', '_FILETIME', '_LARGE_INTEGER',
           '_POINTL', '_RECTL', '_SMALL_RECT', '_ULARGE_INTEGER', 'tagMSG',
           'tagPOINT', 'tagRECT', 'tagSIZE']
