// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the POWERRENAMECONTEXTMENU_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// POWERRENAMECONTEXTMENU_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef POWERRENAMECONTEXTMENU_EXPORTS
#define POWERRENAMECONTEXTMENU_API __declspec(dllexport)
#else
#define POWERRENAMECONTEXTMENU_API __declspec(dllimport)
#endif

// This class is exported from the dll
class POWERRENAMECONTEXTMENU_API CPowerRenameContextMenu {
public:
	CPowerRenameContextMenu(void);
	// TODO: add your methods here.
};

extern POWERRENAMECONTEXTMENU_API int nPowerRenameContextMenu;

POWERRENAMECONTEXTMENU_API int fnPowerRenameContextMenu(void);
