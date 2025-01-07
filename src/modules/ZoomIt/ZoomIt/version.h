#pragma once
//============================================================================\
//
// Version
//
// File version information functions.
//
//============================================================================


//
// File version information
//
typedef struct {
	WORD	wLength;
	WORD	wValueLength;
	WORD	wType;
	WCHAR	szKey[16];
	WORD	Padding1;
	VS_FIXEDFILEINFO Value;
} VERSION_INFO, * P_VERSION_INFO;


//
// Version translation
//
typedef struct {
	WORD langID;			// language ID
	WORD charset;			// character set (code page)
} VERSION_TRANSLATION, * P_VERSION_TRANSLATION;

typedef VS_FIXEDFILEINFO* P_VS_FIXEDFILEINFO;

#define FILEINFOSIG 0xFEEF04BD


const TCHAR* GetVersionString(const VERSION_INFO* VersionInfo,
	const TCHAR* VersionString);

