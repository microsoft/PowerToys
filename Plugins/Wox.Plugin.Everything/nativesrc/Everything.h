
#ifndef _EVERYTHING_DLL_
#define _EVERYTHING_DLL_

#ifndef _INC_WINDOWS
#include <windows.h>
#endif

#define EVERYTHING_OK						0
#define EVERYTHING_ERROR_MEMORY				1
#define EVERYTHING_ERROR_IPC				2
#define EVERYTHING_ERROR_REGISTERCLASSEX	3
#define EVERYTHING_ERROR_CREATEWINDOW		4
#define EVERYTHING_ERROR_CREATETHREAD		5
#define EVERYTHING_ERROR_INVALIDINDEX		6
#define EVERYTHING_ERROR_INVALIDCALL		7

#ifndef EVERYTHINGAPI
#define EVERYTHINGAPI __stdcall
#endif

#ifndef EVERYTHINGUSERAPI
#define EVERYTHINGUSERAPI __declspec(dllimport)
#endif

// write search state
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetSearchW(LPCWSTR lpString);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetSearchA(LPCSTR lpString);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetMatchPath(BOOL bEnable);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetMatchCase(BOOL bEnable);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetMatchWholeWord(BOOL bEnable);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetRegex(BOOL bEnable);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetMax(DWORD dwMax);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetOffset(DWORD dwOffset);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetReplyWindow(HWND hWnd);
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SetReplyID(DWORD nId);

// read search state
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_GetMatchPath(VOID);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_GetMatchCase(VOID);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_GetMatchWholeWord(VOID);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_GetRegex(VOID);
EVERYTHINGUSERAPI DWORD EVERYTHINGAPI Everything_GetMax(VOID);
EVERYTHINGUSERAPI DWORD EVERYTHINGAPI Everything_GetOffset(VOID);
EVERYTHINGUSERAPI LPCSTR EVERYTHINGAPI Everything_GetSearchA(VOID);
EVERYTHINGUSERAPI LPCWSTR EVERYTHINGAPI Everything_GetSearchW(VOID);
EVERYTHINGUSERAPI DWORD EVERYTHINGAPI Everything_GetLastError(VOID);
EVERYTHINGUSERAPI HWND EVERYTHINGAPI Everything_GetReplyWindow(VOID);
EVERYTHINGUSERAPI DWORD EVERYTHINGAPI Everything_GetReplyID(VOID);

// execute query
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_QueryA(BOOL bWait);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_QueryW(BOOL bWait);

// query reply
BOOL EVERYTHINGAPI Everything_IsQueryReply(UINT message,WPARAM wParam,LPARAM lParam,DWORD nId);

// write result state
EVERYTHINGUSERAPI VOID EVERYTHINGAPI Everything_SortResultsByPath(VOID);

// read result state
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetNumFileResults(VOID);
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetNumFolderResults(VOID);
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetNumResults(VOID);
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetTotFileResults(VOID);
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetTotFolderResults(VOID);
EVERYTHINGUSERAPI int EVERYTHINGAPI Everything_GetTotResults(VOID);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_IsVolumeResult(int nIndex);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_IsFolderResult(int nIndex);
EVERYTHINGUSERAPI BOOL EVERYTHINGAPI Everything_IsFileResult(int nIndex);
EVERYTHINGUSERAPI LPCWSTR EVERYTHINGAPI Everything_GetResultFileNameW(int nIndex);
EVERYTHINGUSERAPI LPCSTR EVERYTHINGAPI Everything_GetResultFileNameA(int nIndex);
EVERYTHINGUSERAPI LPCWSTR EVERYTHINGAPI Everything_GetResultPathW(int nIndex);
EVERYTHINGUSERAPI LPCSTR EVERYTHINGAPI Everything_GetResultPathA(int nIndex);
EVERYTHINGUSERAPI int Everything_GetResultFullPathNameW(int nIndex,LPWSTR wbuf,int wbuf_size_in_wchars);
EVERYTHINGUSERAPI int Everything_GetResultFullPathNameA(int nIndex,LPSTR buf,int bufsize);
EVERYTHINGUSERAPI VOID Everything_Reset(VOID);

#ifdef UNICODE
#define Everything_SetSearch Everything_SetSearchW
#define Everything_GetSearch Everything_GetSearchW
#define Everything_Query Everything_QueryW
#define Everything_GetResultFileName Everything_GetResultFileNameW
#define Everything_GetResultPath Everything_GetResultPathW
#else
#define Everything_SetSearch Everything_SetSearchA
#define Everything_GetSearch Everything_GetSearchA
#define Everything_Query Everything_QueryA
#define Everything_GetResultFileName Everything_GetResultFileNameA
#define Everything_GetResultPath Everything_GetResultPathA
#endif


#endif

