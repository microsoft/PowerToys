#pragma once

#include <PowerRenameInterfaces.h>

bool DoRename(CComPtr<IPowerRenameRegEx>& spRenameRegEx, unsigned long& itemEnumIndex, CComPtr<IPowerRenameItem>& spItem);
