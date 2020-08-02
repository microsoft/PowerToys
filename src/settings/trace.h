#pragma once

class Trace
{
public:
    enum class SettingsInitErrorCause : int32_t
    {
        WebViewInitAsyncError,
        WebViewInitWinRTException,
        FailedToDropPrivileges,
    };

    static void SettingsInitError(const SettingsInitErrorCause error_cause);

    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
};
