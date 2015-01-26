
// disable warnings
#pragma warning(disable : 4996) // deprecation

#define EVERYTHINGUSERAPI __declspec(dllexport)

// include
#include "Everything.h"
#include "Everything_IPC.h"

// return copydata code
#define _EVERYTHING_COPYDATA_QUERYCOMPLETEA	0
#define _EVERYTHING_COPYDATA_QUERYCOMPLETEW	1

// internal state
static BOOL _Everything_MatchPath = FALSE;
static BOOL _Everything_MatchCase = FALSE;
static BOOL _Everything_MatchWholeWord = FALSE;
static BOOL _Everything_Regex = FALSE;
static DWORD _Everything_LastError = FALSE;
static DWORD _Everything_Max = EVERYTHING_IPC_ALLRESULTS;
static DWORD _Everything_Offset = 0;
static BOOL _Everything_IsUnicodeQuery = FALSE;
static BOOL _Everything_IsUnicodeSearch = FALSE;
static LPVOID _Everything_Search = NULL; // wchar or char
static LPVOID _Everything_List = NULL; // EVERYTHING_IPC_LISTW or EVERYTHING_IPC_LISTA
static volatile BOOL _Everything_Initialized = FALSE;
static volatile LONG _Everything_InterlockedCount = 0;
static CRITICAL_SECTION _Everything_cs;
static HWND _Everything_ReplyWindow = 0;
static DWORD _Everything_ReplyID = 0;

static VOID _Everything_Initialize(VOID)
{
	if (!_Everything_Initialized)
	{	
		if (InterlockedIncrement(&_Everything_InterlockedCount) == 1)
		{
			// do the initialization..
			InitializeCriticalSection(&_Everything_cs);
			
			_Everything_Initialized = 1;
		}
		else
		{
			// wait for initialization..
			while (!_Everything_Initialized) Sleep(0);
		}
	}
}

static VOID _Everything_Lock(VOID)
{
	_Everything_Initialize();
	
	EnterCriticalSection(&_Everything_cs);
}

static VOID _Everything_Unlock(VOID)
{
	LeaveCriticalSection(&_Everything_cs);
}

// aVOID other libs
static int _Everything_StringLengthA(LPCSTR start)
{
	register LPCSTR s;
	
	s = start;
	
	while(*s)
	{
		s++;
	}
	
	return (int)(s-start);
}

static int _Everything_StringLengthW(LPCWSTR start)
{
	register LPCWSTR s;
	
	s = start;
	
	while(*s)
	{
		s++;
	}
	
	return (int)(s-start);
}

VOID EVERYTHINGAPI Everything_SetSearchW(LPCWSTR lpString)
{
	int len;
	
	_Everything_Lock();
	
	if (_Everything_Search) HeapFree(GetProcessHeap(),0,_Everything_Search);
	
	len = _Everything_StringLengthW(lpString) + 1;

	_Everything_Search = HeapAlloc(GetProcessHeap(),0,len*sizeof(wchar_t));
	if (_Everything_Search)
	{
		CopyMemory(_Everything_Search,lpString,len*sizeof(wchar_t));
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_MEMORY;
	}
	
	_Everything_IsUnicodeSearch = 1;
	
	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetSearchA(LPCSTR lpString)
{
	int size;
	
	_Everything_Lock();
	
	if (_Everything_Search) HeapFree(GetProcessHeap(),0,_Everything_Search);
	
	size = _Everything_StringLengthA(lpString) + 1;

	_Everything_Search = (LPWSTR )HeapAlloc(GetProcessHeap(),0,size);
	if (_Everything_Search)
	{
		CopyMemory(_Everything_Search,lpString,size);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_MEMORY;
	}

	_Everything_IsUnicodeSearch = 0;

	_Everything_Unlock();
}

LPCSTR EVERYTHINGAPI Everything_GetSearchA(VOID)
{
	LPCSTR ret;
	
	_Everything_Lock();
	
	if (_Everything_Search)
	{
		if (_Everything_IsUnicodeSearch)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;
			
			ret = NULL;
		}
		else
		{
			ret = (LPCSTR)_Everything_Search;
		}
	}
	else
	{
		ret = "";
	}

	_Everything_Unlock();

	return ret;
}

LPCWSTR EVERYTHINGAPI Everything_GetSearchW(VOID)
{
	LPCWSTR ret;
	
	_Everything_Lock();

	if (_Everything_Search)
	{
		if (!_Everything_IsUnicodeSearch)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;
			
			ret = NULL;
		}
		else
		{
			ret = (LPCWSTR)_Everything_Search;
		}
	}
	else
	{
		ret = L"";
	}
	
	_Everything_Unlock();

	return ret;
}

