
// Everything IPC

#ifndef _EVERYTHING_IPC_H_
#define _EVERYTHING_IPC_H_

// C
#ifdef __cplusplus
extern "C" {
#endif

// 1 byte packing for our varible sized structs
#pragma pack(push, 1)

// WM_USER (send to the taskbar notification window)
// SendMessage(FindWindow(EVERYTHING_IPC_WNDCLASS,0),WM_USER,EVERYTHING_IPC_*,lParam)
// version format: major.minor.revision.build 
// example: 1.1.4.309
#define EVERYTHING_IPC_GET_MAJOR_VERSION		0 // int major_version = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_GET_MAJOR_VERSION,0);
#define EVERYTHING_IPC_GET_MINOR_VERSION		1 // int minor_version = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_GET_MINOR_VERSION,0);
#define EVERYTHING_IPC_GET_REVISION				2 // int revision = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_GET_REVISION,0);
#define EVERYTHING_IPC_GET_BUILD_NUMBER			3 // int build = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_GET_BUILD,0);

// uninstall options
#define EVERYTHING_IPC_DELETE_START_MENU_SHORTCUTS		100 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_DELETE_START_MENU_SHORTCUTS,0);
#define EVERYTHING_IPC_DELETE_QUICK_LAUNCH_SHORTCUT		101 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_DELETE_QUICK_LAUNCH_SHORTCUT,0);
#define EVERYTHING_IPC_DELETE_DESKTOP_SHORTCUT			102 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_DELETE_DESKTOP_SHORTCUT,0);
#define EVERYTHING_IPC_DELETE_FOLDER_CONTEXT_MENU		103 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_DELETE_FOLDER_CONTEXT_MENU,0);
#define EVERYTHING_IPC_DELETE_RUN_ON_SYSTEM_STARTUP		104 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_DELETE_RUN_ON_SYSTEM_STARTUP,0);

// install options
#define EVERYTHING_IPC_CREATE_START_MENU_SHORTCUTS		200 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_CREATE_START_MENU_SHORTCUTS,0);
#define EVERYTHING_IPC_CREATE_QUICK_LAUNCH_SHORTCUT		201 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_CREATE_QUICK_LAUNCH_SHORTCUT,0);
#define EVERYTHING_IPC_CREATE_DESKTOP_SHORTCUT			202 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_CREATE_DESKTOP_SHORTCUT,0);
#define EVERYTHING_IPC_CREATE_FOLDER_CONTEXT_MENU		203 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_CREATE_FOLDER_CONTEXT_MENU,0);
#define EVERYTHING_IPC_CREATE_RUN_ON_SYSTEM_STARTUP		204 // SendMessage(hwnd,WM_USER,EVERYTHING_IPC_CREATE_RUN_ON_SYSTEM_STARTUP,0);

// get option status; 0 = no, 1 = yes, 2 = indeterminate (partially installed)
#define EVERYTHING_IPC_IS_START_MENU_SHORTCUTS			300 // int ret = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_IS_START_MENU_SHORTCUTS,0);
#define EVERYTHING_IPC_IS_QUICK_LAUNCH_SHORTCUT			301 // int ret = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_IS_QUICK_LAUNCH_SHORTCUT,0);
#define EVERYTHING_IPC_IS_DESKTOP_SHORTCUT				302 // int ret = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_IS_DESKTOP_SHORTCUT,0);
#define EVERYTHING_IPC_IS_FOLDER_CONTEXT_MENU			303 // int ret = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_IS_FOLDER_CONTEXT_MENU,0);
#define EVERYTHING_IPC_IS_RUN_ON_SYSTEM_STARTUP			304 // int ret = (int)SendMessage(hwnd,WM_USER,EVERYTHING_IPC_IS_RUN_ON_SYSTEM_STARTUP,0);

// find the everything window
#define EVERYTHING_IPC_WNDCLASS			TEXT("EVERYTHING_TASKBAR_NOTIFICATION")

// find a everything search window
#define EVERYTHING_IPC_SEARCH_WNDCLASS	TEXT("EVERYTHING")

// this global window message is sent to all top level windows when everything starts.
#define EVERYTHING_IPC_CREATED			TEXT("EVERYTHING_IPC_CREATED")

// search flags for querys
#define EVERYTHING_IPC_MATCHCASE		0x00000001	// match case
#define EVERYTHING_IPC_MATCHWHOLEWORD	0x00000002	// match whole word
#define EVERYTHING_IPC_MATCHPATH		0x00000004	// include paths in search
#define EVERYTHING_IPC_REGEX			0x00000008	// enable regex

// item flags
#define EVERYTHING_IPC_FOLDER			0x00000001	// The item is a folder. (its a file if not set)
#define EVERYTHING_IPC_DRIVE			0x00000002	// The folder is a drive. Path will be an empty string. 
													// (will also have the folder bit set)

// the WM_COPYDATA message for a query.
#define EVERYTHING_IPC_COPYDATAQUERYA	1
#define EVERYTHING_IPC_COPYDATAQUERYW	2

// all results
#define EVERYTHING_IPC_ALLRESULTS		0xFFFFFFFF // all results

// macro to get the filename of an item
#define EVERYTHING_IPC_ITEMFILENAMEA(list,item) (CHAR *)((CHAR *)(list) + ((EVERYTHING_IPC_ITEMA *)(item))->filename_offset)
#define EVERYTHING_IPC_ITEMFILENAMEW(list,item) (WCHAR *)((CHAR *)(list) + ((EVERYTHING_IPC_ITEMW *)(item))->filename_offset)

// macro to get the path of an item
#define EVERYTHING_IPC_ITEMPATHA(list,item) (CHAR *)((CHAR *)(list) + ((EVERYTHING_IPC_ITEMW *)(item))->path_offset)
#define EVERYTHING_IPC_ITEMPATHW(list,item) (WCHAR *)((CHAR *)(list) + ((EVERYTHING_IPC_ITEMW *)(item))->path_offset)

//
// Varible sized query struct sent to everything.
//
// sent in the form of a WM_COPYDAYA message with EVERYTHING_IPC_COPYDATAQUERY as the 
// dwData member in the COPYDATASTRUCT struct.
// set the lpData member of the COPYDATASTRUCT struct to point to your EVERYTHING_IPC_QUERY struct.
// set the cbData member of the COPYDATASTRUCT struct to the size of the 
// EVERYTHING_IPC_QUERY struct minus the size of a CHAR plus the length of the search string in bytes plus 
// one CHAR for the null terminator.
//
// NOTE: to determine the size of this structure use 
// ASCII: sizeof(EVERYTHING_IPC_QUERYA) - sizeof(CHAR) + strlen(search_string)*sizeof(CHAR) + sizeof(CHAR)
// UNICODE: sizeof(EVERYTHING_IPC_QUERYW) - sizeof(WCHAR) + unicode_length_in_wchars(search_string)*sizeof(WCHAR) + sizeof(WCHAR)
//
// NOTE: Everything will only do one query per window.
// Sending another query when a query has not completed 
// will cancel the old query and start the new one. 
//
// Everything will send the results to the reply_hwnd in the form of a 
// WM_COPYDAYA message with the dwData value you specify.
// 
// Everything will return TRUE if successful.
// returns FALSE if not supported.
//
// If you query with EVERYTHING_IPC_COPYDATAQUERYW, the results sent from Everything will be Unicode.
//

typedef struct EVERYTHING_IPC_QUERYW
{
	// the window that will receive the new results.
	INT32 reply_hwnd;
	
	// the value to set the dwData member in the COPYDATASTRUCT struct 
	// sent by Everything when the query is complete.
	INT32 reply_copydata_message;
	
	// search flags (see EVERYTHING_MATCHCASE | EVERYTHING_MATCHWHOLEWORD | EVERYTHING_MATCHPATH)
	INT32 search_flags; 
	
	// only return results after 'offset' results (0 to return the first result)
	// useful for scrollable lists
	INT32 offset; 
	
	// the number of results to return 
	// zero to return no results
	// EVERYTHING_IPC_ALLRESULTS to return ALL results
	INT32 max_results;

	// null terminated string. arbitrary sized search_string buffer.
	INT32 search_string[1];
	
}EVERYTHING_IPC_QUERYW;

// ASCII version
typedef struct EVERYTHING_IPC_QUERYA
{
	// the window that will receive the new results.
	INT32 reply_hwnd;
	
	// the value to set the dwData member in the COPYDATASTRUCT struct 
	// sent by Everything when the query is complete.
	INT32 reply_copydata_message;
	
	// search flags (see EVERYTHING_MATCHCASE | EVERYTHING_MATCHWHOLEWORD | EVERYTHING_MATCHPATH)
	INT32 search_flags; 
	
	// only return results after 'offset' results (0 to return the first result)
	// useful for scrollable lists
	INT32 offset; 
	
	// the number of results to return 
	// zero to return no results
	// EVERYTHING_IPC_ALLRESULTS to return ALL results
	INT32 max_results;

	// null terminated string. arbitrary sized search_string buffer.
	INT32 search_string[1];
	
}EVERYTHING_IPC_QUERYA;

//
// Varible sized result list struct received from Everything.
//
// Sent in the form of a WM_COPYDATA message to the hwnd specifed in the 
// EVERYTHING_IPC_QUERY struct.
// the dwData member of the COPYDATASTRUCT struct will match the sent
// reply_copydata_message member in the EVERYTHING_IPC_QUERY struct.
// 
// make a copy of the data before returning.
//
// return TRUE if you processed the WM_COPYDATA message.
//

typedef struct EVERYTHING_IPC_ITEMW
{
	// item flags
	DWORD flags;

	// The offset of the filename from the beginning of the list structure.
	// (wchar_t *)((char *)everything_list + everythinglist->name_offset)
	DWORD filename_offset;

	// The offset of the filename from the beginning of the list structure.
	// (wchar_t *)((char *)everything_list + everythinglist->path_offset)
	DWORD path_offset;
	
}EVERYTHING_IPC_ITEMW;

typedef struct EVERYTHING_IPC_ITEMA
{
	// item flags
	DWORD flags;

	// The offset of the filename from the beginning of the list structure.
	// (char *)((char *)everything_list + everythinglist->name_offset)
	DWORD filename_offset;

	// The offset of the filename from the beginning of the list structure.
	// (char *)((char *)everything_list + everythinglist->path_offset)
	DWORD path_offset;
	
}EVERYTHING_IPC_ITEMA;

typedef struct EVERYTHING_IPC_LISTW
{
	// the total number of folders found.
	DWORD totfolders;
	
	// the total number of files found.
	DWORD totfiles;
	
	// totfolders + totfiles
	DWORD totitems;
	
	// the number of folders available.
	DWORD numfolders;
	
	// the number of files available.
	DWORD numfiles;
	
	// the number of items available.
	DWORD numitems;

	// index offset of the first result in the item list.
	DWORD offset;
	
	// arbitrary sized item list. 
	// use numitems to determine the actual number of items available.
	EVERYTHING_IPC_ITEMW items[1];
	
}EVERYTHING_IPC_LISTW;

typedef struct EVERYTHING_IPC_LISTA
{
	// the total number of folders found.
	DWORD totfolders;
	
	// the total number of files found.
	DWORD totfiles;
	
	// totfolders + totfiles
	DWORD totitems;
	
	// the number of folders available.
	DWORD numfolders;
	
	// the number of files available.
	DWORD numfiles;
	
	// the number of items available.
	DWORD numitems;

	// index offset of the first result in the item list.
	DWORD offset;
	
	// arbitrary sized item list. 
	// use numitems to determine the actual number of items available.
	EVERYTHING_IPC_ITEMA items[1];
	
}EVERYTHING_IPC_LISTA;

#ifdef UNICODE
#define EVERYTHING_IPC_COPYDATAQUERY	EVERYTHING_IPC_COPYDATAQUERYW
#define EVERYTHING_IPC_ITEMFILENAME		EVERYTHING_IPC_ITEMFILENAMEW
#define EVERYTHING_IPC_ITEMPATH			EVERYTHING_IPC_ITEMPATHW
#define EVERYTHING_IPC_QUERY			EVERYTHING_IPC_QUERYW
#define EVERYTHING_IPC_ITEM				EVERYTHING_IPC_ITEMW
#define EVERYTHING_IPC_LIST				EVERYTHING_IPC_LISTW
#else
#define EVERYTHING_IPC_COPYDATAQUERY	EVERYTHING_IPC_COPYDATAQUERYA
#define EVERYTHING_IPC_ITEMFILENAME		EVERYTHING_IPC_ITEMFILENAMEA
#define EVERYTHING_IPC_ITEMPATH			EVERYTHING_IPC_ITEMPATHA
#define EVERYTHING_IPC_QUERY			EVERYTHING_IPC_QUERYA
#define EVERYTHING_IPC_ITEM				EVERYTHING_IPC_ITEMA
#define EVERYTHING_IPC_LIST				EVERYTHING_IPC_LISTA
#endif


// restore packing
#pragma pack(pop)

// end extern C
#ifdef __cplusplus
}
#endif

#endif // _EVERYTHING_H_

