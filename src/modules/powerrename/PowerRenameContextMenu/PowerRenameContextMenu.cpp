// PowerRenameContextMenu.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "framework.h"
#include "PowerRenameContextMenu.h"


// This is an example of an exported variable
POWERRENAMECONTEXTMENU_API int nPowerRenameContextMenu=0;

// This is an example of an exported function.
POWERRENAMECONTEXTMENU_API int fnPowerRenameContextMenu(void)
{
    return 0;
}

// This is the constructor of a class that has been exported.
CPowerRenameContextMenu::CPowerRenameContextMenu()
{
    return;
}
