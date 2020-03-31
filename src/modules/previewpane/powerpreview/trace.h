#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void SetConfigInvalidJSON(const char* exceptionMessage);
    static void InitSetErrorLoadingFile(const char* exceptionMessage);
    static void EnabledPowerPreview(bool enabled);
    static void PowerPreviewSettingsUpdated(LPCWSTR SettingsName, bool oldState, bool newState, bool globalState);
    static void PowerPreviewSettingsUpdateFailed(LPCWSTR SettingsName, bool oldState, bool newState, bool globalState);
    static void Destroyed();
};