VOID EVERYTHINGAPI Everything_SetMatchPath(BOOL bEnable)
{
	_Everything_Lock();

	_Everything_MatchPath = bEnable;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetMatchCase(BOOL bEnable)
{
	_Everything_Lock();

	_Everything_MatchCase = bEnable;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetMatchWholeWord(BOOL bEnable)
{
	_Everything_Lock();

	_Everything_MatchWholeWord = bEnable;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetRegex(BOOL bEnable)
{
	_Everything_Lock();

	_Everything_Regex = bEnable;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetMax(DWORD dwMax)
{
	_Everything_Lock();

	_Everything_Max = dwMax;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetOffset(DWORD dwOffset)
{
	_Everything_Lock();

	_Everything_Offset = dwOffset;

	_Everything_Unlock();
}

VOID EVERYTHINGAPI Everything_SetReplyWindow(HWND hWnd)
{
	_Everything_Lock();

	_Everything_ReplyWindow = hWnd;

	_Everything_Unlock();
}
	
VOID EVERYTHINGAPI Everything_SetReplyID(DWORD nId)
{
	_Everything_Lock();

	_Everything_ReplyID = nId;

	_Everything_Unlock();
}
	
BOOL EVERYTHINGAPI Everything_GetMatchPath(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_MatchPath;

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_GetMatchCase(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_MatchCase;

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_GetMatchWholeWord(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_MatchWholeWord;

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_GetRegex(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_Regex;

	_Everything_Unlock();
	
	return ret;
}

DWORD EVERYTHINGAPI Everything_GetMax(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_Max;

	_Everything_Unlock();
	
	return ret;
}

DWORD EVERYTHINGAPI Everything_GetOffset(VOID)
{
	BOOL ret;
	
	_Everything_Lock();
	
	ret = _Everything_Offset;

	_Everything_Unlock();
	
	return ret;
}
	
HWND EVERYTHINGAPI Everything_GetReplyWindow(VOID)
{
	HWND ret;
	
	_Everything_Lock();

	ret = _Everything_ReplyWindow;

	_Everything_Unlock();
	
	return ret;
}
	
DWORD EVERYTHINGAPI Everything_GetReplyID(VOID)
{
	DWORD ret;
	
	_Everything_Lock();

	ret = _Everything_ReplyID;

	_Everything_Unlock();
	
	return ret;
}
	
// custom window proc
static LRESULT EVERYTHINGAPI _Everything_window_proc(HWND hwnd,UINT msg,WPARAM wParam,LPARAM lParam)
{
	switch(msg)
	{
		case WM_COPYDATA:
		{
			COPYDATASTRUCT *cds = (COPYDATASTRUCT *)lParam;
			
			switch(cds->dwData)
			{
				case _EVERYTHING_COPYDATA_QUERYCOMPLETEA:

					if (!_Everything_IsUnicodeQuery)				
					{
						if (_Everything_List) HeapFree(GetProcessHeap(),0,_Everything_List);
						
						_Everything_List = (EVERYTHING_IPC_LISTW *)HeapAlloc(GetProcessHeap(),0,cds->cbData);
						
						if (_Everything_List)
						{
							CopyMemory(_Everything_List,cds->lpData,cds->cbData);
						}
						else
						{
							_Everything_LastError = EVERYTHING_ERROR_MEMORY;
						}
						
						PostQuitMessage(0);

						return TRUE;
					}
					
					break;

				case _EVERYTHING_COPYDATA_QUERYCOMPLETEW:
				
					if (_Everything_IsUnicodeQuery)				
					{
						if (_Everything_List) HeapFree(GetProcessHeap(),0,_Everything_List);
						
						_Everything_List = (EVERYTHING_IPC_LISTW *)HeapAlloc(GetProcessHeap(),0,cds->cbData);
						
						if (_Everything_List)
						{
							CopyMemory(_Everything_List,cds->lpData,cds->cbData);
						}
						else
						{
							_Everything_LastError = EVERYTHING_ERROR_MEMORY;
						}
						
						PostQuitMessage(0);

						return TRUE;
					}
					
					break;
			}
			
			break;
		}
	}
	
	return DefWindowProc(hwnd,msg,wParam,lParam);
}

// get the search length
static int _Everything_GetSearchLengthW(VOID)
{
	if (_Everything_Search)
	{
		if (_Everything_IsUnicodeSearch)
		{
			return _Everything_StringLengthW((LPCWSTR )_Everything_Search);
		}
		else
		{
			return MultiByteToWideChar(CP_ACP,0,(LPCSTR )_Everything_Search,-1,0,0);
		}
	}
	
	return 0;
}

// get the search length
static int _Everything_GetSearchLengthA(VOID)
{
	if (_Everything_Search)
	{
		if (_Everything_IsUnicodeSearch)
		{
			return WideCharToMultiByte(CP_ACP,0,(LPCWSTR )_Everything_Search,-1,0,0,0,0);
		}
		else
		{
			return _Everything_StringLengthA((LPCSTR )_Everything_Search);
		}
	}
	
	return 0;
}

// get the search length
static VOID _Everything_GetSearchTextW(LPWSTR wbuf)
{
	int wlen;
	
	if (_Everything_Search)
	{
		wlen = _Everything_GetSearchLengthW();
			
		if (_Everything_IsUnicodeSearch)
		{
			CopyMemory(wbuf,_Everything_Search,(wlen+1) * sizeof(wchar_t));
			
			return;
		}
		else
		{
			MultiByteToWideChar(CP_ACP,0,(LPCSTR )_Everything_Search,-1,wbuf,wlen+1);
			
			return;
		}
	}

	*wbuf = 0;
}

// get the search length
static VOID _Everything_GetSearchTextA(LPSTR buf)
{
	int len;
	
	if (_Everything_Search)
	{
		len = _Everything_GetSearchLengthW();
			
		if (_Everything_IsUnicodeSearch)
		{
			WideCharToMultiByte(CP_ACP,0,(LPCWSTR )_Everything_Search,-1,buf,len+1,0,0);
			
			return;
		}
		else
		{
			CopyMemory(buf,_Everything_Search,len+1);
			
			return;
		}
	}

	*buf = 0;
}

static DWORD EVERYTHINGAPI _Everything_thread_proc(VOID *param)
{
	HWND everything_hwnd;
	COPYDATASTRUCT cds;
	WNDCLASSEX wcex;
	HWND hwnd;
	MSG msg;
	int ret;
	int len;
	int size;
	union
	{
		EVERYTHING_IPC_QUERYA *queryA;
		EVERYTHING_IPC_QUERYW *queryW;
		VOID *query;
	}q;
	
	ZeroMemory(&wcex,sizeof(wcex));
	wcex.cbSize = sizeof(wcex);
	
	if (!GetClassInfoEx(GetModuleHandle(0),TEXT("EVERYTHING_DLL"),&wcex))
	{
		ZeroMemory(&wcex,sizeof(wcex));
		wcex.cbSize = sizeof(wcex);
		wcex.hInstance = GetModuleHandle(0);
		wcex.lpfnWndProc = _Everything_window_proc;
		wcex.lpszClassName = TEXT("EVERYTHING_DLL");
		
		if (!RegisterClassEx(&wcex))
		{
			_Everything_LastError = EVERYTHING_ERROR_REGISTERCLASSEX;
			
			return 0;
		}
	}
	
	hwnd = CreateWindow(
		TEXT("EVERYTHING_DLL"),
		TEXT(""),
		0,
		0,0,0,0,
		0,0,GetModuleHandle(0),0);
		
	if (hwnd)
	{
		everything_hwnd = FindWindow(EVERYTHING_IPC_WNDCLASS,0);
		if (everything_hwnd)
		{
			LPVOID a;

			if (param)
			{
				// unicode
				len = _Everything_GetSearchLengthW();
				
				size = sizeof(EVERYTHING_IPC_QUERYW) - sizeof(wchar_t) + len*sizeof(wchar_t) + sizeof(wchar_t);
			}
			else
			{
				// ansi
				len = _Everything_GetSearchLengthA();
				
				size = sizeof(EVERYTHING_IPC_QUERYA) - sizeof(char) + (len*sizeof(char)) + sizeof(char);
			}

			// alloc
			a = HeapAlloc(GetProcessHeap(),0,size);
			q.query = (EVERYTHING_IPC_QUERYW *)a;
			
			if (q.query)
			{
				if (param)
				{
					q.queryW->max_results = _Everything_Max;
					q.queryW->offset = _Everything_Offset;
					q.queryW->reply_copydata_message = _EVERYTHING_COPYDATA_QUERYCOMPLETEW;
					q.queryW->search_flags = (_Everything_Regex?EVERYTHING_IPC_REGEX:0) | (_Everything_MatchCase?EVERYTHING_IPC_MATCHCASE:0) | (_Everything_MatchWholeWord?EVERYTHING_IPC_MATCHWHOLEWORD:0) | (_Everything_MatchPath?EVERYTHING_IPC_MATCHPATH:0);
					q.queryW->reply_hwnd = (INT32) hwnd;

					_Everything_GetSearchTextW((LPWSTR) q.queryW->search_string);
				}
				else
				{
					q.queryA->max_results = _Everything_Max;
					q.queryA->offset = _Everything_Offset;
					q.queryA->reply_copydata_message = _EVERYTHING_COPYDATA_QUERYCOMPLETEA;
					q.queryA->search_flags = (_Everything_Regex?EVERYTHING_IPC_REGEX:0) | (_Everything_MatchCase?EVERYTHING_IPC_MATCHCASE:0) | (_Everything_MatchWholeWord?EVERYTHING_IPC_MATCHWHOLEWORD:0) | (_Everything_MatchPath?EVERYTHING_IPC_MATCHPATH:0);
					q.queryA->reply_hwnd = (INT32)hwnd;
				
					_Everything_GetSearchTextA((LPSTR) q.queryA->search_string);
				}

				cds.cbData = size;
				cds.dwData = param?EVERYTHING_IPC_COPYDATAQUERYW:EVERYTHING_IPC_COPYDATAQUERYA;
				cds.lpData = q.query;
			
				if (SendMessage(everything_hwnd,WM_COPYDATA,(WPARAM)hwnd,(LPARAM)&cds) == TRUE)
				{
					// message pump
	loop:

					WaitMessage();
					
					// update windows
					while(PeekMessage(&msg,NULL,0,0,0)) 
					{
						ret = (int)GetMessage(&msg,0,0,0);
						if (ret == -1) goto exit;
						if (!ret) goto exit;
						
						// let windows handle it.
						TranslateMessage(&msg);
						DispatchMessage(&msg);
					}			
					
					goto loop;

	exit:
					
					// get result from window.
					DestroyWindow(hwnd);
				}
				else
				{
					_Everything_LastError = EVERYTHING_ERROR_IPC;
				}
				
				// get result from window.
				HeapFree(GetProcessHeap(),0,q.query);
			}
			else
			{
				_Everything_LastError = EVERYTHING_ERROR_MEMORY;
			}
		}
		else
		{
			// the everything window was not found.
			// we can optionally RegisterWindowMessage("EVERYTHING_IPC_CREATED") and 
			// wait for Everything to post this message to all top level windows when its up and running.
			_Everything_LastError = EVERYTHING_ERROR_IPC;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_CREATEWINDOW;
	}

	return 0;
}

static BOOL EVERYTHINGAPI _Everything_Query(BOOL bUnicode)
{
	HANDLE hthread;
	DWORD threadid;
	VOID *param;
	
	// reset the error flag.
	_Everything_LastError = 0;
	
	if (bUnicode)
	{
		param = (VOID *)1;
	}
	else
	{
		param = 0;
	}
	
	_Everything_IsUnicodeQuery = bUnicode;
	
	hthread = CreateThread(0,0,_Everything_thread_proc,param,0,&threadid);
		
	if (hthread)
	{
		WaitForSingleObject(hthread,INFINITE);
		
		CloseHandle(hthread);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_CREATETHREAD;
	}
	
	return (_Everything_LastError == 0)?TRUE:FALSE;
}


BOOL _Everything_SendIPCQuery(BOOL bUnicode)
{
	HWND everything_hwnd;
	COPYDATASTRUCT cds;
	int ret;
	int len;
	int size;
	union
	{
		EVERYTHING_IPC_QUERYA *queryA;
		EVERYTHING_IPC_QUERYW *queryW;
		VOID *query;
	}q;
	
	_Everything_IsUnicodeQuery = bUnicode;
	
		// find the everything ipc window.
	everything_hwnd = FindWindow(EVERYTHING_IPC_WNDCLASS,0);
	if (everything_hwnd)
	{
		if (bUnicode)
		{
			// unicode
			len = _Everything_GetSearchLengthW();
			
			size = sizeof(EVERYTHING_IPC_QUERYW) - sizeof(wchar_t) + len*sizeof(wchar_t) + sizeof(wchar_t);
		}
		else
		{
			// ansi
			len = _Everything_GetSearchLengthA();
			
			size = sizeof(EVERYTHING_IPC_QUERYA) - sizeof(char) + (len*sizeof(char)) + sizeof(char);
		}

		// alloc
		q.query = (EVERYTHING_IPC_QUERYW *)HeapAlloc(GetProcessHeap(),0,size);
		
		if (q.query)
		{
			if (bUnicode)
			{
				q.queryW->max_results = _Everything_Max;
				q.queryW->offset = _Everything_Offset;
				q.queryW->reply_copydata_message = _Everything_ReplyID;
				q.queryW->search_flags = (_Everything_Regex?EVERYTHING_IPC_REGEX:0) | (_Everything_MatchCase?EVERYTHING_IPC_MATCHCASE:0) | (_Everything_MatchWholeWord?EVERYTHING_IPC_MATCHWHOLEWORD:0) | (_Everything_MatchPath?EVERYTHING_IPC_MATCHPATH:0);
				q.queryW->reply_hwnd = (INT32) _Everything_ReplyWindow;

				_Everything_GetSearchTextW((LPWSTR) q.queryW->search_string);
			}
			else
			{
				q.queryA->max_results = _Everything_Max;
				q.queryA->offset = _Everything_Offset;
				q.queryA->reply_copydata_message = _Everything_ReplyID;
				q.queryA->search_flags = (_Everything_Regex?EVERYTHING_IPC_REGEX:0) | (_Everything_MatchCase?EVERYTHING_IPC_MATCHCASE:0) | (_Everything_MatchWholeWord?EVERYTHING_IPC_MATCHWHOLEWORD:0) | (_Everything_MatchPath?EVERYTHING_IPC_MATCHPATH:0);
				q.queryA->reply_hwnd = (INT32) _Everything_ReplyWindow;
			
				_Everything_GetSearchTextA((LPSTR) q.queryA->search_string);
			}

			cds.cbData = size;
			cds.dwData = bUnicode?EVERYTHING_IPC_COPYDATAQUERYW:EVERYTHING_IPC_COPYDATAQUERYA;
			cds.lpData = q.query;
		
			if (SendMessage(everything_hwnd,WM_COPYDATA,(WPARAM)_Everything_ReplyWindow,(LPARAM)&cds))
			{
				// sucessful.
				ret = TRUE;
			}
			else
			{
				// no ipc
				_Everything_LastError = EVERYTHING_ERROR_IPC;
				
				ret = FALSE;
			}
			
			// get result from window.
			HeapFree(GetProcessHeap(),0,q.query);
		}
		else
		{
			_Everything_LastError = EVERYTHING_ERROR_MEMORY;
			
			ret = FALSE;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_IPC;
		
		ret = FALSE;
	}

	return ret;
}

BOOL EVERYTHINGAPI Everything_QueryA(BOOL bWait)
{
	BOOL ret;
	
	_Everything_Lock();

	if (bWait)	
	{
		ret = _Everything_Query(FALSE);
	}
	else
	{
		ret = _Everything_SendIPCQuery(FALSE);
	}

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_QueryW(BOOL bWait)
{
	BOOL ret;
	
	_Everything_Lock();
	
	if (bWait)	
	{
		ret = _Everything_Query(TRUE);
	}
	else
	{
		ret = _Everything_SendIPCQuery(TRUE);
	}

	_Everything_Unlock();
	
	return ret;
}

static int _Everything_CompareA(const VOID *a,const VOID *b)
{
	int i;
	
	i = stricmp(EVERYTHING_IPC_ITEMPATH(_Everything_List,a),EVERYTHING_IPC_ITEMPATH(_Everything_List,b));
	
	if (!i)
	{
		return stricmp(EVERYTHING_IPC_ITEMFILENAMEA(_Everything_List,a),EVERYTHING_IPC_ITEMFILENAMEA(_Everything_List,b));
	}
	else
	if (i > 0)
	{
		return 1;
	}
	else
	{
		return -1;
	}
}

static int _Everything_CompareW(const VOID *a,const VOID *b)
{
	int i;
	
	i = stricmp(EVERYTHING_IPC_ITEMPATH(_Everything_List,a),EVERYTHING_IPC_ITEMPATH(_Everything_List,b));
	
	if (!i)
	{
		return wcsicmp(EVERYTHING_IPC_ITEMFILENAMEW(_Everything_List,a),EVERYTHING_IPC_ITEMFILENAMEW(_Everything_List,b));
	}
	else
	if (i > 0)
	{
		return 1;
	}
	else
	{
		return -1;
	}
}

VOID EVERYTHINGAPI Everything_SortResultsByPath(VOID)
{
	_Everything_Lock();
	
	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			qsort(((EVERYTHING_IPC_LISTW *)_Everything_List)->items,((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems,sizeof(EVERYTHING_IPC_ITEMW),_Everything_CompareW);
		}
		else
		{
			qsort(((EVERYTHING_IPC_LISTA *)_Everything_List)->items,((EVERYTHING_IPC_LISTA *)_Everything_List)->numitems,sizeof(EVERYTHING_IPC_ITEMA),_Everything_CompareA);
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;
	}

	_Everything_Unlock();
}

DWORD EVERYTHINGAPI Everything_GetLastError(VOID)
{
	DWORD ret;
		
	_Everything_Lock();
	
	ret = _Everything_LastError;

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetNumFileResults(VOID)
{
	int ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->numfiles;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->numfiles;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetNumFolderResults(VOID)
{
	int ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->numfolders;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->numfolders;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetNumResults(VOID)
{
	int ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->numitems;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetTotFileResults(VOID)
{
	int ret;
	
	_Everything_Lock();
	
	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->totfiles;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->totfiles;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;
		
		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetTotFolderResults(VOID)
{
	int ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->totfolders;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->totfolders;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

int EVERYTHINGAPI Everything_GetTotResults(VOID)
{
	int ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->totitems;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->totitems;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = 0;
	}

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_IsVolumeResult(int nIndex)
{
	BOOL ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (nIndex >= Everything_GetNumResults())
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex].flags & EVERYTHING_IPC_DRIVE;
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex].flags & EVERYTHING_IPC_DRIVE;
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;
		
		ret = FALSE;
	}
	
exit:

	_Everything_Unlock();

	return ret;	
}

BOOL EVERYTHINGAPI Everything_IsFolderResult(int nIndex)
{
	BOOL ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (nIndex >= Everything_GetNumResults())
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (_Everything_IsUnicodeQuery)
		{
			ret = ((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex].flags & (EVERYTHING_IPC_DRIVE|EVERYTHING_IPC_FOLDER);
		}
		else
		{
			ret = ((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex].flags & (EVERYTHING_IPC_DRIVE|EVERYTHING_IPC_FOLDER);
		}
	}
	else
	{
		ret = FALSE;
	}
	
exit:	

	_Everything_Unlock();
	
	return ret;
}

BOOL EVERYTHINGAPI Everything_IsFileResult(int nIndex)
{
	BOOL ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (nIndex >= Everything_GetNumResults())
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = FALSE;
			
			goto exit;
		}
		
		if (_Everything_IsUnicodeQuery)
		{
			ret = !(((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex].flags & (EVERYTHING_IPC_DRIVE|EVERYTHING_IPC_FOLDER));
		}
		else
		{
			ret = !(((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex].flags & (EVERYTHING_IPC_DRIVE|EVERYTHING_IPC_FOLDER));
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = FALSE;
	}
	
exit:

	_Everything_Unlock();
	
	return ret;
}

LPCWSTR EVERYTHINGAPI Everything_GetResultFileNameW(int nIndex)
{
	LPCWSTR ret;

	_Everything_Lock();

	if ((_Everything_List) && (_Everything_IsUnicodeQuery))
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		ret = EVERYTHING_IPC_ITEMFILENAMEW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = NULL;
	}
	
exit:

	_Everything_Unlock();

	return ret;
}

LPCSTR EVERYTHINGAPI Everything_GetResultFileNameA(int nIndex)
{
	LPCSTR ret;
	
	_Everything_Lock();

	if ((_Everything_List) && (!_Everything_IsUnicodeQuery))
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTA *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		ret = EVERYTHING_IPC_ITEMFILENAMEA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = NULL;
	}
	
exit:

	_Everything_Unlock();
	
	return ret;
}

LPCWSTR EVERYTHINGAPI Everything_GetResultPathW(int nIndex)
{
	LPCWSTR ret;
	
	_Everything_Lock();
	
	if ((_Everything_List) && (_Everything_IsUnicodeQuery))
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		ret = EVERYTHING_IPC_ITEMPATHW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = NULL;
	}
	
exit:

	_Everything_Unlock();
	
	return ret;
}

LPCSTR EVERYTHINGAPI Everything_GetResultPathA(int nIndex)
{
	LPCSTR ret;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTA *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			ret = NULL;
			
			goto exit;
		}
		
		ret = EVERYTHING_IPC_ITEMPATHA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]);
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		ret = NULL;
	}

exit:
	
	_Everything_Unlock();
	
	return ret;
}

// max is in chars
static int _Everything_CopyW(LPWSTR buf,int bufmax,int catlen,LPCWSTR s)
{
	int wlen;

	if (buf)
	{
		buf += catlen;
		bufmax -= catlen;
	}
	
	wlen = _Everything_StringLengthW(s);
	if (!wlen) 
	{
		if (buf)
		{
			buf[wlen] = 0;
		}
	
		return catlen;
	}

	// terminate
	if (wlen > bufmax-1) wlen = bufmax-1;

	if (buf)
	{
		CopyMemory(buf,s,wlen*sizeof(wchar_t));

		buf[wlen] = 0;
	}
	
	return wlen + catlen;
}

static int _Everything_CopyA(LPSTR buf,int max,int catlen,LPCSTR s)
{
	int len;
	
	if (buf)
	{
		buf += catlen;
		max -= catlen;
	}
	
	len = _Everything_StringLengthA(s);
	if (!len) 
	{
		if (buf)
		{
			buf[len] = 0;
		}
	
		return catlen;
	}

	// terminate
	if (len > max-1) len = max-1;

	if (buf)
	{
		CopyMemory(buf,s,len*sizeof(char));

		buf[len] = 0;
	}
	
	return len + catlen;

}

// max is in chars
static int _Everything_CopyWFromA(LPWSTR buf,int bufmax,int catlen,LPCSTR s)
{
	int wlen;

	if (buf)
	{
		buf += catlen;
		bufmax -= catlen;
	}
	
	wlen = MultiByteToWideChar(CP_ACP,0,s,_Everything_StringLengthA(s),0,0);
	if (!wlen) 
	{
		if (buf)
		{
			buf[wlen] = 0;
		}
	
		return catlen;
	}

	// terminate
	if (wlen > bufmax-1) wlen = bufmax-1;

	if (buf)
	{
		MultiByteToWideChar(CP_ACP,0,s,_Everything_StringLengthA(s),buf,wlen);

		buf[wlen] = 0;
	}
	
	return wlen + catlen;
}

static int _Everything_CopyAFromW(LPSTR buf,int max,int catlen,LPCWSTR s)
{
	int len;
	
	if (buf)
	{
		buf += catlen;
		max -= catlen;
	}
	
	len = WideCharToMultiByte(CP_ACP,0,s,_Everything_StringLengthW(s),0,0,0,0);
	if (!len) 
	{
		if (buf)
		{
			buf[len] = 0;
		}
	
		return catlen;
	}

	// terminate
	if (len > max-1) len = max-1;

	if (buf)
	{
		WideCharToMultiByte(CP_ACP,0,s,_Everything_StringLengthW(s),buf,len,0,0);

		buf[len] = 0;
	}
	
	return len + catlen;

}

int EVERYTHINGUSERAPI Everything_GetResultFullPathNameW(int nIndex,LPWSTR wbuf,int wbuf_size_in_wchars)
{
	int len;
	
	_Everything_Lock();
	
	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,0,L"");
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,0,L"");
			
			goto exit;
		}

		len = 0;
		
		if (_Everything_IsUnicodeQuery)		
		{
			len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,len,EVERYTHING_IPC_ITEMPATHW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]));
		}
		else
		{
			len = _Everything_CopyWFromA(wbuf,wbuf_size_in_wchars,len,EVERYTHING_IPC_ITEMPATHA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]));
		}
			
		if (len)
		{
			len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,len,L"\\");
		}

		if (_Everything_IsUnicodeQuery)		
		{
			len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,len,EVERYTHING_IPC_ITEMFILENAMEW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]));
		}
		else
		{
			len = _Everything_CopyWFromA(wbuf,wbuf_size_in_wchars,len,EVERYTHING_IPC_ITEMFILENAMEA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]));
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		len = _Everything_CopyW(wbuf,wbuf_size_in_wchars,0,L"");
	}

