#include "pch.h"
#include "../FileLocksmithExt/RuntimeRegistration.h"

// Update registration based on enabled state
EXTERN_C __declspec(dllexport) void UpdateFileLocksmithRegistrationWin10(bool enabled)
{
    if (enabled)
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        FileLocksmithRuntimeRegistration::EnsureRegistered();
#endif
    }
    else
    {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
        FileLocksmithRuntimeRegistration::Unregister();
#endif
    }
}
