#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void SetConfigInvalidJSON(const char* exceptionMessage);
    static void InitSetErrorLoadingFile(const char* exceptionMessage);
    static void PreviewHandlerEnabled(bool enabled, LPCWSTR previewHandlerName);
    static void PowerPreviewSettingsUpDateFailed(LPCWSTR SettingsName);
    static void Destroyed();
};