exit:

	_Everything_Unlock();
	
	return len;
}

int EVERYTHINGUSERAPI Everything_GetResultFullPathNameA(int nIndex,LPSTR buf,int bufsize)
{
	int len;
	
	_Everything_Lock();

	if (_Everything_List)
	{
		if (nIndex < 0)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			len = _Everything_CopyA(buf,bufsize,0,"");
			
			goto exit;
		}
		
		if (nIndex >= (int)((EVERYTHING_IPC_LISTW *)_Everything_List)->numitems)
		{
			_Everything_LastError = EVERYTHING_ERROR_INVALIDINDEX;
			
			len = _Everything_CopyA(buf,bufsize,0,"");
			
			goto exit;
		}
		
		len = 0;
		
		if (_Everything_IsUnicodeQuery)		
		{
			len = _Everything_CopyAFromW(buf,bufsize,len,EVERYTHING_IPC_ITEMPATHW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]));
		}
		else
		{
			len = _Everything_CopyA(buf,bufsize,len,EVERYTHING_IPC_ITEMPATHA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]));
		}
		
		if (len)
		{
			len = _Everything_CopyA(buf,bufsize,len,"\\");
		}

		if (_Everything_IsUnicodeQuery)		
		{
			len = _Everything_CopyAFromW(buf,bufsize,len,EVERYTHING_IPC_ITEMFILENAMEW(_Everything_List,&((EVERYTHING_IPC_LISTW *)_Everything_List)->items[nIndex]));
		}
		else
		{
			len = _Everything_CopyA(buf,bufsize,len,EVERYTHING_IPC_ITEMFILENAMEA(_Everything_List,&((EVERYTHING_IPC_LISTA *)_Everything_List)->items[nIndex]));
		}
	}
	else
	{
		_Everything_LastError = EVERYTHING_ERROR_INVALIDCALL;

		len = _Everything_CopyA(buf,bufsize,0,"");
	}

