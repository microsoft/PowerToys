#include "pch.h"
#include "winstore.h"

#include <appmodel.h>

bool running_as_packaged()
{
    UINT32 length = 0;
    const auto rc = GetPackageFamilyName(GetCurrentProcess(), &length, nullptr);
    return rc != APPMODEL_ERROR_NO_PACKAGE;
}
