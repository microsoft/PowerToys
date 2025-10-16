#pragma once

#include <mfapi.h>
#include <mfidl.h>

#include "DLLProviderHelpers.h"

DECLARE_DLL_PROVIDER_BEGIN(mfplat)
DECLARE_DLL_FUNCTION(MFCreateAttributes)
DECLARE_DLL_PROVIDER_END

DECLARE_DLL_PROVIDER_BEGIN(mf)
DECLARE_DLL_FUNCTION(MFEnumDeviceSources)
DECLARE_DLL_PROVIDER_END