exit:

	_Everything_Unlock();
	
	return len;
}

BOOL EVERYTHINGAPI Everything_IsQueryReply(UINT message,WPARAM wParam,LPARAM lParam,DWORD nId)
{
	if (message == WM_COPYDATA)
	{
		COPYDATASTRUCT *cds = (COPYDATASTRUCT *)lParam;
		
		if (cds)
		{
			if (cds->dwData == _Everything_ReplyID)
			{
				if (_Everything_IsUnicodeQuery)				
				{
					if (_Everything_List) HeapFree(GetProcessHeap(),0,_Everything_List);
					
					_Everything_List = (EVERYTHING_IPC_LISTW *)HeapAlloc(GetProcessHeap(),0,cds->cbData);
					
					if (_Everything_List)
					{
						CopyMemory(_Everything_List,cds->lpData,cds->cbData);
					}
					else
					{
						_Everything_LastError = EVERYTHING_ERROR_MEMORY;
					}
					
					return TRUE;
				}
				else
				{
					if (_Everything_List) HeapFree(GetProcessHeap(),0,_Everything_List);
					
					_Everything_List = (EVERYTHING_IPC_LISTW *)HeapAlloc(GetProcessHeap(),0,cds->cbData);
					
					if (_Everything_List)
					{
						CopyMemory(_Everything_List,cds->lpData,cds->cbData);
					}
					else
					{
						_Everything_LastError = EVERYTHING_ERROR_MEMORY;
					}

					return TRUE;
				}
			}
		}
	}
	
	return FALSE;
}

