#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void PowerToyIsEnabled();
    static void PowerToyIsDisabled();
    static void Destroy();
    static void SetConfigInvalidJSON(const char* exceptionMessage);
    static void InitSetErrorLoadingFile(const char* exceptionMessage);
};