VOID EVERYTHINGUSERAPI Everything_Reset(VOID)
{
	_Everything_Lock();
	
	if (_Everything_Search)
	{
		HeapFree(GetProcessHeap(),0,_Everything_Search);
		
		_Everything_Search = 0;
	}
	
	if (_Everything_List)
	{
		HeapFree(GetProcessHeap(),0,_Everything_List);
		
		_Everything_List = 0;
	}

	// reset state
	_Everything_MatchPath = FALSE;
	_Everything_MatchCase = FALSE;
	_Everything_MatchWholeWord = FALSE;
	_Everything_Regex = FALSE;
	_Everything_LastError = FALSE;
	_Everything_Max = EVERYTHING_IPC_ALLRESULTS;
	_Everything_Offset = 0;
	_Everything_IsUnicodeQuery = FALSE;
	_Everything_IsUnicodeSearch = FALSE;

	_Everything_Unlock();
}

//VOID DestroyResultArray(VOID *)

// testing
/*
int main(int argc,char **argv)
{
	char buf[MAX_PATH];
	wchar_t wbuf[MAX_PATH];

	// set search	
//	Everything_SetSearchA("sonic");
	Everything_SetSearchW(L"sonic");

//	Everything_QueryA();
	Everything_QueryW(TRUE);

//	Everything_GetResultFullPathNameA(0,buf,sizeof(buf));
	Everything_GetResultFullPathNameW(0,wbuf,sizeof(wbuf)/sizeof(wchar_t));

//	MessageBoxA(0,buf,"result 1",MB_OK);
	MessageBoxW(0,wbuf,L"result 1",MB_OK);

//	MessageBoxA(0,resultA.cFileName,"result 1",MB_OK);
//	MessageBoxW(0,resultW.cFileName,L"result 1",MB_OK);
}
*/